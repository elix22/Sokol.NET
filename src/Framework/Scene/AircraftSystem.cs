using System;
using System.Numerics;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Physics;

namespace GameEditor.Framework.Scene
{
    /// <summary>
    /// Flies every <see cref="AircraftComponent"/> of the scene that plays, before each fixed step of the 3D world (Phase H
    /// E17): the heading turned at the turn rate while Turn is held, the speed eased at a fixed acceleration toward cruise or
    /// fast, the height changed at the climb rate while Up or Down is held (never above the ceiling); the body's velocity
    /// set to that, its turn to the heading with the bank and pitch the input asks for. Nothing here reads the camera, aims
    /// at anything or corrects the pilot (the game's IP rules F2, F3, F11).
    /// </summary>
    public static class AircraftSystem
    {
        public static void Step(float dt)
        {
            IPhysicsWorld? physics = SceneManager.Physics3D;
            var world = ECSWorld.Instance;
            if (physics == null || dt <= 0f || !world.HasAnyComponent<AircraftComponent>()) return;

            foreach (var row in world.Query<AircraftComponent, RigidbodyComponent>().EnumerateWithEntities<AircraftComponent, RigidbodyComponent>())
            {
                if (row.Item2.Value.MotionType != RigidbodyMotionType.Dynamic) continue;
                if (!SceneManager.TryGetBody(row.Entity, out PhysicsBodyHandle body)) continue;
                Fly(physics, body, ref row.Item1.Value, dt);
            }
        }

        private static float Sign(float v) => v > 0.5f ? 1f : v < -0.5f ? -1f : 0f;

        private static float Toward(float now, float want, float rate) => now < want ? MathF.Min(now + rate, want) : MathF.Max(now - rate, want);

        private static void Fly(IPhysicsWorld physics, PhysicsBodyHandle body, ref AircraftComponent a, float dt)
        {
            Quaternion turn = physics.GetRotation(body);
            if (!a.Started)
            {
                // Its heading from where it faces (+Z forward), level.
                Vector3 f = Vector3.Transform(Vector3.UnitZ, turn);
                a.Heading = MathF.Atan2(f.X, f.Z);
                a.Speed = 0f; a.Bank = 0f; a.Pitch = 0f;
                a.Started = true;
            }

            if (a.Player && a.Piloted)
            {
                a.Turn = Sign(Input.InputManager.GetAxis("Horizontal"));
                a.Climb = Sign(Input.InputManager.GetAxis("Vertical"));
                a.Fast = !string.IsNullOrEmpty(a.FastButton) && Input.InputManager.GetButton(a.FastButton);
            }
            float turnIn = a.Piloted ? Sign(a.Turn) : 0f, climbIn = a.Piloted ? Sign(a.Climb) : 0f;
            bool fast = a.Piloted && a.Fast;

            // A turn to the right (+1) is a negative turn about +Y; the heading is the angle from +Z toward +X.
            a.Heading -= turnIn * a.TurnRate * (MathF.PI / 180f) * dt;
            float wantSpeed = a.Piloted ? (fast ? a.FastSpeed : a.CruiseSpeed) : 0f;
            a.Speed = Toward(a.Speed, MathF.Max(0f, wantSpeed), MathF.Max(0f, a.Acceleration) * dt);

            Vector3 at = physics.GetPosition(body);
            float climb = climbIn * a.ClimbRate;
            if (climb > 0f && at.Y >= a.Ceiling) climb = 0f;               // the flat ceiling: Up does nothing there
            if (!a.Piloted) climb = -2f;                                     // left: it settles onto what is below it
            Vector3 forward = new(MathF.Sin(a.Heading), 0f, MathF.Cos(a.Heading));
            physics.SetLinearVelocity(body, forward * a.Speed + Vector3.UnitY * climb);

            // Looks: lean into the turn, nose up climbing, nose down moving on fast - each at a fixed rate, level when let go.
            float level = MathF.Max(0f, a.LevelRate) * dt;
            float fastShare = a.FastSpeed > 0f ? Math.Clamp(a.Speed / a.FastSpeed, 0f, 1f) : 0f;
            a.Bank = Toward(a.Bank, -turnIn * a.BankAngle, level);
            a.Pitch = Toward(a.Pitch, -climbIn * a.PitchAngle + fastShare * a.ForwardTilt, level);
            const float D = MathF.PI / 180f;
            // A positive pitch (CreateFromYawPitchRoll, about +X) turns +Z down: the nose dips.
            Quaternion want = Quaternion.CreateFromYawPitchRoll(a.Heading, a.Pitch * D, a.Bank * D);

            // The body turned to that in this step (the physics keeps its collisions; nothing else turns it).
            Quaternion delta = Quaternion.Normalize(want * Quaternion.Inverse(turn));
            if (delta.W < 0f) delta = -delta;
            float angle = 2f * MathF.Acos(Math.Clamp(delta.W, -1f, 1f));
            float s = MathF.Sqrt(MathF.Max(0f, 1f - delta.W * delta.W));
            Vector3 axis = s > 1e-5f ? new Vector3(delta.X, delta.Y, delta.Z) / s : Vector3.Zero;
            physics.SetAngularVelocity(body, axis * (angle / dt));
        }
    }
}
