using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using GameEditor.Framework.Renderer.Server.Resources;

namespace GameEditor.Framework.Renderer.Server.GI
{
    /// <summary>
    /// What a bake leaves (M8a, docs BAKED_GI_DESIGN.md §3.4), in the scene's folder <c>&lt;SceneName&gt;/</c>:
    /// <c>Lightmap-n_light.png</c> (RGBM, range <see cref="RgbmRange"/>, linear), <c>Lightmap-n_dir.png</c> (directional:
    /// xyz the dominant direction 0..1, w its rebalancing), <c>LightingData.json</c> (pages, each lightmapped entity's
    /// page, the Baked lights) and <c>LightmapMeshes.bin</c> (each lightmapped entity's mesh, its lightmap UV in Uv1).
    /// Written by the editor; read by the editor and by games (JsonDocument and a plain binary reader: no reflection).
    /// </summary>
    public sealed class LightingData
    {
        public const string JsonFile = "LightingData.json", MeshFile = "LightmapMeshes.bin";
        public const float RgbmRange = 8f;
        private const uint MeshMagic = 0x424D4C53;   // "SLMB"
        private const int Version = 1;

        public sealed class Page { public string Light = ""; public string Direction = ""; public int Size; }

        public List<Page> Pages = new();
        /// <summary>Lightmapped entity (scene id) → its page.</summary>
        public Dictionary<int, int> EntityPage = new();
        /// <summary>The Baked lights (scene ids): not drawn on lightmapped surfaces, their light is in the maps.</summary>
        public List<int> BakedLights = new();
        public bool Directional;
        /// <summary>Light probes (M8b): where, and the radiance arriving there on SH L2 (9 colours).</summary>
        public List<(Vector3 Position, Vector3[] Radiance)> Probes = new();
        /// <summary>Lightmapped entity (scene id) → its mesh (null until <see cref="ReadMeshes"/>).</summary>
        public Dictionary<int, ObjSubMesh[]> Meshes = new();

        // ── writing (the editor) ─────────────────────────────────────────────────────────

        /// <summary>Writes a bake into <paramref name="folder"/> (made; files of an older bake there removed first).</summary>
        public static LightingData Write(string folder, BakeResult bake, IReadOnlyList<int> bakedLights, bool directional)
        {
            if (Directory.Exists(folder))
                foreach (var f in Directory.GetFiles(folder))
                {
                    string name = Path.GetFileName(f);
                    if (name.StartsWith("Lightmap-", StringComparison.Ordinal) || name == JsonFile || name == MeshFile) File.Delete(f);
                }
            Directory.CreateDirectory(folder);
            var data = new LightingData { Directional = directional };
            data.BakedLights.AddRange(bakedLights);
            for (int p = 0; p < bake.Pages.Count; p++)
            {
                var page = bake.Pages[p];
                var entry = new Page { Size = page.Size, Light = $"Lightmap-{p}_light.png", Direction = directional ? $"Lightmap-{p}_dir.png" : "" };
                File.WriteAllBytes(Path.Combine(folder, entry.Light), EncodePngRgba(EncodeRgbm(page.Light), page.Size, page.Size));
                if (directional)
                    File.WriteAllBytes(Path.Combine(folder, entry.Direction), EncodePngRgba(EncodeUnit(page.Direction), page.Size, page.Size));
                data.Pages.Add(entry);
            }
            foreach (var e in bake.Entities) { data.EntityPage[e.SceneId] = e.Page; data.Meshes[e.SceneId] = e.Mesh; }
            data.Probes.AddRange(bake.Probes);
            File.WriteAllText(Path.Combine(folder, JsonFile), data.ToJson());
            File.WriteAllBytes(Path.Combine(folder, MeshFile), data.MeshBytes());
            return data;
        }

        /// <summary>Linear light → RGBM bytes: rgb / (m × range), m in alpha (rounded up, never 0).</summary>
        public static byte[] EncodeRgbm(Vector3[] light)
        {
            var b = new byte[light.Length * 4];
            for (int i = 0; i < light.Length; i++)
            {
                var c = Vector3.Max(light[i], Vector3.Zero);
                float m = Math.Clamp(MathF.Max(c.X, MathF.Max(c.Y, c.Z)) / RgbmRange, 1f / 255f, 1f);
                m = MathF.Ceiling(m * 255f) / 255f;
                var e = c / (m * RgbmRange);
                b[i * 4] = Byte(e.X); b[i * 4 + 1] = Byte(e.Y); b[i * 4 + 2] = Byte(e.Z); b[i * 4 + 3] = Byte(m);
            }
            return b;
        }

        public static Vector3 DecodeRgbm(byte r, byte g, byte bl, byte a) => new Vector3(r, g, bl) / 255f * (a / 255f * RgbmRange);

