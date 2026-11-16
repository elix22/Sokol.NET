using System;
using System.Numerics;
using Frent;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;

namespace GameEditor.Framework.Scene
{
    /// <summary>
    /// Moves every camera with a <see cref="FollowCameraComponent"/> once a frame, after its target moved (Phase H E3; the
    /// game's IP rules W16, W17, C15, C16): its preset - its own, or the <see cref="CameraZone"/>'s the target stands in -
    /// and its heading eased with the one smoothing; the boom from the aim point as long as one sphere cast allows; the camera
    /// at its end looking at the aim point. Nothing else touches the camera.
    /// </summary>
    public static class FollowCameraSystem
    {
        internal static void Update(float dt)
        {
            var world = ECSWorld.Instance;
            if (dt <= 0f || !world.HasAnyComponent<FollowCameraComponent>()) return;
            foreach (var row in world.Query<FollowCameraComponent, Transform>().EnumerateWithEntities<FollowCameraComponent, Transform>())
            {
                ref FollowCameraComponent c = ref row.Item1.Value;
                if (!Target(world, c.Target, out Entity target) || !world.TryGetComponent<Transform>(target, out var tt)) continue;
                Matrix4x4 tm = Transform.GetWorldMatrix(world, tt);
                Vector3 at = tm.Translation;
                float k = c.Smoothing <= 0f ? 1f : 1f - MathF.Exp(-dt / c.Smoothing);

                // The preset: the zone's the target stands in, else its own.
                float wantDistance = c.Distance, wantHeight = c.Height, wantPitch = c.Pitch, wantFov = c.Fov;
                float? fixedHeading = null;
                if (ZoneAt(world, at, out var zone, out float zoneHeading))
                {
                    wantDistance = zone.Distance; wantHeight = zone.Height; wantPitch = zone.Pitch; wantFov = zone.Fov;
                    if (zone.FixedHeading) fixedHeading = zoneHeading;
                }

                if (!c.Started)
                {
                    Vector3 f = Vector3.TransformNormal(Vector3.UnitZ, tm);
                    c.Heading = MathF.Atan2(f.X, f.Z);
                    c.CurDistance = wantDistance; c.CurHeight = wantHeight; c.CurPitch = wantPitch; c.CurFov = wantFov;
                    c.Boom = wantDistance * Scale(c);
                    c.LastTarget = at;
                    c.Started = true;
                }
                else
                {
                    c.CurDistance += (wantDistance - c.CurDistance) * k;
                    c.CurHeight += (wantHeight - c.CurHeight) * k;
                    c.CurPitch += (wantPitch - c.CurPitch) * k;
                    c.CurFov += (wantFov - c.CurFov) * k;
                }

                // The heading: toward the target's direction of travel (or the zone's), with the one smoothing.
                Vector3 moved = at - c.LastTarget;
                moved.Y = 0f;
                c.LastTarget = at;
                float wantHeading = fixedHeading ?? (moved.Length() / dt > 0.5f ? MathF.Atan2(moved.X, moved.Z) : c.Heading);
                c.Heading += Wrap(wantHeading - c.Heading) * k;
                c.Heading = Wrap(c.Heading);

                // The boom: from the aim point back and up; one sphere cast sets how long it may be.
                Vector3 aim = at + Vector3.UnitY * c.CurHeight;
                float pitch = c.CurPitch * (MathF.PI / 180f);
                Vector3 back = new(-MathF.Sin(c.Heading) * MathF.Cos(pitch), MathF.Sin(pitch), -MathF.Cos(c.Heading) * MathF.Cos(pitch));
                float length = MathF.Max(0f, c.CurDistance * Scale(c));
                float fraction = SceneManager.Physics3D?.CastSphere(aim, back * length, c.Radius, staticOnly: true) ?? 1f;
                float allowed = MathF.Max(MathF.Min(c.MinDistance, length), length * fraction);
                c.Boom = allowed < c.Boom ? allowed : c.Boom + (allowed - c.Boom) * k;   // in at once (never through a wall), out eased

                ref Transform tr = ref row.Item2.Value;
                tr.Position = aim + back * c.Boom;
                // A camera looks along its +Z; a positive pitch turns +Z down.
                tr.Rotation = Quaternion.CreateFromYawPitchRoll(c.Heading, pitch, 0f);
                if (world.TryGetComponent<CameraComponent>(row.Entity, out var cam))
                {
                    float fov = c.CurFov + c.ExtraFov;
                    if (cam.Fov != fov) { cam.Fov = fov; world.AddComponent(row.Entity, cam); }
                }
            }
        }

        private static float Scale(in FollowCameraComponent c) => c.DistanceScale > 0f ? c.DistanceScale : 1f;

        private static float Wrap(float a)
        {
            while (a > MathF.PI) a -= 2f * MathF.PI;
            while (a < -MathF.PI) a += 2f * MathF.PI;
            return a;
        }

        // The first zone whose box holds the point; its heading is its forward's.
        private static bool ZoneAt(ECSWorld world, Vector3 point, out CameraZone zone, out float heading)
        {
            zone = default; heading = 0f;
            if (!world.HasAnyComponent<CameraZone>()) return false;
            foreach (var row in world.Query<CameraZone, Transform>().Enumerate<CameraZone, Transform>())
            {
                if (!Matrix4x4.Decompose(Transform.GetWorldMatrix(world, row.Item2.Value), out _, out var rot, out var pos)) continue;
                var z = row.Item1.Value;
                var local = Vector3.Transform(point - pos, Quaternion.Inverse(rot));
                if (MathF.Abs(local.X) > z.Size.X * 0.5f || MathF.Abs(local.Y) > z.Size.Y * 0.5f || MathF.Abs(local.Z) > z.Size.Z * 0.5f) continue;
                var f = Vector3.Transform(Vector3.UnitZ, rot);
                heading = MathF.Atan2(f.X, f.Z);
                zone = z;
                return true;
            }
            return false;
        }

        private static bool Target(ECSWorld world, string? name, out Entity found)
        {
            found = default;
            if (string.IsNullOrEmpty(name))
            {
                if (!world.HasAnyComponent<CharacterComponent>()) return false;
                foreach (var row in world.Query<CharacterComponent, Transform>().EnumerateWithEntities<CharacterComponent, Transform>()) { found = row.Entity; return true; }
                return false;
            }
            foreach (Entity e in world.Entities)
                if (world.TryGetComponent<NameTag>(e, out var n) && n.Name == name) { found = e; return true; }
            return false;
        }
    }
}
