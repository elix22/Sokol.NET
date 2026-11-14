using System;
using System.Collections.Generic;
using System.Numerics;

namespace GameEditor.Framework.Renderer.Server.Materials.Graph
{
    /// <summary>
    /// A material drawn by a shader graph (G8.10, docs SHADER_GRAPH_DESIGN.md §3.3): the graph's key and the material's own
    /// values of the graph's properties, by name (what the .pbrmat keeps). They are packed into graph_params (8 vec4: the
    /// properties in [0..6], the time in [7].x at each draw) at the places the graph's shader source gives, again whenever
    /// that source changes (the editor's Save). A property the material does not set takes the graph's default.
    /// </summary>
    public sealed class GraphBinding
    {
        public readonly string Key;
        /// <summary>The material's values of value properties, by name.</summary>
        public readonly Dictionary<string, Vector4> Values = new(StringComparer.Ordinal);
        /// <summary>The material's textures of texture properties (asset paths), by name.</summary>
        public readonly Dictionary<string, string> Textures = new(StringComparer.Ordinal);
        /// <summary>graph_params as drawn.</summary>
        public readonly float[] Params = new float[32];
        private IGraphShaderSource? _packedFor;
        private int _packedVersion;

        public GraphBinding(string key) => Key = key;

        /// <summary>The graph's shaders (null while its key is not registered: the engine's shader draws the material).</summary>
        public IGraphShaderSource? Source
        {
            get
            {
                var s = GraphShaders.Get(Key);
                if (s != null && (!ReferenceEquals(s, _packedFor) || s.Version != _packedVersion)) Pack(s);
                return s;
            }
        }

        private void Pack(IGraphShaderSource s)
        {
            _packedFor = s;
            _packedVersion = s.Version;
            Array.Clear(Params);
            foreach (var slot in s.Slots)
            {
                if (slot.Type == GraphPropertyType.Texture2D) continue;
                Vector4 v;
                if (!Values.TryGetValue(slot.Name, out v))
                {
                    float[] d = s.Default(slot.Name).Value;
                    v = new Vector4(At(d, 0), At(d, 1), At(d, 2), At(d, 3));
                }
                Write(slot.Slot, v);
            }
        }

        private static float At(float[] v, int i) => i < v.Length ? v[i] : 0f;

        private void Write(int slot, Vector4 v)
        {
            int o = slot * 4;
            Params[o] = v.X; Params[o + 1] = v.Y; Params[o + 2] = v.Z; Params[o + 3] = v.W;
        }

        /// <summary>Sets a value property (scripts: material.SetFloat / SetVector / SetColor); used at the next draw, no
        /// allocation once the name is known. False when the graph has no value property of that name.</summary>
        public bool Set(string name, Vector4 value)
        {
            var s = Source;
            Values[name] = value;
            if (s == null) return true;   // packed when the graph's shaders come
            foreach (var slot in s.Slots)
                if (slot.Name == name && slot.Type != GraphPropertyType.Texture2D) { Write(slot.Slot, value); return true; }
            return false;
        }

        public bool SetFloat(string name, float v) => Set(name, new Vector4(v, 0f, 0f, 0f));

        /// <summary>A value property's value now (the material's, else the graph's default).</summary>
        public Vector4 Get(string name)
        {
            if (Values.TryGetValue(name, out var v)) return v;
            float[] d = Source?.Default(name).Value ?? Array.Empty<float>();
            return new Vector4(At(d, 0), At(d, 1), At(d, 2), At(d, 3));
        }

        /// <summary>The material map slot (0-4) of a texture property; -1 when the graph has none of that name.</summary>
        public int TextureSlot(string name)
        {
            if (Source is { } s)
                foreach (var slot in s.Slots)
                    if (slot.Name == name && slot.Type == GraphPropertyType.Texture2D) return slot.Slot;
            return -1;
        }

        /// <summary>A texture property's path: the material's, else the graph's default.</summary>
        public string TexturePath(string name)
            => Textures.TryGetValue(name, out var t) && t.Length > 0 ? t : Source?.Default(name).Texture ?? "";
    }
}
