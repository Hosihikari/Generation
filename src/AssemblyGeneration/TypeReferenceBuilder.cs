using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.RegularExpressions;
using Hosihikari.Generation.Generator;
using Hosihikari.Generation.Parser;
using Hosihikari.Minecraft;
using Hosihikari.NativeInterop;
using Hosihikari.NativeInterop.Generation;
using Hosihikari.NativeInterop.Unmanaged;
using Mono.Cecil;
using Mono.Cecil.Rocks;

namespace Hosihikari.Generation.AssemblyGeneration;

public static class TypeReferenceBuilder
{
    private static readonly Dictionary<string, Dictionary<string, Type>> predefinedTypes = new();

    private static readonly List<(Type, nint, nint)> typeReferenceProviders = [];

    static TypeReferenceBuilder()
    {
        List<Assembly> assemblies =
        [
            typeof(AABB).Assembly,
            typeof(SymbolHelper).Assembly
        ];

        foreach (Type type in from assembly in assemblies
                 from type in assembly.GetExportedTypes()
                 where !type.IsGenericTypeDefinition
                 select type)
        {
            PredefinedTypeAttribute? attribute = type.GetCustomAttribute<PredefinedTypeAttribute>();
            if (attribute is not null)
            {
                Dictionary<string, Type> keyValues;
                if (predefinedTypes.ContainsKey(attribute.NativeTypeNamespace) is false)
                {
                    keyValues = new();
                    predefinedTypes.Add(attribute.NativeTypeNamespace, keyValues);
                }
                else
                {
                    keyValues = predefinedTypes[attribute.NativeTypeNamespace];
                }

                if (type.IsClass)
                {
                    foreach (Type @interface in type.GetInterfaces())
                    {
                        if (!@interface.IsGenericType)
                        {
                            continue;
                        }

                        if (@interface.GetGenericTypeDefinition() == typeof(ICppInstance<>))
                        {
                            keyValues.Add(
                                string.IsNullOrWhiteSpace(attribute.NativeTypeName)
                                    ? type.Name
                                    : attribute.NativeTypeName, type);
                        }
                    }
                }
                else if (type.IsEnum)
                {
                    keyValues.Add(
                        string.IsNullOrWhiteSpace(attribute.NativeTypeName) ? type.Name : attribute.NativeTypeName,
                        type);
                }
                else if (type.IsValueType)
                {
                    keyValues.Add(
                        string.IsNullOrWhiteSpace(attribute.NativeTypeName) ? type.Name : attribute.NativeTypeName,
                        type);
                }
            }

            if (type == typeof(ITypeReferenceProvider) || !type.IsAssignableTo(typeof(ITypeReferenceProvider)))
            {
                continue;
            }

            nint fptr1 = type.GetProperty(
                    nameof(ITypeReferenceProvider.Regex),
                    typeof(Regex))!
                .GetMethod!
                .MethodHandle
                .GetFunctionPointer();

            nint fptr2 = type.GetMethod(
                    nameof(ITypeReferenceProvider.Matched))!
                .MethodHandle
                .GetFunctionPointer();

            typeReferenceProviders.Add((type, fptr1, fptr2));
        }
    }

