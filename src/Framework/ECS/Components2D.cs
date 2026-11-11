using System.Numerics;

namespace GameEditor.Framework.ECS.Components
{
    /// <summary>
    /// Draws an image, or one frame of a sprite sheet, as a flat quad in the entity's XY plane (scene block
    /// "Sprite"). Its size in world units is the image's size in pixels divided by its pixels per unit, times the
    /// Transform's scale. Sprites are drawn after the meshes, back to front by <see cref="Layer"/>, then
    /// <see cref="Order"/>, then distance from the camera.
    /// </summary>
    public struct SpriteRenderer
    {
        /// <summary>100 pixels of an image are one world unit, unless its sprite sheet says otherwise.</summary>
        public const float DefaultPixelsPerUnit = 100f;

        /// <summary>The image's key ("Textures/hero.png"), or a frame of a sheet ("Sprites/hero.sprites#idle_0").</summary>
        public string Texture;
        /// <summary>Multiplies the image's colours: (1, 1, 1, 1) shows them as they are.</summary>
        public Vector4 Color;
        public bool FlipX, FlipY;
        public int Layer, Order;
        public bool Visible;

        public static SpriteRenderer Default => new() { Texture = "", Color = Vector4.One, Visible = true };
    }
}
