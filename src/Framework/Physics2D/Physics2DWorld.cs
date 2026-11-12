using System;
using System.Collections.Generic;
using System.Numerics;
using Frent;
using GameEditor.Framework.Assets;
using GameEditor.Framework.Core;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Scripting;
using Sokol;
using static Sokol.Box2D;

namespace GameEditor.Framework.Physics2D
{
    /// <summary>What a 2D ray met.</summary>
    public struct RaycastHit2D
    {
        /// <summary>Where the ray met the body, in world units.</summary>
        public Vector2 Point;
        /// <summary>The surface's direction there, pointing away from the body.</summary>
        public Vector2 Normal;
        /// <summary>How far along the ray, in world units.</summary>
        public float Distance;
    }

    /// <summary>
    /// The 2D physics world of a running scene: Box2D, beside the 3D world and not touching it. It holds a body
    /// for every entity with a <see cref="Body2D"/> and the walls of every tile map with a solid layer, in the
    /// XY plane. Made when a scene that has such an entity starts to play, or when the first one is placed;
    /// stepped by the scene at a fixed sixtieth of a second. After a step a dynamic body writes its entity's x, y
    /// and turn about z; a kinematic body, and any body a script moved by its Transform, is read from the
    /// Transform before it. Main thread only.
    /// </summary>
    public static unsafe class Physics2DWorld
    {
        /// <summary>The step of the 2D world, in seconds.</summary>
        public const float FixedStep = 1f / 60f;
        private const int SubSteps = 4, MaxStepsAFrame = 4;

        private sealed class Body
        {
            public Entity Entity;
            public b2BodyId Id;
            public Body2DType Type;
            public bool HasComponent;                   // made for a Body2D (false: only a tile map's walls)
            public Vector2 Position;                    // as last given to, or read from, the Transform
            public float Angle;
            public readonly List<b2ShapeId> Walls = new();
            public string? MapKey;                      // the tile map whose solid layers are this body's walls
            public Vector2 WallScale;
            public int MapVersion = -1;
        }

        private static b2WorldId _world;
        private static bool _made, _refused;
        private static float _accumulator;
        private static readonly List<Body?> _bodies = new();
        private static readonly Stack<int> _free = new();
        private static readonly Dictionary<Entity, int> _byEntity = new();
        // Pairs of bodies that touch, with how many of their shapes do: Enter at the first, Exit after the last.
        private static readonly Dictionary<(int, int), int> _touching = new();
        private static readonly List<(int, int)> _began = new(), _pairs = new();
        private static Vector2 _gravity = new(0f, -9.81f);
        // Entities whose body is as large as their sprite, and whose sprite's image has not been read yet.
        private static readonly HashSet<Entity> _waiting = new();
        private static readonly List<Entity> _arrived = new();

        /// <summary>Finds a sprite's frame by what the sprite shows: <see cref="SpriteLibrary.Find"/>'s shape.</summary>
        public delegate SpriteLibrary.Answer FrameFinder(string? texture, out SpriteFrame frame);

        /// <summary>
        /// Asked for the frame of a sprite whose body has no shape of its own. When set, it alone is asked (a
        /// test); otherwise <see cref="SpriteLibrary.Find"/>.
        /// </summary>
        public static FrameFinder? Frames { get; set; }

        private static SpriteLibrary.Answer FindFrame(string? texture, out SpriteFrame frame)
            => Frames != null ? Frames(texture, out frame) : SpriteLibrary.Find(texture, out frame);

        /// <summary>A world has been made (the scene has, or had, a 2D body while playing).</summary>
        public static bool Active => _made;

        /// <summary>Bodies in the world, tile map walls included.</summary>
        public static int BodyCount => _byEntity.Count;

        /// <summary>The gravity of the 2D world, in units per second squared. The project's "gravity2D" at start.</summary>
        public static Vector2 Gravity
        {
            get => _gravity;
            set
            {
                _gravity = value;
                if (_made) b2World_SetGravity(_world, new b2Vec2 { x = value.X, y = value.Y });
            }
        }

        private static b2Rot MakeRot(float radians) => new() { c = MathF.Cos(radians), s = MathF.Sin(radians) };
        private static float AngleOf(b2Rot rot) => MathF.Atan2(rot.s, rot.c);

        // ── Start and stop ──────────────────────────────────────────────────────────────

        /// <summary>Play begins: the bodies of the scene's entities. No world is made for a scene that has none.</summary>
        public static void Start(ECSWorld world)
        {
            Stop();
            _gravity = ConfigManager.Config?.Gravity2D ?? new Vector2(0f, -9.81f);
            _refused = false;
            foreach (Entity e in world.Entities) AddEntity(world, e);
        }

