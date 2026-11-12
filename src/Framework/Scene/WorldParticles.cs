using System;
using System.Collections.Generic;
using System.Numerics;
using Frent;
using GameEditor.Framework.Assets;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using Sokol.Render2D;
using static Sokol.SG;

namespace GameEditor.Framework.Scene
{
    /// <summary>
    /// The particles of the world (docs/H5_DESIGN.md §16): one pool for each entity with a <see cref="ParticlesComponent"/>,
    /// made the first time it is seen and kept; <see cref="Step"/> ages, moves and makes them with the game's time, and the
    /// sprite drawing draws them (<see cref="Draw"/>) as quads facing the camera, the emitters back to front, a texture's
    /// particles in one draw call. Nothing is allocated per frame. Each emitter has its own seed: the same particles on
    /// every machine.
    /// </summary>
    public static class WorldParticles
    {
        struct Particle
        {
            public Vector3 Pos, Vel;    // Pos: in the world, or (not World) from the emitter's position
            public float Age, Life;
        }

        sealed class Pool
        {
            public Particle[] Items = Array.Empty<Particle>();
            public int Count, Pending, Seen;
            public float Carry;
            public uint Seed;
            public ParticlesComponent C;
            public Vector3 Origin;
            public float Depth;
        }

        private static readonly Dictionary<Entity, Pool> _pools = new();
        private static readonly List<Entity> _gone = new();
        private static Pool[] _sorted = new Pool[16];
        private static int _frame;
        private static sg_image _dotImage;
        private static sg_view _dot;

        /// <summary>The particles alive now, all emitters together.</summary>
        public static int Alive
        {
            get { int n = 0; foreach (Pool p in _pools.Values) n += p.Count; return n; }
        }

        /// <summary>The particles alive now of one emitter.</summary>
        public static int AliveOf(Entity entity) => _pools.TryGetValue(entity, out Pool? p) ? p.Count : 0;

        /// <summary>Where one emitter's particles are now, in the world: as many as fit into <paramref name="into"/>; returns how many.</summary>
        public static int CopyPositions(Entity entity, Span<Vector3> into)
        {
            if (!_pools.TryGetValue(entity, out Pool? p)) return 0;
            int n = Math.Min(p.Count, into.Length);
            for (int i = 0; i < n; i++) into[i] = p.C.World ? p.Items[i].Pos : p.Origin + p.Items[i].Pos;
            return n;
        }

        /// <summary>Makes the emitter's <see cref="ParticlesComponent.Burst"/> particles at once (in the next step).</summary>
        public static void Burst(Entity entity)
        {
            if (!ECSWorld.Instance.TryGetComponent<ParticlesComponent>(entity, out var c)) return;
            Get(entity, in c).Pending += Math.Max(0, c.Burst);
        }

        /// <summary>Every pool emptied (the scene stops or changes).</summary>
        public static void Clear() => _pools.Clear();

        private static Pool Get(Entity e, in ParticlesComponent c)
        {
            if (!_pools.TryGetValue(e, out Pool? pool))
            {
                pool = new Pool { Seed = 0x9E3779B9u ^ (uint)e.GetHashCode() * 2654435761u };
                if (pool.Seed == 0) pool.Seed = 1;
                if (c.Emitting) pool.Pending = Math.Max(0, c.Burst);
                _pools[e] = pool;
            }
            int max = Math.Clamp(c.Max, 1, 4096);
            if (pool.Items.Length != max)
            {
                Array.Resize(ref pool.Items, max);
                pool.Count = Math.Min(pool.Count, max);
            }
            return pool;
        }

        // xorshift: 0..1
        private static float Next(ref uint s)
        {
            s ^= s << 13; s ^= s >> 17; s ^= s << 5;
            return (s & 0xFFFFFF) / 16777216f;
        }

