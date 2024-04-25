using System.Diagnostics.CodeAnalysis;

namespace Hosihikari.Generation.CppParser;

public static class CppTypeParser
{
    public static bool TryParse(string type, [NotNullWhen(true)] out CppType? result)
    {
        result = null;

        type = type.Trim();

        if (type.Contains('(') || type.Contains(')'))
            return false;// TODO

        if (type is "...")
        {
            result = new()
            {
                Type = CppTypeEnum.VarArgs,
                TypeIdentifier = type,
                OriginalTypeString = type
            };
            return true;
        }

        return TryParseCppTypeNodes(type, out result);
    }

    /// <summary>
    ///     Tries to get the C++ type from the input type string.
    /// </summary>
    /// <param name="type">The input type string.</param>
    /// <param name="result">The tuple containing the <see cref="CppTypeEnum" /> and the handled type.</param>
    /// <returns>True if the C++ type is successfully obtained, otherwise false.</returns>
    public static bool TryGetCppType(string type, out (CppTypeEnum type, string handledType) result)
    {
        const string EnumClass = "enum class ";
        const string Enum = "enum ";
        const string Union = "union ";
        const string Struct = "struct ";
        const string Class = "class ";

        type = type.Trim();
        result = default;

        if (type.StartsWith(EnumClass))
        {
            result.handledType = type[EnumClass.Length..];
            result.type = CppTypeEnum.Enum;
            return true;
        }

        if (type.StartsWith(Class))
        {
            result.handledType = type[Class.Length..];
            result.type = CppTypeEnum.Class;
            return true;
        }

        if (type.StartsWith(Struct))
        {
            result.handledType = type[Struct.Length..];
            result.type = CppTypeEnum.Struct;
            return true;
        }

        if (type.StartsWith(Enum))
        {
            result.handledType = type[Enum.Length..];
            result.type = CppTypeEnum.Enum;
            return true;
        }

        if (type.StartsWith(Union))
        {
            result.handledType = type[Union.Length..];
            result.type = CppTypeEnum.Union;
            return true;
        }

        result.handledType = type;

        return false;
    }


    /// <summary>
    ///     Tries to extract the template type from the input string.
    /// </summary>
    /// <param name="typeWithoutPrefix">The input string without the prefix.</param>
    /// <param name="result">The extracted template type and template arguments.</param>
    /// <returns>Returns true if the template type was successfully extracted, otherwise false.</returns>
    public static bool TryGetTemplateType(string typeWithoutPrefix,
        out (string[] templateArgs, string typeWithoutTemplateArgs) result)
    {
        // Initialize the result
        result = default;
        typeWithoutPrefix = typeWithoutPrefix.Trim();

        // Initialize search variables
        uint searchDepth = 0;
        int templateArgsStartIndex = 0, templateArgsEndIndex = 0;
        List<int> templateArgSeparatorsIndexList = [];

        // Iterate through the input string to extract template type and arguments
        for (int i = 0; i < typeWithoutPrefix.Length; i++)
        {
            switch (typeWithoutPrefix[i])
            {
                case '<':
                    // If search depth is 0, set the start index of template arguments
                    if (searchDepth is 0)
                    {
                        templateArgsStartIndex = i;
                    }

                    checked
                    {
                        searchDepth++;
                    }

                    continue;

                case '>':
                    checked
                    {
                        searchDepth--;
                    }

                    // If search depth is 0, set the end index of template arguments
                    if (searchDepth is 0)
                    {
                        templateArgsEndIndex = i;
                    }

                    continue;

                case ',':
                    // If search depth is 1, add the index to the list of template argument separators
                    if (searchDepth is 1)
                    {
                        templateArgSeparatorsIndexList.Add(i);
                    }

                    continue;
            }
        }

        // If end index of template arguments is 0, return false
        if (templateArgsEndIndex is 0)
        {
            return false;
        }

        templateArgSeparatorsIndexList.Add(templateArgsEndIndex);

        // Extract individual template arguments
        string[] templateArgs = new string[templateArgSeparatorsIndexList.Count];

        int prevIndex = templateArgsStartIndex + 1;

        for (int i = 0; i < templateArgSeparatorsIndexList.Count; i++)
        {
            templateArgs[i] = typeWithoutPrefix[prevIndex..templateArgSeparatorsIndexList[i]].Trim();
            prevIndex = templateArgSeparatorsIndexList[i] + 1;
        }

        // Set the result and return true
        result.typeWithoutTemplateArgs
            = typeWithoutPrefix.Remove(templateArgsStartIndex, (templateArgsEndIndex - templateArgsStartIndex) + 1);

        result.templateArgs = templateArgs;
        return true;
    }

