using Hosihikari.Generation.Generator;
using System.Collections.ObjectModel;
using System.Text;
using System.Text.RegularExpressions;
using static Hosihikari.Generation.LeviLaminaExportGeneration.LeviLaminaExportGeneration.RegexStrings;
using static Hosihikari.Generation.LeviLaminaExportGeneration.LeviLaminaExportGeneration.Regexes;
using static Hosihikari.Generation.LeviLaminaExportGeneration.LeviLaminaExportGeneration.Defines;
using GroupName = Hosihikari.Generation.LeviLaminaExportGeneration.LeviLaminaExportGeneration.Expression.GroupName;
using ParsingDataCollection = System.Collections.Generic.Dictionary<string, object>;

namespace Hosihikari.Generation.LeviLaminaExportGeneration;

public static partial class LeviLaminaExportGeneration
{
    public const string DefinesHeaderName = "hosihikari_defines.h";

    public static void Run(string sourceDir, string outputPath)
    {
        //Expressions.ExportFunctionExp.Match("AutoGenerate HosihikariExport(bool ExportFuncName(unhook)(void* target, void* detour, bool stopTheWorld)){}  AutoGenerate HosihikariExport(bool ExportFuncName(unhook)(void* target, void* detour, bool stopTheWorld)){}");
        //Expressions.FillerDefinitionExp.Match("FillerDef(0x30)");
        //Expressions.PointerExp.Match("Pointer(std::string)");
        //Expressions.SupportedTypeExp.Match("void*");
        //Expressions.SupportedTypeExp.Match("FillerDef(0x30)");
        //Expressions.FunctionPointerDefinitionExp.Match(@"FunctionPointerDef(""get_name"", __stdcall, Pointer(std::string), Pointer(ll::plugin::Dependency), void*, double, int)");
        //Expressions.RecordFieldExp.Match(@"RecordField(int, ""testInt"")");
        Expressions.InteropRecordDefinitionExp.Match(@"InteropRecordDefinition(
    ll_plugin_Dependency_functions,
    FunctionPointerDef(""dtor"", __stdcall, void, Pointer(ll::plugin::Dependency)),
    FunctionPointerDef(
        ""ctor"",
        __stdcall,
        void,
        Pointer(ll::plugin::Dependency),
        Pointer(std::string),
        Pointer(std::string)
    ),
    FunctionPointerDef(""get_name"", __stdcall, Pointer(std::string), Pointer(ll::plugin::Dependency)),
    FunctionPointerDef(""get_version"", __stdcall, Pointer(std::string), Pointer(ll::plugin::Dependency)),
    FunctionPointerDef(
        ""operator_equals"",
        __stdcall,
        bool,
        Pointer(ll::plugin::Dependency),
        Pointer(ll::plugin::Dependency)
    )
);");

        throw new NotImplementedException();
    }

    public static class Defines
    {
        public const string AutoGenerate = "AutoGenerate";
        public const string Manually = "Manually";
        public const string RecordInit = "RecordInit";
        public const string InteropRecordName = "InteropRecordName";
        public const string HosihikariExport = "HosihikariExport";
        public const string ExportFuncName = "ExportFuncName";
        public const string ExportFuncNameOverload = "ExportFuncNameOverload";
        public const string RecordField = "RecordField";
        public const string InteropRecordDefinition = "InteropRecordDefinition";
        public const string FunctionPointerDef = "FunctionPointerDef";
        public const string FillerDef = "FillerDef";
        public const string Pointer = "Pointer";
        public const string Reference = "Reference";
    }

    /// <summary>
    ///     Contains regular expressions for matching parameters, types, names, and numbers.
    /// </summary>
    public static partial class Regexes
    {
        /// <summary>
        ///     Regular expression for matching parameters.
        /// </summary>
        [GeneratedRegex(@"\s*([a-zA-Z_]\S*\s*\s*\w+)\s*(?:,\s*([a-zA-Z_]\S*\s*\s*\w+))*")]
        public static partial Regex Parameters();

