using System.Collections.ObjectModel;
using System.CommandLine;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Hosihikari.Generation.LeviLaminaExportGeneration;

public static partial class LeviLaminaExportGeneration
{
    public const string DefinesHeaderName = "hosihikari_defines.h";

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

    public static partial class Regexes
    {
        [GeneratedRegex(@"\s*([a-zA-Z_]\S*\s*\s*\w+)\s*(?:,\s*([a-zA-Z_]\S*\s*\s*\w+))*")]
        public static partial Regex Parameters();

        [GeneratedRegex(@"[a-zA-Z_]\S*")]
        public static partial Regex ReturnType();

        [GeneratedRegex(@"[a-zA-Z_]*")]
        public static partial Regex Name();
    }

    public class Expression
    {
        public class Symbol
        {
            [Flags]
            public enum Type
            {
                Regex,
                String,
                Expression
            }

            private object? Value { get; set; }

            public Type SymbolType => Value switch
            {
                Regex _ => Type.Regex,
                string _ => Type.String,
                Expression _ => Type.Expression,
                _ => throw new NotSupportedException()
            };

            public static implicit operator Symbol(Regex regex) => new() { Value = regex };
            public static implicit operator Symbol(string str) => new() { Value = str };
            public static implicit operator Symbol(Expression exp) => new() { Value = exp };

            public T Target<T>() => (T)Value!;

            public override string ToString() => Value?.ToString() ?? string.Empty;
        }


        /// <summary>
        /// Initializes a new instance of the Expression class.
        /// </summary>
        /// <param name="format">The format string.</param>
        /// <param name="symbols">The list of symbols.</param>
        /// <param name="matched">The action to be performed when a match is found.</param>
        public Expression(string format, IList<Symbol>? symbols = null, Action<IReadOnlyList<Symbol>, GroupCollection>? matched = null)
        {
            Format = format;
            Symbols = new ReadOnlyCollection<Symbol>(symbols ?? []);
            this.matched = matched;

            var expStr = format;
            // If symbols are provided, create the string representations and indexes
            if (symbols is not null)
            {
                var strings = new string[symbols.Count];
                var indexes = new List<int>();

                for (int i = 0, istr = 0, iexp = 0, ireg = 0; i < symbols.Count; i++)
                {
                    var symbolType = symbols[i].SymbolType;
                    strings[i] = symbolType switch
                    {
                        Symbol.Type.String => $"(?<__substr_{istr++}>{symbols[i]})",
                        Symbol.Type.Expression => $"(?<__subexp_{iexp++}>.*)",
                        Symbol.Type.Regex => $"(?<__subreg_{ireg++}>{symbols[i]})",
                        _ => throw new NotSupportedException()
                    };
                    if (symbolType == Symbol.Type.Expression) indexes.Add(i);
                }

                expStr = string.Format(format, strings);
                subExpressionIndexes = new ReadOnlyCollection<int>(indexes);
            }
            exp = new Regex(expStr, RegexOptions.Compiled);
        }


        public string Format { get; }

        public IReadOnlyList<Symbol>? Symbols { get; }

        private readonly Regex exp;

        private readonly Action<IReadOnlyList<Symbol>, GroupCollection>? matched;

        private readonly IReadOnlyList<int>? subExpressionIndexes;

        public override string ToString() => exp.ToString();

        /// <summary>
        /// Matches the input string with the expression and invokes a visitor function for each symbol match.
        /// </summary>
        /// <param name="input">The input string to match.</param>
        /// <param name="visitor">The visitor function to invoke for each symbol match.</param>
        /// <returns>The updated Expression object after matching the input string.</returns>
        public Expression Match(string input, Action<Symbol?, Group>? visitor = null)
        {
            Success = false;

            var match = exp.Match(input);
            if (match.Success is false)
                return this;

            // Invoke visitor function for each symbol match
            if (Symbols is not null)
            {
                for (int i = 0, istr = 0, iexp = 0, ireg = 0; i < Symbols.Count; i++)
                {
                    visitor?.Invoke(Symbols[i], match.Groups[Symbols[i].SymbolType switch
                    {
                        Symbol.Type.String => $"__substr_{istr++}",
                        Symbol.Type.Expression => $"__subexp_{iexp++}",
                        Symbol.Type.Regex => $"__subreg_{ireg++}",
                        _ => throw new NotSupportedException()
                    }]);
                }
            }

            // Match sub expressions if defined
            if (subExpressionIndexes is not null)
            {
                int expIndex = 0;
                foreach (var i in subExpressionIndexes)
                {
                    var current = Symbols![i].Target<Expression>();
                    if (current.Match(match.Groups[$"__subexp_{expIndex++}"].Value, visitor).Success is false)
                        return this;
                }
            }

            Success = true;
            groups = match.Groups;

            // Invoke the matched event
            matched?.Invoke(Symbols ?? [], groups);

            return this;
        }


        private GroupCollection? groups;

        public bool Success { get; private set; }
    }

    public static class Expressions
    {
        /// <summary>
        /// AutoGenerate HosihikariExport(<return_type> ExportFuncName(<function_name>))(<parameters>)
        /// </summary>
        public static Expression ExportAutoGenerate { get; } = new(
            @"{0} {1}\({2}\)", [
                Defines.AutoGenerate,
                Defines.HosihikariExport,
                new Expression(
                    @"{0} {1}\({2}\)\({3}\)", [
                        Regexes.ReturnType(),
                        Defines.ExportFuncName,
                        Regexes.Name(),
                        Regexes.Parameters()
                    ],
                    (symbols, groups) =>
                    {
                        throw new NotImplementedException();
                    }
                )
            ]
        );

        /// <summary>
        /// Manually HosihikariExport(<return_type> ExportFuncName(<function_name>))(<parameters>)
        /// </summary>
        public static Expression ExportManually { get; } = new(
            @"{0} {1}\({2}\)", [
                Defines.Manually,
                Defines.HosihikariExport,
                new Expression(
                    @"{0} {1}\({2}\)\({3}\)", [
                        Regexes.ReturnType(),
                        Defines.ExportFuncName,
                        Regexes.Name(),
                        Regexes.Parameters()
                    ],
                    (symbols, groups) =>
                    {
                        throw new NotImplementedException();
                    }
                )
            ]
        );

        //todo
    }

    public static void Run(string sourceDir, string outputPath)
    {
        //var exp = new Expression(
        //    @"{0} {1}\({2}\)", [
        //        Defines.AutoGenerate,
        //        Defines.HosihikariExport,
        //        new Expression(
        //            @"{0} {1}\({2}\)\({3}\)", [
        //                Regexes.ReturnType(),
        //                Defines.ExportFuncName,
        //                Regexes.Name(),
        //                Regexes.Parameters()
        //            ])]);
        //exp.Match("AutoGenerate HosihikariExport(bool ExportFuncName(unhook)(void* target, void* detour, bool stopTheWorld))");

        throw new NotImplementedException();
    }
}
