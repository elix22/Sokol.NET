using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;

namespace GameEditor.Framework.Assets
{
    /// <summary>
    /// One layer of a tile map: a rectangle of cells, each holding a tile number — 0 for none, n for the n-th
    /// frame of the map's sheet (from 1). Cells are addressed in the map's own coordinates, x to the right and y
    /// up; the layer covers the cells from (<see cref="OriginX"/>, <see cref="OriginY"/>), its bottom-left one.
    /// </summary>
    public sealed class TileLayer
    {
        public string Name = "";
        /// <summary>Added to the component's Order: a higher layer is drawn over a lower one.</summary>
        public int Order;
        public int OriginX, OriginY;
        /// <summary>Every tile of the layer is a wall for 2D bodies.</summary>
        public bool Solid;
        public int Width, Height;
        /// <summary>The cells row by row from the BOTTOM row, each row from the left: [(y - OriginY) * Width + (x - OriginX)].</summary>
        public int[] Cells = Array.Empty<int>();
        /// <summary>Members of the file's layer this class does not know (name and raw JSON), kept when it is written.</summary>
        public List<KeyValuePair<string, string>>? Extra;

        /// <summary>The tile in a cell of the map; 0 outside the layer.</summary>
        public int Get(int x, int y)
        {
            x -= OriginX; y -= OriginY;
            return x < 0 || y < 0 || x >= Width || y >= Height ? 0 : Cells[y * Width + x];
        }

