using System;
using System.Numerics;

namespace GameEditor.Framework.ECS.Components
{
    /// <summary>The Shape module's kinds (the reference editor's Particle System shapes).</summary>
    public enum ParticleShape : byte { Cone, Sphere, Hemisphere, Box, Circle, Edge }

    /// <summary>Where a Cone emits from: its base (a disc of the radius), or anywhere in its volume up to its length.</summary>
    public enum ParticleConeFrom : byte { Base, Volume }

    /// <summary>Where a Box emits from: inside it, its faces, or its edges.</summary>
    public enum ParticleBoxFrom : byte { Volume, Shell, Edge }

    /// <summary>The Renderer module's modes: facing the camera, stretched along the speed, flat on the ground, upright facing the camera.</summary>
    public enum ParticleRenderMode : byte { Billboard, Stretched, Horizontal, Vertical }

    public enum ParticleBlend : byte { Alpha, Additive }

    /// <summary>The order an emitter's particles are drawn in: as made, the farthest first, the oldest in front, the youngest in front.</summary>
    public enum ParticleSort : byte { None, ByDistance, OldestInFront, YoungestInFront }

    /// <summary>The Collision module's kinds: planes (entities' positions and ups) or the physics world.</summary>
    public enum ParticleCollisionType : byte { Planes, World }

    /// <summary>How many particles a frame are tested against the world: all, half, a quarter (each from where it was last tested).</summary>
    public enum ParticleCollisionQuality : byte { High, Medium, Low }

    /// <summary>The Texture Sheet Animation's time: the frames over a particle's life, or so many a second.</summary>
    public enum ParticleSheetTime : byte { Lifetime, Fps }

    /// <summary>
    /// A particle system in the world (examples/GameEditor/docs/PARTICLES_3D_DESIGN.md; H5_DESIGN.md §16 before it): quads made
    /// at the entity, its modules those of the reference editor's Particle System - Main, Emission, Shape, Velocity / Limit
    /// Velocity / Force / Color / Size / Rotation over Lifetime, Noise, Texture Sheet Animation, Renderer - each module with
    /// its checkbox (<c>...On</c>). Block "Particles". Simulated on the CPU, a pool per emitter made once at <see cref="Max"/>:
    /// nothing is allocated per frame. Every value is a value (the curve and the gradient too): a copy is a whole copy.
    /// The fields of before G8.7 keep their meaning (§3.0).
    /// </summary>
    public struct ParticlesComponent
    {
        // ---- Main ----
        /// <summary>Seconds of one loop of the system: Rate over Time and the Bursts run over it.</summary>
        public float Duration;
        public bool Looping;
        /// <summary>A looping system starts as if it had already run one loop.</summary>
        public bool Prewarm;
        /// <summary>Seconds after Play before it starts making particles.</summary>
        public float StartDelay;
        /// <summary>Plays when the scene starts (or the emitter first runs); off: only after <c>WorldParticles.Play</c>.</summary>
        public bool PlayOnAwake;
        /// <summary>How fast the whole system runs (1: the game's time).</summary>
        public float SimulationSpeed;
        /// <summary>Start Lifetime, seconds: [min, max] (equal: a constant).</summary>
        public Vector2 Life;
        /// <summary>Start Speed, metres a second: [min, max].</summary>
        public Vector2 Speed;
        /// <summary>Start Size, metres: [min, max].</summary>
        public Vector2 StartSize;
        /// <summary>Start Rotation, degrees in the view: [min, max].</summary>
        public Vector2 StartRotation;
        /// <summary>Start Color: random between the two (equal: a constant).</summary>
        public Vector4 StartColor, StartColor2;
        /// <summary>Metres a second squared, down (below 0: they rise). The Inspector shows the Gravity Modifier, this / 9.81.</summary>
        public float Gravity;
        /// <summary>Simulation Space. True: a particle stays where it was made as the entity moves on (dust). False: it moves
        /// (and turns) with the entity (a flame).</summary>
        public bool World;
        /// <summary>Max Particles: at most this many at once (the pool).</summary>
        public int Max;
        /// <summary>On: each emitter its own seed (from the entity); off: <see cref="RandomSeed"/> - the same particles every run.</summary>
        public bool AutoRandomSeed;
        public uint RandomSeed;

