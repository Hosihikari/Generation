using Hosihikari.Generation.CppParser;
using Hosihikari.Generation.Utils;
using Hosihikari.NativeInterop.Generation;
using Hosihikari.NativeInterop.Unmanaged;
using Mono.Cecil;
using Mono.Cecil.Rocks;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Reflection;

namespace Hosihikari.Generation.ILGenerate;

public class TypeRegistry
{

    private ConcurrentDictionary<CppType, TypeGenerator> Types { get; } = [];

    public FrozenDictionary<CppType, TypeReference> PredefinedEnums { get; }

    public FrozenDictionary<CppType, TypeReference> PredefinedTypes { get; }

    public AssemblyGenerator Assembly { get; }

    public IReadOnlyList<Assembly> ReferenceAssemblies { get; }



    public TypeRegistry(AssemblyGenerator assemblyGenerator, IReadOnlyList<Assembly> references)
    {
        if (assemblyGenerator.TypeRegistry is not null)
            throw new InvalidOperationException("TypeRegistry is already initialized");

        Assembly = assemblyGenerator;
        ReferenceAssemblies = [typeof(ICppInstanceNonGeneric).Assembly, .. references];

        var types = from reference in ReferenceAssemblies
                    from type in reference.GetTypes()
                    let attributes = type.GetCustomAttributes<PredefinedTypeAttribute>()
                    where attributes.Any()
                    select (attributes.First(), type);

        var predefinedTypes = from type in types
                              let rlt = CppTypeParser.TryParse(type.Item1.TypeName)
                              where rlt.Key
                              select (type.Item1.IgnoreTemplateArgs ? rlt.Value!.Clone().Operate(t => t.TemplateTypes = null) : rlt.Value, type.type);

        PredefinedEnums = (from type in predefinedTypes
                           where type.Item1.Type is CppTypeEnum.Enum && type.type.IsEnum
                           select KeyValuePair.Create(type.Item1, Assembly.ImportRef(type.type))).ToFrozenDictionary();

        PredefinedTypes = (from type in predefinedTypes
                           select KeyValuePair.Create(type.Item1, Assembly.ImportRef(type.type))).ToFrozenDictionary();
    }

    public async ValueTask<TypeGenerator?> GetOrRegisterTypeAsync(CppType type, OriginalClass? @class)
    {
        if (Types.TryGetValue(type, out var typeGenerator))
            return typeGenerator;

        return await RegisterTypeAsync(type, @class);
    }

    public async ValueTask<TypeGenerator?> RegisterTypeAsync(CppType type, OriginalClass? @class)
    {
        type = type.RootType;

        if (Types.ContainsKey(type))
            return null;

        TypeGenerator? rlt = null;

        await Task.Run(() =>
        {
            if (@class is null)
            {
                TypeGenerator.TryCreateEmptyTypeGenerator(Assembly, type, out var typeGenerator);
                rlt = typeGenerator;
            }
            else
            {
                TypeGenerator.TryCreateTypeGenerator(Assembly, type, @class, out var typeGenerator);
                rlt = typeGenerator;
            }
        });

        if (rlt is not null)
            Types[type] = rlt;

        return rlt;
    }

    /// <summary>
    /// Resolves the given CppType asynchronously, returning the corresponding Type.
    /// </summary>
    /// <param name="type">The CppType to resolve.</param>
    /// <param name="autoRegister">Flag indicating whether to automatically register the type if not found.</param>
    /// <returns>The resolved Type or null.</returns>
    public async ValueTask<TypeReference?> ResolveTypeAsync(CppType type, bool autoRegister = true)
    {
        var rootType = type.RootType;
        var subTypes = type.ToEnumerable().Skip(1);

        TypeReference? ret;

        switch (rootType.Type)
        {
            case CppTypeEnum.FundamentalType:
                if (rootType.FundamentalType is CppFundamentalType.Void && subTypes.Count() is 0)
                    return Assembly.ImportRef(typeof(void));

                return await ResolveFundamentalTypeAsync(type);

            case CppTypeEnum.Enum:
                return await ResolveEnumTypeAsync(type);

            case CppTypeEnum.Class:
            case CppTypeEnum.Struct:
            case CppTypeEnum.Union:
                var temp = await ResolvePredefinedTypeAsync(type);
                if (temp is not null) return temp;

                if (Types.TryGetValue(rootType, out var typeGenerator))
                {
                    ret = typeGenerator.Type;
                }
                else
                {
                    if (autoRegister)
                        ret = (await RegisterTypeAsync(type, null))?.Type;
                    else
                        ret = null;
                }
                break;

            case CppTypeEnum.Function:
                //throw new NotImplementedException("Not implemented");
                ret = null;
                break;

            case CppTypeEnum.VarArgs:
            case CppTypeEnum.Pointer:
            case CppTypeEnum.Ref:
            case CppTypeEnum.RValueRef:
            case CppTypeEnum.Array:
            default:
                //throw new InvalidOperationException();
                ret = null;
                break;
        }

        if (ret is null)
            return null;

        ret = await ModifyTypeAsync(subTypes, ret);

        if (ret is null)
            return null;

        if (ret.IsValueType || ret.IsPointer || ret.IsByReference)
            return ret;

        return Assembly.ImportRef(typeof(UnexploredType<>)).MakeGenericInstanceType(ret);
    }