    /// <summary>
    ///     Tries to parse CppType nodes from the given string.
    /// </summary>
    /// <param name="original">The original string to parse.</param>
    /// <param name="cppType">The parsed CppType if successful.</param>
    /// <returns>True if parsing was successful, false otherwise.</returns>
    public static bool TryParseCppTypeNodes(string original, [NotNullWhen(true)] out CppType? cppType)
    {
        // Initialize variables
        string type = original;
        cppType = null;

        // Check if the input string is empty
        if (string.IsNullOrWhiteSpace(type))
        {
            return false;
        }

        // Trim the input string
        type = type.Trim();
        if (TryGetCppType(type, out (CppTypeEnum, string) result))
        {
            type = result.Item2;
        }
        else
        {
            result.Item1 = CppTypeEnum.Class;
        }

        // Parse the CppType nodes
        CppType? latest = null;
        bool constSigned = false;
        List<CppType> cppTypes = [];
        CppTypeParseContext ctx = new() { Type = type };

        // Iterate through the characters in the string
        while (!ctx.IsEnd)
        {
            char c = ctx.Current;
            CppType? temp;

            // This switch statement processes different characters and performs corresponding actions
            // based on the input character.
            switch (c)
            {
                // If the character is '*', try to pack a pointer type and add it to cppTypes list
                case '*':
                    if (!TryPackPointerType(original, in ctx, out temp))
                    {
                        return false;
                    }

                    cppTypes.Add(temp);
                    latest = temp;

                    goto SKIP_WHITESPACE;

                // If the character is '&', try to pack a reference type and add it to cppTypes list
                case '&':
                    if (!TryPackReferenceType(original, in ctx, out temp))
                    {
                        return false;
                    }

                    cppTypes.Add(temp);
                    latest = temp;

                    goto SKIP_WHITESPACE;

                // If the character is ']', try to pack an array type and add it to cppTypes list
                case ']':
                    if (!TryPackArrayType(original, in ctx, out temp))
                    {
                        return false;
                    }

                    cppTypes.Add(temp);
                    latest = temp;

                    goto SKIP_WHITESPACE;

                // If the character is 't' and it is const, set IsConst flag for the latest type
                case 't':
                    if (!IsConst(in ctx))
                    {
                        goto default;
                    }

                    ctx.SkipWhitespace();

                    if (latest is null)
                    {
                        constSigned = true;
                    }
                    else
                    {
                        latest.IsConst = true;
                    }

                    continue;

                // Default case handles non-matching characters
                default:
                    if (char.IsWhiteSpace(c))
                    {
                        goto SKIP_WHITESPACE;
                    }

                    if (!TryPackType(original, in ctx, out temp, result.Item1))
                    {
                        return false;
                    }

                    cppTypes.Add(temp);
                    latest = temp;

                    break;

                SKIP_WHITESPACE:
                    ctx.SkipWhitespace();
                    break;
            }

            if (!constSigned)
            {
                continue;
            }

            // Handle const modifiers
            constSigned = false;
            if (latest is not null)
            {
                latest.IsConst = true;
            }
        }

        // Link the parsed CppType nodes
        cppType = LinkTypes(cppTypes);
        return cppType is not null && string.IsNullOrWhiteSpace(cppType.RootType.TypeIdentifier) is false;
    }


    /// <summary>
    ///     Tries to pack a pointer type from the original string based on the context provided.
    /// </summary>
    /// <param name="original">The original string to parse.</param>
    /// <param name="ctx">The context for parsing.</param>
    /// <param name="type">The parsed CppType if successful.</param>
    /// <returns>True if a pointer type was successfully packed, false otherwise.</returns>
    public static bool TryPackPointerType(string original, in CppTypeParseContext ctx,
        [NotNullWhen(true)] out CppType? type)
    {
        // Initialize the output parameter
        type = null;

        // Check if the current character is a pointer symbol '*'
        char c = ctx.Current;
        if (c is not '*')
        {
            return false;
        }

        // Move to the next character in the context
        ctx.MoveNext();

        // Create a new CppType for pointer type
        CppType cppType = new()
        {
            OriginalTypeString = original,
            Type = CppTypeEnum.Pointer,
            TypeIdentifier = "*"
        };

        // Assign the created CppType to the output parameter
        type = cppType;

        return true;
    }


