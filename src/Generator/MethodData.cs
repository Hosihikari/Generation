using Hosihikari.Utils;
using System.Text;
using static Hosihikari.Utils.OriginalData.Class;

namespace Hosihikari.Generation.Generator;

public readonly struct MethodData
{
    public readonly Item Data;
    public readonly bool IsStatic;
    public readonly TypeData[] Parameters;
    public readonly TypeData ReturnType;

    public readonly SymbolType SymbolType;
    public readonly AccessType AccessType;

    public readonly string[] Lines;
    public readonly string NativeFunctionPointerFieldName;
    public readonly string InvocationName;


    public readonly bool isProperty;
    public readonly Item? Getter;
    public readonly Item? Setter;

    [Flags]
    public enum PropertyMemberType { Getter, Setter }

    public MethodData(in Item item, bool isStatic, Func<PropertyMemberType, (Item method, bool isStatic, string fieldName)>? ifRequestGetterOrSetter = null, bool autoGeneratingForProperty = true, bool autoBuild = true)
    {
        Data = item;
        IsStatic = isStatic;
        SymbolType = (SymbolType)Data.SymbolType;
        AccessType = (AccessType)Data.AccessType;

        if (SymbolType is SymbolType.StaticField)
            throw new InvalidOperationException();

        ReturnType = new(Data.Type);
        var @params = new TypeData[Data.Params.Count];
        for (int i = 0; i < Data.Params.Count; i++)
            @params[i] = new(Data.Params[i]);

        Parameters = @params;
        if (autoBuild)
            (Lines, NativeFunctionPointerFieldName, InvocationName, isProperty, Getter, Setter) = BuildLines(this, ifRequestGetterOrSetter, autoGeneratingForProperty);
        else
        {
            Lines = Array.Empty<string>();
            NativeFunctionPointerFieldName = string.Empty;
            InvocationName = string.Empty;
        }
    }

    private static (string[] lines, string fieldName, string invovationName, bool isProperty, Item? Getter, Item? Setter) BuildLines(
        in MethodData data, Func<PropertyMemberType, (Item method, bool isStatic, string fieldName)>? ifRequestGetterOrSetter, bool autoGeneratingForProperty)
    {
        var (fieldLines, fieldName) = BuildFunctionPointerFieldLines(data);
        var (methodLines, invovationName, isPropetry, getter, setter) = BuildMethodDefintionAndBody(data, fieldName, ifRequestGetterOrSetter, autoGeneratingForProperty);

        var lines = new string[fieldLines.Length + methodLines.Length];
        fieldLines.CopyTo(lines, 0);
        methodLines.CopyTo(lines, fieldLines.Length);

        return (lines, fieldName, invovationName, isPropetry, getter, setter);
    }

    private static (string[] lines, string fieldName) BuildFunctionPointerFieldLines(in MethodData data)
    {
        var temp = data.Data.Name + '_' + data.Data.Symbol;
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

    private static (string[] lines, string invocationName, bool isPropetry, Item? getter, Item? setter) BuildMethodDefintionAndBody(
        in MethodData data, string fieldName, Func<PropertyMemberType, (Item method, bool isStatic, string fieldName)>? ifRequestGetterOrSetter, bool autoGeneratingForProperty)
    {
        if (autoGeneratingForProperty is false)
        {
            var (lines, name) = BuildMethod(data, fieldName);
            return (lines, name, false, null, null);
        }
        else
        {
            var item = data.Data;
            if (data.Parameters.Length is 0 && data.ReturnType.Type is not "void")
            {
                (Item method, bool isStatic, string fieldName)? temp;
                if (item.Name.StartsWith("get"))
                {
                    temp = ifRequestGetterOrSetter?.Invoke(PropertyMemberType.Setter);
                    MethodData? data2 = temp is null ? null : new MethodData(temp.Value.method, data.IsStatic, autoBuild: false);

                    string? fieldName2 = null;
                    if (data2 is not null)
                    {
                        var _temp = data2.Value.Data.Name + '_' + data2.Value.Data.Symbol;
                        StringBuilder builder = new();
                        foreach (var c in _temp)
                            builder.Append(TypeAnalyzer.IsLetterOrUnderline(c) ? c : '_');
                        fieldName2 = builder.ToString();
                    }

                    var (lines, name) = BuildPropetry(data, fieldName, data2, fieldName2);
                    return (lines, name, true, data.Data, temp!.Value.method);
                }
                else if (item.Name.StartsWith("set"))
                {
                    temp = ifRequestGetterOrSetter?.Invoke(PropertyMemberType.Getter);
                    MethodData? data2 = temp is null ? null : new MethodData(temp.Value.method, data.IsStatic, autoBuild: false);

                    string? fieldName2 = null;
                    if (data2 is not null)
                    {
                        var _temp = data2.Value.Data.Name + '_' + data2.Value.Data.Symbol;
                        StringBuilder builder = new();
                        foreach (var c in _temp)
                            builder.Append(TypeAnalyzer.IsLetterOrUnderline(c) ? c : '_');
                        fieldName2 = builder.ToString();
                    }

                    var (lines, name) = BuildPropetry(data2, fieldName2, data, fieldName);
                    return (lines, name, true, data.Data, temp!.Value.method);
                }
            }
            var (_lines, _name) = BuildMethod(data, fieldName);
            return (_lines, _name, false, null, null);
        }
    }

    private static (string[] lines, string invocationName) BuildMethod(in MethodData data, string fieldName)
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
        funcPointerTypeStr.Append(data.ReturnType.Type);

        StringBuilder argListStr = new();
        index = 0;
        foreach (var param in data.Parameters)
        {
            if (index is not 0)
                argListStr.Append(", ");
            argListStr.Append($"{Utils.StrIfTrue("ref ", param.IsByRef)}a{index}");
            ++index;
        }

        var methodDefintion = $"{data.AccessType.ToString().ToLower()} {(data.IsStatic ? "static " : string.Empty)}unsafe {data.ReturnType.Type} {methodName}({paramListStr})";

        var lines = new string[]
        {
            methodDefintion,
            $"{{",
            //    var func = (delegate* unmanaged<[nint,] type1, type2, ...>)fieldName.Value;
            $"    var func = (delegate* unmanaged<{Utils.StrIfFalse("nint", data.IsStatic)}{Utils.StrIfTrue(", ", data.IsStatic is false && funcPointerTypeStr.Length is not 0)}{funcPointerTypeStr}>){fieldName}.Value;",
            //    [return ][ref ] func([this.Pointer], [ref ]a0, [ref ]a1, ...);
            $"    {Utils.StrIfFalse("return ", data.ReturnType.IsVoid)}{Utils.StrIfTrue("ref ", data.ReturnType.IsByRef)}func({Utils.StrIfFalse("this.Pointer", data.IsStatic)}{Utils.StrIfTrue(", ", data.IsStatic is false && argListStr.Length is not 0)}{argListStr});",
            $"}}"
        };

        return (lines, methodName);
    }

    private static (string[] lines, string invocationName) BuildPropetry(in MethodData? getter, string? getterFuncPtrFieldName, in MethodData? setter, string? setterFuncPtrFieldName)
    {
        //if ((getter is null || getterFuncPtrFieldName is null))
        throw new NotImplementedException();
    }
}
