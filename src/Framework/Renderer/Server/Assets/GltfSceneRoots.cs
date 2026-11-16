using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace GameEditor.Framework.Renderer.Server.Assets
{
    /// <summary>
    /// A glTF whose scene lists a node that is another node's child (the spec says a scene's nodes are roots): some exporters
    /// write it (UniGLTF: Kenney's nature kit lists its mesh node, the child of a "tmpParent"), and cgltf refuses the whole
    /// file. <see cref="Fixed"/> gives the file with each such node replaced by its topmost ancestor - what the other
    /// engines draw - or null when no scene lists a child (cgltf refused it for something else).
    /// </summary>
    public static class GltfSceneRoots
    {
        public static byte[]? Fixed(byte[] file)
        {
            bool glb = file.Length >= 20 && file[0] == (byte)'g' && file[1] == (byte)'l' && file[2] == (byte)'T' && file[3] == (byte)'F';
            int jsonAt = 0, jsonLength = file.Length;
            if (glb)
            {
                jsonLength = (int)BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(12));
                if (BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(16)) != 0x4E4F534Au || 20 + jsonLength > file.Length) return null;   // "JSON"
                jsonAt = 20;
            }
            byte[]? json;
            try { json = FixedJson(file.AsMemory(jsonAt, jsonLength)); }
            catch (JsonException) { return null; }
            if (json == null) return null;
            if (!glb) return json;

            // The JSON chunk again, padded with spaces to 4 bytes; the chunks after it (the binary) as they were.
            int padded = (json.Length + 3) & ~3;
            int rest = file.Length - (20 + jsonLength);
            var o = new byte[20 + padded + rest];
            file.AsSpan(0, 12).CopyTo(o);
            BinaryPrimitives.WriteUInt32LittleEndian(o.AsSpan(8), (uint)o.Length);
            BinaryPrimitives.WriteUInt32LittleEndian(o.AsSpan(12), (uint)padded);
            BinaryPrimitives.WriteUInt32LittleEndian(o.AsSpan(16), 0x4E4F534Au);
            json.CopyTo(o, 20);
            o.AsSpan(20 + json.Length, padded - json.Length).Fill((byte)' ');
            file.AsSpan(20 + jsonLength, rest).CopyTo(o.AsSpan(20 + padded));
            return o;
        }

        static byte[]? FixedJson(ReadOnlyMemory<byte> json)
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true });
            var root = doc.RootElement;
            if (!root.TryGetProperty("scenes", out var scenes) || scenes.ValueKind != JsonValueKind.Array) return null;

            // Each node's parent.
            var parent = new Dictionary<int, int>();
            if (root.TryGetProperty("nodes", out var nodes) && nodes.ValueKind == JsonValueKind.Array)
            {
                int i = 0;
                foreach (var n in nodes.EnumerateArray())
                {
                    if (n.TryGetProperty("children", out var children) && children.ValueKind == JsonValueKind.Array)
                        foreach (var c in children.EnumerateArray())
                            if (c.TryGetInt32(out int ci)) parent.TryAdd(ci, i);
                    i++;
                }
            }
            int Top(int n)
            {
                for (int guard = 0; guard < 1024 && parent.TryGetValue(n, out int p); guard++) n = p;
                return n;
            }

            // Each scene's nodes: their topmost ancestors, each once, in order.
            var fixedScenes = new List<List<int>?>();
            bool any = false;
            foreach (var s in scenes.EnumerateArray())
            {
                if (!s.TryGetProperty("nodes", out var list) || list.ValueKind != JsonValueKind.Array) { fixedScenes.Add(null); continue; }
                var tops = new List<int>();
                bool changed = false;
                foreach (var e in list.EnumerateArray())
                {
                    if (!e.TryGetInt32(out int n)) continue;
                    int t = Top(n);
                    changed |= t != n;
                    if (!tops.Contains(t)) tops.Add(t);
                }
                fixedScenes.Add(changed ? tops : null);
                any |= changed;
            }
            if (!any) return null;

            using var ms = new MemoryStream();
            using (var w = new Utf8JsonWriter(ms))
            {
                w.WriteStartObject();
                foreach (var prop in root.EnumerateObject())
                {
                    if (prop.Name != "scenes") { prop.WriteTo(w); continue; }
                    w.WritePropertyName("scenes");
                    w.WriteStartArray();
                    int si = 0;
                    foreach (var s in scenes.EnumerateArray())
                    {
                        var tops = fixedScenes[si++];
                        if (tops == null) { s.WriteTo(w); continue; }
                        w.WriteStartObject();
                        foreach (var sp in s.EnumerateObject())
                        {
                            if (sp.Name != "nodes") { sp.WriteTo(w); continue; }
                            w.WritePropertyName("nodes");
                            w.WriteStartArray();
                            foreach (int t in tops) w.WriteNumberValue(t);
                            w.WriteEndArray();
                        }
                        w.WriteEndObject();
                    }
                    w.WriteEndArray();
                }
                w.WriteEndObject();
            }
            return ms.ToArray();
        }
    }
}
