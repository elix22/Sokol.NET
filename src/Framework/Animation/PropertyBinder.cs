// PropertyBinder.cs — a clip's curves bound once to the entities and component fields they animate, then written each
// time the clip is applied (docs: ANIMATION_SYSTEM_IMPLEMENTATION_PLAN.md §17). An explicit table, no reflection: a built
// game is NativeAOT.
//
// The properties (the usual names and units):
//   Transform  LocalPosition.x/y/z, LocalEulerAngles.x/y/z (degrees, the editor's Euler order), LocalScale.x/y/z
//   Light      Intensity, Color.r/g/b, Range, SpotAngle, InnerSpotAngle (full cone angles, degrees: the component
//              keeps half-angles)
//   Camera     FieldOfView (vertical, degrees), OrthographicSize (half the view's height)

using System.Collections.Generic;
using System.Numerics;
using Frent;
using GameEditor.Framework.Core;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;

namespace GameEditor.Framework.Animation
{
    /// <summary>A clip's curves bound to one entity's hierarchy (<see cref="PropertyBinder.Bind"/>).</summary>
    public sealed class BoundClip
    {
        internal struct Target
        {
            public AnimationCurve Curve;
            public Entity Entity;
            public PropertyBinder.Field Field;
            public int Axis;          // 0..2 for x/y/z or r/g/b
            public int Hint;          // the curve's last segment
            public int Euler;         // index into Eulers for LocalEulerAngles, else -1
        }

        internal Target[] Targets = System.Array.Empty<Target>();
        // One rotation per entity whose Euler angles are animated: the angles as the curves leave them, written as one
        // rotation once all three are set. The axes no curve animates keep the angles the entity had when bound.
        internal (Entity Entity, Vector3 Angles)[] Eulers = System.Array.Empty<(Entity, Vector3)>();

        public AnimationClip Clip { get; internal set; } = null!;

        /// <summary>The entities the bound curves write (each once): what an editor's preview keeps and gives back.</summary>
        public IEnumerable<Entity> TargetEntities
        {
            get
            {
                var seen = new HashSet<Entity>();
                foreach (var t in Targets) if (seen.Add(t.Entity)) yield return t.Entity;
            }
        }
        /// <summary>The curves that found their entity and property.</summary>
        public int Bound => Targets.Length;
        /// <summary>The curves that did not (a path naming nothing, a property not known) — each warned once, at the binding.</summary>
        public int Missing { get; internal set; }
    }

    public static class PropertyBinder
    {
        internal enum Field : byte
        {
            Position, EulerAngles, Scale,
            LightIntensity, LightColor, LightRange, LightSpotAngle, LightInnerSpotAngle,
            CameraFov, CameraOrthoSize,
        }

        /// <summary>The field and axis of (component, property); false when not animatable.</summary>
        internal static bool Resolve(string component, string property, out Field field, out int axis)
        {
            axis = 0;
            field = default;
            int dot = property.IndexOf('.');
            string name = dot < 0 ? property : property[..dot];
            string part = dot < 0 ? "" : property[(dot + 1)..];
            switch (component)
            {
                case "Transform":
                    field = name switch { "LocalPosition" => Field.Position, "LocalEulerAngles" => Field.EulerAngles, "LocalScale" => Field.Scale, _ => (Field)255 };
                    axis = part switch { "x" => 0, "y" => 1, "z" => 2, _ => -1 };
                    return (int)field != 255 && axis >= 0;
                case "Light":
                    if (name == "Color") { field = Field.LightColor; axis = part switch { "r" => 0, "g" => 1, "b" => 2, _ => -1 }; return axis >= 0; }
                    field = name switch
                    {
                        "Intensity" => Field.LightIntensity, "Range" => Field.LightRange,
                        "SpotAngle" => Field.LightSpotAngle, "InnerSpotAngle" => Field.LightInnerSpotAngle, _ => (Field)255,
                    };
                    return (int)field != 255 && part.Length == 0;
                case "Camera":
                    field = name switch { "FieldOfView" => Field.CameraFov, "OrthographicSize" => Field.CameraOrthoSize, _ => (Field)255 };
                    return (int)field != 255 && part.Length == 0;
            }
            return false;
        }

