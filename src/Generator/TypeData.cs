using Hosihikari.Generation.Parser;
using System.Text;
using static Hosihikari.Generation.Utils.OriginalData;

namespace Hosihikari.Generation.Generator;

public readonly struct TypeData
{
    public TypeData(in Class.Item.TypeData type)
    {
        if (type.Name.Contains('(') || type.Name.Contains(')'))
        {
            throw new NotSupportedException();
        }

        TypeAnalyzer analyzer = TypeAnalyzer.Analyze(type.Name);
        CppTypeNode handle = analyzer.CppTypeHandle;

        Analyzer = analyzer;

        if (string.IsNullOrWhiteSpace(handle.RootType.TypeIdentifier))
        {
            throw new InvalidDataException();
        }

        if (handle.Type is not CppTypeEnum.VarArgs
            && handle.RootType.IsFundamentalType is false &&
            TypeAnalyzer.IsLegalName(handle.RootType.TypeIdentifier!) is false)
        {
            throw new InvalidDataException();
        }

        Type = BuildManagedType(analyzer);

        string[]? namespaces = handle.RootType.Namespaces;

        Namespaces = namespaces ?? Array.Empty<string>();
        TypeIdentifier = handle.RootType.TypeIdentifier!;
        FullTypeIdentifier =
            $"{string.Join('.', Namespaces)}{Utils.StrIfTrue(".", Namespaces.Count is not 0)}{TypeIdentifier}";
    }

    public readonly TypeAnalyzer Analyzer;
    private readonly string Type;
    public readonly IReadOnlyList<string> Namespaces;
    public readonly string TypeIdentifier;
    public readonly string FullTypeIdentifier;

    public override string ToString()
    {
        return Type;
    }

    private static string BuildManagedType(TypeAnalyzer analyzer)
    {
        IEnumerable<CppTypeNode> arr = analyzer.CppTypeHandle.ToArray().Reverse();
        StringBuilder builder = new();

        bool isIcppInstance;
        bool temp = false;

        foreach (CppTypeNode item in arr)
        {
            isIcppInstance = temp;
            temp = item.Type is CppTypeEnum.Class or CppTypeEnum.Struct;

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
                        _ => throw new InvalidOperationException()
                    });
                    break;
                case CppTypeEnum.Pointer:
                    if (isIcppInstance)
                    {
                        builder.Insert(0, "Pointer<").Append('>');
                    }
                    else
                    {
                        builder.Append('*');
                    }

                    break;
                case CppTypeEnum.RValueRef:
                case CppTypeEnum.Ref:
                    if (isIcppInstance)
                    {
                        builder.Insert(0, "Reference<").Append('>');
                    }
                    else
                    {
                        builder.Insert(0, "ref ");
                    }

                    break;
                case CppTypeEnum.Enum:
                    builder.Append("int");
                    break;

                case CppTypeEnum.Class:
                    {
                        string namespaces = string.Join('.', item.Namespaces ?? Array.Empty<string>());
                        builder.Append(
                            $"{namespaces}{(string.IsNullOrWhiteSpace(namespaces) ? string.Empty : ".")}{item.TypeIdentifier}");
                    }
                    break;
                case CppTypeEnum.Struct:
                    {
                        string namespaces = string.Join('.', item.Namespaces ?? Array.Empty<string>());
                        builder.Append(
                            $"{namespaces}{(string.IsNullOrWhiteSpace(namespaces) ? string.Empty : ".")}{item.TypeIdentifier}");
                    }
                    break;
                case CppTypeEnum.Union:
                    {
                        string namespaces = string.Join('.', item.Namespaces ?? Array.Empty<string>());
                        builder.Append(
                            $"{namespaces}{(string.IsNullOrWhiteSpace(namespaces) ? string.Empty : ".")}{item.TypeIdentifier}");
                    }
                    break;
                case CppTypeEnum.Array:
                    throw new NotImplementedException();
                case CppTypeEnum.VarArgs:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        return builder.ToString();
    }
}