using System.Numerics;
using static Sokol.SG;

namespace GameEditor.Framework.Renderer.Server.Materials
{
    /// <summary>
    /// PBR metallic-roughness material.
    /// Maps directly onto pbr_material_params (binding=1) in pbr.glsl.
    /// Supports the M4 feature set: base colour, metallic/roughness, normal,
    /// occlusion, emissive maps plus optional IBL and CSM shadows.
    /// </summary>
    public sealed class PbrMaterial : Material
    {
        public override MaterialKind Kind => MaterialKind.Pbr;

        /// <summary>G8.10: a shader graph that draws this material (its key, its properties' values); null - the engine's
        /// PBR shader. Its texture properties are this material's map slots (BaseColorMap … EmissiveMap, slots 0-4).</summary>
        public Graph.GraphBinding? Graph;

        // ── Core PBR scalars ────────────────────────────────────────────────
        public Vector4 BaseColorFactor    = Vector4.One;
        public float   MetallicFactor     = 1f;
        public float   RoughnessFactor    = 1f;
        public Vector3 EmissiveFactor     = Vector3.Zero;
        public float   EmissiveStrength   = 1f;
        public float   OcclusionStrength  = 1f;

        // ── Alpha ────────────────────────────────────────────────────────────
        // AlphaMode: 0 = Opaque, 1 = Mask (alpha test), 2 = Blend
        public int   AlphaMode   = 0;
        public float AlphaCutoff = 0.5f;

        // ── Face culling ─────────────────────────────────────────────────────
        // glTF KHR doubleSided — render both faces (no back-face cull).
        public bool DoubleSided = false;

        // ── Normal map ───────────────────────────────────────────────────────
        public float NormalMapScale = 1f;

        // ── Texture views (GPU handles) ──────────────────────────────────────
        // Populated by MaterialRegistry / GltfImporter after texture upload.
        // Zero-initialized; shader checks has_*_tex flag to skip unused slots.
        public sg_view BaseColorMap;
        public sg_view MetallicRoughnessMap;
        public sg_view NormalMap;
        public sg_view OcclusionMap;
        public sg_view EmissiveMap;

        // Shared sampler for all five slots (from TextureCache.DefaultSampler).
        public sg_sampler Sampler;

        // ── Source paths (for reload / TextureCache keying) ──────────────────
        public string BaseColorMapPath         = "";
        public string MetallicRoughnessMapPath = "";
        public string NormalMapPath            = "";
        public string OcclusionMapPath         = "";
        public string EmissiveMapPath          = "";

        // ── Texture-coordinate set indices ───────────────────────────────────
        public float BaseColorTexcoord         = 0f;
        public float MetallicRoughnessTexcoord = 0f;
        public float NormalTexcoord            = 0f;
        public float OcclusionTexcoord         = 0f;
        public float EmissiveTexcoord          = 0f;

        // ── Texture transforms (KHR_texture_transform) ───────────────────────
        public Vector2 BaseColorTexOffset           = Vector2.Zero;
        public float   BaseColorTexRotation         = 0f;
        public Vector2 BaseColorTexScale             = Vector2.One;

        public Vector2 MetallicRoughnessTexOffset   = Vector2.Zero;
        public float   MetallicRoughnessTexRotation = 0f;
        public Vector2 MetallicRoughnessTexScale     = Vector2.One;

        public Vector2 NormalTexOffset   = Vector2.Zero;
        public float   NormalTexRotation = 0f;
        public Vector2 NormalTexScale     = Vector2.One;

        public Vector2 OcclusionTexOffset   = Vector2.Zero;
        public float   OcclusionTexRotation = 0f;
        public Vector2 OcclusionTexScale     = Vector2.One;

        public Vector2 EmissiveTexOffset   = Vector2.Zero;
        public float   EmissiveTexRotation = 0f;
        public Vector2 EmissiveTexScale     = Vector2.One;

        // ── Transmission (KHR_materials_transmission) — screen-space refraction ───
        // TransmissionFactor > 0 routes the material through the post-opaque
        // transmission pass (RenderingServer.RenderTransmission) instead of the
        // regular opaque/transparent queue.
        public float   TransmissionFactor   = 0f;
        public sg_view TransmissionMap;
        public string  TransmissionMapPath  = "";
        public float   TransmissionTexcoord = 0f;

        // ── Volume (KHR_materials_volume) — Beer-Lambert tint over the ray ───────
        public float   ThicknessFactor      = 0f;
        public Vector3 AttenuationColor     = Vector3.One;
        public float   AttenuationDistance  = float.MaxValue;

        // ── Index of refraction (KHR_materials_ior) ──────────────────────────────
        public float   Ior                  = 1.5f;

        // ── Clearcoat (KHR_materials_clearcoat) — thin glossy coat (e.g. car paint) ──
        public float   ClearcoatFactor      = 0f;
        public float   ClearcoatRoughness   = 0f;

        // ── Convenience helpers ──────────────────────────────────────────────
        public bool HasBaseColorMap         => !string.IsNullOrEmpty(BaseColorMapPath);
        public bool HasMetallicRoughnessMap => !string.IsNullOrEmpty(MetallicRoughnessMapPath);
        public bool HasNormalMap            => !string.IsNullOrEmpty(NormalMapPath);
        public bool HasOcclusionMap         => !string.IsNullOrEmpty(OcclusionMapPath);
        public bool HasEmissiveMap          => !string.IsNullOrEmpty(EmissiveMapPath);

        /// <summary>True when alpha blending or alpha-test is required.</summary>
        public bool NeedsBlending => AlphaMode != 0;

        /// <summary>
        /// True when this material refracts the background (KHR_materials_transmission).
        /// Such materials are drawn in the dedicated post-opaque transmission pass — they
        /// are skipped by the normal opaque/transparent queue in <c>SubmitView</c>.
        /// </summary>
        public bool IsTransmissive => TransmissionFactor > 0f;
    }
}
