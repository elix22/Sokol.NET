using System;
using System.Collections.Generic;
using Frent;
using GameEditor.Framework.Assets;
using GameEditor.Framework.Core;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using Sokol;
using Sokol.GUI;
using static Sokol.NanoVG;
using GVec2 = Sokol.GUI.Vector2;
using Vector4 = System.Numerics.Vector4;

namespace GameEditor.Framework.UI
{
    /// <summary>
    /// A game's UI: the entities of the scene that have a <see cref="UIElement"/> — texts, pictures, buttons and
    /// panels — as Sokol.GUI widgets under one <see cref="Root"/>. A host puts the root where the game's picture
    /// is (a built game: the whole window; an editor: over its Game view) and calls <see cref="Sync"/> once a
    /// frame with the picture's size: every widget is then made, placed and changed as its entity's components
    /// say. Nothing of the look comes from a theme: a game's UI is the same in an editor and in the game.
    /// </summary>
    public static class GameUI
    {
        /// <summary>The height of the screen the sizes of a UI are written for.</summary>
        public const float DesignHeight = 720f;

        /// <summary>The engine's own font's name in the font registry (a game's text with no font of its own).</summary>
        public const string DefaultFont = "game-ui";

        private sealed class Item
        {
            public Widget Widget = null!;
            public int Kind;            // 1 text, 2 picture, 3 button, 4 panel, 0 none of them (a place only)
            public int Order, Made, Seen;
            public bool HasAbove;       // its entity is a child of another element's
            public Entity Above;
            public readonly List<Item> Kids = new();
        }

        private static readonly Dictionary<Entity, Item> _items = new();
        private static readonly Dictionary<Entity, Rect> _rects = new();
        private static readonly List<Entity> _gone = new();
        private static readonly Dictionary<Entity, Entity> _above = new();     // an element's entity → the element it lies in
        private static readonly List<Item> _sorted = new(), _roots = new();
        private static readonly List<Entity> _clicked = new(), _clickedNow = new();
        private static int _frame, _made;
        private static bool _fontAsked;

        /// <summary>Every widget of the game's UI is a child of this one. It is as large as the game's picture.</summary>
        public static Widget Root { get; } = new GameUIRoot();

        private static Widget? _host;

        /// <summary>
        /// What the UI is shown on: a built game's screen (an editor's is made by <see cref="HostInit"/>), or any
        /// widget. The root is its child only while the game has a UI: a screen with a child walks it every frame
        /// in a way that allocates, and a game with no UI should pay nothing for it.
        /// </summary>
        public static void ShowOn(Widget? host)
        {
            if (_host != null && Root.Parent == _host) Root.RemoveFromParent();
            _host = host;
        }

        /// <summary>What a design unit is in the picture's own units: the picture's height over 720.</summary>
        public static float Scale { get; private set; } = 1f;

        /// <summary>How many elements the UI has now.</summary>
        public static int Count => _items.Count;

        /// <summary>
        /// Where the UI lies in the picture, in the picture's own units: all of it, less what a phone's notch and
        /// home bar take from its edges. An element that is no child of another is placed in this rectangle.
        /// </summary>
        public static (float X, float Y, float Width, float Height) Area { get; private set; }

        // ── For a host that is not a built game (an editor) ─────────────────────────────
        // An editor has a Sokol.GUI of its own, apart from the engine's: it cannot hold the engine's widgets. It
        // asks the engine to draw the game's UI itself, as a built game does, into the part of the window where
        // the game's picture is — and speaks to it in plain numbers.

        private static IntPtr _hostVg;
        private static Screen? _hostScreen;
        private static readonly List<Entity> _outlined = new();

        /// <summary>Makes the engine's own screen for the game's UI. Once, when the graphics are set up.</summary>
        public static void HostInit()
        {
            if (_hostScreen != null) return;
            _hostVg = nvgCreateSokol(NVG_ANTIALIAS | NVG_STENCIL_STROKES);
            _hostScreen = Screen.Initialize(_hostVg);
            ShowOn(_hostScreen);
        }

