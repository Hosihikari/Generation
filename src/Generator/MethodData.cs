using Hosihikari.Utils;
using static Hosihikari.Utils.OriginalData.Class;

namespace Hosihikari.Generation.Generator;

public readonly struct MethodData
{
    public readonly Item Data;
    public readonly TypeData[] Parameters;
    public readonly TypeData ReturnType;

    public readonly SymbolType SymbolType;
    public readonly AccessType AccessType;

    public readonly string[] Lines;

    public MethodData(in Item item)
    {
        Data = item;
        SymbolType = (SymbolType)Data.SymbolType;
        AccessType = (AccessType)Data.AccessType;

        if (SymbolType is SymbolType.StaticField)
            throw new InvalidOperationException();

        ReturnType = new(Data.Type);
        var @params = new TypeData[Data.Params.Count];
        for (int i = 0; i < Data.Params.Count; i++)
            @params[i] = new(Data.Params[i]);

        Parameters = @params;
        Lines = BuildLines(this);
    }

    private static string[] BuildLines(in MethodData data)
    {
        throw new NotImplementedException();
    }
}
