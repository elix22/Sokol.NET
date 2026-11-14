using System;
using System.Collections.Generic;
using System.Numerics;
using Frent;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;

namespace GameEditor.Framework.Scene
{
    /// <summary>
    /// The particle systems of the world (examples/GameEditor/docs/PARTICLES_3D_DESIGN.md; H5_DESIGN.md §16 before it): one
    /// pool for each entity with a <see cref="ParticlesComponent"/>, made the first time it is seen and kept; <see cref="Step"/>
    /// runs each system's time (its loops, its start delay, its rate and bursts), makes particles from its shape, ages and moves
    /// them by its modules; <see cref="Draw"/> (WorldParticles.Draw.cs) draws them. Nothing is allocated per frame. Each emitter
    /// has its own seed: the same particles on every machine.
    /// </summary>
    public static partial class WorldParticles
    {
        struct Particle
        {
            public Vector3 Pos, Vel;    // in the world, or (not World) in the emitter's own frame (its position and rotation)
            public float Age, Life;
            public float Size;          // Start Size
            public float Rot, Spin;     // degrees, degrees a second
            public Vector4 Color;       // Start Color
            public float Frame;         // the Texture Sheet's start frame
        }

        sealed class Pool
        {
            public Particle[] Items = Array.Empty<Particle>();
            public int Count, Pending, Seen;
            public float Carry, DistanceCarry;
            public uint Seed, NoiseSeed;
            public ParticlesComponent C;
            public Vector3 Origin, Last;
            public Quaternion Rotation = Quaternion.Identity;
            public bool HasLast;
            public float Depth;
            /// <summary>Seconds the system has run since Play (its own time: times the Simulation Speed).</summary>
            public float Time;
            /// <summary>Its time runs and, while in its loop, it makes particles. False once a system that does not loop has
            /// run its Duration, or after Stop.</summary>
            public bool Playing;
            public bool Paused;
            public bool NeedsPrewarm;
            public float[] SortKeys = Array.Empty<float>();
            public int[] SortOrder = Array.Empty<int>();
        }

        private static readonly Dictionary<Entity, Pool> _pools = new();
        private static readonly List<Entity> _gone = new();
        private static int _frame;

        /// <summary>The particles alive now, all emitters together.</summary>
        public static int Alive
        {
            get { int n = 0; foreach (Pool p in _pools.Values) n += p.Count; return n; }
        }

        /// <summary>The particles alive now of one emitter (the reference editor's particleCount).</summary>
        public static int AliveOf(Entity entity) => _pools.TryGetValue(entity, out Pool? p) ? p.Count : 0;

        /// <summary>Where one emitter's particles are now, in the world: as many as fit into <paramref name="into"/>; returns how many.</summary>
        public static int CopyPositions(Entity entity, Span<Vector3> into)
        {
            if (!_pools.TryGetValue(entity, out Pool? p)) return 0;
            int n = Math.Min(p.Count, into.Length);
            for (int i = 0; i < n; i++) into[i] = WorldPos(p, in p.Items[i]);
            return n;
        }

        /// <summary>How old each of one emitter's particles is, seconds (in the order of <see cref="CopyPositions"/>).</summary>
        public static int CopyAges(Entity entity, Span<float> into)
        {
            if (!_pools.TryGetValue(entity, out Pool? p)) return 0;
            int n = Math.Min(p.Count, into.Length);
            for (int i = 0; i < n; i++) into[i] = p.Items[i].Age;
            return n;
        }

        /// <summary>Makes the emitter's <see cref="ParticlesComponent.Burst"/> particles at once (in the next step).</summary>
        public static void Burst(Entity entity)
        {
            if (!ECSWorld.Instance.TryGetComponent<ParticlesComponent>(entity, out var c)) return;
            Get(entity, in c).Pending += Math.Max(0, c.Burst);
        }

