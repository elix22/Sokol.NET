using System;
using System.Collections.Generic;
using System.Numerics;
using Frent;
using GameEditor.Framework.Assets;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;

namespace GameEditor.Framework.Scene
{
    /// <summary>
    /// The items of a race (docs/H5_DESIGN.md §13), in the fixed physics steps after the race: item boxes give a racer with
    /// an empty slot an item drawn by its place; the player uses it with Fire1 or Jump, a computer driver by a rule, a script
    /// with <see cref="Race.UseItem"/>; a boost, a puddle dropped behind, a bolt fired ahead, a guard. A kart hit hops and
    /// loses drive; nothing turns it, and after a hit it is protected for <see cref="Protection"/> seconds (R1, R16).
    /// </summary>
    public static class ItemSystem
    {
        public const float BoxReach = 1.6f, BoostTime = 1.5f, BoostScale = 1.4f, PuddleTime = 1.2f, PuddleReach = 1.2f;
        public const float BoltSpeed = 40f, BoltRange = 60f, BoltReach = 1.3f, StunTime = 1f, GuardTime = 4f, Protection = 1.5f;

        // The draw: a table per place bucket — first, middle, last — fixed here.
        private static readonly (ItemKind kind, float weight)[][] Table =
        {
            new[] { (ItemKind.Boost, 0.3f), (ItemKind.Puddle, 0.4f), (ItemKind.Guard, 0.3f) },
            new[] { (ItemKind.Boost, 0.3f), (ItemKind.Puddle, 0.25f), (ItemKind.Bolt, 0.3f), (ItemKind.Guard, 0.15f) },
            new[] { (ItemKind.Boost, 0.5f), (ItemKind.Puddle, 0.1f), (ItemKind.Bolt, 0.3f), (ItemKind.Guard, 0.1f) },
        };

        private static readonly Dictionary<Entity, float> _hidden = new();      // a box: seconds until it is back
        private static readonly List<Entity> _gone = new();
        private static readonly List<Entity> _boxes = new();

        /// <summary>The bucket of a place among <paramref name="count"/> racers: 0 first, 2 last, 1 between.</summary>
        public static int Bucket(int place, int count) => place <= 1 ? 0 : place >= count ? 2 : 1;

        /// <summary>The item a racer in <paramref name="bucket"/> draws from a number 0 ≤ r &lt; 1.</summary>
        public static ItemKind Draw(int bucket, float r)
        {
            var row = Table[Math.Clamp(bucket, 0, 2)];
            float sum = 0f;
            foreach (var (_, w) in row) sum += w;
            float at = r * sum;
            foreach (var (kind, w) in row) { if (at < w) return kind; at -= w; }
            return row[^1].kind;
        }

        public static void Reset()
        {
            _hidden.Clear();
            _gone.Clear();
        }

        /// <summary>One fixed step of the items (called by the scene manager after the race's step).</summary>
        internal static void Step(float dt)
        {
            if (Race.State != RaceState.Racing || Race.Track is not { } t) return;
            var world = ECSWorld.Instance;
            Boxes(world, dt);
            foreach (RacerState r in Race.Racers) Timers(world, r, dt);
            Puddles(world, dt);
            Bolts(world, t, dt);
            Uses(world, t);
        }

        private static Vector3 At(ECSWorld world, Entity e)
            => world.TryGetComponent<Transform>(e, out var tr) ? Transform.GetWorldMatrix(world, tr).Translation : Vector3.Zero;

        private static void Boxes(ECSWorld world, float dt)
        {
            _boxes.Clear();
            foreach (var row in world.Query<ItemBoxComponent, Transform>().EnumerateWithEntities<ItemBoxComponent, Transform>()) _boxes.Add(row.Entity);
            for (int b = 0; b < _boxes.Count; b++)
            {
                Entity box = _boxes[b];
                if (_hidden.TryGetValue(box, out float left))
                {
                    left -= dt;
                    if (left > 0f) { _hidden[box] = left; continue; }
                    _hidden.Remove(box);
                    Show(world, box, true);
                }
                // A box that is there turns about its up axis (90 degrees a second): seen, it is a box to drive into.
                ref Transform spin = ref world.GetComponent<Transform>(box);
                spin.Rotation = Quaternion.Normalize(spin.Rotation * Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2f * dt));
                Vector3 at = At(world, box);
                foreach (RacerState r in Race.Racers)
                {
                    if (r.Item != ItemKind.None || r.Finished || !Race.Where(world, r.Entity, out Vector3 kart, out _)) continue;
                    if (Vector3.DistanceSquared(kart, at) > BoxReach * BoxReach) continue;
                    uint seed = (uint)(world.GetSceneId(r.Entity) * 2654435761u) ^ (uint)((b + 1) * 40503) ^ (uint)(r.Lap * 9176) ^ 0x5bd1e995u;
                    r.Item = Draw(Bucket(r.Place, Race.Racers.Count), Next(ref seed));
                    _hidden[box] = MathF.Max(0.5f, world.GetComponent<ItemBoxComponent>(box).Respawn);
                    Show(world, box, false);
                    break;
                }
            }
        }