        /// <summary>
        /// Once a frame: the widgets brought in line with the scene and laid out for a picture of this size (in
        /// the host's own units; <paramref name="dpi"/>: pixels to one of them). <paramref name="playing"/>
        /// false: a button pressed now tells no script later.
        /// </summary>
        public static void HostFrame(float width, float height, float dpi, bool playing)
        {
            if (_hostScreen == null) return;
            if (!playing) _clicked.Clear();
            // The buttons that hold are held at the start of each game frame (ApplyHolds), not here at the end of the editor's.
            Sync(ECSWorld.Instance, width, height, Thickness.Zero, holds: false);
            _hostScreen.Update(width, height, dpi);
        }

        /// <summary>Draws the game's UI. Inside a pass, with the viewport on the game's picture.</summary>
        public static void HostDraw(float width, float height, float dpi) => _hostScreen?.Draw(width, height, dpi);

        /// <summary>An event for the game's UI, its pointer positions counted from the picture's top-left corner (in pixels).</summary>
        public static unsafe void HostEvent(SApp.sapp_event* e) => _hostScreen?.DispatchEvent(e);

        /// <summary>The elements whose rectangles the host wants outlined (its selection). Asked for each frame.</summary>
        public static void HostOutline(IEnumerable<Entity>? entities)
        {
            _outlined.Clear();
            if (entities != null) _outlined.AddRange(entities);
        }

        /// <summary>An element's rectangle after the last frame, in plain numbers. False for an entity that is no element.</summary>
        public static bool TryGetRect(Entity entity, out float x, out float y, out float width, out float height)
        {
            bool found = _rects.TryGetValue(entity, out Rect r);
            (x, y, width, height) = (r.X, r.Y, r.Width, r.Height);
            return found;
        }

        /// <summary>
        /// Tells of every element, in the order they are drawn: its entity, its kind ("UIText", "UIImage",
        /// "UIButton", "UIPanel", or "UI" for a place only), its text, its rectangle and whether it is seen.
        /// </summary>
        public static void Describe(Action<Entity, string, string, float, float, float, float, bool> each)
        {
            foreach (Widget w in Root.Children)
                foreach (var pair in _items)
                {
                    if (!ReferenceEquals(pair.Value.Widget, w)) continue;
                    string kind = pair.Value.Kind switch { 1 => "UIText", 2 => "UIImage", 3 => "UIButton", 4 => "UIPanel", _ => "UI" };
                    string text = w is Label label ? label.Text : "";
                    each(pair.Key, kind, text, w.Bounds.X, w.Bounds.Y, w.Bounds.Width, w.Bounds.Height, w.Visible);
                    break;
                }
        }

        // ── Where an element lies ───────────────────────────────────────────────────────

        /// <summary>
        /// An element's rectangle inside <paramref name="parent"/>: its anchor's point, its offset towards the
        /// inside and its size, the last two in design units times <paramref name="scale"/>.
        /// </summary>
        public static Rect Place(in UIElement element, Rect parent, float scale)
        {
            float w = element.Size.X * scale, h = element.Size.Y * scale;
            float ox = element.Offset.X * scale, oy = element.Offset.Y * scale;
            int column = (int)element.Anchor % 3, row = (int)element.Anchor / 3;
            float x = column switch
            {
                0 => parent.X + ox,
                1 => parent.X + (parent.Width - w) * 0.5f + ox,
                _ => parent.X + parent.Width - w - ox,
            };
            float y = row switch
            {
                0 => parent.Y + oy,
                1 => parent.Y + (parent.Height - h) * 0.5f + oy,
                _ => parent.Y + parent.Height - h - oy,
            };
            return new Rect(x, y, w, h);
        }

        /// <summary>The rectangle an element was given by the last <see cref="Sync"/>, in the root's units. False for an entity that is no element.</summary>
        public static bool TryGetRect(Entity entity, out Rect rect) => _rects.TryGetValue(entity, out rect);

        /// <summary>The widget of an element, when it has one (a test, an editor's outline).</summary>
        public static Widget? WidgetOf(Entity entity) => _items.TryGetValue(entity, out Item? item) ? item.Widget : null;