        /// <summary>Makes <paramref name="count"/> particles at once (in the next step), playing or not (the reference editor's Emit).</summary>
        public static void Emit(Entity entity, int count)
        {
            if (count <= 0 || !ECSWorld.Instance.TryGetComponent<ParticlesComponent>(entity, out var c)) return;
            Get(entity, in c).Pending += count;
        }

        /// <summary>Plays the system: a paused one goes on; a stopped one starts again from its beginning (its living particles kept).</summary>
        public static void Play(Entity entity)
        {
            if (!ECSWorld.Instance.TryGetComponent<ParticlesComponent>(entity, out var c)) return;
            Pool p = Get(entity, in c);
            if (p.Paused) { p.Paused = false; if (p.Playing) return; }
            if (p.Playing) return;
            Begin(p, in c);
        }

        /// <summary>Holds the system as it is: its particles neither age nor move, none are made, until Play.</summary>
        public static void Pause(Entity entity)
        {
            if (_pools.TryGetValue(entity, out Pool? p)) p.Paused = true;
        }

        /// <summary>Stops making particles; the living ones live on (<paramref name="clear"/>: they go at once).</summary>
        public static void Stop(Entity entity, bool clear = false)
        {
            if (!_pools.TryGetValue(entity, out Pool? p)) return;
            p.Playing = false; p.Paused = false; p.Pending = 0;
            if (clear) p.Count = 0;
        }

        /// <summary>The system from its beginning, its particles gone (the editor's Restart).</summary>
        public static void Restart(Entity entity)
        {
            if (!ECSWorld.Instance.TryGetComponent<ParticlesComponent>(entity, out var c)) return;
            Pool p = Get(entity, in c);
            p.Count = 0; p.Paused = false;
            Begin(p, in c);
        }

        /// <summary>One emitter's particles taken away; it goes on as it was.</summary>
        public static void Clear(Entity entity)
        {
            if (_pools.TryGetValue(entity, out Pool? p)) { p.Count = 0; p.Pending = 0; }
        }

        /// <summary>It is playing, or particles of it are still alive.</summary>
        public static bool IsAlive(Entity entity) => _pools.TryGetValue(entity, out Pool? p) && (p.Playing || p.Count > 0);

        public static bool IsPlaying(Entity entity) => _pools.TryGetValue(entity, out Pool? p) && p.Playing && !p.Paused;

        public static bool IsPaused(Entity entity) => _pools.TryGetValue(entity, out Pool? p) && p.Paused;

        /// <summary>Seconds the system has run since it started playing (the editor's Playback Time).</summary>
        public static float TimeOf(Entity entity) => _pools.TryGetValue(entity, out Pool? p) ? p.Time : 0f;

        /// <summary>Every pool emptied (the scene stops or changes).</summary>
        public static void Clear() => _pools.Clear();

        private static void Begin(Pool p, in ParticlesComponent c)
        {
            p.Playing = true;
            p.Time = 0f;
            p.Carry = 0f; p.DistanceCarry = 0f; p.HasLast = false;
            p.NeedsPrewarm = c.Prewarm && c.Looping;
        }

