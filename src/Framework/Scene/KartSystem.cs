using System;
using System.Numerics;
using Frent;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Physics;

namespace GameEditor.Framework.Scene
{
    /// <summary>
    /// Drives every <see cref="KartComponent"/> of the scene that plays: before each fixed step of the 3D world (so
    /// a kart drives the same at any frame rate, and on every machine), its suspension, drive, grip and steering.
    /// Nothing here corrects a kart's steering or brings it back to an earlier heading: a kart goes where its input
    /// and the physics take it.
    /// </summary>
    public static class KartSystem
    {
        private static readonly Vector3[] Wheels = new Vector3[4];

        /// <summary>One fixed step of every kart. <paramref name="dt"/>: the step's length.</summary>
        public static void Step(float dt)
        {
            IPhysicsWorld? physics = SceneManager.Physics3D;
            var world = ECSWorld.Instance;
            if (physics == null || dt <= 0f || !world.HasAnyComponent<KartComponent>()) return;

            foreach (var row in world.Query<KartComponent, RigidbodyComponent>().EnumerateWithEntities<KartComponent, RigidbodyComponent>())
            {
                if (!SceneManager.TryGetBody(row.Entity, out PhysicsBodyHandle body)) continue;
                ref readonly RigidbodyComponent rb = ref row.Item2.Value;
                if (rb.MotionType != RigidbodyMotionType.Dynamic) continue;
                // A kart a computer driver drives is not steered by the keys, the player's too (a check's driven run).
                Drive(physics, body, ref row.Item1.Value, MathF.Max(rb.Mass, 0.01f), dt, world.HasComponent<DriverComponent>(row.Entity));
            }
        }

        private static void Drive(IPhysicsWorld physics, PhysicsBodyHandle body, ref KartComponent k, float mass, float dt, bool driven)
        {
            if (k.Player && !driven)
            {
                k.Steer = Input.InputManager.GetAxis("Horizontal");
                float v = Input.InputManager.GetAxis("Vertical");
                k.Throttle = k.AutoThrottle ? (v < -0.1f ? 0f : 1f) : MathF.Max(v, 0f);
                k.Brake = MathF.Max(-v, 0f);
            }
            float throttle = Math.Clamp(k.Throttle, 0f, 1f), brake = Math.Clamp(k.Brake, 0f, 1f), steer = Math.Clamp(k.Steer, -1f, 1f);
            bool held = k.Held;
            if (held) { throttle = 0f; brake = 0f; steer = 0f; }
            if (k.Stunned) throttle = 0f;
            float drive = k.DriveScale > 0f ? k.DriveScale : 1f, grip = k.GripScale > 0f ? k.GripScale : 1f;
            float topSpeed = k.TopSpeed * drive, acceleration = k.Acceleration * (drive > 1f ? 2f : drive);

            Vector3 at = physics.GetPosition(body);
            Quaternion turn = physics.GetRotation(body);
            Vector3 velocity = physics.GetLinearVelocity(body);
            Vector3 spin = physics.GetAngularVelocity(body);
            Vector3 up = Vector3.Transform(Vector3.UnitY, turn);
            Vector3 forward = Vector3.Transform(Vector3.UnitZ, turn);
            // Seen from behind a kart that faces +Z with +Y up, its right is -X (right-handed axes).
            Vector3 right = Vector3.Transform(-Vector3.UnitX, turn);

            // The suspension: a ray down from each wheel's place; a spring and a damper push the body up there.
            Wheels[0] = new Vector3(-k.WheelSpan.X, k.WheelHeight, k.WheelSpan.Y);
            Wheels[1] = new Vector3(k.WheelSpan.X, k.WheelHeight, k.WheelSpan.Y);
            Wheels[2] = new Vector3(-k.WheelSpan.X, k.WheelHeight, -k.WheelSpan.Y);
            Wheels[3] = new Vector3(k.WheelSpan.X, k.WheelHeight, -k.WheelSpan.Y);
            int down = 0;
            Vector3 force = Vector3.Zero, torque = Vector3.Zero;
            float reach = MathF.Max(k.Suspension, 0.01f);
            for (int i = 0; i < 4; i++)
            {
                Vector3 offset = Vector3.Transform(Wheels[i], turn);
                Vector3 wheel = at + offset;
                if (!physics.Raycast(wheel, -up, reach, out RaycastHit hit)) continue;
                down++;
                float pressed = reach - hit.Distance;
                Vector3 wheelVelocity = velocity + Vector3.Cross(spin, offset);
                float push = mass * 0.25f * (k.Spring * pressed - k.Damper * Vector3.Dot(wheelVelocity, up));
                if (push <= 0f) continue;
                Vector3 f = up * push;
                force += f;
                torque += Vector3.Cross(offset, f);
            }
            k.WheelsDown = down;
            float speed = Vector3.Dot(velocity, forward);
            k.Speed = speed;

            if (down > 0)
            {
                // Drive and brake along the kart's forward direction (on its wheels it lies along the ground).
                Vector3 along = forward;
                float change;
                if (held) change = -MathF.Sign(speed) * MathF.Min(k.Braking, MathF.Abs(speed) / dt);   // held: to a standstill, and kept there
                else if (brake > 0f && speed > 0.5f) change = -k.Braking * brake;
                else if (brake > 0f) change = speed > -k.ReverseSpeed ? -k.Acceleration * brake : 0f;
                // Well above its top speed (a boost that ended) it slows as it rolls, down to it; at its top speed it holds it.
                else if (throttle > 0f) change = speed < topSpeed ? acceleration * throttle : speed > topSpeed + 0.5f ? -MathF.Min(k.Coasting, (speed - topSpeed) / dt) : 0f;
                else change = -MathF.Sign(speed) * MathF.Min(k.Coasting, MathF.Abs(speed) / dt);
                // Never past the top speed (or the reverse one) in one step.
                float next = speed + change * dt;
                if (change > 0f && next > topSpeed) change = (topSpeed - speed) / dt;
                if (change < 0f && throttle == 0f && brake > 0f && speed <= 0.5f && next < -k.ReverseSpeed) change = (-k.ReverseSpeed - speed) / dt;
                force += along * (mass * change);

                // Grip: the sideways sliding taken away, at most all of it in one step.
                float sideways = Vector3.Dot(velocity, right);
                float take = MathF.Min(k.Grip * grip * dt, 1f);
                force -= right * (mass * sideways * take / dt);

                // Steering: the turn about the kart's own up axis set to the rate asked for (slower when slow,
                // reversed when going backward); the other turns are the physics'. A turn to the right is a
                // negative turn about +Y.
                float rate = -steer * k.TurnRate * (MathF.PI / 180f) * Math.Clamp(MathF.Abs(speed) / MathF.Max(k.TurnFullSpeed, 0.01f), 0f, 1f) * (speed < 0f ? -1f : 1f);
                float spinUp = Vector3.Dot(spin, up);
                physics.SetAngularVelocity(body, spin + up * (rate - spinUp));
            }

            if (force != Vector3.Zero) physics.AddForce(body, force);
            if (torque != Vector3.Zero) physics.AddTorque(body, torque);
        }
    }
}