        private static void Show(ECSWorld world, Entity box, bool shown)
        {
            if (!world.TryGetComponent<MeshRenderer>(box, out var mr) || mr.Visible == shown) return;
            mr.Visible = shown;
            world.AddComponent(box, mr);
        }

        // The timers of a racer and what they do to its kart.
        private static void Timers(ECSWorld world, RacerState r, float dt)
        {
            r.Boosting = MathF.Max(0f, r.Boosting - dt);
            r.Slowed = MathF.Max(0f, r.Slowed - dt);
            r.Stunned = MathF.Max(0f, r.Stunned - dt);
            r.Guarded = MathF.Max(0f, r.Guarded - dt);
            r.Protected = MathF.Max(0f, r.Protected - dt);
            ref KartComponent k = ref world.GetComponent<KartComponent>(r.Entity);
            k.DriveScale = r.Stunned > 0f ? 1f : r.Slowed > 0f ? 0.4f : r.Boosting > 0f ? BoostScale : 1f;
            k.GripScale = r.Slowed > 0f ? 0.5f : 1f;
            k.Stunned = r.Stunned > 0f;
        }

        /// <summary>A hit on a racer: a guard takes it, protection ignores it; else the kart hops and loses drive (a bolt) or slides (a puddle).</summary>
        private static bool Hit(RacerState r, bool bolt)
        {
            if (r.Protected > 0f || r.Finished) return false;
            if (r.Guarded > 0f) { r.Guarded = 0f; r.Protected = Protection; return false; }
            r.Hits++;
            r.Protected = Protection;
            if (bolt)
            {
                r.Stunned = StunTime;
                SceneManager.AddImpulse(r.Entity, Vector3.UnitY * 150f * 4f);      // a hop: nothing turns it
            }
            else r.Slowed = PuddleTime;
            return true;
        }

        private static void Puddles(ECSWorld world, float dt)
        {
            _gone.Clear();
            foreach (var row in world.Query<PuddleComponent, Transform>().EnumerateWithEntities<PuddleComponent, Transform>())
            {
                ref PuddleComponent p = ref row.Item1.Value;
                p.Age += dt;
                Vector3 at = Transform.GetWorldMatrix(world, row.Item2.Value).Translation;
                foreach (RacerState r in Race.Racers)
                {
                    if ((r.Entity == p.Owner && p.Age < 1f) || !Race.Where(world, r.Entity, out Vector3 kart, out _)) continue;
                    if (Vector3.DistanceSquared(kart, at) > PuddleReach * PuddleReach) continue;
                    Hit(r, bolt: false);
                    _gone.Add(row.Entity);
                    break;
                }
                if (p.Age > 30f) _gone.Add(row.Entity);
            }
            foreach (Entity e in _gone) SceneManager.Destroy(e);
        }