        private static Pool Get(Entity e, in ParticlesComponent c)
        {
            if (!_pools.TryGetValue(e, out Pool? pool))
            {
                pool = new Pool { Seed = c.AutoRandomSeed ? 0x9E3779B9u ^ (uint)e.GetHashCode() * 2654435761u : c.RandomSeed };
                if (pool.Seed == 0) pool.Seed = 1;
                pool.NoiseSeed = pool.Seed * 747796405u + 2891336453u;
                if (c.PlayOnAwake) Begin(pool, in c);
                _pools[e] = pool;
            }
            int max = Math.Clamp(c.Max, 1, 4096);
            if (pool.Items.Length != max)
            {
                Array.Resize(ref pool.Items, max);
                Array.Resize(ref pool.SortKeys, max);
                Array.Resize(ref pool.SortOrder, max);
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

        private static Vector3 WorldPos(Pool p, in Particle q) => p.C.World ? q.Pos : p.Origin + Vector3.Transform(q.Pos, p.Rotation);

        /// <summary>One frame of the particles: <paramref name="dt"/> is the game's time (TimeScale 0 holds them).</summary>
        public static void Step(ECSWorld world, float dt) => StepSome(world, dt, default, false);

        /// <summary>
        /// The editor's preview (a selected emitter plays in the Scene view without Play): one frame of only the emitters of
        /// <paramref name="root"/> and of the entities under it; every other pool is taken away.
        /// </summary>
        public static void StepPreview(ECSWorld world, float dt, Entity root) => StepSome(world, dt, root, true);

        private static void StepSome(ECSWorld world, float dt, Entity root, bool onlyUnder)
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
                if (onlyUnder && !IsUnder(world, e, root)) continue;
                ref readonly var c = ref row.Item2.Value;
                Pool pool = Get(e, in c);
                pool.Seen = _frame;
                pool.C = c;
                if (!row.Item1.Value.Active) { pool.Count = 0; continue; }
                Matrix4x4 model = Transform.GetWorldMatrix(world, row.Item3.Value);
                pool.Origin = model.Translation;
                pool.Rotation = Matrix4x4.Decompose(model, out _, out Quaternion r, out _) && float.IsFinite(r.W) ? Quaternion.Normalize(r) : Quaternion.Identity;
                if (dt <= 0f || pool.Paused) continue;
                float run = dt * MathF.Max(0f, c.SimulationSpeed);
                if (run <= 0f) continue;
                if (pool.NeedsPrewarm)
                {
                    // A looping system starts as if it had run one loop: that loop, in steps of a thirtieth of a second.
                    pool.NeedsPrewarm = false;
                    pool.HasLast = true; pool.Last = pool.Origin;
                    int steps = (int)MathF.Ceiling(MathF.Max(0.05f, c.Duration) * 30f);
                    for (int i = 0; i < steps; i++) Simulate(pool, in c, in model, 1f / 30f);
                }
                Simulate(pool, in c, in model, run);
            }
            // An emitter gone from the scene takes its particles with it.
            _gone.Clear();
            foreach (var pair in _pools) if (pair.Value.Seen != _frame) _gone.Add(pair.Key);
            foreach (Entity e in _gone) _pools.Remove(e);
        }

        private static bool IsUnder(ECSWorld world, Entity e, Entity root)
        {
            for (int depth = 0; depth < 64; depth++)
            {
                if (e == root) return true;
                if (!world.TryGetComponent<Transform>(e, out var t) || t.Parent is not { } parent) return false;
                e = parent;
            }
            return false;
        }

