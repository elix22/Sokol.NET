
using System.Numerics;
using Sokol;
using static Sokol.SG;
using static Sokol.Utils;
using static Sokol.SApp;
using SharpGLTF.Schema2;
using static pbr_shader_morphing_cs_morphing.Shaders;

public static unsafe partial class SharpGLTFApp
{
    /// <summary>
    /// Render a morphing mesh (without skinning) using pbr-shader-morphing.cs
    /// </summary>
    public static void RenderMorphingMesh(
        Sokol.Mesh mesh,
        SharpGltfNode node,
        Matrix4x4 modelMatrix,
        sg_pipeline pipeline,
        pbr_shader_cs.Shaders.light_params_t lightParams,
        bool useScreenTexture)
    {
        // Vertex shader uniforms
        morphing_vs_params_t vsParams = new morphing_vs_params_t();
        vsParams.model = modelMatrix;
        vsParams.view_proj = state.camera.ViewProj;
        vsParams.eye_pos = state.camera.EyePos;
        vsParams.use_morphing = 1;

        // Set morph weights if mesh has morph targets
        if (mesh.HasMorphTargets && node.CachedGltfNode != null)
        {
            var gltfNode = node.CachedGltfNode;
            var gltfMesh = gltfNode.Mesh;
            
            // Get weights - priority: animator > node > mesh
            IReadOnlyList<float>? weights = null;
            
            if (state.animator != null)
            {
                int nodeIndex = -1;
                var logicalNodes = state.model.ModelRoot.LogicalNodes;
                for (int i = 0; i < logicalNodes.Count; i++)
                {
                    if (logicalNodes[i] == gltfNode)
                    {
                        nodeIndex = i;
                        break;
                    }
                }
                
                if (nodeIndex >= 0)
                {
                    var animatedWeights = state.animator.GetAnimatedMorphWeights(nodeIndex);
                    if (animatedWeights != null && animatedWeights.Length > 0)
                    {
                        weights = animatedWeights;
                    }
                }
            }
            
            // Fall back to static weights if no animation
            if (weights == null)
            {
                if (gltfNode.MorphWeights != null && gltfNode.MorphWeights.Count > 0)
                {
                    weights = gltfNode.MorphWeights;
                }
                else if (gltfMesh != null && gltfMesh.MorphWeights != null && gltfMesh.MorphWeights.Count > 0)
                {
                    weights = gltfMesh.MorphWeights;
                }
            }
            
            // Pack up to 8 weights into 2 vec4s
            if (weights != null && weights.Count > 0)
            {
                for (int i = 0; i < Math.Min(weights.Count, 8); i++)
                {
                    int vec4Index = i / 4;  // 0 or 1
                    int componentIndex = i % 4;  // 0, 1, 2, or 3
                    
                    ref var vec = ref vsParams.u_morphWeights[vec4Index];
                    switch (componentIndex)
                    {
                        case 0: vec.X = weights[i]; break;
                        case 1: vec.Y = weights[i]; break;
                        case 2: vec.Z = weights[i]; break;
                        case 3: vec.W = weights[i]; break;
                    }
                }
            }
        }

        sg_apply_pipeline(pipeline);
        sg_apply_uniforms(UB_morphing_vs_params, SG_RANGE(ref vsParams));

        // Material uniforms
        morphing_metallic_params_t metallicParams = new morphing_metallic_params_t();
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

        sg_apply_uniforms(UB_morphing_metallic_params, SG_RANGE(ref metallicParams));

        // Light uniforms - convert to morphing version
        morphing_light_params_t morphingLightParams = new morphing_light_params_t();
        morphingLightParams.num_lights = lightParams.num_lights;
        morphingLightParams.ambient_strength = lightParams.ambient_strength;
        for (int i = 0; i < 4; i++)
        {
            morphingLightParams.light_positions[i] = lightParams.light_positions[i];
            morphingLightParams.light_directions[i] = lightParams.light_directions[i];
            morphingLightParams.light_colors[i] = lightParams.light_colors[i];
            morphingLightParams.light_params_data[i] = lightParams.light_params_data[i];
        }
        sg_apply_uniforms(UB_morphing_light_params, SG_RANGE(ref morphingLightParams));
        
        // Camera params (required by pbr.glsl) - MORPHING VERSION
        morphing_camera_params_t cameraParams = new morphing_camera_params_t();
        cameraParams.u_Camera = state.camera.EyePos;
        sg_apply_uniforms(UB_morphing_camera_params, SG_RANGE(ref cameraParams));
        
        // IBL params (required by pbr.glsl) - MORPHING VERSION
        morphing_ibl_params_t iblParams = new morphing_ibl_params_t();
        iblParams.u_EnvIntensity = 0.3f;
        iblParams.u_EnvBlurNormalized = 0.0f;
        iblParams.u_MipCount = 1;
        iblParams.u_EnvRotation = Matrix4x4.Identity;
        unsafe {
            iblParams.u_TransmissionFramebufferSize[0] = sapp_width();
            iblParams.u_TransmissionFramebufferSize[1] = sapp_height();
        }
        // Set view and projection matrices for transmission refraction
        iblParams.u_ViewMatrix = state.camera.View;
        iblParams.u_ProjectionMatrix = state.camera.Proj;
        iblParams.u_ModelMatrix = modelMatrix;
        sg_apply_uniforms(UB_morphing_ibl_params, SG_RANGE(ref iblParams));
        
        // Tonemapping params (required by pbr.glsl) - MORPHING VERSION
        morphing_tonemapping_params_t tonemappingParams = new morphing_tonemapping_params_t();
        tonemappingParams.u_Exposure = 1.0f;
        sg_apply_uniforms(UB_morphing_tonemapping_params, SG_RANGE(ref tonemappingParams));
        
        // Rendering flags (required by pbr.glsl) - MORPHING VERSION
        morphing_rendering_flags_t renderingFlags = new morphing_rendering_flags_t();
        renderingFlags.use_ibl = (state.environmentMap != null && state.environmentMap.IsLoaded) ? 1 : 0;
        renderingFlags.use_punctual_lights = 1;
        renderingFlags.use_tonemapping = 0;
        renderingFlags.linear_output = 0;
        renderingFlags.alphamode = mesh.AlphaMode == AlphaMode.MASK ? 1 : (mesh.AlphaMode == AlphaMode.BLEND ? 2 : 0);
        renderingFlags.use_skinning = 0;
        renderingFlags.use_morphing = 1;
        sg_apply_uniforms(UB_morphing_rendering_flags, SG_RANGE(ref renderingFlags));

        // Draw the mesh with morph target texture and optional screen texture
        sg_view screenView = useScreenTexture && state.transmission.screen_color_view.id != 0
            ? state.transmission.screen_color_view
            : default;
        sg_sampler screenSampler = useScreenTexture && state.transmission.sampler.id != 0
            ? state.transmission.sampler
            : default;
        sg_view morphView = state.morphTargetView.id != 0 
            ? state.morphTargetView
            : default;
        sg_sampler morphSampler = state.morphTargetSampler.id != 0 
            ? state.morphTargetSampler 
            : default;
        
        mesh.Draw(pipeline, state.environmentMap, screenView, screenSampler, default, default, morphView, morphSampler);
    }
}