        // ---- Emission ----
        /// <summary>The Emission module's checkbox: off, nothing new is made; the ones alive live on.</summary>
        public bool Emitting;
        /// <summary>Rate over Time: particles a second.</summary>
        public float Rate;
        /// <summary>Rate over Distance: particles a metre the emitter moves.</summary>
        public float RateOverDistance;
        public ParticleBursts Bursts;
        /// <summary>Particles made at once by <c>WorldParticles.Burst(entity)</c>.</summary>
        public int Burst;

        // ---- Shape ----
        public bool ShapeOn;
        public ParticleShape Shape;
        /// <summary>The shape's axis, in the entity's own space (the way a Cone opens, a Hemisphere's top, a Box's "forward").</summary>
        public Vector3 Direction;
        /// <summary>Degrees of the Cone's opening, the whole of it (the Inspector's Angle is the half).</summary>
        public float Spread;
        public float Radius;
        /// <summary>0: only from the shape's surface; 1: from all of it.</summary>
        public float RadiusThickness;
        /// <summary>Degrees of the circle used, around the axis.</summary>
        public float Arc;
        /// <summary>The Cone's length (when it emits from its volume).</summary>
        public float Length;
        public ParticleConeFrom ConeFrom;
        public ParticleBoxFrom BoxFrom;
        /// <summary>The shape's place and size in the entity's space (a Box's size is its scale).</summary>
        public Vector3 ShapePosition, ShapeScale;
        /// <summary>0..1: the direction mixed with a random one / with the one from the shape's centre.</summary>
        public float RandomizeDirection, SpherizeDirection;

        // ---- Velocity over Lifetime ----
        public bool VelocityOn;
        public Vector3 Velocity;
        public bool VelocityWorld;

        // ---- Limit Velocity over Lifetime ----
        public bool LimitOn;
        /// <summary>The speed a particle is slowed to (by <see cref="Dampen"/>), metres a second.</summary>
        public float SpeedLimit;
        /// <summary>0..1: how much of the speed above the limit is taken away (a thirtieth of a second's worth).</summary>
        public float Dampen;
        /// <summary>How quickly they slow (1/seconds).</summary>
        public float Drag;

        // ---- Force over Lifetime ----
        public bool ForceOn;
        public Vector3 Force;
        public bool ForceWorld;

        // ---- Color over Lifetime ----
        public bool ColorOn;
        public ParticleGradient ColorOverLifetime;

        // ---- Size over Lifetime ----
        public bool SizeOn;
        public ParticleCurve SizeOverLifetime;

        // ---- Rotation over Lifetime ----
        public bool RotationOn;
        /// <summary>Degrees a second: [min, max].</summary>
        public Vector2 AngularVelocity;

        // ---- Noise ----
        public bool NoiseOn;
        /// <summary>Metres a second the noise pushes at most.</summary>
        public float NoiseStrength;
        /// <summary>How fine the noise is (1/metres).</summary>
        public float NoiseFrequency;
        /// <summary>How fast the noise changes (its field moves through itself, a second).</summary>
        public float NoiseScroll;

        // ---- Collision (G8.8 Q1) ----
        public bool CollisionOn;
        public ParticleCollisionType CollisionType;
        public ParticlePlanes Planes;
        /// <summary>0..1: the part of the speed lost in a hit.</summary>
        public float CollisionDampen;
        /// <summary>The part of the speed into the surface given back (1: as fast out as in).</summary>
        public float Bounce;
        /// <summary>0..1: the part of its life a particle loses in a hit (1: it dies).</summary>
        public float LifetimeLoss;
        /// <summary>After a hit a particle slower than this, or faster than <see cref="MaxKillSpeed"/>, dies.</summary>
        public float MinKillSpeed, MaxKillSpeed;
        /// <summary>A particle's radius for its hits: half its size times this.</summary>
        public float RadiusScale;
        public ParticleCollisionQuality CollisionQuality;
        /// <summary>Calls OnParticleCollision(other) on the emitter's scripts and on the other's.</summary>
        public bool SendCollisionMessages;

        // ---- Texture Sheet Animation ----
        public bool SheetOn;
        public int SheetColumns, SheetRows;
        public ParticleSheetTime SheetTime;
        /// <summary>Frames a second (time mode Fps).</summary>
        public float SheetFps;
        /// <summary>Times through the sheet over a life (time mode Lifetime).</summary>
        public float SheetCycles;
        /// <summary>Start Frame: [min, max].</summary>
        public Vector2 SheetStartFrame;

