using Hosihikari.Generation.CppParser;
using Hosihikari.Generation.Utils;
using Hosihikari.NativeInterop.Unmanaged;

namespace Hosihikari.Generation.ILGenerate;

public class TypeRegistry
{

    private Dictionary<CppType, TypeGenerator> Types { get; } = [];

    public Dictionary<CppType, Type> PredefinedEnums { get; } = [];

    public AssemblyGenerator AssemblyGenerator { get; }

    public TypeRegistry(AssemblyGenerator assemblyGenerator)
    {
        if (assemblyGenerator.TypeRegistry is not null)
            throw new InvalidOperationException("TypeRegistry is already initialized");

        AssemblyGenerator = assemblyGenerator;
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
                TypeGenerator.TryCreateEmptyTypeGenerator(AssemblyGenerator, type, out var typeGenerator);
                rlt = typeGenerator;
            }
            else
            {
                TypeGenerator.TryCreateTypeGenerator(AssemblyGenerator, type, @class, out var typeGenerator);
                rlt = typeGenerator;
            }
        });

        if (rlt is not null)
            Types.Add(type, rlt);

        return rlt;
    }

    public async ValueTask<Type?> ResolveTypeAsync(CppType type, bool autoRegister = true)
    {
        var rootType = type.RootType;

        switch (rootType.Type)
        {
            case CppTypeEnum.FundamentalType:
                return await ResolveFoundationTypeAsync(type);

            case CppTypeEnum.Enum:
                return await ResolvePredefinedEnumTypeAsync(type);

            case CppTypeEnum.Class:
            case CppTypeEnum.Struct:
            case CppTypeEnum.Union:
                if (Types.TryGetValue(rootType, out var typeGenerator))
                    return typeGenerator.TypeBuilder;
                else
                {
                    if (autoRegister)
                        return (await RegisterTypeAsync(type, null))?.TypeBuilder;

                    return null;
                }

            case CppTypeEnum.Function:
                throw new NotImplementedException("Not implemented");

            case CppTypeEnum.VarArgs:
            case CppTypeEnum.Pointer:
            case CppTypeEnum.Ref:
            case CppTypeEnum.RValueRef:
            case CppTypeEnum.Array:
            default:
                throw new InvalidOperationException();
        }

        //type = type.RootType;

        //if (Types.TryGetValue(type, out var typeGenerator))
        //    return typeGenerator.TypeBuilder;

        //if (autoRegister)
        //    return (await RegisterTypeAsync(type, null))?.TypeBuilder;

        //return null;
    }

    /// <summary>
    /// Resolves the foundation type asynchronously.
    /// </summary>
    /// <param name="type">The CppType to resolve.</param>
    /// <returns>The resolved Type or null if not found.</returns>
    private static async ValueTask<Type?> ResolveFoundationTypeAsync(CppType type)
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
        return await ModifyTypeAsync(subTypes, rootClrType);
    }

    /// <summary>
    /// Resolves the predefined enum type asynchronously.
    /// </summary>
    /// <param name="type">The CppType to resolve.</param>
    /// <returns>A ValueTask containing the resolved Type, or null if unable to resolve.</returns>
    private async ValueTask<Type?> ResolvePredefinedEnumTypeAsync(CppType type)
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
            return await ModifyTypeAsync(type.ToEnumerable().Skip(1), typeof(int));
    }


    /// <summary>
    /// Modifies the given root CLR type based on the provided subtypes.
    /// </summary>
    /// <param name="subTypes">The collection of subtypes to apply to the root CLR type.</param>
    /// <param name="rootClrType">The root CLR type to modify.</param>
    /// <returns>The modified Type or null if modification fails.</returns>
    private static async ValueTask<Type?> ModifyTypeAsync(IEnumerable<CppType> subTypes, Type rootClrType)
    {
        Type? current = rootClrType;

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
                            if (current.IsValueType)
                                current = current.MakePointerType();
                            else
                                current = typeof(Pointer<>).MakeGenericType(current);
                        }
                        break;
                    case CppTypeEnum.Ref:
                    case CppTypeEnum.RValueRef:
                        {
                            if (current.IsValueType)
                                current = current.MakeByRefType();
                            else
                                current = typeof(Reference<>).MakeGenericType(current);
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

}