    private static TypeReference BuildFundamentalTypeReference(ModuleDefinition module, CppTypeNode node)
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
            _ => throw new InvalidOperationException()
        };
    }

    public static bool TryGetPredefinedType(CppTypeNode node, bool isEnum, [NotNullWhen(true)] out Type? predefinedType)
    {
        predefinedType = null;

        string @namespace = string.Empty;
        if (node.Namespaces is not null)
        {
            @namespace = string.Join("::", node.Namespaces);
        }

        if (predefinedTypes.TryGetValue(@namespace, out Dictionary<string, Type>? types))
        {
            if (types.TryGetValue(node.TypeIdentifierWithTemplateArgs ?? string.Empty, out Type? type))
            {
                if (isEnum && type.IsEnum is false)
                {
                    return false;
                }

                predefinedType = type;

                return true;
            }
        }

        foreach ((Type _, nint get_Regex_methodFptr, nint Matched_methodFptr) in typeReferenceProviders)
        {
            unsafe
            {
                Regex regex = ((delegate* managed<Regex>)get_Regex_methodFptr)();
                Match match = regex.Match(node.OriginalTypeString ?? string.Empty);
                if (!match.Success)
                {
                    continue;
                }

                Type? type = ((delegate* managed<Match, Type?>)Matched_methodFptr)(match);
                predefinedType = type;

                if (predefinedType is not null)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryBuildPredefinedTypeReference(ModuleDefinition module, CppTypeNode node, bool isEnum,
        [NotNullWhen(true)] out TypeReference? reference)
    {
        reference = null;

        if (!TryGetPredefinedType(node, isEnum, out Type? predefinedType))
        {
            return false;
        }

        reference = module.ImportReference(predefinedType);
        return true;
    }

    public static TypeReference BuildReference(
        Dictionary<string, TypeDefinition> definedTypes,
        ModuleDefinition module,
        in TypeData type,
        bool isResult = false)
    {
        IEnumerable<CppTypeNode> arr = type.Analyzer.CppTypeHandle.ToArray().Reverse();

        TypeReference? reference = null;
        bool isUnmanagedType = false;
        bool rootTypeParsed = false;

        foreach (CppTypeNode item in arr)
        {
            switch (item.Type)
            {
                case CppTypeEnum.FundamentalType:
                    if (rootTypeParsed)
                    {
                        throw new InvalidOperationException();
                    }

                    reference = BuildFundamentalTypeReference(module, item);

                    isUnmanagedType = true;
                    rootTypeParsed = true;
                    break;

                case CppTypeEnum.Enum:
                    if (rootTypeParsed)
                    {
                        throw new InvalidOperationException();
                    }

                {
                    if (TryBuildPredefinedTypeReference(module, item, true, out TypeReference? @ref))
                    {
                        reference = @ref;
                        isUnmanagedType = true;
                        break;
                    }

                    goto ENUM_DEFAULT_PARSE;
                }

                case CppTypeEnum.Array:
                case CppTypeEnum.Pointer:
                    if (isUnmanagedType)
                    {
                        reference = reference.MakePointerType();
                    }
                    else
                    {
                        GenericInstanceType pointerType = new(module.ImportReference(typeof(Pointer<>)));
                        pointerType.GenericArguments.Add(reference);
                        reference = module.ImportReference(pointerType);
                        isUnmanagedType = true;
                    }

                    break;

                case CppTypeEnum.RValueRef:
                    if (isUnmanagedType)
                    {
                        return reference.MakeByReferenceType();
                    }

                    GenericInstanceType rvalRefType = new(module.ImportReference(typeof(RValueReference<>)));
                    rvalRefType.GenericArguments.Add(reference);
                    return module.ImportReference(rvalRefType);

                case CppTypeEnum.Ref:
                    if (isUnmanagedType)
                    {
                        return reference.MakeByReferenceType();
                    }

                    GenericInstanceType refType = new(module.ImportReference(typeof(Reference<>)));
                    refType.GenericArguments.Add(reference);
                    return module.ImportReference(refType);

                case CppTypeEnum.Class:
                case CppTypeEnum.Struct:
                case CppTypeEnum.Union:
                    if (rootTypeParsed)
                    {
                        throw new InvalidOperationException();
                    }

                {
                    if (TryBuildPredefinedTypeReference(module, item, false, out TypeReference? @ref))
                    {
                        reference = @ref;
                        isUnmanagedType = @ref.IsValueType;
                        rootTypeParsed = true;
                        break;
                    }

                    goto TYPE_DEFAULT_PARSE;
                }

                    ENUM_DEFAULT_PARSE:
                    reference = module.ImportReference(typeof(int));
                    isUnmanagedType = true;
                    break;

                    TYPE_DEFAULT_PARSE:
                    if (definedTypes.TryGetValue(type.FullTypeIdentifier, out TypeDefinition? t) is false)
                    {
                        reference = module.ImportReference(typeof(nint));
                        return reference!;
                    }

                    reference = t;
                    rootTypeParsed = true;
                    break;
                case CppTypeEnum.VarArgs:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        if (rootTypeParsed && isUnmanagedType is false)
        {
            if (isResult)
            {
                GenericInstanceType rltType = new(module.ImportReference(typeof(Result<>)));
                rltType.GenericArguments.Add(reference);
                reference = module.ImportReference(rltType);
            }
            else
            {
                GenericInstanceType refType = new(module.ImportReference(typeof(Reference<>)));
                refType.GenericArguments.Add(reference);
                reference = module.ImportReference(refType);
            }
        }

        if (reference?.Module.Assembly.Name.Name is "System.Private.CoreLib")
        {
            Console.WriteLine($"{reference} {reference.Module.Assembly}");
        }

        return reference!;
    }
}