        private static byte[] EncodeUnit(Vector4[] v)
        {
            var b = new byte[v.Length * 4];
            for (int i = 0; i < v.Length; i++) { b[i * 4] = Byte(v[i].X); b[i * 4 + 1] = Byte(v[i].Y); b[i * 4 + 2] = Byte(v[i].Z); b[i * 4 + 3] = Byte(v[i].W); }
            return b;
        }

        /// <summary>An 8-bit RGBA PNG (colour type 6: the alpha kept - RGBM's multiplier, the direction's rebalancing).</summary>
        public static byte[] EncodePngRgba(byte[] rgba, int w, int h)
        {
            var raw = new MemoryStream((w * 4 + 1) * h);
            for (int y = 0; y < h; y++) { raw.WriteByte(0); raw.Write(rgba, y * w * 4, w * 4); }
            var deflated = new MemoryStream();
            using (var z = new System.IO.Compression.ZLibStream(deflated, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
                z.Write(raw.GetBuffer(), 0, (int)raw.Length);
            var png = new MemoryStream();
            png.Write(new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A });
            var ihdr = new byte[13];
            BE32(ihdr, 0, (uint)w); BE32(ihdr, 4, (uint)h);
            ihdr[8] = 8; ihdr[9] = 6;   // 8 bits, RGBA
            Chunk(png, "IHDR", ihdr, ihdr.Length);
            Chunk(png, "IDAT", deflated.GetBuffer(), (int)deflated.Length);
            Chunk(png, "IEND", Array.Empty<byte>(), 0);
            return png.ToArray();
        }

        private static void BE32(byte[] d, int at, uint v) { d[at] = (byte)(v >> 24); d[at + 1] = (byte)(v >> 16); d[at + 2] = (byte)(v >> 8); d[at + 3] = (byte)v; }

        private static void Chunk(Stream png, string type, byte[] data, int length)
        {
            var head = new byte[8];
            BE32(head, 0, (uint)length);
            for (int i = 0; i < 4; i++) head[4 + i] = (byte)type[i];
            png.Write(head, 0, 8);
            png.Write(data, 0, length);
            uint crc = 0xFFFFFFFFu;
            for (int i = 4; i < 8; i++) crc = Crc[(crc ^ head[i]) & 0xFF] ^ (crc >> 8);
            for (int i = 0; i < length; i++) crc = Crc[(crc ^ data[i]) & 0xFF] ^ (crc >> 8);
            var tail = new byte[4];
            BE32(tail, 0, crc ^ 0xFFFFFFFFu);
            png.Write(tail, 0, 4);
        }

        private static readonly uint[] Crc = MakeCrc();
        private static uint[] MakeCrc()
        {
            var t = new uint[256];
            for (uint n = 0; n < 256; n++) { uint c = n; for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1; t[n] = c; }
            return t;
        }

        private static byte Byte(float x) => (byte)Math.Clamp((int)MathF.Round(x * 255f), 0, 255);

        public string ToJson()
        {
            using var ms = new MemoryStream();
            using (var w = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true }))
            {
                w.WriteStartObject();
                w.WriteNumber("version", Version);
                w.WriteBoolean("directional", Directional);
                w.WriteStartArray("pages");
                foreach (var p in Pages)
                {
                    w.WriteStartObject();
                    w.WriteString("light", p.Light);
                    if (p.Direction.Length > 0) w.WriteString("dir", p.Direction);
                    w.WriteNumber("size", p.Size);
                    w.WriteEndObject();
                }
                w.WriteEndArray();
                w.WriteStartArray("entities");
                foreach (var (id, page) in EntityPage) { w.WriteStartObject(); w.WriteNumber("id", id); w.WriteNumber("page", page); w.WriteEndObject(); }
                w.WriteEndArray();
                w.WriteStartArray("bakedLights");
                foreach (int id in BakedLights) w.WriteNumberValue(id);
                w.WriteEndArray();
                w.WriteStartArray("probes");
                foreach (var (pos, sh) in Probes)
                {
                    w.WriteStartObject();
                    w.WriteStartArray("p"); w.WriteNumberValue(pos.X); w.WriteNumberValue(pos.Y); w.WriteNumberValue(pos.Z); w.WriteEndArray();
                    w.WriteStartArray("sh");
                    foreach (var c in sh) { w.WriteNumberValue(c.X); w.WriteNumberValue(c.Y); w.WriteNumberValue(c.Z); }
                    w.WriteEndArray();
                    w.WriteEndObject();
                }
                w.WriteEndArray();
                w.WriteEndObject();
            }
            return Encoding.UTF8.GetString(ms.ToArray());
        }