        /// <summary>
        ///     Regular expression for matching types.
        /// </summary>
        [GeneratedRegex(@"[a-zA-Z_]\S*")]
        public static partial Regex CppType();

        /// <summary>
        ///     Regular expression for matching names.
        /// </summary>
        [GeneratedRegex(@"[a-zA-Z_]*")]
        public static partial Regex Name();

        /// <summary>
        ///     Regular expression for matching numbers.
        /// </summary>
        [GeneratedRegex(@"(0x[0-9A-Fa-f]+)|\d+")]
        public static partial Regex Number();

        /// <summary>
        ///     Represents a regular expression for matching calling conventions.
        /// </summary>
        [GeneratedRegex("__(cdecl|fastcall|stdcall|thiscall)")]
        public static partial Regex Convention();
    }

    /// <summary>
    ///     This class contains regex strings for common patterns.
    /// </summary>
    public static class RegexStrings
    {
        /// <summary>
        ///     Regex string for left parenthesis with optional spaces around it.
        /// </summary>
        public const string LeftParenthesis = @"\s*\(\s*";

        /// <summary>
        ///     Regex string for right parenthesis with optional spaces around it.
        /// </summary>
        public const string RightParenthesis = @"\s*\)\s*";

        /// <summary>
        ///     Regex string for comma with optional spaces around it.
        /// </summary>
        public const string Comma = @"\s*,\s*";

        /// <summary>
        ///     Regex string for whitespace.
        /// </summary>
        public const string Whitespace = @"\s+";

        /// <summary>
        ///     Regex string for a string identifier.
        /// </summary>
        public const string String = @"[a-zA-Z_]\S*";

        /// <summary>
        ///     Regex string for matching all characters.
        /// </summary>
        public const string All = @"[\s\S]*";

        public const string AllWithLazyQuantifier = All + "?";

        /// <summary>
        ///     Alias for LeftParenthesis.
        /// </summary>
        public const string Lparen = LeftParenthesis;

        /// <summary>
        ///     Alias for RightParenthesis.
        /// </summary>
        public const string Rparen = RightParenthesis;
    }

    public record MatchedResult(bool Success, ParsingDataCollection Data);

    public class Expression
    {
        /// <summary>
        ///     Represents a method that handles matching of an expression.
        /// </summary>
        /// <param name="rootExp">The root expression.</param>
        /// <param name="symbols">The list of symbols.</param>
        /// <param name="groups">The group collection.</param>
        /// <returns>True if the expression is matched; otherwise, false.</returns>
        public delegate bool MatchedHandler(ParsingDataCollection data, IReadOnlyList<Symbol> symbols,
            GroupCollection groups);

        private readonly Regex exp;

        private readonly MatchedHandler? matched;

        private readonly IReadOnlyList<int>? subExpressionIndexes;

        private GroupCollection? groups;

        /// <summary>
        ///     Initializes a new instance of the Expression class.
        /// </summary>
        /// <param name="format">The format string.</param>
        /// <param name="symbols">The list of symbols.</param>
        /// <param name="matched">The action to be performed when a match is found.</param>
        public Expression(string format, IList<Symbol>? symbols = default, MatchedHandler? matched = default)
        {
            Format = format;
            Symbols = new ReadOnlyCollection<Symbol>(symbols ?? []);
            this.matched = matched;

            string expStr = format;
            // If symbols are provided, create the string representations and indexes
            if (symbols is not null)
            {
                string[] strings = new string[symbols.Count];
                List<int> indexes = [];

                for (int i = 0, istr = 0, iexp = 0, ireg = 0; i < symbols.Count; i++)
                {
                    Symbol.Type symbolType = symbols[i].SymbolType;
                    strings[i] = symbolType switch
                    {
                        Symbol.Type.String => $"(?<{GroupName.SubString(istr++)}>{symbols[i]})",
                        Symbol.Type.Expression => $"(?<{GroupName.SubExpression(iexp++)}>.*)",
                        Symbol.Type.Regex => $"(?<{GroupName.SubRegex(ireg++)}>{symbols[i]})",
                        _ => throw new NotSupportedException()
                    };
                    if (symbolType is Symbol.Type.Expression)
                    {
                        indexes.Add(i);
                    }
                }

                expStr = string.Format(format, strings);
                subExpressionIndexes = new ReadOnlyCollection<int>(indexes);
            }

            exp = new(expStr, RegexOptions.Compiled);
        }