    /// <summary>
    ///     Tries to pack a reference type based on the input string and context.
    /// </summary>
    /// <param name="original">The original type string.</param>
    /// <param name="ctx">The context for parsing.</param>
    /// <param name="type">The packed CppType if successful, null otherwise.</param>
    /// <returns>True if packing was successful, false otherwise.</returns>
    public static bool TryPackReferenceType(string original, in CppTypeParseContext ctx,
        [NotNullWhen(true)] out CppType? type)
    {
        // Initialize the output type as null
        type = null;

        // Check if the current character is '&'
        char c = ctx.Current;
        if (c is not '&')
        {
            return false;
        }

        // Move to the next character
        ctx.MoveNext();

        // Check if the next character is '&'
        if (ctx.Current is '&')
        {
            ctx.MoveNext();
            // Create a new CppType for RValueRef
            type = new()
            {
                OriginalTypeString = original,
                Type = CppTypeEnum.RValueRef,
                TypeIdentifier = "&&"
            };
            return true;
        }

        // Create a new CppType for Ref
        type = new()
        {
            OriginalTypeString = original,
            Type = CppTypeEnum.Ref,
            TypeIdentifier = "&"
        };
        return true;
    }


    /// <summary>
    ///     Tries to pack an array type from the original type string.
    /// </summary>
    /// <param name="original">The original type string.</param>
    /// <param name="ctx">The CppTypeParseContext.</param>
    /// <param name="type">The packed array type, if successful.</param>
    /// <returns>True if the array type was successfully packed, otherwise false.</returns>
    public static bool TryPackArrayType(string original, in CppTypeParseContext ctx,
        [NotNullWhen(true)] out CppType? type)
    {
        type = null;

        // Check if the current character is not ']'
        if (ctx.Current is not ']')
        {
            return false;
        }

        // Check if the next character is not '['
        if (ctx.Next is not '[')
        {
            return false;
        }

        // Skip the next two characters
        ctx.Skip(2);

        // Pack the array type
        type = new()
        {
            OriginalTypeString = original,
            Type = CppTypeEnum.Array,
            TypeIdentifier = "[]"
        };

        return true;
    }


    /// <summary>
    ///     Tries to pack the type based on the provided original type string and context.
    /// </summary>
    /// <param name="original">The original type string to pack.</param>
    /// <param name="ctx">The context containing the parsing information.</param>
    /// <param name="type">The packed CppType if successful, otherwise null.</param>
    /// <param name="cppType">The CppTypeEnum representing the type.</param>
    /// <returns>True if the packing was successful, false otherwise.</returns>
    public static bool TryPackType(string original, in CppTypeParseContext ctx, [NotNullWhen(true)] out CppType? type,
        CppTypeEnum cppType)
    {
        // Calculate the length to extract the type
        int length = ctx.Index + 1;

        ReadOnlySpan<char> span = ctx.Type[..length];
        string typeStr = span.ToString();

        // Move the context index
        ctx.Skip(length);

        if (TryGetFundamentalType(typeStr, out CppFundamentalType? fundamentalType))
        {
            type = new()
            {
                OriginalTypeString = original,
                Type = CppTypeEnum.FundamentalType,
                TypeIdentifier = typeStr,
                FundamentalType = fundamentalType
            };
            return true;
        }

        // Create a new CppType instance with the necessary information
        type = new()
        {
            OriginalTypeString = original,
            Type = cppType,
            TypeIdentifier = typeStr
        };

        if (TryGetTemplateType(typeStr, out (string[] templateArgs, string typeWithoutTemplateArgs) templateArgs))
        {
            // Initialize the template types array and update the type identifier
            type.TemplateTypes = new CppType[templateArgs.templateArgs.Length];
            type.TypeIdentifierWithTemplateArgs = type.TypeIdentifier;
            type.TypeIdentifier = templateArgs.typeWithoutTemplateArgs;

            // Parse and store each template argument
            for (int i = 0; i < templateArgs.templateArgs.Length; i++)
            {
                string args = templateArgs.templateArgs[i];

                // Try to parse the template argument
                if (!TryParseCppTypeNodes(args, out CppType? temp))
                {
                    return false;
                }

                type.TemplateTypes[i] = temp;
            }
        }

        var namespaces = type.TypeIdentifier.Split("::");
        if (namespaces.Length > 1)
        {
            type.Namespaces = namespaces[..^1];
            type.TypeIdentifier = namespaces[^1];
        }


        return true;
    }