        /// <summary>One frame of the particles: <paramref name="dt"/> is the game's time (TimeScale 0 holds them).</summary>
        public static void Step(ECSWorld world, float dt)
        {
            _frame++;
            if (!world.HasAnyComponent<ParticlesComponent>())
            {
                if (_pools.Count > 0) _pools.Clear();
                return;
            }
            foreach (var row in world.Query<ActiveFlag, ParticlesComponent, Transform>().EnumerateWithEntities<ActiveFlag, ParticlesComponent, Transform>())
            {
                Entity e = row.Entity;
                ref readonly var c = ref row.Item2.Value;
                Pool pool = Get(e, in c);
                pool.Seen = _frame;
                pool.C = c;
                if (!row.Item1.Value.Active) { pool.Count = 0; continue; }
                Matrix4x4 model = Transform.GetWorldMatrix(world, row.Item3.Value);
                pool.Origin = model.Translation;
                if (dt <= 0f) continue;

                // Age and move.
                float keep = MathF.Max(0f, 1f - c.Drag * dt);
                for (int i = 0; i < pool.Count;)
                {
                    ref Particle p = ref pool.Items[i];
                    p.Age += dt;
                    if (p.Age >= p.Life) { pool.Items[i] = pool.Items[--pool.Count]; continue; }
                    p.Vel.Y -= c.Gravity * dt;
                    p.Vel *= keep;
                    p.Pos += p.Vel * dt;
                    i++;
                }

                // Make.
                int make = pool.Pending;
                pool.Pending = 0;
                if (c.Emitting && c.Rate > 0f)
                {
                    pool.Carry += c.Rate * dt;
                    int whole = (int)pool.Carry;
                    make += whole;
                    pool.Carry -= whole;
                }
                if (make == 0) continue;
                Vector3 axis = Vector3.TransformNormal(c.Direction.LengthSquared() > 1e-8f ? c.Direction : Vector3.UnitY, model);
                axis = axis.LengthSquared() > 1e-12f ? Vector3.Normalize(axis) : Vector3.UnitY;
                Vector3 a = MathF.Abs(axis.Y) < 0.99f ? Vector3.UnitY : Vector3.UnitX;
                Vector3 u = Vector3.Normalize(Vector3.Cross(axis, a)), v = Vector3.Cross(axis, u);
                float cosHalf = MathF.Cos(Math.Clamp(c.Spread, 0f, 360f) * 0.5f * MathF.PI / 180f);
                for (int n = 0; n < make && pool.Count < pool.Items.Length; n++)
                {
                    // A direction in the cone (uniform over its cap), a speed and a life between their bounds.
                    float cos = 1f - Next(ref pool.Seed) * (1f - cosHalf);
                    float sin = MathF.Sqrt(MathF.Max(0f, 1f - cos * cos));
                    float phi = Next(ref pool.Seed) * MathF.Tau;
                    Vector3 dir = axis * cos + (u * MathF.Cos(phi) + v * MathF.Sin(phi)) * sin;
                    float speed = c.Speed.X + (c.Speed.Y - c.Speed.X) * Next(ref pool.Seed);
                    float life = c.Life.X + (c.Life.Y - c.Life.X) * Next(ref pool.Seed);
                    if (life <= 0f) continue;
                    pool.Items[pool.Count++] = new Particle { Pos = c.World ? pool.Origin : Vector3.Zero, Vel = dir * speed, Life = life };
                }
            }
            // An emitter gone from the scene takes its particles with it.
            _gone.Clear();
            foreach (var pair in _pools) if (pair.Value.Seen != _frame) _gone.Add(pair.Key);
            foreach (Entity e in _gone) _pools.Remove(e);
        }