        /// <summary>
        ///     Gets the format of the expression.
        /// </summary>
        public string Format { get; }

        /// <summary>
        ///     Gets the list of symbols associated with the expression.
        /// </summary>
        public IReadOnlyList<Symbol>? Symbols { get; }

        /// <summary>
        ///     Gets a value indicating whether the expression was successfully processed.
        /// </summary>
        public bool Success { get; private set; }

        /// <inheritdoc />
        public override string ToString()
        {
            return exp.ToString();
        }

        /// <summary>
        /// Adds parsed data to the expression with the specified key and value.
        /// </summary>
        /// <param name="key">The key of the parsed data.</param>
        /// <param name="value">The value of the parsed data.</param>
        /// <returns>The current Expression instance.</returns>
        //public Expression SetParsedData(string key, object value)
        //{
        //    parsedData[key] = value;
        //    return this;
        //}

        //public Expression SetParsedData(IReadOnlyDictionary<string, object> data)
        //{
        //    foreach (var (key, value) in data) parsedData[key] = value;
        //    return this;
        //}

        //public Expression SetParsedData(IEnumerable<KeyValuePair<string, object>> kvps)
        //{
        //    foreach (var (key, value) in kvps) parsedData[key] = value;
        //    return this;
        //}

        /// <summary>
        ///     Matches the input string with the expression and invokes a visitor function for each symbol match.
        /// </summary>
        /// <param name="input">The input string to match.</param>
        /// <param name="visitor">The visitor function to invoke for each symbol match.</param>
        /// <returns>The updated Expression object after matching the input string.</returns>
        public MatchedResult Match(string input, Action<Symbol?, Group>? visitor = default)
        {
            lock (this)
            {
                return MatchInternal(input, visitor, []);
            }
        }

        private MatchedResult MatchInternal(string input, Action<Symbol?, Group>? visitor,
            ParsingDataCollection parsedData)
        {
            Success = false;

            Match match = exp.Match(input);
            if (!match.Success)
            {
                return new(false, parsedData);
            }

            // Invoke visitor function for each symbol match
            if (Symbols is not null)
            {
                for (int i = 0, istr = 0, iexp = 0, ireg = 0; i < Symbols.Count; i++)
                {
                    string groupName = Symbols[i].SymbolType switch
                    {
                        Symbol.Type.String => GroupName.SubString(istr++),
                        Symbol.Type.Expression => GroupName.SubExpression(iexp++),
                        Symbol.Type.Regex => GroupName.SubRegex(ireg++),
                        _ => throw new NotSupportedException()
                    };
                    visitor?.Invoke(Symbols[i], match.Groups[groupName]);
                    parsedData.Add(groupName, match.Groups[groupName]);
                }
            }

            // Match sub expressions if defined
            if (subExpressionIndexes is not null)
            {
                int expIndex = 0;
                foreach (int i in subExpressionIndexes)
                {
                    Expression current = Symbols![i].Target<Expression>();
                    string groupName = GroupName.SubExpression(expIndex++);
                    string val = match.Groups[groupName].Value;
                    MatchedResult rlt = current.Match(val, visitor);
                    if (!rlt.Success)
                    {
                        return new(false, parsedData);
                    }

                    parsedData.Add($"{groupName}_data", rlt.Data);
                }
            }

            Success = true;
            groups = match.Groups;

            // Invoke the matched event
            matched?.Invoke(parsedData, Symbols ?? [], groups);

            return new(true, parsedData);
        }

        /// <summary>
        ///     Represents a symbol with a specific type and value.
        /// </summary>
        public class Symbol
        {
            /// <summary>
            ///     The type of symbol.
            /// </summary>
            [Flags]
            public enum Type
            {
                /// <summary>
                ///     Regular expression type.
                /// </summary>
                Regex,

