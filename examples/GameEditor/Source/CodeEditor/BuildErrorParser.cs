// BuildErrorParser.cs — Parses MSBuild/dotnet-build stdout/stderr into structured diagnostics.
//
// Handles the standard MSBuild format:
//   /path/to/File.cs(12,5): error CS0246: The type or namespace name 'Foo' ...
//   /path/to/File.cs(30,3): warning CS8600: Converting null literal...

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace GameEditor.CodeEditor
{
    public readonly struct BuildDiagnostic
    {
        public string FilePath    { get; init; }
        public int    Line        { get; init; }  // 1-based
        public int    Column      { get; init; }  // 1-based
        public bool   IsError     { get; init; }
        public string Code        { get; init; }
        public string Message     { get; init; }
    }

    public static class BuildErrorParser
    {
        // Standard MSBuild diagnostic line:
        //   path(line,col): error|warning CSXXXX: message
        private static readonly Regex DiagLine = new(
            @"^(.+)\((\d+),(\d+)\):\s+(error|warning)\s+(\w+):\s+(.+)$",
            RegexOptions.Compiled | RegexOptions.Multiline);

        // Roslyn / C# compiler short form (no path prefix) sometimes seen on some platforms:
        //   path : error CSXXXX : message
        private static readonly Regex DiagLineShort = new(
            @"^(.+?)\s*:\s*(error|warning)\s+(CS\w+)\s*:\s*(.+)$",
            RegexOptions.Compiled | RegexOptions.Multiline);

        /// <summary>
        /// Parse all diagnostics from combined build stdout+stderr output.
        /// Only lines matching the MSBuild format are returned; noise is dropped.
        /// </summary>
        public static IReadOnlyList<BuildDiagnostic> Parse(string buildOutput)
        {
            var results = new List<BuildDiagnostic>();
            if (string.IsNullOrEmpty(buildOutput)) return results;

            foreach (Match m in DiagLine.Matches(buildOutput))
            {
                if (!int.TryParse(m.Groups[2].Value, out int line))   continue;
                if (!int.TryParse(m.Groups[3].Value, out int col))    continue;
                string severity = m.Groups[4].Value;
                results.Add(new BuildDiagnostic
                {
                    FilePath = m.Groups[1].Value.Trim(),
                    Line     = line,
                    Column   = col,
                    IsError  = severity.Equals("error", StringComparison.OrdinalIgnoreCase),
                    Code     = m.Groups[5].Value,
                    Message  = m.Groups[6].Value.Trim()
                });
            }

            return results;
        }

        /// <summary>
        /// Group diagnostics by file path, returning a dictionary of
        /// path → (line-1-based → message).  Used to feed TextEditorWidget.
        /// </summary>
        public static (
            Dictionary<string, Dictionary<int, string>> Errors,
            Dictionary<string, Dictionary<int, string>> Warnings
        ) GroupByFile(IReadOnlyList<BuildDiagnostic> diagnostics)
        {
            var errors   = new Dictionary<string, Dictionary<int, string>>(StringComparer.OrdinalIgnoreCase);
            var warnings = new Dictionary<string, Dictionary<int, string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var d in diagnostics)
            {
                var target = d.IsError ? errors : warnings;
                if (!target.TryGetValue(d.FilePath, out var lineMap))
                    target[d.FilePath] = lineMap = new Dictionary<int, string>();

                string msg = $"{d.Code}: {d.Message}";
                // Accumulate multiple diagnostics on the same line
                if (lineMap.TryGetValue(d.Line, out string? existing))
                    lineMap[d.Line] = existing + "\n" + msg;
                else
                    lineMap[d.Line] = msg;
            }

            return (errors, warnings);
        }
    }
}
