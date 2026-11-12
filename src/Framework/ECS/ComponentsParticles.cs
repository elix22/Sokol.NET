using System.Numerics;

namespace GameEditor.Framework.ECS.Components
{
    /// <summary>
    /// Particles in the world (docs/H5_DESIGN.md §16): small quads that face the camera, made at the entity while
    /// <see cref="Emitting"/> - dust behind a kart, sparks off a wall, a boost's flame. Block "Particles". Drawn with the
    /// sprites, one draw call per texture; nothing is allocated per frame (each emitter's pool is made once, at <see cref="Max"/>).
    /// </summary>
    public struct ParticlesComponent
    {
        /// <summary>An image's key or a sheet's frame ("Sheets/fx.sprites#spark"); empty: a soft round dot the engine makes.</summary>
        public string Texture;
        public bool Emitting;
        /// <summary>Particles a second while emitting.</summary>
        public float Rate;
        /// <summary>Particles made at once by <c>Particles.Burst(entity)</c>, and when the scene starts if <see cref="Emitting"/>.</summary>
        public int Burst;
        /// <summary>How long one lives, seconds: [min, max].</summary>
        public Vector2 Life;
        /// <summary>How fast one leaves, metres a second: [min, max].</summary>
        public Vector2 Speed;
        /// <summary>The entity's own axis they leave along.</summary>
        public Vector3 Direction;
        /// <summary>Degrees of the cone about <see cref="Direction"/> they leave in.</summary>
        public float Spread;
        /// <summary>Metres a second squared, down (below 0: they rise).</summary>
        public float Gravity;
        /// <summary>How quickly they slow (1/seconds).</summary>
        public float Drag;
        /// <summary>Its size in metres when made and when it dies.</summary>
        public Vector2 Size;
        public Vector4 ColorStart, ColorEnd;
        /// <summary>True: a particle stays where it was made as the entity moves on (dust). False: it moves with the entity (a flame).</summary>
        public bool World;
        /// <summary>At most this many at once (the pool).</summary>
        public int Max;

        public static ParticlesComponent Default => Dust;

        public static ParticlesComponent Dust => new()
        {
            Texture = "", Emitting = true, Rate = 30f, Burst = 0, Life = new Vector2(0.6f, 1.2f), Speed = new Vector2(0.5f, 1.5f),
            Direction = Vector3.UnitY, Spread = 70f, Gravity = -0.4f, Drag = 1.5f, Size = new Vector2(0.3f, 0.9f),
            ColorStart = new Vector4(0.58f, 0.52f, 0.44f, 0.6f), ColorEnd = new Vector4(0.58f, 0.52f, 0.44f, 0f), World = true, Max = 64,
        };

        public static ParticlesComponent Sparks => new()
        {
            Texture = "", Emitting = true, Rate = 60f, Burst = 0, Life = new Vector2(0.2f, 0.5f), Speed = new Vector2(4f, 8f),
            Direction = Vector3.UnitY, Spread = 100f, Gravity = 9.8f, Drag = 0.5f, Size = new Vector2(0.08f, 0.02f),
            ColorStart = new Vector4(1f, 0.95f, 0.6f, 1f), ColorEnd = new Vector4(1f, 0.5f, 0.1f, 0f), World = true, Max = 64,
        };

        public static ParticlesComponent Flame => new()
        {
            Texture = "", Emitting = true, Rate = 80f, Burst = 0, Life = new Vector2(0.15f, 0.3f), Speed = new Vector2(3f, 5f),
            Direction = -Vector3.UnitZ, Spread = 12f, Gravity = -1f, Drag = 2f, Size = new Vector2(0.35f, 0.1f),
            ColorStart = new Vector4(1f, 0.8f, 0.3f, 0.9f), ColorEnd = new Vector4(1f, 0.2f, 0.05f, 0f), World = false, Max = 48,
        };

        /// <summary>The presets by name (the Inspector's list): Dust, Sparks, Flame.</summary>
        public static readonly string[] Presets = { "Dust", "Sparks", "Flame" };

        public static ParticlesComponent Preset(string name) => name switch
        {
            "Sparks" => Sparks,
            "Flame" => Flame,
            _ => Dust,
        };
    }
}
