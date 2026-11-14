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
        private static readonly Dictionary<Entity, Rect> _parents = new();     // an element's entity → the rectangle it is placed in
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
        public static void HostFrame(float width, float height, float dpi, bool playing,
                                     float safeLeft = 0f, float safeTop = 0f, float safeRight = 0f, float safeBottom = 0f)
        {
            if (_hostScreen == null) return;
            if (!playing) _clicked.Clear();
            // The safe area a phone would have (an editor's preview of one): kept out of, and shown as a faint band.
            _hostSafe = new Thickness(safeLeft, safeTop, safeRight, safeBottom);
            _hostWidth = width; _hostHeight = height;
            // The buttons that hold are held at the start of each game frame (ApplyHolds), not here at the end of the editor's.
            Sync(ECSWorld.Instance, width, height, _hostSafe, holds: false);
            _hostScreen.Update(width, height, dpi);
        }

        private static Thickness _hostSafe = Thickness.Zero;
        private static float _hostWidth, _hostHeight;

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

        private static Entity _canvas;

        /// <summary>The element a host edits with the pointer (its canvas): drawn with handles at its corners and edges and
        /// a mark at the point of its parent it is placed from. Default: none. Asked for each frame.</summary>
        public static void HostCanvas(Entity element) => _canvas = element;

        /// <summary>The side of a handle of <see cref="HostCanvas"/>, in the picture's units.</summary>
        public const float HandleSize = 8f;

        /// <summary>
        /// The handles of an element's rectangle, in the order a host asks for them by: 0 top-left, 1 top, 2 top-right,
        /// 3 right, 4 bottom-right, 5 bottom, 6 bottom-left, 7 left. Their centres.
        /// </summary>
        public static System.Numerics.Vector2 HandleAt(Rect r, int handle) => handle switch
        {
            0 => new(r.X, r.Y), 1 => new(r.X + r.Width * 0.5f, r.Y), 2 => new(r.X + r.Width, r.Y),
            3 => new(r.X + r.Width, r.Y + r.Height * 0.5f), 4 => new(r.X + r.Width, r.Y + r.Height),
            5 => new(r.X + r.Width * 0.5f, r.Y + r.Height), 6 => new(r.X, r.Y + r.Height), _ => new(r.X, r.Y + r.Height * 0.5f),
        };

        /// <summary>The point of a rectangle an anchor names (one of nine).</summary>
        public static System.Numerics.Vector2 AnchorPoint(UIAnchor anchor, Rect r)
        {
            int column = (int)anchor % 3, row = (int)anchor / 3;
            return new(r.X + r.Width * column * 0.5f, r.Y + r.Height * row * 0.5f);
        }

        /// <summary>The front-most element under a point of the picture (the root's units) that is seen; Entity.Null for none.
        /// A child is drawn over its parent: under a child, the child.</summary>
        public static Entity HitTest(float x, float y)
        {
            var children = Root.Children;
            for (int i = children.Count - 1; i >= 0; i--)
            {
                Widget w = children[i];
                if (!w.Visible) continue;
                var b = w.Bounds;
                if (x < b.X || y < b.Y || x > b.X + b.Width || y > b.Y + b.Height) continue;
                foreach (var pair in _items)
                    if (ReferenceEquals(pair.Value.Widget, w)) return pair.Key;
            }
            return Entity.Null;
        }

        /// <summary>The rectangle an element is placed in (its parent element's, or the screen's safe area), after the last frame.</summary>
        public static bool TryGetParentRect(Entity entity, out Rect rect) => _parents.TryGetValue(entity, out rect);

        // The same, in plain numbers: a host with a Sokol.GUI of its own (an editor) has another Rect type.

        public static bool TryGetParentRect(Entity entity, out float x, out float y, out float width, out float height)
        {
            bool found = _parents.TryGetValue(entity, out Rect r);
            (x, y, width, height) = (r.X, r.Y, r.Width, r.Height);
            return found;
        }

        public static (float X, float Y, float Width, float Height) DesignRectOf(in UIElement element, float parentWidth, float parentHeight)
        {
            Rect r = DesignRect(in element, parentWidth, parentHeight);
            return (r.X, r.Y, r.Width, r.Height);
        }

        public static UIElement WithRect(in UIElement element, float x, float y, float width, float height, float parentWidth, float parentHeight) =>
            WithRect(in element, new Rect(x, y, width, height), parentWidth, parentHeight);

        public static (float X, float Y, float Width, float Height) Resized(float x, float y, float width, float height, int handle, float dx, float dy, float min)
        {
            Rect r = Resized(new Rect(x, y, width, height), handle, dx, dy, min);
            return (r.X, r.Y, r.Width, r.Height);
        }

        public static System.Numerics.Vector2 HandleAt(float x, float y, float width, float height, int handle) => HandleAt(new Rect(x, y, width, height), handle);

        // ── An element's rectangle in design units, and back ─────────────────────────────

        /// <summary>An element's rectangle inside a parent of <paramref name="parentWidth"/> × <paramref name="parentHeight"/>
        /// design units, counted from the parent's top-left corner.</summary>
        public static Rect DesignRect(in UIElement element, float parentWidth, float parentHeight) =>
            Place(in element, new Rect(0f, 0f, parentWidth, parentHeight), 1f);

        /// <summary>
        /// The element with its offset and size worked out for <paramref name="rect"/> (design units, from the parent's
        /// top-left corner) under its own anchor — the opposite of <see cref="DesignRect"/>. A move, a resize and a new
        /// anchor that keeps the element where it is are each a rectangle given here.
        /// </summary>
        public static UIElement WithRect(in UIElement element, Rect rect, float parentWidth, float parentHeight)
        {
            var e = element;
            int column = (int)e.Anchor % 3, row = (int)e.Anchor / 3;
            float ox = column switch { 0 => rect.X, 1 => rect.X - (parentWidth - rect.Width) * 0.5f, _ => parentWidth - rect.Width - rect.X };
            float oy = row switch { 0 => rect.Y, 1 => rect.Y - (parentHeight - rect.Height) * 0.5f, _ => parentHeight - rect.Height - rect.Y };
            e.Offset = new System.Numerics.Vector2(ox, oy);
            e.Size = new System.Numerics.Vector2(rect.Width, rect.Height);
            return e;
        }

        /// <summary>A rectangle with one handle (see <see cref="HandleAt"/>) moved by (dx, dy): the opposite side stays; no
        /// side shorter than <paramref name="min"/>.</summary>
        public static Rect Resized(Rect r, int handle, float dx, float dy, float min)
        {
            float left = r.X, top = r.Y, right = r.X + r.Width, bottom = r.Y + r.Height;
            if (handle is 0 or 6 or 7) left = MathF.Min(left + dx, right - min);
            if (handle is 2 or 3 or 4) right = MathF.Max(right + dx, left + min);
            if (handle is 0 or 1 or 2) top = MathF.Min(top + dy, bottom - min);
            if (handle is 4 or 5 or 6) bottom = MathF.Max(bottom + dy, top + min);
            return new Rect(left, top, right - left, bottom - top);
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
                    string kind = pair.Value.Kind switch { 1 => "UIText", 2 => "UIImage", 3 => "UIButton", 4 => "UIPanel", 6 => "UIStick", _ => "UI" };
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
            _parents.Clear();
            _holders.Clear();
            _sticks.Clear();
            _holdWidth = width;
            bool order = false;

            foreach (Entity e in world.Entities)
            {
                if (!world.TryGetComponent<UIElement>(e, out var element)) continue;
                int kind = world.HasComponent<UIText>(e) ? 1 : world.HasComponent<UIPicture>(e) ? 2 : world.HasComponent<UIButton>(e) ? 3
                         : world.HasComponent<UIPanel>(e) ? 4 : world.HasComponent<UIMiniMap>(e) ? 5 : world.HasComponent<UIStick>(e) ? 6 : 0;
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
                if (kind == 6 && w.Visible && world.TryGetComponent<UIStick>(e, out var stick))
                    _sticks.Add((e, rect, stick));
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
            bool wanted = _items.Count > 0 || _outlined.Count > 0 || !_canvas.IsNull || _hostSafe.Horizontal + _hostSafe.Vertical > 0f;
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
                // Under a layout: placed with its brothers, one after another (Phase G2.3).
                if (world.TryGetComponent<UILayout>(p, out var layout))
                {
                    LayOut(world, p, in layout, parent);
                    if (_rects.TryGetValue(e, out Rect laid)) return laid;
                }
            }
            Rect rect = Place(in element, parent, Scale);
            _rects[e] = rect;
            _parents[e] = parent;
            return rect;
        }

        private static readonly List<(Entity E, int Order, int Index, UIElement Element)> _laying = new();

        // The elements under a layout, placed one after another inside it; each also given its parent's rectangle.
        private static void LayOut(ECSWorld world, Entity parent, in UILayout layout, Rect box)
        {
            _laying.Clear();
            int index = 0;
            foreach (Entity child in world.GetChildren(parent))
            {
                index++;
                if (world.TryGetComponent<UIElement>(child, out var el)) _laying.Add((child, el.Order, index, el));
            }
            _laying.Sort(static (a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : a.Index.CompareTo(b.Index));
            var placed = Arrange(in layout, box, Scale, _laying.ConvertAll(x => (x.Element.Size, x.Element.Visible)));
            for (int i = 0; i < _laying.Count; i++)
            {
                _rects[_laying[i].E] = placed[i];
                _parents[_laying[i].E] = box;
            }
        }

        /// <summary>
        /// Where a layout puts its elements, in order: their sizes (design units) and whether each is seen, inside
        /// <paramref name="box"/> (the root's units), <paramref name="scale"/> root units to a design unit. A hidden one
        /// takes no place (it is given an empty rectangle where it would begin).
        /// </summary>
        public static List<Rect> Arrange(in UILayout layout, Rect box, float scale, List<(System.Numerics.Vector2 Size, bool Visible)> elements)
        {
            bool row = layout.Dir == UILayoutDir.Row;
            float padL = layout.Padding.X * scale, padT = layout.Padding.Y * scale, padR = layout.Padding.Z * scale, padB = layout.Padding.W * scale;
            float x0 = box.X + padL, y0 = box.Y + padT;
            float along = row ? box.Width - padL - padR : box.Height - padT - padB;     // the length the elements go along
            float across = row ? box.Height - padT - padB : box.Width - padL - padR;
            float spacing = layout.Spacing * scale;
            int seen = 0;
            foreach (var el in elements) if (el.Visible) seen++;
            float share = seen > 0 ? (along - spacing * (seen - 1)) / seen : 0f;
            var result = new List<Rect>(elements.Count);
            float at = 0f;
            foreach (var el in elements)
            {
                if (!el.Visible) { result.Add(row ? new Rect(x0 + at, y0, 0f, 0f) : new Rect(x0, y0 + at, 0f, 0f)); continue; }
                float length = layout.Fit ? MathF.Max(0f, share) : (row ? el.Size.X : el.Size.Y) * scale;
                float thick = layout.Align == UILayoutAlign.Stretch ? across : (row ? el.Size.Y : el.Size.X) * scale;
                float offset = layout.Align switch
                {
                    UILayoutAlign.Center => (across - thick) * 0.5f,
                    UILayoutAlign.End => across - thick,
                    _ => 0f,
                };
                result.Add(row ? new Rect(x0 + at, y0 + offset, length, thick) : new Rect(x0 + offset, y0 + at, thick, length));
                at += length + spacing;
            }
            return result;
        }

        /// <summary>The element lies under a layout (its own anchor and offset are not used).</summary>
        public static bool IsLaidOut(ECSWorld world, Entity e) =>
            world.TryGetComponent<Transform>(e, out var tr) && tr.Parent is { } p && world.HasComponent<UILayout>(p) && world.HasComponent<UIElement>(p);

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

        // ── On-screen sticks (G7.2) ─────────────────────────────────────────────────────

        private const int NoFinger = -1, MouseFinger = -2;

        private sealed class StickState
        {
            public int Finger = NoFinger;      // the touch's finger that holds it, the mouse, or none
            public GVec2 Knob;                 // the knob's offset from the ring's middle, in the root's units
            public float X, Y;                 // the axes it gives now
        }

        private static readonly List<(Entity Entity, Rect Rect, UIStick Stick)> _sticks = new();   // where the last Sync placed them
        private static readonly Dictionary<Entity, StickState> _stickState = new();

        /// <summary>The axes an on-screen stick gives now (x right, y up, each −1 … 1). False for an entity that is no stick.</summary>
        public static bool TryGetStick(Entity entity, out float x, out float y)
        {
            bool found = _stickState.TryGetValue(entity, out var st);
            (x, y) = found ? (st!.X, st.Y) : (0f, 0f);
            return found;
        }

        /// <summary>
        /// What a stick gives for a knob <paramref name="dx"/>, <paramref name="dy"/> from the ring's middle (screen units,
        /// y down) on a ring of <paramref name="radius"/>: the knob clamped to the ring, each axis 0 inside the dead zone and
        /// 1 at the ring's edge, y up.
        /// </summary>
        public static (float X, float Y, float KnobX, float KnobY) StickAxes(float dx, float dy, float radius, float deadZone)
        {
            if (radius <= 0f) return (0f, 0f, 0f, 0f);
            float len = MathF.Sqrt(dx * dx + dy * dy);
            if (len > radius) { dx *= radius / len; dy *= radius / len; len = radius; }
            float m = len / radius, dz = Math.Clamp(deadZone, 0f, 0.9f);
            if (m <= dz) return (0f, 0f, dx, dy);
            float k = (m - dz) / (1f - dz) / m;
            return (Math.Clamp(dx / radius * k, -1f, 1f), Math.Clamp(-dy / radius * k, -1f, 1f), dx, dy);
        }

        // Each frame: a finger pressed on a stick takes it until it is lifted (the mouse's left button is a finger); the
        // knob follows it, and the stick's axes are held as a button's Holds are.
        private static void MoveSticks()
        {
            if (_sticks.Count == 0 && _stickState.Count == 0) return;
            var size = Input.InputManager.ScreenSize;
            float k = size.X > 0f ? _holdWidth / size.X : 1f;
            foreach (var (e, rect, stick) in _sticks)
            {
                if (!_stickState.TryGetValue(e, out var st)) _stickState[e] = st = new StickState();
                if (st.Finger == NoFinger)
                {
                    for (int i = 0; i < Input.InputManager.TouchCount && st.Finger == NoFinger; i++)
                    {
                        var t = Input.InputManager.GetTouch(i);
                        if (t.Phase == Input.TouchPhase.Began && Inside(rect, t.Position.X * k, t.Position.Y * k) && !Taken(t.FingerId)) st.Finger = t.FingerId;
                    }
                    var m = Input.InputManager.MousePosition;
                    if (st.Finger == NoFinger && Input.InputManager.GetMouseButtonDown(0) && Inside(rect, m.X * k, m.Y * k) && !Taken(MouseFinger)) st.Finger = MouseFinger;
                }
                GVec2? at = null;
                if (st.Finger == MouseFinger)
                {
                    if (Input.InputManager.GetMouseButton(0)) at = new GVec2(Input.InputManager.MousePosition.X * k, Input.InputManager.MousePosition.Y * k);
                }
                else if (st.Finger != NoFinger)
                    for (int i = 0; i < Input.InputManager.TouchCount; i++)
                    {
                        var t = Input.InputManager.GetTouch(i);
                        if (t.FingerId == st.Finger && t.Phase != Input.TouchPhase.Ended && t.Phase != Input.TouchPhase.Cancelled) at = new GVec2(t.Position.X * k, t.Position.Y * k);
                    }
                if (at is not { } p) { st.Finger = NoFinger; st.Knob = default; st.X = st.Y = 0f; continue; }
                float cx = rect.X + rect.Width * 0.5f, cy = rect.Y + rect.Height * 0.5f;
                var (x, y, kx, ky) = StickAxes(p.X - cx, p.Y - cy, MathF.Min(rect.Width, rect.Height) * 0.5f, stick.DeadZone);
                st.Knob = new GVec2(kx, ky);
                st.X = x; st.Y = y;
                if (!string.IsNullOrEmpty(stick.X)) Input.InputManager.SetVirtualAxis(stick.X, x);
                if (!string.IsNullOrEmpty(stick.Y)) Input.InputManager.SetVirtualAxis(stick.Y, y);
            }

            static bool Inside(Rect r, float x, float y) => x >= r.X && y >= r.Y && x <= r.X + r.Width && y <= r.Y + r.Height;
        }

        private static bool Taken(int finger)
        {
            foreach (var st in _stickState.Values) if (st.Finger == finger) return true;
            return false;
        }
        private static float _holdWidth;

        /// <summary>
        /// The axes and buttons of the buttons that a finger or the mouse is on now, as the input of this frame
        /// (InputManager's overrides, which last one frame): call after the frame's InputManager.BeginFrame and before
        /// its physics. <see cref="Sync"/> calls it; an editor, whose Sync comes at the end of its frame, calls it at the
        /// start of each game frame it runs.
        /// </summary>
        public static void ApplyHolds()
        {
            MoveSticks();
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
                case 6: return new StickWidget();
                default: return new PlaceWidget();
            }
        }

        private static UIColor Colour(Vector4 c) => new(c.X, c.Y, c.Z, c.W);

        private static void Apply(ECSWorld world, Entity e, Item item)
        {
            switch (item.Kind)
            {
                case 1 when item.Widget is Label label && world.TryGetComponent<UIText>(e, out var text):
                    label.Text = Said(text.Key, text.Text);
                    label.FontName = FontOf(text.Font);
                    label.FontSize = MathF.Max(1f, text.FontSize * Scale);
                    label.ForeColor = Colour(text.Color);
                    AlignText(label, text.Align);
                    label.Wrap = text.Wrap ? TextWrap.Wrap : TextWrap.None;
                    break;
                case 2 when item.Widget is PictureWidget picture && world.TryGetComponent<UIPicture>(e, out var image):
                    picture.Texture = image.Texture ?? "";
                    picture.Tint = image.Color;
                    break;
                case 3 when item.Widget is ButtonWidget button && world.TryGetComponent<UIButton>(e, out var b):
                    button.Text = Said(b.Key, b.Text);
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
                case 6 when item.Widget is StickWidget stickWidget && world.TryGetComponent<UIStick>(e, out var st):
                    stickWidget.Ring = Colour(st.Color);
                    stickWidget.Knob = _stickState.TryGetValue(e, out var held) ? held.Knob : default;
                    break;
                case 5 when item.Widget is MiniMapWidget map && world.TryGetComponent<UIMiniMap>(e, out var mm):
                    map.Road = Colour(mm.Road); map.Kart = Colour(mm.Kart); map.Player = Colour(mm.Player);
                    map.Line = MathF.Max(1f, mm.Line * Scale);
                    map.Refresh(world);
                    break;
            }
        }

        // What a text or a button says: its key's text in the game's language, else its own text.
        private static string Said(string? key, string? text) =>
            string.IsNullOrEmpty(key) ? text ?? "" : Texts.Get(key) ?? text ?? "";

        // In a right-to-left language Left and Right are the start and the end of the line: swapped, and the label told
        // not to flip again. Otherwise as Sokol.GUI does (a Hebrew line in an English game starts at the right).
        private static void AlignText(Label label, UIAlign align)
        {
            if (Texts.RightToLeft)
            {
                label.FlowDirection = FlowDirection.LeftToRight;
                label.Align = align switch { UIAlign.Left => TextAlign.Right, UIAlign.Right => TextAlign.Left, _ => TextAlign.Center };
            }
            else
            {
                label.FlowDirection = FlowDirection.Auto;
                label.Align = (TextAlign)(int)align;
            }
        }

        // A picture for the UI: a PNG / JPG as before, or an .svg rasterized at its own size (G8.4 D2).
        private static UIImage? Picture(IntPtr vg, byte[] bytes, NVGimageFlags flags)
        {
            if (!Assets.SvgRaster.IsSvg(bytes)) return UIImage.LoadFromMemory(vg, bytes, flags);
            var rgba = Assets.SvgRaster.Rasterize(bytes, 1f, out int w, out int h);
            return rgba == null ? null : UIImage.FromRgba(vg, w, h, rgba, flags);
        }

        // ── For the world's text (G8.4: Text3D draws with the UI's NanoVG context and fonts) ───────────────
        /// <summary>The NanoVG context of the screen the game's UI is shown on (a built game's, an editor's host), or 0.</summary>
        internal static IntPtr TextVg => (_host as Screen)?.Renderer?.VGContext ?? IntPtr.Zero;
        /// <summary>The registry's name of a text's font (the engine's own, or the game's font file by its key), registered.</summary>
        internal static string FontName(string? key) => FontOf(key);
        /// <summary>A text as shown: its key's text in the game's language, else the text itself.</summary>
        internal static string Shown(string? key, string? text) => Said(key, text);

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
            if (_fonts.Contains(key))
            {
                // A font of the game falls back to the engine's for the letters it lacks (a Hebrew font has no Latin).
                if (!_fallbacks.Contains(key) && FontRegistry.Instance.Get(key) is { } font && FontRegistry.Instance.Get(DefaultFont) is { } engine)
                {
                    _fallbacks.Add(key);
                    _ = nvgAddFallbackFontId(vg, font.Id, engine.Id);
                }
                return key;
            }
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

        private static readonly HashSet<string> _fonts = new(StringComparer.Ordinal), _fontsMissing = new(StringComparer.Ordinal), _fallbacks = new(StringComparer.Ordinal);

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
            _sticks.Clear();
            _stickState.Clear();
            _holders.Clear();
        }

        // ── The widgets ─────────────────────────────────────────────────────────────────

        // The root draws its children, then the rectangles a host asked to have outlined.
        private sealed class GameUIRoot : Widget
        {
            private static readonly UIColor Outline = UIColor.FromHex("#F9E2AF");
            private static readonly UIColor HandleEdge = UIColor.FromHex("#1E1E2E");
            private static readonly UIColor Anchor = UIColor.FromHex("#89DCEB");

            // Sync places every widget itself, each frame: the screen's layout pass has nothing to do here (and
            // allocated in every frame).
            public override void PerformLayout(Sokol.GUI.Renderer renderer, bool force = false) { }

            private static readonly UIColor Unsafe = new(1f, 1f, 1f, 0.08f);

            public override void Draw(Sokol.GUI.Renderer renderer)
            {
                // A host's preview of a phone: its notch and home bar as faint bands, under the UI.
                var safe = _hostSafe;
                if (safe.Left > 0f) renderer.FillRect(new Rect(0f, 0f, safe.Left, _hostHeight), Unsafe);
                if (safe.Right > 0f) renderer.FillRect(new Rect(_hostWidth - safe.Right, 0f, safe.Right, _hostHeight), Unsafe);
                if (safe.Top > 0f) renderer.FillRect(new Rect(0f, 0f, _hostWidth, safe.Top), Unsafe);
                if (safe.Bottom > 0f) renderer.FillRect(new Rect(0f, _hostHeight - safe.Bottom, _hostWidth, safe.Bottom), Unsafe);
                base.Draw(renderer);
                foreach (Entity e in _outlined)
                {
                    if (!_rects.TryGetValue(e, out Rect r)) continue;
                    renderer.SetStrokeColor(Outline);
                    renderer.SetStrokeWidth(1.5f);
                    renderer.StrokeRect(new Rect(r.X + 0.75f, r.Y + 0.75f, MathF.Max(0f, r.Width - 1.5f), MathF.Max(0f, r.Height - 1.5f)));
                }
                // The element a host edits: a line from the point of its parent it is placed from to its own point that
                // counts, a mark there, and a handle at each corner and edge.
                if (!_canvas.IsNull && _rects.TryGetValue(_canvas, out Rect c) && _parents.TryGetValue(_canvas, out Rect parent)
                    && ECSWorld.Instance.TryGetComponent<UIElement>(_canvas, out var element))
                {
                    var from = AnchorPoint(element.Anchor, parent);
                    var to = AnchorPoint(element.Anchor, c);
                    renderer.SetStrokeColor(Anchor);
                    renderer.SetStrokeWidth(1f);
                    renderer.DrawLine(from.X, from.Y, to.X, to.Y, 1f, Anchor);
                    renderer.FillRect(new Rect(from.X - 4f, from.Y - 4f, 8f, 8f), Anchor);
                    for (int h = 0; h < 8; h++)
                    {
                        var at = HandleAt(c, h);
                        var box = new Rect(at.X - HandleSize * 0.5f, at.Y - HandleSize * 0.5f, HandleSize, HandleSize);
                        renderer.FillRect(box, Outline);
                        renderer.SetStrokeColor(HandleEdge);
                        renderer.SetStrokeWidth(1f);
                        renderer.StrokeRect(box);
                    }
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

        // A ring filling the element and a knob where the finger holds it (in the middle when none does).
        private sealed class StickWidget : Widget
        {
            public UIColor Ring;
            public GVec2 Knob;

            public override void Draw(Sokol.GUI.Renderer renderer)
            {
                if (!Visible) return;
                float r = MathF.Min(Bounds.Width, Bounds.Height) * 0.5f;
                var c = new GVec2(Bounds.Width * 0.5f, Bounds.Height * 0.5f);
                renderer.SetFillColor(new UIColor(Ring.R, Ring.G, Ring.B, Ring.A * 0.5f));
                renderer.FillCircle(c, r);
                renderer.SetStrokeColor(Ring);
                renderer.SetStrokeWidth(MathF.Max(1.5f, r * 0.04f));
                renderer.StrokeCircle(c, r - 1f);
                renderer.SetFillColor(new UIColor(Ring.R, Ring.G, Ring.B, MathF.Min(1f, Ring.A * 2.2f)));
                renderer.FillCircle(new GVec2(c.X + Knob.X, c.Y + Knob.Y), r * 0.42f);
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
                    image = System.IO.File.Exists(path) ? Picture(vg, System.IO.File.ReadAllBytes(path), flags) : null;
                    if (image == null) Logger.Warning($"[UI] The picture '{key}' could not be read.");
                    return _images[key] = image;
                }
                _pending.Add(key);
                SFilesystem.LoadFileAsync(key, (path, buffer, status) =>
                {
                    _pending.Remove(key);
                    UIImage? read = status == SFileLoadStatus.Success && buffer != null ? Picture(vg, buffer, flags) : null;
                    if (read == null) Logger.Warning($"[UI] The picture '{key}' could not be read.");
                    _images[key] = read;
                });
                return null;
            }
        }
    }
}