        // ---- Renderer ----
        /// <summary>An image's key or a sheet's frame ("Sheets/fx.sprites#spark"); empty: a soft round dot the engine makes.</summary>
        public string Texture;
        public ParticleRenderMode RenderMode;
        /// <summary>Stretched: the length grows by this many times the speed (seconds) ...</summary>
        public float SpeedScale;
        /// <summary>... and is this many times the size.</summary>
        public float LengthScale;
        public ParticleBlend Blend;
        public ParticleSort Sort;

        // ---- The fields of before G8.7, as views over the modules (§3.0) ----

        /// <summary>Color over Lifetime's colour at birth; setting it sets the gradient's first keys and turns the module on.</summary>
        public Vector4 ColorStart
        {
            readonly get => ColorOverLifetime.Evaluate(0f);
            set { SetEnd(ref ColorOverLifetime, value, 0f); ColorOn = true; }
        }

        /// <summary>Color over Lifetime's colour at death; setting it sets the gradient's last keys and turns the module on.</summary>
        public Vector4 ColorEnd
        {
            readonly get => ColorOverLifetime.Evaluate(1f);
            set { SetEnd(ref ColorOverLifetime, value, 1f); ColorOn = true; }
        }

        /// <summary>The size when made and when it dies: Start Size times Size over Lifetime at 0 and 1. Setting it gives Start
        /// Size the larger and a straight Size over Lifetime.</summary>
        public Vector2 Size
        {
            readonly get => SizeOn ? new Vector2(StartSize.X * SizeOverLifetime.Evaluate(0f), StartSize.X * SizeOverLifetime.Evaluate(1f)) : new Vector2(StartSize.X, StartSize.X);
            set
            {
                float m = MathF.Max(value.X, value.Y);
                StartSize = new Vector2(m, m);
                SizeOverLifetime = m > 0f ? ParticleCurve.Line(value.X / m, value.Y / m) : ParticleCurve.Line(0f, 0f);
                SizeOn = true;
            }
        }

        private static void SetEnd(ref ParticleGradient g, Vector4 v, float end)
        {
            if (g.ColorCount == 0 || g.AlphaCount == 0) g = ParticleGradient.Two(v, v);
            int ci = end == 0f ? 0 : g.ColorCount - 1, ai = end == 0f ? 0 : g.AlphaCount - 1;
            g.Colors[ci] = new Vector4(v.X, v.Y, v.Z, g.Colors[ci].W);
            g.Alphas[ai] = new Vector2(v.W, g.Alphas[ai].Y);
        }

        // ---- Presets ----

        /// <summary>Every module's own defaults (the reference editor's), all but Main, Emission, Shape and Renderer off.</summary>
        public static ParticlesComponent Base => new()
        {
            Duration = 5f, Looping = true, Prewarm = false, StartDelay = 0f, PlayOnAwake = true, SimulationSpeed = 1f,
            Life = new Vector2(5f, 5f), Speed = new Vector2(5f, 5f), StartSize = new Vector2(1f, 1f), StartRotation = Vector2.Zero,
            StartColor = Vector4.One, StartColor2 = Vector4.One, Gravity = 0f, World = false, Max = 1000, AutoRandomSeed = true, RandomSeed = 0,
            Emitting = true, Rate = 10f, RateOverDistance = 0f, Burst = 0,
            ShapeOn = true, Shape = ParticleShape.Cone, Direction = Vector3.UnitY, Spread = 50f, Radius = 1f, RadiusThickness = 1f,
            Arc = 360f, Length = 5f, ConeFrom = ParticleConeFrom.Base, BoxFrom = ParticleBoxFrom.Volume,
            ShapePosition = Vector3.Zero, ShapeScale = Vector3.One,
            Velocity = Vector3.Zero, SpeedLimit = 1f, Dampen = 0f, Drag = 0f, Force = Vector3.Zero,
            ColorOverLifetime = ParticleGradient.White, SizeOverLifetime = ParticleCurve.Line(1f, 1f),
            AngularVelocity = new Vector2(45f, 45f),
            NoiseStrength = 1f, NoiseFrequency = 0.5f, NoiseScroll = 0f,
            CollisionType = ParticleCollisionType.Planes, CollisionDampen = 0f, Bounce = 1f, LifetimeLoss = 0f, MinKillSpeed = 0f,
            MaxKillSpeed = 10000f, RadiusScale = 1f, CollisionQuality = ParticleCollisionQuality.High,
            SheetColumns = 1, SheetRows = 1, SheetTime = ParticleSheetTime.Lifetime, SheetFps = 10f, SheetCycles = 1f, SheetStartFrame = Vector2.Zero,
            Texture = "", RenderMode = ParticleRenderMode.Billboard, SpeedScale = 0f, LengthScale = 2f, Blend = ParticleBlend.Alpha, Sort = ParticleSort.None,
        };

