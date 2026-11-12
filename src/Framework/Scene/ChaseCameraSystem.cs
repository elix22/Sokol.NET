using System;
using System.Numerics;
using Frent;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;

namespace GameEditor.Framework.Scene
{
    /// <summary>
    /// Moves every camera with a <see cref="ChaseCameraComponent"/> behind its kart, once a frame after the physics (a
    /// camera is only seen, so frames are its steps): to its place smoothly, looking past the kart; in front looking back
    /// while the look-back button is held.
    /// </summary>
    public static class ChaseCameraSystem
    {
        internal static void Update(float dt)
        {
            var world = ECSWorld.Instance;
            if (!world.HasAnyComponent<ChaseCameraComponent>()) return;
            foreach (var row in world.Query<ChaseCameraComponent, Transform>().EnumerateWithEntities<ChaseCameraComponent, Transform>())
            {
                ref readonly ChaseCameraComponent c = ref row.Item1.Value;
                if (!Target(world, c.Target, out Entity target) || !world.TryGetComponent<Transform>(target, out var tt)) continue;
                Matrix4x4 m = Transform.GetWorldMatrix(world, tt);
                Vector3 at = m.Translation;
                Vector3 fwd = Vector3.TransformNormal(Vector3.UnitZ, m);
                fwd.Y = 0f;
                fwd = fwd.LengthSquared() > 1e-6f ? Vector3.Normalize(fwd) : Vector3.UnitZ;
                bool back = !string.IsNullOrEmpty(c.LookBack) && Input.InputManager.GetButton(c.LookBack);
                Vector3 dir = back ? -fwd : fwd;
                Vector3 want = at - dir * c.Distance + Vector3.UnitY * c.Height;
                Vector3 look = at + dir * c.LookAhead;
                ref Transform tr = ref row.Item2.Value;
                float k = c.Spring <= 0f || back ? 1f : 1f - MathF.Exp(-c.Spring * dt);
                tr.Position = Vector3.Lerp(tr.Position, want, k);
                Vector3 d = look - tr.Position;
                if (d.LengthSquared() < 1e-6f) continue;
                d = Vector3.Normalize(d);
                // A camera looks along its +Z; a positive pitch turns +Z down.
                tr.Rotation = Quaternion.CreateFromYawPitchRoll(MathF.Atan2(d.X, d.Z), MathF.Asin(Math.Clamp(-d.Y, -1f, 1f)), 0f);
            }
        }

        private static bool Target(ECSWorld world, string? name, out Entity found)
        {
            found = default;
            foreach (var row in world.Query<KartComponent, Transform>().EnumerateWithEntities<KartComponent, Transform>())
            {
                if (string.IsNullOrEmpty(name) ? row.Item1.Value.Player : world.TryGetComponent<NameTag>(row.Entity, out var nt) && nt.Name == name)
                { found = row.Entity; return true; }
            }
            if (string.IsNullOrEmpty(name)) return false;
            foreach (Entity e in world.Entities)
                if (world.TryGetComponent<NameTag>(e, out var n) && n.Name == name) { found = e; return true; }
            return false;
        }
    }
}
