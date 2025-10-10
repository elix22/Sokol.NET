// machine generated, do not edit
using System;
using System.Runtime.InteropServices;
using M = System.Runtime.InteropServices.MarshalAsAttribute;
using U = System.Runtime.InteropServices.UnmanagedType;

namespace Sokol
{
public static unsafe partial class CGltf
{
public enum cgltf_file_type
{
    cgltf_file_type_invalid,
    cgltf_file_type_gltf,
    cgltf_file_type_glb,
    cgltf_file_type_max_enum,
}
public enum cgltf_result
{
    cgltf_result_success,
    cgltf_result_data_too_short,
    cgltf_result_unknown_format,
    cgltf_result_invalid_json,
    cgltf_result_invalid_gltf,
    cgltf_result_invalid_options,
    cgltf_result_file_not_found,
    cgltf_result_io_error,
    cgltf_result_out_of_memory,
    cgltf_result_legacy_gltf,
    cgltf_result_max_enum,
}
[StructLayout(LayoutKind.Sequential)]
public struct cgltf_memory_options
{
    public delegate* unmanaged<void*, nuint, void*> alloc_func;
    public delegate* unmanaged<void*, void*, void> free_func;
    public void* user_data;
}
[StructLayout(LayoutKind.Sequential)]
public struct cgltf_file_options
{
    public delegate* unmanaged<void *, void *, byte*, nuint*, IntPtr, void*> read;
    public delegate* unmanaged<void *, void *, void*, void> release;
    public void* user_data;
}
[StructLayout(LayoutKind.Sequential)]
public struct cgltf_options
{
    public cgltf_file_type type;
    public nuint json_token_count;
    public cgltf_memory_options memory;
    public cgltf_file_options file;
}
public enum cgltf_buffer_view_type
{
    cgltf_buffer_view_type_invalid,
    cgltf_buffer_view_type_indices,
    cgltf_buffer_view_type_vertices,
    cgltf_buffer_view_type_max_enum,
}
public enum cgltf_attribute_type
{
    cgltf_attribute_type_invalid,
    cgltf_attribute_type_position,
    cgltf_attribute_type_normal,
    cgltf_attribute_type_tangent,
    cgltf_attribute_type_texcoord,
    cgltf_attribute_type_color,
    cgltf_attribute_type_joints,
    cgltf_attribute_type_weights,
    cgltf_attribute_type_custom,
    cgltf_attribute_type_max_enum,
}
public enum cgltf_component_type
{
    cgltf_component_type_invalid,
    cgltf_component_type_r_8,
    cgltf_component_type_r_8u,
    cgltf_component_type_r_16,
    cgltf_component_type_r_16u,
    cgltf_component_type_r_32u,
    cgltf_component_type_r_32f,
    cgltf_component_type_max_enum,
}
public enum cgltf_type
{
    cgltf_type_invalid,
    cgltf_type_scalar,
    cgltf_type_vec2,
    cgltf_type_vec3,
    cgltf_type_vec4,
    cgltf_type_mat2,
    cgltf_type_mat3,
    cgltf_type_mat4,
    cgltf_type_max_enum,
}
public enum cgltf_primitive_type
{
    cgltf_primitive_type_invalid,
    cgltf_primitive_type_points,
    cgltf_primitive_type_lines,
    cgltf_primitive_type_line_loop,
    cgltf_primitive_type_line_strip,
    cgltf_primitive_type_triangles,
    cgltf_primitive_type_triangle_strip,
    cgltf_primitive_type_triangle_fan,
    cgltf_primitive_type_max_enum,
}
public enum cgltf_alpha_mode
{
    cgltf_alpha_mode_opaque,
    cgltf_alpha_mode_mask,
    cgltf_alpha_mode_blend,
    cgltf_alpha_mode_max_enum,
}
public enum cgltf_animation_path_type
{
    cgltf_animation_path_type_invalid,
    cgltf_animation_path_type_translation,
    cgltf_animation_path_type_rotation,
    cgltf_animation_path_type_scale,
    cgltf_animation_path_type_weights,
    cgltf_animation_path_type_max_enum,
}
public enum cgltf_interpolation_type
{
    cgltf_interpolation_type_linear,
    cgltf_interpolation_type_step,
    cgltf_interpolation_type_cubic_spline,
    cgltf_interpolation_type_max_enum,
}
public enum cgltf_camera_type
{
    cgltf_camera_type_invalid,
    cgltf_camera_type_perspective,
    cgltf_camera_type_orthographic,
    cgltf_camera_type_max_enum,
}
public enum cgltf_light_type
{
    cgltf_light_type_invalid,
    cgltf_light_type_directional,
    cgltf_light_type_point,
    cgltf_light_type_spot,
    cgltf_light_type_max_enum,
}
public enum cgltf_data_free_method
{
    cgltf_data_free_method_none,
    cgltf_data_free_method_file_release,
    cgltf_data_free_method_memory_free,
    cgltf_data_free_method_max_enum,
}
[StructLayout(LayoutKind.Sequential)]
public struct cgltf_extras
{
    public nuint start_offset;
    public nuint end_offset;
    public IntPtr data;
}
[StructLayout(LayoutKind.Sequential)]
public struct cgltf_extension
{
    public IntPtr name;
    public IntPtr data;
}
[StructLayout(LayoutKind.Sequential)]
public struct cgltf_buffer
{
    public IntPtr name;
    public nuint size;
    public IntPtr uri;
    public void* data;
    public cgltf_data_free_method data_free_method;
    public cgltf_extras extras;
    public nuint extensions_count;
    public cgltf_extension* extensions;
}
public enum cgltf_meshopt_compression_mode
{
    cgltf_meshopt_compression_mode_invalid,
    cgltf_meshopt_compression_mode_attributes,
    cgltf_meshopt_compression_mode_triangles,
    cgltf_meshopt_compression_mode_indices,
    cgltf_meshopt_compression_mode_max_enum,
}
public enum cgltf_meshopt_compression_filter
{
    cgltf_meshopt_compression_filter_none,
    cgltf_meshopt_compression_filter_octahedral,
    cgltf_meshopt_compression_filter_quaternion,
    cgltf_meshopt_compression_filter_exponential,
    cgltf_meshopt_compression_filter_max_enum,
}
[StructLayout(LayoutKind.Sequential)]
public struct cgltf_meshopt_compression
{
    public cgltf_buffer* buffer;
    public nuint offset;
    public nuint size;
    public nuint stride;
    public nuint count;
    public cgltf_meshopt_compression_mode mode;
    public cgltf_meshopt_compression_filter filter;
}
[StructLayout(LayoutKind.Sequential)]
public struct cgltf_buffer_view
{
    public IntPtr name;
    public cgltf_buffer* buffer;
    public nuint offset;
    public nuint size;
    public nuint stride;
    public cgltf_buffer_view_type type;
    public void* data;
    public int has_meshopt_compression;
    public cgltf_meshopt_compression meshopt_compression;
    public cgltf_extras extras;
    public nuint extensions_count;
    public cgltf_extension* extensions;
}
[StructLayout(LayoutKind.Sequential)]
public struct cgltf_accessor_sparse
{
    public nuint count;
    public cgltf_buffer_view* indices_buffer_view;
    public nuint indices_byte_offset;
    public cgltf_component_type indices_component_type;
    public cgltf_buffer_view* values_buffer_view;
    public nuint values_byte_offset;
}
[StructLayout(LayoutKind.Sequential)]
public struct cgltf_accessor
{
    public IntPtr name;
    public cgltf_component_type component_type;
    public int normalized;
    public cgltf_type type;
    public nuint offset;
    public nuint count;
    public nuint stride;
    public cgltf_buffer_view* buffer_view;
    public int has_min;
    #pragma warning disable 169
    public struct minCollection
    {
        public ref float this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 16)[index];
        private float _item0;
        private float _item1;
        private float _item2;
        private float _item3;
        private float _item4;
        private float _item5;
        private float _item6;
        private float _item7;
        private float _item8;
        private float _item9;
        private float _item10;
        private float _item11;
        private float _item12;
        private float _item13;
        private float _item14;
        private float _item15;
    }
    #pragma warning restore 169
    public minCollection min;
    public int has_max;
    #pragma warning disable 169
    public struct maxCollection
    {
        public ref float this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 16)[index];
        private float _item0;
        private float _item1;
        private float _item2;
        private float _item3;
        private float _item4;
        private float _item5;
        private float _item6;
        private float _item7;
        private float _item8;
        private float _item9;
        private float _item10;
        private float _item11;
        private float _item12;
        private float _item13;
        private float _item14;
        private float _item15;
    }
    #pragma warning restore 169
    public maxCollection max;
    public int is_sparse;
    public cgltf_accessor_sparse sparse;
    public cgltf_extras extras;
    public nuint extensions_count;
    public cgltf_extension* extensions;
}
[StructLayout(LayoutKind.Sequential)]
public struct cgltf_attribute
{
    public IntPtr name;
    public cgltf_attribute_type type;
    public int index;
    public cgltf_accessor* data;
}
[StructLayout(LayoutKind.Sequential)]
public struct cgltf_image
{
    public IntPtr name;
    public IntPtr uri;
    public cgltf_buffer_view* buffer_view;
    public IntPtr mime_type;
    public cgltf_extras extras;
    public nuint extensions_count;
    public cgltf_extension* extensions;
}
public enum cgltf_filter_type
{
    cgltf_filter_type_undefined = 0,
    cgltf_filter_type_nearest = 9728,
    cgltf_filter_type_linear = 9729,
    cgltf_filter_type_nearest_mipmap_nearest = 9984,
    cgltf_filter_type_linear_mipmap_nearest = 9985,
    cgltf_filter_type_nearest_mipmap_linear = 9986,
    cgltf_filter_type_linear_mipmap_linear = 9987,
}
public enum cgltf_wrap_mode
{
    cgltf_wrap_mode_clamp_to_edge = 33071,
    cgltf_wrap_mode_mirrored_repeat = 33648,
    cgltf_wrap_mode_repeat = 10497,
}
[StructLayout(LayoutKind.Sequential)]
public struct cgltf_sampler
{
    public IntPtr name;
    public cgltf_filter_type mag_filter;
    public cgltf_filter_type min_filter;
    public cgltf_wrap_mode wrap_s;
    public cgltf_wrap_mode wrap_t;
    public cgltf_extras extras;
    public nuint extensions_count;
    public cgltf_extension* extensions;
}
[StructLayout(LayoutKind.Sequential)]
public struct cgltf_texture
{
    public IntPtr name;
    public cgltf_image* image;
    public cgltf_sampler* sampler;
    public int has_basisu;
    public cgltf_image* basisu_image;
    public int has_webp;
    public cgltf_image* webp_image;
    public cgltf_extras extras;
    public nuint extensions_count;
    public cgltf_extension* extensions;
}
[StructLayout(LayoutKind.Sequential)]
public struct cgltf_texture_transform
{
    #pragma warning disable 169
    public struct offsetCollection
    {
        public ref float this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 2)[index];
        private float _item0;
        private float _item1;
    }
    #pragma warning restore 169
    public offsetCollection offset;
    public float rotation;
    #pragma warning disable 169
    public struct scaleCollection
    {
        public ref float this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 2)[index];
        private float _item0;
        private float _item1;
    }
    #pragma warning restore 169
    public scaleCollection scale;
    public int has_texcoord;
    public int texcoord;
}
[StructLayout(LayoutKind.Sequential)]
public struct cgltf_texture_view
{
    public cgltf_texture* texture;
    public int texcoord;
    public float scale;
    public int has_transform;
    public cgltf_texture_transform transform;
}
[StructLayout(LayoutKind.Sequential)]
public struct cgltf_pbr_metallic_roughness
{
    public cgltf_texture_view base_color_texture;
    public cgltf_texture_view metallic_roughness_texture;
    #pragma warning disable 169
    public struct base_color_factorCollection
    {
        public ref float this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 4)[index];
        private float _item0;
        private float _item1;
        private float _item2;
        private float _item3;
    }
    #pragma warning restore 169
    public base_color_factorCollection base_color_factor;
    public float metallic_factor;
    public float roughness_factor;
}
[StructLayout(LayoutKind.Sequential)]
public struct cgltf_pbr_specular_glossiness
{
    public cgltf_texture_view diffuse_texture;
    public cgltf_texture_view specular_glossiness_texture;
    #pragma warning disable 169
    public struct diffuse_factorCollection
    {
        public ref float this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 4)[index];
        private float _item0;
        private float _item1;
        private float _item2;
        private float _item3;
    }
    #pragma warning restore 169
    public diffuse_factorCollection diffuse_factor;
    #pragma warning disable 169
    public struct specular_factorCollection
    {
        public ref float this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 3)[index];
        private float _item0;
        private float _item1;
        private float _item2;
    }
    #pragma warning restore 169
    public specular_factorCollection specular_factor;
    public float glossiness_factor;
}
[StructLayout(LayoutKind.Sequential)]
public struct cgltf_clearcoat
{
    public cgltf_texture_view clearcoat_texture;
    public cgltf_texture_view clearcoat_roughness_texture;
    public cgltf_texture_view clearcoat_normal_texture;
    public float clearcoat_factor;
    public float clearcoat_roughness_factor;
}
[StructLayout(LayoutKind.Sequential)]
public struct cgltf_transmission
{
    public cgltf_texture_view transmission_texture;
    public float transmission_factor;
}
[StructLayout(LayoutKind.Sequential)]
public struct cgltf_ior
{
    public float ior;
}
[StructLayout(LayoutKind.Sequential)]
public struct cgltf_specular
{
    public cgltf_texture_view specular_texture;
    public cgltf_texture_view specular_color_texture;
    #pragma warning disable 169
    public struct specular_color_factorCollection
    {
        public ref float this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 3)[index];
        private float _item0;
        private float _item1;
        private float _item2;
    }
    #pragma warning restore 169
    public specular_color_factorCollection specular_color_factor;
    public float specular_factor;
}
[StructLayout(LayoutKind.Sequential)]
public struct cgltf_volume
{
    public cgltf_texture_view thickness_texture;
    public float thickness_factor;
    #pragma warning disable 169
    public struct attenuation_colorCollection
    {
        public ref float this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 3)[index];
        private float _item0;
        private float _item1;
        private float _item2;
    }
    #pragma warning restore 169
    public attenuation_colorCollection attenuation_color;
    public float attenuation_distance;
}
[StructLayout(LayoutKind.Sequential)]
public struct cgltf_sheen
{
    public cgltf_texture_view sheen_color_texture;
    #pragma warning disable 169
    public struct sheen_color_factorCollection
    {
        public ref float this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 3)[index];
        private float _item0;
        private float _item1;
        private float _item2;
    }
    #pragma warning restore 169
    public sheen_color_factorCollection sheen_color_factor;
    public cgltf_texture_view sheen_roughness_texture;
    public float sheen_roughness_factor;
}
[StructLayout(LayoutKind.Sequential)]
public struct cgltf_emissive_strength
{
    public float emissive_strength;
}
[StructLayout(LayoutKind.Sequential)]
public struct cgltf_iridescence
{
    public float iridescence_factor;
    public cgltf_texture_view iridescence_texture;
    public float iridescence_ior;
    public float iridescence_thickness_min;
    public float iridescence_thickness_max;
    public cgltf_texture_view iridescence_thickness_texture;
}
[StructLayout(LayoutKind.Sequential)]
public struct cgltf_diffuse_transmission
{
    public cgltf_texture_view diffuse_transmission_texture;
    public float diffuse_transmission_factor;
    #pragma warning disable 169
    public struct diffuse_transmission_color_factorCollection
    {
        public ref float this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 3)[index];
        private float _item0;
        private float _item1;
        private float _item2;
    }
    #pragma warning restore 169
    public diffuse_transmission_color_factorCollection diffuse_transmission_color_factor;
    public cgltf_texture_view diffuse_transmission_color_texture;
}
[StructLayout(LayoutKind.Sequential)]
public struct cgltf_anisotropy
{
    public float anisotropy_strength;
    public float anisotropy_rotation;
    public cgltf_texture_view anisotropy_texture;
}
[StructLayout(LayoutKind.Sequential)]
public struct cgltf_dispersion
{
    public float dispersion;
}
[StructLayout(LayoutKind.Sequential)]
public struct cgltf_material
{
    public IntPtr name;
    public int has_pbr_metallic_roughness;
    public int has_pbr_specular_glossiness;
    public int has_clearcoat;
    public int has_transmission;
    public int has_volume;
    public int has_ior;
    public int has_specular;
    public int has_sheen;
    public int has_emissive_strength;
    public int has_iridescence;
    public int has_diffuse_transmission;
    public int has_anisotropy;
    public int has_dispersion;
    public cgltf_pbr_metallic_roughness pbr_metallic_roughness;
    public cgltf_pbr_specular_glossiness pbr_specular_glossiness;
    public cgltf_clearcoat clearcoat;
    public cgltf_ior ior;
    public cgltf_specular specular;
    public cgltf_sheen sheen;
    public cgltf_transmission transmission;
    public cgltf_volume volume;
    public cgltf_emissive_strength emissive_strength;
    public cgltf_iridescence iridescence;
    public cgltf_diffuse_transmission diffuse_transmission;
    public cgltf_anisotropy anisotropy;
    public cgltf_dispersion dispersion;
    public cgltf_texture_view normal_texture;
    public cgltf_texture_view occlusion_texture;
    public cgltf_texture_view emissive_texture;
    #pragma warning disable 169
    public struct emissive_factorCollection
    {
        public ref float this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 3)[index];
        private float _item0;
        private float _item1;
        private float _item2;
    }
    #pragma warning restore 169
    public emissive_factorCollection emissive_factor;
    public cgltf_alpha_mode alpha_mode;
    public float alpha_cutoff;
    public int double_sided;
    public int unlit;
    public cgltf_extras extras;
    public nuint extensions_count;
    public cgltf_extension* extensions;
}
[StructLayout(LayoutKind.Sequential)]
public struct cgltf_material_mapping
{
    public nuint variant;
    public cgltf_material* material;
    public cgltf_extras extras;
}
[StructLayout(LayoutKind.Sequential)]
public struct cgltf_morph_target
{
    public cgltf_attribute* attributes;
    public nuint attributes_count;
}
[StructLayout(LayoutKind.Sequential)]
public struct cgltf_draco_mesh_compression
{
    public cgltf_buffer_view* buffer_view;
    public cgltf_attribute* attributes;
    public nuint attributes_count;
}
[StructLayout(LayoutKind.Sequential)]
public struct cgltf_mesh_gpu_instancing
{
    public cgltf_attribute* attributes;
    public nuint attributes_count;
}
[StructLayout(LayoutKind.Sequential)]
public struct cgltf_primitive
{
    public cgltf_primitive_type type;
    public cgltf_accessor* indices;
    public cgltf_material* material;
    public cgltf_attribute* attributes;
    public nuint attributes_count;
    public cgltf_morph_target* targets;
    public nuint targets_count;
    public cgltf_extras extras;
    public int has_draco_mesh_compression;
    public cgltf_draco_mesh_compression draco_mesh_compression;
    public cgltf_material_mapping* mappings;
    public nuint mappings_count;
    public nuint extensions_count;
    public cgltf_extension* extensions;
}
[StructLayout(LayoutKind.Sequential)]
public struct cgltf_mesh
{
    public IntPtr name;
    public cgltf_primitive* primitives;
    public nuint primitives_count;
    public float * weights;
    public nuint weights_count;
    public IntPtr target_names;
    public nuint target_names_count;
    public cgltf_extras extras;
    public nuint extensions_count;
    public cgltf_extension* extensions;
}
[StructLayout(LayoutKind.Sequential)]
public struct cgltf_node_
{
}
[StructLayout(LayoutKind.Sequential)]
public struct cgltf_skin
{
    public IntPtr name;
    public cgltf_node** joints;
    public nuint joints_count;
    public cgltf_node* skeleton;
    public cgltf_accessor* inverse_bind_matrices;
    public cgltf_extras extras;
    public nuint extensions_count;
    public cgltf_extension* extensions;
}
[StructLayout(LayoutKind.Sequential)]
public struct cgltf_camera_perspective
{
    public int has_aspect_ratio;
    public float aspect_ratio;
    public float yfov;
    public int has_zfar;
    public float zfar;
    public float znear;
    public cgltf_extras extras;
}
[StructLayout(LayoutKind.Sequential)]
public struct cgltf_camera_orthographic
{
    public float xmag;
    public float ymag;
    public float zfar;
    public float znear;
    public cgltf_extras extras;
}
[StructLayout(LayoutKind.Sequential)]
public struct cgltf_light
{
    public IntPtr name;
    #pragma warning disable 169
    public struct colorCollection
    {
        public ref float this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 3)[index];
        private float _item0;
        private float _item1;
        private float _item2;
    }
    #pragma warning restore 169
    public colorCollection color;
    public float intensity;
    public cgltf_light_type type;
    public float range;
    public float spot_inner_cone_angle;
    public float spot_outer_cone_angle;
    public cgltf_extras extras;
}
[StructLayout(LayoutKind.Sequential)]
public struct cgltf_node
{
    public IntPtr name;
    public cgltf_node* parent;
    public cgltf_node** children;
    public nuint children_count;
    public cgltf_skin* skin;
    public cgltf_mesh* mesh;
    public cgltf_camera* camera;
    public cgltf_light* light;
    public float * weights;
    public nuint weights_count;
    public int has_translation;
    public int has_rotation;
    public int has_scale;
    public int has_matrix;
    #pragma warning disable 169
    public struct translationCollection
    {
        public ref float this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 3)[index];
        private float _item0;
        private float _item1;
        private float _item2;
    }
    #pragma warning restore 169
    public translationCollection translation;
    #pragma warning disable 169
    public struct rotationCollection
    {
        public ref float this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 4)[index];
        private float _item0;
        private float _item1;
        private float _item2;
        private float _item3;
    }
    #pragma warning restore 169
    public rotationCollection rotation;
    #pragma warning disable 169
    public struct scaleCollection
    {
        public ref float this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 3)[index];
        private float _item0;
        private float _item1;
        private float _item2;
    }
    #pragma warning restore 169
    public scaleCollection scale;
    #pragma warning disable 169
    public struct matrixCollection
    {
        public ref float this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 16)[index];
        private float _item0;
        private float _item1;
        private float _item2;
        private float _item3;
        private float _item4;
        private float _item5;
        private float _item6;
        private float _item7;
        private float _item8;
        private float _item9;
        private float _item10;
        private float _item11;
        private float _item12;
        private float _item13;
        private float _item14;
        private float _item15;
    }
    #pragma warning restore 169
    public matrixCollection matrix;
    public cgltf_extras extras;
    public int has_mesh_gpu_instancing;
    public cgltf_mesh_gpu_instancing mesh_gpu_instancing;
    public nuint extensions_count;
    public cgltf_extension* extensions;
}
[StructLayout(LayoutKind.Sequential)]
public struct cgltf_scene
{
    public IntPtr name;
    public cgltf_node** nodes;
    public nuint nodes_count;
    public cgltf_extras extras;
    public nuint extensions_count;
    public cgltf_extension* extensions;
}
[StructLayout(LayoutKind.Sequential)]
public struct cgltf_animation_sampler
{
    public cgltf_accessor* input;
    public cgltf_accessor* output;
    public cgltf_interpolation_type interpolation;
    public cgltf_extras extras;
    public nuint extensions_count;
    public cgltf_extension* extensions;
}
[StructLayout(LayoutKind.Sequential)]
public struct cgltf_animation_channel
{
    public cgltf_animation_sampler* sampler;
    public cgltf_node* target_node;
    public cgltf_animation_path_type target_path;
    public cgltf_extras extras;
    public nuint extensions_count;
    public cgltf_extension* extensions;
}
[StructLayout(LayoutKind.Sequential)]
public struct cgltf_animation
{
    public IntPtr name;
    public cgltf_animation_sampler* samplers;
    public nuint samplers_count;
    public cgltf_animation_channel* channels;
    public nuint channels_count;
    public cgltf_extras extras;
    public nuint extensions_count;
    public cgltf_extension* extensions;
}
[StructLayout(LayoutKind.Sequential)]
public struct cgltf_material_variant
{
    public IntPtr name;
    public cgltf_extras extras;
}
[StructLayout(LayoutKind.Sequential)]
public struct cgltf_asset
{
    public IntPtr copyright;
    public IntPtr generator;
    public IntPtr version;
    public IntPtr min_version;
    public cgltf_extras extras;
    public nuint extensions_count;
    public cgltf_extension* extensions;
}
[StructLayout(LayoutKind.Sequential)]
public struct cgltf_data
{
    public cgltf_file_type file_type;
    public void* file_data;
    public cgltf_asset asset;
    public cgltf_mesh* meshes;
    public nuint meshes_count;
    public cgltf_material* materials;
    public nuint materials_count;
    public cgltf_accessor* accessors;
    public nuint accessors_count;
    public cgltf_buffer_view* buffer_views;
    public nuint buffer_views_count;
    public cgltf_buffer* buffers;
    public nuint buffers_count;
    public cgltf_image* images;
    public nuint images_count;
    public cgltf_texture* textures;
    public nuint textures_count;
    public cgltf_sampler* samplers;
    public nuint samplers_count;
    public cgltf_skin* skins;
    public nuint skins_count;
    public cgltf_camera* cameras;
    public nuint cameras_count;
    public cgltf_light* lights;
    public nuint lights_count;
    public cgltf_node* nodes;
    public nuint nodes_count;
    public cgltf_scene* scenes;
    public nuint scenes_count;
    public cgltf_scene* scene;
    public cgltf_animation* animations;
    public nuint animations_count;
    public cgltf_material_variant* variants;
    public nuint variants_count;
    public cgltf_extras extras;
    public nuint data_extensions_count;
    public cgltf_extension* data_extensions;
    public IntPtr extensions_used;
    public nuint extensions_used_count;
    public IntPtr extensions_required;
    public nuint extensions_required_count;
    public IntPtr json;
    public nuint json_size;
    public void* bin;
    public nuint bin_size;
    public cgltf_memory_options memory;
    public cgltf_file_options file;
}
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "cgltf_parse", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "cgltf_parse", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern cgltf_result cgltf_parse(in cgltf_options options, void* data, nuint size,  out cgltf_data * out_data);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "cgltf_parse_file", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "cgltf_parse_file", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern cgltf_result cgltf_parse_file(in cgltf_options options, [M(U.LPUTF8Str)] string path,  out cgltf_data * out_data);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "cgltf_load_buffers", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "cgltf_load_buffers", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern cgltf_result cgltf_load_buffers(in cgltf_options options, cgltf_data* data, [M(U.LPUTF8Str)] string gltf_path);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "cgltf_load_buffer_base64", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "cgltf_load_buffer_base64", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern cgltf_result cgltf_load_buffer_base64(in cgltf_options options, nuint size, [M(U.LPUTF8Str)] string base64, IntPtr out_data);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "cgltf_decode_string", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "cgltf_decode_string", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern nuint cgltf_decode_string(IntPtr _string);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "cgltf_decode_uri", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "cgltf_decode_uri", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern nuint cgltf_decode_uri(IntPtr uri);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "cgltf_validate", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "cgltf_validate", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern cgltf_result cgltf_validate(cgltf_data* data);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "cgltf_free", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "cgltf_free", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void cgltf_free(cgltf_data* data);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "cgltf_node_transform_local", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "cgltf_node_transform_local", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void cgltf_node_transform_local(in cgltf_node node, float * out_matrix);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "cgltf_node_transform_world", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "cgltf_node_transform_world", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void cgltf_node_transform_world(in cgltf_node node, float * out_matrix);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "cgltf_buffer_view_data", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "cgltf_buffer_view_data", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern byte* cgltf_buffer_view_data(in cgltf_buffer_view view);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "cgltf_find_accessor", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "cgltf_find_accessor", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern cgltf_accessor* cgltf_find_accessor(in cgltf_primitive prim, cgltf_attribute_type type, int index);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "cgltf_accessor_read_float", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "cgltf_accessor_read_float", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int cgltf_accessor_read_float(in cgltf_accessor accessor, nuint index, float * _out, nuint element_size);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "cgltf_accessor_read_uint", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "cgltf_accessor_read_uint", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int cgltf_accessor_read_uint(in cgltf_accessor accessor, nuint index, ref uint _out, nuint element_size);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "cgltf_accessor_read_index", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "cgltf_accessor_read_index", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern nuint cgltf_accessor_read_index(in cgltf_accessor accessor, nuint index);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "cgltf_num_components", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "cgltf_num_components", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern nuint cgltf_num_components(cgltf_type type);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "cgltf_component_size", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "cgltf_component_size", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern nuint cgltf_component_size(cgltf_component_type component_type);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "cgltf_calc_size", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "cgltf_calc_size", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern nuint cgltf_calc_size(cgltf_type type, cgltf_component_type component_type);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "cgltf_accessor_unpack_floats", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "cgltf_accessor_unpack_floats", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern nuint cgltf_accessor_unpack_floats(in cgltf_accessor accessor, float * _out, nuint float_count);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "cgltf_accessor_unpack_indices", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "cgltf_accessor_unpack_indices", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern nuint cgltf_accessor_unpack_indices(in cgltf_accessor accessor, void* _out, nuint out_component_size, nuint index_count);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "cgltf_copy_extras_json", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "cgltf_copy_extras_json", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern cgltf_result cgltf_copy_extras_json(in cgltf_data data, in cgltf_extras extras, IntPtr dest, ref nuint dest_size);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "cgltf_mesh_index", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "cgltf_mesh_index", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern nuint cgltf_mesh_index(in cgltf_data data, in cgltf_mesh _object);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "cgltf_material_index", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "cgltf_material_index", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern nuint cgltf_material_index(in cgltf_data data, in cgltf_material _object);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "cgltf_accessor_index", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "cgltf_accessor_index", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern nuint cgltf_accessor_index(in cgltf_data data, in cgltf_accessor _object);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "cgltf_buffer_view_index", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "cgltf_buffer_view_index", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern nuint cgltf_buffer_view_index(in cgltf_data data, in cgltf_buffer_view _object);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "cgltf_buffer_index", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "cgltf_buffer_index", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern nuint cgltf_buffer_index(in cgltf_data data, in cgltf_buffer _object);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "cgltf_image_index", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "cgltf_image_index", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern nuint cgltf_image_index(in cgltf_data data, in cgltf_image _object);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "cgltf_texture_index", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "cgltf_texture_index", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern nuint cgltf_texture_index(in cgltf_data data, in cgltf_texture _object);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "cgltf_sampler_index", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "cgltf_sampler_index", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern nuint cgltf_sampler_index(in cgltf_data data, in cgltf_sampler _object);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "cgltf_skin_index", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "cgltf_skin_index", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern nuint cgltf_skin_index(in cgltf_data data, in cgltf_skin _object);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "cgltf_camera_index", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "cgltf_camera_index", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern nuint cgltf_camera_index(in cgltf_data data, in cgltf_camera _object);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "cgltf_light_index", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "cgltf_light_index", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern nuint cgltf_light_index(in cgltf_data data, in cgltf_light _object);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "cgltf_node_index", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "cgltf_node_index", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern nuint cgltf_node_index(in cgltf_data data, in cgltf_node _object);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "cgltf_scene_index", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "cgltf_scene_index", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern nuint cgltf_scene_index(in cgltf_data data, in cgltf_scene _object);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "cgltf_animation_index", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "cgltf_animation_index", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern nuint cgltf_animation_index(in cgltf_data data, in cgltf_animation _object);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "cgltf_animation_sampler_index", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "cgltf_animation_sampler_index", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern nuint cgltf_animation_sampler_index(in cgltf_animation animation, in cgltf_animation_sampler _object);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "cgltf_animation_channel_index", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "cgltf_animation_channel_index", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern nuint cgltf_animation_channel_index(in cgltf_animation animation, in cgltf_animation_channel _object);

}
}