                /// <summary>
                ///     String type.
                /// </summary>
                String,

                /// <summary>
                ///     Expression type.
                /// </summary>
                Expression
            }

            /// <summary>
            ///     The value of the symbol.
            /// </summary>
            private object? Value { get; set; }

            /// <summary>
            ///     The type of the symbol.
            /// </summary>
            public Type SymbolType => Value switch
            {
                Regex _ => Type.Regex,
                string _ => Type.String,
                Expression _ => Type.Expression,
                _ => throw new NotSupportedException()
            };

            /// <summary>
            ///     Implicit conversion operator from Regex to Symbol.
            /// </summary>
            public static implicit operator Symbol(Regex regex)
            {
                return new() { Value = regex };
            }

            /// <summary>
            ///     Implicit conversion operator from string to Symbol.
            /// </summary>
            public static implicit operator Symbol(string str)
            {
                return new() { Value = str };
            }

            /// <summary>
            ///     Implicit conversion operator from Expression to Symbol.
            /// </summary>
            public static implicit operator Symbol(Expression exp)
            {
                return new() { Value = exp };
            }

            /// <summary>
            ///     Gets the value of the symbol as a specific type.
            /// </summary>
            public T Target<T>()
            {
                return (T)Value!;
            }

            /// <summary>
            ///     Returns the string representation of the symbol's value.
            /// </summary>
            public override string ToString()
            {
                return Value?.ToString() ?? string.Empty;
            }
        }

        /// <summary>
        ///     Provides utility methods for generating substring, subexpression, and subregex names.
        /// </summary>
        public static class GroupName
        {
            /// <summary>
            ///     Generates a substring name based on the given index.
            /// </summary>
            /// <param name="index">The index used to generate the substring name.</param>
            /// <returns>The generated substring name.</returns>
            public static string SubString(int index)
            {
                return $"__substr_{index}";
            }

            /// <summary>
            ///     Generates a subexpression name based on the given index.
            /// </summary>
            /// <param name="index">The index used to generate the subexpression name.</param>
            /// <returns>The generated subexpression name.</returns>
            public static string SubExpression(int index)
            {
                return $"__subexp_{index}";
            }

            /// <summary>
            ///     Generates a subregex name based on the given index.
            /// </summary>
            /// <param name="index">The index used to generate the subregex name.</param>
            /// <returns>The generated subregex name.</returns>
            public static string SubRegex(int index)
            {
                return $"__subreg_{index}";
            }
        }
    }

    public static class Expressions
    {
        [Flags]
        public enum SupportedType
        {
            Fundamental,
            Filler,
            Fptr,
            Pointer,
            Reference
        }

        public static Expression CppFundamentalTypeExp { get; } = new(
            "{0}",
            [RegexStrings.String],
            (exp, symbols, groups) =>
            {
                string str = groups[GroupName.SubString(0)].Value;
                try
                {
                    TypeData type = new(new() { Kind = 0, Name = str });
                    if (type.Analyzer.CppTypeHandle.RootType.FundamentalType is not null)
                    {
                        exp.Add("Type", type.ToString());
                        return true;
                    }
                }
                catch
                {
                }

                return false;
            });

        public static Expression SupportedTypeExp { get; } = new(
            "{0}",
            [RegexStrings.String],
            (data, symbols, groups) =>
            {
                string str = groups[GroupName.SubString(0)].Value.Trim();

                MatchedResult rlt = FillerDefinitionExp.Match(str);
                if (rlt.Success)
                {
                    data.Add("Type", SupportedType.Filler);
                    data.Add("TypeData", rlt.Data);

                    return true;
                }

                rlt = PointerExp.Match(str);
                if (rlt.Success)
                {
                    data.Add("Type", SupportedType.Pointer);
                    data.Add("TypeData", rlt.Data);
                    return true;
                }

                rlt = ReferenceExp.Match(str);
                if (rlt.Success)
                {
                    data.Add("Type", SupportedType.Reference);
                    data.Add("TypeData", rlt.Data);
                    return true;
                }

                rlt = FunctionPointerDefinitionExp!.Match(str);
                if (rlt.Success)
                {
                    data.Add("Type", SupportedType.Fptr);
                    data.Add("TypeData", rlt.Data);
                    return true;
                }

                rlt = CppFundamentalTypeExp!.Match(str);
                if (!rlt.Success)
                {
                    return false;
                }

                data.Add("Type", SupportedType.Fundamental);
                data.Add("TypeData", rlt.Data);
                return true;
            });

