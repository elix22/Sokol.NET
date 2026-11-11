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

    /// <summary>
    /// Plays a clip of the sheet the entity's sprite shows (scene block "SpriteAnim"): while the game plays, the
    /// sprite's <see cref="SpriteRenderer.Texture"/> is set to the clip's frame for the time passed. A script
    /// changes <see cref="Clip"/>, <see cref="Speed"/> and <see cref="Playing"/>; a new clip starts at its first
    /// frame. A clip that does not loop stops on its last frame and sets <see cref="Playing"/> false.
    /// </summary>
    public struct SpriteAnimator
    {
        /// <summary>The name of a clip of the sprite's sheet.</summary>
        public string Clip;
        /// <summary>1 plays the clip at its own frames per second; 2 twice as fast; a negative speed plays it backwards.</summary>
        public float Speed;
        public bool Playing;

        // ── Set while the game plays; not saved ──
        /// <summary>The frame of the clip that is shown, from 0.</summary>
        public int Frame;
        /// <summary>Seconds into the clip.</summary>
        public float Time;
        /// <summary>The clip <see cref="Time"/> and <see cref="Frame"/> belong to; another <see cref="Clip"/> starts from 0.</summary>
        public string? Current;
        /// <summary>The sheet the sprite showed when the clip was last looked up.</summary>
        public string? Sheet;

        public static SpriteAnimator Default => new() { Clip = "", Speed = 1f, Playing = true };
    }

    /// <summary>
    /// Draws a tile map (scene block "Tilemap"): the layers of a <c>.tilemap</c> file, each cell a frame of the
    /// map's sprite sheet. The corner of cell (0, 0) is at the entity's position; a cell is the map's cell size
    /// times the Transform's scale. A layer is drawn in its turn among the sprites: by <see cref="Layer"/>, then
    /// <see cref="Order"/> plus the layer's own order, then distance from the camera.
    /// </summary>
    public struct TilemapRenderer
    {
        /// <summary>The map file's key ("Tilemaps/level1.tilemap").</summary>
        public string Map;
        public int Layer, Order;
        /// <summary>Multiplies the tiles' colours: (1, 1, 1, 1) shows them as they are.</summary>
        public Vector4 Color;
        public bool Visible;

        public static TilemapRenderer Default => new() { Map = "", Color = Vector4.One, Visible = true };
    }
}