        // ── Once a frame ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Brings the widgets in line with the scene. <paramref name="width"/> and <paramref name="height"/>: the
        /// game's picture in the root's units; <paramref name="safe"/>: what a phone's notch and home bar take
        /// from its edges. <paramref name="holds"/>: the buttons that hold are held for this frame (<see cref="ApplyHolds"/>);
        /// a game calls Sync after the frame's input and before its physics.
        /// </summary>
        public static void Sync(ECSWorld world, float width, float height, Thickness safe, bool holds = true)
        {
            if (width <= 0f || height <= 0f) return;
            _frame++;
            Scale = height / DesignHeight;
            var screen = new Rect(safe.Left, safe.Top, MathF.Max(0f, width - safe.Horizontal), MathF.Max(0f, height - safe.Vertical));
            Area = (screen.X, screen.Y, screen.Width, screen.Height);
            _rects.Clear();
            _above.Clear();
            _holders.Clear();
            _holdWidth = width;
            bool order = false;

            foreach (Entity e in world.Entities)
            {
                if (!world.TryGetComponent<UIElement>(e, out var element)) continue;
                int kind = world.HasComponent<UIText>(e) ? 1 : world.HasComponent<UIPicture>(e) ? 2 : world.HasComponent<UIButton>(e) ? 3
                         : world.HasComponent<UIPanel>(e) ? 4 : world.HasComponent<UIMiniMap>(e) ? 5 : 0;
                if (!_items.TryGetValue(e, out Item? item) || item.Kind != kind)
                {
                    if (item != null) item.Widget.RemoveFromParent();
                    item = new Item { Kind = kind, Widget = Make(kind, e), Made = _made++ };
                    _items[e] = item;
                    order = true;
                }
                item.Seen = _frame;
                Rect rect = RectOf(world, e, in element, screen, 0);
                bool hasAbove = _above.TryGetValue(e, out Entity above);
                if (item.Order != element.Order || item.HasAbove != hasAbove || (hasAbove && item.Above != above))
                {
                    item.Order = element.Order; item.HasAbove = hasAbove; item.Above = above;
                    order = true;
                }

                Widget w = item.Widget;
                w.Position = new GVec2(rect.X, rect.Y);
                w.FixedSize = new GVec2(rect.Width, rect.Height);
                w.Bounds = rect;
                w.Visible = element.Visible && Shown(world, e);
                Apply(world, e, item);
                if (kind == 3 && w.Visible && world.TryGetComponent<UIButton>(e, out var held) && !string.IsNullOrEmpty(held.Holds))
                    _holders.Add((rect, held.Holds));
            }
            if (holds) ApplyHolds();

            _gone.Clear();
            foreach (var pair in _items)
                if (pair.Value.Seen != _frame) _gone.Add(pair.Key);
            foreach (Entity e in _gone)
            {
                _items[e].Widget.RemoveFromParent();
                _items.Remove(e);
            }

            // On the screen while there is something to show (see ShowOn).
            bool wanted = _items.Count > 0 || _outlined.Count > 0;
            if (_host != null && wanted != (Root.Parent == _host))
            {
                if (wanted) _host.AddChild(Root);
                else Root.RemoveFromParent();
            }

            if (order || _gone.Count > 0)
            {
                // An element is drawn over the one it lies in, with that one: a text of a panel is under whatever
                // is over the panel. Among the elements of one parent — or of the screen — by order, then as
                // they were made.
                _roots.Clear();
                foreach (Item item in _items.Values) item.Kids.Clear();
                foreach (Item item in _items.Values)
                {
                    if (item.HasAbove && _items.TryGetValue(item.Above, out Item? parent)) parent.Kids.Add(item);
                    else _roots.Add(item);
                }
                _sorted.Clear();
                InOrder(_roots, 0);
                Root.ClearChildren();
                foreach (Item item in _sorted) Root.AddChild(item.Widget);
            }
        }

