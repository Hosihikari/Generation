using Hosihikari.Generation.Generator;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using static Hosihikari.Generation.LeviLaminaExportGeneration.LeviLaminaExportGeneration.RegexStrings;
using static Hosihikari.Generation.LeviLaminaExportGeneration.LeviLaminaExportGeneration.Regexes;
using static Hosihikari.Generation.LeviLaminaExportGeneration.LeviLaminaExportGeneration.Defines;

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
        Expressions.FunctionPointerDefinitionExp.Match(
            @"FunctionPointerDef(""get_name"", __stdcall, Pointer(std::string), Pointer(ll::plugin::Dependency))");

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

        [GeneratedRegex("\"\"[a-zA-Z_]*\"\"")]
        public static partial Regex NameWithQuotationMarks();
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
        public const string All = ".*";

        /// <summary>
        ///     Alias for LeftParenthesis.
        /// </summary>
        public const string Lparen = LeftParenthesis;

        /// <summary>
        ///     Alias for RightParenthesis.
        /// </summary>
        public const string Rparen = RightParenthesis;
    }

    public class Expression
    {
        private readonly Regex exp;

        private readonly Func<IReadOnlyList<Symbol>, GroupCollection, bool>? matched;

        private readonly IReadOnlyList<int>? subExpressionIndexes;


        private GroupCollection? groups;


        /// <summary>
        ///     Initializes a new instance of the Expression class.
        /// </summary>
        /// <param name="format">The format string.</param>
        /// <param name="symbols">The list of symbols.</param>
        /// <param name="matched">The action to be performed when a match is found.</param>
        public Expression(string format, IList<Symbol>? symbols = null,
            Func<IReadOnlyList<Symbol>, GroupCollection, bool>? matched = null)
        {
            Format = format;
            Symbols = new ReadOnlyCollection<Symbol>(symbols ?? []);
            this.matched = matched;

            string expStr = format;
            // If symbols are provided, create the string representations and indexes
            if (symbols is not null)
            {
                string[] strings = new string[symbols.Count];
                List<int> indexes = new();

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
                    if (symbolType == Symbol.Type.Expression)
                    {
                        indexes.Add(i);
                    }
                }

                expStr = string.Format(format, strings);
                subExpressionIndexes = new ReadOnlyCollection<int>(indexes);
            }

            exp = new(expStr, RegexOptions.Compiled);
        }


        public string Format { get; }

        public IReadOnlyList<Symbol>? Symbols { get; }

        public bool Success { get; private set; }

        public override string ToString()
        {
            return exp.ToString();
        }

        /// <summary>
        ///     Matches the input string with the expression and invokes a visitor function for each symbol match.
        /// </summary>
        /// <param name="input">The input string to match.</param>
        /// <param name="visitor">The visitor function to invoke for each symbol match.</param>
        /// <returns>The updated Expression object after matching the input string.</returns>
        public Expression Match(string input, Action<Symbol?, Group>? visitor = null)
        {
            Success = false;

            Match match = exp.Match(input);
            if (match.Success is false)
            {
                return this;
            }

            // Invoke visitor function for each symbol match
            if (Symbols is not null)
            {
                for (int i = 0, istr = 0, iexp = 0, ireg = 0; i < Symbols.Count; i++)
                {
                    visitor?.Invoke(Symbols[i], match.Groups[Symbols[i].SymbolType switch
                    {
                        Symbol.Type.String => GroupName.SubString(istr++),
                        Symbol.Type.Expression => GroupName.SubExpression(iexp++),
                        Symbol.Type.Regex => GroupName.SubRegex(ireg++),
                        _ => throw new NotSupportedException()
                    }]);
                }
            }

            // Match sub expressions if defined
            if (subExpressionIndexes is not null)
            {
                int expIndex = 0;
                foreach (int i in subExpressionIndexes)
                {
                    Expression current = Symbols![i].Target<Expression>();
                    if (current.Match(match.Groups[GroupName.SubExpression(expIndex++)].Value, visitor)
                            .Success is false)
                    {
                        return this;
                    }
                }
            }

            Success = true;
            groups = match.Groups;

            // Invoke the matched event
            matched?.Invoke(Symbols ?? [], groups);

            return this;
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
        public static Expression CppFundamentalTypeExp { get; } = new(
            "{0}",
            [RegexStrings.String],
            (symbols, groups) =>
            {
                string? str = groups[Expression.GroupName.SubString(0)].Value;
                try
                {
                    TypeData type = new(new() { Kind = 0, Name = str });
                    if (type.Analyzer.CppTypeHandle.RootType.FundamentalType is not null)
                    {
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
            (symbols, groups) =>
            {
                string? str = groups[Expression.GroupName.SubString(0)].Value.Trim();

                if (FillerDefinitionExp!.Match(str).Success)
                {
                    return true;
                }

                if (FunctionPointerDefinitionExp!.Match(str).Success)
                {
                    return true;
                }

                return CppFundamentalTypeExp.Match(str).Success;
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
            (symbols, groups) =>
            {
                ExportManuallyMatched?.Invoke(null, new(symbols, groups));
                return true;
            });

        /// <summary>
        ///     Manually HosihikariExport(<return_type> ExportFuncName(<function_name>))(<parameters>)
        /// </summary>
        public static Expression ExportManuallyExp { get; } = new(
            @"{0}\s+{1}",
            [Manually, ExportFunctionExp],
            (symbols, groups) =>
            {
                ExportManuallyMatched?.Invoke(null, new(symbols, groups));
                return true;
            });


        public static Expression RecordFieldExp { get; } = new(
            @$"{{0}}{Lparen}{{1}}{Comma}{{2}}{Rparen}",
            [
                RecordField,
                SupportedTypeExp,
                NameWithQuotationMarks()
            ]);

        public static Expression FunctionPointerDefinitionExp { get; } = new(
            @$"{{0}}{Lparen}{{1}}{Comma}{{2}}{Comma}{{3}}{Comma}{{4}}",
            [
                FunctionPointerDef,
                NameWithQuotationMarks(),
                Convention(),
                SupportedTypeExp,
                All
            ],
            (symbols, groups) =>
            {
                IEnumerable<string>? parameters = from str in groups[Expression.GroupName.SubString(1)].Value.Split(',')
                    where SupportedTypeExp.Match(str).Success
                    select str.Trim();
                throw new NotImplementedException();
            });

        public static Expression FillerDefinitionExp { get; } = new(
            @$"{{0}}{Lparen}{{1}}{Rparen}",
            [FillerDef, Number()]
        );

        public static Expression PointerExp { get; } = new(
            @$"{{0}}{Lparen}{{1}}{Rparen}",
            [Pointer, SupportedTypeExp]
        );

        public static Expression ReferenceExp { get; } = new(
            @$"{{0}}{Lparen}{{1}}{Rparen}",
            [Reference, SupportedTypeExp]
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


        //public static Expression InteropRecordDefinition { get; } = new(
        //    @"{0}\s*\(\s*{1}\s\)\s*;", [
        //        Defines.InteropRecordDefinition,
        //        new Expression(@"([\s\S])+", null, (symbols, groups) => {
        //            throw new NotImplementedException();
        //        })
        //    ]);

        //todo
    }
}