using System.Numerics;

namespace GameEditor.Framework.ECS.Components
{
    /// <summary>
    /// A kart: the engine's arcade controller on the entity's 3D body (a dynamic Rigidbody). Before each fixed step of
    /// the physics, four rays from the wheels' places hold the body above the ground with a spring and a damper each;
    /// on the ground a force drives it along its forward direction (+Z) toward the speed the input asks for, a
    /// sideways force holds its sliding down (grip), and it turns at a set rate. In the air it is only carried.
    /// Block "Kart". The handling numbers are written in the scene; the inputs are the game's, each frame.
    /// </summary>
    public struct KartComponent
    {
        /// <summary>Metres a second, forward.</summary>
        public float TopSpeed;
        /// <summary>Metres a second, backward.</summary>
        public float ReverseSpeed;
        /// <summary>Metres a second each second, with the throttle down.</summary>
        public float Acceleration;
        /// <summary>Metres a second each second, with the brake down while going forward.</summary>
        public float Braking;
        /// <summary>Metres a second each second, with neither: rolling to a stop.</summary>
        public float Coasting;
        /// <summary>Degrees a second, full steer.</summary>
        public float TurnRate;
        /// <summary>Below this speed (metres a second) the kart turns more slowly, in proportion; standing, not at all.</summary>
        public float TurnFullSpeed;
        /// <summary>How quickly sideways sliding is taken away, each second (a larger number holds the road harder).</summary>
        public float Grip;
        /// <summary>Half the distance between the left and right wheels, and between the front and back ones (metres).</summary>
        public Vector2 WheelSpan;
        /// <summary>Where the wheels' rays start, below the body's middle (metres, negative is down).</summary>
        public float WheelHeight;
        /// <summary>The longest a suspension reaches below a wheel's place (metres).</summary>
        public float Suspension;
        /// <summary>Spring: metres a second each second, per metre the suspension is pressed in.</summary>
        public float Spring;
        /// <summary>Damper: metres a second each second, per metre a second the wheel moves up or down.</summary>
        public float Damper;
        /// <summary>The player drives it: steer from GetAxis("Horizontal"), throttle and brake from GetAxis("Vertical").</summary>
        public bool Player;
        /// <summary>The player's kart drives on by itself: full throttle unless Vertical asks for the brake (a phone's setting).</summary>
        public bool AutoThrottle;

        // ── The game's inputs, each frame (not written in the scene) ────────────────
        /// <summary>0 to 1.</summary>
        public float Throttle;
        /// <summary>0 to 1: slows a kart going forward; held while standing or going back, drives it backward.</summary>
        public float Brake;
        /// <summary>-1 (left) to 1 (right).</summary>
        public float Steer;
        /// <summary>Held by a race's countdown: braked to a standstill and kept there, the inputs not used (the race sets it).</summary>
        public bool Held;
        /// <summary>Drive (top speed and acceleration) times this — a boost's 1.4, a puddle's 0.4; 0 counts as 1 (the race's items set it).</summary>
        public float DriveScale;
        /// <summary>Grip times this — a puddle's 0.5; 0 counts as 1.</summary>
        public float GripScale;
        /// <summary>No drive at all (hit by a bolt); it rolls on and the physics carries it.</summary>
        public bool Stunned;

        // ── What the controller found in the last step (read only) ─────────────────
        /// <summary>Metres a second along the kart's forward direction (negative: backward).</summary>
        public float Speed;
        /// <summary>How many wheels touch the ground.</summary>
        public int WheelsDown;

        public static KartComponent Default => new()
        {
            TopSpeed = 18f, ReverseSpeed = 6f, Acceleration = 12f, Braking = 24f, Coasting = 3f,
            TurnRate = 100f, TurnFullSpeed = 5f, Grip = 8f,
            WheelSpan = new Vector2(0.55f, 0.75f), WheelHeight = -0.2f, Suspension = 0.5f, Spring = 60f, Damper = 8f,
        };
    }
}
