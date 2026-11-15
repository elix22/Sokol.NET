using System;
using System.Numerics;
using System.Text;

namespace GameEditor.Framework.Determinism
{
    /// <summary>
    /// A simulation's state as one 64-bit number (G10, docs DETERMINISM_DESIGN.md §3): FNV-1a over the values' bytes
    /// (floats by their bits), so two devices compare a whole world in one line.
    /// </summary>
    public struct StateHash
    {
        private const ulong Offset = 14695981039346656037UL, Prime = 1099511628211UL;
        private ulong _h;
        private bool _started;

        public ulong Value => _started ? _h : Offset;
        public string Hex => Value.ToString("x16");

        private void Byte(byte b) { if (!_started) { _h = Offset; _started = true; } _h = (_h ^ b) * Prime; }
        private void Bytes(ulong v, int n) { for (int i = 0; i < n; i++) Byte((byte)(v >> (8 * i))); }

        public void Add(int v) => Bytes((uint)v, 4);
        public void Add(uint v) => Bytes(v, 4);
        public void Add(long v) => Bytes((ulong)v, 8);
        public void Add(ulong v) => Bytes(v, 8);
        public void Add(bool v) => Byte(v ? (byte)1 : (byte)0);
        public void Add(float v) => Bytes((uint)BitConverter.SingleToInt32Bits(v), 4);
        public void Add(double v) => Bytes((ulong)BitConverter.DoubleToInt64Bits(v), 8);
        public void Add(Fixed v) => Add(v.Raw);
        public void Add(Vector2 v) { Add(v.X); Add(v.Y); }
        public void Add(Vector3 v) { Add(v.X); Add(v.Y); Add(v.Z); }
        public void Add(Quaternion q) { Add(q.X); Add(q.Y); Add(q.Z); Add(q.W); }
        public void Add(string s) { foreach (byte b in Encoding.UTF8.GetBytes(s)) Byte(b); Byte(0); }
    }
}
