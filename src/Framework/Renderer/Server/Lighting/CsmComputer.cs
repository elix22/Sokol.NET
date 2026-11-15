// CsmComputer.cs — Cascaded Shadow Map split and VP matrix computation.
// Uses a practical split scheme (blend of logarithmic + uniform) to produce
// N tight orthographic VP matrices, one per cascade.
// Zero allocations per frame — outputs are pre-allocated fixed-size arrays.

using System;
using System.Numerics;

namespace GameEditor.Framework.Renderer.Server.Lighting
{
    /// <summary>
    /// Computes per-cascade split depths and tight orthographic VP matrices for
    /// Cascaded Shadow Maps (CSM / PSSM).
    ///
    /// Usage each frame:
    ///   csm.Compute(view, proj, near, shadowRange, lightDirection, sliceResolution, numCascades);
    ///   then upload CascadeVP[] into pbr_vs_params.csm_vp and
    ///   SplitDepths[] into pbr_csm_params.csm_split_depths.
    /// </summary>
    public sealed class CsmComputer
    {
        public const int MaxCascades = 4;

        // Blend toward logarithmic split (1.0) vs uniform split (0.0).
        // 0.75 works well for typical outdoor scenes.
        public float Lambda = 0.75f;

        // Additional pull-back factor for the light near-plane (fraction of cascade depth range).
        // Prevents casters behind the camera sub-frustum from dropping out of shadow.
        public float NearPullback = 0.1f;

        // Outputs — populated by Compute(), sized for MaxCascades.
        // Read only the first [cascadeCount] entries after calling Compute().
        public readonly Matrix4x4[] CascadeVP  = new Matrix4x4[MaxCascades];
        public readonly float[]     SplitDepths = new float[MaxCascades];
        /// <summary>Each cascade's texel (world units) and depth range: its shadow biases are sizes in its texels.</summary>
        public readonly float[]     TexelWorld  = new float[MaxCascades];
        public readonly float[]     DepthRange  = new float[MaxCascades];

        // Reused per-frame scratch — avoids allocations.
        private readonly Vector3[] _corners = new Vector3[8];

        // ─────────────────────────────────────────────────────────────────────
        // NDC corners in clip space (shared across all cascades).
        private static ReadOnlySpan<Vector3> NdcCorners => new Vector3[]
        {
            new(-1f, -1f, -1f), new( 1f, -1f, -1f), new( 1f,  1f, -1f), new(-1f,  1f, -1f), // near
            new(-1f, -1f,  1f), new( 1f, -1f,  1f), new( 1f,  1f,  1f), new(-1f,  1f,  1f), // far
        };

