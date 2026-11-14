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
            public Vector3 Tested;      // where it was when last tested against the world (Collision Quality Medium / Low)
            public float SubCarry;      // its Birth sub-emitters' part of a particle not yet made
            public int Trail;           // its trail's slot + 1 (0: none)
            public bool Lit;            // it carries a light (the Lights module's Ratio)
            public byte Inside;         // the triggers it was inside at the last step (a bit each)
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
            /// <summary>A sub-emitter of an active emitter: it makes particles only when its parent asks (G8.8 Q2).</summary>
            public bool IsSub;
            /// <summary>The emitter's velocity in the world this step (Inherit Velocity).</summary>
            public Vector3 EmitterVel;
            public readonly ParticleCollisionEvent[] Hits = new ParticleCollisionEvent[64];
            public int HitCount;
            public int LightCount;      // its lights given to the renderer at the last gathering
            public readonly ParticleTriggerEvent[] Triggered = new ParticleTriggerEvent[64];
            public int TriggeredCount;
            public bool TellTrigger;    // a Callback condition met this step: the emitter's scripts are told
            // Trails (G8.8 Q3): slots of TrailPoints points each, oldest first; a slot is a living particle's or a lingering one.
            public Vector3[] TrailPos = Array.Empty<Vector3>();
            public float[] TrailAge = Array.Empty<float>();
            public int[] TrailCount = Array.Empty<int>(), FreeSlots = Array.Empty<int>(), Lingering = Array.Empty<int>();
            public float[] SlotLife = Array.Empty<float>(), SlotSize = Array.Empty<float>();
            public Vector4[] SlotColor = Array.Empty<Vector4>();
            public int FreeCount, LingerCount;
            public float[] SortKeys = Array.Empty<float>();
            public int[] SortOrder = Array.Empty<int>();
        }

        private static readonly Dictionary<Entity, Pool> _pools = new();
        /// <summary>The particles that met a Callback condition of the Triggers module in the last step (at most 64): where, the
        /// trigger, the condition.</summary>
        public static int CopyTriggered(Entity entity, Span<ParticleTriggerEvent> into)
        {
            if (!_pools.TryGetValue(entity, out Pool? p)) return 0;
            int n = Math.Min(p.TriggeredCount, into.Length);
            for (int i = 0; i < n; i++) into[i] = p.Triggered[i];
            return n;
        }

        // The wind of the step (G8.8 Q7): the active Wind Zones, at most 16, and the time their pulse and turbulence run on.
        private struct Wind { public Vector3 At, Forward; public WindZone Z; }
        private static readonly Wind[] _winds = new Wind[16];
        private static int _windCount;
        private static float _windTime;

        private static Vector3 WindAt(Vector3 p)
        {
            Vector3 f = Vector3.Zero;
            for (int k = 0; k < _windCount; k++)
            {
                ref readonly Wind w = ref _winds[k];
                float main = w.Z.Main * (1f + w.Z.PulseMagnitude * MathF.Sin(MathF.Tau * w.Z.PulseFrequency * _windTime));
                if (w.Z.Mode == WindZoneMode.Directional) f += w.Forward * main;
                else
                {
                    Vector3 d = p - w.At;
                    float len = d.Length();
                    if (len >= w.Z.Radius || len < 1e-5f) continue;
                    f += d / len * (main * (1f - len / w.Z.Radius));
                }
                if (w.Z.Turbulence > 0f) f += Noise3(p * 0.5f + new Vector3(_windTime), 0xA511E9B3u + (uint)k) * w.Z.Turbulence;
            }
            return f;
        }

        // The triggers of the emitter being stepped: their inverse world matrices (the shapes' space) and shapes.
        private struct TriggerBody { public Matrix4x4 ToLocal; public float Scale; public Entity E; public System.Collections.Generic.List<Physics.ShapeEntry>? Shapes; }
        private static readonly TriggerBody[] _triggers = new TriggerBody[ParticlePlanes.Capacity];
        private static int _triggerCount;

        private static void PrepareTriggers(ECSWorld world, in ParticlesComponent c)
        {
            _triggerCount = 0;
            if (!c.TriggersOn) return;
            for (int k = 0; k < c.Triggers.Count; k++)
            {
                Entity te = c.Triggers.Items[k];
                if (te.IsNull || !te.IsAlive || !world.TryGetComponent<Transform>(te, out var tt) || !world.TryGetComponent<RigidbodyComponent>(te, out var rb)) continue;
                Matrix4x4 m = Transform.GetWorldMatrix(world, tt);
                if (!Matrix4x4.Invert(m, out var inv)) continue;
                float scale = MathF.Cbrt(MathF.Abs(m.GetDeterminant()));
                _triggers[_triggerCount++] = new TriggerBody { ToLocal = inv, Scale = MathF.Max(1e-5f, scale), E = te, Shapes = rb.Shapes };
            }
        }

        // A point (grown by a radius, world metres) inside a trigger's shapes: in the body's own space, each shape at its offset.
        private static bool InsideTrigger(in TriggerBody t, Vector3 world, float radius)
        {
            Vector3 p = Vector3.Transform(world, t.ToLocal);
            float r = radius / t.Scale;
            if (t.Shapes == null || t.Shapes.Count == 0) return MathF.Abs(p.X) <= 0.5f + r && MathF.Abs(p.Y) <= 0.5f + r && MathF.Abs(p.Z) <= 0.5f + r;
            foreach (var s in t.Shapes)
            {
                Vector3 q = Vector3.Transform(p - s.Offset, Quaternion.Conjugate(s.OffsetRotation == default ? Quaternion.Identity : s.OffsetRotation));
                switch (s.Shape)
                {
                    case Physics.ColliderShape.Sphere:
                        if (q.Length() <= s.Radius + r) return true;
                        break;
                    case Physics.ColliderShape.Capsule:
                    {
                        float y = Math.Clamp(q.Y, -s.HalfHeight, s.HalfHeight);
                        if (Vector3.Distance(q, new Vector3(0f, y, 0f)) <= s.Radius + r) return true;
                        break;
                    }
                    case Physics.ColliderShape.Cylinder:
                        if (MathF.Abs(q.Y) <= s.HalfHeight + r && new Vector2(q.X, q.Z).Length() <= s.Radius + r) return true;
                        break;
                    default:   // Box, and a plane, hull or mesh as their box
                        if (MathF.Abs(q.X) <= s.HalfExtent.X + r && MathF.Abs(q.Y) <= s.HalfExtent.Y + r && MathF.Abs(q.Z) <= s.HalfExtent.Z + r) return true;
                        break;
                }
            }
            return false;
        }

        // One particle against the Triggers module: false - it dies (a Kill condition met).
        private static bool Trigger(Pool pool, in ParticlesComponent c, ref Particle p)
        {
            Vector3 at = WorldPos(pool, in p);
            float radius = SizeNow(in c, in p) * 0.5f * MathF.Max(0f, c.TriggerRadiusScale);
            int now = 0, first = -1;
            for (int k = 0; k < _triggerCount; k++)
                if (InsideTrigger(in _triggers[k], at, radius)) { now |= 1 << k; if (first < 0) first = k; }
            int was = p.Inside;
            p.Inside = (byte)now;
            bool kill = false;
            void Meet(Pool pool, ParticleTriggerAction action, ParticleTriggerKind kind, int k, Vector3 at, ref bool kill)
            {
                if (action == ParticleTriggerAction.Kill) kill = true;
                else if (action == ParticleTriggerAction.Callback)
                {
                    pool.TellTrigger = true;
                    if (pool.TriggeredCount < pool.Triggered.Length)
                        pool.Triggered[pool.TriggeredCount++] = new ParticleTriggerEvent { Position = at, Trigger = k >= 0 ? _triggers[k].E : default, Kind = kind };
                }
            }
            for (int k = 0; k < _triggerCount; k++)
            {
                int bit = 1 << k;
                if ((now & bit) != 0 && (was & bit) == 0) Meet(pool, c.TriggerEnter, ParticleTriggerKind.Enter, k, at, ref kill);
                if ((now & bit) == 0 && (was & bit) != 0) Meet(pool, c.TriggerExit, ParticleTriggerKind.Exit, k, at, ref kill);
            }
            if (now != 0) Meet(pool, c.TriggerInside, ParticleTriggerKind.Inside, first, at, ref kill);
            else Meet(pool, c.TriggerOutside, ParticleTriggerKind.Outside, -1, at, ref kill);
            return !kill;
        }

        /// <summary>A mesh particle for the renderer (G8.8 Q6): its model matrix and its colour (the material's tint).</summary>
        public struct MeshParticle { public Matrix4x4 Model; public Vector4 Tint; }

        /// <summary>One system's mesh particles: its mesh and material, where they start in <see cref="Meshes"/>, how many.</summary>
        public struct MeshBatch { public string Mesh, Material; public int Start, Count; public bool CastShadows; }

        public const int MaxMeshes = 4096;
        private static readonly MeshParticle[] _meshes = new MeshParticle[MaxMeshes];
        private static readonly MeshBatch[] _meshBatches = new MeshBatch[128];
        private static int _meshCount, _meshBatchCount;

        public static ReadOnlySpan<MeshParticle> Meshes => new(_meshes, 0, _meshCount);
        public static ReadOnlySpan<MeshBatch> MeshBatches => new(_meshBatches, 0, _meshBatchCount);

        /// <summary>
        /// The mesh particles now (Render Mode Mesh), for the renderer and its shadow passes: each system's particles as model
        /// matrices - scaled by their size, turned about their own Y by their rotation (after +Z along the velocity, aligned so)
        /// - and colours. Returns how many batches (<see cref="MeshBatches"/>).
        /// </summary>
        public static int GatherMeshes()
        {
            _meshCount = 0; _meshBatchCount = 0;
            const float Deg = MathF.PI / 180f;
            foreach (Pool pool in _pools.Values)
            {
                ref readonly ParticlesComponent c = ref pool.C;
                if (c.RenderMode != ParticleRenderMode.Mesh || pool.Count == 0 || string.IsNullOrEmpty(c.MeshKey) || _meshBatchCount == _meshBatches.Length) continue;
                int start = _meshCount;
                for (int i = 0; i < pool.Count && _meshCount < MaxMeshes; i++)
                {
                    ref readonly Particle q = ref pool.Items[i];
                    float size = SizeNow(in c, in q);
                    if (size <= 0f) continue;
                    Matrix4x4 m = Matrix4x4.CreateScale(size) * Matrix4x4.CreateRotationY(q.Rot * Deg);
                    if (c.MeshAlignment == ParticleMeshAlignment.Velocity)
                    {
                        Vector3 v = WorldVel(pool, in q);
                        if (v.LengthSquared() > 1e-10f)
                        {
                            Vector3 f = Vector3.Normalize(v);
                            Vector3 up = MathF.Abs(f.Y) < 0.99f ? Vector3.UnitY : Vector3.UnitX;
                            m *= Matrix4x4.CreateWorld(Vector3.Zero, -f, up);    // the world's -Z along -f: the mesh's +Z along the velocity
                        }
                    }
                    m.Translation = WorldPos(pool, in q);
                    _meshes[_meshCount++] = new MeshParticle { Model = m, Tint = ColorNow(in c, in q) };
                }
                if (_meshCount > start)
                    _meshBatches[_meshBatchCount++] = new MeshBatch { Mesh = c.MeshKey, Material = c.MeshMaterial ?? "", Start = start, Count = _meshCount - start, CastShadows = c.MeshCastShadows };
            }
            return _meshBatchCount;
        }

        /// <summary>A particle's light for the renderer (the Lights module): a point light.</summary>
        public struct ParticleLight { public Vector3 Position, Color; public float Intensity, Range; }

        /// <summary>The particles' lights at most a frame, all systems together.</summary>
        public const int MaxLights = 256;
        private static readonly ParticleLight[] _lights = new ParticleLight[MaxLights];

        /// <summary>The lights of the last <see cref="GatherLights"/>.</summary>
        public static ReadOnlySpan<ParticleLight> Lights => new(_lights, 0, _lightTotal);
        private static int _lightTotal;

        /// <summary>The lights one emitter gave the renderer at its last gathering.</summary>
        public static int LightCountOf(Entity entity) => _pools.TryGetValue(entity, out Pool? p) ? p.LightCount : 0;

        /// <summary>
        /// The particles' point lights now (G8.8 Q5), for the renderer's light gathering: each system's lit particles, at most
        /// its Maximum Lights, from its template light - colour, intensity and range, times the particle's as asked. Returns how
        /// many (<see cref="Lights"/>).
        /// </summary>
        public static int GatherLights(ECSWorld world)
        {
            _lightTotal = 0;
            foreach (Pool pool in _pools.Values)
            {
                pool.LightCount = 0;
                ref readonly ParticlesComponent c = ref pool.C;
                if (!c.LightsOn || pool.Count == 0 || c.LightTemplate.IsNull || !c.LightTemplate.IsAlive
                    || !world.TryGetComponent<LightComponent>(c.LightTemplate, out var t)) continue;
                int max = Math.Max(0, c.LightMax);
                for (int i = 0; i < pool.Count && pool.LightCount < max && _lightTotal < MaxLights; i++)
                {
                    ref readonly Particle q = ref pool.Items[i];
                    if (!q.Lit) continue;
                    Vector4 col = ColorNow(in c, in q);
                    _lights[_lightTotal++] = new ParticleLight
                    {
                        Position = WorldPos(pool, in q),
                        Color = c.LightUseParticleColor ? t.Color * new Vector3(col.X, col.Y, col.Z) : t.Color,
                        Intensity = t.Intensity * c.LightIntensityMultiplier * (c.LightAlphaAffectsIntensity ? Math.Clamp(col.W, 0f, 1f) : 1f),
                        Range = t.Range * c.LightRangeMultiplier * (c.LightSizeAffectsRange ? SizeNow(in c, in q) : 1f),
                    };
                    pool.LightCount++;
                }
            }
            return _lightTotal;
        }

        /// <summary>The points of one trail at most (a ribbon's corners).</summary>
        public const int TrailPoints = 64;
        // The collision messages of a step, each (emitter, other) once: two arrays, compared by hand (a list of tuples and its
        // Contains, a generic comparer over a tuple of structs, stopped the web build's Mono interpreter on an assertion).
        private const int MaxMessages = 256;
        private static readonly Entity[] _messageEmitter = new Entity[MaxMessages], _messageOther = new Entity[MaxMessages];
        private static int _messageCount;
        private static readonly List<Entity> _gone = new();
        private static int _frame;

        // The sub-emitters of the step (G8.8 Q2): the entities named by an active emitter's Sub Emitters, and for the emitter
        // being simulated its entries made ready (pool, component, matrix, burst count). Arrays, compared by hand.
        private const int MaxSubs = 256;
        private static readonly Entity[] _subEntities = new Entity[MaxSubs];
        private static int _subEntityCount;
        private struct SubInfo { public Pool? Pool; public ParticlesComponent C; public Matrix4x4 Model; public ParticleSubEmitter Entry; public int Burst; }
        private static readonly SubInfo[] _subs = new SubInfo[ParticleSubEmitters.Capacity];
        private static int _subCount;

        /// <summary>The particles alive now, all emitters together.</summary>
        public static int Alive
        {
            get { int n = 0; foreach (Pool p in _pools.Values) n += p.Count; return n; }
        }

        /// <summary>Something to draw: particles alive, or the trails of dead ones (Die with Particles off).</summary>
        public static bool AnyToDraw
        {
            get { foreach (Pool p in _pools.Values) if (p.Count > 0 || p.LingerCount > 0) return true; return false; }
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

        /// <summary>The colour of each of one emitter's particles now (its Start Color times Color over Lifetime), in the order of
        /// <see cref="CopyPositions"/>.</summary>
        public static int CopyColors(Entity entity, Span<Vector4> into)
        {
            if (!_pools.TryGetValue(entity, out Pool? p)) return 0;
            int n = Math.Min(p.Count, into.Length);
            for (int i = 0; i < n; i++) into[i] = ColorNow(in p.C, in p.Items[i]);
            return n;
        }

        // A particle's colour and size now: its start values times the over-lifetime modules' and the by-speed ones'.
        private static Vector4 ColorNow(in ParticlesComponent c, in Particle q)
        {
            Vector4 col = c.ColorOn ? q.Color * c.ColorOverLifetime.Evaluate(q.Life > 0f ? q.Age / q.Life : 1f) : q.Color;
            if (c.ColorBySpeedOn) col *= c.ColorBySpeed.Evaluate(SpeedFrac(c.ColorSpeedRange, q.Vel.Length()));
            return col;
        }

        private static float SizeNow(in ParticlesComponent c, in Particle q)
        {
            float size = q.Size * (c.SizeOn ? c.SizeOverLifetime.Evaluate(q.Life > 0f ? q.Age / q.Life : 1f) : 1f);
            if (c.SizeBySpeedOn) size *= c.SizeBySpeed.Evaluate(SpeedFrac(c.SizeSpeedRange, q.Vel.Length()));
            return size;
        }

        // A speed's place in a by-speed module's range, 0..1.
        private static float SpeedFrac(Vector2 range, float speed) =>
            range.Y > range.X ? Math.Clamp((speed - range.X) / (range.Y - range.X), 0f, 1f) : speed >= range.Y ? 1f : 0f;

        /// <summary>Each of one emitter's particles' velocity in the world, in the order of <see cref="CopyPositions"/>.</summary>
        public static int CopyVelocities(Entity entity, Span<Vector3> into)
        {
            if (!_pools.TryGetValue(entity, out Pool? p)) return 0;
            int n = Math.Min(p.Count, into.Length);
            for (int i = 0; i < n; i++) into[i] = WorldVel(p, in p.Items[i]);
            return n;
        }

        /// <summary>Each of one emitter's particles' rotation now (degrees in the view), in the order of <see cref="CopyPositions"/>.</summary>
        public static int CopyRotations(Entity entity, Span<float> into)
        {
            if (!_pools.TryGetValue(entity, out Pool? p)) return 0;
            int n = Math.Min(p.Count, into.Length);
            for (int i = 0; i < n; i++) into[i] = p.Items[i].Rot;
            return n;
        }

        /// <summary>Each of one emitter's particles' size now (metres), in the order of <see cref="CopyPositions"/>.</summary>
        public static int CopySizes(Entity entity, Span<float> into)
        {
            if (!_pools.TryGetValue(entity, out Pool? p)) return 0;
            int n = Math.Min(p.Count, into.Length);
            for (int i = 0; i < n; i++) into[i] = SizeNow(in p.C, in p.Items[i]);
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

        /// <summary>The hits of one emitter's particles in the last step (Collision module): as many as fit; returns how many
        /// (at most 64 a step are kept).</summary>
        public static int CopyCollisions(Entity entity, Span<ParticleCollisionEvent> into)
        {
            if (!_pools.TryGetValue(entity, out Pool? p)) return 0;
            int n = Math.Min(p.HitCount, into.Length);
            for (int i = 0; i < n; i++) into[i] = p.Hits[i];
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
            if (clear) { p.Count = 0; ResetTrails(p); }
        }

        /// <summary>The system from its beginning, its particles gone (the editor's Restart).</summary>
        public static void Restart(Entity entity)
        {
            if (!ECSWorld.Instance.TryGetComponent<ParticlesComponent>(entity, out var c)) return;
            Pool p = Get(entity, in c);
            p.Count = 0; p.Paused = false;
            ResetTrails(p);
            Begin(p, in c);
        }

        /// <summary>One emitter's particles taken away; it goes on as it was.</summary>
        public static void Clear(Entity entity)
        {
            if (_pools.TryGetValue(entity, out Pool? p)) { p.Count = 0; p.Pending = 0; ResetTrails(p); }
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
                if (pool.TrailCount.Length > 0) SizeTrails(pool, max);
            }
            if (c.TrailsOn && pool.TrailCount.Length != 2 * max) SizeTrails(pool, max);
            return pool;
        }

        // The trail slots: two for each particle the pool holds (a lingering trail keeps its slot), all free.
        private static void SizeTrails(Pool p, int max)
        {
            int slots = 2 * max;
            Array.Resize(ref p.TrailPos, slots * TrailPoints);
            Array.Resize(ref p.TrailAge, slots * TrailPoints);
            Array.Resize(ref p.TrailCount, slots);
            Array.Resize(ref p.FreeSlots, slots);
            Array.Resize(ref p.Lingering, slots);
            Array.Resize(ref p.SlotLife, slots);
            Array.Resize(ref p.SlotSize, slots);
            Array.Resize(ref p.SlotColor, slots);
            for (int i = 0; i < p.Count; i++) p.Items[i].Trail = 0;
            ResetTrails(p);
        }

        // Every slot free (the pool emptied at once: Stop, Clear, Restart).
        private static void ResetTrails(Pool p)
        {
            int slots = p.TrailCount.Length;
            for (int i = 0; i < slots; i++) { p.FreeSlots[i] = slots - 1 - i; p.TrailCount[i] = 0; }
            p.FreeCount = slots;
            p.LingerCount = 0;
        }

        private static void FreeTrail(Pool p, int slot)
        {
            p.TrailCount[slot] = 0;
            if (p.FreeCount < p.FreeSlots.Length) p.FreeSlots[p.FreeCount++] = slot;
        }

        // A particle ends: its trail freed, or kept until its points age out.
        private static void EndTrail(Pool pool, in ParticlesComponent c, in Particle q)
        {
            if (q.Trail == 0) return;
            int slot = q.Trail - 1;
            if (c.TrailDieWithParticles || pool.TrailCount[slot] == 0 || pool.LingerCount >= pool.Lingering.Length) { FreeTrail(pool, slot); return; }
            pool.SlotLife[slot] = q.Life;
            pool.SlotSize[slot] = SizeNow(in c, in q);
            pool.SlotColor[slot] = ColorNow(in c, in q);
            pool.Lingering[pool.LingerCount++] = slot;
        }

        // A trail's points aged by dt; those older than its lifetime dropped (from the front: the oldest).
        private static void AgeTrail(Pool pool, int slot, float dt, float maxAge)
        {
            int at = slot * TrailPoints, n = pool.TrailCount[slot], drop = 0;
            for (int k = 0; k < n; k++)
            {
                pool.TrailAge[at + k] += dt;
                if (pool.TrailAge[at + k] > maxAge) drop = k + 1;
            }
            if (drop > 0)
            {
                Array.Copy(pool.TrailPos, at + drop, pool.TrailPos, at, n - drop);
                Array.Copy(pool.TrailAge, at + drop, pool.TrailAge, at, n - drop);
                pool.TrailCount[slot] = n - drop;
            }
        }

        // The head's point added when it moved far enough from the newest (the oldest dropped when full).
        private static void PushTrail(Pool pool, int slot, Vector3 at, float minDistance)
        {
            int b = slot * TrailPoints, n = pool.TrailCount[slot];
            if (n > 0 && Vector3.DistanceSquared(pool.TrailPos[b + n - 1], at) < minDistance * minDistance) return;
            if (n == TrailPoints)
            {
                Array.Copy(pool.TrailPos, b + 1, pool.TrailPos, b, n - 1);
                Array.Copy(pool.TrailAge, b + 1, pool.TrailAge, b, n - 1);
                n--;
            }
            pool.TrailPos[b + n] = at;
            pool.TrailAge[b + n] = 0f;
            pool.TrailCount[slot] = n + 1;
        }

        // A point of the head in the trail's space: the world, or the emitter's (its position and rotation).
        private static Vector3 TrailSpace(Pool pool, in ParticlesComponent c, Vector3 world) =>
            c.TrailWorldSpace ? world : Vector3.Transform(world - pool.Origin, Quaternion.Conjugate(pool.Rotation));

        /// <summary>The points of one emitter's trails now (the living particles' and the lingering ones), in the world.</summary>
        public static int CopyTrailPoints(Entity entity, Span<Vector3> into)
        {
            if (!_pools.TryGetValue(entity, out Pool? p) || p.TrailCount.Length == 0) return 0;
            int n = 0;
            for (int s = 0; s < p.TrailCount.Length; s++)
                for (int k = 0; k < p.TrailCount[s] && n < into.Length; k++)
                {
                    Vector3 v = p.TrailPos[s * TrailPoints + k];
                    into[n++] = p.C.TrailWorldSpace ? v : p.Origin + Vector3.Transform(v, p.Rotation);
                }
            return n;
        }

        /// <summary>How many trails one emitter has now: its particles' and the lingering ones.</summary>
        public static int TrailCountOf(Entity entity)
        {
            if (!_pools.TryGetValue(entity, out Pool? p) || p.TrailCount.Length == 0) return 0;
            return p.TrailCount.Length - p.FreeCount;
        }

        /// <summary>The age of each trail point (seconds), in the order of <see cref="CopyTrailPoints"/>.</summary>
        public static int CopyTrailAges(Entity entity, Span<float> into)
        {
            if (!_pools.TryGetValue(entity, out Pool? p) || p.TrailCount.Length == 0) return 0;
            int n = 0;
            for (int s = 0; s < p.TrailCount.Length; s++)
                for (int k = 0; k < p.TrailCount[s] && n < into.Length; k++) into[n++] = p.TrailAge[s * TrailPoints + k];
            return n;
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
            _messageCount = 0;
            if (!world.HasAnyComponent<ParticlesComponent>())
            {
                if (_pools.Count > 0) _pools.Clear();
                return;
            }
            // The Wind Zones (G8.8 Q7), for the systems with External Forces.
            _windCount = 0;
            _windTime += dt;
            if (world.HasAnyComponent<WindZone>())
                foreach (var row in world.Query<ActiveFlag, WindZone, Transform>().EnumerateWithEntities<ActiveFlag, WindZone, Transform>())
                {
                    if (!row.Item1.Value.Active || _windCount == _winds.Length) continue;
                    Matrix4x4 wm = Transform.GetWorldMatrix(world, row.Item3.Value);
                    Vector3 fw = Vector3.TransformNormal(Vector3.UnitZ, wm);
                    _winds[_windCount++] = new Wind { At = wm.Translation, Forward = fw.LengthSquared() > 1e-10f ? Vector3.Normalize(fw) : Vector3.UnitZ, Z = row.Item2.Value };
                }
            // The sub-emitters named by the active emitters: they do not make particles on their own.
            _subEntityCount = 0;
            foreach (var row in world.Query<ActiveFlag, ParticlesComponent>().EnumerateWithEntities<ActiveFlag, ParticlesComponent>())
            {
                ref readonly var c = ref row.Item2.Value;
                if (!c.SubOn || !row.Item1.Value.Active) continue;
                for (int k = 0; k < c.SubEmitters.Count && _subEntityCount < MaxSubs; k++)
                    if (c.SubEmitters.Items[k].Emitter != row.Entity) _subEntities[_subEntityCount++] = c.SubEmitters.Items[k].Emitter;
            }
            foreach (var row in world.Query<ActiveFlag, ParticlesComponent, Transform>().EnumerateWithEntities<ActiveFlag, ParticlesComponent, Transform>())
            {
                Entity e = row.Entity;
                if (onlyUnder && !IsUnder(world, e, root)) continue;
                ref readonly var c = ref row.Item2.Value;
                Pool pool = Get(e, in c);
                pool.Seen = _frame;
                pool.C = c;
                pool.IsSub = IsSubEntity(e);
                if (!row.Item1.Value.Active) { pool.Count = 0; ResetTrails(pool); continue; }
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
                    for (int i = 0; i < steps; i++) Simulate(world, e, pool, in c, in model, 1f / 30f);
                }
                Simulate(world, e, pool, in c, in model, run);
            }
            // An emitter gone from the scene takes its particles with it.
            _gone.Clear();
            foreach (var pair in _pools) if (pair.Value.Seen != _frame) _gone.Add(pair.Key);
            foreach (Entity e in _gone) _pools.Remove(e);
            // The trigger callbacks (G8.8 Q7): the emitter's scripts told once a step.
            if (!onlyUnder)
                foreach (var pair in _pools)
                    if (pair.Value.TellTrigger) { pair.Value.TellTrigger = false; Scripting.ScriptSystem.DispatchParticleTrigger(pair.Key); }
            // The collision messages (not in the editor's preview: no script runs there), each pair once a step, both sides.
            if (!onlyUnder)
                for (int i = 0; i < _messageCount; i++)
                {
                    Entity emitter = _messageEmitter[i], other = _messageOther[i];
                    Scripting.ScriptSystem.DispatchParticleCollision(emitter, other);
                    Scripting.ScriptSystem.DispatchParticleCollision(other, emitter);
                }
        }

        private static bool IsSubEntity(Entity e)
        {
            for (int i = 0; i < _subEntityCount; i++) if (_subEntities[i] == e) return true;
            return false;
        }

        // The emitter's sub-emitters made ready for its step: each entry's pool, component, matrix and burst count.
        private static void PrepareSubs(ECSWorld world, Entity self, in ParticlesComponent c)
        {
            _subCount = 0;
            if (!c.SubOn) return;
            for (int k = 0; k < c.SubEmitters.Count; k++)
            {
                ParticleSubEmitter entry = c.SubEmitters.Items[k];
                Entity se = entry.Emitter;
                if (se.IsNull || !se.IsAlive || se == self || !world.TryGetComponent<ParticlesComponent>(se, out var sc)
                    || !world.TryGetComponent<Transform>(se, out var st)) continue;
                ref SubInfo s = ref _subs[_subCount++];
                s.C = sc;
                s.Pool = Get(se, in sc);
                s.Model = Transform.GetWorldMatrix(world, st);
                s.Entry = entry;
                int burst = 0;
                for (int b = 0; b < sc.Bursts.Count; b++) burst += Math.Max(0, sc.Bursts.Items[b].Count);
                s.Burst = sc.Bursts.Count > 0 ? burst : Math.Max(0, sc.Burst);
            }
        }

        // Particles of sub-emitter s made at a parent particle's place, taking what the entry says from it.
        private static void SpawnSub(ref uint seed, ref SubInfo s, int count, Vector3 at, Vector3 vel, Vector4 color, float size, float rot, float lifeMul)
        {
            if (s.Pool == null || count <= 0) return;
            if (s.Entry.Probability < 1f && Next(ref seed) >= s.Entry.Probability) return;
            Pool sp = s.Pool;
            Matrix4x4 m = s.Model;
            m.Translation = at;
            int before = sp.Count;
            Make(sp, in s.C, in m, count, placed: true);
            ParticleInherit inherit = s.Entry.Inherit;
            Vector3 add = s.C.InheritVelocityOn ? vel * s.C.InheritVelocity : Vector3.Zero;
            if (!s.C.World) add = Vector3.Transform(add, Quaternion.Conjugate(sp.Rotation));
            for (int i = before; i < sp.Count; i++)
            {
                ref Particle q = ref sp.Items[i];
                if ((inherit & ParticleInherit.Color) != 0) q.Color *= color;
                if ((inherit & ParticleInherit.Size) != 0) q.Size *= size;
                if ((inherit & ParticleInherit.Rotation) != 0) q.Rot += rot;
                if ((inherit & ParticleInherit.Lifetime) != 0) q.Life = MathF.Max(1e-3f, q.Life * lifeMul);
                q.Vel += add;
            }
        }

        // A parent particle's event: each sub-emitter of that type makes its count at the particle's place.
        private static void SubEvent(Pool pool, in ParticlesComponent c, ref Particle p, ParticleSubType type, Vector3 at, Vector3 vel)
        {
            if (_subCount == 0) return;
            Vector4 color = ColorNow(in c, in p);
            float size = SizeNow(in c, in p), life = type == ParticleSubType.Death ? 1f : MathF.Max(0f, 1f - p.Age / MathF.Max(1e-6f, p.Life));
            for (int k = 0; k < _subCount; k++)
            {
                ref SubInfo s = ref _subs[k];
                if (s.Entry.Type != type) continue;
                SpawnSub(ref pool.Seed, ref s, s.Burst, at, vel, color, size, p.Rot, life);
            }
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

        private static void Simulate(ECSWorld world, Entity self, Pool pool, in ParticlesComponent c, in Matrix4x4 model, float dt)
        {
            PrepareSubs(world, self, in c);
            PrepareTriggers(world, in c);
            pool.TriggeredCount = 0;
            bool wind = c.ExternalForcesOn && _windCount > 0;
            bool births = false;
            for (int k = 0; k < _subCount; k++) births |= _subs[k].Entry.Type == ParticleSubType.Birth;
            pool.EmitterVel = pool.HasLast && dt > 0f ? (pool.Origin - pool.Last) / dt : Vector3.Zero;
            Vector3 carried = c.InheritVelocityOn && c.InheritVelocityMode == ParticleInheritVelocity.Current && c.World ? pool.EmitterVel * c.InheritVelocity : Vector3.Zero;
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

            // The Collision module: the planes in the world (a position and its up), the world's quality stride.
            pool.HitCount = 0;
            bool collide = c.CollisionOn;
            Span<Vector3> planeAt = stackalloc Vector3[ParticlePlanes.Capacity], planeUp = stackalloc Vector3[ParticlePlanes.Capacity];
            Span<int> planeOf = stackalloc int[ParticlePlanes.Capacity];
            int planes = 0;
            if (collide && c.CollisionType == ParticleCollisionType.Planes)
                for (int k = 0; k < c.Planes.Count; k++)
                {
                    Entity pe = c.Planes.Items[k];
                    if (pe.IsNull || !pe.IsAlive || !world.TryGetComponent<Transform>(pe, out var pt)) continue;
                    Matrix4x4 pm = Transform.GetWorldMatrix(world, pt);
                    Vector3 up = Vector3.TransformNormal(Vector3.UnitY, pm);
                    if (up.LengthSquared() < 1e-12f) continue;
                    planeAt[planes] = pm.Translation; planeUp[planes] = Vector3.Normalize(up); planeOf[planes] = k;
                    planes++;
                }
            int stride = c.CollisionQuality == ParticleCollisionQuality.Low ? 4 : c.CollisionQuality == ParticleCollisionQuality.Medium ? 2 : 1;
            int phase = _frame % stride;

            // Age and move.
            for (int i = 0; i < pool.Count;)
            {
                ref Particle p = ref pool.Items[i];
                p.Age += dt;
                if (p.Age >= p.Life)
                {
                    if (_subCount > 0) SubEvent(pool, in c, ref p, ParticleSubType.Death, WorldPos(pool, in p), WorldVel(pool, in p));
                    if (p.Trail != 0) EndTrail(pool, in c, in p);
                    pool.Items[i] = pool.Items[--pool.Count];
                    continue;
                }
                Vector3 from = p.Pos;
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
                if (carried != Vector3.Zero) p.Pos += carried * dt;
                if (wind)
                {
                    // External Forces: the Wind Zones' push at the particle (in the world; into the emitter's space when local).
                    Vector3 push = WindAt(c.World ? p.Pos : pool.Origin + Vector3.Transform(p.Pos, pool.Rotation)) * (c.ExternalMultiplier * dt);
                    p.Vel += c.World ? push : Vector3.Transform(push, Quaternion.Conjugate(pool.Rotation));
                }
                if (c.NoiseOn)
                {
                    Vector3 at = (c.World ? p.Pos : pool.Origin + Vector3.Transform(p.Pos, pool.Rotation)) * c.NoiseFrequency + new Vector3(scroll);
                    p.Pos += Noise3(at, pool.NoiseSeed) * (c.NoiseStrength * dt);
                }
                if (spin) p.Rot += p.Spin * dt;
                if (c.RotationBySpeedOn)
                {
                    float f = SpeedFrac(c.RotationSpeedRange, p.Vel.Length());
                    p.Rot += (c.RotationBySpeed.X + (c.RotationBySpeed.Y - c.RotationBySpeed.X) * f) * dt;
                }
                if (collide && !Collide(self, pool, in c, ref p, from, i, stride, phase, planeAt[..planes], planeUp[..planes], planeOf[..planes]))
                {
                    if (_subCount > 0) SubEvent(pool, in c, ref p, ParticleSubType.Death, WorldPos(pool, in p), WorldVel(pool, in p));
                    if (p.Trail != 0) EndTrail(pool, in c, in p);
                    pool.Items[i] = pool.Items[--pool.Count];
                    continue;
                }
                if (_triggerCount > 0 && !Trigger(pool, in c, ref p))
                {
                    if (_subCount > 0) SubEvent(pool, in c, ref p, ParticleSubType.Death, WorldPos(pool, in p), WorldVel(pool, in p));
                    if (p.Trail != 0) EndTrail(pool, in c, in p);
                    pool.Items[i] = pool.Items[--pool.Count];
                    continue;
                }
                if (p.Trail != 0 && c.TrailsOn)
                {
                    // Its trail: the points aged, the old ones dropped, the head's new point.
                    int slot = p.Trail - 1;
                    AgeTrail(pool, slot, dt, MathF.Max(0f, c.TrailLifetime) * p.Life);
                    PushTrail(pool, slot, TrailSpace(pool, in c, WorldPos(pool, in p)), MathF.Max(0.001f, c.TrailMinVertexDistance));
                }
                if (births)
                {
                    // Birth sub-emitters: the particle is their emitter (their Rate over Time and over Distance).
                    Vector3 here = WorldPos(pool, in p);
                    for (int k = 0; k < _subCount; k++)
                    {
                        ref SubInfo s = ref _subs[k];
                        if (s.Entry.Type != ParticleSubType.Birth) continue;
                        p.SubCarry += s.C.Rate * dt + s.C.RateOverDistance * Vector3.Distance(p.Pos, from);
                        int whole = (int)p.SubCarry;
                        if (whole <= 0) continue;
                        p.SubCarry -= whole;
                        SpawnSub(ref pool.Seed, ref s, whole, here, WorldVel(pool, in p), ColorNow(in c, in p), SizeNow(in c, in p), p.Rot,
                                 MathF.Max(0f, 1f - p.Age / MathF.Max(1e-6f, p.Life)));
                    }
                }
                i++;
            }

            // The lingering trails (of particles that died, Die with Particles off): aged until their points are gone.
            for (int k = 0; k < pool.LingerCount;)
            {
                int slot = pool.Lingering[k];
                AgeTrail(pool, slot, dt, MathF.Max(0f, c.TrailLifetime) * pool.SlotLife[slot]);
                if (pool.TrailCount[slot] == 0) { FreeTrail(pool, slot); pool.Lingering[k] = pool.Lingering[--pool.LingerCount]; continue; }
                k++;
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
                if (c.Emitting && !pool.IsSub)
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
            if (make > 0)
            {
                int first = pool.Count;
                Make(pool, in c, in model, make);
                if (c.InheritVelocityOn && c.InheritVelocityMode == ParticleInheritVelocity.Initial && pool.EmitterVel != Vector3.Zero)
                {
                    Vector3 add = pool.EmitterVel * c.InheritVelocity;
                    if (!c.World) add = Vector3.Transform(add, Quaternion.Conjugate(pool.Rotation));
                    for (int i = first; i < pool.Count; i++) pool.Items[i].Vel += add;
                }
            }
        }

        // One particle's step against the Collision module (docs PARTICLES_3D_DESIGN.md §6.1): planes every frame, the world for
        // the particles of this frame's share (from where each was last tested). A hit puts it on the surface at its radius and
        // turns its speed; false: it dies (its life lost, too slow or too fast).
        private static bool Collide(Entity self, Pool pool, in ParticlesComponent c, ref Particle p, Vector3 from, int index, int stride,
                                    int phase, ReadOnlySpan<Vector3> planeAt, ReadOnlySpan<Vector3> planeUp, ReadOnlySpan<int> planeOf)
        {
            bool local = !c.World;
            Vector3 ToWorld(Vector3 v) => local ? pool.Origin + Vector3.Transform(v, pool.Rotation) : v;
            float t = p.Life > 0f ? p.Age / p.Life : 1f;
            float radius = MathF.Max(0f, p.Size * (c.SizeOn ? c.SizeOverLifetime.Evaluate(t) : 1f) * 0.5f * c.RadiusScale);
            Vector3 b = ToWorld(p.Pos);
            bool hit = false;
            Vector3 at = default, normal = default;
            Entity other = default;
            if (c.CollisionType == ParticleCollisionType.Planes)
            {
                Vector3 a = ToWorld(from);
                for (int k = 0; k < planeAt.Length && !hit; k++)
                {
                    Vector3 n = planeUp[k];
                    float d0 = Vector3.Dot(a - planeAt[k], n), d1 = Vector3.Dot(b - planeAt[k], n);
                    // Into the plane, its sphere reaching it, and not from far behind it.
                    if (d1 < radius && d0 >= -radius && Vector3.Dot(b - a, n) < 0f)
                    {
                        hit = true; normal = n; at = b + n * (radius - d1); other = c.Planes.Items[planeOf[k]];
                    }
                }
            }
            else if ((index + phase) % stride == 0)
            {
                Vector3 a = ToWorld(stride > 1 ? p.Tested : from);
                Vector3 dir = b - a;
                float len = dir.Length();
                if (len > 1e-6f)
                {
                    dir /= len;
                    if (SceneManager.Raycast(a, dir, len + radius, out Entity e3, out var h3) && h3.Distance - radius <= len)
                    {
                        hit = true; normal = h3.Normal; at = a + dir * MathF.Max(0f, h3.Distance - radius); other = e3;
                    }
                    else if (Physics2D.Physics2DWorld.Raycast(new Vector2(a.X, a.Y), new Vector2(dir.X, dir.Y), (len + radius) * new Vector2(dir.X, dir.Y).Length(),
                                                              out Entity e2, out var h2))
                    {
                        float along = h2.Distance / MathF.Max(1e-6f, new Vector2(dir.X, dir.Y).Length());
                        if (along - radius <= len)
                        {
                            hit = true; normal = new Vector3(h2.Normal, 0f); at = a + dir * MathF.Max(0f, along - radius); other = e2;
                        }
                    }
                }
                if (!hit) p.Tested = p.Pos;
            }
            if (!hit) return true;

            // The speed into the surface turned (times Bounce), the whole speed less Dampen.
            Vector3 vel = local ? Vector3.Transform(p.Vel, pool.Rotation) : p.Vel;
            float vn = Vector3.Dot(vel, normal);
            if (vn < 0f) vel = (vel - normal * vn * (1f + c.Bounce)) * (1f - Math.Clamp(c.CollisionDampen, 0f, 1f));
            Quaternion toLocal = Quaternion.Conjugate(pool.Rotation);
            p.Pos = local ? Vector3.Transform(at - pool.Origin, toLocal) : at;
            p.Vel = local ? Vector3.Transform(vel, toLocal) : vel;
            p.Tested = p.Pos;
            if (pool.HitCount < pool.Hits.Length)
                pool.Hits[pool.HitCount++] = new ParticleCollisionEvent { Point = at, Normal = normal, Velocity = vel, Other = other };
            if (c.SendCollisionMessages && !other.IsNull) Message(self, other);
            if (_subCount > 0) SubEvent(pool, in c, ref p, ParticleSubType.Collision, at, vel);
            p.Age += p.Life * Math.Clamp(c.LifetimeLoss, 0f, 1f);
            float speed = vel.Length();
            return p.Age < p.Life && speed >= c.MinKillSpeed && speed <= c.MaxKillSpeed;
        }

        private static void Message(Entity emitter, Entity other)
        {
            for (int i = 0; i < _messageCount; i++)
                if (_messageEmitter[i] == emitter && _messageOther[i] == other) return;
            if (_messageCount == MaxMessages) return;
            _messageEmitter[_messageCount] = emitter;
            _messageOther[_messageCount] = other;
            _messageCount++;
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

        private static Vector3 WorldVel(Pool p, in Particle q) => p.C.World ? q.Vel : Vector3.Transform(q.Vel, p.Rotation);

        // placed: the shape at the matrix's translation (a sub-emitter's at its parent particle), not at the pool's origin.
        private static void Make(Pool pool, in ParticlesComponent c, in Matrix4x4 model, int make, bool placed = false)
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
            bool atOrigin = c.ShapePosition == Vector3.Zero && !placed;
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
                if (c.LifetimeBySpeedOn) life *= c.LifetimeBySpeed.Evaluate(SpeedFrac(c.LifetimeSpeedRange, pool.EmitterVel.Length()));
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
                q.Tested = q.Pos;
                q.SubCarry = 0f;
                q.Lit = c.LightsOn && (c.LightRatio >= 1f || (c.LightRatio > 0f && Next(ref pool.Seed) < c.LightRatio));
                // A trail for the Ratio's share (a random drawn only for a ratio under 1), from a free slot.
                if (c.TrailsOn && pool.FreeCount > 0 && (c.TrailRatio >= 1f || (c.TrailRatio > 0f && Next(ref pool.Seed) < c.TrailRatio)))
                {
                    int slot = pool.FreeSlots[--pool.FreeCount];
                    pool.TrailCount[slot] = 0;
                    q.Trail = slot + 1;
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

    /// <summary>One hit of a particle (the Collision module): where, the surface's normal, its velocity after, what it hit.</summary>
    public struct ParticleCollisionEvent
    {
        public Vector3 Point, Normal, Velocity;
        public Entity Other;
    }

    /// <summary>The Triggers module's conditions.</summary>
    public enum ParticleTriggerKind : byte { Inside, Outside, Enter, Exit }

    /// <summary>A particle that met a Callback condition of the Triggers module: where it was, the trigger (none for Outside), which condition.</summary>
    public struct ParticleTriggerEvent
    {
        public Vector3 Position;
        public Entity Trigger;
        public ParticleTriggerKind Kind;
    }
}