        private static readonly Comparison<Item> ByOrder = static (a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : a.Made.CompareTo(b.Made);

        private static void InOrder(List<Item> items, int guard)
        {
            items.Sort(ByOrder);
            foreach (Item item in items)
            {
                _sorted.Add(item);
                if (guard < 32) InOrder(item.Kids, guard + 1);
            }
        }

        // An element's rectangle: in its parent's when its entity is a child of another element, else in the screen's.
        private static Rect RectOf(ECSWorld world, Entity e, in UIElement element, Rect screen, int guard)
        {
            if (_rects.TryGetValue(e, out Rect known)) return known;
            Rect parent = screen;
            if (guard < 32 && world.TryGetComponent<Transform>(e, out var tr) && tr.Parent is { } p && world.TryGetComponent<UIElement>(p, out var parentElement))
            {
                parent = RectOf(world, p, in parentElement, screen, guard + 1);
                _above[e] = p;
            }
            Rect rect = Place(in element, parent, Scale);
            _rects[e] = rect;
            return rect;
        }

        // An element is seen when every element above it is.
        private static bool Shown(ECSWorld world, Entity e)
        {
            for (int guard = 0; guard < 32; guard++)
            {
                if (!world.TryGetComponent<Transform>(e, out var tr) || tr.Parent is not { } p) return true;
                if (!world.TryGetComponent<UIElement>(p, out var parent)) return true;
                if (!parent.Visible) return false;
                e = p;
            }
            return true;
        }

        // ── Buttons that hold ───────────────────────────────────────────────────────────

        private static readonly Dictionary<string, float> _holdAxes = new(StringComparer.Ordinal);
        private static readonly HashSet<string> _holdButtons = new(StringComparer.Ordinal);
        private static readonly List<(Rect Rect, string Holds)> _holders = new();   // the buttons that hold, where the last Sync placed them
        private static float _holdWidth;

        /// <summary>
        /// The axes and buttons of the buttons that a finger or the mouse is on now, as the input of this frame
        /// (InputManager's overrides, which last one frame): call after the frame's InputManager.BeginFrame and before
        /// its physics. <see cref="Sync"/> calls it; an editor, whose Sync comes at the end of its frame, calls it at the
        /// start of each game frame it runs.
        /// </summary>
        public static void ApplyHolds()
        {
            if (_holders.Count == 0) return;
            _holdAxes.Clear();
            _holdButtons.Clear();
            foreach (var (rect, spec) in _holders)
                if (Held(rect, _holdWidth)) Hold(spec);
            foreach (var pair in _holdAxes) Input.InputManager.SetVirtualAxis(pair.Key, Math.Clamp(pair.Value, -1f, 1f));
            foreach (string name in _holdButtons) Input.InputManager.SetVirtualButton(name, true);
        }

        // A finger on the rectangle (logical units), or the mouse's left button held on it.
        private static bool Held(Rect rect, float width)
        {
            var size = Input.InputManager.ScreenSize;
            float k = size.X > 0f ? width / size.X : 1f;
            for (int i = 0; i < Input.InputManager.TouchCount; i++)
            {
                var t = Input.InputManager.GetTouch(i);
                if (t.Phase == Input.TouchPhase.Ended || t.Phase == Input.TouchPhase.Cancelled) continue;
                if (Inside(rect, t.Position.X * k, t.Position.Y * k)) return true;
            }
            var m = Input.InputManager.MousePosition;
            return Input.InputManager.GetMouseButton(0) && Inside(rect, m.X * k, m.Y * k);

            static bool Inside(Rect r, float x, float y) => x >= r.X && y >= r.Y && x <= r.X + r.Width && y <= r.Y + r.Height;
        }

        // "Horizontal:-1" adds to an axis (two buttons of one axis cancel); "Fire1" holds a button.
        private static void Hold(string spec)
        {
            int colon = spec.IndexOf(':');
            if (colon > 0 && float.TryParse(spec.AsSpan(colon + 1), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v))
            {
                string axis = spec.Substring(0, colon);
                _holdAxes[axis] = (_holdAxes.TryGetValue(axis, out float sum) ? sum : 0f) + v;
            }
            else _holdButtons.Add(spec);
        }

        private static Widget Make(int kind, Entity e)
        {
            switch (kind)
            {
                case 1: return new Label();
                case 2: return new PictureWidget();
                case 3:
                    var button = new ButtonWidget();
                    button.Clicked += () => _clicked.Add(e);
                    return button;
                case 4: return new PanelWidget();
                case 5: return new MiniMapWidget();
                default: return new PlaceWidget();
            }
        }

        private static UIColor Colour(Vector4 c) => new(c.X, c.Y, c.Z, c.W);

        private static void Apply(ECSWorld world, Entity e, Item item)
        {
            switch (item.Kind)
            {
                case 1 when item.Widget is Label label && world.TryGetComponent<UIText>(e, out var text):
                    label.Text = text.Text ?? "";
                    label.FontName = FontOf(text.Font);
                    label.FontSize = MathF.Max(1f, text.FontSize * Scale);
                    label.ForeColor = Colour(text.Color);
                    label.Align = (TextAlign)(int)text.Align;
                    label.Wrap = text.Wrap ? TextWrap.Wrap : TextWrap.None;
                    break;
                case 2 when item.Widget is PictureWidget picture && world.TryGetComponent<UIPicture>(e, out var image):
                    picture.Texture = image.Texture ?? "";
                    picture.Tint = image.Color;
                    break;
                case 3 when item.Widget is ButtonWidget button && world.TryGetComponent<UIButton>(e, out var b):
                    button.Text = b.Text ?? "";
                    button.FontName = FontOf(b.Font);
                    button.FontSize = MathF.Max(1f, b.FontSize * Scale);
                    button.ForeColor = Colour(b.TextColor);
                    button.Face = Colour(b.Color);
                    button.Radius = b.Radius * Scale;
                    break;
                case 4 when item.Widget is PanelWidget panel && world.TryGetComponent<UIPanel>(e, out var p):
                    panel.Face = Colour(p.Color);
                    panel.Radius = p.Radius * Scale;
                    break;
                case 5 when item.Widget is MiniMapWidget map && world.TryGetComponent<UIMiniMap>(e, out var mm):
                    map.Road = Colour(mm.Road); map.Kart = Colour(mm.Kart); map.Player = Colour(mm.Player);
                    map.Line = MathF.Max(1f, mm.Line * Scale);
                    map.Refresh(world);
                    break;
            }
        }

        // ── Fonts ───────────────────────────────────────────────────────────────────────

        // The font registry's name for a text's font: the engine's own, or a font file of the game by its key.
        private static string FontOf(string? key)
        {
            // The screen the UI is shown on: a built game's, or the one an editor asked the engine for. None in a
            // test or a tool: nothing to register with.
            IntPtr vg = (_host as Screen)?.Renderer?.VGContext ?? IntPtr.Zero;
            if (vg == IntPtr.Zero) return string.IsNullOrEmpty(key) ? DefaultFont : key;
            if (!_fontAsked)
            {
                _fontAsked = true;
                using var stream = typeof(GameUI).Assembly.GetManifestResourceStream("GameEditor.Framework.UI.Roboto-Regular.ttf");
                if (stream == null) Logger.Warning("[UI] The engine's font is not in this build: a text with no font of its own is not drawn.");
                else
                {
                    var bytes = new byte[stream.Length];
                    stream.ReadExactly(bytes);
                    FontRegistry.Instance.RegisterFromMemory(vg, DefaultFont, bytes);
                }
            }
            if (string.IsNullOrEmpty(key)) return DefaultFont;
            if (_fonts.Contains(key)) return key;
            // A font file of the game: in an editor read at once, in a built game when it arrives; until then
            // the text is drawn with nothing (the registry measures it again when the font is there).
            if (ConfigManager.HasProject)
            {
                // In an editor the file may be put there after its name was typed: looked for again now and then.
                if (_fontsMissing.Contains(key) && _frame % 30 != 0) return key;
                string path = System.IO.Path.Combine(ConfigManager.ProjectFolder!, "Assets", key);
                if (System.IO.File.Exists(path))
                {
                    FontRegistry.Instance.RegisterFromMemory(vg, key, System.IO.File.ReadAllBytes(path));
                    _fonts.Add(key);
                    _fontsMissing.Remove(key);
                }
                else if (_fontsMissing.Add(key)) Logger.Warning($"[UI] The font '{key}' is not in the project's Assets.");
            }
            else
            {
                _fonts.Add(key);
                FontRegistry.Instance.RegisterAsync(vg, key, key);
            }
            return key;
        }

        private static readonly HashSet<string> _fonts = new(StringComparer.Ordinal), _fontsMissing = new(StringComparer.Ordinal);

        // ── Clicks ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// The buttons pressed since the last frame become this frame's: their entities' scripts get OnClick, and
        /// <see cref="WasClicked"/> answers for them until the next frame. Called by the script system at the
        /// start of a frame of play; <paramref name="tell"/> is told each clicked entity.
        /// </summary>
        public static void TakeClicks(Action<Entity>? tell)
        {
            _clickedNow.Clear();
            _clickedNow.AddRange(_clicked);
            _clicked.Clear();
            if (tell != null)
                foreach (Entity e in _clickedNow) tell(e);
        }

        /// <summary>Was this button pressed and let go since the last frame?</summary>
        public static bool WasClicked(Entity button)
        {
            foreach (Entity e in _clickedNow)
                if (e == button) return true;
            return false;
        }

        /// <summary>Presses a button as a finger would have: for a test, or a script that plays the game.</summary>
        public static void Click(Entity button)
        {
            if (_items.TryGetValue(button, out Item? item) && item.Kind == 3) _clicked.Add(button);
        }

        /// <summary>Forgets every widget and click: the scene is another one, or a test is over.</summary>
        public static void Clear()
        {
            foreach (Item item in _items.Values) item.Widget.RemoveFromParent();
            _items.Clear();
            _rects.Clear();
            _clicked.Clear();
            _clickedNow.Clear();
            _holders.Clear();
        }

        // ── The widgets ─────────────────────────────────────────────────────────────────

        // The root draws its children, then the rectangles a host asked to have outlined.
        private sealed class GameUIRoot : Widget
        {
            private static readonly UIColor Outline = UIColor.FromHex("#F9E2AF");

            // Sync places every widget itself, each frame: the screen's layout pass has nothing to do here (and
            // allocated in every frame).
            public override void PerformLayout(Sokol.GUI.Renderer renderer, bool force = false) { }

            public override void Draw(Sokol.GUI.Renderer renderer)
            {
                base.Draw(renderer);
                foreach (Entity e in _outlined)
                {
                    if (!_rects.TryGetValue(e, out Rect r)) continue;
                    renderer.SetStrokeColor(Outline);
                    renderer.SetStrokeWidth(1.5f);
                    renderer.StrokeRect(new Rect(r.X + 0.75f, r.Y + 0.75f, MathF.Max(0f, r.Width - 1.5f), MathF.Max(0f, r.Height - 1.5f)));
                }
            }
        }

        // An element that is a place only: something for its children to lie in.
        private sealed class PlaceWidget : Widget { }

        // The race's map: the track's centre line fitted into the rectangle (north, +Z, up), a dot for each racer.
        private sealed class MiniMapWidget : Widget
        {
            public UIColor Road, Kart, Player;
            public float Line;
            private TrackData? _track;
            private int _version = -1;
            private readonly List<System.Numerics.Vector2> _line = new();
            private readonly List<(System.Numerics.Vector2 at, bool player)> _dots = new();
            private float _minX, _maxZ, _scale = 1f, _ox, _oy, _w, _h;

            public void Refresh(ECSWorld world)
            {
                if (Bounds.Width != _w || Bounds.Height != _h) { _w = Bounds.Width; _h = Bounds.Height; _version = -1; }   // fitted again when it is resized
                if (Scene.Race.Track is { } raced) _track = raced;
                else if (_track == null || _version != TrackLibrary.Version)
                {
                    _track = null;
                    foreach (Entity e in world.Entities)
                        if (world.TryGetComponent<TrackComponent>(e, out var tc) && TrackLibrary.Track(tc.Key) is { } t) { _track = t; break; }
                }
                if (_track != null && _version != TrackLibrary.Version)
                {
                    _version = TrackLibrary.Version;
                    _line.Clear();
                    float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
                    for (int i = 0; i < _track.Frames.Count; i += 4)
                    {
                        var p = _track.Frames[i].Position;
                        _line.Add(new System.Numerics.Vector2(p.X, p.Z));
                        minX = MathF.Min(minX, p.X); maxX = MathF.Max(maxX, p.X); minZ = MathF.Min(minZ, p.Z); maxZ = MathF.Max(maxZ, p.Z);
                    }
                    float pad = 6f;
                    float w = MathF.Max(1f, maxX - minX), h = MathF.Max(1f, maxZ - minZ);
                    _scale = MathF.Min((Bounds.Width - 2 * pad) / w, (Bounds.Height - 2 * pad) / h);
                    _minX = minX; _maxZ = maxZ;
                    _ox = (Bounds.Width - w * _scale) * 0.5f; _oy = (Bounds.Height - h * _scale) * 0.5f;
                }
                _dots.Clear();
                foreach (var r in Scene.Race.Racers)
                    if (world.TryGetComponent<Transform>(r.Entity, out var tr))
                    {
                        var at = Transform.GetWorldMatrix(world, tr).Translation;
                        bool player = world.TryGetComponent<KartComponent>(r.Entity, out var k) && k.Player;
                        _dots.Add((new System.Numerics.Vector2(at.X, at.Z), player));
                    }
            }

            private GVec2 Map(System.Numerics.Vector2 p) => new(_ox + (p.X - _minX) * _scale, _oy + (_maxZ - p.Y) * _scale);

            /// <summary>Where each racer's dot is drawn in the element (for a check).</summary>
            public int Dots => _dots.Count;

            public override void Draw(Sokol.GUI.Renderer renderer)
            {
                if (!Visible || _line.Count < 2) return;
                renderer.SetStrokeColor(Road);
                renderer.SetStrokeWidth(Line);
                renderer.BeginPath();
                GVec2 first = Map(_line[0]);
                renderer.MoveTo(first.X, first.Y);
                for (int i = 1; i < _line.Count; i++) { GVec2 q = Map(_line[i]); renderer.LineTo(q.X, q.Y); }
                renderer.ClosePath();
                renderer.Stroke();
                float dot = MathF.Max(3f, Line * 1.4f);
                foreach (var (at, player) in _dots)
                {
                    if (player) continue;
                    renderer.SetFillColor(Kart);
                    renderer.FillCircle(Map(at), dot);
                }
                foreach (var (at, player) in _dots)           // the player's dot over the others
                {
                    if (!player) continue;
                    renderer.SetFillColor(Player);
                    renderer.FillCircle(Map(at), dot * 1.3f);
                }
                base.Draw(renderer);
            }
        }

        private class PanelWidget : Widget
        {
            public UIColor Face;
            public float Radius;

            public override void Draw(Sokol.GUI.Renderer renderer)
            {
                if (!Visible) return;
                if (Face.A > 0f)
                {
                    renderer.SetFillColor(Face);
                    renderer.FillRoundedRect(new Rect(0, 0, Bounds.Width, Bounds.Height), new CornerRadius(MathF.Min(Radius, MathF.Min(Bounds.Width, Bounds.Height) * 0.5f)));
                }
                base.Draw(renderer);
            }
        }

        // A face with its text in the middle; darker while it is held. Clicked when it is let go on it.
        private sealed class ButtonWidget : Label
        {
            public UIColor Face;
            public float Radius;
            private bool _held;

            public ButtonWidget() { Align = TextAlign.Center; }

            public override void Draw(Sokol.GUI.Renderer renderer)
            {
                if (!Visible) return;
                UIColor face = _held ? new UIColor(Face.R * 0.75f, Face.G * 0.75f, Face.B * 0.75f, Face.A) : Face;
                renderer.SetFillColor(face);
                renderer.FillRoundedRect(new Rect(0, 0, Bounds.Width, Bounds.Height), new CornerRadius(MathF.Min(Radius, MathF.Min(Bounds.Width, Bounds.Height) * 0.5f)));
                base.Draw(renderer);
            }

            public override bool OnMouseLeave(MouseEvent e) { _held = false; return true; }

            public override bool OnMouseDown(MouseEvent e)
            {
                if (e.Button != MouseButton.Left) return false;
                _held = true;
                return true;
            }

            public override bool OnMouseUp(MouseEvent e)
            {
                if (e.Button != MouseButton.Left || !_held) return false;
                _held = false;
                GVec2 p = e.LocalPosition;
                if (p.X >= 0f && p.Y >= 0f && p.X <= Bounds.Width && p.Y <= Bounds.Height) RaiseClicked();
                return true;
            }
        }

        // An image, or a frame of a sheet, stretched over the element. The image is read a second time for the
        // UI's renderer: it draws from its own images, not from the scene's textures.
        private sealed class PictureWidget : Widget
        {
            public string Texture = "";
            public Vector4 Tint = Vector4.One;

            public override void Draw(Sokol.GUI.Renderer renderer)
            {
                if (!Visible || Texture.Length == 0) return;
                int hash = Texture.IndexOf('#');
                string file = hash < 0 ? Texture : Texture.Substring(0, hash);
                string imageKey = file;
                SpriteSheet? sheet = null;
                if (SpriteSheet.IsSheetKey(file))
                {
                    if (SpriteLibrary.FindSheet(file, out sheet) != SpriteLibrary.Answer.Ready || sheet!.Image.Length == 0) return;
                    imageKey = sheet.Image;
                }
                if (Images.Find(renderer.VGContext, imageKey, sheet?.Nearest ?? false) is not { } image) return;

                // The part of the image to show, in its pixels: all of it, or the sheet's frame.
                float fx = 0f, fy = 0f, fw = image.Width, fh = image.Height;
                if (sheet != null)
                {
                    IReadOnlyList<SheetFrame> frames = sheet.FramesFor(image.Width, image.Height);
                    if (frames.Count == 0) return;
                    SheetFrame frame = frames[0];
                    if (hash >= 0 && !sheet.TryGetFrame(Texture.Substring(hash + 1), image.Width, image.Height, out frame)) return;
                    fx = frame.X; fy = frame.Y; fw = frame.W; fh = frame.H;
                }
                if (fw <= 0f || fh <= 0f) return;

                // The whole image laid out so that the part lies exactly on the element; only the element is filled.
                float sx = Bounds.Width / fw, sy = Bounds.Height / fh;
                var paint = nvgImagePattern(renderer.VGContext, -fx * sx, -fy * sy, image.Width * sx, image.Height * sy, 0f, image.Id, Tint.W);
                renderer.BeginPath();
                renderer.AddRect(0f, 0f, Bounds.Width, Bounds.Height);
                renderer.SetFillPaint(paint);
                renderer.Fill();
            }
        }

        // The UI renderer's images, by key.
        private static class Images
        {
            private static readonly Dictionary<string, UIImage?> _images = new(StringComparer.Ordinal);
            private static readonly HashSet<string> _pending = new(StringComparer.Ordinal);

            public static UIImage? Find(IntPtr vg, string key, bool nearest)
            {
                if (_images.TryGetValue(key, out UIImage? image)) return image;
                if (_pending.Contains(key)) return null;
                NVGimageFlags flags = nearest ? NVGimageFlags.NVG_IMAGE_NEAREST : 0;
                if (ConfigManager.HasProject)
                {
                    string path = System.IO.Path.Combine(ConfigManager.ProjectFolder!, "Assets", key);
                    image = System.IO.File.Exists(path) ? UIImage.LoadFromMemory(vg, System.IO.File.ReadAllBytes(path), flags) : null;
                    if (image == null) Logger.Warning($"[UI] The picture '{key}' could not be read.");
                    return _images[key] = image;
                }
                _pending.Add(key);
                SFilesystem.LoadFileAsync(key, (path, buffer, status) =>
                {
                    _pending.Remove(key);
                    UIImage? read = status == SFileLoadStatus.Success && buffer != null ? UIImage.LoadFromMemory(vg, buffer, flags) : null;
                    if (read == null) Logger.Warning($"[UI] The picture '{key}' could not be read.");
                    _images[key] = read;
                });
                return null;
            }
        }
    }
}