        /// <summary>Play ends: the world and everything in it is given back.</summary>
        public static void Stop()
        {
            if (_made) b2DestroyWorld(_world);
            _made = false;
            _world = default;
            _bodies.Clear(); _free.Clear(); _byEntity.Clear(); _touching.Clear(); _waiting.Clear();
            _accumulator = 0f;
        }

        private static bool Ensure()
        {
            if (_made) return true;
            if (string.Equals(ConfigManager.Config?.Physics2D, "none", StringComparison.OrdinalIgnoreCase))
            {
                if (!_refused) Logger.Warning("[Physics2D] The project's 2D physics engine is \"none\": 2D bodies do nothing (Project Settings, physics.engine2D).");
                _refused = true;
                return false;
            }
            b2WorldDef def = b2DefaultWorldDef();
            def.gravity = new b2Vec2 { x = _gravity.X, y = _gravity.Y };
            _world = b2CreateWorld(def);
            _made = true;
            return true;
        }

        // ── Bodies ──────────────────────────────────────────────────────────────────────

        /// <summary>Is there a 2D body for the entity?</summary>
        public static bool Has(Entity entity) => _made && _byEntity.ContainsKey(entity);

        /// <summary>
        /// Makes the body of an entity that has a <see cref="Body2D"/>, or is a tile map with a solid layer (or
        /// both: the walls are then shapes of its own body). An entity with neither, or one that has a body
        /// already, is left alone. A body with no shape of its own is as large as its entity's sprite: when the
        /// sprite's image has not been read yet (a game that has just started), the body is made when it has.
        /// </summary>
        public static void AddEntity(ECSWorld world, Entity entity)
        {
            if (_byEntity.ContainsKey(entity)) return;
            if (!world.TryGetComponent<Transform>(entity, out var tr)) return;
            bool has = world.TryGetComponent<Body2D>(entity, out var body);
            TilemapData? map = world.TryGetComponent<TilemapRenderer>(entity, out var tilemap) ? TilemapLibrary.Map(tilemap.Map) : null;
            bool walls = map != null && HasSolid(map);
            if (!has && !walls) return;
            if (!Ensure()) return;
            if (has && body.Shapes is not { Length: > 0 } && world.TryGetComponent<SpriteRenderer>(entity, out var shown)
                && FindFrame(shown.Texture, out _) == SpriteLibrary.Answer.Loading)
            {
                _waiting.Add(entity);
                return;
            }

            Matrix4x4 model = Transform.GetWorldMatrix(world, tr);
            Decompose(in model, out Vector2 position, out float angle, out Vector2 scale);

            int index = _free.Count > 0 ? _free.Pop() : _bodies.Count;
            if (index == _bodies.Count) _bodies.Add(null);

            b2BodyDef def = b2DefaultBodyDef();
            def.type = !has ? b2BodyType.b2_staticBody : body.Type switch
            {
                Body2DType.Dynamic => b2BodyType.b2_dynamicBody,
                Body2DType.Kinematic => b2BodyType.b2_kinematicBody,
                _ => b2BodyType.b2_staticBody,
            };
            def.position = new b2Vec2 { x = position.X, y = position.Y };
            def.rotation = MakeRot(angle);
            def.userData = (void*)(nint)(index + 1);
            if (has)
            {
                def.gravityScale = body.GravityScale;
                def.linearDamping = body.LinearDamping;
                def.angularDamping = body.AngularDamping;
                def.isBullet = body.Bullet;
                b2MotionLocks locks = def.motionLocks;
                locks.angularZ = body.FixedRotation;
                def.motionLocks = locks;
            }
            var made = new Body { Entity = entity, Id = b2CreateBody(_world, def), Type = has ? body.Type : Body2DType.Static, HasComponent = has, Position = position, Angle = angle };
            _bodies[index] = made;
            _byEntity[entity] = index;

            if (has) AddShapes(world, entity, made, in body, scale, index);
            if (walls) { made.MapKey = tilemap.Map; made.WallScale = scale; BuildWalls(made, map!, index); }
        }

        /// <summary>Takes an entity's body out of the world. Its scripts get no Exit for what it was touching.</summary>
        public static void RemoveEntity(Entity entity)
        {
            _waiting.Remove(entity);
            if (!_made || !_byEntity.Remove(entity, out int index)) return;
            Body body = _bodies[index]!;
            if (b2Body_IsValid(body.Id)) b2DestroyBody(body.Id);
            _bodies[index] = null;
            _free.Push(index);
            _pairs.Clear();
            foreach (var pair in _touching.Keys)
                if (pair.Item1 == index || pair.Item2 == index) _pairs.Add(pair);
            foreach (var pair in _pairs) _touching.Remove(pair);
        }

