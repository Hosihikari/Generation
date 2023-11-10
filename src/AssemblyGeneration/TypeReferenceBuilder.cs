using Hosihikari.NativeInterop.Generation;
using Hosihikari.NativeInterop.Unmanaged;
using Mono.Cecil;
using System.Reflection;
using Hosihikari.Generation.Generator;
using Mono.Cecil.Rocks;
using System.Text.RegularExpressions;
using static Hosihikari.Utils.OriginalData.Class;
using System.Diagnostics.CodeAnalysis;

namespace Hosihikari.Generation.AssemblyGeneration;

public static class TypeReferenceBuilder
{
    private static readonly Dictionary<string, Dictionary<string, Type>> predefinedTypes = new();

    private static readonly List<(Type, nint, nint)> typeReferenceProviders = new();

    static TypeReferenceBuilder()
    {
        var assemblies = new List<Assembly>()
        {
            typeof(Minecraft.Foundation.AABB).Assembly,
            typeof(NativeInterop.SymbolHelper).Assembly,
        };

        foreach (var assembly in assemblies)
        {
            foreach (var type in assembly.GetExportedTypes())
            {
                if (type.IsGenericTypeDefinition)
                    continue;

                Dictionary<string, Type> keyValues;
                var attribute = type.GetCustomAttribute<PredefinedTypeAttribute>();
                if (attribute is not null)
                {
                    if (predefinedTypes.ContainsKey(attribute.NativeTypeNamespace) is false)
                    {
                        keyValues = new();
                        predefinedTypes.Add(attribute.NativeTypeNamespace, keyValues);
                    }
                    else
                        keyValues = predefinedTypes[attribute.NativeTypeNamespace];

                    if (type.IsClass)
                    {
                        foreach (var @interface in type.GetInterfaces())
                            if (@interface.IsGenericType)
                                if (@interface.GetGenericTypeDefinition() == typeof(ICppInstance<>))
                                {
                                    keyValues.Add(string.IsNullOrWhiteSpace(attribute.NativeTypeName) ? type.Name : attribute.NativeTypeName, type);
                                    continue;
                                }
                    }
                    else if (type.IsEnum)
                        keyValues.Add(string.IsNullOrWhiteSpace(attribute.NativeTypeName) ? type.Name : attribute.NativeTypeName, type);
                    else if (type.IsValueType)
                        keyValues.Add(string.IsNullOrWhiteSpace(attribute.NativeTypeName) ? type.Name : attribute.NativeTypeName, type);
                }

                if (type != typeof(ITypeReferenceProvider) && type.IsAssignableTo(typeof(ITypeReferenceProvider)))
                {
                    var fptr1 = type.GetProperty(
                        nameof(ITypeReferenceProvider.Regex),
                        typeof(Regex))!
                        .GetMethod!
                        .MethodHandle
                        .GetFunctionPointer();

                    var fptr2 = type.GetMethod(
                        nameof(ITypeReferenceProvider.Matched))!
                        .MethodHandle
                        .GetFunctionPointer();

                    typeReferenceProviders.Add((type, fptr1, fptr2));
                }
            }
        }
    }

    public static TypeReference BuildFundamentalTypeReference(ModuleDefinition module, CppTypeNode node)
    {
        return node.FundamentalType! switch
        {
            CppFundamentalType.Void => module.ImportReference(typeof(void)),
            CppFundamentalType.Boolean => module.ImportReference(typeof(bool)),
            CppFundamentalType.Float => module.ImportReference(typeof(float)),
            CppFundamentalType.Double => module.ImportReference(typeof(double)),
            CppFundamentalType.WChar => module.ImportReference(typeof(char)),
            CppFundamentalType.SChar => module.ImportReference(typeof(sbyte)),
            CppFundamentalType.Int16 => module.ImportReference(typeof(short)),
            CppFundamentalType.Int32 => module.ImportReference(typeof(int)),
            CppFundamentalType.Int64 => module.ImportReference(typeof(long)),
            CppFundamentalType.Char => module.ImportReference(typeof(byte)),
            CppFundamentalType.UInt16 => module.ImportReference(typeof(ushort)),
            CppFundamentalType.UInt32 => module.ImportReference(typeof(uint)),
            CppFundamentalType.UInt64 => module.ImportReference(typeof(ulong)),
            _ => throw new InvalidOperationException(),
        };
    }

    public static bool TryGetPredefinedType(CppTypeNode node, bool isEnum, [NotNullWhen(true)] out Type? predefinedType)
    {
        predefinedType = null;

        var @namespace = string.Empty;
        if (node.Namespaces is not null)
            @namespace = string.Join("::", node.Namespaces);

        if (predefinedTypes.TryGetValue(@namespace, out var types))
        {
            if (types.TryGetValue(node.TypeIdentifierWithTemplateArgs ?? string.Empty, out var type))
            {
                if (isEnum && type.IsEnum is false)
                    return false;

                predefinedType = type;

                return true;
            }
        }

        foreach (var (_, get_Regex_methodFptr, Matched_methodFptr) in typeReferenceProviders)
        {
            unsafe
            {
                var regex = ((delegate* managed<Regex>)get_Regex_methodFptr)();
                var match = regex.Match(node.OriginalTypeString ?? string.Empty);
                if (match.Success)
                {
                    var type = ((delegate* managed<Match, Type?>)Matched_methodFptr)(match);
                    predefinedType = type;

                    if (predefinedType is not null) return true;
                }
            }
        }

        return false;
    }

