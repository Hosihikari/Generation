using System.Text.RegularExpressions;

namespace Hosihikari.Utils;

internal static partial class RegexHelper
{
    [GeneratedRegex("class std::basic_string<(?<char_type>.*), struct std::char_traits<(\\k<char_type>)>, class std::allocator<(\\k<char_type>)>>")]
    internal static partial Regex StdBasicStringRegex();
    [GeneratedRegex("class std::vector<(?<class_type>.*), class std::allocator<(\\k<class_type>)>>")]
    internal static partial Regex StdVectorRegex();
    [GeneratedRegex("class std::optional<(?<class_type>.*)>")]
    internal static partial Regex StdOptionalRegex();
    [GeneratedRegex("class std::unordered_map<(?<class_type_1>.*), (?<class_type_2>.*), struct std::hash<(\\k<class_type_1>)>, struct std::equal_to<(\\k<class_type_1>)>, class std::allocator<struct std::pair<(\\k<class_type_1>) const, (\\k<class_type_2>)>>>")]
    internal static partial Regex StdUnorderedMapRegex();
    [GeneratedRegex("class std::function<(?<function_type>.*)>")]
    internal static partial Regex StdFunctionRegex();
    [GeneratedRegex("class gsl::not_null<(?<class_type>.*)>")]
    internal static partial Regex GslNotNullRegex();
    [GeneratedRegex("class gsl::basic_string_span<(?<char_type>.*), (?<value>.*)>")]
    internal static partial Regex GslBasicStringSpanRegex();
}