        // The entity's place in the plane: where it is, how far it is turned about z, and how large it is drawn.
        private static void Decompose(in Matrix4x4 model, out Vector2 position, out float angle, out Vector2 scale)
        {
            position = new Vector2(model.M41, model.M42);
            var axisX = new Vector2(model.M11, model.M12);
            var axisY = new Vector2(model.M21, model.M22);
            angle = MathF.Atan2(axisX.Y, axisX.X);
            scale = new Vector2(MathF.Max(axisX.Length(), 1e-6f), MathF.Max(axisY.Length(), 1e-6f));
        }

        private static b2ShapeDef ShapeDef(in Body2D body, int index, bool hasComponent)
        {
            b2ShapeDef def = b2DefaultShapeDef();
            def.userData = (void*)(nint)(index + 1);
            def.enableContactEvents = true;
            def.enableSensorEvents = true;        // a shape that walks into a trigger is told of, only when it says so
            if (!hasComponent) return def;
            def.density = body.Density;
            b2SurfaceMaterial material = def.material;
            material.friction = body.Friction;
            material.restitution = body.Restitution;
            def.material = material;
            def.isSensor = body.IsTrigger;
            b2Filter filter = def.filter;
            filter.categoryBits = (ulong)(uint)body.Layer;
            filter.maskBits = (ulong)(uint)body.Mask;
            def.filter = filter;
            return def;
        }

        private static void AddShapes(ECSWorld world, Entity entity, Body made, in Body2D body, Vector2 scale, int index)
        {
            b2ShapeDef def = ShapeDef(in body, index, hasComponent: true);
            if (body.Shapes is not { Length: > 0 } shapes)
            {
                // No shape given: a box as large as the entity's sprite, or one unit.
                Vector2 size = Vector2.One, pivot = new(0.5f, 0.5f);
                if (world.TryGetComponent<SpriteRenderer>(entity, out var sprite) && FindFrame(sprite.Texture, out SpriteFrame frame) == SpriteLibrary.Answer.Ready)
                { size = frame.Size; pivot = frame.Pivot; }
                var centre = new Vector2((0.5f - pivot.X) * size.X, (0.5f - pivot.Y) * size.Y) * scale;
                b2Polygon box = b2MakeOffsetBox(size.X * scale.X * 0.5f, size.Y * scale.Y * 0.5f, new b2Vec2 { x = centre.X, y = centre.Y }, MakeRot(0f));
                b2CreatePolygonShape(made.Id, def, box);
                return;
            }
            foreach (Shape2D shape in shapes)
            {
                Vector2 offset = shape.Offset * scale;
                var centre = new b2Vec2 { x = offset.X, y = offset.Y };
                float turn = shape.Angle * (MathF.PI / 180f);
                switch (shape.Type)
                {
                    case Shape2DType.Circle:
                        b2CreateCircleShape(made.Id, def, new b2Circle { center = centre, radius = MathF.Max(shape.Radius * MathF.Max(scale.X, scale.Y), 1e-4f) });
                        break;
                    case Shape2DType.Capsule:
                    {
                        // As wide and as high as its Size: a round-ended box, standing or lying by which is larger.
                        float w = MathF.Max(shape.Size.X * scale.X, 1e-4f), h = MathF.Max(shape.Size.Y * scale.Y, 1e-4f);
                        float radius = MathF.Min(w, h) * 0.5f, half = MathF.Max(w, h) * 0.5f - radius;
                        Vector2 along = h >= w ? new Vector2(-MathF.Sin(turn), MathF.Cos(turn)) : new Vector2(MathF.Cos(turn), MathF.Sin(turn));
                        Vector2 a = offset - along * half, b = offset + along * half;
                        if (half < 1e-5f) b2CreateCircleShape(made.Id, def, new b2Circle { center = centre, radius = radius });
                        else b2CreateCapsuleShape(made.Id, def, new b2Capsule { center1 = new b2Vec2 { x = a.X, y = a.Y }, center2 = new b2Vec2 { x = b.X, y = b.Y }, radius = radius });
                        break;
                    }
                    case Shape2DType.Polygon when shape.Points is { Length: >= 3 } points:
                    {
                        int count = Math.Min(points.Length, 8);
                        b2Vec2* scaled = stackalloc b2Vec2[8];
                        float cos = MathF.Cos(turn), sin = MathF.Sin(turn);
                        for (int i = 0; i < count; i++)
                        {
                            Vector2 p = points[i] * scale;
                            scaled[i] = new b2Vec2 { x = offset.X + p.X * cos - p.Y * sin, y = offset.Y + p.X * sin + p.Y * cos };
                        }
                        b2Hull hull = b2ComputeHull(in scaled[0], count);
                        if (hull.count >= 3) b2CreatePolygonShape(made.Id, def, b2MakePolygon(hull, 0f));
                        else Logger.Warning("[Physics2D] A polygon shape's points do not make a convex shape: it is left out.");
                        break;
                    }
                    default:
                        b2CreatePolygonShape(made.Id, def, b2MakeOffsetBox(MathF.Max(shape.Size.X * scale.X * 0.5f, 1e-4f), MathF.Max(shape.Size.Y * scale.Y * 0.5f, 1e-4f), centre, MakeRot(turn)));
                        break;
                }
            }
        }

