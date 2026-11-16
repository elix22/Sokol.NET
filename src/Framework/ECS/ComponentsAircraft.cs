namespace GameEditor.Framework.ECS.Components
{
    /// <summary>
    /// Arcade flight on one physics body (Phase H E17; the game's IP rules F1-F13): the body moves along its own heading at its
    /// speed, plus its climb rate. Every input is a RATE - a turn rate, a climb rate, a second fixed speed while Fast is held -
    /// never a direction to reach; nothing here reads the camera. Released, bank and pitch come back to level at a fixed rate.
    /// No stall, no spin, no fuel. A helicopter is the same body with a cruise speed of 0 (it hovers; Fast held moves it on).
    /// The entity needs a dynamic Rigidbody; gravity is not used while it flies.
    /// </summary>
    public struct AircraftComponent
    {
        /// <summary>Speed with Fast not held, m/s (0: it hovers - a helicopter).</summary>
        public float CruiseSpeed;
        /// <summary>Speed while Fast is held, m/s (a helicopter's Forward).</summary>
        public float FastSpeed;
        /// <summary>How quickly the speed reaches the one asked for, m/s².</summary>
        public float Acceleration;
        /// <summary>Degrees a second it turns while Turn is held.</summary>
        public float TurnRate;
        /// <summary>Metres a second it climbs or descends while Up or Down is held.</summary>
        public float ClimbRate;
        /// <summary>World height above which Up does nothing, m.</summary>
        public float Ceiling;
        /// <summary>Degrees it leans into a turn (looks only).</summary>
        public float BankAngle;
        /// <summary>Degrees its nose rises climbing, falls descending (looks only).</summary>
        public float PitchAngle;
        /// <summary>Degrees its nose dips at Fast speed (a helicopter moving on; looks only).</summary>
        public float ForwardTilt;
        /// <summary>Degrees a second bank and pitch move toward what is asked (and back to level when let go).</summary>
        public float LevelRate;
        /// <summary>Someone flies it. Not flown: it stays where it is, level, settling onto what is below.</summary>
        public bool Piloted;
        /// <summary>The keys and the game's touch buttons fly it: the Horizontal axis turns, Vertical climbs, <see cref="FastButton"/>
        /// held is Fast. Off: a script sets <see cref="Turn"/>, <see cref="Climb"/> and <see cref="Fast"/>.</summary>
        public bool Player;
        /// <summary>The button held for Fast (a key's name or a UI button's Holds), "Fire1" by default.</summary>
        public string FastButton;

        // ── Input this frame (not saved): -1, 0 or +1 each - a held button, never an amount ──────────────
        /// <summary>-1 turns left, +1 right.</summary>
        public float Turn;
        /// <summary>+1 climbs, -1 descends; 0 holds the height.</summary>
        public float Climb;
        public bool Fast;

        // ── Its flight now (not saved) ───────────────────────────────────────────────────────────
        public float Heading, Speed, Bank, Pitch;
        public bool Started;

        public static AircraftComponent Helicopter => new()
        {
            CruiseSpeed = 0f, FastSpeed = 18f, Acceleration = 8f, TurnRate = 70f, ClimbRate = 6f, Ceiling = 120f,
            BankAngle = 20f, PitchAngle = 8f, ForwardTilt = 12f, LevelRate = 60f, Piloted = false, Player = true, FastButton = "Fire1",
        };

        public static AircraftComponent Default => Helicopter;
    }
}