        /// <summary>
        ///     HosihikariExport(<return_type> ExportFuncName(<function_name>))(<parameters>)
        /// </summary>
        public static Expression ExportFunctionExp { get; } = new(
            @$"{{0}}{Lparen}{{1}}{Rparen}",
            [
                HosihikariExport,
                new Expression(
                    @$"{{0}}\s+{{1}}{Lparen}{{2}}{Rparen}\s*{Lparen}{{3}}{Rparen}",
                    [
                        SupportedTypeExp,
                        ExportFuncName,
                        Name(),
                        Parameters()
                    ])
            ]);

        /// <summary>
        ///     AutoGenerate HosihikariExport(<return_type> ExportFuncName(<function_name>))(<parameters>)
        /// </summary>
        public static Expression ExportAutoGenerateExp { get; } = new(
            @"{0}\s+{1}",
            [AutoGenerate, ExportFunctionExp],
            (data, symbols, groups) =>
            {
                ExportManuallyMatched?.Invoke(default, new(symbols, groups));
                return true;
            });

        /// <summary>
        ///     Manually HosihikariExport(<return_type> ExportFuncName(<function_name>))(<parameters>)
        /// </summary>
        public static Expression ExportManuallyExp { get; } = new(
            @"{0}\s+{1}",
            [Manually, ExportFunctionExp],
            (data, symbols, groups) =>
            {
                ExportManuallyMatched?.Invoke(default, new(symbols, groups));
                return true;
            });

        public static Expression RecordFieldExp { get; } = new(
            @$"{{0}}{Lparen}{{1}}{Comma}{{2}}{Rparen}",
            [
                RecordField,
                SupportedTypeExp,
                All
            ],
            (data, symbols, groups) =>
            {
                string name = groups[GroupName.SubString(1)].Value;
                if (!(name.StartsWith('"') && name.EndsWith('"')))
                {
                    return false;
                }

                name = name.Trim('"');

                data.Add("FieldName", name);
                return true;
            });

        public static Expression FunctionPointerDefinitionExp { get; } = new(
            @$"{{0}}{Lparen}{{1}}{Comma}{{2}}{Comma}{{3}}{Comma}{{4}}{Rparen}",
            [
                FunctionPointerDef,
                All,
                Convention(),
                AllWithLazyQuantifier,
                All
            ],
            (exp, symbols, groups) =>
            {
                string name = groups[GroupName.SubString(1)].Value;
                if (!(name.StartsWith('"') && name.EndsWith('"')))
                {
                    return false;
                }

                name = name.Trim('"');
                if (!Name().IsMatch(name))
                {
                    return false;
                }

                exp.Add("FptrName", name);

                string ret = groups[GroupName.SubString(2)].Value;
                MatchedResult rlt = SupportedTypeExp.Match(ret);
                if (!rlt.Success)
                {
                    return false;
                }

                exp.Add("ReturnType", rlt.Data);

                IEnumerable<string> parameters;

                {
                    string parametersString = groups[GroupName.SubString(3)].Value;
                    List<string> parameterList = [];
                    StringBuilder builder = new(0xf);
                    bool innerExp = false;
                    foreach (char c in parametersString)
                    {
                        switch (c)
                        {
                            case '(':
                                innerExp = true;
                                builder.Append(c);
                                break;
                            case ')':
                                innerExp = false;
                                builder.Append(c);
                                break;

                            case ',':
                                if (!innerExp)
                                {
                                    parameterList.Add(builder.ToString().Trim());
                                    builder.Clear();
                                }

                                break;

                            default:
                                builder.Append(c);
                                break;
                        }
                    }

                    parameterList.Add(builder.ToString().Trim());
                    parameters = parameterList;
                }

                ParsingDataCollection paramData = new(parameters.Count());
                int i = 0;
                foreach (string parameter in parameters)
                {
                    rlt = SupportedTypeExp.Match(parameter);
                    if (rlt.Success)
                    {
                        paramData.Add($"param_{i++}", rlt.Data);
                    }
                    else
                    {
                        return false;
                    }
                }

                exp.Add("Parameters", paramData);

                return true;
            });

