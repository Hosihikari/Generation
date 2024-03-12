using System.Diagnostics.CodeAnalysis;

namespace Hosihikari.Generation.CppParser;

public static class CppTypeParser
{

    public static bool TryParse(string type, [NotNullWhen(true)] out CppType? result)
    {
        throw new NotImplementedException();

        result = null;

        if (type is "...")
        {
            result = new CppType()
            {
                Type = CppTypeEnum.VarArgs,
                TypeIdentifier = type,
                OriginalTypeString = type,
            };
            return true;
        }
    }

    /// <summary>
    /// Tries to get the C++ type from the input type string.
    /// </summary>
    /// <param name="type">The input type string.</param>
    /// <param name="result">The tuple containing the CppTypeEnum and the handled type.</param>
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
    /// Tries to extract the template type from the input string.
    /// </summary>
    /// <param name="typeWithoutPrefix">The input string without the prefix.</param>
    /// <param name="result">The extracted template type and template arguments.</param>
    /// <returns>Returns true if the template type was successfully extracted, otherwise false.</returns>
    public static bool TryGetTemplateType(string typeWithoutPrefix, out (string[] templateArgs, string typeWithoutTemplateArgs) result)
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
                        templateArgsStartIndex = i;

                    checked { searchDepth++; }

                    continue;

                case '>':
                    checked { searchDepth--; }

                    // If search depth is 0, set the end index of template arguments
                    if (searchDepth is 0)
                        templateArgsEndIndex = i;

                    continue;

                case ',':
                    // If search depth is 1, add the index to the list of template argument separators
                    if (searchDepth is 1)
                        templateArgSeparatorsIndexList.Add(i);

                    continue;
            }
        }

        // If end index of template arguments is 0, return false
        if (templateArgsEndIndex is 0)
            return false;

        templateArgSeparatorsIndexList.Add(templateArgsEndIndex);

        // Extract individual template arguments
        var templateArgs = new string[templateArgSeparatorsIndexList.Count];

        int prevIndex = templateArgsStartIndex + 1;

        for (int i = 0; i < templateArgSeparatorsIndexList.Count; i++)
        {
            templateArgs[i] = typeWithoutPrefix[prevIndex..templateArgSeparatorsIndexList[i]].Trim();
            prevIndex = templateArgSeparatorsIndexList[i] + 1;
        }

        // Set the result and return true
        result.typeWithoutTemplateArgs = typeWithoutPrefix[..templateArgsStartIndex].Trim();
        result.templateArgs = templateArgs;
        return true;
    }

}