        /// <summary>
        /// Where an entity's <see cref="Body2D"/> shapes are in the world, as the world would make them — for an
        /// editor's outlines, with no world needed. Each shape is a closed loop of points appended to
        /// <paramref name="points"/>; <paramref name="loops"/> gets the number of points of each. A circle and a
        /// capsule's ends are drawn with <paramref name="segments"/> points to a full turn.
        /// </summary>
        public static void ShapeOutlines(ECSWorld world, Entity entity, List<Vector2> points, List<int> loops, int segments = 24)
        {
            if (!world.TryGetComponent<Body2D>(entity, out var body) || !world.TryGetComponent<Transform>(entity, out var tr)) return;
            Matrix4x4 model = Transform.GetWorldMatrix(world, tr);
            Decompose(in model, out Vector2 position, out float angle, out Vector2 scale);
            float cosB = MathF.Cos(angle), sinB = MathF.Sin(angle);
            Vector2 ToWorld(Vector2 local) => position + new Vector2(local.X * cosB - local.Y * sinB, local.X * sinB + local.Y * cosB);

            void Box(Vector2 centre, Vector2 half, float turn)
            {
                float c = MathF.Cos(turn), s = MathF.Sin(turn);
                for (int i = 0; i < 4; i++)
                {
                    float x = (i == 0 || i == 3 ? -half.X : half.X), y = (i < 2 ? -half.Y : half.Y);
                    points.Add(ToWorld(centre + new Vector2(x * c - y * s, x * s + y * c)));
                }
                loops.Add(4);
            }

            if (body.Shapes is not { Length: > 0 } shapes)
            {
                Vector2 size = Vector2.One, pivot = new(0.5f, 0.5f);
                if (world.TryGetComponent<SpriteRenderer>(entity, out var sprite) && FindFrame(sprite.Texture, out SpriteFrame frame) == SpriteLibrary.Answer.Ready)
                { size = frame.Size; pivot = frame.Pivot; }
                Box(new Vector2((0.5f - pivot.X) * size.X, (0.5f - pivot.Y) * size.Y) * scale, size * scale * 0.5f, 0f);
                return;
            }
            foreach (Shape2D shape in shapes)
            {
                Vector2 offset = shape.Offset * scale;
                float turn = shape.Angle * (MathF.PI / 180f);
                switch (shape.Type)
                {
                    case Shape2DType.Circle:
                    {
                        float radius = MathF.Max(shape.Radius * MathF.Max(scale.X, scale.Y), 1e-4f);
                        for (int i = 0; i < segments; i++)
                        {
                            float a = i * MathF.Tau / segments;
                            points.Add(ToWorld(offset + new Vector2(MathF.Cos(a), MathF.Sin(a)) * radius));
                        }
                        loops.Add(segments);
                        break;
                    }
                    case Shape2DType.Capsule:
                    {
                        float w = MathF.Max(shape.Size.X * scale.X, 1e-4f), h = MathF.Max(shape.Size.Y * scale.Y, 1e-4f);
                        float radius = MathF.Min(w, h) * 0.5f, half = MathF.Max(w, h) * 0.5f - radius;
                        float lean = h >= w ? turn + MathF.PI * 0.5f : turn;          // the direction from one end to the other
                        var along = new Vector2(MathF.Cos(lean), MathF.Sin(lean));
                        int arc = Math.Max(segments / 2, 2);
                        for (int end = 0; end < 2; end++)
                        {
                            Vector2 centre = offset + along * (end == 0 ? half : -half);
                            float start = lean + (end == 0 ? -MathF.PI * 0.5f : MathF.PI * 0.5f);
                            for (int i = 0; i <= arc; i++)
                            {
                                float a = start + i * MathF.PI / arc;
                                points.Add(ToWorld(centre + new Vector2(MathF.Cos(a), MathF.Sin(a)) * radius));
                            }
                        }
                        loops.Add(2 * (arc + 1));
                        break;
                    }
                    case Shape2DType.Polygon when shape.Points is { Length: >= 3 } corners:
                    {
                        float c = MathF.Cos(turn), s = MathF.Sin(turn);
                        int count = Math.Min(corners.Length, 8);
                        for (int i = 0; i < count; i++)
                        {
                            Vector2 p = corners[i] * scale;
                            points.Add(ToWorld(offset + new Vector2(p.X * c - p.Y * s, p.X * s + p.Y * c)));
                        }
                        loops.Add(count);
                        break;
                    }
                    case Shape2DType.Polygon: break;
                    default:
                        Box(offset, new Vector2(MathF.Max(shape.Size.X * scale.X * 0.5f, 1e-4f), MathF.Max(shape.Size.Y * scale.Y * 0.5f, 1e-4f)), turn);
                        break;
                }
            }
        }

