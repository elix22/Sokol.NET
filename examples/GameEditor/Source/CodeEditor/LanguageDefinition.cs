// LanguageDefinition.cs — C# token patterns for the syntax highlighter.
using System.Text.RegularExpressions;

namespace GameEditor.CodeEditor
{
    /// <summary>
    /// Describes how to tokenise a language into <see cref="PaletteIndex"/> colours.
    /// </summary>
    public sealed class LanguageDefinition
    {
        public string Name { get; }

        // Each entry: (compiled Regex, palette colour to assign)
        public (Regex Pattern, PaletteIndex Color)[] TokenRules { get; }

        // String used to start a single-line comment (used by the highlighter to
        // handle the rest of the line without running every regex against it).
        public string SingleLineComment { get; }

        // Block comment delimiters
        public string BlockCommentStart { get; }
        public string BlockCommentEnd   { get; }

        public LanguageDefinition(
            string name,
            (Regex, PaletteIndex)[] tokenRules,
            string singleLineComment,
            string blockCommentStart,
            string blockCommentEnd)
        {
            Name               = name;
            TokenRules         = tokenRules;
            SingleLineComment  = singleLineComment;
            BlockCommentStart  = blockCommentStart;
            BlockCommentEnd    = blockCommentEnd;
        }

        // ── C# built-in ──────────────────────────────────────────────────────
        public static readonly LanguageDefinition CSharp = BuildCSharp();

        private static LanguageDefinition BuildCSharp()
        {
            // Order matters: more specific patterns first.
            var rules = new (Regex, PaletteIndex)[]
            {
                // Preprocessor directives – must come before keywords
                (new Regex(@"^\s*#\s*(if|else|elif|endif|define|undef|region|endregion|pragma|warning|error|line|nullable)\b.*",
                    RegexOptions.Compiled),
                    PaletteIndex.Preprocessor),

                // Verbatim string literal  @"..."
                (new Regex(@"@""(?:[^""]|"""")*""",
                    RegexOptions.Compiled),
                    PaletteIndex.String),

                // Raw string literal  """..."""  (single-line form; multi-line is rare in practice)
                (new Regex("\"\"\"+.*?\"\"\"+"  ,
                    RegexOptions.Compiled),
                    PaletteIndex.String),

                // Interpolated + regular string literal  "..." (non-greedy, handles escapes)
                (new Regex(@"\$?""(?:[^""\\]|\\.)*""",
                    RegexOptions.Compiled),
                    PaletteIndex.String),

                // Character literal  'x'  or escape '\n'
                (new Regex(@"'(?:[^'\\]|\\.)'",
                    RegexOptions.Compiled),
                    PaletteIndex.CharLiteral),

                // Number literals: hex 0x…, binary 0b…, float/double/decimal, int
                (new Regex(@"\b(?:0[xX][0-9A-Fa-f_]+|0[bB][01_]+|\d[\d_]*(?:\.[\d_]+)?(?:[eE][+-]?\d+)?[fFdDmMlLuUiI]*)\b",
                    RegexOptions.Compiled),
                    PaletteIndex.Number),

                // C# keywords
                (new Regex(@"\b(?:abstract|as|base|bool|break|byte|case|catch|char|checked|class|const|continue|decimal|default|delegate|do|double|else|enum|event|explicit|extern|false|finally|fixed|float|for|foreach|goto|if|implicit|in|int|interface|internal|is|lock|long|namespace|new|null|object|operator|out|override|params|private|protected|public|readonly|ref|return|sbyte|sealed|short|sizeof|stackalloc|static|string|struct|switch|this|throw|true|try|typeof|uint|ulong|unchecked|unsafe|ushort|using|virtual|void|volatile|while)\b",
                    RegexOptions.Compiled),
                    PaletteIndex.Keyword),

                // Contextual keywords
                (new Regex(@"\b(?:add|alias|and|ascending|async|await|by|descending|dynamic|equals|from|get|global|group|init|into|join|let|managed|nameof|nint|not|notnull|nuint|on|or|orderby|partial|record|remove|required|scoped|select|set|unmanaged|value|var|when|where|with|yield)\b",
                    RegexOptions.Compiled),
                    PaletteIndex.Keyword),

                // Well-known built-in types and common framework types rendered in teal
                (new Regex(@"\b(?:Action|Activator|ArgumentException|Array|ArrayList|Boolean|Byte|Char|Comparison|Console|Convert|DateTime|DateTimeOffset|Decimal|Dictionary|Double|Environment|Exception|Func|GC|Guid|HashSet|IComparable|IDisposable|IEnumerable|IEnumerator|IEquatable|IList|Int16|Int32|Int64|InvalidOperationException|KeyValuePair|List|Math|MathF|MemoryStream|NotImplementedException|NotSupportedException|NullReferenceException|Nullable|ObjectDisposedException|OperationCanceledException|OverflowException|Queue|Random|ReadOnlySpan|SByte|Single|Span|Stack|StreamReader|StreamWriter|String|StringBuilder|Task|TimeSpan|Tuple|Type|UInt16|UInt32|UInt64|Uri|ValueTask|Vector2|Vector3|Vector4|Matrix4x4|Quaternion)\b",
                    RegexOptions.Compiled),
                    PaletteIndex.KnownType),

                // Identifier (last, catches everything else that looks like a word)
                (new Regex(@"\b[A-Za-z_]\w*\b",
                    RegexOptions.Compiled),
                    PaletteIndex.Identifier),

                // Punctuation / operators
                (new Regex(@"[;,\.\[\]\(\)\{\}<>!&\|^~\+\-\*/%=:?]",
                    RegexOptions.Compiled),
                    PaletteIndex.Punctuation),
            };

            return new LanguageDefinition(
                name:               "C#",
                tokenRules:         rules,
                singleLineComment:  "//",
                blockCommentStart:  "/*",
                blockCommentEnd:    "*/");
        }
    }
}