        private static void Bolts(ECSWorld world, TrackData t, float dt)
        {
            _gone.Clear();
            foreach (var row in world.Query<BoltComponent, Transform>().EnumerateWithEntities<BoltComponent, Transform>())
            {
                ref BoltComponent b = ref row.Item1.Value;
                float step = BoltSpeed * dt;
                b.Along += step;
                b.Travelled += step;
                TrackFrame f = t.FrameAt(b.Along);
                ref Transform tr = ref row.Item2.Value;
                tr.Position = f.Position + f.Right * b.Offset + f.Up * 0.6f;
                tr.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.Atan2(f.Forward.X, f.Forward.Z));
                bool hit = false;
                foreach (RacerState r in Race.Racers)
                {
                    if (r.Entity == b.Owner || !Race.Where(world, r.Entity, out Vector3 kart, out _)) continue;
                    if (Vector3.DistanceSquared(kart, tr.Position) > BoltReach * BoltReach) continue;
                    Hit(r, bolt: true);
                    hit = true;
                    break;
                }
                if (hit || b.Travelled > BoltRange) _gone.Add(row.Entity);
            }
            foreach (Entity e in _gone) SceneManager.Destroy(e);
        }

        // The player's Fire, and the computer drivers' rules.
        private static void Uses(ECSWorld world, TrackData t)
        {
            foreach (RacerState r in Race.Racers)
            {
                if (r.Item == ItemKind.None || r.Finished || !world.TryGetComponent<KartComponent>(r.Entity, out var k)) continue;
                bool use = false;
                // A kart a computer driver drives uses its items by the driver's rule (the player's too, in a check's driven run).
                if (world.HasComponent<DriverComponent>(r.Entity)) use = DriverWants(world, r, t);
                else if (k.Player) use = Input.InputManager.GetButtonDown("Fire1") || Input.InputManager.GetButtonDown("Jump");
                if (use) Race.UseItem(r.Entity);
            }
        }

        private static bool DriverWants(ECSWorld world, RacerState me, TrackData t)
        {
            if (!Race.Where(world, me.Entity, out Vector3 at, out _)) return false;
            float along = t.Nearest(at, out _);
            switch (me.Item)
            {
                case ItemKind.Guard: return true;
                case ItemKind.Boost: return DriverSystem.SpeedAt(along + 20f) >= 0.98f * world.GetComponent<KartComponent>(me.Entity).TopSpeed;
                default:
                    foreach (RacerState o in Race.Racers)
                    {
                        if (o == me || !Race.Where(world, o.Entity, out Vector3 other, out _)) continue;
                        float gap = t.Nearest(other, out _) - along;
                        if (gap < -t.Length / 2f) gap += t.Length; else if (gap > t.Length / 2f) gap -= t.Length;
                        if (me.Item == ItemKind.Bolt && gap > 2f && gap < 30f) return true;
                        if (me.Item == ItemKind.Puddle && gap < -2f && gap > -20f) return true;
                    }
                    return false;
            }
        }

        /// <summary>Uses a racer's item now (Race.UseItem).</summary>
        internal static bool Use(RacerState r)
        {
            var world = ECSWorld.Instance;
            if (r.Item == ItemKind.None || Race.Track is not { } t || !Race.Where(world, r.Entity, out Vector3 at, out Quaternion turn)) return false;
            ItemKind kind = r.Item;
            r.Item = ItemKind.None;
            float along = t.Nearest(at, out float offset);
            switch (kind)
            {
                case ItemKind.Boost: r.Boosting = BoostTime; break;
                case ItemKind.Guard: r.Guarded = GuardTime; break;
                case ItemKind.Puddle:
                {
                    Vector3 back = Vector3.Transform(-Vector3.UnitZ, turn);
                    Entity p = Make(world, "Puddle", at + back * 2.2f + Vector3.UnitY * -0.45f, new Vector3(2.2f, 0.05f, 2.2f), GameEditor.Framework.Renderer.PrimitiveMeshSpec.ToMeshPath(GameEditor.Framework.Renderer.PrimitiveMeshSpec.Default(GameEditor.Framework.Renderer.PrimitiveKind.Cylinder)));
                    world.AddComponent(p, new PuddleComponent { Owner = r.Entity });
                    break;
                }
                case ItemKind.Bolt:
                {
                    Entity b = Make(world, "Bolt", at, new Vector3(0.3f, 0.3f, 0.9f), GameEditor.Framework.Renderer.PrimitiveMeshSpec.ToMeshPath(GameEditor.Framework.Renderer.PrimitiveMeshSpec.Default(GameEditor.Framework.Renderer.PrimitiveKind.Box)));
                    world.AddComponent(b, new BoltComponent { Owner = r.Entity, Along = along + 1.5f, Offset = offset });
                    break;
                }
            }
            return true;
        }

        private static Entity Make(ECSWorld world, string name, Vector3 at, Vector3 size, string mesh)
        {
            Entity e = SceneManager.ActiveScene!.CreateEntity(name);
            var tr = world.GetComponent<Transform>(e);
            tr.Position = at;
            tr.Scale = size;
            world.AddComponent(e, tr);
            world.AddComponent(e, new MeshRenderer { MeshPath = mesh, Visible = true, ReceivesShadows = true, CastsShadows = false });
            return e;
        }

        private static float Next(ref uint s) { s ^= s << 13; s ^= s >> 17; s ^= s << 5; return (s & 0xFFFFFF) / (float)0x1000000; }
    }
}
