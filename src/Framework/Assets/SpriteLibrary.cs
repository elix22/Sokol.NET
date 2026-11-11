using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json;
using GameEditor.Framework.Core;
using GameEditor.Framework.ECS.Components;
using Sokol;

namespace GameEditor.Framework.Assets
{
    /// <summary>What a sprite draws: a part of a texture, and how large it is in the world.</summary>
    public struct SpriteFrame
    {
        public SG.sg_view View;
        /// <summary>Sample the texture without smoothing (pixel art).</summary>
        public bool Nearest;
        /// <summary>The part of the texture, in uv: X, Y = its top-left corner, Z, W = its bottom-right corner.</summary>
        public Vector4 Uv;
        /// <summary>Size in world units at scale 1.</summary>
        public Vector2 Size;
        /// <summary>The point of the frame that sits on the entity's position: (0, 0) bottom-left, (1, 1) top-right.</summary>
        public Vector2 Pivot;
    }

    /// <summary>
    /// Turns what a <see cref="SpriteRenderer"/> names — an image's key, or a frame of a sprite sheet
    /// ("Sprites/hero.sprites#walk_3") — into the frame to draw, and keeps the sheets that were read. Answers are
    /// kept, so asking every frame for every sprite costs a lookup. Main thread only.
    /// </summary>
    public static class SpriteLibrary
    {
        public enum Answer { Ready, Loading, Missing }

        /// <summary>
        /// Reads a sheet file's text by key; null when there is none. When set, it alone is asked (a test, a tool);
        /// otherwise the open project's Assets folder, and without a project the app's files as they arrive.
        /// </summary>
        public static Func<string, string?>? Reader { get; set; }

        private static readonly Dictionary<string, SpriteFrame> _frames = new(StringComparer.Ordinal);
        private static readonly HashSet<string> _missing = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, SpriteSheet?> _sheets = new(StringComparer.Ordinal);   // null: cannot be read
        private static readonly HashSet<string> _pendingSheets = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, SpriteFrame[]> _tiles = new(StringComparer.Ordinal);
        private static readonly HashSet<string> _missingTiles = new(StringComparer.Ordinal);

        /// <summary>The frame <paramref name="texture"/> names. Loading: a file is on its way, ask again.</summary>
        public static Answer Find(string? texture, out SpriteFrame frame)
        {
            frame = default;
            if (string.IsNullOrEmpty(texture)) return Answer.Missing;
            if (_frames.TryGetValue(texture, out frame)) return Answer.Ready;
            if (_missing.Contains(texture)) return Answer.Missing;

            int hash = texture.IndexOf('#');
            string file = hash < 0 ? texture : texture.Substring(0, hash);
            Answer answer = SpriteSheet.IsSheetKey(file)
                ? FindInSheet(file, hash < 0 ? null : texture.Substring(hash + 1), out frame)
                : hash < 0 ? FindImage(texture, out frame) : Answer.Missing;

            if (answer == Answer.Ready) _frames[texture] = frame;
            else if (answer == Answer.Missing) _missing.Add(texture);
            return answer;
        }

        // A whole image: 100 pixels to a unit, smoothed, its middle on the entity.
        private static Answer FindImage(string key, out SpriteFrame frame)
        {
            frame = default;
            if (AssetLibrary.Texture(key) is { } tex && tex.Width > 0 && tex.Height > 0)
            {
                frame = new SpriteFrame
                {
                    View   = tex.View,
                    Uv     = new Vector4(0f, 0f, 1f, 1f),
                    Size   = new Vector2(tex.Width, tex.Height) / SpriteRenderer.DefaultPixelsPerUnit,
                    Pivot  = new Vector2(0.5f, 0.5f),
                };
                return Answer.Ready;
            }
            return AssetLibrary.IsTextureMissing(key) ? Answer.Missing : Answer.Loading;
        }

        // A frame of a sheet by its name; with no name, the sheet's first frame.
        private static Answer FindInSheet(string sheetKey, string? frameName, out SpriteFrame frame)
        {
            frame = default;
            Answer found = FindSheet(sheetKey, out SpriteSheet? sheet);
            if (found != Answer.Ready) return found;
            if (sheet!.Image.Length == 0) return Answer.Missing;

            if (AssetLibrary.Texture(sheet.Image) is not { } tex || tex.Width <= 0 || tex.Height <= 0)
                return AssetLibrary.IsTextureMissing(sheet.Image) ? Answer.Missing : Answer.Loading;

            SheetFrame cut;
            if (frameName == null)
            {
                IReadOnlyList<SheetFrame> all = sheet.FramesFor(tex.Width, tex.Height);
                if (all.Count == 0) return Answer.Missing;
                cut = all[0];
            }
            else if (!sheet.TryGetFrame(frameName, tex.Width, tex.Height, out cut))
            {
                Logger.Warning($"[Sprites] '{sheetKey}' has no frame '{frameName}'.");
                return Answer.Missing;
            }

            frame = Cut(sheet, tex, cut);
            return Answer.Ready;
        }