        private static void Simulate(Pool pool, in ParticlesComponent c, in Matrix4x4 model, float dt)
        {
            Quaternion toLocal = Quaternion.Conjugate(pool.Rotation);
            Vector3 down = new(0f, -c.Gravity, 0f);
            if (!c.World) down = Vector3.Transform(down, toLocal);
            Vector3 force = c.ForceOn ? Space(c.Force, c.ForceWorld, c.World, pool.Rotation, toLocal) : Vector3.Zero;
            Vector3 velocity = c.VelocityOn ? Space(c.Velocity, c.VelocityWorld, c.World, pool.Rotation, toLocal) : Vector3.Zero;
            float keep = c.LimitOn ? MathF.Max(0f, 1f - c.Drag * dt) : 1f;
            float dampen = c.LimitOn && c.Dampen > 0f ? 1f - MathF.Pow(1f - Math.Clamp(c.Dampen, 0f, 1f), dt * 30f) : 0f;
            float limit = MathF.Max(0f, c.SpeedLimit);
            bool spin = c.RotationOn;
            float scroll = pool.Time * c.NoiseScroll;

            // Age and move.
            for (int i = 0; i < pool.Count;)
            {
                ref Particle p = ref pool.Items[i];
                p.Age += dt;
                if (p.Age >= p.Life) { pool.Items[i] = pool.Items[--pool.Count]; continue; }
                if (c.Gravity != 0f) p.Vel += down * dt;
                if (c.ForceOn) p.Vel += force * dt;
                if (c.LimitOn)
                {
                    p.Vel *= keep;
                    if (dampen > 0f)
                    {
                        float speed = p.Vel.Length();
                        if (speed > limit && speed > 1e-6f) p.Vel *= (speed - (speed - limit) * dampen) / speed;
                    }
                }
                p.Pos += p.Vel * dt;
                if (c.VelocityOn) p.Pos += velocity * dt;
                if (c.NoiseOn)
                {
                    Vector3 at = (c.World ? p.Pos : pool.Origin + Vector3.Transform(p.Pos, pool.Rotation)) * c.NoiseFrequency + new Vector3(scroll);
                    p.Pos += Noise3(at, pool.NoiseSeed) * (c.NoiseStrength * dt);
                }
                if (spin) p.Rot += p.Spin * dt;
                i++;
            }

            // The system's time: its loops, its start delay; what it makes in this step.
            float duration = MathF.Max(0.05f, c.Duration);
            float before = pool.Time - c.StartDelay;
            pool.Time += dt;
            float now = pool.Time - c.StartDelay;
            Vector3 moved = pool.HasLast ? pool.Origin - pool.Last : Vector3.Zero;
            pool.HasLast = true; pool.Last = pool.Origin;
            if (pool.Playing && now > 0f && (c.Looping || before < duration))
            {
                if (c.Emitting)
                {
                    // Rate over Time over the part of the step inside the loop (all of it, looping: exactly dt).
                    float span = c.Looping && before >= 0f ? dt : MathF.Min(now, c.Looping ? now : duration) - MathF.Max(before, 0f);
                    if (c.Rate > 0f && span > 0f)
                    {
                        pool.Carry += c.Rate * span;
                        int whole = (int)pool.Carry;
                        pool.Pending += whole;
                        pool.Carry -= whole;
                    }
                    if (c.RateOverDistance > 0f)
                    {
                        pool.DistanceCarry += c.RateOverDistance * moved.Length();
                        int whole = (int)pool.DistanceCarry;
                        pool.Pending += whole;
                        pool.DistanceCarry -= whole;
                    }
                    if (c.Bursts.Count > 0) FireBursts(pool, in c, MathF.Max(before, 0f), now, duration);
                }
                if (!c.Looping && now >= duration) pool.Playing = false;
            }

            int make = pool.Pending;
            pool.Pending = 0;
            if (make > 0) Make(pool, in c, in model, make);
        }

        // A vector given in the emitter's space or the world's, into the space the particles live in.
        private static Vector3 Space(Vector3 v, bool givenInWorld, bool simInWorld, Quaternion toWorld, Quaternion toLocal)
        {
            if (givenInWorld == simInWorld) return v;
            return simInWorld ? Vector3.Transform(v, toWorld) : Vector3.Transform(v, toLocal);
        }

        // The bursts whose times fall in [from, to) of the system's emitting time, loop by loop.
        private static void FireBursts(Pool pool, in ParticlesComponent c, float from, float to, float duration)
        {
            int l0 = c.Looping ? (int)MathF.Floor(from / duration) : 0, l1 = c.Looping ? (int)MathF.Floor(to / duration) : 0;
            for (int loop = l0; loop <= l1; loop++)
            {
                float start = loop * duration;
                for (int b = 0; b < c.Bursts.Count; b++)
                {
                    ParticleBurst burst = c.Bursts.Items[b];
                    int cycles = burst.Cycles <= 0 ? int.MaxValue : burst.Cycles;
                    float interval = MathF.Max(0.001f, burst.Interval);
                    int k = Math.Max(0, (int)MathF.Floor((from - start - burst.Time) / interval));
                    for (; k < cycles; k++)
                    {
                        float t = burst.Time + k * interval;
                        if (t >= duration) break;
                        float at = start + t;
                        if (at >= to) break;
                        if (at < from) continue;
                        if (burst.Probability < 1f && Next(ref pool.Seed) >= burst.Probability) continue;
                        pool.Pending += Math.Max(0, burst.Count);
                    }
                }
            }
        }

