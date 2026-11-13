// GltfSceneParts.cs — a glTF file's lights (KHR_lights_punctual) and cameras as the engine's Light and Camera components.
//
// glTF gives a light and a camera on a node, both looking down the node's -Z; the engine's look down their entity's +Z.
// Light comes in light's own units (a sun in lux, a lamp in candela); the engine's intensity is the number an artist
// types - Blender's "strength" for a sun, and for a lamp what lights a wall as its watts do at the same distance.

using System;
using System.Numerics;
using GameEditor.Framework.ECS.Components;

namespace GameEditor.Framework.Renderer.Server.Assets
{
    public static class GltfSceneParts
    {
        /// <summary>Lumens in a watt of light, as glTF writers (Blender's among them) count them.</summary>
        public const float LumensPerWatt = 683f;

        static readonly Quaternion HalfTurn = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI);

        /// <summary>A sun's intensity from its lux: 683 lux is strength 1.</summary>
        public static float SunIntensity(float lux) => lux / LumensPerWatt;

        /// <summary>A point or spot light's intensity from its candela: what falls on a wall at a distance d is
        /// intensity / d², as for a lamp of 4π² × intensity watts in Blender.</summary>
        public static float LampIntensity(float candela) => candela / (LumensPerWatt * MathF.PI);

        /// <summary>A lamp's range: the file's when it gives one, else where the lamp has fallen to a hundredth of a sun
        /// of strength 1 - between 5 and 100.</summary>
        public static float Range(float fileRange, float intensity) =>
            fileRange > 0f ? fileRange : Math.Clamp(MathF.Sqrt(MathF.Max(intensity, 0f) / 0.01f), 5f, 100f);

        /// <summary>The rotation of the entity of a node that carries a light or a camera: the node's, turned half a turn
        /// about its own Y, so that the entity's +Z is the node's -Z.</summary>
        public static Quaternion Turned(Quaternion nodeRotation) => Quaternion.Normalize(nodeRotation * HalfTurn);

        /// <summary>The place and rotation of a child under a <see cref="Turned"/> entity that leave it where the file has it.</summary>
        public static void UnderTurned(ref Vector3 position, ref Quaternion rotation)
        {
            position = Vector3.Transform(position, HalfTurn);            // a half turn is its own inverse
            rotation = Quaternion.Normalize(HalfTurn * rotation);
        }

        /// <summary>The Light of a glTF light. kind: 0 a sun (intensity in lux), 1 a point light, 2 a spot light (candela);
        /// the spot's angles are half-angles in radians. A sun casts shadows; a lamp does when it is asked to.</summary>
        public static LightComponent Light(int kind, Vector3 color, float intensity, float range, float innerAngle, float outerAngle)
        {
            float value = kind == 0 ? SunIntensity(intensity) : LampIntensity(intensity);
            return new LightComponent
            {
                Type = kind == 0 ? LightType.Directional : kind == 1 ? LightType.Point : LightType.Spot,
                Color = color,
                Intensity = value,
                Range = kind == 0 ? 10f : Range(range, value),
                InnerAngle = kind == 2 ? innerAngle * (180f / MathF.PI) : 25f,
                OuterAngle = kind == 2 ? outerAngle * (180f / MathF.PI) : 35f,
                CastsShadows = kind == 0,
                Enabled = true,
            };
        }

        /// <summary>The Camera of a glTF camera: a perspective one by its vertical angle (radians), an orthographic one by
        /// half its height. It is not the main camera: which one a scene plays through is the scene's to say.</summary>
        public static CameraComponent Camera(bool orthographic, float yfovOrHalfHeight, float near, float far)
        {
            return new CameraComponent
            {
                Fov = orthographic ? 60f : yfovOrHalfHeight * (180f / MathF.PI),
                NearZ = near > 0f ? near : 0.1f,
                FarZ = far > near && far > 0f ? far : 1000f,
                IsMain = false,
                IsOrthographic = orthographic,
                OrthoSize = orthographic ? yfovOrHalfHeight : 5f,
                Enabled = true,
            };
        }
    }
}
