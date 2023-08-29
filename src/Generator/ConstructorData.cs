using Hosihikari.Utils;
using System.Reflection;
using System.Text;
using static Hosihikari.Utils.OriginalData.Class;

namespace Hosihikari.Generation.Generator;

public readonly struct ConstructorData
{
    public readonly Item Data;
    public readonly TypeData[] Parameters;
    public readonly string ClassName;

    public readonly SymbolType SymbolType;
    public readonly AccessType AccessType;

    public readonly string[] Lines;
    public readonly string NativeFunctionPointerFieldName;
    public readonly string InvocationName;

    public ConstructorData(in Item item, string className)
    {
        Data = item;
        ClassName = className;
        SymbolType = (SymbolType)Data.SymbolType;
        AccessType = (AccessType)Data.AccessType;

        var @params = new TypeData[Data.Params.Count];
        for (int i = 0; i < Data.Params.Count; i++)
            @params[i] = new(Data.Params[i]);
        Parameters = @params;

        (Lines, NativeFunctionPointerFieldName, InvocationName) = BuildLines(this);
    }

    private static (string[] lines, string fieldName) BuildFunctionPointerFieldLines(in ConstructorData data)
    {
        var temp = "Constructor" + data.Data.Name + '_' + data.Data.Symbol;
        string fieldName;
        StringBuilder builder = new();
        foreach (var c in temp)
            builder.Append(TypeAnalyzer.IsLetterOrUnderline(c) ? c : '_');
        fieldName = builder.ToString();

        var lines = new string[]
        {
            $"private static readonly Lazy<nint> {fieldName}",
            "    = global::Hosihikari.NativeInterop.SymbolHelper.DlsymLazy(",
            $"        \"{data.Data.Symbol}\");"
        };

        return (lines, fieldName);
    }

    private static (string[] lines, string fieldName, string invovationName) BuildLines(in ConstructorData data)
    {
        var (fieldLines, fieldName) = BuildFunctionPointerFieldLines(data);

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

        var methodLines = new string[]
        {
            $"{data.AccessType.ToString().ToLower()} unsafe {data.ClassName}({paramListStr})",
            $"{{",
            $"    var func = (delegate* unmanaged<void*{Utils.StrIfTrue(", ", funcPointerTypeStr.Length is not 0)}{funcPointerTypeStr}>){fieldName}.Value;",
            $"    var ptr = global::Hosihikari.NativeInterop.Unmanaged.HeapAlloc.New(ClassSize);",
            $"    func(ptr{Utils.StrIfTrue(", ", argListStr.Length is not 0)}, {argListStr})",
            $"    this.Pointer = new(ptr);",
            $"    this.IsOwner = true;",
            $"}}"
        };

        var lines = new string[fieldLines.Length + methodLines.Length];
        fieldLines.CopyTo(lines, 0);
        methodLines.CopyTo(lines, fieldLines.Length);

        return (lines, fieldName, data.ClassName);
    }
}
