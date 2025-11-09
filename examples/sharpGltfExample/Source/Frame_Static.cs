using System;
using System.Numerics;
using Sokol;
using static Sokol.SG;
using static Sokol.Utils;
using static Sokol.SApp;
using SharpGLTF.Schema2;
using static pbr_shader_cs.Shaders;

public static unsafe partial class SharpGLTFApp
{
    /// <summary>
    /// Render a static mesh (no skinning, no morphing) using pbr-shader.cs
    /// </summary>
    public static void RenderStaticMesh(
        Sokol.Mesh mesh,
        SharpGltfNode node,
        Matrix4x4 modelMatrix,
        sg_pipeline pipeline,
        light_params_t lightParams,
        bool useScreenTexture)
    {
        // Vertex shader uniforms
        vs_params_t vsParams = new vs_params_t();
        vsParams.model = modelMatrix;
        vsParams.view_proj = state.camera.ViewProj;
        vsParams.eye_pos = state.camera.EyePos;
        vsParams.use_morphing = 0;

        sg_apply_pipeline(pipeline);
        sg_apply_uniforms(UB_vs_params, SG_RANGE(ref vsParams));

        // Material uniforms
        metallic_params_t metallicParams = new metallic_params_t();
        metallicParams.base_color_factor = mesh.BaseColorFactor;
        metallicParams.metallic_factor = mesh.MetallicFactor;
        metallicParams.roughness_factor = mesh.RoughnessFactor;
        metallicParams.emissive_factor = mesh.EmissiveFactor;

        // Set texture availability flags
        metallicParams.has_base_color_tex = mesh.Textures.Count > 0 && mesh.Textures[0] != null ? 1.0f : 0.0f;
        metallicParams.has_metallic_roughness_tex = mesh.Textures.Count > 1 && mesh.Textures[1] != null ? 1.0f : 0.0f;
        metallicParams.has_normal_tex = mesh.Textures.Count > 2 && mesh.Textures[2] != null ? 1.0f : 0.0f;
        metallicParams.has_occlusion_tex = mesh.Textures.Count > 3 && mesh.Textures[3] != null ? 1.0f : 0.0f;
        metallicParams.has_emissive_tex = mesh.Textures.Count > 4 && mesh.Textures[4] != null ? 1.0f : 0.0f;

        // Set alpha cutoff (0.0 for OPAQUE/BLEND, actual value for MASK)
        metallicParams.alpha_cutoff = mesh.AlphaMode == AlphaMode.MASK ? mesh.AlphaCutoff : 0.0f;

        // Set emissive strength (KHR_materials_emissive_strength extension)
        metallicParams.emissive_strength = mesh.EmissiveStrength;

        // Get glass material values (with overrides if enabled)
        var glassValues = GetGlassMaterialValues(mesh);
        
        // Set transmission parameters (KHR_materials_transmission extension)
        metallicParams.transmission_factor = glassValues.transmission;
        metallicParams.ior = glassValues.ior;

        // Set volume absorption parameters (KHR_materials_volume extension - Beer's Law)
        metallicParams.attenuation_color = glassValues.attenuationColor;
        metallicParams.attenuation_distance = glassValues.attenuationDistance;
        metallicParams.thickness_factor = glassValues.thickness;

        // Set clearcoat parameters (KHR_materials_clearcoat extension)
        metallicParams.clearcoat_factor = mesh.ClearcoatFactor;
        metallicParams.clearcoat_roughness = mesh.ClearcoatRoughness;

        // Set texture transforms for all texture types (KHR_texture_transform extension)
        unsafe {
            // Base Color
            metallicParams.base_color_tex_offset[0] = mesh.BaseColorTexOffset.X;
            metallicParams.base_color_tex_offset[1] = mesh.BaseColorTexOffset.Y;
            metallicParams.base_color_tex_scale[0] = mesh.BaseColorTexScale.X;
            metallicParams.base_color_tex_scale[1] = mesh.BaseColorTexScale.Y;
            metallicParams.base_color_tex_rotation = mesh.BaseColorTexRotation;
            
            // Metallic-Roughness
            metallicParams.metallic_roughness_tex_offset[0] = mesh.MetallicRoughnessTexOffset.X;
            metallicParams.metallic_roughness_tex_offset[1] = mesh.MetallicRoughnessTexOffset.Y;
            metallicParams.metallic_roughness_tex_scale[0] = mesh.MetallicRoughnessTexScale.X;
            metallicParams.metallic_roughness_tex_scale[1] = mesh.MetallicRoughnessTexScale.Y;
            metallicParams.metallic_roughness_tex_rotation = mesh.MetallicRoughnessTexRotation;
            
            // Normal
            metallicParams.normal_tex_offset[0] = mesh.NormalTexOffset.X;
            metallicParams.normal_tex_offset[1] = mesh.NormalTexOffset.Y;
            metallicParams.normal_tex_scale[0] = mesh.NormalTexScale.X;
            metallicParams.normal_tex_scale[1] = mesh.NormalTexScale.Y;
            metallicParams.normal_tex_rotation = mesh.NormalTexRotation;
            metallicParams.normal_map_scale = mesh.NormalMapScale;
            
            // Occlusion
            metallicParams.occlusion_tex_offset[0] = mesh.OcclusionTexOffset.X;
            metallicParams.occlusion_tex_offset[1] = mesh.OcclusionTexOffset.Y;
            metallicParams.occlusion_tex_scale[0] = mesh.OcclusionTexScale.X;
            metallicParams.occlusion_tex_scale[1] = mesh.OcclusionTexScale.Y;
            metallicParams.occlusion_tex_rotation = mesh.OcclusionTexRotation;
            
            // Emissive
            metallicParams.emissive_tex_offset[0] = mesh.EmissiveTexOffset.X;
            metallicParams.emissive_tex_offset[1] = mesh.EmissiveTexOffset.Y;
            metallicParams.emissive_tex_scale[0] = mesh.EmissiveTexScale.X;
            metallicParams.emissive_tex_scale[1] = mesh.EmissiveTexScale.Y;
            metallicParams.emissive_tex_rotation = mesh.EmissiveTexRotation;
        }

        // Debug view uniforms
        metallicParams.debug_view_enabled = state.ui.debug_view_enabled;
        metallicParams.debug_view_mode = state.ui.debug_view_mode;

        sg_apply_uniforms(UB_metallic_params, SG_RANGE(ref metallicParams));

        // Light uniforms
        sg_apply_uniforms(UB_light_params, SG_RANGE(ref lightParams));
        
        // Camera params (required by pbr.glsl)
        camera_params_t cameraParams = new camera_params_t();
        cameraParams.u_Camera = state.camera.EyePos;
        sg_apply_uniforms(UB_camera_params, SG_RANGE(ref cameraParams));
        
        // IBL params (required by pbr.glsl)
        ibl_params_t iblParams = new ibl_params_t();
        if (state.environmentMap != null && state.environmentMap.IsLoaded)
        {
            iblParams.u_EnvIntensity = state.environmentMap.Intensity;
            iblParams.u_EnvBlurNormalized = 0.0f;
            iblParams.u_MipCount = state.environmentMap.MipCount;
            iblParams.u_EnvRotation = state.environmentMap.Rotation;
        }
        else
        {
            iblParams.u_EnvIntensity = 1.0f;
            iblParams.u_EnvBlurNormalized = 0.0f;
            iblParams.u_MipCount = 1;
            iblParams.u_EnvRotation = Matrix4x4.Identity;
        }
        unsafe {
            iblParams.u_TransmissionFramebufferSize[0] = sapp_width();
            iblParams.u_TransmissionFramebufferSize[1] = sapp_height();
        }
        // Set view and projection matrices for transmission refraction
        iblParams.u_ViewMatrix = state.camera.View;
        iblParams.u_ProjectionMatrix = state.camera.Proj;
        iblParams.u_ModelMatrix = modelMatrix;
        sg_apply_uniforms(UB_ibl_params, SG_RANGE(ref iblParams));
        
        // Tonemapping params (required by pbr.glsl)
        tonemapping_params_t tonemappingParams = new tonemapping_params_t();
        tonemappingParams.u_Exposure = 1.0f;
        sg_apply_uniforms(UB_tonemapping_params, SG_RANGE(ref tonemappingParams));
        
        // Rendering flags (required by pbr.glsl)
        rendering_flags_t renderingFlags = new rendering_flags_t();
        renderingFlags.use_ibl = (state.environmentMap != null && state.environmentMap.IsLoaded) ? 1 : 0;
        renderingFlags.use_punctual_lights = 1;
        renderingFlags.use_tonemapping = 0;
        renderingFlags.linear_output = 0;
        renderingFlags.alphamode = mesh.AlphaMode == AlphaMode.MASK ? 1 : (mesh.AlphaMode == AlphaMode.BLEND ? 2 : 0);
        renderingFlags.use_skinning = 0;
        renderingFlags.use_morphing = 0;
        sg_apply_uniforms(UB_rendering_flags, SG_RANGE(ref renderingFlags));

        // Draw the mesh with optional screen texture for refraction
        sg_view screenView = useScreenTexture && state.transmission.screen_color_view.id != 0
            ? state.transmission.screen_color_view
            : default;
        sg_sampler screenSampler = useScreenTexture && state.transmission.sampler.id != 0
            ? state.transmission.sampler
            : default;
        
        mesh.Draw(pipeline, state.environmentMap, screenView, screenSampler);
    }
}
