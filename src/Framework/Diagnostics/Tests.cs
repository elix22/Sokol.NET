using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using GameEditor.Framework.Core;
using GameEditor.Framework.Determinism;

namespace GameEditor.Framework.Diagnostics
{
    /// <summary>A test's assertion failed (<see cref="Tests.Equal"/>, <see cref="Tests.True"/>, <see cref="Tests.Near"/>).</summary>
    public sealed class TestFailure : Exception
    {
        public TestFailure(string message) : base(message) { }
    }

    /// <summary>
    /// Script tests (G11.6, the GameEditor's docs/QOL_DESIGN.md; the reference editor's Test Runner): a script registers its
    /// tests in <c>OnStart</c> - <c>Tests.Add("jump height", () =&gt; Tests.Near(2f, JumpHeight(), 0.01f))</c> - and the
    /// Test Runner runs them in the editor's Play or on the attached development game; the determinism kit's golden tests
    /// are listed with them. No reflection: a test is a delegate (built games run them).
    /// </summary>
    public static class Tests
    {
        public readonly struct Result
        {
            public readonly string Name, Message;
            public readonly bool Passed;
            public readonly double Ms;
            public Result(string name, bool passed, double ms, string message) { Name = name; Passed = passed; Ms = ms; Message = message; }
        }

        static readonly List<(string Name, Action Body)> _tests = new();

        /// <summary>Registers a test (again under its name: replaced where it was).</summary>
        public static void Add(string name, Action body)
        {
            for (int i = 0; i < _tests.Count; i++)
                if (_tests[i].Name == name) { _tests[i] = (name, body); return; }
            _tests.Add((name, body));
        }

        public static void Clear() => _tests.Clear();

        /// <summary>The script tests' names, then the golden tests' ("golden: " and the name).</summary>
        public static List<string> Names()
        {
            var n = new List<string>(_tests.Count + GoldenTests.Count);
            foreach (var t in _tests) n.Add(t.Name);
            foreach (var g in GoldenTests.Names()) n.Add("golden: " + g);
            return n;
        }

        public static void Equal<T>(T expected, T actual, string? what = null)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new TestFailure($"{(what != null ? what + ": " : "")}expected {Show(expected)}, got {Show(actual)}");
        }

        public static void True(bool condition, string? message = null)
        {
            if (!condition) throw new TestFailure(message ?? "expected true");
        }

        public static void Near(float expected, float actual, float tolerance, string? what = null)
        {
            if (!(MathF.Abs(expected - actual) <= tolerance))
                throw new TestFailure($"{(what != null ? what + ": " : "")}expected {Show(expected)} ± {Show(tolerance)}, got {Show(actual)}");
        }

        static string Show(object? v) => v switch
        {
            null => "null",
            float f => f.ToString("R", CultureInfo.InvariantCulture),
            double d => d.ToString("R", CultureInfo.InvariantCulture),
            string s => "\"" + s + "\"",
            _ => Convert.ToString(v, CultureInfo.InvariantCulture) ?? "?",
        };

        /// <summary>Runs the named tests (null: all), each logged "[TEST] name PASS|FAIL (ms) message".</summary>
        public static List<Result> Run(ICollection<string>? names = null)
        {
            var results = new List<Result>();
            foreach (var (name, body) in _tests.ToArray())
            {
                if (names != null && !names.Contains(name)) continue;
                var sw = Stopwatch.StartNew();
                string message = "";
                bool ok;
                try { body(); ok = true; }
                catch (TestFailure f) { ok = false; message = f.Message; }
                catch (Exception e) { ok = false; message = e.GetType().Name + ": " + e.Message; }
                results.Add(new Result(name, ok, sw.Elapsed.TotalMilliseconds, message));
            }
            var golden = new List<string>();
            foreach (var g in GoldenTests.Names())
                if (names == null || names.Contains("golden: " + g)) golden.Add(g);
            if (golden.Count > 0)
            {
                var sw = Stopwatch.StartNew();
                foreach (var r in GoldenTests.RunAll())
                    if (golden.Contains(r.Name))
                        results.Add(new Result("golden: " + r.Name, r.Passed, sw.Elapsed.TotalMilliseconds,
                            r.Golden == 0 ? $"hash {r.Hash:x16} (not pinned)" : r.Passed ? $"hash {r.Hash:x16}" : $"hash {r.Hash:x16}, want {r.Golden:x16}"));
            }
            foreach (var r in results)
                Logger.Info($"[TEST] {r.Name} {(r.Passed ? "PASS" : "FAIL")} ({r.Ms.ToString("0.0", CultureInfo.InvariantCulture)} ms){(r.Message.Length > 0 ? " " + r.Message : "")}");
            return results;
        }

        // ── The player link ───────────────────────────────────────────────────────────────
        internal static void Link()
        {
            PlayerLink.Received += (t, line) =>
            {
                if (t == "tests.list") PlayerLink.Send(NamesLine());
                else if (t == "tests.run") PlayerLink.Send(ResultsLine(Run(NamesIn(line))));
            };
        }

        static string NamesLine()
        {
            var sb = new StringBuilder("{\"t\":\"tests\",\"n\":[");
            var names = Names();
            for (int i = 0; i < names.Count; i++) { if (i > 0) sb.Append(','); sb.Append(PlayerLink.Str(names[i])); }
            return sb.Append("]}").ToString();
        }

        static string ResultsLine(List<Result> results)
        {
            var sb = new StringBuilder("{\"t\":\"testresults\",\"r\":[");
            for (int i = 0; i < results.Count; i++)
            {
                var r = results[i];
                if (i > 0) sb.Append(',');
                sb.Append('[').Append(PlayerLink.Str(r.Name)).Append(',').Append(r.Passed ? "true" : "false").Append(',')
                  .Append(r.Ms.ToString("0.###", CultureInfo.InvariantCulture)).Append(',').Append(PlayerLink.Str(r.Message)).Append(']');
            }
            return sb.Append("]}").ToString();
        }

        // {"t":"tests.run","n":[...]} - the names, or null (all) when there are none.
        static HashSet<string>? NamesIn(string line)
        {
            using var doc = System.Text.Json.JsonDocument.Parse(line);
            if (!doc.RootElement.TryGetProperty("n", out var n) || n.ValueKind != System.Text.Json.JsonValueKind.Array) return null;
            var set = new HashSet<string>();
            foreach (var e in n.EnumerateArray()) if (e.GetString() is { } s) set.Add(s);
            return set;
        }
    }
}