    /// <summary>
    ///     Check if the given CppTypeParseContext represents a const keyword.
    /// </summary>
    /// <param name="ctx">The CppTypeParseContext to be checked.</param>
    /// <returns>True if the CppTypeParseContext represents a const keyword, otherwise false.</returns>
    public static bool IsConst(in CppTypeParseContext ctx)
    {
        int length = ctx.Index + 1;
        if ((length <= 5) || ctx[length - 1] is not 't' || ctx[length - 2] is not 's' || ctx[length - 3] is not 'n' ||
            ctx[length - 4] is not 'o' ||
            ctx[length - 5] is not 'c')
        {
            return false;
        }

        ctx.Skip(5);
        return true;
    }

    /// <summary>
    ///     Tries to get the fundamental C++ type based on the input string.
    /// </summary>
    /// <param name="type">The input string representing the C++ type.</param>
    /// <param name="result">
    ///     When this method returns, contains the fundamental type if the conversion succeeded,
    ///     or the default value if the conversion failed.
    /// </param>
    /// <returns>True if the conversion was successful; otherwise, false.</returns>
    public static bool TryGetFundamentalType(string type, [NotNullWhen(true)] out CppFundamentalType? result)
    {
        // Initialize the result
        result = default;

        // Initialize variables
        bool isUnsigned = false, isSigned = false;
        string temp;
        CppFundamentalType? fundamentalType;

        // Check if the type is signed or unsigned
        if (type.StartsWith("unsigned "))
        {
            isUnsigned = true;
        }
        else if (type.StartsWith("signed "))
        {
            isSigned = true;
        }

        // Extract the base type
        if (isSigned)
        {
            temp = type["signed ".Length..];
        }
        else if (isUnsigned)
        {
            temp = type["unsigned ".Length..];
        }
        else
        {
            temp = type;
        }

        // Map the base type to the corresponding CppFundamentalType
        fundamentalType = temp switch
        {
            "void" => CppFundamentalType.Void,
            "bool" => CppFundamentalType.Boolean,
            "float" => CppFundamentalType.Float,
            "double" => CppFundamentalType.Double,
            "wchar_t" => CppFundamentalType.WChar,
            "char" => CppFundamentalType.Char,
            "short" or "INT16" => CppFundamentalType.Int16,
            "int" or "long" or "INT32" => CppFundamentalType.Int32,
            "__int64" or "long long" or "INT64" => CppFundamentalType.Int64,
            _ => null
        };

        if (fundamentalType is CppFundamentalType.Char && isUnsigned)
            isUnsigned = false;

        // Adjust the fundamental type based on signed or unsigned
        if (isSigned)
        {
            fundamentalType -= 8;
        }
        else if (isUnsigned)
        {
            fundamentalType += 8;
        }

        // Check if the conversion was successful
        if (fundamentalType is null)
        {
            return false;
        }

        // Set the result
        result = fundamentalType.Value;
        return true;
    }


    /// <summary>
    ///     Links the given list of CppType instances and returns the root CppType.
    /// </summary>
    /// <param name="types">The list of CppType instances to be linked.</param>
    /// <returns>The root CppType of the linked types, or null if the list is empty.</returns>
    public static CppType? LinkTypes(List<CppType> types)
    {
        // Reverse the list of types
        types.Reverse();

        // Return null if the list is empty
        if (types.Count is 0)
        {
            return null;
        }

        // Set the root type and initialize the current type
        CppType root = types[0];
        root.RootType = root;
        CppType current = root;

        if (types.Count <= 1)
        {
            return root;
        }

        // Link the types together
        foreach (CppType type in types.Skip(1))
        {
            type.RootType = root;
            type.Parent = current;
            current.SubType = type;
            current = type;
        }

        // Return the root type
        return root;
    }
}