        // ── A tile map's walls ──────────────────────────────────────────────────────────

        private static bool HasSolid(TilemapData map)
        {
            foreach (TileLayer layer in map.Layers)
                if (layer.Solid) return true;
            return false;
        }

        /// <summary>
        /// The boxes that cover a layer's tiles, as few as rows allow: a run of tiles in a row is one box, and the
        /// same run in the row above joins it. Each box is (x, y, width, height) in cells.
        /// </summary>
        public static List<(int X, int Y, int W, int H)> WallBoxes(TileLayer layer)
        {
            var boxes = new List<(int X, int Y, int W, int H)>();
            var open = new List<int>();          // boxes of the row below that a run of this row may join
            var next = new List<int>();
            for (int row = 0; row < layer.Height; row++)
            {
                next.Clear();
                int x = 0;
                while (x < layer.Width)
                {
                    if (layer.Cells[row * layer.Width + x] == 0) { x++; continue; }
                    int start = x;
                    while (x < layer.Width && layer.Cells[row * layer.Width + x] != 0) x++;
                    int joined = -1;
                    foreach (int i in open)
                        if (boxes[i].X == layer.OriginX + start && boxes[i].W == x - start) { joined = i; break; }
                    if (joined >= 0) boxes[joined] = (boxes[joined].X, boxes[joined].Y, boxes[joined].W, boxes[joined].H + 1);
                    else { joined = boxes.Count; boxes.Add((layer.OriginX + start, layer.OriginY + row, x - start, 1)); }
                    next.Add(joined);
                }
                open.Clear();
                open.AddRange(next);
            }
            return boxes;
        }

        private static void BuildWalls(Body body, TilemapData map, int index)
        {
            foreach (b2ShapeId old in body.Walls)
                if (b2Shape_IsValid(old)) b2DestroyShape(old, false);
            body.Walls.Clear();
            body.MapVersion = TilemapLibrary.Version;

            Body2D none = default;
            b2ShapeDef def = ShapeDef(in none, index, hasComponent: false);
            b2SurfaceMaterial material = def.material;
            material.friction = 0.6f;
            def.material = material;
            Vector2 cell = map.Cell * body.WallScale;
            foreach (TileLayer layer in map.Layers)
            {
                if (!layer.Solid) continue;
                foreach (var (x, y, w, h) in WallBoxes(layer))
                {
                    var centre = new b2Vec2 { x = (x + w * 0.5f) * cell.X, y = (y + h * 0.5f) * cell.Y };
                    body.Walls.Add(b2CreatePolygonShape(body.Id, def, b2MakeOffsetBox(w * cell.X * 0.5f, h * cell.Y * 0.5f, centre, MakeRot(0f))));
                }
            }
        }

        // ── The step ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Moves the world on by the frame's time, in whole steps of <see cref="FixedStep"/> (what is left over
        /// waits for the next frame; a frame far too long is not caught up beyond four steps).
        /// </summary>
        public static void Step(ECSWorld world, float deltaTime)
        {
            if (!_made) return;
            if (_waiting.Count > 0)
            {
                // AddEntity keeps the ones whose image is still on its way.
                _arrived.Clear();
                _arrived.AddRange(_waiting);
                _waiting.Clear();
                foreach (Entity e in _arrived) AddEntity(world, e);
            }
            _accumulator += deltaTime;
            int steps = 0;
            while (_accumulator >= FixedStep - 1e-6f && steps < MaxStepsAFrame)
            {
                _accumulator -= FixedStep;
                steps++;
                BeforeStep(world);
                b2World_Step(_world, FixedStep, SubSteps);
                AfterStep(world);
                Events();
            }
            if (_accumulator > FixedStep * MaxStepsAFrame) _accumulator = 0f;
        }

