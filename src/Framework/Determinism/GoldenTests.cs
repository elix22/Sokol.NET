using System;
using System.Collections.Generic;
using GameEditor.Framework.Core;

namespace GameEditor.Framework.Determinism
{
    /// <summary>
    /// The golden-hash runner (G10, docs DETERMINISM_DESIGN.md §3): a game registers its deterministic simulations - each
    /// steps a world and hashes it - with the hash every device must make (0: not pinned yet); <see cref="RunAll"/> runs
    /// them and logs <c>[DET] &lt;name&gt; PASS|FAIL hash=… want=…</c>. No reflection: built games run it.
    /// </summary>
    public static class GoldenTests
    {
        public delegate void Run(ref StateHash hash);

        /// <summary>One test's outcome (a plain struct: NativeAOT and record structs have bitten before).</summary>
        public readonly struct Result
        {
            public readonly string Name; public readonly ulong Hash, Golden; public readonly bool Passed;
            public Result(string name, ulong hash, ulong golden, bool passed) { Name = name; Hash = hash; Golden = golden; Passed = passed; }
        }

        private static readonly List<(string Name, ulong Golden, Run Run)> _tests = new();

        public static void Add(string name, ulong golden, Run run)
        {
            for (int i = 0; i < _tests.Count; i++)
                if (_tests[i].Name == name) { _tests[i] = (name, golden, run); return; }   // registered again: replaced where it was
            _tests.Add((name, golden, run));
        }

        public static void Clear() => _tests.Clear();
        public static int Count => _tests.Count;

        public static List<Result> RunAll()
        {
            var results = new List<Result>(_tests.Count);
            foreach (var (name, golden, run) in _tests)
            {
                var h = new StateHash();
                run(ref h);
                bool ok = golden == 0 || h.Value == golden;
                results.Add(new Result(name, h.Value, golden, ok));
                Logger.Info($"[DET] {name} {(ok ? "PASS" : "FAIL")} hash={h.Hex} want={(golden == 0 ? "(not pinned)" : golden.ToString("x16"))}");
            }
            return results;
        }
    }
}
