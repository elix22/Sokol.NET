using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Sokol;
using static Sokol.SG;
using static Sokol.Utils;
using static Sokol.SApp;
using SharpGLTF.Schema2;
using static pbr_shader_skinning_cs_skinning.Shaders;

public static unsafe partial class SharpGLTFApp
{
    /// <summary>
    /// Render a skinned mesh (without morphing) using pbr-shader-skinning.cs
    /// </summary>
    public static void RenderSkinnedMesh(
        Sokol.Mesh mesh,
        SharpGltfNode node,
        Matrix4x4 modelMatrix,
        sg_pipeline pipeline,
        pbr_shader_cs.Shaders.light_params_t lightParams,
        bool useScreenTexture)
    {
        // Vertex shader uniforms
        skinning_vs_params_t vsParams = new skinning_vs_params_t();
        vsParams.model = modelMatrix;
        vsParams.view_proj = state.camera.ViewProj;
        vsParams.eye_pos = state.camera.EyePos;

        // Copy bone matrices
        var boneMatrices = state.animator.GetFinalBoneMatrices();
        var destSpan = MemoryMarshal.CreateSpan(ref vsParams.finalBonesMatrices[0], AnimationConstants.MAX_BONES);
        boneMatrices.AsSpan().CopyTo(destSpan);

        sg_apply_pipeline(pipeline);
        sg_apply_uniforms(UB_skinning_vs_params, SG_RANGE(ref vsParams));

        // Material uniforms
        skinning_metallic_params_t metallicParams = new skinning_metallic_params_t();
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

        // Set texture transform for normal map (KHR_texture_transform extension)
        unsafe {
            metallicParams.normal_tex_offset[0] = mesh.NormalTexOffset.X;
            metallicParams.normal_tex_offset[1] = mesh.NormalTexOffset.Y;
            metallicParams.normal_tex_scale[0] = mesh.NormalTexScale.X;
            metallicParams.normal_tex_scale[1] = mesh.NormalTexScale.Y;
        }
        metallicParams.normal_tex_rotation = mesh.NormalTexRotation;
        metallicParams.normal_map_scale = mesh.NormalMapScale;

        // Debug view uniforms
        metallicParams.debug_view_enabled = state.ui.debug_view_enabled;
        metallicParams.debug_view_mode = state.ui.debug_view_mode;

        sg_apply_uniforms(UB_skinning_metallic_params, SG_RANGE(ref metallicParams));

        // Light uniforms (cast to skinning version)
        skinning_light_params_t skinningLightParams = new skinning_light_params_t();
        skinningLightParams.num_lights = lightParams.num_lights;
        skinningLightParams.ambient_strength = lightParams.ambient_strength;
        for (int i = 0; i < 4; i++)
        {
            skinningLightParams.light_positions[i] = lightParams.light_positions[i];
            skinningLightParams.light_directions[i] = lightParams.light_directions[i];
            skinningLightParams.light_colors[i] = lightParams.light_colors[i];
            skinningLightParams.light_params_data[i] = lightParams.light_params_data[i];
        }
        sg_apply_uniforms(UB_skinning_light_params, SG_RANGE(ref skinningLightParams));
        
        // Camera params (required by pbr.glsl) - SKINNED VERSION
        skinning_camera_params_t cameraParams = new skinning_camera_params_t();
        cameraParams.u_Camera = state.camera.EyePos;
        sg_apply_uniforms(UB_skinning_camera_params, SG_RANGE(ref cameraParams));
        
        // IBL params (required by pbr.glsl) - SKINNED VERSION
        skinning_ibl_params_t iblParams = new skinning_ibl_params_t();
        iblParams.u_EnvIntensity = 0.3f;
        iblParams.u_EnvBlurNormalized = 0.0f;
        iblParams.u_MipCount = 1;
        iblParams.u_EnvRotation = Matrix4x4.Identity;
        unsafe {
            iblParams.u_TransmissionFramebufferSize[0] = sapp_width();
            iblParams.u_TransmissionFramebufferSize[1] = sapp_height();
        }
        sg_apply_uniforms(UB_skinning_ibl_params, SG_RANGE(ref iblParams));
        
        // Tonemapping params (required by pbr.glsl) - SKINNED VERSION
        skinning_tonemapping_params_t tonemappingParams = new skinning_tonemapping_params_t();
        tonemappingParams.u_Exposure = 1.0f;
        sg_apply_uniforms(UB_skinning_tonemapping_params, SG_RANGE(ref tonemappingParams));
        
        // Rendering flags (required by pbr.glsl) - SKINNED VERSION
        skinning_rendering_flags_t renderingFlags = new skinning_rendering_flags_t();
        renderingFlags.use_ibl = (state.environmentMap != null && state.environmentMap.IsLoaded) ? 1 : 0;
        renderingFlags.use_punctual_lights = 1;
        renderingFlags.use_tonemapping = 0;
        renderingFlags.linear_output = 0;
        renderingFlags.alphamode = mesh.AlphaMode == AlphaMode.MASK ? 1 : (mesh.AlphaMode == AlphaMode.BLEND ? 2 : 0);
        renderingFlags.use_skinning = mesh.HasSkinning ? 1 : 0;
        renderingFlags.use_morphing = mesh.HasMorphTargets ? 1 : 0;
        sg_apply_uniforms(UB_skinning_rendering_flags, SG_RANGE(ref renderingFlags));

        // Draw the mesh with joint matrix texture and optional screen texture
        sg_view screenView = useScreenTexture && state.transmission.screen_color_view.id != 0
            ? state.transmission.screen_color_view
            : default;
        sg_sampler screenSampler = useScreenTexture && state.transmission.sampler.id != 0
            ? state.transmission.sampler
            : default;
        sg_view jointView = state.jointMatrixView.id != 0 
            ? state.jointMatrixView
            : default;
        sg_sampler jointSampler = state.jointMatrixSampler.id != 0 
            ? state.jointMatrixSampler 
            : default;
        
        mesh.Draw(pipeline, state.environmentMap, screenView, screenSampler, jointView, jointSampler, default, default);
    }
}
