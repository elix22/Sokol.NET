using System;

namespace GameEditor.Framework.Determinism
{
    /// <summary>
    /// A seeded random generator whose sequence is the same on every device, runtime and version (G10, docs
    /// DETERMINISM_DESIGN.md §3) - the reference editor's math library's <c>Random</c>: xorshift32 (13, 17, 5) over a
    /// <c>uint</c> state. Its <see cref="State"/> is the whole generator: saved, restored, sent with a turn.
    /// </summary>
    public struct DetRandom
    {
        /// <summary>The generator's whole state (never 0).</summary>
        public uint State;

        public DetRandom(uint seed) { State = 0; InitState(seed); }

        /// <summary>Starts again from <paramref name="seed"/> (0 is taken as 0x6E624EB7: xorshift never leaves 0).</summary>
        public void InitState(uint seed)
        {
            State = seed == 0 ? 0x6E624EB7u : seed;
            NextState();   // the first value already mixed
        }

        private uint NextState()
        {
            uint x = State;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            State = x;
            return x;
        }

        /// <summary>A uint, any of its 2³² - 1 non-zero values.</summary>
        public uint NextUInt() => NextState() - 1u;

        /// <summary>A uint in [0, max) (0 when max is 0).</summary>
        public uint NextUInt(uint max) => max == 0 ? 0u : (uint)(((ulong)NextUInt() * max) >> 32);

        /// <summary>An int in [min, max) (min when max ≤ min).</summary>
        public int NextInt(int min, int max) => max <= min ? min : min + (int)NextUInt((uint)(max - min));

        /// <summary>A float in [0, 1): 24 random bits.</summary>
        public float NextFloat() => (NextUInt() >> 8) * (1f / 16777216f);

        /// <summary>A float in [min, max).</summary>
        public float NextFloat(float min, float max) => min + (max - min) * NextFloat();

        public bool NextBool() => (NextUInt() & 1u) != 0;
    }
}
