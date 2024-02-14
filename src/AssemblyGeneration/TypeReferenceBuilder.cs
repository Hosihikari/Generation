using Hosihikari.Generation.Generator;
using Hosihikari.Generation.Parser;
using Hosihikari.Minecraft;
using Hosihikari.NativeInterop;
using Hosihikari.NativeInterop.Generation;
using Hosihikari.NativeInterop.Unmanaged;
using Mono.Cecil;
using Mono.Cecil.Rocks;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Linq;

namespace Hosihikari.Generation.AssemblyGeneration;

public static class TypeReferenceBuilder
{
    private static readonly Dictionary<string, Dictionary<string, Type>> predefinedTypes = [];

    private static readonly List<(Type, nint, nint)> typeReferenceProviders = [];

    /// <summary>
    /// Initializes the predefined types and type reference providers from the given configuration.
    /// </summary>
    /// <param name="config">The configuration to use for initialization.</param>
    public static void Init(Config config)
    {
        // Load assemblies from the specified directory
        var assemblies = new DirectoryInfo(config.RefAssemblyDir)
            .EnumerateFiles()
            .Where(file => file.Extension == ".dll")
            .Select(file => Assembly.LoadFrom(file.FullName));

        // Iterate through the assemblies and types to initialize predefined types and type reference providers
        foreach (var type in assemblies.SelectMany(assembly => assembly.GetExportedTypes())
            .Where(type => type.IsGenericTypeDefinition is false))
        {
            // Check if the type has the PredefinedTypeAttribute
            if (type.GetCustomAttribute<PredefinedTypeAttribute>() is not null)
            {
                var attribute = type.GetCustomAttribute<PredefinedTypeAttribute>()!;
                var keyValues = predefinedTypes.TryGetValue(attribute.NativeTypeNamespace, out Dictionary<string, Type>? value) ? value : predefinedTypes[attribute.NativeTypeNamespace] = [];

                // Add the type to predefined types based on its attributes
                if (type.IsClass)
                {
                    foreach (var @interface in type.GetInterfaces().Where(@interface => @interface.IsGenericType && @interface.GetGenericTypeDefinition() == typeof(ICppInstance<>)))
                    {
                        keyValues.Add(
                            string.IsNullOrWhiteSpace(attribute.NativeTypeName) ? type.Name : attribute.NativeTypeName, type);
                    }
                }
                else if (type.IsEnum || type.IsValueType)
                {
                    keyValues.Add(
                        string.IsNullOrWhiteSpace(attribute.NativeTypeName) ? type.Name : attribute.NativeTypeName,
                        type);
                }
            }

            // Check if the type is a type reference provider and add it to the type reference providers list
            if (type != typeof(ITypeReferenceProvider) && type.IsAssignableTo(typeof(ITypeReferenceProvider)))
            {
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

        string namespaceStr = node.Namespaces is not null ? string.Join("::", node.Namespaces) : string.Empty;

        if (predefinedTypes.TryGetValue(namespaceStr, out Dictionary<string, Type>? types) &&
            types.TryGetValue(node.TypeIdentifierWithTemplateArgs ?? string.Empty, out Type? type))
        {
            if (isEnum && type.IsEnum is false)
                return false;

            predefinedType = type;
            return true;
        }

        foreach ((Type _, nint getRegexMethodFptr, nint matchedMethodFptr) in typeReferenceProviders)
        {
            unsafe
            {
                Regex regex = ((delegate* managed<Regex>)getRegexMethodFptr)();
                Match match = regex.Match(node.OriginalTypeString ?? string.Empty);
                if (!match.Success)
                    continue;

                Type? matchedType = ((delegate* managed<Match, Type?>)matchedMethodFptr)(match);
                predefinedType = matchedType;

                if (predefinedType is not null)
                    return true;
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

    /// <summary>
    /// Builds a TypeReference based on the provided type data and module information.
    /// </summary>
    /// <param name="definedTypes">Dictionary of defined types</param>
    /// <param name="module">Module definition</param>
    /// <param name="type">Type data</param>
    /// <param name="isResult">Flag indicating if the type is a result</param>
    /// <returns>TypeReference for the given type data</returns>
    public static TypeReference BuildReference(
        Dictionary<string, TypeDefinition> definedTypes,
        ModuleDefinition module,
        in TypeData type,
        bool isResult = false)
    {
        IEnumerable<CppTypeNode> typeNodes = type.Analyzer.CppTypeHandle.ToArray().Reverse();

        TypeReference? reference = null;
        bool isUnmanagedType = false;
        bool rootTypeParsed = false;

        foreach (CppTypeNode typeNode in typeNodes)
        {
            switch (typeNode.Type)
            {
                case CppTypeEnum.FundamentalType:
                    if (rootTypeParsed)
                    {
                        throw new InvalidOperationException();
                    }

                    reference = BuildFundamentalTypeReference(module, typeNode);
                    isUnmanagedType = true;
                    rootTypeParsed = true;
                    break;

                case CppTypeEnum.Enum:
                    if (rootTypeParsed)
                    {
                        throw new InvalidOperationException();
                    }
                    {
                        if (TryBuildPredefinedTypeReference(module, typeNode, true, out TypeReference? @ref))
                        {
                            reference = @ref;
                            isUnmanagedType = true;
                            break;
                        }
                        goto EnumDefaultParse;
                    }

                case CppTypeEnum.Array:
                case CppTypeEnum.Pointer:
                    reference = isUnmanagedType ? reference.MakePointerType() :
                        module.ImportReference(new GenericInstanceType(module.ImportReference(typeof(Pointer<>)))
                        {
                            GenericArguments = { reference }
                        });
                    isUnmanagedType = true;
                    break;

                case CppTypeEnum.RValueRef:
                    return isUnmanagedType ? reference.MakeByReferenceType() :
                        module.ImportReference(new GenericInstanceType(module.ImportReference(typeof(RValueReference<>)))
                        {
                            GenericArguments = { reference }
                        });

                case CppTypeEnum.Ref:
                    return isUnmanagedType ? reference.MakeByReferenceType() :
                        module.ImportReference(new GenericInstanceType(module.ImportReference(typeof(Reference<>)))
                        {
                            GenericArguments = { reference }
                        });

                case CppTypeEnum.Class:
                case CppTypeEnum.Struct:
                case CppTypeEnum.Union:
                    if (rootTypeParsed)
                    {
                        throw new InvalidOperationException();
                    }
                    {
                        if (TryBuildPredefinedTypeReference(module, typeNode, false, out TypeReference? @ref))
                        {
                            reference = @ref;
                            isUnmanagedType = @ref.IsValueType;
                            rootTypeParsed = true;
                            break;
                        }
                        goto TypeDefaultParse;
                    }

                EnumDefaultParse:
                    reference = module.ImportReference(typeof(int));
                    isUnmanagedType = true;
                    break;

                TypeDefaultParse:
                    if (!definedTypes.TryGetValue(type.FullTypeIdentifier, out TypeDefinition? definition))
                    {
                        reference = module.ImportReference(typeof(nint));
                        return reference!;
                    }
                    reference = definition;
                    rootTypeParsed = true;
                    break;

                case CppTypeEnum.VarArgs:
                    break;

                default:
                    throw new ArgumentOutOfRangeException("Type: " + typeNode.Type);
            }
        }

        if (rootTypeParsed && !isUnmanagedType)
        {
            reference = isResult ?
                module.ImportReference(new GenericInstanceType(module.ImportReference(typeof(Result<>)))
                {
                    GenericArguments = { reference }
                }) :
                module.ImportReference(new GenericInstanceType(module.ImportReference(typeof(Reference<>)))
                {
                    GenericArguments = { reference }
                });
        }

        return reference!;
    }


}