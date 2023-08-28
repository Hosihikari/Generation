using System.Text;

namespace Hosihikari.Generation;

public enum CppFundamentalType
{
    Void = 0,

    Boolean = 1,

    Float = 2,
    Double = 3,

    WChar = 4,

    SChar = 8,
    Int16 = 9,
    Int32 = 10,
    Int64 = 11,

    Char = 16,
    UInt16 = 17,
    UInt32 = 18,
    UInt64 = 19
}

public enum CppTypeEnum
{
    FundamentalType,
    Pointer,
    Ref,
    RValueRef,
    Enum,
    Class,
    Struct,
    Union,
    Array
}

#nullable enable
public class CppTypeNode
{
    public TypeAnalyzer Analyzer;

    public CppTypeNode RootType;

    public CppTypeEnum Type;

    public CppFundamentalType? FundamentalType;

    public string? TypeIdentifier;

    public CppTypeNode? SubType;

    public CppTypeNode[]? TemplateTypes;

    public string[]? Namespaces;

    public bool IsFundamentalType => FundamentalType != null;

    public bool HasTypeIdentifier => TypeIdentifier != null;

    public bool HasSubType => SubType != null;

    public bool IsTemplate => TemplateTypes != null;

    public string TypeIdentifierWithTemplateArgs
    {
        get
        {
            if (TemplateTypes is null)
                return TypeIdentifier ?? throw new NullReferenceException();

            StringBuilder @string = new();
            @string.Append(TypeIdentifier).Append('<');
            uint currentTemplateArgIndex = 0;

            foreach (var type in TemplateTypes)
            {
                if (currentTemplateArgIndex > 0)
                    @string.Append(", ");

                @string.Append(type.TypeIdentifierWithTemplateArgs);
                ++currentTemplateArgIndex;

            }

            @string.Append('>');

            return @string.ToString();
        }
    }

    public CppTypeNode(
        TypeAnalyzer analyzer, CppTypeNode root, CppTypeEnum type, CppFundamentalType? fundamentalType, string? typeIdentifier, CppTypeNode? subType, string[]? namespaces)
    {
        Analyzer = analyzer;
        RootType = root;
        Type = type;
        FundamentalType = fundamentalType;
        TypeIdentifier = typeIdentifier;
        SubType = subType;
        Namespaces = namespaces;
    }

    public CppTypeNode(TypeAnalyzer analyzer)
    {
        Analyzer = analyzer;
        RootType = this;
    }

    public override string ToString()
    {
        return Type switch
        {
            CppTypeEnum.FundamentalType => FundamentalType.ToString()!,
            CppTypeEnum.Pointer => "*",
            CppTypeEnum.Ref => "&",
            CppTypeEnum.Array => $"[]",
            CppTypeEnum.Enum => $"enum {string.Join(".", Namespaces ?? Array.Empty<string>())} {TypeIdentifier}",
            CppTypeEnum.Class => $"class {string.Join(".", Namespaces ?? Array.Empty<string>())} {TypeIdentifier}",
            CppTypeEnum.Struct => $"struct {string.Join(".", Namespaces ?? Array.Empty<string>())} {TypeIdentifier}",
            CppTypeEnum.Union => $"union {string.Join(".", Namespaces ?? Array.Empty<string>())} {TypeIdentifier}",
            _ => "",
        };
    }

    public void ForEach(Action<CppTypeNode, int, bool> action)
    {
        CppTypeNode? current = this;
        int index = 0;
        while (current is not null)
        {
            action(current, index, current.SubType is null);
            index++;
            current = current.SubType;
        }
    }

    public CppTypeNode[] ToArray()
    {
        List<CppTypeNode> nodes = new();
        ForEach((node, _, _) => nodes.Add(node));
        return nodes.ToArray();
    }
}

#nullable enable
public sealed class TypeAnalyzer
{


    public string OriginalType { get; private set; }

    public CppTypeNode CppTypeHandle { get; private set; }

    private TypeAnalyzer(string type)
    {
        OriginalType = type;
        CppTypeHandle = AnalyzeCppType(this, OriginalType);
    }

    public static TypeAnalyzer Analyze(string type)
    {
        return new TypeAnalyzer(type);
    }

    private static CppTypeNode AnalyzeCppType(TypeAnalyzer analyzer, string typeStr)
    {
        var ret = __AnalyzeCppType(analyzer, typeStr);

        CppTypeNode? root = null;
        ret.ForEach((node, index, isroot) =>
        {
            if (isroot)
                root = node;
        });
        ret.ForEach((node, _, _) =>
        {
            node.RootType = root!;
        });
        return ret;
    }

