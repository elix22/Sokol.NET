using System.Numerics;

namespace GameEditor.Framework.ECS.Components
{
    /// <summary>The nine points of a screen (or of a parent element) an element is placed from.</summary>
    public enum UIAnchor { TopLeft, Top, TopRight, Left, Center, Right, BottomLeft, Bottom, BottomRight }

    /// <summary>How a text lies in its element.</summary>
    public enum UIAlign { Left, Center, Right }

    /// <summary>
    /// A place on the screen (scene block "UI"): what makes an entity a part of the game's UI. With it goes one of
    /// <see cref="UIText"/>, <see cref="UIPicture"/>, <see cref="UIButton"/> or <see cref="UIPanel"/>, which says
    /// what is drawn there. Sizes are in design units of a screen 720 high: on a phone and in a window the UI is
    /// as large in parts of the screen. An element whose entity is a child of another UI entity is placed in
    /// that one's rectangle (a button on a panel).
    /// </summary>
    public struct UIElement
    {
        /// <summary>The point of the screen — less a phone's notch and home bar — the element is placed from.</summary>
        public UIAnchor Anchor;
        /// <summary>
        /// How far from that point, towards the inside: from a corner, in from both edges; from the middle of an
        /// edge, in from the edge and along it (right or down); from the centre, right and down.
        /// </summary>
        public Vector2 Offset;
        /// <summary>Its width and height.</summary>
        public Vector2 Size;
        /// <summary>Drawn over elements of a lower order; a child over its parent.</summary>
        public int Order;
        public bool Visible;

        public static UIElement Default => new() { Anchor = UIAnchor.Center, Size = new Vector2(200f, 48f), Visible = true };
    }

    /// <summary>A text (scene block "UIText"). A script changes what it says: <c>ref var t = ref GetComponent&lt;UIText&gt;(); t.Text = "…";</c></summary>
    public struct UIText
    {
        public string Text;
        /// <summary>The key of a font file below Assets ("Fonts/pixel.ttf"); empty: the engine's own.</summary>
        public string Font;
        /// <summary>The height of its letters, in design units.</summary>
        public float FontSize;
        public Vector4 Color;
        public UIAlign Align;
        /// <summary>A text longer than its element is broken into lines.</summary>
        public bool Wrap;
        /// <summary>A key of the game's text table (UI.Texts): set, it shows that text in the game's language, and <see cref="Text"/> where the key has none.</summary>
        public string Key;

        public static UIText Default => new() { Text = "Text", Font = "", FontSize = 28f, Color = Vector4.One, Align = UIAlign.Left, Key = "" };
    }

    /// <summary>A picture (scene block "UIImage"): an image, or a frame of a sprite sheet, stretched over its element.</summary>
    public struct UIPicture
    {
        /// <summary>An image's key, or a sheet's frame: "Sprites/hud.sprites#heart".</summary>
        public string Texture;
        public Vector4 Color;

        public static UIPicture Default => new() { Texture = "", Color = Vector4.One };
    }

    /// <summary>
    /// A button (scene block "UIButton"). Pressed and let go on it, the scripts of its entity get OnClick(), and
    /// for that frame UI.GameUI.WasClicked(entity) is true for any script.
    /// </summary>
    public struct UIButton
    {
        public string Text;
        public string Font;
        public float FontSize;
        /// <summary>Its face; it is drawn darker while it is held.</summary>
        public Vector4 Color;
        public Vector4 TextColor;
        /// <summary>How round its corners are, in design units.</summary>
        public float Radius;
        /// <summary>
        /// What it holds while a finger (or the mouse) is on it: an axis and a value, "Horizontal:-1", "Vertical:1", or a
        /// button's name, "Fire1", "Jump" — as a key held down. Empty: it only clicks. Several fingers on several buttons hold all.
        /// </summary>
        public string Holds;
        /// <summary>A key of the game's text table (UI.Texts): set, the button says that text in the game's language, and <see cref="Text"/> where the key has none.</summary>
        public string Key;

        public static UIButton Default => new()
        {
            Text = "Button", Font = "", FontSize = 28f, Color = new Vector4(0.2f, 0.45f, 0.9f, 1f), TextColor = Vector4.One, Radius = 10f, Key = "",
        };
    }

    /// <summary>
    /// A map of the race (scene block "UIMiniMap"): the scene's track drawn into the element's rectangle, a dot for each kart
    /// that races — the player's in its own colour.
    /// </summary>
    public struct UIMiniMap
    {
        public Vector4 Road;
        public Vector4 Kart;
        public Vector4 Player;
        /// <summary>The road's line width, in design units.</summary>
        public float Line;

        public static UIMiniMap Default => new()
        {
            Road = new Vector4(1f, 1f, 1f, 0.8f), Kart = new Vector4(0.95f, 0.3f, 0.25f, 1f), Player = new Vector4(1f, 0.85f, 0.1f, 1f), Line = 4f,
        };
    }

    /// <summary>A plain rectangle (scene block "UIPanel"): what stands behind a group of elements.</summary>
    public struct UIPanel
    {
        public Vector4 Color;
        public float Radius;

        public static UIPanel Default => new() { Color = new Vector4(0f, 0f, 0f, 0.6f), Radius = 12f };
    }

    /// <summary>Which way a layout puts its elements.</summary>
    public enum UILayoutDir { Column, Row }

    /// <summary>Where a layout's elements lie across its direction.</summary>
    public enum UILayoutAlign { Start, Center, End, Stretch }

    /// <summary>
    /// A layout (scene block "UILayout"), on an element: the elements under it are placed one after another inside
    /// it — in a column or a row, by <see cref="UIElement.Order"/> and then as they were made — and their own anchor
    /// and offset are not used. A hidden one takes no place. Lengths are in design units.
    /// </summary>
    public struct UILayout
    {
        public UILayoutDir Dir;
        /// <summary>Between two elements.</summary>
        public float Spacing;
        /// <summary>Inside the element's edges: left, top, right, bottom.</summary>
        public Vector4 Padding;
        public UILayoutAlign Align;
        /// <summary>The elements share the length (each keeps its size across); false: each keeps its own size.</summary>
        public bool Fit;

        public static UILayout Default => new() { Dir = UILayoutDir.Column, Spacing = 8f, Align = UILayoutAlign.Start };
    }
}