        // The presets of H5 (Zippalong's karts): a cone of no radius, a straight colour and size over the life, drag.
        private static ParticlesComponent Simple(float rate, Vector2 life, Vector2 speed, Vector3 direction, float spread, float gravity,
                                                 float drag, Vector2 size, Vector4 c0, Vector4 c1, bool world, int max)
        {
            var p = Base;
            p.Rate = rate; p.Life = life; p.Speed = speed; p.Direction = direction; p.Spread = spread; p.Radius = 0f;
            p.Gravity = gravity; p.Drag = drag; p.LimitOn = true; p.Dampen = 0f;
            p.Size = size;
            p.ColorOverLifetime = ParticleGradient.Two(c0, c1); p.ColorOn = true;
            p.World = world; p.Max = max;
            return p;
        }

        public static ParticlesComponent Default => Dust;

        public static ParticlesComponent Dust => Simple(30f, new Vector2(0.6f, 1.2f), new Vector2(0.5f, 1.5f), Vector3.UnitY, 70f, -0.4f, 1.5f,
            new Vector2(0.3f, 0.9f), new Vector4(0.58f, 0.52f, 0.44f, 0.6f), new Vector4(0.58f, 0.52f, 0.44f, 0f), true, 64);

        public static ParticlesComponent Sparks => Simple(60f, new Vector2(0.2f, 0.5f), new Vector2(4f, 8f), Vector3.UnitY, 100f, 9.8f, 0.5f,
            new Vector2(0.08f, 0.02f), new Vector4(1f, 0.95f, 0.6f, 1f), new Vector4(1f, 0.5f, 0.1f, 0f), true, 64);

        public static ParticlesComponent Flame => Simple(80f, new Vector2(0.15f, 0.3f), new Vector2(3f, 5f), -Vector3.UnitZ, 12f, -1f, 2f,
            new Vector2(0.35f, 0.1f), new Vector4(1f, 0.8f, 0.3f, 0.9f), new Vector4(1f, 0.2f, 0.05f, 0f), false, 48);

        /// <summary>The reference editor's new Particle System: a cone of radius 1 and 25°, 10 a second, white, five seconds each.</summary>
        public static ParticlesComponent Standard => Base;

        /// <summary>A fire of the new modules: additive, a gradient yellow → red → gone, a size curve that swells and shrinks, noise.</summary>
        public static ParticlesComponent Fire
        {
            get
            {
                var p = Base;
                p.Rate = 40f; p.Life = new Vector2(0.6f, 1.1f); p.Speed = new Vector2(0.8f, 1.6f); p.StartSize = new Vector2(0.4f, 0.7f);
                p.StartRotation = new Vector2(0f, 360f); p.Gravity = -1.5f; p.World = true; p.Max = 128;
                p.Spread = 20f; p.Radius = 0.25f;
                p.ColorOn = true;
                p.ColorOverLifetime = ParticleGradient.Two(new Vector4(1f, 0.9f, 0.4f, 0f), new Vector4(0.8f, 0.1f, 0.02f, 0f));
                p.ColorOverLifetime.AddColor(0.4f, new Vector3(1f, 0.45f, 0.08f), out _);
                p.ColorOverLifetime.AddAlpha(0.15f, 1f, out _);
                p.SizeOn = true; p.SizeOverLifetime = ParticleCurve.Of(new Vector2(0f, 0.5f), new Vector2(0.3f, 1f), new Vector2(1f, 0.2f));
                p.RotationOn = true; p.AngularVelocity = new Vector2(-90f, 90f);
                p.NoiseOn = true; p.NoiseStrength = 0.6f; p.NoiseFrequency = 1.2f; p.NoiseScroll = 0.8f;
                p.Blend = ParticleBlend.Additive;
                return p;
            }
        }

        /// <summary>The presets by name (the Inspector's list).</summary>
        public static readonly string[] Presets = { "Standard", "Dust", "Sparks", "Flame", "Fire" };

        public static ParticlesComponent Preset(string name) => name switch
        {
            "Standard" => Standard,
            "Sparks" => Sparks,
            "Flame" => Flame,
            "Fire" => Fire,
            _ => Dust,
        };
    }
}