        /// <summary>
        /// Puts a tile into a cell of the map. A cell outside the layer makes the layer larger to hold it (what
        /// is there stays where it is); putting 0 outside changes nothing. Returns false when nothing changed.
        /// </summary>
        public bool Set(int x, int y, int tile)
        {
            if (tile < 0) tile = 0;
            if (x < OriginX || y < OriginY || x >= OriginX + Width || y >= OriginY + Height)
            {
                if (tile == 0) return false;
                int x0 = Width == 0 ? x : Math.Min(OriginX, x), y0 = Height == 0 ? y : Math.Min(OriginY, y);
                int x1 = Width == 0 ? x : Math.Max(OriginX + Width - 1, x), y1 = Height == 0 ? y : Math.Max(OriginY + Height - 1, y);
                Resize(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
            }
            int at = (y - OriginY) * Width + (x - OriginX);
            if (Cells[at] == tile) return false;
            Cells[at] = tile;
            return true;
        }

        /// <summary>Puts one tile into every cell of a rectangle of the map (0 empties it). Returns how many cells changed.</summary>
        public int Fill(int x, int y, int width, int height, int tile)
        {
            if (width <= 0 || height <= 0) return 0;
            // One growth for the whole rectangle, not one for each cell outside.
            if (tile > 0 && (x < OriginX || y < OriginY || x + width > OriginX + Width || y + height > OriginY + Height))
            {
                int x0 = Width == 0 ? x : Math.Min(OriginX, x), y0 = Height == 0 ? y : Math.Min(OriginY, y);
                int x1 = Width == 0 ? x + width : Math.Max(OriginX + Width, x + width), y1 = Height == 0 ? y + height : Math.Max(OriginY + Height, y + height);
                Resize(x0, y0, x1 - x0, y1 - y0);
            }
            int changed = 0;
            for (int cy = y; cy < y + height; cy++)
                for (int cx = x; cx < x + width; cx++)
                    if (Set(cx, cy, tile)) changed++;
            return changed;
        }

        /// <summary>
        /// Gives the layer another rectangle of cells: what lies in both the old and the new one stays in its
        /// cell, what is outside the new one is lost, new cells are empty.
        /// </summary>
        public void Resize(int originX, int originY, int width, int height)
        {
            width = Math.Max(width, 0); height = Math.Max(height, 0);
            var cells = new int[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    cells[y * width + x] = Get(originX + x, originY + y);
            OriginX = originX; OriginY = originY; Width = width; Height = height; Cells = cells;
        }

        /// <summary>The smallest rectangle that holds every tile of the layer (false: the layer is empty).</summary>
        public bool Used(out int x, out int y, out int width, out int height)
        {
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
            for (int cy = 0; cy < Height; cy++)
                for (int cx = 0; cx < Width; cx++)
                    if (Cells[cy * Width + cx] != 0)
                    {
                        x0 = Math.Min(x0, cx); x1 = Math.Max(x1, cx);
                        y0 = Math.Min(y0, cy); y1 = Math.Max(y1, cy);
                    }
            if (x1 < x0) { x = y = width = height = 0; return false; }
            x = OriginX + x0; y = OriginY + y0; width = x1 - x0 + 1; height = y1 - y0 + 1;
            return true;
        }
    }

    /// <summary>
    /// A tile map file (<c>.tilemap</c>): layers of tile numbers over one sprite sheet. The file writes a layer's
    /// rows as the map looks, the top row first; in memory a cell is (x, y) with y up.
    /// </summary>
    public sealed class TilemapData
    {
        public const string Extension = ".tilemap";

        /// <summary>The map's own key ("Tilemaps/level1.tilemap").</summary>
        public string Key = "";
        /// <summary>The key of the sprite sheet whose frames are the tiles.</summary>
        public string Tiles = "";
        /// <summary>A cell's size in world units, at scale 1.</summary>
        public Vector2 Cell = Vector2.One;
        public List<TileLayer> Layers = new();
        /// <summary>Root members this class does not know, kept when the file is written.</summary>
        public List<KeyValuePair<string, string>>? Extra;

        private static readonly JsonDocumentOptions Lenient = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

        public static bool IsMapKey(string? key) => key != null && key.EndsWith(Extension, StringComparison.OrdinalIgnoreCase);

        /// <summary>The layer of that name, or null.</summary>
        public TileLayer? Layer(string? name)
        {
            foreach (TileLayer layer in Layers)
                if (layer.Name == name) return layer;
            return null;
        }

        /// <summary>Reads a tile map file's text. Null, with the reason in <paramref name="error"/>, when it is not one.</summary>
        public static TilemapData? Parse(string key, string json, out string? error)
        {
            error = null;
            json = AssetRefs.Resolve(json);   // a sheet that moved is asked for where it is now
            try
            {
                using var doc = JsonDocument.Parse(json, Lenient);
                JsonElement root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("tilemap", out var version) || version.ValueKind != JsonValueKind.Number)
                {
                    error = "not a tile map (no \"tilemap\" member)";
                    return null;
                }
                var map = new TilemapData { Key = key };
                foreach (JsonProperty m in root.EnumerateObject())
                {
                    switch (m.Name)
                    {
                        case "tilemap": break;
                        case AssetRefs.Member: break;   // the id table is made again when the file is written
                        case "tiles": map.Tiles = m.Value.GetString() ?? ""; break;
                        case "cell":
                            if (m.Value.ValueKind == JsonValueKind.Array && m.Value.GetArrayLength() == 2 && m.Value[0].GetSingle() > 0f && m.Value[1].GetSingle() > 0f)
                                map.Cell = new Vector2(m.Value[0].GetSingle(), m.Value[1].GetSingle());
                            break;
                        case "layers":
                            if (m.Value.ValueKind == JsonValueKind.Array)
                                foreach (JsonElement layer in m.Value.EnumerateArray())
                                    if (layer.ValueKind == JsonValueKind.Object) map.Layers.Add(ReadLayer(layer));
                            break;
                        default: (map.Extra ??= new()).Add(new(m.Name, m.Value.GetRawText())); break;
                    }
                }
                return map;
            }
            catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException)
            {
                error = "not valid: " + e.Message;
                return null;
            }
        }