        private static void Make(Pool pool, in ParticlesComponent c, in Matrix4x4 model, int make)
        {
            // The shape's axis in the world, and two across it (the axis as the entity's matrix turns and scales it: the
            // shape is scaled with the entity, the particles are not).
            Vector3 dirL = c.Direction.LengthSquared() > 1e-8f ? Vector3.Normalize(c.Direction) : Vector3.UnitY;
            Vector3 axis = Vector3.TransformNormal(c.Direction.LengthSquared() > 1e-8f ? c.Direction : Vector3.UnitY, model);
            axis = axis.LengthSquared() > 1e-12f ? Vector3.Normalize(axis) : Vector3.UnitY;
            Vector3 a = MathF.Abs(axis.Y) < 0.99f ? Vector3.UnitY : Vector3.UnitX;
            Vector3 u = Vector3.Normalize(Vector3.Cross(axis, a)), v = Vector3.Cross(axis, u);
            // The frame the shape's points are given in, in the entity's own space: the shortest turn from up to the axis
            // (the axis up: the entity's own x, y, z).
            Quaternion turn = TurnFromUp(dirL);
            Vector3 uL = Vector3.Transform(Vector3.UnitX, turn), vL = Vector3.Transform(Vector3.UnitZ, turn);
            const float Deg = MathF.PI / 180f;
            float halfAngle = Math.Clamp(c.Spread, 0f, 360f) * 0.5f * Deg;
            float cosHalf = MathF.Cos(halfAngle);
            float arc = c.Arc >= 360f ? MathF.Tau : Math.Clamp(c.Arc, 0f, 360f) * Deg;
            float thick = Math.Clamp(c.RadiusThickness, 0f, 1f);
            float radius = MathF.Max(0f, c.Radius);
            bool atOrigin = c.ShapePosition == Vector3.Zero;
            Vector3 centre = atOrigin ? pool.Origin : Vector3.Transform(c.ShapePosition, model);
            Quaternion toLocal = Quaternion.Conjugate(pool.Rotation);

            for (int n = 0; n < make && pool.Count < pool.Items.Length; n++)
            {
                Vector3 pos = centre, dir = axis;
                if (c.ShapeOn)
                {
                    if (c.Shape == ParticleShape.Cone && radius <= 0f && c.ConeFrom == ParticleConeFrom.Base)
                    {
                        // A point: a direction in the cone, uniform over its cap (as before G8.7, the same randoms in the same order).
                        float cos = 1f - Next(ref pool.Seed) * (1f - cosHalf);
                        float sin = MathF.Sqrt(MathF.Max(0f, 1f - cos * cos));
                        float phi = Next(ref pool.Seed) * arc;
                        dir = axis * cos + (u * MathF.Cos(phi) + v * MathF.Sin(phi)) * sin;
                    }
                    else ShapePoint(ref pool.Seed, in c, in model, dirL, uL, vL, radius, thick, arc, halfAngle, out pos, out dir);
                }
                if (c.RandomizeDirection > 0f) dir = Vector3.Normalize(Vector3.Lerp(dir, RandomUnit(ref pool.Seed), Math.Clamp(c.RandomizeDirection, 0f, 1f)) + new Vector3(1e-7f));
                if (c.SpherizeDirection > 0f && Vector3.DistanceSquared(pos, centre) > 1e-10f)
                    dir = Vector3.Normalize(Vector3.Lerp(dir, Vector3.Normalize(pos - centre), Math.Clamp(c.SpherizeDirection, 0f, 1f)) + new Vector3(1e-7f));
                float speed = c.Speed.X + (c.Speed.Y - c.Speed.X) * Next(ref pool.Seed);
                float life = c.Life.X + (c.Life.Y - c.Life.X) * Next(ref pool.Seed);
                if (life <= 0f) continue;
                // The new start values draw a random only where they are a range (an emitter of before draws none).
                var q = new Particle
                {
                    Vel = dir * speed, Life = life,
                    Size = Range(ref pool.Seed, c.StartSize),
                    Rot = Range(ref pool.Seed, c.StartRotation),
                    Color = c.StartColor == c.StartColor2 ? c.StartColor : Vector4.Lerp(c.StartColor, c.StartColor2, Next(ref pool.Seed)),
                    Spin = c.RotationOn ? Range(ref pool.Seed, c.AngularVelocity) : 0f,
                    Frame = c.SheetOn ? MathF.Floor(Range(ref pool.Seed, c.SheetStartFrame)) : 0f,
                };
                if (c.World) q.Pos = pos;
                else
                {
                    q.Pos = pos == pool.Origin ? Vector3.Zero : Vector3.Transform(pos - pool.Origin, toLocal);
                    q.Vel = Vector3.Transform(q.Vel, toLocal);
                }
                pool.Items[pool.Count++] = q;
            }
        }

