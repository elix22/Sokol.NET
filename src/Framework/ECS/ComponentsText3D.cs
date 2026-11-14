using System.Numerics;

namespace GameEditor.Framework.ECS.Components
{
    /// <summary>How a <see cref="Text3D"/> faces: as its entity turns it (Off), the camera (Full), the camera about Y only.</summary>
    public enum Text3DBillboard { Off, Full, Y }

    /// <summary>
    /// Text in the world (G8.4, examples/GameEditor/docs/TEXT3D_SVG_DRAW_DESIGN.md; scene block "Text3D"): a sign, a name
    /// over a character, a score rising from a hit — placed and turned by its entity, depth-tested with the scene. Drawn
    /// with the game UI's fonts (any font file of the project) into a texture again only when it changes.
    /// </summary>
    public struct Text3D
    {
        public string Text;
        /// <summary>A key of the game's text table (UI.Texts): set, the text in the game's language, <see cref="Text"/> where the key has none.</summary>
        public string Key;
        /// <summary>The key of a font file below Assets; empty: the engine's own.</summary>
        public string Font;
        /// <summary>The height of a line, in world units (metres).</summary>
        public float Size;
        public Vector4 Color;
        public UIAlign Align;
        public Text3DBillboard Billboard;
        /// <summary>The outline's width, a fraction of a line's height (0: none; 0.1 a thin one).</summary>
        public float Outline;
        public Vector4 OutlineColor;

        public static Text3D Default => new()
        {
            Text = "Text", Key = "", Font = "", Size = 0.5f, Color = Vector4.One, Align = UIAlign.Center,
            Billboard = Text3DBillboard.Off, Outline = 0f, OutlineColor = new Vector4(0, 0, 0, 1),
        };
    }
}