        private static void BeforeStep(ECSWorld world)
        {
            for (int i = 0; i < _bodies.Count; i++)
            {
                if (_bodies[i] is not { } body) continue;
                // A tile map whose cells changed gets its walls again.
                if (body.MapKey != null && body.MapVersion != TilemapLibrary.Version && TilemapLibrary.Map(body.MapKey) is { } map) BuildWalls(body, map, i);

                if (!world.TryGetComponent<Transform>(body.Entity, out var tr)) continue;
                Matrix4x4 model = Transform.GetWorldMatrix(world, tr);
                Decompose(in model, out Vector2 position, out float angle, out _);
                if (Vector2.DistanceSquared(position, body.Position) < 1e-10f && MathF.Abs(MathF.IEEERemainder(angle - body.Angle, MathF.Tau)) < 1e-6f) continue;

                // The Transform was moved by a script (or, for a kinematic body, is how it is driven).
                if (body.Type == Body2DType.Kinematic)
                {
                    // Carried there by its speed over this step, so that what it meets is pushed, not passed through.
                    Vector2 v = (position - body.Position) / FixedStep;
                    b2Body_SetLinearVelocity(body.Id, new b2Vec2 { x = v.X, y = v.Y });
                    b2Body_SetAngularVelocity(body.Id, MathF.IEEERemainder(angle - body.Angle, MathF.Tau) / FixedStep);
                }
                else
                {
                    b2Body_SetTransform(body.Id, new b2Vec2 { x = position.X, y = position.Y }, MakeRot(angle));
                    b2Body_SetAwake(body.Id, true);
                    body.Position = position; body.Angle = angle;
                }
            }
        }

        private static void AfterStep(ECSWorld world)
        {
            for (int i = 0; i < _bodies.Count; i++)
            {
                if (_bodies[i] is not { } body || body.Type == Body2DType.Static) continue;
                b2Vec2 p = b2Body_GetPosition(body.Id);
                float angle = AngleOf(b2Body_GetRotation(body.Id));
                var position = new Vector2(p.x, p.y);
                if (body.Type == Body2DType.Kinematic)
                {
                    // It is where its Transform sent it; it stands still until the Transform moves again.
                    b2Body_SetLinearVelocity(body.Id, default);
                    b2Body_SetAngularVelocity(body.Id, 0f);
                    body.Position = position; body.Angle = angle;
                    continue;
                }
                if (position == body.Position && angle == body.Angle) continue;
                if (!world.TryGetComponentRef<Transform>(body.Entity, out var trRef)) continue;
                ref Transform tr = ref trRef.Value;

                // x, y and the turn about z are the body's; z and any other turn stay the entity's own.
                Matrix4x4 before = Transform.GetWorldMatrix(world, tr);
                Vector3 worldPosition = new(position.X, position.Y, before.M43);
                Quaternion worldRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, angle - body.Angle) * Scene.SceneManager.WorldRotationOf(world, tr);
                (tr.Position, tr.Rotation) = Scene.SceneManager.ToLocal(world, tr.Parent, worldPosition, worldRotation);
                body.Position = position; body.Angle = angle;
            }
        }

        private static bool BodyOf(b2ShapeId shape, out int index)
        {
            index = -1;
            if (!b2Shape_IsValid(shape)) return false;
            index = (int)(nint)b2Shape_GetUserData(shape) - 1;
            return index >= 0 && index < _bodies.Count && _bodies[index] != null;
        }

        private static (int, int) Pair(int a, int b) => a < b ? (a, b) : (b, a);

        // What began and ended in the step, told to the scripts of both entities; and Stay for what goes on touching.
        private static void Events()
        {
            _began.Clear();
            b2ContactEvents contacts = b2World_GetContactEvents(_world);
            for (int i = 0; i < contacts.beginCount; i++)
            {
                if (!BodyOf(contacts.beginEvents[i].shapeIdA, out int a) || !BodyOf(contacts.beginEvents[i].shapeIdB, out int b) || a == b) continue;
                var pair = Pair(a, b);
                _touching.TryGetValue(pair, out int count);
                _touching[pair] = count + 1;
                if (count == 0) _began.Add(pair);
            }
            _pairs.Clear();
            for (int i = 0; i < contacts.endCount; i++)
            {
                // A shape that was destroyed is not valid any more: its body's pairs were taken out with it.
                if (!BodyOf(contacts.endEvents[i].shapeIdA, out int a) || !BodyOf(contacts.endEvents[i].shapeIdB, out int b) || a == b) continue;
                var pair = Pair(a, b);
                if (!_touching.TryGetValue(pair, out int count)) continue;
                if (count > 1) _touching[pair] = count - 1;
                else { _touching.Remove(pair); _pairs.Add(pair); }
            }
            foreach (var (a, b) in _began)
                if (_bodies[a] is { } x && _bodies[b] is { } y) { ScriptSystem.DispatchCollisionEnter(x.Entity, y.Entity); ScriptSystem.DispatchCollisionEnter(y.Entity, x.Entity); }
            foreach (var (a, b) in _pairs)
                if (_bodies[a] is { } x && _bodies[b] is { } y) { ScriptSystem.DispatchCollisionExit(x.Entity, y.Entity); ScriptSystem.DispatchCollisionExit(y.Entity, x.Entity); }
            _pairs.Clear();
            foreach (var pair in _touching.Keys)
                if (!_began.Contains(pair)) _pairs.Add(pair);
            foreach (var (a, b) in _pairs)
                if (_bodies[a] is { } x && _bodies[b] is { } y) { ScriptSystem.DispatchCollisionStay(x.Entity, y.Entity); ScriptSystem.DispatchCollisionStay(y.Entity, x.Entity); }

            b2SensorEvents sensors = b2World_GetSensorEvents(_world);
            for (int i = 0; i < sensors.beginCount; i++)
                if (BodyOf(sensors.beginEvents[i].sensorShapeId, out int s) && BodyOf(sensors.beginEvents[i].visitorShapeId, out int v) && s != v)
                { ScriptSystem.DispatchTriggerEnter(_bodies[s]!.Entity, _bodies[v]!.Entity); ScriptSystem.DispatchTriggerEnter(_bodies[v]!.Entity, _bodies[s]!.Entity); }
            for (int i = 0; i < sensors.endCount; i++)
                if (BodyOf(sensors.endEvents[i].sensorShapeId, out int s) && BodyOf(sensors.endEvents[i].visitorShapeId, out int v) && s != v)
                { ScriptSystem.DispatchTriggerExit(_bodies[s]!.Entity, _bodies[v]!.Entity); ScriptSystem.DispatchTriggerExit(_bodies[v]!.Entity, _bodies[s]!.Entity); }
        }

