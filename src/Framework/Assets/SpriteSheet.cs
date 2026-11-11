using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;

namespace GameEditor.Framework.Assets
{
    /// <summary>One frame of a sheet: a rectangle of its image in pixels, from the image's top-left corner.</summary>
    public struct SheetFrame
    {
        public string Name;
        public int X, Y, W, H;
        /// <summary>The point of the frame that sits on the entity's position: (0, 0) bottom-left, (1, 1) top-right.</summary>
        public Vector2 Pivot;
        /// <summary>Members of the file's frame this class does not know (name and raw JSON), kept when it is written.</summary>
        public List<KeyValuePair<string, string>>? Extra;
        /// <summary>The file gives a rectangle for this frame (a frame named as a grid cell may give only a pivot).</summary>
        public bool HasRect;
    }

    /// <summary>A run of a sheet's frames, played by a SpriteAnim component.</summary>
    public sealed class SpriteClip
    {
        public string Name = "";
        public List<string> Frames = new();
        public float Fps = 10f;
        public bool Loop = true;
        public List<KeyValuePair<string, string>>? Extra;

        // What a sprite's Tex is set to for each frame ("<sheet>#<frame>"), made once so that playing allocates nothing.
        internal string[] Keys = Array.Empty<string>();
    }

    /// <summary>
    /// A sprite sheet file (<c>.sprites</c>): an image cut into named frames — by a grid, by rectangles, or both —
    /// with their pivots, the pixels that make one world unit, how the image is sampled, and clips (runs of frames)
    /// for animation. The frames of a grid depend on the image's size, so they are asked for with it
    /// (<see cref="FramesFor"/>).
    /// </summary>
    public sealed class SpriteSheet
    {
        public const string Extension = ".sprites";

        /// <summary>The sheet's own key ("Sprites/hero.sprites").</summary>
        public string Key = "";
        /// <summary>The key of the image the frames are cut from.</summary>
        public string Image = "";
        public float PixelsPerUnit = 100f;
        /// <summary>Sample without smoothing (pixel art): the file's "filter" is "nearest".</summary>
        public bool Nearest;

        /// <summary>The image is cut into cells of <see cref="CellW"/> × <see cref="CellH"/> pixels, row by row from the top-left.</summary>
        public bool HasGrid;
        public int CellW, CellH, Margin, Spacing;
        /// <summary>The cells are named "<c>GridName</c>_0", "_1", …; when the file gives none, the sheet's file name.</summary>
        public string GridName = "";
        public List<KeyValuePair<string, string>>? GridExtra;

        /// <summary>The frames the file lists by name (rectangles of their own, or a grid cell's name with its own pivot).</summary>
        public List<SheetFrame> Frames = new();
        public List<SpriteClip> Clips = new();
        /// <summary>Root members this class does not know, kept when the file is written.</summary>
        public List<KeyValuePair<string, string>>? Extra;

        private static readonly JsonDocumentOptions Lenient = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

        public static bool IsSheetKey(string? key) => key != null && key.EndsWith(Extension, StringComparison.OrdinalIgnoreCase);

        /// <summary>The name the cells of a grid get when the file gives none: the file's name without its ending.</summary>
        public static string DefaultGridName(string key) => System.IO.Path.GetFileNameWithoutExtension(key);

        // ── Reading ─────────────────────────────────────────────────────────────────────

