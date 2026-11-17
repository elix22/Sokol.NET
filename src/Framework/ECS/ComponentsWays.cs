using System.Numerics;

namespace GameEditor.Framework.ECS.Components
{
    /// <summary>What a <see cref="WayGraph"/> is walked or driven by.</summary>
    public enum WayKind { Road = 0, Pavement = 1 }

    /// <summary>
    /// One edge of a way graph: one way, from point <see cref="From"/> to point <see cref="To"/>, at <see cref="Speed"/> m/s.
    /// A two-way street is two edges. <see cref="Crossing"/> marks a pavement edge that crosses a road (people wait there while
    /// a car is near).
    /// </summary>
    public struct WayEdge
    {
        public int From, To;
        public float Speed;
        public bool Crossing;
    }

    /// <summary>
    /// A graph of ways in the world (Phase H gap E4; the game's IP rules W1, W3): its points (world positions) and its one-way
    /// edges. The roads are one flat graph, all in memory; traffic follows it edge by edge and picks its next edge at each
    /// point - it never searches a route and never leaves the graph. The pavements are another graph, walked by people.
    /// </summary>
    public struct WayGraph
    {
        public WayKind Kind;
        public Vector3[] Points;
        public WayEdge[] Edges;

        public static WayGraph Empty(WayKind kind) => new() { Kind = kind, Points = System.Array.Empty<Vector3>(), Edges = System.Array.Empty<WayEdge>() };
    }

    /// <summary>
    /// A car of the traffic (E4): it drives the road graph's edges at the edge's speed (never above its own
    /// <see cref="MaxSpeed"/>), keeps <see cref="Gap"/> metres to the car ahead, waits before a point while another car is in it,
    /// and picks its next edge there at random (not the way back while there is another), and brakes for the player in its
    /// lane. It is moved, not driven by physics: with a kinematic Rigidbody, nothing passes through it; when the player's kart
    /// hits it, it becomes a dynamic body - pushed, it stops - and after a while, far from the player, it is put back on a
    /// road and drives on.
    /// </summary>
    public struct TrafficCar
    {
        public int Edge;
        /// <summary>Metres along its edge.</summary>
        public float Along;
        public float MaxSpeed;
        public float Gap;
        /// <summary>Metres a second it gains or loses speed by, a second.</summary>
        public float Acceleration;
        public int Seed;

        // ── now (not saved) ──
        public float Speed;
        public int Next;            // -1: not chosen yet
        public int Prev;            // the edge it came from (-1: none): its curve through the point between them
        public bool Waiting;
        public uint Random;
        /// <summary>Seconds since the player hit it (0: not hit).</summary>
        public float Struck;
        /// <summary>Kept away (more than the game keeps out): hidden, not moving.</summary>
        public bool Away;

        public static TrafficCar Default => new() { Edge = 0, Along = 0f, MaxSpeed = 10f, Gap = 6f, Acceleration = 4f, Seed = 1, Next = -1, Prev = -1 };
    }

    /// <summary>
    /// A person walking the pavement graph (E4): along its edges at <see cref="Speed"/>, the next edge at random at each point,
    /// waiting before a crossing while a car is within <see cref="WaitRadius"/> metres of it. Its model's clips:
    /// <see cref="WalkClip"/> while it walks, <see cref="IdleClip"/> while it waits.
    /// </summary>
    public struct Walker
    {
        public int Edge;
        public float Along;
        public float Speed;
        public float WaitRadius;
        public string WalkClip;
        public string IdleClip;
        public int Seed;

        // ── now (not saved) ──
        public int Next;
        public bool Waiting;
        public uint Random;
        /// <summary>Metres it has stepped aside from its line (+ to its right), out of the way of something fast.</summary>
        public float Aside;
        /// <summary>Seconds it has waited at this crossing.</summary>
        public float Waited;
        /// <summary>Kept away (more than the game keeps out): hidden, not moving.</summary>
        public bool Away;

        public static Walker Default => new() { Edge = 0, Along = 0f, Speed = 1.4f, WaitRadius = 12f, WalkClip = "walk", IdleClip = "idle", Seed = 1, Next = -1 };
    }
}
