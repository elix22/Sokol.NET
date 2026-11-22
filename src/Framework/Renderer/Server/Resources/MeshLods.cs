// MeshLods.cs — a mesh's index LODs: each level an index list over
// the full mesh's own vertices, with its error (how far its surface is from the full mesh's, in the mesh's units), and the
// choice of a level by that error as the camera sees it.

using System;

namespace GameEditor.Framework.Renderer.Server.Resources
{
    public sealed class MeshLods
    {
        /// <summary>Level 1, 2, … (level 0 is the mesh's own index list).</summary>
        public uint[][] Indices = Array.Empty<uint[]>();
        /// <summary>Each level's error, in the mesh's own units.</summary>
        public float[] Errors = Array.Empty<float>();
    }

    public static class MeshLodPicker
    {
        /// <summary>The largest error on the screen a level may have, in pixels of a 1080-pixel-high picture: the quality
        /// level's LOD error (Quality.Apply sets it). 0 or less: always the full mesh.</summary>
        public static float Pixels = 1f;

        /// <summary>
        /// The coarsest level whose error, at <paramref name="distance"/> and scaled by <paramref name="scale"/> (the entity's
        /// world size per mesh unit), covers fewer than <see cref="Pixels"/> pixels; <paramref name="cotHalfFov"/> is the
        /// projection's vertical scale (1 / tan(fov / 2)). 0: the full mesh.
        /// </summary>
        public static int Pick(MeshResource mesh, float scale, float distance, float cotHalfFov)
        {
            var slots = mesh.LodSlots;
            if (!mesh.HasIndexLods || slots.Length == 0 || Pixels <= 0f) return 0;
            float limit = Limit(scale, distance, cotHalfFov);
            int pick = 0;
            for (int k = 0; k < slots.Length && k < 15; k++)
            {
                if (slots[k] is not { } s || s.LodError > limit) break;
                pick = k + 1;
            }
            return pick;
        }

        /// <summary>As <see cref="Pick(MeshResource, float, float, float)"/> over a list of the levels' errors (a skinned mesh's).</summary>
        public static int Pick(ReadOnlySpan<float> errors, float scale, float distance, float cotHalfFov)
        {
            if (errors.Length == 0 || Pixels <= 0f) return 0;
            float limit = Limit(scale, distance, cotHalfFov);
            int pick = 0;
            for (int k = 0; k < errors.Length && k < 15; k++)
            {
                if (errors[k] > limit) break;
                pick = k + 1;
            }
            return pick;
        }

        // The largest error a level may have, in mesh units: a pixel at this distance is 2 * d * tan(fov/2) / 1080 world units.
        static float Limit(float scale, float distance, float cotHalfFov)
        {
            float pixel = 2f * MathF.Max(distance, 1e-4f) / MathF.Max(cotHalfFov, 1e-4f) / 1080f;
            return Pixels * pixel / MathF.Max(scale, 1e-12f);
        }
    }
}