        /// <summary>Reads a sheet file's text. Null, with the reason in <paramref name="error"/>, when it is not one.</summary>
        public static SpriteSheet? Parse(string key, string json, out string? error)
        {
            error = null;
            json = AssetRefs.Resolve(json);   // an image that moved is asked for where it is now
            try
            {
                using var doc = JsonDocument.Parse(json, Lenient);
                JsonElement root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("sprites", out var version) || version.ValueKind != JsonValueKind.Number)
                {
                    error = "not a sprite sheet (no \"sprites\" member)";
                    return null;
                }

                var sheet = new SpriteSheet { Key = key, GridName = DefaultGridName(key) };
                foreach (JsonProperty m in root.EnumerateObject())
                {
                    switch (m.Name)
                    {
                        case "sprites": break;
                        case AssetRefs.Member: break;   // the id table is made again when the file is written
                        case "image":  sheet.Image = m.Value.GetString() ?? ""; break;
                        case "ppu":    if (m.Value.ValueKind == JsonValueKind.Number && m.Value.GetSingle() > 0f) sheet.PixelsPerUnit = m.Value.GetSingle(); break;
                        case "filter": sheet.Nearest = string.Equals(m.Value.GetString(), "nearest", StringComparison.OrdinalIgnoreCase); break;
                        case "grid":   ReadGrid(sheet, m.Value); break;
                        case "frames": ReadFrames(sheet, m.Value); break;
                        case "clips":  ReadClips(sheet, m.Value); break;
                        default: (sheet.Extra ??= new()).Add(new(m.Name, m.Value.GetRawText())); break;
                    }
                }
                sheet.MakeClipKeys();
                return sheet;
            }
            catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException)
            {
                error = "not valid: " + e.Message;
                return null;
            }
        }

        private static void ReadGrid(SpriteSheet sheet, JsonElement grid)
        {
            if (grid.ValueKind != JsonValueKind.Object) return;
            foreach (JsonProperty m in grid.EnumerateObject())
            {
                switch (m.Name)
                {
                    case "w":       sheet.CellW   = m.Value.GetInt32(); break;
                    case "h":       sheet.CellH   = m.Value.GetInt32(); break;
                    case "margin":  sheet.Margin  = m.Value.GetInt32(); break;
                    case "spacing": sheet.Spacing = m.Value.GetInt32(); break;
                    case "name":    if (m.Value.GetString() is { Length: > 0 } name) sheet.GridName = name; break;
                    default: (sheet.GridExtra ??= new()).Add(new(m.Name, m.Value.GetRawText())); break;
                }
            }
            sheet.HasGrid = sheet.CellW > 0 && sheet.CellH > 0;
        }

        private static void ReadFrames(SpriteSheet sheet, JsonElement frames)
        {
            if (frames.ValueKind != JsonValueKind.Array) return;
            foreach (JsonElement f in frames.EnumerateArray())
            {
                if (f.ValueKind != JsonValueKind.Object) continue;
                var frame = new SheetFrame { Name = "", Pivot = new Vector2(0.5f, 0.5f) };
                foreach (JsonProperty m in f.EnumerateObject())
                {
                    switch (m.Name)
                    {
                        case "name": frame.Name = m.Value.GetString() ?? ""; break;
                        case "x": frame.X = m.Value.GetInt32(); break;
                        case "y": frame.Y = m.Value.GetInt32(); break;
                        case "w": frame.W = m.Value.GetInt32(); break;
                        case "h": frame.H = m.Value.GetInt32(); break;
                        case "pivot":
                            if (m.Value.ValueKind == JsonValueKind.Array && m.Value.GetArrayLength() == 2)
                                frame.Pivot = new Vector2(m.Value[0].GetSingle(), m.Value[1].GetSingle());
                            break;
                        default: (frame.Extra ??= new()).Add(new(m.Name, m.Value.GetRawText())); break;
                    }
                }
                frame.HasRect = frame.W > 0 && frame.H > 0;
                if (frame.Name.Length > 0) sheet.Frames.Add(frame);
            }
        }

        private static void ReadClips(SpriteSheet sheet, JsonElement clips)
        {
            if (clips.ValueKind != JsonValueKind.Object) return;
            foreach (JsonProperty c in clips.EnumerateObject())
            {
                if (c.Value.ValueKind != JsonValueKind.Object) continue;
                var clip = new SpriteClip { Name = c.Name };
                foreach (JsonProperty m in c.Value.EnumerateObject())
                {
                    switch (m.Name)
                    {
                        case "frames":
                            if (m.Value.ValueKind == JsonValueKind.Array)
                                foreach (JsonElement name in m.Value.EnumerateArray())
                                    if (name.GetString() is { Length: > 0 } n) clip.Frames.Add(n);
                            break;
                        case "fps":  if (m.Value.ValueKind == JsonValueKind.Number) clip.Fps = m.Value.GetSingle(); break;
                        case "loop": clip.Loop = m.Value.ValueKind != JsonValueKind.False; break;
                        default: (clip.Extra ??= new()).Add(new(m.Name, m.Value.GetRawText())); break;
                    }
                }
                sheet.Clips.Add(clip);
            }
        }

        /// <summary>The clip of that name, or null.</summary>
        public SpriteClip? Clip(string? name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (SpriteClip c in Clips)
                if (c.Name == name) return c;
            return null;
        }

        /// <summary>Call after the clips or the key were changed in code: makes what a clip sets a sprite's Tex to.</summary>
        public void MakeClipKeys()
        {
            foreach (SpriteClip clip in Clips)
            {
                clip.Keys = new string[clip.Frames.Count];
                for (int i = 0; i < clip.Keys.Length; i++) clip.Keys[i] = Key + "#" + clip.Frames[i];
            }
        }

        // ── Frames ──────────────────────────────────────────────────────────────────────

        private List<SheetFrame>? _resolved;
        private Dictionary<string, int>? _byName;
        private int _resolvedW = -1, _resolvedH = -1;

        /// <summary>Cells across and down that a grid cuts an image of this size into.</summary>
        public (int Columns, int Rows) GridCells(int imageW, int imageH)
        {
            if (!HasGrid) return (0, 0);
            int columns = (imageW - 2 * Margin + Spacing) / (CellW + Spacing);
            int rows    = (imageH - 2 * Margin + Spacing) / (CellH + Spacing);
            return (Math.Max(columns, 0), Math.Max(rows, 0));
        }

        /// <summary>
        /// The sheet's frames in their order for an image of this size: the grid's cells row by row from the
        /// top-left, named "<see cref="GridName"/>_0" on, then the listed frames. A listed frame with the name of
        /// a cell takes the cell's place (and its rectangle, when it gives none of its own).
        /// A tile map's tile number n is the n-th of these, from 1.
        /// </summary>
        public IReadOnlyList<SheetFrame> FramesFor(int imageW, int imageH)
        {
            if (_resolved != null && _resolvedW == imageW && _resolvedH == imageH) return _resolved;
            var list = new List<SheetFrame>();
            var byName = new Dictionary<string, int>(StringComparer.Ordinal);

            var (columns, rows) = GridCells(imageW, imageH);
            for (int row = 0; row < rows; row++)
                for (int column = 0; column < columns; column++)
                {
                    string name = GridName + "_" + list.Count.ToString(CultureInfo.InvariantCulture);
                    byName[name] = list.Count;
                    list.Add(new SheetFrame
                    {
                        Name = name, X = Margin + column * (CellW + Spacing), Y = Margin + row * (CellH + Spacing),
                        W = CellW, H = CellH, Pivot = new Vector2(0.5f, 0.5f), HasRect = true,
                    });
                }

            foreach (SheetFrame listed in Frames)
            {
                SheetFrame f = listed;
                if (byName.TryGetValue(f.Name, out int at))
                {
                    if (!f.HasRect) { SheetFrame cell = list[at]; f.X = cell.X; f.Y = cell.Y; f.W = cell.W; f.H = cell.H; }
                    list[at] = f;
                }
                else if (f.HasRect)
                {
                    byName[f.Name] = list.Count;
                    list.Add(f);
                }
            }

            _resolved = list; _byName = byName; _resolvedW = imageW; _resolvedH = imageH;
            return list;
        }

        /// <summary>The frame of that name for an image of this size.</summary>
        public bool TryGetFrame(string name, int imageW, int imageH, out SheetFrame frame)
        {
            IReadOnlyList<SheetFrame> frames = FramesFor(imageW, imageH);
            if (_byName!.TryGetValue(name, out int at)) { frame = frames[at]; return true; }
            frame = default;
            return false;
        }

        /// <summary>Call after the grid or the listed frames were changed in code.</summary>
        public void FramesChanged() { _resolved = null; _byName = null; _resolvedW = _resolvedH = -1; }

        // ── Writing ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// The sheet as its file's text: the known members in a fixed order, then the members it was read with
        /// and does not know, unchanged. The id table is not written here (<see cref="AssetRefs.Stamp"/> adds it).
        /// </summary>
        public string ToJson()
        {
            var sb = new StringBuilder();
            sb.Append("{\n  \"sprites\": 1");
            sb.Append(",\n  \"image\": ").Append(Text(Image));
            sb.Append(",\n  \"ppu\": ").Append(Num(PixelsPerUnit));
            sb.Append(",\n  \"filter\": ").Append(Nearest ? "\"nearest\"" : "\"linear\"");
            if (HasGrid || GridExtra != null)
            {
                sb.Append(",\n  \"grid\": { \"w\": ").Append(CellW).Append(", \"h\": ").Append(CellH)
                  .Append(", \"margin\": ").Append(Margin).Append(", \"spacing\": ").Append(Spacing)
                  .Append(", \"name\": ").Append(Text(GridName));
                AppendExtra(sb, GridExtra, ", ");
                sb.Append(" }");
            }
            if (Frames.Count > 0)
            {
                sb.Append(",\n  \"frames\": [");
                for (int i = 0; i < Frames.Count; i++)
                {
                    SheetFrame f = Frames[i];
                    sb.Append(i == 0 ? "\n    { " : ",\n    { ").Append("\"name\": ").Append(Text(f.Name));
                    if (f.HasRect)
                        sb.Append(", \"x\": ").Append(f.X).Append(", \"y\": ").Append(f.Y).Append(", \"w\": ").Append(f.W).Append(", \"h\": ").Append(f.H);
                    sb.Append(", \"pivot\": [").Append(Num(f.Pivot.X)).Append(", ").Append(Num(f.Pivot.Y)).Append(']');
                    AppendExtra(sb, f.Extra, ", ");
                    sb.Append(" }");
                }
                sb.Append("\n  ]");
            }
            if (Clips.Count > 0)
            {
                sb.Append(",\n  \"clips\": {");
                for (int i = 0; i < Clips.Count; i++)
                {
                    SpriteClip c = Clips[i];
                    sb.Append(i == 0 ? "\n    " : ",\n    ").Append(Text(c.Name)).Append(": { \"frames\": [");
                    for (int k = 0; k < c.Frames.Count; k++) sb.Append(k == 0 ? "" : ", ").Append(Text(c.Frames[k]));
                    sb.Append("], \"fps\": ").Append(Num(c.Fps)).Append(", \"loop\": ").Append(c.Loop ? "true" : "false");
                    AppendExtra(sb, c.Extra, ", ");
                    sb.Append(" }");
                }
                sb.Append("\n  }");
            }
            AppendExtra(sb, Extra, ",\n  ");
            return sb.Append("\n}\n").ToString();
        }

        private static void AppendExtra(StringBuilder sb, List<KeyValuePair<string, string>>? extra, string separator)
        {
            if (extra == null) return;
            foreach (var member in extra) sb.Append(separator).Append(Text(member.Key)).Append(": ").Append(member.Value);
        }

        private static string Text(string s) => "\"" + JsonEncodedText.Encode(s, System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping).ToString() + "\"";
        private static string Num(float v) => v.ToString("G", CultureInfo.InvariantCulture);
    }
}