        public static Expression FillerDefinitionExp { get; } = new(
            @$"{{0}}{Lparen}{{1}}{Rparen}",
            [FillerDef, Number()],
            (exp, symbols, groups) =>
            {
                exp.Add("FillerSize", groups[GroupName.SubRegex(0)].Value);
                return true;
            }
        );

        public static Expression PointerExp { get; } = new(
            @$"{{0}}{Lparen}{{1}}{Rparen}",
            [Pointer, All],
            (exp, symbols, groups) =>
            {
                exp.Add("PointerType", groups[GroupName.SubString(1)].Value);
                return true;
            }
        );

        public static Expression ReferenceExp { get; } = new(
            @$"{{0}}{Lparen}{{1}}{Rparen}",
            [Reference, All],
            (exp, symbols, groups) =>
            {
                exp.Add("ReferenceType", groups[GroupName.SubString(1)].Value);
                return true;
            }
        );

        public static Expression InteropRecordDefinitionExp { get; } = new(
            @$"{{0}}{Lparen}{{1}}{Comma}{{2}}{Rparen}",
            [
                InteropRecordDefinition,
                AllWithLazyQuantifier,
                All
            ],
            (data, symbols, groups) =>
            {
                string name = groups[GroupName.SubString(1)].Value;
                if (!Name().IsMatch(name))
                {
                    return false;
                }

                IEnumerable<string> fields;
                string fieldsStr = groups[GroupName.SubString(2)].Value;
                {
                    List<string> fieldList = [];
                    StringBuilder builder = new(0xf);
                    bool innerExp = false;
                    foreach (char c in fieldsStr)
                    {
                        switch (c)
                        {
                            case '(':
                                innerExp = true;
                                builder.Append(c);
                                break;
                            case ')':
                                innerExp = false;
                                builder.Append(c);
                                break;

                            case ',':
                                if (!innerExp)
                                {
                                    fieldList.Add(builder.ToString().Trim());
                                    builder.Clear();
                                }

                                break;

                            default:
                                builder.Append(c);
                                break;
                        }
                    }

                    fieldList.Add(builder.ToString().Trim());
                    fields = fieldList;
                }

                Dictionary<string, string> fieldsWithName = [];
                int i = 0;
                foreach (string field in fields)
                {
                    MatchedResult rlt = RecordFieldExp.Match(field);
                    if (rlt.Success)
                    {
                        data.Add($"field_{i++}_data", rlt.Data);
                        continue;
                    }

                    rlt = SupportedTypeExp.Match(field);
                    if (rlt.Success)
                    {
                        data.Add($"field_{i++}_data", rlt.Data);
                    }
                }

                return true;
            }
        );

        public static event EventHandler<MatchedEventArgs>? ExportAutoGenerateMatched;

        public static event EventHandler<MatchedEventArgs>? ExportManuallyMatched;

        /// <summary>
        ///     Represents the event arguments for when a match is found.
        /// </summary>
        public class MatchedEventArgs(IReadOnlyList<Expression.Symbol> symbols, GroupCollection groups) : EventArgs
        {
            /// <summary>
            ///     Gets the list of symbols involved in the match.
            /// </summary>
            public IReadOnlyList<Expression.Symbol> Symbols => symbols;

            /// <summary>
            ///     Gets the collection of groups captured in the match.
            /// </summary>
            public GroupCollection Groups => groups;
        }

        //todo
    }
}