using Hosihikari.Generation.CppParser;
using Hosihikari.Generation.Utils;

namespace Hosihikari.Generation.ILGenerate;

public class TypeRegistry
{

    private Dictionary<CppType, TypeGenerator> Types { get; } = [];

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

        switch (type.Type)
        {
            case CppTypeEnum.FundamentalType:
                return await ResolveFoundationTypeAsync(type);
        }

        //type = type.RootType;

        //if (Types.TryGetValue(type, out var typeGenerator))
        //    return typeGenerator.TypeBuilder;

        //if (autoRegister)
        //    return (await RegisterTypeAsync(type, null))?.TypeBuilder;

        //return null;
    }

    private async ValueTask<Type?> ResolveFoundationTypeAsync(CppType type)
    {
        if (type.RootType.Type is not CppTypeEnum.FundamentalType)
            throw new InvalidOperationException("Not a fundamental type");

        var collection = type.ToEnumerable();
        var rootType = collection.First();
        var subTypes = collection.Count() > 1 ? collection.Skip(1) : [];

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

        Type rlt = rootClrType;
        foreach (var subType in subTypes)
        {
            rlt = await ModifyTypeAsync(subType, subTypes, rlt);
        }

        return rlt;
    }

    private async ValueTask<Type> ModifyTypeAsync(CppType root, IEnumerable<CppType> subTypes, Type rootClrType)
    {
        throw new NotImplementedException();
    }
}