        private static Quaternion TurnFromUp(Vector3 to)
        {
            float d = Vector3.Dot(Vector3.UnitY, to);
            if (d > 0.999999f) return Quaternion.Identity;
            if (d < -0.999999f) return Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI);
            Vector3 axis = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, to));
            return Quaternion.CreateFromAxisAngle(axis, MathF.Acos(Math.Clamp(d, -1f, 1f)));
        }

        private static float Range(ref uint seed, Vector2 r) => r.X == r.Y ? r.X : r.X + (r.Y - r.X) * Next(ref seed);

        private static Vector3 RandomUnit(ref uint seed)
        {
            float z = 1f - 2f * Next(ref seed), phi = Next(ref seed) * MathF.Tau;
            float s = MathF.Sqrt(MathF.Max(0f, 1f - z * z));
            return new Vector3(s * MathF.Cos(phi), z, s * MathF.Sin(phi));
        }

        // A point of the shape and the way a particle made there leaves, in the world. The shape's frame: x along uL, y along
        // the axis, z along vL (all in the entity's space), scaled by ShapeScale, placed at ShapePosition, then the entity's matrix.
        private static void ShapePoint(ref uint seed, in ParticlesComponent c, in Matrix4x4 model, Vector3 aL, Vector3 uL, Vector3 vL,
                                       float radius, float thick, float arc, float halfAngle, out Vector3 pos, out Vector3 dir)
        {
            Vector3 f, d;      // in the shape's frame
            switch (c.Shape)
            {
                case ParticleShape.Sphere:
                case ParticleShape.Hemisphere:
                {
                    d = RandomUnit(ref seed);
                    if (c.Shape == ParticleShape.Hemisphere) d.Y = MathF.Abs(d.Y);
                    float inner = 1f - thick;
                    float r = radius * MathF.Cbrt(inner * inner * inner + (1f - inner * inner * inner) * Next(ref seed));
                    f = d * r;
                    break;
                }
                case ParticleShape.Box:
                {
                    float x = Next(ref seed) - 0.5f, y = Next(ref seed) - 0.5f, z = Next(ref seed) - 0.5f;
                    if (c.BoxFrom != ParticleBoxFrom.Volume)
                    {
                        // Shell: one coordinate pushed to its face; Edge: two.
                        int k = (int)(Next(ref seed) * 3f) % 3;
                        float side = Next(ref seed) < 0.5f ? -0.5f : 0.5f;
                        if (k == 0) x = side; else if (k == 1) y = side; else z = side;
                        if (c.BoxFrom == ParticleBoxFrom.Edge)
                        {
                            float side2 = Next(ref seed) < 0.5f ? -0.5f : 0.5f;
                            if (k == 0) y = side2; else if (k == 1) z = side2; else x = side2;
                        }
                    }
                    f = new Vector3(x, y, z);
                    d = Vector3.UnitY;
                    break;
                }
                case ParticleShape.Circle:
                {
                    float inner = 1f - thick;
                    float r = radius * MathF.Sqrt(inner * inner + (1f - inner * inner) * Next(ref seed));
                    float phi = Next(ref seed) * arc;
                    d = new Vector3(MathF.Cos(phi), 0f, MathF.Sin(phi));
                    f = d * r;
                    break;
                }
                case ParticleShape.Edge:
                    f = new Vector3((Next(ref seed) * 2f - 1f) * radius, 0f, 0f);
                    d = Vector3.UnitY;
                    break;
                default:    // a Cone with a radius, or from its volume
                {
                    float inner = 1f - thick;
                    float r = radius * MathF.Sqrt(inner * inner + (1f - inner * inner) * Next(ref seed));
                    float phi = Next(ref seed) * arc;
                    Vector3 out_ = new(MathF.Cos(phi), 0f, MathF.Sin(phi));
                    float tilt = radius > 0f ? halfAngle * (r / radius) : halfAngle * Next(ref seed);
                    d = Vector3.UnitY * MathF.Cos(tilt) + out_ * MathF.Sin(tilt);
                    f = out_ * r;
                    if (c.ConeFrom == ParticleConeFrom.Volume) f += d * (MathF.Max(0f, c.Length) * Next(ref seed));
                    break;
                }
            }
            f *= c.ShapeScale;
            Vector3 local = c.ShapePosition + uL * f.X + aL * f.Y + vL * f.Z;
            pos = Vector3.Transform(local, model);
            Vector3 dl = uL * d.X + aL * d.Y + vL * d.Z;
            dir = Vector3.TransformNormal(dl, model);
            dir = dir.LengthSquared() > 1e-12f ? Vector3.Normalize(dir) : aL;
        }

        // A smooth noise of three channels in -1..1, seeded (value noise, a smoothstep between the corners of a unit grid).
        private static Vector3 Noise3(Vector3 p, uint seed) =>
            new(Noise1(p, seed), Noise1(p + new Vector3(31.4f, 17.7f, 5.3f), seed ^ 0x68E31DA4u), Noise1(p + new Vector3(-11.3f, 47.1f, 23.9f), seed ^ 0xB5297A4Du));

        private static float Noise1(Vector3 p, uint seed)
        {
            float fx = MathF.Floor(p.X), fy = MathF.Floor(p.Y), fz = MathF.Floor(p.Z);
            int x = (int)fx, y = (int)fy, z = (int)fz;
            float tx = p.X - fx, ty = p.Y - fy, tz = p.Z - fz;
            tx = tx * tx * (3f - 2f * tx); ty = ty * ty * (3f - 2f * ty); tz = tz * tz * (3f - 2f * tz);
            float c000 = Hash(x, y, z, seed), c100 = Hash(x + 1, y, z, seed), c010 = Hash(x, y + 1, z, seed), c110 = Hash(x + 1, y + 1, z, seed);
            float c001 = Hash(x, y, z + 1, seed), c101 = Hash(x + 1, y, z + 1, seed), c011 = Hash(x, y + 1, z + 1, seed), c111 = Hash(x + 1, y + 1, z + 1, seed);
            float a = c000 + (c100 - c000) * tx, b = c010 + (c110 - c010) * tx, cc = c001 + (c101 - c001) * tx, dd = c011 + (c111 - c011) * tx;
            float e = a + (b - a) * ty, g = cc + (dd - cc) * ty;
            return e + (g - e) * tz;
        }

        private static float Hash(int x, int y, int z, uint seed)
        {
            uint h = (uint)x * 0x8DA6B343u ^ (uint)y * 0xD8163841u ^ (uint)z * 0xCB1AB31Fu ^ seed;
            h ^= h >> 13; h *= 0x5BD1E995u; h ^= h >> 15;
            return (h & 0xFFFFFF) / 8388608f - 1f;
        }
    }
}
