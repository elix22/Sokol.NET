using System.Numerics;

namespace GameEditor.Framework.ECS.Components
{
    /// <summary>
    /// A third-person camera on a boom (Phase H E3; the game's IP rules W16, W17, C15, C16): behind and above its target,
    /// looking at a fixed point on it (<see cref="Height"/> above its feet). Its heading eases toward the target's direction
    /// of travel with ONE smoothing time; the boom is as long as ONE sphere cast from that point back along it allows (never
    /// shorter than <see cref="MinDistance"/>). Height and pitch never change with speed. A <see cref="CameraZone"/> the target
    /// stands in brings its own distance, height, pitch, field of view and, if it says so, a fixed heading - eased to with
    /// the same smoothing. Nothing else: no look-ahead, no second camera, no test of the wanted place, no cut.
    /// Put it on the entity with the Camera.
    /// </summary>
    public struct FollowCameraComponent
    {
        /// <summary>The name of the entity it follows; empty: the first entity with a Character.</summary>
        public string Target;
        public float Distance;
        /// <summary>The point it looks at, above the target's feet, m.</summary>
        public float Height;
        /// <summary>Degrees the boom looks down.</summary>
        public float Pitch;
        /// <summary>Field of view, degrees (the Camera's).</summary>
        public float Fov;
        /// <summary>The one smoothing time, seconds (heading, a preset's change, the boom growing back); 0: at once.</summary>
        public float Smoothing;
        /// <summary>The radius of the sphere cast along the boom, m.</summary>
        public float Radius;
        public float MinDistance;
        /// <summary>The player's distance setting: near / far (1 = as authored).</summary>
        public float DistanceScale;
        /// <summary>Degrees either side of the camera's forward within which the target's travel turns the camera; travel
        /// farther round (sideways, back) leaves the heading as it is. 180: any travel turns it. A move control read
        /// relative to the camera would otherwise run its target in circles when held sideways.</summary>
        public float FollowAngle;

        /// <summary>Degrees added to the field of view this frame by a script (sprinting widens it a little); not saved.</summary>
        public float ExtraFov;

        // ── Now (not saved) ────────────────────────────────────────────────────────────────
        public float Heading, CurDistance, CurHeight, CurPitch, CurFov, Boom;
        public Vector3 LastTarget;
        public bool Started;

        public static FollowCameraComponent Default => new()
        {
            Target = "", Distance = 4.5f, Height = 1.6f, Pitch = 15f, Fov = 60f, Smoothing = 0.35f, Radius = 0.25f, MinDistance = 0.8f, DistanceScale = 1f, FollowAngle = 180f,
        };
    }

    /// <summary>
    /// A box (its <see cref="Size"/>, turned with the entity) that gives a follow camera its own preset while the target
    /// stands in it (Phase H E3): a stealth site's higher, steeper view; with <see cref="FixedHeading"/> the camera looks along
    /// the zone's forward (+Z) whatever the target does.
    /// </summary>
    public struct CameraZone
    {
        public Vector3 Size;
        public float Distance;
        public float Height;
        public float Pitch;
        public float Fov;
        public bool FixedHeading;

        public static CameraZone Default => new() { Size = new Vector3(10f, 6f, 10f), Distance = 8f, Height = 1.6f, Pitch = 45f, Fov = 60f, FixedHeading = true };
    }
}