    private static CppTypeNode __AnalyzeCppType(TypeAnalyzer analyzer, string typeStr)
    {
        var ret = new CppTypeNode(analyzer);

        StringBuilder identifierBulider = new();
        uint searchDepth = 0;
        int templateArgsStartIndex = 0, templateArgsEndIndex = 0;


        for (int i = typeStr.Length - 1; i >= 0; --i)
        {
            var c = typeStr[i];

            switch (c)
            {
                case '*':
                    {
                        if (searchDepth == 0)
                        {
                            ret.Type = CppTypeEnum.Pointer;
                            var subTypeStr = typeStr[..i].Trim();
                            if (subTypeStr.Length > 0)
                            {
                                ret.SubType = __AnalyzeCppType(analyzer, subTypeStr);
                            }
                            return ret;
                        }
                    }
                    goto default;

                case '&':
                    {
                        if (searchDepth == 0)
                        {
                            if (typeStr[--i] is '&')
                                ret.Type = CppTypeEnum.RValueRef;
                            else
                                ret.Type = CppTypeEnum.Ref;


                            var subTypeStr = typeStr[..i].Trim();
                            if (subTypeStr.Length > 0)
                            {
                                ret.SubType = __AnalyzeCppType(analyzer, subTypeStr);
                            }
                            return ret;
                        }
                    }
                    goto default;

                case '[':
                    {
                        if (searchDepth == 0)
                        {
                            if (ret.Type is not CppTypeEnum.Array)
                                throw new InvalidDataException();
                        }
                    }
                    goto default;

                case ']':
                    {
                        if (searchDepth == 0)
                        {
                            if (typeStr[i - 1] is not '[')
                                throw new InvalidDataException();

                            ret.Type = CppTypeEnum.Array;
                            var subTypeStr = typeStr[..(i - 1)].Trim();
                            if (subTypeStr.Length > 0)
                            {
                                ret.SubType = __AnalyzeCppType(analyzer, subTypeStr);
                            }
                            return ret;
                        }
                    }
                    goto default;

                case '>':
                    {
                        if (searchDepth == 0)
                            templateArgsEndIndex = i;
                        ++searchDepth;
                    }
                    continue;

                case '<':
                    {
                        --searchDepth;
                        if (searchDepth == 0)
                            templateArgsStartIndex = i;
                    }
                    continue;

                case 'c':
                    {
                        if (searchDepth == 0)
                        {
                            if (i > 0 && IsLetterOrUnderline(typeStr[i - 1]) || i == 0)
                                goto default;
                            else
                            {
                                if (typeStr.Length - i >= 5 && typeStr[i..(i + 5)] is "const")
                                {
                                    typeStr = typeStr.Remove(i, 5).Trim();
                                    identifierBulider.Clear();

                                    i = typeStr.Length;
                                }
                                else goto default;
                            }
                        }
                    }
                    continue;

                default:
                    {
                        if (searchDepth == 0)
                        {
                            identifierBulider.Append(c);
                        }
                    }
                    continue;
            }
        }
        ret.TypeIdentifier = new string(identifierBulider.ToString().Reverse().ToArray());

        ret.FundamentalType = ret.TypeIdentifier switch
        {
            "void" => CppFundamentalType.Void,
            "bool" => CppFundamentalType.Boolean,
            "float" => CppFundamentalType.Float,
            "double" => CppFundamentalType.Double,
            "wchar_t" => CppFundamentalType.WChar,
            "char" => CppFundamentalType.SChar,
            "short" or "INT16" => CppFundamentalType.Int16,
            "int" or "long" or "INT32" => CppFundamentalType.Int32,
            "__int64" or "long long" or "INT64" => CppFundamentalType.Int64,
            _ => null
        };

        if (ret.FundamentalType != null)
        {
            ret.Type = CppTypeEnum.FundamentalType;
            return ret;
        }

        if (ret.TypeIdentifier.StartsWith("union "))
        {
            ret.Type = CppTypeEnum.Union;
            ret.TypeIdentifier = ret.TypeIdentifier.Remove(0, "union ".Length);
        }
        else if (ret.TypeIdentifier.StartsWith("class "))
        {
            ret.Type = CppTypeEnum.Class;
            ret.TypeIdentifier = ret.TypeIdentifier.Remove(0, "class ".Length);
        }
        else if (ret.TypeIdentifier.StartsWith("struct "))
        {
            ret.Type = CppTypeEnum.Struct;
            ret.TypeIdentifier = ret.TypeIdentifier.Remove(0, "struct ".Length);
        }
        else if (ret.TypeIdentifier.StartsWith("enum "))
        {
            ret.Type = CppTypeEnum.Enum;
            if (ret.TypeIdentifier.StartsWith("enum class "))
            {
                ret.TypeIdentifier = ret.TypeIdentifier.Remove(0, "enum class ".Length);
            }
            else
            {
                ret.TypeIdentifier = ret.TypeIdentifier.Remove(0, "enum ".Length);
            }
        }
        else
        {
            ret.Type = CppTypeEnum.Class;
        }

        if (templateArgsStartIndex != 0 || templateArgsEndIndex != 0)
        {
            var templateArgs = typeStr.Substring(
                templateArgsStartIndex + 1, templateArgsEndIndex - templateArgsStartIndex - 1);

            List<int> indexs = new();

            uint _searchDepth = 0;

            for (int i = 0; i < templateArgs.Length; ++i)
            {
                switch (templateArgs[i])
                {
                    case '<':
                        ++_searchDepth;
                        break;
                    case '>':
                        --_searchDepth;
                        break;
                    case ',':
                        if (_searchDepth == 0)
                        {
                            indexs.Add(i);
                        }
                        break;
                    default: continue;
                }
            }

            indexs.Add(templateArgs.Length);

            ret.TemplateTypes = new CppTypeNode[indexs.Count];

            int currentIndex = -2;

            for (int i = 0; i < indexs.Count; ++i)
            {
                ret.TemplateTypes[i] = __AnalyzeCppType(analyzer, templateArgs.Substring(currentIndex + 2, indexs[i] - currentIndex - 2).Trim());
                currentIndex = indexs[i];
            }
        }

        var arr = ret.TypeIdentifier.Split("::");
        ret.TypeIdentifier = arr.LastOrDefault();
        ret.Namespaces = arr.Length > 0 ? arr.Take(arr.Length - 1).ToArray() : null;

        return ret;
    }

    private static bool IsLetterOrUnderline(char c)
    {
        return c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c == '_';
    }

    private static bool IsDigit(char c)
    {
        return c >= '0' && c <= '9';
    }
}
