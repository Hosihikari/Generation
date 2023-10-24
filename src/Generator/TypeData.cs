using Hosihikari.Utils;
using Mono.Cecil;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using static Hosihikari.Utils.OriginalData;

namespace Hosihikari.Generation.Generator;

public readonly struct TypeData
{

    public bool IsVoid => Analyzer.CppTypeHandle.FundamentalType is CppFundamentalType.Void;

    public TypeData(in Class.Item.TypeData type)
    {
        if (type.Name.Contains('(') || type.Name.Contains(')'))
            throw new NotSupportedException();

        var analyzer = TypeAnalyzer.Analyze(type.Name);
        var handle = analyzer.CppTypeHandle;

        if (handle.RootType.IsTemplate)
            throw new NotImplementedException();

        Analyzer = analyzer;

        if (string.IsNullOrWhiteSpace(Analyzer.CppTypeHandle.RootType.TypeIdentifier))
            throw new InvalidDataException();

        if (TypeAnalyzer.IsLegalName(Analyzer.CppTypeHandle.RootType.TypeIdentifier!) is false)
            throw new InvalidDataException();

        (Type, IsByRef) = BuildManagedType(analyzer);

        var namespaces = Analyzer.CppTypeHandle.RootType.Namespaces;

        Namespaces = namespaces ?? Array.Empty<string>();
        TypeIdentifier = Analyzer.CppTypeHandle.RootType.TypeIdentifier!;
        FullTypeIdentifier = $"{string.Join('.', Namespaces)}{Utils.StrIfTrue(".", Namespaces.Count is not 0)}{TypeIdentifier}";
    }

    public readonly TypeAnalyzer Analyzer;
    public readonly string Type;
    public readonly bool IsByRef;
    public readonly IReadOnlyList<string> Namespaces;
    public readonly string TypeIdentifier;
    public readonly string FullTypeIdentifier;

    public override string ToString() => Type;

    private static (string type, bool isByRef) BuildManagedType(TypeAnalyzer analyzer)
    {
        var arr = analyzer.CppTypeHandle.ToArray().Reverse();
        var builder = new StringBuilder();

        bool isIcppInstance;
        bool isByRef = false;
        bool temp = false;

        foreach (var item in arr)
        {
            isIcppInstance = temp;
            temp = false;

            if (item.Type is CppTypeEnum.Class or CppTypeEnum.Struct)
                temp = true;

            switch (item.Type)
            {
                case CppTypeEnum.FundamentalType:
                    builder.Append(item.FundamentalType!.Value switch
                    {
                        CppFundamentalType.Void => "void",
                        CppFundamentalType.Boolean => "bool",
                        CppFundamentalType.Float => "float",
                        CppFundamentalType.Double => "double",
                        CppFundamentalType.WChar => "char",
                        CppFundamentalType.SChar => "sbyte",
                        CppFundamentalType.Int16 => "short",
                        CppFundamentalType.Int32 => "int",
                        CppFundamentalType.Int64 => "long",
                        CppFundamentalType.Char => "byte",
                        CppFundamentalType.UInt16 => "ushort",
                        CppFundamentalType.UInt32 => "uint",
                        CppFundamentalType.UInt64 => "ulong",
                        _ => throw new InvalidOperationException(),
                    });
                    break;
                case CppTypeEnum.Pointer:
                    if (isIcppInstance)
                        builder.Insert(0, "Pointer<").Append('>');
                    else
                        builder.Append('*');
                    break;
                case CppTypeEnum.RValueRef:
                case CppTypeEnum.Ref:
                    if (isIcppInstance)
                        builder.Insert(0, "Reference<").Append('>');
                    else
                    {
                        builder.Insert(0, "ref ");
                        isByRef = true;
                    }
                    break;
                case CppTypeEnum.Enum:
                    builder.Append("int");
                    break;

                case CppTypeEnum.Class:
                    {
                        var namespaces = string.Join('.', item.Namespaces ?? Array.Empty<string>());
                        builder.Append($"{namespaces}{(string.IsNullOrWhiteSpace(namespaces) ? string.Empty : ".")}{item.TypeIdentifier}");
                    }
                    break;
                case CppTypeEnum.Struct:
                    {
                        var namespaces = string.Join('.', item.Namespaces ?? Array.Empty<string>());
                        builder.Append($"{namespaces}{(string.IsNullOrWhiteSpace(namespaces) ? string.Empty : ".")}{item.TypeIdentifier}");
                    }
                    break;
                case CppTypeEnum.Union:
                    {
                        var namespaces = string.Join('.', item.Namespaces ?? Array.Empty<string>());
                        builder.Append($"{namespaces}{(string.IsNullOrWhiteSpace(namespaces) ? string.Empty : ".")}{item.TypeIdentifier}");
                    }
                    break;
                case CppTypeEnum.Array:
                    throw new NotImplementedException();
            }
        }

        return (builder.ToString(), isByRef);
    }
}
