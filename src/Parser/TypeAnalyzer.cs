using System.Text;

namespace Hosihikari.Generation.Parser;

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
    Array,
    VarArgs
}

public class CppTypeNode
{
    public CppFundamentalType? FundamentalType;

    public string[]? Namespaces;

    public string? OriginalTypeString;

    public CppTypeNode RootType;

    public CppTypeNode? SubType;

    public CppTypeNode[]? TemplateTypes;

    public CppTypeEnum Type;

    public string? TypeIdentifier;

    public string? TypeIdentifierWithTemplateArgs;

    public CppTypeNode()
    {
        RootType = this;
    }

    public bool IsFundamentalType => FundamentalType is not null;

    public bool IsTemplate => TemplateTypes is not null;

    public override string ToString()
    {
        return Type switch
        {
            CppTypeEnum.FundamentalType => FundamentalType.ToString()!,
            CppTypeEnum.Pointer => "*",
            CppTypeEnum.Ref => "&",
            CppTypeEnum.Array => "[]",
            CppTypeEnum.Enum => $"enum {string.Join(".", Namespaces ?? Array.Empty<string>())} {TypeIdentifier}",
            CppTypeEnum.Class => $"class {string.Join(".", Namespaces ?? Array.Empty<string>())} {TypeIdentifier}",
            CppTypeEnum.Struct => $"struct {string.Join(".", Namespaces ?? Array.Empty<string>())} {TypeIdentifier}",
            CppTypeEnum.Union => $"union {string.Join(".", Namespaces ?? Array.Empty<string>())} {TypeIdentifier}",
            _ => string.Empty
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

    public IEnumerable<CppTypeNode> ToArray()
    {
        List<CppTypeNode> nodes = [];
        ForEach((node, _, _) => nodes.Add(node));
        return nodes.ToArray();
    }
}

public sealed class TypeAnalyzer
{
    private TypeAnalyzer(string type)
    {
        OriginalType = type;
        CppTypeHandle = AnalyzeCppType(OriginalType);
    }


    private string OriginalType { get; }

    public CppTypeNode CppTypeHandle { get; private set; }

    public static TypeAnalyzer Analyze(string type)
    {
        return new(type);
    }

    private static CppTypeNode AnalyzeCppType(string typeStr)
    {
        CppTypeNode ret = __AnalyzeCppType(typeStr);

        CppTypeNode? root = null;
        ret.ForEach((node, _, isroot) =>
        {
            if (isroot)
            {
                root = node;
            }
        });
        ret.ForEach((node, _, _) => { node.RootType = root!; });
        return ret;
    }

    private static CppTypeNode __AnalyzeCppType(string typeStr)
    {
        CppTypeNode ret = new()
        {
            OriginalTypeString = typeStr
        };

        if (typeStr is "...")
        {
            ret.Type = CppTypeEnum.VarArgs;
            ret.TypeIdentifier = typeStr;
            return ret;
        }

        StringBuilder identifierBulider = new();
        int templateArgsStartIndex = 0, templateArgsEndIndex = 0;


        for (int i = typeStr.Length - 1, searchDepth = 0; i >= 0; --i)
        {
            char c = typeStr[i];

            switch (c)
            {
                case '*':
                    {
                        if (searchDepth is 0)
                        {
                            ret.Type = CppTypeEnum.Pointer;
                            string subTypeStr = typeStr[..i].Trim();
                            if (subTypeStr.Length > 0)
                            {
                                ret.SubType = __AnalyzeCppType(subTypeStr);
                            }

                            return ret;
                        }
                    }
                    goto default;

                case '&':
                    {
                        if (searchDepth is 0)
                        {
                            if (typeStr[i - 1] is '&')
                            {
                                ret.Type = CppTypeEnum.RValueRef;
                                --i;
                            }
                            else
                            {
                                ret.Type = CppTypeEnum.Ref;
                            }


                            string subTypeStr = typeStr[..i].Trim();
                            if (subTypeStr.Length > 0)
                            {
                                ret.SubType = __AnalyzeCppType(subTypeStr);
                            }

                            return ret;
                        }
                    }
                    goto default;

                case '[':
                    {
                        if (searchDepth is 0)
                        {
                            if (ret.Type is not CppTypeEnum.Array)
                            {
                                throw new InvalidDataException();
                            }
                        }
                    }
                    goto default;

                case ']':
                    {
                        if (searchDepth is 0)
                        {
                            if (typeStr[i - 1] is not '[')
                            {
                                throw new InvalidDataException();
                            }

                            ret.Type = CppTypeEnum.Array;
                            string subTypeStr = typeStr[..(i - 1)].Trim();
                            if (subTypeStr.Length > 0)
                            {
                                ret.SubType = __AnalyzeCppType(subTypeStr);
                            }

                            return ret;
                        }
                    }
                    goto default;

                case '>':
                    {
                        if (searchDepth is 0)
                        {
                            templateArgsEndIndex = i;
                        }

                        ++searchDepth;
                    }
                    continue;

                case '<':
                    {
                        --searchDepth;
                        if (searchDepth is 0)
                        {
                            templateArgsStartIndex = i;
                        }
                    }
                    continue;

                case 'c':
                    {
                        if (searchDepth is 0)
                        {
                            if (((i > 0) && IsLetterOrUnderline(typeStr[i - 1])) || i is 0)
                            {
                                goto default;
                            }

                            if (((typeStr.Length - i) >= 5) && typeStr[i..(i + 5)] is "const")
                            {
                                typeStr = typeStr.Remove(i, 5).Trim();
                                identifierBulider.Clear();

                                i = typeStr.Length;
                            }
                            else
                            {
                                goto default;
                            }
                        }
                    }
                    continue;

                default:
                    {
                        if (searchDepth is 0)
                        {
                            identifierBulider.Append(c);
                        }
                    }
                    continue;
            }
        }

        ret.TypeIdentifier = new(identifierBulider.ToString().Reverse().ToArray());


        string typeId;
        bool isUnsigned = false, isSigned = false;
        if (ret.TypeIdentifier.StartsWith("unsigned "))
        {
            isUnsigned = true;
        }
        else if (ret.TypeIdentifier.StartsWith("signed "))
        {
            isSigned = true;
        }


        if (isSigned)
        {
            typeId = ret.TypeIdentifier["signed ".Length..];
        }
        else if (isUnsigned)
        {
            typeId = ret.TypeIdentifier["unsigned ".Length..];
        }
        else
        {
            typeId = ret.TypeIdentifier;
        }


        ret.FundamentalType = typeId switch
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

        if (isSigned)
        {
            ret.FundamentalType -= 8;
        }
        else if (isUnsigned)
        {
            ret.FundamentalType += 8;
        }

        if (ret.FundamentalType is not null)
        {
            ret.Type = CppTypeEnum.FundamentalType;
            return ret;
        }

        string typeIdentifier = ret.TypeIdentifier;
        ret.Type = typeIdentifier.StartsWith("union ") ? CppTypeEnum.Union :
            typeIdentifier.StartsWith("class ") ? CppTypeEnum.Class :
            typeIdentifier.StartsWith("struct ") ? CppTypeEnum.Struct :
            typeIdentifier.StartsWith("enum class ") ? CppTypeEnum.Enum :
            typeIdentifier.StartsWith("enum ") ? CppTypeEnum.Enum : CppTypeEnum.Class;

        if (ret.Type != CppTypeEnum.FundamentalType)
        {
            ret.TypeIdentifier = typeIdentifier.Remove(0, ret.Type.ToString().Length + 1);
        }

        string templateArgs = string.Empty;
        if (templateArgsStartIndex != 0 || templateArgsEndIndex != 0)
        {
            templateArgs = typeStr.Substring(templateArgsStartIndex + 1, templateArgsEndIndex - templateArgsStartIndex - 1);

            var indexs = new List<int>(templateArgs.Length / 2); // Pre-allocate the size

            for (int i = 0, searchDepth = 0; i < templateArgs.Length; ++i)
            {
                switch (templateArgs[i])
                {
                    case '<':
                        ++searchDepth;
                        break;
                    case '>':
                        --searchDepth;
                        break;
                    case ',' when searchDepth == 0:
                        indexs.Add(i);
                        break;
                }
            }

            indexs.Add(templateArgs.Length);

            var templateArgsSpan = templateArgs.AsSpan(); // Use Span<T> for substring operations

            var templateTypes = new CppTypeNode[indexs.Count];

            int currentIndex = -2;

            for (int i = 0; i < indexs.Count; ++i)
            {
                templateTypes[i] = __AnalyzeCppType(templateArgsSpan.Slice(currentIndex + 2, indexs[i] - currentIndex - 2).Trim().ToString());
                currentIndex = indexs[i];
            }
        }

        var arr = ret.TypeIdentifier.Split("::");
        ret.TypeIdentifier = arr.LastOrDefault();
        ret.Namespaces = arr.Length > 0 ? arr.Take(arr.Length - 1).ToArray() : null;

        ret.TypeIdentifierWithTemplateArgs = ret.TypeIdentifier;

        if (!string.IsNullOrEmpty(templateArgs))
        {
            ret.TypeIdentifierWithTemplateArgs += $"<{templateArgs}>";
        }

        return ret;
    }

    public static bool IsLetterOrUnderline(char c)
    {
        return c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or '_';
    }

    private static bool IsDigit(char c)
    {
        return c is >= '0' and <= '9';
    }

    public static unsafe bool IsLegalName(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return false;
        }

        fixed (char* ptr = typeName)
        {
            if (IsLetterOrUnderline(*ptr) is false)
            {
                return false;
            }

            for (int i = 1; i < typeName.Length; ++i)
            {
                if ((IsLetterOrUnderline(ptr[i]) || IsDigit(ptr[i]) || ptr[i] is '.') is false)
                {
                    return false;
                }
            }
        }

        return true;
    }
}