        // ── What a script asks of its body ──────────────────────────────────────────────

        private static bool Find(Entity entity, out Body body)
        {
            body = null!;
            if (!_made || !_byEntity.TryGetValue(entity, out int index) || _bodies[index] is not { } found) return false;
            body = found;
            return true;
        }

        public static bool TryGetLinearVelocity(Entity entity, out Vector2 velocity)
        {
            velocity = default;
            if (!Find(entity, out Body body)) return false;
            b2Vec2 v = b2Body_GetLinearVelocity(body.Id);
            velocity = new Vector2(v.x, v.y);
            return true;
        }

        public static bool SetLinearVelocity(Entity entity, Vector2 velocity)
        {
            if (!Find(entity, out Body body)) return false;
            b2Body_SetLinearVelocity(body.Id, new b2Vec2 { x = velocity.X, y = velocity.Y });
            b2Body_SetAwake(body.Id, true);
            return true;
        }

        public static bool TryGetAngularVelocity(Entity entity, out float velocity)
        {
            velocity = 0f;
            if (!Find(entity, out Body body)) return false;
            velocity = b2Body_GetAngularVelocity(body.Id);
            return true;
        }

        public static bool SetAngularVelocity(Entity entity, float velocity)
        {
            if (!Find(entity, out Body body)) return false;
            b2Body_SetAngularVelocity(body.Id, velocity);
            b2Body_SetAwake(body.Id, true);
            return true;
        }

        public static bool AddForce(Entity entity, Vector2 force)
        {
            if (!Find(entity, out Body body)) return false;
            b2Body_ApplyForceToCenter(body.Id, new b2Vec2 { x = force.X, y = force.Y }, true);
            return true;
        }

        public static bool AddImpulse(Entity entity, Vector2 impulse)
        {
            if (!Find(entity, out Body body)) return false;
            b2Body_ApplyLinearImpulseToCenter(body.Id, new b2Vec2 { x = impulse.X, y = impulse.Y }, true);
            return true;
        }

        public static bool AddTorque(Entity entity, float torque)
        {
            if (!Find(entity, out Body body)) return false;
            b2Body_ApplyTorque(body.Id, torque, true);
            return true;
        }

        // ── Queries ─────────────────────────────────────────────────────────────────────

        private static b2ShapeId[] _shapes = new b2ShapeId[64];

        // A body's shapes, into _shapes: a tile map's walls can be many.
        private static int ShapesOf(b2BodyId id)
        {
            int count = b2Body_GetShapeCount(id);
            if (count > _shapes.Length) _shapes = new b2ShapeId[count * 2];
            fixed (b2ShapeId* p = _shapes) return b2Body_GetShapes(id, p, _shapes.Length);
        }