        private byte[] MeshBytes()
        {
            using var ms = new MemoryStream();
            using (var w = new BinaryWriter(ms))
            {
                w.Write(MeshMagic); w.Write(Version); w.Write(Meshes.Count);
                foreach (var (id, subs) in Meshes)
                {
                    w.Write(id); w.Write(subs.Length);
                    foreach (var sm in subs)
                    {
                        w.Write(sm.MaterialName ?? "");
                        w.Write(sm.Vertices.Length);
                        w.Write(MemoryMarshal.AsBytes(sm.Vertices.AsSpan()));
                        w.Write(sm.Indices.Length);
                        w.Write(MemoryMarshal.AsBytes(sm.Indices.AsSpan()));
                    }
                }
            }
            return ms.ToArray();
        }

        // ── reading (editor and games) ───────────────────────────────────────────────────

        /// <summary>The bake's description from its LightingData.json text; null when it is not one.</summary>
        public static LightingData? Parse(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var r = doc.RootElement;
                if (r.ValueKind != JsonValueKind.Object || !r.TryGetProperty("pages", out var pages)) return null;
                var d = new LightingData { Directional = r.TryGetProperty("directional", out var dir) && dir.ValueKind == JsonValueKind.True };
                foreach (var p in pages.EnumerateArray())
                    d.Pages.Add(new Page
                    {
                        Light = p.TryGetProperty("light", out var l) ? l.GetString() ?? "" : "",
                        Direction = p.TryGetProperty("dir", out var dd) ? dd.GetString() ?? "" : "",
                        Size = p.TryGetProperty("size", out var s) && s.TryGetInt32(out int sz) ? sz : 0,
                    });
                if (r.TryGetProperty("entities", out var ents))
                    foreach (var e in ents.EnumerateArray())
                        if (e.TryGetProperty("id", out var id) && e.TryGetProperty("page", out var pg)) d.EntityPage[id.GetInt32()] = pg.GetInt32();
                if (r.TryGetProperty("bakedLights", out var bl))
                    foreach (var e in bl.EnumerateArray()) d.BakedLights.Add(e.GetInt32());
                if (r.TryGetProperty("probes", out var pr))
                    foreach (var e in pr.EnumerateArray())
                    {
                        if (!e.TryGetProperty("p", out var pp) || !e.TryGetProperty("sh", out var sv) || sv.GetArrayLength() != 27) continue;
                        var sh = new Vector3[9];
                        for (int k = 0; k < 9; k++) sh[k] = new Vector3(sv[k * 3].GetSingle(), sv[k * 3 + 1].GetSingle(), sv[k * 3 + 2].GetSingle());
                        d.Probes.Add((new Vector3(pp[0].GetSingle(), pp[1].GetSingle(), pp[2].GetSingle()), sh));
                    }
                return d;
            }
            catch (Exception) { return null; }
        }

        /// <summary>Reads LightmapMeshes.bin's bytes into <see cref="Meshes"/>; false when they are not one.</summary>
        public bool ReadMeshes(ReadOnlySpan<byte> bytes)
        {
            Meshes.Clear();
            try
            {
                int at = 0;
                uint U32(ReadOnlySpan<byte> b) { uint v = BitConverter.ToUInt32(b.Slice(at, 4)); at += 4; return v; }
                int I32(ReadOnlySpan<byte> b) { int v = BitConverter.ToInt32(b.Slice(at, 4)); at += 4; return v; }
                if (U32(bytes) != MeshMagic || I32(bytes) != Version) return false;
                int count = I32(bytes);
                for (int e = 0; e < count; e++)
                {
                    int id = I32(bytes), subCount = I32(bytes);
                    var subs = new ObjSubMesh[subCount];
                    for (int s = 0; s < subCount; s++)
                    {
                        // BinaryWriter's string: a 7-bit encoded length, then UTF-8
                        int len = 0, shift = 0; byte b;
                        do { b = bytes[at++]; len |= (b & 0x7F) << shift; shift += 7; } while ((b & 0x80) != 0);
                        string name = Encoding.UTF8.GetString(bytes.Slice(at, len)); at += len;
                        int vc = I32(bytes);
                        var verts = MemoryMarshal.Cast<byte, ObjVertex>(bytes.Slice(at, vc * Marshal.SizeOf<ObjVertex>())).ToArray(); at += vc * Marshal.SizeOf<ObjVertex>();
                        int ic = I32(bytes);
                        var idx = MemoryMarshal.Cast<byte, uint>(bytes.Slice(at, ic * 4)).ToArray(); at += ic * 4;
                        subs[s] = new ObjSubMesh { MaterialName = name, Vertices = verts, Indices = idx };
                    }
                    Meshes[id] = subs;
                }
                return true;
            }
            catch (Exception) { Meshes.Clear(); return false; }
        }
    }
}