        /// <summary>
        /// Binds every curve of <paramref name="clip"/> below <paramref name="root"/>: its path followed by name down the
        /// hierarchy ("" = root), its property resolved. A curve that binds to nothing is skipped and warned once, here.
        /// </summary>
        public static BoundClip Bind(AnimationClip clip, Entity root, ECSWorld? world = null)
        {
            world ??= ECSWorld.Instance;
            var targets = new List<BoundClip.Target>();
            var eulers = new List<(Entity, Vector3)>();
            int missing = 0;
            foreach (var (binding, curve) in clip.Curves)
            {
                string why = "";
                Entity? at = Find(world, root, binding.Path);
                if (at is not { } e) why = $"no entity at the path '{binding.Path}'";
                else if (!Resolve(binding.Component, binding.Property, out Field field, out int axis)) why = $"'{binding.Component}.{binding.Property}' is not animatable";
                else if (!Has(world, e, field)) why = $"the entity has no {binding.Component}";
                else
                {
                    int euler = -1;
                    if (field == Field.EulerAngles)
                    {
                        euler = eulers.FindIndex(x => x.Item1 == e);
                        if (euler < 0) { euler = eulers.Count; eulers.Add((e, world.GetComponent<Transform>(e).EulerAngles)); }
                    }
                    targets.Add(new BoundClip.Target { Curve = curve, Entity = e, Field = field, Axis = axis, Hint = -1, Euler = euler });
                    continue;
                }
                missing++;
                Logger.Warning($"[Animation] '{clip.Name}': the curve {binding} is skipped: {why}.");
            }
            return new BoundClip { Clip = clip, Targets = targets.ToArray(), Eulers = eulers.ToArray(), Missing = missing };
        }

        /// <summary>Writes every bound curve's value at <paramref name="time"/> (seconds, the clip's own time).</summary>
        public static void Apply(BoundClip bound, float time, ECSWorld? world = null)
        {
            world ??= ECSWorld.Instance;
            var targets = bound.Targets;
            for (int i = 0; i < targets.Length; i++)
            {
                ref var t = ref targets[i];
                float v = t.Curve.Evaluate(time, ref t.Hint);
                // TryGetComponentRef: an entity a script destroyed while the clip plays is passed over, not an exception.
                switch (t.Field)
                {
                    case Field.Position: if (world.TryGetComponentRef<Transform>(t.Entity, out var tp)) Set(ref tp.Value.Position, t.Axis, v); break;
                    case Field.Scale: if (world.TryGetComponentRef<Transform>(t.Entity, out var ts)) Set(ref ts.Value.Scale, t.Axis, v); break;
                    case Field.EulerAngles: Set(ref bound.Eulers[t.Euler].Angles, t.Axis, v); break;
                    case Field.CameraFov: if (world.TryGetComponentRef<CameraComponent>(t.Entity, out var cf)) cf.Value.Fov = v; break;
                    case Field.CameraOrthoSize: if (world.TryGetComponentRef<CameraComponent>(t.Entity, out var co)) co.Value.OrthoSize = v; break;
                    default:
                        if (!world.TryGetComponentRef<LightComponent>(t.Entity, out var lr)) break;
                        ref LightComponent l = ref lr.Value;
                        switch (t.Field)
                        {
                            case Field.LightIntensity: l.Intensity = v; break;
                            case Field.LightColor: Set(ref l.Color, t.Axis, v); break;
                            case Field.LightRange: l.Range = v; break;
                            case Field.LightSpotAngle: l.OuterAngle = v * 0.5f; break;
                            case Field.LightInnerSpotAngle: l.InnerAngle = v * 0.5f; break;
                        }
                        break;
                }
            }
            // The Euler angles of each entity, all three set: one rotation.
            foreach (var (e, angles) in bound.Eulers)
                if (world.TryGetComponentRef<Transform>(e, out var tr)) tr.Value.EulerAngles = angles;
        }

        private static void Set(ref Vector3 v, int axis, float value)
        {
            if (axis == 0) v.X = value; else if (axis == 1) v.Y = value; else v.Z = value;
        }

        private static bool Has(ECSWorld world, Entity e, Field f) => f switch
        {
            Field.Position or Field.EulerAngles or Field.Scale => world.HasComponent<Transform>(e),
            Field.CameraFov or Field.CameraOrthoSize => world.HasComponent<CameraComponent>(e),
            _ => world.HasComponent<LightComponent>(e),
        };

        // The entity at path below root: each segment the name of a child (a Transform whose Parent is the one before).
        private static Entity? Find(ECSWorld world, Entity root, string path)
        {
            Entity at = root;
            if (string.IsNullOrEmpty(path)) return at;
            foreach (string name in path.Split('/'))
            {
                Entity? next = null;
                foreach (var e in world.Entities)
                    if (world.TryGetComponent<Transform>(e, out var t) && t.Parent == at
                        && world.TryGetComponent<NameTag>(e, out var n) && n.Name == name) { next = e; break; }
                if (next is not { } found) return null;
                at = found;
            }
            return at;
        }
    }
}