        private static TileLayer ReadLayer(JsonElement el)
        {
            var layer = new TileLayer();
            JsonElement rows = default;
            foreach (JsonProperty m in el.EnumerateObject())
            {
                switch (m.Name)
                {
                    case "name":  layer.Name = m.Value.GetString() ?? ""; break;
                    case "order": layer.Order = m.Value.GetInt32(); break;
                    case "solid": layer.Solid = m.Value.ValueKind == JsonValueKind.True; break;
                    case "origin":
                        if (m.Value.ValueKind == JsonValueKind.Array && m.Value.GetArrayLength() == 2) { layer.OriginX = m.Value[0].GetInt32(); layer.OriginY = m.Value[1].GetInt32(); }
                        break;
                    case "rows": rows = m.Value; break;
                    default: (layer.Extra ??= new()).Add(new(m.Name, m.Value.GetRawText())); break;
                }
            }
            if (rows.ValueKind != JsonValueKind.Array) return layer;

            // The first row written is the top one. A row shorter than the longest ends in empty cells.
            int height = rows.GetArrayLength(), width = 0;
            foreach (JsonElement row in rows.EnumerateArray())
                if (row.ValueKind == JsonValueKind.Array) width = Math.Max(width, row.GetArrayLength());
            layer.Width = width; layer.Height = height; layer.Cells = new int[width * height];
            int r = 0;
            foreach (JsonElement row in rows.EnumerateArray())
            {
                int y = height - 1 - r++, x = 0;
                if (row.ValueKind != JsonValueKind.Array) continue;
                foreach (JsonElement cell in row.EnumerateArray())
                    layer.Cells[y * width + x++] = Math.Max(cell.GetInt32(), 0);
            }
            return layer;
        }

        /// <summary>
        /// The map as its file's text: a layer's rows one to a line, the top row first, the numbers in columns —
        /// so the text looks like the map. Members it was read with and does not know follow the known ones.
        /// The id table is not written here (<see cref="AssetRefs.Stamp"/> adds it).
        /// </summary>
        public string ToJson()
        {
            var sb = new StringBuilder();
            sb.Append("{\n  \"tilemap\": 1");
            sb.Append(",\n  \"tiles\": ").Append(Text(Tiles));
            sb.Append(",\n  \"cell\": [").Append(Num(Cell.X)).Append(", ").Append(Num(Cell.Y)).Append(']');
            sb.Append(",\n  \"layers\": [");
            for (int i = 0; i < Layers.Count; i++)
            {
                TileLayer layer = Layers[i];
                sb.Append(i == 0 ? "\n    { " : ",\n    { ").Append("\"name\": ").Append(Text(layer.Name))
                  .Append(", \"order\": ").Append(layer.Order)
                  .Append(", \"origin\": [").Append(layer.OriginX).Append(", ").Append(layer.OriginY).Append(']')
                  .Append(", \"solid\": ").Append(layer.Solid ? "true" : "false");
                if (layer.Extra != null)
                    foreach (var member in layer.Extra) sb.Append(", ").Append(Text(member.Key)).Append(": ").Append(member.Value);
                sb.Append(",\n      \"rows\": [");
                int widest = 1;
                foreach (int cell in layer.Cells) widest = Math.Max(widest, Digits(cell));
                for (int y = layer.Height - 1; y >= 0; y--)
                {
                    sb.Append(y == layer.Height - 1 ? "\n        [" : ",\n        [");
                    for (int x = 0; x < layer.Width; x++)
                    {
                        int cell = layer.Cells[y * layer.Width + x];
                        if (x > 0) sb.Append(", ");
                        sb.Append(' ', widest - Digits(cell)).Append(cell);
                    }
                    sb.Append(']');
                }
                sb.Append(layer.Height > 0 ? "\n      ] }" : "] }");
            }
            sb.Append(Layers.Count > 0 ? "\n  ]" : "]");
            if (Extra != null)
                foreach (var member in Extra) sb.Append(",\n  ").Append(Text(member.Key)).Append(": ").Append(member.Value);
            return sb.Append("\n}\n").ToString();
        }

        private static int Digits(int n) => n < 10 ? 1 : n < 100 ? 2 : n < 1000 ? 3 : n.ToString(CultureInfo.InvariantCulture).Length;
        private static string Text(string s) => "\"" + JsonEncodedText.Encode(s, System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping).ToString() + "\"";
        private static string Num(float v) => v.ToString("G", CultureInfo.InvariantCulture);
    }
}