        private static SpriteFrame Cut(SpriteSheet sheet, Renderer.Server.Resources.TextureResource tex, SheetFrame cut)
        {
            // A smoothed frame is sampled half a pixel inside its rectangle, or its edge would take colour from
            // the frame beside it. An unsmoothed one shows its pixels whole.
            float inset = sheet.Nearest ? 0f : 0.5f;
            return new SpriteFrame
            {
                View    = tex.View,
                Nearest = sheet.Nearest,
                Uv      = new Vector4((cut.X + inset) / tex.Width, (cut.Y + inset) / tex.Height,
                                      (cut.X + cut.W - inset) / tex.Width, (cut.Y + cut.H - inset) / tex.Height),
                Size    = new Vector2(cut.W, cut.H) / sheet.PixelsPerUnit,
                Pivot   = cut.Pivot,
            };
        }

        /// <summary>
        /// Every frame of a sheet in the sheet's order, ready to draw: a tile map's tile n is <c>tiles[n - 1]</c>.
        /// Loading: the sheet or its image is on its way, ask again.
        /// </summary>
        public static Answer FindTiles(string? sheetKey, out SpriteFrame[] tiles)
        {
            tiles = Array.Empty<SpriteFrame>();
            if (string.IsNullOrEmpty(sheetKey)) return Answer.Missing;
            if (_tiles.TryGetValue(sheetKey, out SpriteFrame[]? known)) { tiles = known; return Answer.Ready; }
            if (_missingTiles.Contains(sheetKey)) return Answer.Missing;

            Answer found = FindSheet(sheetKey, out SpriteSheet? sheet);
            if (found == Answer.Ready)
            {
                if (sheet!.Image.Length == 0) found = Answer.Missing;
                else if (AssetLibrary.Texture(sheet.Image) is not { } tex || tex.Width <= 0 || tex.Height <= 0)
                    found = AssetLibrary.IsTextureMissing(sheet.Image) ? Answer.Missing : Answer.Loading;
                else
                {
                    IReadOnlyList<SheetFrame> frames = sheet.FramesFor(tex.Width, tex.Height);
                    tiles = new SpriteFrame[frames.Count];
                    for (int i = 0; i < tiles.Length; i++) tiles[i] = Cut(sheet, tex, frames[i]);
                    _tiles[sheetKey] = tiles;
                }
            }
            if (found == Answer.Missing) _missingTiles.Add(sheetKey);
            return found;
        }

        // ── Sheets ──────────────────────────────────────────────────────────────────────

        /// <summary>The sheet under <paramref name="key"/>. Loading: its file is on its way (the web, phones), ask again.</summary>
        public static Answer FindSheet(string key, out SpriteSheet? sheet)
        {
            if (_sheets.TryGetValue(key, out sheet)) return sheet != null ? Answer.Ready : Answer.Missing;
            if (_pendingSheets.Contains(key)) return Answer.Loading;

            if (Reader != null) return Arrived(key, Reader(key), out sheet);
            if (ConfigManager.HasProject) return Arrived(key, ReadProjectFile(key), out sheet);

            // A built game: its files are read as they arrive.
            AskForSheet(key);
            return _sheets.TryGetValue(key, out sheet) ? (sheet != null ? Answer.Ready : Answer.Missing) : Answer.Loading;
        }

        // Apart from FindSheet, which is asked every frame: a method that hands its argument to a callback makes
        // an object for it each time it is entered.
        private static void AskForSheet(string key)
        {
            _pendingSheets.Add(key);
            SFilesystem.LoadFileAsync(key, (path, buffer, status) =>
            {
                _pendingSheets.Remove(key);
                Arrived(key, status == SFileLoadStatus.Success && buffer != null ? System.Text.Encoding.UTF8.GetString(buffer) : null, out _);
            });
        }

        /// <summary>The sheet under <paramref name="key"/> when it is read, else null (missing, or on its way).</summary>
        public static SpriteSheet? Sheet(string key) => FindSheet(key, out SpriteSheet? sheet) == Answer.Ready ? sheet : null;

        /// <summary>
        /// Hands over a sheet file's text (read by the caller, or just written), replacing what was known for the
        /// key. Returns the sheet, or null when the text is not one (logged).
        /// </summary>
        public static SpriteSheet? PutSheet(string key, string json)
        {
            Arrived(key, json, out SpriteSheet? sheet);
            return sheet;
        }