    /// <summary>
    /// Resolves the foundation type asynchronously.
    /// </summary>
    /// <param name="type">The CppType to resolve.</param>
    /// <returns>The resolved Type or null if not found.</returns>
    private async ValueTask<TypeReference?> ResolveFundamentalTypeAsync(CppType type)
    {
        // Check if the type is a fundamental type
        if (type.RootType.Type is not CppTypeEnum.FundamentalType)
            throw new InvalidOperationException("Not a fundamental type");

        // Convert the type to an enumerable collection
        var collection = type.ToEnumerable();
        var rootType = collection.First();
        var subTypes = collection.Count() > 1 ? collection.Skip(1) : [];

        // Map the fundamental type to its corresponding CLR type
        var rootClrType = rootType.FundamentalType switch
        {
            CppFundamentalType.Void => typeof(void),

            CppFundamentalType.Boolean => typeof(bool),

            CppFundamentalType.Float => typeof(float),
            CppFundamentalType.Double => typeof(double),

            CppFundamentalType.WChar => typeof(char),

            CppFundamentalType.SChar => typeof(sbyte),
            CppFundamentalType.Int16 => typeof(short),
            CppFundamentalType.Int32 => typeof(int),
            CppFundamentalType.Int64 => typeof(long),

            CppFundamentalType.Char => typeof(byte),
            CppFundamentalType.UInt16 => typeof(ushort),
            CppFundamentalType.UInt32 => typeof(uint),
            CppFundamentalType.UInt64 => typeof(ulong),

            _ => throw new InvalidOperationException("Unknown fundamental type")
        };

        // Modify the type asynchronously
        return await ModifyTypeAsync(subTypes, Assembly.ImportRef(rootClrType));
    }

    /// <summary>
    /// Resolves the predefined enum type asynchronously.
    /// </summary>
    /// <param name="type">The CppType to resolve.</param>
    /// <returns>A ValueTask containing the resolved Type, or null if unable to resolve.</returns>
    private async ValueTask<TypeReference?> ResolveEnumTypeAsync(CppType type)
    {
        // Get the root type
        var rootType = type.RootType;

        // Check if the root type is an Enum
        if (rootType.Type is not CppTypeEnum.Enum)
            return null;

        // Try to get the corresponding CLR type for the enum
        if (PredefinedEnums.TryGetValue(rootType, out var clrType))
            return await ModifyTypeAsync(type.ToEnumerable().Skip(1), clrType);
        else
            return await ModifyTypeAsync(type.ToEnumerable().Skip(1), Assembly.ImportRef(typeof(int)));
    }

    private async ValueTask<TypeReference?> ResolvePredefinedTypeAsync(CppType type)
    {
        if (PredefinedTypes.TryGetValue(type, out var predefinedType))
        {
            return await ModifyTypeAsync(type.ToEnumerable().Skip(1), predefinedType);
        }
        else if (PredefinedTypes.TryGetValue(type.Clone().Operate(t => t.TemplateTypes = null), out predefinedType))
        {
            return await ModifyTypeAsync(type.ToEnumerable().Skip(1), predefinedType);
        }
        else return null;
    }


    /// <summary>
    /// Modifies the given root CLR type based on the provided subtypes.
    /// </summary>
    /// <param name="subTypes">The collection of subtypes to apply to the root CLR type.</param>
    /// <param name="rootClrType">The root CLR type to modify.</param>
    /// <returns>The modified Type or null if modification fails.</returns>
    private async ValueTask<TypeReference?> ModifyTypeAsync(IEnumerable<CppType> subTypes, TypeReference rootClrType)
    {
        TypeReference? current = rootClrType;

        // Running the modification process asynchronously
        await Task.Run(() =>
        {
            foreach (var subType in subTypes)
            {
                switch (subType.Type)
                {
                    case CppTypeEnum.Pointer:
                    case CppTypeEnum.Array:
                        {
                            if (current.IsValueType || current.IsPointer)
                                current = current.MakePointerType();
                            else
                                current = Assembly.ImportRef(typeof(Pointer<>)).MakeGenericInstanceType(current);
                        }
                        break;
                    case CppTypeEnum.Ref:
                    case CppTypeEnum.RValueRef:
                        {
                            if (current.IsValueType || current.IsPointer)
                                current = current.MakeByReferenceType();
                            else
                                current = Assembly.ImportRef(typeof(Reference<>)).MakeGenericInstanceType(current);
                        }
                        break;

                    default:
                        current = null;
                        return;
                }
            }
        });

        return current;
    }

    public async ValueTask GenerateAllTypes()
    {
        foreach (var (_, type) in Types)
        {
            if (type.Generated)
                continue;

            await type.GenerateAsync();
        }
    }
}
