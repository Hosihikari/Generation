using Hosihikari.Utils;
using System.Text;
using static Hosihikari.Utils.OriginalData.Class;

namespace Hosihikari.Generation.Generator;

public readonly struct VirtualMethodData
{
    public readonly Item Data;
    public readonly ulong VirtFunctionIndex;
    public readonly TypeData[] Parameters;
    public readonly TypeData ReturnType;

    public readonly SymbolType SymbolType;
    public readonly AccessType AccessType;

    public readonly string[] Lines;
    public readonly string InvocationName;

    public VirtualMethodData(in Item item, ulong virtFunctionIndex)
    {
        Data = item;
        VirtFunctionIndex = virtFunctionIndex;
        SymbolType = (SymbolType)Data.SymbolType;
        AccessType = (AccessType)Data.AccessType;

        ReturnType = new(Data.Type);
        var @params = new TypeData[Data.Params.Count];
        for (int i = 0; i < Data.Params.Count; i++)
            @params[i] = new(Data.Params[i]);

        Parameters = @params;

        (Lines, InvocationName) = BuildLines(this);
    }

    private static (string[] lines, string invovationName) BuildLines(in VirtualMethodData data)
    {
        var item = data.Data;
        var name = item.Name;
        var methodName = $"{char.ToUpper(name[0])}{name[1..]}";

        StringBuilder paramListStr = new();

        int index = 0;
        foreach (var param in data.Parameters)
        {
            if (index is not 0)
                paramListStr.Append(", ");
            paramListStr.Append(param.Type).Append($" a{index}");
            ++index;
        }

        StringBuilder funcPointerTypeStr = new();
        index = 0;
        foreach (var param in data.Parameters)
        {
            if (index is not 0)
                funcPointerTypeStr.Append(", ");
            funcPointerTypeStr.Append(param.Type);
            ++index;
        }
        if (index is not 0)
            funcPointerTypeStr.Append(", ");
        funcPointerTypeStr.Append("void");

        StringBuilder argListStr = new();
        index = 0;
        foreach (var param in data.Parameters)
        {
            if (index is not 0)
                argListStr.Append(", ");
            argListStr.Append($"{Utils.StrIfTrue("ref ", param.IsByRef)}a{index}");
            ++index;
        }

        var lines = new string[]
        {
            $"{data.AccessType.ToString().ToLower()} unsafe {data.ReturnType.Type} {methodName}({paramListStr})",
            $"{{",
            $"    var address = *(ulong*)((*(long*)(void*)this.Pointer) + {8 * data.VirtFunctionIndex})",
            $"    var func = (delegate* unmanaged<nint{Utils.StrIfTrue(", ", funcPointerTypeStr.Length is not 0)}{funcPointerTypeStr}>)address;",
            $"    {Utils.StrIfFalse("return ", data.ReturnType.IsVoid)}{Utils.StrIfTrue("ref ", data.ReturnType.IsByRef)}func(this.Pointer{Utils.StrIfTrue(", ", argListStr.Length is not 0)}{argListStr});",
            $"}}"
        };

        return (lines, methodName);
    }

}