    public static bool TryBuildPredefinedTypeReference(ModuleDefinition module, CppTypeNode node, bool isEnum, [NotNullWhen(true)] out TypeReference? reference)
    {
        reference = null;

        if (TryGetPredefinedType(node, isEnum, out var predefinedType))
        {
            reference = module.ImportReference(predefinedType);
            return true;
        }

        return false;
    }

    public static (TypeReference @ref, string name) BuildReference(
        Dictionary<string, TypeDefinition> definedTypes,
        ModuleDefinition module,
        in TypeData type,
        bool isResult = false)
    {
        var arr = type.Analyzer.CppTypeHandle.ToArray().Reverse();

        TypeReference? reference = null;
        bool isUnmanagedType = false;
        bool rootTypeParsed = false;

        foreach (var item in arr)
        {

            switch (item.Type)
            {
                case CppTypeEnum.FundamentalType:
                    if (rootTypeParsed is true)
                        throw new InvalidOperationException();

                    reference = BuildFundamentalTypeReference(module, item);

                    isUnmanagedType = true;
                    rootTypeParsed = true;
                    break;

                case CppTypeEnum.Enum:
                    if (rootTypeParsed is true)
                        throw new InvalidOperationException();
                    {
                        if (TryBuildPredefinedTypeReference(module, item, true, out var @ref))
                        {
                            reference = @ref;
                            isUnmanagedType = true;
                            break;
                        }
                        else
                            goto ENUM_DEFAULT_PARSE;
                    }

                case CppTypeEnum.Array:
                case CppTypeEnum.Pointer:
                    if (isUnmanagedType)
                        reference = reference.MakePointerType();
                    else
                    {
                        var pointerType = new GenericInstanceType(module.ImportReference(typeof(Pointer<>)));
                        pointerType.GenericArguments.Add(reference);
                        reference = module.ImportReference(pointerType);
                        isUnmanagedType = true;
                    }
                    break;

                case CppTypeEnum.RValueRef:
                    if (isUnmanagedType)
                        return (reference.MakeByReferenceType(), "rvalRef");
                    else
                    {
                        var rvalRefType = new GenericInstanceType(module.ImportReference(typeof(RValueReference<>)));
                        rvalRefType.GenericArguments.Add(reference);
                        return (module.ImportReference(rvalRefType), string.Empty);
                    }

                case CppTypeEnum.Ref:
                    if (isUnmanagedType)
                        return (reference.MakeByReferenceType(), string.Empty);
                    else
                    {
                        var refType = new GenericInstanceType(module.ImportReference(typeof(Reference<>)));
                        refType.GenericArguments.Add(reference);
                        return (module.ImportReference(refType), string.Empty);
                    }

                case CppTypeEnum.Class:
                case CppTypeEnum.Struct:
                case CppTypeEnum.Union:
                    if (rootTypeParsed is true)
                        throw new InvalidOperationException();

                    {
                        if (TryBuildPredefinedTypeReference(module, item, false, out var @ref))
                        {
                            reference = @ref;
                            isUnmanagedType = @ref.IsValueType;
                            rootTypeParsed = true;
                            break;
                        }
                        else
                            goto TYPE_DEFAULT_PARSE;
                    }

                ENUM_DEFAULT_PARSE:
                    reference = module.ImportReference(typeof(int));
                    isUnmanagedType = true;
                    break;

                TYPE_DEFAULT_PARSE:
                    if (definedTypes.TryGetValue(type.FullTypeIdentifier, out var t) is false)
                    {
                        reference = module.ImportReference(typeof(nint));
                        isUnmanagedType = true;
                    }
                    else
                    {
                        reference = t;
                    }
                    rootTypeParsed = true;
                    break;
            }
        }

        if (rootTypeParsed && isUnmanagedType is false)
        {
            if (isResult)
            {
                var rltType = new GenericInstanceType(module.ImportReference(typeof(Result<>)));
                rltType.GenericArguments.Add(reference);
                reference = module.ImportReference(rltType);
            }
            else
            {
                var refType = new GenericInstanceType(module.ImportReference(typeof(Reference<>)));
                refType.GenericArguments.Add(reference);
                reference = module.ImportReference(refType);
            }
        }

        if (reference is not null && reference.Module.Assembly.Name.Name is "System.Private.CoreLib")
            Console.WriteLine($"{reference} {reference.Module.Assembly}");

        return (reference!, string.Empty);
    }
}