        private static Answer Arrived(string key, string? json, out SpriteSheet? sheet)
        {
            sheet = null;
            if (json == null) Logger.Warning($"[Sprites] '{key}' could not be read.");
            else
            {
                sheet = SpriteSheet.Parse(key, json, out string? error);
                if (sheet == null) Logger.Warning($"[Sprites] '{key}': {error}.");
            }
            _sheets[key] = sheet;
            _frames.Clear();      // answers made from the sheet this one replaces
            _missing.Clear();
            _tiles.Clear();
            _missingTiles.Clear();
            return sheet != null ? Answer.Ready : Answer.Missing;
        }

        private static string? ReadProjectFile(string key)
        {
            string path = System.IO.Path.Combine(ConfigManager.ProjectFolder!, "Assets", key);
            try { return System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path) : null; }
            catch (System.IO.IOException) { return null; }
        }

        // ── For a built game: the sheets a scene names, read before it ─────────────────────

        /// <summary>
        /// The sheet keys a scene's or a prefab's text names: every text value that is a <c>.sprites</c> file,
        /// alone or before a '#'.
        /// </summary>
        public static List<string> SheetKeysIn(string json)
        {
            var keys = new List<string>();
            if (json.IndexOf(SpriteSheet.Extension, StringComparison.OrdinalIgnoreCase) < 0) return keys;
            json = AssetRefs.Resolve(json);   // a sheet that moved is asked for where it is now
            try
            {
                using var doc = JsonDocument.Parse(json);
                Collect(doc.RootElement, keys);
            }
            catch (JsonException) { }   // the reader of the scene reports a text that is not JSON
            return keys;

            static void Collect(JsonElement e, List<string> keys)
            {
                switch (e.ValueKind)
                {
                    case JsonValueKind.Object:
                        foreach (var p in e.EnumerateObject()) Collect(p.Value, keys);
                        break;
                    case JsonValueKind.Array:
                        foreach (var item in e.EnumerateArray()) Collect(item, keys);
                        break;
                    case JsonValueKind.String:
                        string? text = e.GetString();
                        if (text == null) break;
                        int hash = text.IndexOf('#');
                        if (hash >= 0) text = text.Substring(0, hash);
                        if (SpriteSheet.IsSheetKey(text) && !keys.Contains(text)) keys.Add(text);
                        break;
                }
            }
        }

        /// <summary>
        /// Reads the sheet files the texts name and asks for their images, then calls <paramref name="done"/> —
        /// at once when they name none, or all are known. So a scene read after it has its sheets from its first frame.
        /// </summary>
        internal static void LoadNamedIn(IEnumerable<string> texts, Action done)
        {
            int waiting = 0;
            bool listed = false;
            foreach (string text in texts)
                foreach (string key in SheetKeysIn(text))
                {
                    if (_sheets.ContainsKey(key) || _pendingSheets.Contains(key)) continue;
                    if (Reader != null || ConfigManager.HasProject) { AskImage(Sheet(key)); continue; }
                    waiting++;
                    _pendingSheets.Add(key);
                    string asked = key;
                    SFilesystem.LoadFileAsync(asked, (path, buffer, status) =>
                    {
                        _pendingSheets.Remove(asked);
                        Arrived(asked, status == SFileLoadStatus.Success && buffer != null ? System.Text.Encoding.UTF8.GetString(buffer) : null, out SpriteSheet? sheet);
                        AskImage(sheet);
                        waiting--;
                        if (listed && waiting == 0) done();
                    });
                }
            listed = true;
            if (waiting == 0) done();

            static void AskImage(SpriteSheet? sheet)
            {
                if (sheet != null && sheet.Image.Length > 0) AssetLibrary.Texture(sheet.Image);
            }
        }

        // ── Forgetting ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Forgets every answer and what was read under <paramref name="key"/> — a sheet, or an image: after its
        /// file changed, moved or came back.
        /// </summary>
        public static void Forget(string key)
        {
            _frames.Clear();
            _missing.Clear();
            _tiles.Clear();
            _missingTiles.Clear();
            if (SpriteSheet.IsSheetKey(key)) _sheets.Remove(key);
            else AssetLibrary.ForgetTexture(key);
        }

        /// <summary>Forgets every answer and every sheet (the images stay loaded): when another project is opened.</summary>
        public static void Clear()
        {
            _frames.Clear();
            _missing.Clear();
            _sheets.Clear();
            _tiles.Clear();
            _missingTiles.Clear();
        }
    }
}