        /// <summary>
        /// Compute cascade VP matrices and split depths.
        /// </summary>
        /// <param name="cameraView">Camera view matrix.</param>
        /// <param name="cameraProj">Camera perspective projection matrix.</param>
        /// <param name="nearClip">Camera near plane distance.</param>
        /// <param name="shadowRange">Maximum shadow distance (camera-space Z). Must be &gt; nearClip.</param>
        /// <param name="lightDir">World-space direction the light is shining (normalised, points toward the scene).</param>
        /// <param name="shadowMapSize">Shadow atlas slice resolution (e.g. 2048) for texel snapping.</param>
        /// <param name="cascadeCount">Number of active cascades. Clamped to [1, MaxCascades].</param>
        /// <param name="farClip">The camera's far plane (the frustum's corners are at it); 0: <paramref name="shadowRange"/>.</param>
        public void Compute(
            in Matrix4x4 cameraView,
            in Matrix4x4 cameraProj,
            float nearClip,
            float shadowRange,
            in Vector3 lightDir,
            int shadowMapSize,
            int cascadeCount,
            float farClip = 0f)
        {
            cascadeCount = Math.Clamp(cascadeCount, 1, MaxCascades);

            // Full camera frustum in world space — inverse of view * proj.
            Matrix4x4.Invert(cameraView * cameraProj, out var invVP);

            // Pre-compute all 8 full-frustum world-space corners.
            var ndcCorners = NdcCorners;
            for (int i = 0; i < 8; i++)
            {
                var c = ndcCorners[i];
                var w = Vector4.Transform(new Vector4(c.X, c.Y, c.Z, 1f), invVP);
                _corners[i] = new Vector3(w.X, w.Y, w.Z) / w.W;
            }

            // Light direction the shadow camera looks along (into the scene).
            var lightFwd   = Vector3.Normalize(lightDir);
            var worldUp    = Math.Abs(Vector3.Dot(lightFwd, Vector3.UnitY)) < 0.999f
                           ? Vector3.UnitY
                           : Vector3.UnitX;

            float prevSplit = nearClip;
            // The splits share out the shadow range; the slices are cut along the frustum's edges, which reach the far plane.
            if (farClip <= nearClip) farClip = shadowRange;
            float rangeInv  = 1f / (farClip - nearClip);

            // Hoisted out of the cascade loop: all 8 entries are overwritten every iteration,
            // so the buffer is reused rather than re-allocated on the stack per cascade.
            Span<Vector3> sliceCorners = stackalloc Vector3[8];

            for (int c = 0; c < cascadeCount; c++)
            {
                // ── PSSM split depth ──────────────────────────────────────────
                float t        = (c + 1f) / cascadeCount;
                float logSplit = nearClip * MathF.Pow(shadowRange / nearClip, t);
                float uniSplit = nearClip + (shadowRange - nearClip) * t;
                float splitDepth = Lambda * logSplit + (1f - Lambda) * uniSplit;
                SplitDepths[c] = splitDepth;

                // ── Sub-frustum corners in world space ────────────────────────
                // Lerp along the near-to-far segment for both planes of the slice.
                float tNear = (prevSplit  - nearClip) * rangeInv;
                float tFar  = (splitDepth - nearClip) * rangeInv;

                for (int i = 0; i < 4; i++)
                {
                    var n = _corners[i];
                    var f = _corners[i + 4];
                    sliceCorners[i]     = Vector3.Lerp(n, f, tNear);
                    sliceCorners[i + 4] = Vector3.Lerp(n, f, tFar);
                }

                // ── Stable light view: a right-handed look-at along lightFwd ──
                // (System.Numerics CreateLookAt + CreateOrthographicOffCenter expect the scene
                //  in FRONT at -Z. The previous hand-built basis placed it at +Z, so every
                //  cascade clipped to empty — no shadows. Measure the slice AABB in THIS view.)
                Vector3 centroid = Vector3.Zero;
                for (int i = 0; i < 8; i++) centroid += sliceCorners[i];
                centroid /= 8f;

                float radius = 0f;
                for (int i = 0; i < 8; i++)
                    radius = MathF.Max(radius, Vector3.Distance(centroid, sliceCorners[i]));

                // Eye behind the slice (toward the light) so all casters are in front.
                Vector3 eye   = centroid - lightFwd * (radius + 1f);
                var lightView = Matrix4x4.CreateLookAt(eye, centroid, worldUp);

                float minX = float.MaxValue, maxX = float.MinValue;
                float minY = float.MaxValue, maxY = float.MinValue;
                float minZ = float.MaxValue, maxZ = float.MinValue;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 v = Vector3.Transform(sliceCorners[i], lightView); // view space, -Z forward
                    if (v.X < minX) minX = v.X;  if (v.X > maxX) maxX = v.X;
                    if (v.Y < minY) minY = v.Y;  if (v.Y > maxY) maxY = v.Y;
                    if (v.Z < minZ) minZ = v.Z;  if (v.Z > maxZ) maxZ = v.Z;
                }

                // ── Texel snapping (eliminates shimmer on camera movement) ──
                float worldPerTexelX = (maxX - minX) / shadowMapSize;
                float worldPerTexelY = (maxY - minY) / shadowMapSize;
                if (worldPerTexelX > 0f)
                {
                    minX = MathF.Floor(minX / worldPerTexelX) * worldPerTexelX;
                    maxX = MathF.Ceiling(maxX / worldPerTexelX) * worldPerTexelX;
                }
                if (worldPerTexelY > 0f)
                {
                    minY = MathF.Floor(minY / worldPerTexelY) * worldPerTexelY;
                    maxY = MathF.Ceiling(maxY / worldPerTexelY) * worldPerTexelY;
                }

                // View-Z is negative in front: near = -maxZ (closest), far = -minZ (farthest).
                // Pull the near plane toward the light so casters in front of the slice register.
                float near = MathF.Max(0.01f, -maxZ - radius * NearPullback);
                float far  = -minZ + 1f;

                var lightProj = Matrix4x4.CreateOrthographicOffCenter(minX, maxX, minY, maxY, near, far);
                CascadeVP[c] = lightView * lightProj;
                TexelWorld[c] = MathF.Max(worldPerTexelX, worldPerTexelY);
                DepthRange[c] = far - near;
                prevSplit    = splitDepth;
            }
        }
    }
}