        /// <summary>
        /// The first body a ray meets: from <paramref name="origin"/> along <paramref name="direction"/> (any
        /// length) for <paramref name="distance"/> units. Triggers are not met.
        /// </summary>
        public static bool Raycast(Vector2 origin, Vector2 direction, float distance, out Entity hit, out RaycastHit2D info)
        {
            hit = default; info = default;
            float length = direction.Length();
            if (!_made || length < 1e-9f || !(distance > 0f)) return false;
            Vector2 along = direction / length * distance;
            var from = new b2Vec2 { x = origin.X, y = origin.Y };
            var translation = new b2Vec2 { x = along.X, y = along.Y };
            b2RayResult result = b2World_CastRayClosest(_world, from, translation, b2DefaultQueryFilter());
            if (!result.hit) return false;
            if (!b2Shape_IsSensor(result.shapeId))
            {
                if (!BodyOf(result.shapeId, out int index)) return false;
                hit = _bodies[index]!.Entity;
                info = new RaycastHit2D { Point = new Vector2(result.point.x, result.point.y), Normal = new Vector2(result.normal.x, result.normal.y), Distance = result.fraction * distance };
                return true;
            }

            // The nearest thing on the ray is a trigger, which a ray does not meet: every shape that is not one is
            // asked in turn (rare enough not to need the world's own search).
            var input = new b2RayCastInput { origin = from, translation = translation, maxFraction = 1f };
            float nearest = float.MaxValue;
            for (int i = 0; i < _bodies.Count; i++)
            {
                if (_bodies[i] is not { } body) continue;
                int count = ShapesOf(body.Id);
                for (int s = 0; s < count; s++)
                {
                    if (b2Shape_IsSensor(_shapes[s])) continue;
                    b2CastOutput cast = b2Shape_RayCast(_shapes[s], in input);
                    if (!cast.hit || cast.fraction >= nearest) continue;
                    nearest = cast.fraction;
                    hit = body.Entity;
                    info = new RaycastHit2D { Point = new Vector2(cast.point.x, cast.point.y), Normal = new Vector2(cast.normal.x, cast.normal.y), Distance = cast.fraction * distance };
                }
            }
            return nearest != float.MaxValue;
        }

        /// <summary>The entities whose body holds a point. Returns how many.</summary>
        public static int OverlapPoint(Vector2 point, List<Entity> results)
        {
            b2Vec2 p = new() { x = point.X, y = point.Y };
            return Overlap(b2MakeProxy(in p, 1, 0f), results);
        }

        /// <summary>The entities whose body a circle reaches.</summary>
        public static int OverlapCircle(Vector2 centre, float radius, List<Entity> results)
        {
            b2Vec2 p = new() { x = centre.X, y = centre.Y };
            return Overlap(b2MakeProxy(in p, 1, MathF.Max(radius, 0f)), results);
        }

        /// <summary>The entities whose body a box reaches: its middle, its width and height, its turn in degrees.</summary>
        public static int OverlapBox(Vector2 centre, Vector2 size, float angle, List<Entity> results)
        {
            float cos = MathF.Cos(angle * (MathF.PI / 180f)), sin = MathF.Sin(angle * (MathF.PI / 180f));
            Vector2 half = size * 0.5f;
            b2Vec2* corners = stackalloc b2Vec2[4];
            for (int i = 0; i < 4; i++)
            {
                float x = (i == 0 || i == 3 ? -half.X : half.X), y = (i < 2 ? -half.Y : half.Y);
                corners[i] = new b2Vec2 { x = centre.X + x * cos - y * sin, y = centre.Y + x * sin + y * cos };
            }
            return Overlap(b2MakeProxy(in corners[0], 4, 0f), results);
        }

        // Every body one of whose shapes is no farther than nothing from the asked shape (given in world units).
        private static int Overlap(b2ShapeProxy asked, List<Entity> results)
        {
            results.Clear();
            if (!_made) return 0;
            b2Vec2* points = stackalloc b2Vec2[8];
            var input = new b2DistanceInput { proxyA = asked, useRadii = true };
            input.transformA = new b2Transform { p = default, q = MakeRot(0f) };
            for (int i = 0; i < _bodies.Count; i++)
            {
                if (_bodies[i] is not { } body) continue;
                int count = ShapesOf(body.Id);
                input.transformB = b2Body_GetTransform(body.Id);
                for (int s = 0; s < count; s++)
                {
                    switch (b2Shape_GetType(_shapes[s]))
                    {
                        case b2ShapeType.b2_circleShape:
                        {
                            b2Circle circle = b2Shape_GetCircle(_shapes[s]);
                            input.proxyB = b2MakeProxy(in circle.center, 1, circle.radius);
                            break;
                        }
                        case b2ShapeType.b2_capsuleShape:
                        {
                            b2Capsule capsule = b2Shape_GetCapsule(_shapes[s]);
                            points[0] = capsule.center1; points[1] = capsule.center2;
                            input.proxyB = b2MakeProxy(in points[0], 2, capsule.radius);
                            break;
                        }
                        case b2ShapeType.b2_polygonShape:
                        {
                            b2Polygon polygon = b2Shape_GetPolygon(_shapes[s]);
                            int n = Math.Min(polygon.count, 8);
                            for (int k = 0; k < n; k++) points[k] = polygon.vertices[k];
                            input.proxyB = b2MakeProxy(in points[0], n, polygon.radius);
                            break;
                        }
                        default: continue;
                    }
                    b2SimplexCache cache = default;
                    if (b2ShapeDistance(in input, &cache, null, 0).distance > 1e-5f) continue;
                    results.Add(body.Entity);
                    break;
                }
            }
            return results.Count;
        }
    }
}