        /// <summary>The particles into the open batch, after the sprites: quads facing the camera of <paramref name="viewProj"/>.</summary>
        internal static void Draw(SpriteBatch batch, in Matrix4x4 viewProj)
        {
            if (_pools.Count == 0) return;
            // The camera's right and up in the world: the first two columns of the view-projection (no skew in either kind of camera).
            Vector3 right = new(viewProj.M11, viewProj.M21, viewProj.M31), up = new(viewProj.M12, viewProj.M22, viewProj.M32);
            if (right.LengthSquared() < 1e-12f || up.LengthSquared() < 1e-12f) return;
            right = Vector3.Normalize(right); up = Vector3.Normalize(up);

            // The emitters back to front (by the depth of their position).
            int n = 0;
            foreach (Pool pool in _pools.Values)
            {
                if (pool.Count == 0) continue;
                Vector4 clip = Vector4.Transform(new Vector4(pool.Origin, 1f), viewProj);
                pool.Depth = clip.W != 0f ? clip.Z / clip.W : clip.Z;
                if (n == _sorted.Length) Array.Resize(ref _sorted, n * 2);
                _sorted[n++] = pool;
            }
            for (int i = 1; i < n; i++)
            {
                Pool x = _sorted[i];
                int j = i - 1;
                while (j >= 0 && _sorted[j].Depth < x.Depth) { _sorted[j + 1] = _sorted[j]; j--; }
                _sorted[j + 1] = x;
            }

            for (int k = 0; k < n; k++)
            {
                Pool pool = _sorted[k];
                ref readonly ParticlesComponent c = ref pool.C;
                sg_view view;
                bool nearest = false;
                Vector4 uv = new(0f, 1f, 1f, 0f);
                if (string.IsNullOrEmpty(c.Texture)) view = Dot();
                else
                {
                    if (SpriteLibrary.Find(c.Texture, out SpriteFrame f) != SpriteLibrary.Answer.Ready) continue;
                    view = f.View; nearest = f.Nearest;
                    uv = new Vector4(f.Uv.X, f.Uv.W, f.Uv.Z, f.Uv.Y);
                }
                for (int i = 0; i < pool.Count; i++)
                {
                    ref readonly Particle p = ref pool.Items[i];
                    float t = p.Age / p.Life;
                    float size = c.Size.X + (c.Size.Y - c.Size.X) * t;
                    if (size <= 0f) continue;
                    Vector4 col = Vector4.Lerp(c.ColorStart, c.ColorEnd, t);
                    if (col.W <= 0f) continue;
                    Vector3 centre = c.World ? p.Pos : pool.Origin + p.Pos;
                    Vector3 ax = right * size, ay = up * size;
                    batch.Draw(view, nearest, centre - (ax + ay) * 0.5f, ax, ay, uv, Pack(col));
                }
            }
        }

        private static uint Pack(Vector4 c)
        {
            static uint B(float v) => (uint)(Math.Clamp(v, 0f, 1f) * 255f + 0.5f);
            return B(c.X) | (B(c.Y) << 8) | (B(c.Z) << 16) | (B(c.W) << 24);
        }

        // A soft round dot, white, 32 x 32: the texture of an emitter that names none. Made once, at the first draw.
        private static unsafe sg_view Dot()
        {
            if (_dot.id != 0) return _dot;
            const int N = 32;
            byte* px = stackalloc byte[N * N * 4];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                    float a = Math.Clamp(1f - MathF.Sqrt(dx * dx + dy * dy), 0f, 1f);
                    a = a * a * (3f - 2f * a);                                  // smooth to the edge
                    int i = (y * N + x) * 4;
                    px[i] = 255; px[i + 1] = 255; px[i + 2] = 255; px[i + 3] = (byte)(a * 255f + 0.5f);
                }
            var desc = new sg_image_desc { width = N, height = N, pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA8, label = "particles-dot" };
            desc.data.mip_levels[0] = new sg_range { ptr = px, size = N * N * 4 };
            _dotImage = sg_make_image(desc);
            _dot = sg_make_view(new sg_view_desc { texture = new sg_texture_view_desc { image = _dotImage }, label = "particles-dot" });
            return _dot;
        }

        /// <summary>The dot's texture given back (the renderer shuts down).</summary>
        internal static void Shutdown()
        {
            if (_dot.id != 0) sg_destroy_view(_dot);
            if (_dotImage.id != 0) sg_destroy_image(_dotImage);
            _dot = default; _dotImage = default;
            _pools.Clear();
        }
    }
}
