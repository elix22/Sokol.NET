#-------------------------------------------------------------------------------
#   Read output of gen_json.py and generate C# language bindings.
#
#   C# coding style:
#   - everything is PascalCase
#-------------------------------------------------------------------------------
import gen_ir
import json, re, os, shutil
import gen_ir
import sys
import gen_util as util

module_names = {
    'slog_':    'SLog',
    'sg_':      'SG',
    'sapp_':    'SApp',
    'stm_':     'STM',
    'saudio_':  'SAudio',
    'sgl_':     'SGL',
    'sdtx_':    'SDebugText',
    'sshape_':  'SShape',
    'sglue_':   'SGlue',
    'sfetch_':  'SFetch',
    'simgui_':  'SImgui',
    'sgp_':     'SGP',
    'sspine_':  'SSpine',
    'cgltf_':   'CGltf',
    'sbasisu_': 'SBasisu',
    'simgui_':  'SImgui',
    'sgimgui_': 'SGImgui',
    'sfons_':   'SFontstash',
    'fons':     'Fontstash',
    'stbi_':    'StbImage',
    'ozz_':     'OzzUtil',
    'EXR':      'TinyEXR',  # TinyEXR uses struct/type names without prefix
    'b2':       'Box2D',
    'cam_':     'CameraC',
    'cam':      'CameraC',
    'nn_':      'NN',
    'manifold_': 'Manifoldc',
    'sfs_':      'SFilesystem',
    'nvg':       'NanoVG',
    'nsvg':      'NanoSVG',
    'ma_':       'MiniAudio',
    'tsf_':      'TSF',
    'tml_':      'TML',
}

# Namespace names per prefix
namespace_names = {
    'cam_':     'System.Hardware',
    'cam':      'System.Hardware',
    'nn_':      'Sokol.NearNet',
    'manifold_': 'Manifold',
    'ma_':       'MiniAudioNS',
    'tsf_':      'TinySoundFont',
    'tml_':      'TinySoundFont',
}
default_namespace = 'Sokol'

# Output directories per prefix (relative to bindgen/).  Falls back to ../src/sokol/generated/
output_dirs = {
    'cam_':     '../src/System.Hardware',
    'cam':      '../src/System.Hardware',
    'nn_':      '../ext/nearnet/bindings/csharp',
    'manifold_': '../ext/manifold/bindings/csharp',
    'ma_':       '../ext/miniaudio/bindings/csharp',
    'tsf_':      '../ext/TinySoundFont/bindings/csharp',
    'tml_':      '../ext/TinySoundFont/bindings/csharp',
}
default_output_dir = '../src/sokol/generated'

# Extra include directories for clang, per prefix (relative to bindgen/)
extra_include_dirs = {
    'manifold_': ['../ext/manifold/bindings/c/include'],
}

# Extra hand-written declarations to emit at the top of a generated class, per prefix.
# Each entry is a list of raw C# lines.
extra_declarations = {
}

# Additional API prefixes per module (captures types with different naming conventions)
# e.g. manifold functions use 'manifold_' but types use 'Manifold' (PascalCase)
extra_api_prefixes = {
    'manifold_': ['Manifold'],
    'nvg':       ['NVG', 'snvg_'],
    'nsvg':      ['NSVG'],
    'tsf_':      ['TSF'],
    'tml_':      ['tsf_', 'TSF'],
}

# Library names for DllImport statements
library_names = {
    'slog_':    'sokol',
    'sg_':      'sokol',
    'sapp_':    'sokol',
    'stm_':     'sokol',
    'saudio_':  'sokol',
    'sgl_':     'sokol',
    'sdtx_':    'sokol',
    'sshape_':  'sokol',
    'sglue_':   'sokol',
    'sfetch_':  'sokol',
    'simgui_':  'sokol',
    'sspine_':  'spine-c',  # spine-c uses separate library
    'cgltf_':   'sokol',
    'sbasisu_': 'sokol',
    'sgimgui_': 'sokol',
    'sfons_':   'sokol',
    'fons':     'sokol',
    'stbi_':    'sokol',
    'ozz_':     'ozzutil',
    'EXR':      'sokol',  # TinyEXR compiled into sokol
    'b2':       'box2d',  # Box2D uses separate library
    'cam_':     'camerac',  # CameraC uses separate library
    'cam':      'camerac',  # CameraC uses separate library (no-underscore prefix)
    'nn_':      'nearnet',  # NearNet uses separate library
    'manifold_': 'manifoldc',  # manifoldc uses separate library
    'sfs_':      'sokol',
    'nvg':       'sokol',
    'nsvg':      'sokol',
    'ma_':       'miniaudio',
    'tsf_':      'tsf',
    'tml_':      'tsf',
}


c_source_paths = {
    'slog_':    'c/sokol_log.c',
    'sg_':      'c/sokol_gfx.c',
    'sapp_':    'c/sokol_app.c',
    'stm_':     'c/sokol_time.c',
    'saudio_':  'c/sokol_audio.c',
    'sgl_':     'c/sokol_gl.c',
    'sdtx_':    'c/sokol_debugtext.c',
    'sshape_':  'c/sokol_shape.c',
    'sglue_':   'c/sokol_glue.c',
    'sfetch_':  'c/sokol_fetch.c',
    'simgui_':  'c/sokol_imgui.c',
    'sgp_':     'c/sokol_gp.c',
    'sspine_':  'c/sokol_spine.c',
    'cgltf_':   'c/cgltf.c',
    'sbasisu_': 'c/sokol_basisu.c',
    'simgui_':  'c/sokol_imgui.c',
    'sgimgui_': 'c/sokol_gfx_imgui.c',
    'sfons_':   'c/sokol_fontstash.c',
    'fons':     'c/fontstash.c',
    'stbi_':    'c/stb_image.c',
    'ozz_':     'c/ozzutil.c',
    'EXR':      'c/tinyexr.c',
    'b2':       'c/box2d.c',
    'cam_':     'c/camerac.c',
    'cam':      'c/camerac.c',
    'nn_':      'c/nearnet.c',
    'manifold_': 'c/manifoldc.c',  # manifoldc has its own CMake build — no single .c source
    'sfs_':      'c/sokol_filesystem.c',
    'nvg':       'c/nanovg.c',
    'nsvg':      'c/nanosvg.c',
    'ma_':       'c/miniaudio.c',
    'tsf_':      'c/TinySoundFont.c',
    'tml_':      'c/TinyMidiLoader.c',
}

# Overrides for anonymous/unnamed union fields that the generator cannot auto-map.
# Key: "c_struct_name.c_field_name"
# Value: list of raw C# lines to emit for that field (replaces the normal field emission).
# Use void* for single-pointer unions — it is pointer-sized on both x64 (8 B) and wasm32 (4 B).
struct_field_overrides = {
    # union { float* f32; ma_int16* s16; }  — 1 pointer
    'ma_linear_resampler.x0':       ['    public void* x0; // union { float* f32; ma_int16* s16; }'],
    'ma_linear_resampler.x1':       ['    public void* x1; // union { float* f32; ma_int16* s16; }'],
    # union { float** f32; ma_int32** s16; }  — 1 pointer
    'ma_channel_converter.weights': ['    public void* weights; // union { float** f32; ma_int32** s16; }'],
    # union { ma_linear_resampler linear; }  — emit as the contained struct type
    'ma_resampler.state':           ['    public ma_linear_resampler state; // union { ma_linear_resampler linear; }'],
    # struct { ma_uint32 lpfOrder; }  — anonymous struct, emit as plain field
    'ma_resampler_config.linear':   ['    public uint lpfOrder; /* linear.lpfOrder */'],
    # struct { ma_atomic_float volumeBeg/End; ma_atomic_uint64 fadeLengthInFrames/absoluteGlobalTimeInFrames; }
    # 24 bytes, 8-byte aligned. Must be a nested struct so C# respects the 8-byte alignment
    # (plain inlined floats would NOT add the required 4-byte pre-padding, causing offset mismatch).
    'ma_engine_node.fadeSettings': [
        '    public struct _fadeSettings_e__Struct {',
        '        public ma_atomic_float volumeBeg;',
        '        public ma_atomic_float volumeEnd;',
        '        public ma_atomic_uint64 fadeLengthInFrames;',
        '        public ma_atomic_uint64 absoluteGlobalTimeInFrames;',
        '    }',
        '    public _fadeSettings_e__Struct fadeSettings;',
    ],
    # union { struct{pVFS*,file*} vfs; struct{pData*,size_t,size_t} memory; }
    # largest branch = memory = 3 × pointer_size → 24 B (x64) / 12 B (wasm32)
    'ma_decoder.data':              ['    public ma_decoder_data_union data; // union { vfs { pVFS, file }; memory { pData, dataSize, currentReadPos }; }'],
    # tml_message: inject the anonymous union (skipped by the IR as IndirectFieldDecl)
    # before the 'next' pointer. SequentialLayout will insert the 2 padding bytes between
    # channel and data automatically, so offsets match the C struct exactly.
    'tml_message.next': [
        '    public tml_union data; // union: data.key, data.vel, data.control, data.value, data.program, data.pitch_bend (ushort)',
        '    public tml_message* next;',
    ],
}

# C# declarations that must be emitted immediately BEFORE specific struct definitions.
# Key: c_struct_name   Value: list of raw C# lines
struct_type_preambles = {
    # tml_message needs a helper union type for its anonymous union field.
    # The C union is 2 bytes: byte0=(key/control/program), byte1=(vel/value),
    # or as an unsigned short (pitch_bend).  Size MUST be 2 to keep next at offset 8.
    'tml_message': [
        '// Helper union for tml_message: 2 bytes wide matching the actual C union.',
        '// Byte 0: key / control / program (all aliases).  Byte 1: vel / value (aliases).',
        '// pitch_bend is the raw unsigned short spanning both bytes.',
        '[StructLayout(LayoutKind.Explicit, Size = 2)]',
        'public struct tml_union {',
        '    [FieldOffset(0)] public byte key;',
        '    [FieldOffset(1)] public byte vel;',
        '    [FieldOffset(0)] public byte control;',
        '    [FieldOffset(1)] public byte value;',
        '    [FieldOffset(0)] public byte program;',
        '    [FieldOffset(0)] public ushort pitch_bend;',
        '}',
    ],
    # ma_decoder needs a helper type whose size is platform-conditional
    'ma_decoder': [
        '// Helper for ma_decoder.data union: max(vfs:2 ptrs, memory:3 ptr-sized) = 3 * pointer_size',
        '#if WEB',
        '[StructLayout(LayoutKind.Explicit, Size = 12)]   // 3 x 4 bytes (wasm32)',
        '#else',
        '[StructLayout(LayoutKind.Explicit, Size = 24)]   // 3 x 8 bytes (x64)',
        '#endif',
        'public struct ma_decoder_data_union { }',
    ],
}

name_ignores = [
    'sdtx_printf',
    'sdtx_vprintf',
    'sg_install_trace_hooks',
    'sg_trace_hooks',
    'cgltf_camera',
    'sg_color', # will be create manually inorder to support additional vonversion to Vector3,Vector4,float[] , Span
    # 'sgimgui_init' and 'sgimgui_t' removed — sgimgui is now singleton (no handle) since upstream upmerge
    'fonsSetErrorCallback', # function pointer callback not supported
    'camDevice_t_',          # opaque handle, exposed as IntPtr via camDevice typedef
    'manifold_warp',         # hand-written wrapper in manifoldc_csharp_internal_wrappers.h
    'NVGcolor',              # has anonymous union; defined manually in NanoVG.cs partial class
    # NanoVG internal backend types (not needed from C#; use nvgCreateSokol instead)
    'NVGscissor',
    'NVGvertex',
    'NVGpath',
    'NVGparams',
    'nvgCreateInternal',
    'nvgDeleteInternal',
    'nvgInternalParams',
    'NSVGpaint',              # has anonymous union; define manually if needed
    'NSVGrasterizer',         # opaque struct; handle as IntPtr manually
    'nsvgCreateRasterizer',   # returns opaque NSVGrasterizer*; define manually
    'nsvgRasterize',          # takes opaque NSVGrasterizer*; define manually
    'nsvgDeleteRasterizer',   # takes opaque NSVGrasterizer* (unnamed param); define manually
    'nsvgParseFromFile',      # won;t work on mobile platforms due to file I/O; ignore for now
    # miniaudio: variadic / va_list functions cannot be bound from C#
    'ma_log_postv',            # variadic va_list param
    'ma_log_postf',            # variadic (redundant with ma_log_post)
]

name_overrides = {
    'sgl_error':    'sgl_get_error',  
    'sgl_deg':      'sgl_as_degrees',
    'sgl_rad':      'sgl_as_radians',
    'sapp_isvalid': 'sapp_is_valid',
    'slog_func':    'slog_func_native',
    'lock':         'dolock',
    'params':       'parameters',
    'sshape_element_range': 'sshape_make_element_range',
    'sshape_mat4':  'sshape_make_mat4',
    'readonly':     '_readonly',
    'ref':          '_ref',
    'override':     '_override',
    'event':        '_event',
    'string':       '_string',
    'out':          '_out',
    'object':       '_object',
    'in':           '_in',
    'base':        '_base'
}

# NOTE: syntax for function results: "func_name.RESULT"
type_overrides = {
    'sg_context_desc.color_format':         'int',
    'sg_context_desc.depth_format':         'int',
    'sg_apply_uniforms.ub_index':           'uint32_t',
    'sg_draw.base_element':                 'uint32_t',
    'sg_draw.num_elements':                 'uint32_t',
    'sg_draw.num_instances':                'uint32_t',
    'sshape_element_range_t.base_element':  'uint32_t',
    'sshape_element_range_t.num_elements':  'uint32_t',
    'sdtx_font.font_index':                 'uint32_t',
    'sfetch_response_t.path':               'void*',
    'cgltf_data.json':                      'void*',
    # miniaudio float** fields: pointer-to-pointer, map to IntPtr in structs
    'ma_channel_converter_config.ppWeights':     'IntPtr',
    'ma_data_converter_config.ppChannelWeights': 'IntPtr',
}


prim_types = {
    'int':          'int',
    'const int':    'int',
    'bool':         'bool',
    'char':         'byte',
    'int8_t':       'sbyte',
    'uint8_t':      'byte',
    'int16_t':      'short',
    'uint16_t':     'ushort',
    'int32_t':      'int',
    'uint32_t':     'uint',
    'unsigned int': 'uint',
    'int64_t':      'long',
    'uint64_t':     'ulong',
    'float':        'float',
    'double':       'double',
    'uintptr_t':    'nuint',
    'intptr_t':     'nint',
    'size_t':       'nuint',
    'void*':        'IntPtr',
    'sspine_color': 'sg_color',
    'cgltf_size':   'nuint',
    'cgltf_ssize':  'long',
    'cgltf_int':    'int',
    'cgltf_uint':   'uint',
    'cgltf_bool':   'int',
    'cgltf_float':  'float',
    'char *':       'IntPtr',
    'char*':        'IntPtr',
    'char*':        'IntPtr',
    'char **':       'IntPtr',
    'void **':      'IntPtr',
    'cgltf_float *' : 'float *',    
    'uint8_t *':   'byte*',
    'unsigned char': 'byte',
    'unsigned char *': 'byte*',
    'EXRHeader *': 'IntPtr',
    'const char ***': 'IntPtr',
    'const EXRImage *': 'IntPtr',
    'const char **': 'IntPtr',
    # NearNet opaque handles + peer-id scalar (nn_-prefixed: no collision with other libs)
    'nn_context':              'IntPtr',
    'nn_context *':            'IntPtr*',
    'nn_context*':             'IntPtr*',
    'nn_session':              'IntPtr',
    'nn_session *':            'IntPtr*',
    'nn_session*':             'IntPtr*',
    'nn_peer_id':              'uint',
    # CameraC opaque handle and callback types
    'camDevice':               'IntPtr',
    'camFrameCallback':        'delegate* unmanaged<IntPtr, camFrame*, void*, void>',
    'camPermissionCallback':   'delegate* unmanaged<IntPtr, camPermission, void*, void>',
    # Manifoldc opaque pointer types
    'ManifoldManifold *':       'IntPtr',
    'ManifoldManifoldVec *':    'IntPtr',
    'ManifoldCrossSection *':   'IntPtr',
    'ManifoldCrossSectionVec *':'IntPtr',
    'ManifoldSimplePolygon *':  'IntPtr',
    'ManifoldPolygons *':       'IntPtr',
    'ManifoldMeshGL *':         'IntPtr',
    'ManifoldMeshGL64 *':       'IntPtr',
    'ManifoldBox *':            'IntPtr',
    'ManifoldRect *':           'IntPtr',
    'ManifoldTriangulation *':  'IntPtr',
    # Manifoldc function pointer typedef
    'ManifoldSdf':              'delegate* unmanaged<double, double, double, void*, double>',
    # ManifoldSimplePolygon** — double pointer to opaque handle
    'ManifoldSimplePolygon **':  'IntPtr',
    # sokol_filesystem opaque file handle and callback
    'sfs_file_t *':              'IntPtr',
    'sfs_enumerate_callback_t':  'IntPtr',
    # nanovg opaque context handle
    'NVGcontext *':              'IntPtr',
    # miniaudio primitive typedefs
    'ma_uint8':   'byte',
    'ma_int8':    'sbyte',
    'ma_uint16':  'ushort',
    'ma_int16':   'short',
    'ma_uint32':  'uint',
    'ma_int32':   'int',
    'ma_uint64':  'ulong',
    'ma_int64':   'long',
    'ma_bool8':   'byte',
    'ma_bool32':  'uint',
    'ma_uintptr': 'nuint',
    'ma_handle':  'IntPtr',
    'ma_ptr':     'IntPtr',
    'ma_proc':    'IntPtr',
    'ma_wchar_win32': 'ushort',
    'ma_channel': 'byte',
    # miniaudio opaque OS/platform handles
    'ma_thread':  'IntPtr',
    'ma_mutex':   'IntPtr',
    'ma_event':   'IntPtr',
    'ma_semaphore': 'IntPtr',
    'ma_timer':   'IntPtr',
    # miniaudio additional primitive aliases
    'ma_float':   'float',
    'ma_spinlock': 'uint',
    # miniaudio opaque struct pointer types (forward-declared interfaces)
    'ma_async_notification *':  'IntPtr',
    'ma_data_source *':         'IntPtr',
    'ma_node *':                'IntPtr',
    'ma_resampling_backend *':  'IntPtr',
    'ma_vfs *':                 'IntPtr',
    # miniaudio function pointer typedefs
    'ma_log_callback_proc':         'IntPtr',
    'ma_decoder_read_proc':         'IntPtr',
    'ma_decoder_seek_proc':         'IntPtr',
    'ma_decoder_tell_proc':         'IntPtr',
    'ma_device_data_proc':          'IntPtr',
    'ma_device_notification_proc':  'IntPtr',
    'ma_encoder_init_proc':         'IntPtr',
    'ma_encoder_seek_proc':         'IntPtr',
    'ma_encoder_uninit_proc':       'IntPtr',
    'ma_encoder_write_pcm_frames_proc': 'IntPtr',
    'ma_encoder_write_proc':        'IntPtr',
    'ma_engine_process_proc':       'delegate* unmanaged<void*, float*, ulong, void>',
    'ma_read_proc':                 'IntPtr',
    'ma_seek_proc':                 'IntPtr',
    'ma_sound_end_proc':            'IntPtr',
    'ma_stop_proc':                 'IntPtr',
    'ma_tell_proc':                 'IntPtr',
    'ma_data_source_get_next_proc': 'IntPtr',
    # miniaudio platform threading types (opaque on all platforms)
    'ma_pthread_mutex_t': 'IntPtr',
    'ma_pthread_cond_t':  'IntPtr',
    # C types with spaces that appear as array element types
    'unsigned char': 'byte',
    # misc pointer types
    'const wchar_t *': 'IntPtr',
    # pass-through entries so type_overrides can emit raw C# type names
    'IntPtr': 'IntPtr',
    'void*':  'void*',
    # miniaudio opaque/interface types accessed via const pointer (treated as IntPtr)
    'const ma_data_source *':      'IntPtr',
    'const ma_node *':             'IntPtr',
    'const ma_async_notification *': 'IntPtr',
    'const ma_resampling_backend *': 'IntPtr',
    'const ma_vfs *':              'IntPtr',
    # miniaudio vfs file handle (typedef'd integer on all platforms)
    'ma_vfs_file':                 'IntPtr',
    'ma_vfs_file *':               'IntPtr',
    # miniaudio enum callback
    'ma_enum_devices_callback_proc': 'IntPtr',
    # miniaudio spinlock is volatile uint
    'volatile ma_spinlock *':      'uint*',
    # TinySoundFont opaque handle (tsf* is the main synthesizer context)
    'tsf *':                       'IntPtr',
    'const tsf *':                 'IntPtr',
    # TinySoundFont enum passed as parameter
    'enum TSFOutputMode':          'TSFOutputMode',
    # TinyMidiLoader message linked list pointer
    'tml_message *':               'tml_message*',
    # short*/float* PCM buffers (tsf_render_short)
    'short *':                     'short*',
}


prim_defaults = {
    'int':          '0',
    'bool':         'false',
    'int8_t':       '0',
    'uint8_t':      '0',
    'int16_t':      '0',
    'uint16_t':     '0',
    'int32_t':      '0',
    'uint32_t':     '0',
    'int64_t':      '0',
    'uint64_t':     '0',
    'float':        '0.0f',
    'double':       '0.0',
    'uintptr_t':    '0',
    'intptr_t':     '0',
    'size_t':       '0'
}

# Functions which need special handling for WebAssembly (Emscripten) platform
# These functions return structs with an 'id' field that need special wrapper treatment
web_wrapper_functions = {
    'sg_make_shader',
    'sg_alloc_shader',
    'sg_make_pipeline', 
    'sg_alloc_pipeline',
    'sg_make_view',
    'sg_alloc_view',
    'sg_make_buffer',
    'sg_alloc_buffer',
    'sg_make_image',
    'sg_alloc_image',
    'sg_make_sampler',
    'sg_alloc_sampler',
    'sgp_make_pipeline',
    'sgl_make_pipeline',
    'sdtx_make_context',
    'sdtx_get_context',
    'sdtx_default_context',
}

# AUTO-DETECTED: Functions that return structs by value (detected automatically during binding generation)
# This dictionary is populated by detect_struct_return_functions() during pre_parse()
# Format: {'function_name': 'return_type'}
web_wrapper_struct_return_functions = {}

struct_types = []
enum_types = []
enum_items = {}
struct_type_aliases = {}  # alias name -> canonical struct name
out_lines = ''
current_library_name = 'sokol'  # Default library name, will be set per module

def reset_globals():
    global struct_types
    global enum_types
    global enum_items
    global struct_type_aliases
    global out_lines
    global web_wrapper_struct_return_functions
    global current_library_name
    struct_types = []
    enum_types = []
    enum_items = {}
    struct_type_aliases = {}
    out_lines = ''
    current_library_name = 'sokol'  # Reset to default
    # Note: web_wrapper_struct_return_functions is NOT reset here
    # It accumulates across all modules for the C header generation

def l(s):
    global out_lines
    out_lines += s + '\n'

def as_csharp_prim_type(s):
    return prim_types[s]

# prefix_bla_blub(_t) => (dep.)BlaBlub
def as_csharp_struct_type(s, prefix):
    # If s is a typedef alias, return the canonical C name instead
    return struct_type_aliases.get(s, s)

# prefix_bla_blub(_t) => (dep.)BlaBlub
def as_csharp_enum_type(s, prefix):
    return s

def check_type_override(func_or_struct_name, field_or_arg_name, orig_type):
    s = f"{func_or_struct_name}.{field_or_arg_name}"
    print(f"Checking override for {s}")  # Debugging output
    if s in type_overrides:
        print(f"Applying override for {s}: {type_overrides[s]}")
        return type_overrides[s]
    return orig_type

def check_name_override(name):
    if name in name_overrides:
        return name_overrides[name]
    else:
        return name

def check_name_ignore(name):
    return name in name_ignores


# prefix_bla_blub => BlaBlub
def as_pascal_case(s, prefix):
    return s



# PREFIX_ENUM_BLA => Bla, _PREFIX_ENUM_BLA => Bla
def as_enum_item_name(s):
    return s

def enum_default_item(enum_name):
    return enum_items[enum_name][0]

def is_prim_type(s):
    return s in prim_types

def is_struct_type(s):
    return s in struct_types or s in struct_type_aliases

def is_enum_type(s):
    return s in enum_types

def is_string_ptr(s):
    return s == "const char *"

def is_const_void_ptr(s):
    return s == "const void *"

def is_void_ptr(s):
    return s == "void *"

def is_const_prim_ptr(s):
    for prim_type in prim_types:
        if s == f"const {prim_type} *":
            return True
    return False

def is_prim_ptr(s):
    for prim_type in prim_types:
        if s == f"{prim_type} *":
            return True
    return False

def is_struct_ptr(s):
    for struct_type in struct_types:
        if s == f"{struct_type} *" or s == f"struct {struct_type} *":
            return True
    return False

def is_struct_ptr_ptr(s):
    for struct_type in struct_types:
        if s == f"{struct_type} **":
            return True
    return False

def is_const_struct_ptr(s):
    for struct_type in struct_types:
        if s == f"const {struct_type} *":
            return True
    return False

def is_const_struct_sturct_ptr(s):
    for struct_type in struct_types:
        if s == f"const struct {struct_type} *":
            return True
    return False


def is_func_ptr(s):
    return '(*)' in s

def type_default_value(s):
    return prim_defaults[s]

def extract_array_type(s):
    return s[:s.index('[')].strip()

def extract_array_nums(s):
    return s[s.index('['):].replace('[', ' ').replace(']', ' ').split()

def extract_ptr_type(s):
    # Remove 'const', 'struct' keyword, and pointer markers to get the base type
    # e.g., "const unsigned char *" -> "unsigned char"
    # e.g., "struct NSVGpath *" -> "NSVGpath"
    s = s.replace('const', '').replace('struct ', '').replace('*', '').strip()
    return s

def as_extern_c_arg_type(arg_type, prefix):
    if arg_type == "void":
        return "void"
    elif is_prim_type(arg_type):
        return as_csharp_prim_type(arg_type)
    elif is_struct_type(arg_type):
        return as_csharp_struct_type(arg_type, prefix)
    elif is_enum_type(arg_type):
        return as_csharp_enum_type(arg_type, prefix)
    elif is_void_ptr(arg_type):
        return "void*"
    elif is_const_void_ptr(arg_type):
        return "void*"
    elif is_string_ptr(arg_type):
        return "byte*"
    elif is_const_struct_ptr(arg_type):
        return f"{as_csharp_struct_type(extract_ptr_type(arg_type), prefix)}*"
    elif is_const_struct_sturct_ptr(arg_type):
        return f"void *" 
    elif is_prim_ptr(arg_type):
        return f"{as_csharp_prim_type(extract_ptr_type(arg_type))}*"
    elif is_const_prim_ptr(arg_type):
        return f"{as_csharp_prim_type(extract_ptr_type(arg_type))}*"
    elif is_struct_ptr(arg_type):
        # non-const struct pointer (e.g. ma_context *, ma_device *)
        return f"{as_csharp_struct_type(extract_ptr_type(arg_type), prefix)}*"
    elif arg_type.endswith(" **") or arg_type.endswith("**"):
        # double pointer — extract base type, emit as void* (opaque)
        base = arg_type.replace('const ', '').replace('*', '').strip()
        if is_struct_type(base):
            return f"{as_csharp_struct_type(base, prefix)}**"
        else:
            return "void*"
    elif arg_type == "float **" or arg_type == "const float **":
        return "float**"
    elif arg_type.endswith(" *") and is_enum_type(arg_type[:-2].rstrip()):
        # pointer to enum (e.g. ma_format *)
        return f"{as_csharp_enum_type(arg_type[:-2].rstrip(), prefix)}*"
    else:
        return '??? (as_extern_c_arg_type)'


def as_csharp_arg_type(arg_prefix, arg_type, prefix):
    # NOTE: if arg_prefix is None, the result is used as return value
    pre = "" if arg_prefix is None else arg_prefix
    if arg_type == "void":
        if arg_prefix is None:
            return "void"
        else:
            return ""
    elif arg_type.startswith("const ImVec4 *"):
        return f"ImVec4_t *{pre}"
    elif is_prim_type(arg_type):
        return as_csharp_prim_type(arg_type) + pre
    elif is_struct_type(arg_type):
        return as_csharp_struct_type(arg_type, prefix) + pre
    elif is_enum_type(arg_type):
        return as_csharp_enum_type(arg_type, prefix) + pre
    elif is_void_ptr(arg_type):
        return "void*" + pre
    elif is_const_void_ptr(arg_type):
        return "void*" + pre
    elif is_string_ptr(arg_type):
        return "string" + pre
    elif is_const_struct_ptr(arg_type):
        if arg_prefix is None:
            # Return type: use pointer syntax (C# 'in' is only valid as a parameter modifier)
            return f"{as_csharp_struct_type(extract_ptr_type(arg_type), prefix)}*" + pre
        else:
            # Parameter: pass const structs by value using 'in'
            return f"in {as_csharp_struct_type(extract_ptr_type(arg_type), prefix)}" + pre
    elif is_struct_ptr(arg_type):
        # For struct pointers, use pointer syntax (cgltf_data*) not ref
        return f"{as_csharp_struct_type(extract_ptr_type(arg_type), prefix)}*" + pre
    elif is_prim_ptr(arg_type):
        if arg_prefix is None:
            # Return type: use pointer syntax
            return f"{as_csharp_prim_type(extract_ptr_type(arg_type))}*" + pre
        else:
            # Parameter: use ref
            return f"ref {as_csharp_prim_type(extract_ptr_type(arg_type))}" + pre
    elif is_const_prim_ptr(arg_type):
        if arg_prefix is None:
            # Return type: use pointer syntax
            return f"{as_csharp_prim_type(extract_ptr_type(arg_type))}*" + pre
        else:
            # Parameter: use in
            return f"in {as_csharp_prim_type(extract_ptr_type(arg_type))}" + pre
    # Explicit handling for specific SGP types:
    elif arg_type.startswith("const sgp_point *"):
        return f"in sgp_vec2{pre}"
    elif arg_type.startswith("sgp_state *"):
        return f"ref sgp_state{pre}"
    elif arg_type.startswith("cgltf_data **"):
        return f" out cgltf_data *{pre}"
    elif arg_type.startswith("cgltf_data *"):
        return f"cgltf_data *{pre}"
    elif arg_type.startswith("void **"):
        return f" out IntPtr{pre}"
    elif arg_type.startswith("float **"):
        return f" out float *{pre}"
    elif arg_type.startswith("const char **"):
        return f" out byte *{pre}"
    elif arg_type.startswith("const struct cgltf_memory_options*"):
        return f"in cgltf_memory_options{pre}"
    elif arg_type.startswith("const struct cgltf_file_options*"):
        return f"in cgltf_file_options{pre}"
    elif arg_type.startswith("const cgltf_sampler *"):
        return f"in cgltf_sampler{pre}"
    elif arg_type.startswith("cgltf_accessor *"):
        return f"cgltf_accessor *"
    elif arg_type.startswith("FONScontext *"):
        return f"IntPtr{pre}"
    elif arg_type.startswith("FONSparams *"):
        return f"IntPtr{pre}"
    elif arg_type.startswith("FONStextIter *"):
        return f"IntPtr{pre}"
    elif arg_type.startswith("struct FONSquad *"):
        return f"IntPtr{pre}"
    elif arg_type.startswith("const unsigned char *"):
        return f"byte *{pre}"
    elif arg_type.startswith("unsigned char *"):
        return f"byte *{pre}"
    # ozz-animation types
    elif arg_type.startswith("const ozz_desc_t *"):
        return f"in ozz_desc_t{pre}"
    elif arg_type.startswith("ozz_instance_t *"):
        return f"IntPtr{pre}"  # Handle as opaque pointer
    elif arg_type.startswith("ozz_t"):
        return f"IntPtr{pre}"  # Handle as opaque pointer
    # Box2D function pointer types (callbacks/delegates)
    elif arg_type.startswith("b2AllocFcn *"):
        return f"IntPtr{pre}"  # Function pointer: void* (*)(unsigned int size, int alignment)
    elif arg_type.startswith("b2FreeFcn *"):
        return f"IntPtr{pre}"  # Function pointer: void (*)(void* mem)
    elif arg_type.startswith("b2AssertFcn *"):
        return f"IntPtr{pre}"  # Function pointer: int (*)(const char*, const char*, int)
    elif arg_type.startswith("b2TaskCallback *"):
        return f"IntPtr{pre}"  # Function pointer: void (*)(int, int, uint32_t, void*)
    elif arg_type.startswith("b2EnqueueTaskCallback *"):
        return f"IntPtr{pre}"  # Function pointer: void* (*)(b2TaskCallback*, int, int, void*, void*)
    elif arg_type.startswith("b2FinishTaskCallback *"):
        return f"IntPtr{pre}"  # Function pointer: void (*)(void*, void*)
    elif arg_type.startswith("b2FrictionCallback *"):
        return f"IntPtr{pre}"  # Function pointer: float (*)(float, uint64_t, float, uint64_t)
    elif arg_type.startswith("b2RestitutionCallback *"):
        return f"IntPtr{pre}"  # Function pointer: float (*)(float, uint64_t, float, uint64_t)
    elif arg_type.startswith("b2CustomFilterFcn *"):
        return f"IntPtr{pre}"  # Function pointer: bool (*)(b2ShapeId, b2ShapeId, void*)
    elif arg_type.startswith("b2PreSolveFcn *"):
        return f"IntPtr{pre}"  # Function pointer: bool (*)(b2ShapeId, b2ShapeId, b2Vec2, b2Vec2, void*)
    elif arg_type.startswith("b2OverlapResultFcn *"):
        return f"IntPtr{pre}"  # Function pointer: bool (*)(b2ShapeId, void*)
    elif arg_type.startswith("b2CastResultFcn *"):
        return f"IntPtr{pre}"  # Function pointer: float (*)(b2ShapeId, b2Vec2, b2Vec2, float, void*)
    elif arg_type.startswith("b2PlaneResultFcn *"):
        return f"IntPtr{pre}"  # Function pointer: bool (*)(b2ShapeId, const b2PlaneResult*, void*)
    elif arg_type.startswith("b2TreeQueryCallbackFcn *"):
        return f"IntPtr{pre}"  # Function pointer: bool (*)(int, uint64_t, void*)
    elif arg_type.startswith("b2TreeRayCastCallbackFcn *"):
        return f"IntPtr{pre}"  # Function pointer: float (*)(const b2RayCastInput*, int, uint64_t, void*)
    elif arg_type.startswith("b2TreeShapeCastCallbackFcn *"):
        return f"IntPtr{pre}"  # Function pointer: float (*)(const b2ShapeCastInput*, int, uint64_t, void*)
    # miniaudio: enum* / backend* / double-pointer out-params
    elif arg_type == "ma_format *":
        return f"ref ma_format{pre}"
    elif arg_type == "ma_backend *":
        return f"ma_backend*{pre}"
    elif arg_type == "const ma_backend *":
        return f"ma_backend*{pre}"
    elif arg_type == "ma_audio_buffer **":
        return f"out IntPtr{pre}"
    elif arg_type == "ma_device_info **":
        return f"out IntPtr{pre}"
    elif arg_type == "ma_paged_audio_buffer_page **":
        return f"out IntPtr{pre}"
    elif arg_type == "const void **":
        return f"void**{pre}"
    # Manifoldc inline function pointer types
    elif arg_type == 'ManifoldVec3 (*)(double, double, double, void *)':
        return f"delegate* unmanaged<double, double, double, void*, ManifoldVec3>{pre}"
    elif arg_type == 'ManifoldVec2 (*)(double, double, void *)':
        return f"delegate* unmanaged<double, double, void*, ManifoldVec2>{pre}"
    elif arg_type == 'void (*)(char *, void *)':
        return f"delegate* unmanaged<byte*, void*, void>{pre}"
    elif arg_type == 'void (*)(double *, ManifoldVec3, const double *, void *)':
        return f"delegate* unmanaged<double*, ManifoldVec3, double*, void*, void>{pre}"
    else:
        print(f"[DEBUG] as_csharp_arg_type not handled for arg_type: '{arg_type}', arg_prefix: '{arg_prefix}', prefix: '{prefix}'", file=sys.stderr, flush=True)
        if arg_prefix is None:
            return "IntPtr  /* ??? (as_csharp_arg_type) */"
        else:
            return arg_prefix + "IntPtr  /* ??? (as_csharp_arg_type) */"

# get C-style arguments of a function pointer as string
def funcptr_args_c(field_type, prefix):
    tokens = field_type[field_type.index('(*)')+4:-1].split(',')
    s = ""
    for token in tokens:
        arg_type = token.strip()
        if s != "":
            s += ", "
        c_arg = as_extern_c_arg_type(arg_type, prefix)
        if (c_arg == "void"):
            return ""
        else:
            s += c_arg
    return s

# get C-style result of a function pointer as string
def funcptr_res_c(field_type):
    res_type = field_type[:field_type.index('(*)')].strip()
    if res_type == 'void':
        return 'void'
    elif is_const_void_ptr(res_type):
        return 'void*'
    else:
        return 'void*'

def funcdecl_args_c(decl, prefix):
    s = ""
    func_name = decl['name']
    for param_decl in decl['params']:
        if s != "":
            s += ", "
        param_name = param_decl['name']
        param_type = check_type_override(func_name, param_name, param_decl['type'])
        s += as_extern_c_arg_type(param_type, prefix)
    return s

def funcdecl_args_csharp(decl, prefix):
    s = ""
    func_name = decl['name']
    for param_decl in decl['params']:
        if s != "":
            s += ", "
        param_name = check_name_override(param_decl['name'])
        param_type = check_type_override(func_name, param_name, param_decl['type'])

        if is_string_ptr(param_type):
            s += "[M(U.LPUTF8Str)] "

        s += f"{as_csharp_arg_type(f' {param_name}', param_type, prefix)}"
    return s

def is_funcptr_prim_type(s):
    """Return True if the C# prim type for this C type is a function pointer (delegate* unmanaged<>)."""
    return is_prim_type(s) and as_csharp_prim_type(s).startswith('delegate* unmanaged')

def func_has_funcptr_params(decl, prefix):
    """Return True if any parameter resolves to a delegate* unmanaged<> function pointer."""
    func_name = decl.get('name', '')
    for param_decl in decl.get('params', []):
        param_name = check_name_override(param_decl['name'])
        param_type = check_type_override(func_name, param_name, param_decl['type'])
        if is_funcptr_prim_type(param_type) or util.is_func_ptr(param_type):
            return True
    return False

def funcdecl_args_csharp_intptr(decl, prefix):
    """Like funcdecl_args_csharp but replaces delegate* unmanaged<> params with IntPtr (for WEB P/Invoke)."""
    s = ""
    func_name = decl['name']
    for param_decl in decl['params']:
        if s != "":
            s += ", "
        param_name = check_name_override(param_decl['name'])
        param_type = check_type_override(func_name, param_name, param_decl['type'])
        if is_funcptr_prim_type(param_type) or util.is_func_ptr(param_type):
            s += f"IntPtr {param_name}"
        else:
            if is_string_ptr(param_type):
                s += "[M(U.LPUTF8Str)] "
            s += f"{as_csharp_arg_type(f' {param_name}', param_type, prefix)}"
    return s

def funcdecl_call_args_with_intptr_cast(decl, prefix):
    """Generate call argument list, casting delegate* unmanaged<> params to IntPtr."""
    parts = []
    func_name = decl['name']
    for param_decl in decl['params']:
        param_name = check_name_override(param_decl['name'])
        param_type = check_type_override(func_name, param_name, param_decl['type'])
        if is_funcptr_prim_type(param_type) or util.is_func_ptr(param_type):
            parts.append(f"(IntPtr){param_name}")
        else:
            parts.append(param_name)
    return ", ".join(parts)

def funcdecl_result_c(decl, prefix):
    func_name = decl['name']
    decl_type = decl['type']
    result_type = check_type_override(func_name, 'RESULT', decl_type[:decl_type.index('(')].strip())
    return as_extern_c_arg_type(result_type, prefix)

def funcdecl_result_csharp(decl, prefix):
    func_name = decl['name']
    decl_type = decl['type']
    result_type = check_type_override(func_name, 'RESULT', decl_type[:decl_type.index('(')].strip())
    csharp_res_type = as_csharp_arg_type(None, result_type, prefix)
    if csharp_res_type == "":
        csharp_res_type = "void"
    return csharp_res_type

def gen_struct(decl, prefix):
    struct_name = decl['name']
    csharp_type = as_csharp_struct_type(struct_name, prefix)
    # Emit any helper types that must appear before this struct
    if struct_name in struct_type_preambles:
        for line in struct_type_preambles[struct_name]:
            l(line)
    l(f"[StructLayout(LayoutKind.Sequential)]")
    l(f"public struct {csharp_type}")
    l("{")
    for field in decl['fields']:
        if 'name' not in field:
            continue  # skip anonymous/unnamed struct fields
        field_name = as_pascal_case(check_name_override(field['name']), "")
        field_type = field['type']
        field_type = check_type_override(struct_name, field_name, field_type)
        # Check explicit override first — handles injecting extra fields before a known field.
        override_key = f"{struct_name}.{field['name']}"
        if override_key in struct_field_overrides:
            for line in struct_field_overrides[override_key]:
                l(line)
            continue
        if field_type == "bool":
            # Conditional for bool fields with properties
            l("#if WEB")
            l(f"    private byte _{field_name};")
            l(f"    public bool {field_name} {{ get => _{field_name} != 0; set => _{field_name} = value ? (byte)1 : (byte)0; }}")
            l("#else")
            l(f"    [M(U.I1)] public bool {field_name};")
            l("#endif")
        elif is_prim_type(field_type):
            l(f"    public {as_csharp_prim_type(field_type)} {field_name};")
        elif is_struct_type(field_type):
            l(f"    public {as_csharp_struct_type(field_type, prefix)} {field_name};")
        elif is_enum_type(field_type):
            l(f"    public {as_csharp_enum_type(field_type, prefix)} {field_name};")
        elif util.is_string_ptr(field_type):
            # Conditional for string fields with properties
            l("#if WEB")
            l(f"    private IntPtr _{field_name};")
            l(f"    public string {field_name} {{ get => Marshal.PtrToStringAnsi(_{field_name});  set {{ if (_{field_name} != IntPtr.Zero) {{ Marshal.FreeHGlobal(_{field_name}); _{field_name} = IntPtr.Zero; }} if (value != null) {{ _{field_name} = Marshal.StringToHGlobalAnsi(value); }} }} }}")
            l("#else")
            l(f"    [M(U.LPUTF8Str)] public string {field_name};")
            l("#endif")
        elif util.is_const_void_ptr(field_type):
            l(f"    public void* {field_name};")
        elif util.is_void_ptr(field_type):
            l(f"    public void* {field_name};")
        elif is_const_prim_ptr(field_type):
            l(f"    public {as_csharp_prim_type(extract_ptr_type(field_type))}* {field_name};")
        elif is_prim_ptr(field_type):
            l(f"    public {as_csharp_prim_type(extract_ptr_type(field_type))}* {field_name};")
        elif is_struct_ptr(field_type):
            l(f"    public {as_csharp_struct_type(extract_ptr_type(field_type), prefix)}* {field_name};")
        elif is_const_struct_ptr(field_type):
            l(f"    public {as_csharp_struct_type(extract_ptr_type(field_type), prefix)}* {field_name};")
        elif is_struct_ptr_ptr(field_type):
            l(f"    public {as_csharp_struct_type(extract_ptr_type(field_type), prefix)}** {field_name};")
        elif util.is_func_ptr(field_type):
            args = funcptr_args_c(field_type, prefix)
            if args != "":
                args += ", "
            l(f"    public delegate* unmanaged<{args}{funcptr_res_c(field_type)}> {field_name};")
        elif util.is_1d_array_type(field_type):
            array_type = util.extract_array_type(field_type)
            array_nums = util.extract_array_sizes(field_type)
            if is_prim_type(array_type) or is_struct_type(array_type) or is_enum_type(array_type)  or is_const_void_ptr(array_type):
                if is_prim_type(array_type):
                    csharp_type = as_csharp_prim_type(array_type)
                elif is_struct_type(array_type):
                    csharp_type = as_csharp_struct_type(array_type, prefix)
                elif is_enum_type(array_type):
                    csharp_type = as_csharp_enum_type(array_type, prefix)
                elif is_const_void_ptr(array_type):
                    csharp_type = "IntPtr"
                else:
                    csharp_type = '??? (1d array type)'
                l("    #pragma warning disable 169")
                l(f"    public struct {field_name}Collection")
                l("    {")
                l(f"        public ref {csharp_type} this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, {array_nums[0]})[index];")
                for i in range(0, int(array_nums[0])):
                    l(f"        private {csharp_type} _item{i};")
                l("    }")
                l("    #pragma warning restore 169")

                l(f"    public {field_name}Collection {field_name};")
            elif util.is_const_void_ptr(array_type):
                l(f"    {field_name}: [{array_nums[0]}]?*const anyopaque = [_]?*const anyopaque{{null}} ** {array_sizes[0]},")
            else:
                sys.exit(f"ERROR gen_struct: array {field_name}: {field_type} => {array_type} [{array_nums[0]}]")
        elif util.is_2d_array_type(field_type):
            array_type = util.extract_array_type(field_type)
            array_nums = util.extract_array_sizes(field_type)
            if is_prim_type(array_type):
                csharp_type = as_csharp_prim_type(array_type)
                def_val = type_default_value(array_type)
            elif is_struct_type(array_type):
                csharp_type = as_csharp_struct_type(array_type, prefix)
                def_val = ".{ }"
            elif is_enum_type(array_type):
                csharp_type = as_csharp_enum_type(array_type, prefix)
            elif is_const_void_ptr(array_type):
                csharp_type = "IntPtr"
            else:
                csharp_type = '??? (2d array type)'
                def_val = "???"

            l("    #pragma warning disable 169")
            l(f"    public struct {field_name}Collection")
            l("    {")
            l(f"        public ref {csharp_type} this[int x, int y] {{ get {{ fixed ({csharp_type}* pTP = &_item0) return ref *(pTP + x + (y * {array_nums[0]})); }} }}")
            for i in range(0, int(array_nums[0]) * int(array_nums[1])):
                l(f"        private {csharp_type} _item{i};")
            l("    }")
            l("    #pragma warning restore 169")

            l(f"    public {field_name}Collection {field_name};")

            #t0 = f"[{array_nums[0]}][{array_nums[1]}]{csharp_type}"
            #l(f"    {field_name}: {t0} = [_][{array_nums[1]}]{csharp_type}{{[_]{csharp_type}{{ {def_val} }}**{array_nums[1]}}}**{array_nums[0]},")
        else:
            if 'unnamed struct' not in field_type and 'unnamed union' not in field_type and \
               'anonymous' not in field_type:
                # Silently skip true anonymous/unnamed inner types (layout preserved by C side).
                l(f"// FIXME: {field_name}: {field_type};")
            # else: silently skip unnamed/anonymous types not in overrides
    l("}")

def gen_consts(decl, prefix):
    for item in decl['items']:
        l(f"public const int {as_pascal_case(item['name'], prefix)} = {item['value']};")

def gen_enum(decl, prefix):
    l(f"public enum {as_csharp_enum_type(decl['name'], prefix)}")
    l("{")
    for item in decl['items']:
        item_name = as_enum_item_name(item['name'])
        if item_name != "ForceU32":
            if 'value' in item:
                l(f"    {item_name} = {item['value']},")
            else:
                l(f"    {item_name},")
    l("}")

def gen_func_c(decl, prefix):
    c_func_name = decl['name']
    if c_func_name not in web_wrapper_struct_return_functions:
        # bool return types are fully handled in gen_func_csharp (WEB + non-WEB DllImport)
        if funcdecl_result_csharp(decl, prefix) == "bool":
            return
        # funcptr params are fully handled in gen_func_csharp (WEB + non-WEB DllImport)
        if func_has_funcptr_params(decl, prefix):
            return
        # Use framework path on iOS, library name on all other platforms
        l("#if __IOS__")
        l(f"[DllImport(\"@rpath/{current_library_name}.framework/{current_library_name}\", EntryPoint = \"{decl['name']}\", CallingConvention = CallingConvention.Cdecl)]")
        l("#else")
        l(f"[DllImport(\"{current_library_name}\", EntryPoint = \"{decl['name']}\", CallingConvention = CallingConvention.Cdecl)]")
        l("#endif")

def gen_func_csharp(decl, prefix):
    c_func_name = decl['name']
    csharp_func_name = as_pascal_case(check_name_override(decl['name']), prefix)
    csharp_res_type = funcdecl_result_csharp(decl, prefix)

    # Special case for sg_make_shader on WebAssembly
    if c_func_name in web_wrapper_functions:
        l("#if WEB")
        l(f"static extern uint {csharp_func_name}_internal({funcdecl_args_csharp(decl, prefix)});")
        l(f"public static {csharp_res_type} {csharp_func_name}({funcdecl_args_csharp(decl, prefix)})")
        l("{")
        # Handle functions with parameters vs those without
        if decl['params']:
            # Functions like sg_make_shader that take parameters - use the actual parameter names
            param_names = [check_name_override(param['name']) for param in decl['params']]
            param_list = ", ".join(param_names)
            l(f"    uint _id = {csharp_func_name}_internal({param_list});")
        else:
            # Functions like sg_alloc_shader that take no parameters
            l(f"    uint _id = {csharp_func_name}_internal();")

        l(f"    return new {csharp_res_type} {{ id = _id }};")
        l("}")
        l("#else")
        if csharp_res_type == "string":
            # Manual string marshalling for non-WebAssembly and WebAssembly platforms
            l(f"private static extern IntPtr {csharp_func_name}_native({funcdecl_args_csharp(decl, prefix)});")
            l("")
            l(f"public static string {csharp_func_name}({funcdecl_args_csharp(decl, prefix)})")
            l("{")
            if decl['params']:
                param_names = [check_name_override(param['name']) for param in decl['params']]
                param_list = ", ".join(param_names)
                l(f"    IntPtr ptr = {csharp_func_name}_native({param_list});")
            else:
                l(f"    IntPtr ptr = {csharp_func_name}_native();")
            l("    if (ptr == IntPtr.Zero)")
            l("        return \"\";")
            l("")
            l("    // Manual UTF-8 to string conversion to avoid marshalling corruption")
            l("    try")
            l("    {")
            l("        return Marshal.PtrToStringUTF8(ptr) ?? \"\";")
            l("    }")
            l("    catch")
            l("    {")
            l("        // Fallback in case of any marshalling issues")
            l("        return \"\";")
            l("    }")
            l("}")
        else:
            l(f"public static extern {csharp_res_type} {csharp_func_name}({funcdecl_args_csharp(decl, prefix)});")
        l("#endif")
        l("")
        return

      # Special case for large struct return functions in WebAssembly
    if c_func_name in web_wrapper_struct_return_functions:
        l("#if WEB")
        l(f"public static {csharp_res_type} {csharp_func_name}({funcdecl_args_csharp(decl, prefix)})")
        l("{")
        l(f"    {csharp_res_type} result = default;")
        if decl['params']:
            param_names = [check_name_override(param['name']) for param in decl['params']]
            param_list = ", ".join(param_names)
            l(f"    {csharp_func_name}_internal(ref result, {param_list});")
        else:
            l(f"    {csharp_func_name}_internal(ref result);")
        l("    return result;")
        l("}")
        l("#else")
        # Use framework path on iOS, library name on all other platforms
        l("#if __IOS__")
        l(f"[DllImport(\"@rpath/{current_library_name}.framework/{current_library_name}\", EntryPoint = \"{decl['name']}\", CallingConvention = CallingConvention.Cdecl)]")
        l("#else")
        l(f"[DllImport(\"{current_library_name}\", EntryPoint = \"{decl['name']}\", CallingConvention = CallingConvention.Cdecl)]")
        l("#endif")
        if csharp_res_type == "string":
            # Manual string marshalling for WebAssembly to avoid corruption
            l(f"private static extern IntPtr {csharp_func_name}_native({funcdecl_args_csharp(decl, prefix)});")
            l("")
            l(f"public static string {csharp_func_name}({funcdecl_args_csharp(decl, prefix)})")
            l("{")
            if decl['params']:
                param_names = [check_name_override(param['name']) for param in decl['params']]
                param_list = ", ".join(param_names)
                l(f"    IntPtr ptr = {csharp_func_name}_native({param_list});")
            else:
                l(f"    IntPtr ptr = {csharp_func_name}_native();")
            l("    if (ptr == IntPtr.Zero)")
            l("        return \"\";")
            l("")
            l("    // Manual UTF-8 to string conversion to avoid marshalling corruption")
            l("    try")
            l("    {")
            l("        return Marshal.PtrToStringUTF8(ptr) ?? \"\";")
            l("    }")
            l("    catch")
            l("    {")
            l("        // Fallback in case of any marshalling issues")
            l("        return \"\";")
            l("    }")
            l("}")
        else:
            l(f"public static extern {csharp_res_type} {csharp_func_name}({funcdecl_args_csharp(decl, prefix)});")
        l("#endif")
        l("")
        return
      
    if csharp_res_type == "string":
        # Manual string marshalling for all platforms to avoid corruption
        l(f"private static extern IntPtr {csharp_func_name}_native({funcdecl_args_csharp(decl, prefix)});")
        l("")
        l(f"public static string {csharp_func_name}({funcdecl_args_csharp(decl, prefix)})")
        l("{")
        if decl['params']:
            param_names = [check_name_override(param['name']) for param in decl['params']]
            param_list = ", ".join(param_names)
            l(f"    IntPtr ptr = {csharp_func_name}_native({param_list});")
        else:
            l(f"    IntPtr ptr = {csharp_func_name}_native();")
        l("    if (ptr == IntPtr.Zero)")
        l("        return \"\";")
        l("")
        l("    // Manual UTF-8 to string conversion to avoid marshalling corruption")
        l("    try")
        l("    {")
        l("        return Marshal.PtrToStringUTF8(ptr) ?? \"\";")
        l("    }")
        l("    catch")
        l("    {")
        l("        // Fallback in case of any marshalling issues")
        l("        return \"\";")
        l("    }")
        l("}")
    elif csharp_res_type == "bool":
        # bool is not blittable; use byte on WEB (Emscripten ignores MarshalAs) and
        # [return: M(U.I1)] on all other platforms.
        args_decl = funcdecl_args_csharp(decl, prefix)
        if decl['params']:
            param_names = [check_name_override(param['name']) for param in decl['params']]
            param_list = ", ".join(param_names)
        else:
            param_list = ""
        l("#if WEB")
        l(f"[DllImport(\"{current_library_name}\", EntryPoint = \"{decl['name']}\", CallingConvention = CallingConvention.Cdecl)]")
        l(f"private static extern int {csharp_func_name}_native({args_decl});")
        if args_decl:
            l(f"public static bool {csharp_func_name}({args_decl}) => {csharp_func_name}_native({param_list}) != 0;")
        else:
            l(f"public static bool {csharp_func_name}() => {csharp_func_name}_native() != 0;")
        l("#else")
        l("#if __IOS__")
        l(f"[DllImport(\"@rpath/{current_library_name}.framework/{current_library_name}\", EntryPoint = \"{decl['name']}\", CallingConvention = CallingConvention.Cdecl)]")
        l("#else")
        l(f"[DllImport(\"{current_library_name}\", EntryPoint = \"{decl['name']}\", CallingConvention = CallingConvention.Cdecl)]")
        l("#endif")
        l("[return: M(U.I1)]")
        l(f"public static extern bool {csharp_func_name}({args_decl});")
        l("#endif")
    elif func_has_funcptr_params(decl, prefix):
        # delegate* unmanaged<> params cannot be marshalled directly via P/Invoke on WASM NativeAOT.
        # Generate a #if WEB wrapper that uses IntPtr for function pointer params.
        args_decl = funcdecl_args_csharp(decl, prefix)
        args_intptr = funcdecl_args_csharp_intptr(decl, prefix)
        call_args = funcdecl_call_args_with_intptr_cast(decl, prefix)
        l("#if WEB")
        l(f"[DllImport(\"{current_library_name}\", EntryPoint = \"{decl['name']}\", CallingConvention = CallingConvention.Cdecl)]")
        l(f"private static extern {csharp_res_type} {csharp_func_name}_native({args_intptr});")
        l(f"public static {csharp_res_type} {csharp_func_name}({args_decl}) => {csharp_func_name}_native({call_args});")
        l("#else")
        l("#if __IOS__")
        l(f"[DllImport(\"@rpath/{current_library_name}.framework/{current_library_name}\", EntryPoint = \"{decl['name']}\", CallingConvention = CallingConvention.Cdecl)]")
        l("#else")
        l(f"[DllImport(\"{current_library_name}\", EntryPoint = \"{decl['name']}\", CallingConvention = CallingConvention.Cdecl)]")
        l("#endif")
        l(f"public static extern {csharp_res_type} {csharp_func_name}({args_decl});")
        l("#endif")
    else:
        l(f"public static extern {csharp_res_type} {csharp_func_name}({funcdecl_args_csharp(decl, prefix)});")
    l("")

def detect_struct_return_functions(inp):
    """
    Automatically detect functions that return structs by value.
    These need special handling for WebAssembly marshalling.
    """
    global web_wrapper_struct_return_functions
    
    for decl in inp['decls']:
        if not decl['is_dep'] and decl['kind'] == 'func':
            func_name = decl['name']
            
            # Skip if already in web_wrapper_functions (those have special id-based handling)
            if func_name in web_wrapper_functions:
                continue
            
            # Skip if function is in ignore list
            if check_name_ignore(func_name):
                continue
            
            # Extract return type from function signature
            decl_type = decl['type']
            return_type = check_type_override(func_name, 'RESULT', 
                                              decl_type[:decl_type.index('(')].strip())
            
            # Check if return type is a struct (not pointer, not primitive, not void)
            if (is_struct_type(return_type) and 
                not is_struct_ptr(return_type) and 
                not is_const_struct_ptr(return_type) and
                return_type != 'void'):
                
                # Add to the dictionary for WebAssembly wrapper generation
                web_wrapper_struct_return_functions[func_name] = return_type
                print(f"  [AUTO-DETECTED] {func_name} returns struct {return_type}")

def pre_parse(inp):
    global struct_types
    global enum_types
    global struct_type_aliases
    for decl in inp['decls']:
        kind = decl['kind']
        if kind == 'struct':
            struct_types.append(decl['name'])
        elif kind == 'enum':
            enum_name = decl['name']
            enum_types.append(enum_name)
            enum_items[enum_name] = []
            for item in decl['items']:
                enum_items[enum_name].append(as_enum_item_name(item['name']))
        elif kind == 'typedef_alias':
            # Register struct and enum typedef aliases so field type resolution works
            alias_name = decl['name']
            canonical = decl['type']
            # Strip C type qualifiers like "struct " or "enum " from the canonical name
            for qualifier in ('struct ', 'enum ', 'union '):
                if canonical.startswith(qualifier):
                    canonical = canonical[len(qualifier):]
                    break
            if canonical in struct_types:
                struct_type_aliases[alias_name] = canonical
                struct_types.append(alias_name)
            elif canonical in enum_types:
                enum_types.append(alias_name)
    
    # After parsing types, detect struct-returning functions for WebAssembly
    detect_struct_return_functions(inp)

def gen_imports(inp, dep_prefixes):
    for dep_prefix in dep_prefixes:
        dep_module_name = module_names[dep_prefix]
        dep_namespace = namespace_names.get(dep_prefix, default_namespace)
        l(f'using static {dep_namespace}.{dep_module_name};')
        l('')

def gen_internal_functions(inp, prefix):
    """Generate _internal function declarations for web wrapper struct return functions."""
    for decl in inp['decls']:
        if not decl['is_dep'] and decl['kind'] == 'func':
            c_func_name = decl['name']
            if c_func_name in web_wrapper_struct_return_functions:
                csharp_func_name = as_pascal_case(check_name_override(decl['name']), prefix)
                csharp_res_type = funcdecl_result_csharp(decl, prefix)
                
                # Use framework path on iOS, library name on all other platforms
                l("#if __IOS__")
                l(f"[DllImport(\"@rpath/{current_library_name}.framework/{current_library_name}\", EntryPoint = \"{c_func_name}_internal\", CallingConvention = CallingConvention.Cdecl)]")
                l("#else")
                l(f"[DllImport(\"{current_library_name}\", EntryPoint = \"{c_func_name}_internal\", CallingConvention = CallingConvention.Cdecl)]")
                l("#endif")
                if decl['params']:
                    l(f"public static extern void {csharp_func_name}_internal(ref {csharp_res_type} result, {funcdecl_args_csharp(decl, prefix)});")
                else:
                    l(f"public static extern void {csharp_func_name}_internal(ref {csharp_res_type} result);")
                l("")

def gen_c_internal_wrappers_header(all_inputs):
    """Generate C header file with _internal wrapper function implementations (excluding spine-c)."""
    header_lines = []
    header_lines.append("/*")
    header_lines.append("    AUTO-GENERATED C INTERNAL WRAPPER FUNCTIONS")
    header_lines.append("    This file is automatically generated by gen_csharp.py")
    header_lines.append("    DO NOT EDIT MANUALLY")
    header_lines.append("")
    header_lines.append("    WebAssembly/Emscripten cannot marshal structs returned by value through P/Invoke.")
    header_lines.append("    These _internal helper functions work around this limitation by taking an output")
    header_lines.append("    pointer parameter instead.")
    header_lines.append("*/")
    header_lines.append("")
    header_lines.append("#ifndef SOKOL_CSHARP_INTERNAL_WRAPPERS_H")
    header_lines.append("#define SOKOL_CSHARP_INTERNAL_WRAPPERS_H")
    header_lines.append("")
    
    # Group functions by module/prefix (excluding spine-c, ozz, and box2d)
    module_functions = {}
    for inp in all_inputs:
        prefix = inp['prefix']
        if prefix == 'sspine_':  # Skip spine functions - they go in separate header
            continue
        if prefix == 'ozz_':  # Skip ozz functions - they go in separate header
            continue
        if prefix == 'b2':  # Skip box2d functions - they go in separate header
            continue
        if prefix == 'manifold_':  # Skip manifoldc functions - they go in separate header
            continue
        if prefix == 'ma_':  # Skip miniaudio functions - they go in separate header
            continue
            
        module_name = inp['module']
        module_functions[prefix] = {'module': module_name, 'functions': []}
        
        for decl in inp['decls']:
            if not decl['is_dep'] and decl['kind'] == 'func':
                c_func_name = decl['name']
                if c_func_name in web_wrapper_struct_return_functions:
                    module_functions[prefix]['functions'].append(decl)
    
    # Define optional modules that may not be included in all builds
    optional_modules = {
        'SGP': 'SOKOL_GP_INCLUDED',
        'SBasisu': 'SOKOL_BASISU_INCLUDED'
    }
    
    # Generate functions grouped by module
    for prefix, data in module_functions.items():
        if not data['functions']:
            continue
            
        module_name = data['module']
        
        # Check if this is an optional module and add conditional compilation
        if module_name in optional_modules:
            header_lines.append(f"#if defined({optional_modules[module_name]})")
            header_lines.append(f"// ========== {module_name} ({prefix}) ==========")
        else:
            header_lines.append(f"// ========== {module_name} ({prefix}) ==========")
        header_lines.append("")
        
        for decl in data['functions']:
            c_func_name = decl['name']
            return_type = web_wrapper_struct_return_functions[c_func_name]
            
            # Build parameter list for C
            params_c = []
            for param in decl['params']:
                param_type = check_type_override(c_func_name, param['name'], param['type'])
                param_name = param['name']
                
                # Convert type to C syntax
                if is_const_struct_ptr(param_type):
                    params_c.append(f"{param_type} {param_name}")
                elif is_prim_type(param_type):
                    params_c.append(f"{param_type} {param_name}")
                else:
                    params_c.append(f"{param_type} {param_name}")
            
            params_str = ", ".join(params_c) if params_c else ""
            if params_str:
                params_str = ", " + params_str
            
            # Build argument list for function call
            args = [param['name'] for param in decl['params']]
            args_str = ", ".join(args)
            
            header_lines.append(f"SOKOL_API_IMPL void {c_func_name}_internal({return_type}* result{params_str}) {{")
            header_lines.append(f"    *result = {c_func_name}({args_str});")
            header_lines.append("}")
            header_lines.append("")
        
        # Close conditional compilation for optional modules
        if module_name in optional_modules:
            header_lines.append(f"#endif // {optional_modules[module_name]}")
            header_lines.append("")
    
    header_lines.append("#endif // SOKOL_CSHARP_INTERNAL_WRAPPERS_H")
    header_lines.append("")
    
    return "\n".join(header_lines)

def gen_c_spine_wrappers_header(all_inputs):
    """Generate C header file with spine-c _internal wrapper function implementations."""
    header_lines = []
    header_lines.append("/*")
    header_lines.append("    AUTO-GENERATED SPINE-C INTERNAL WRAPPER FUNCTIONS")
    header_lines.append("    This file is automatically generated by gen_csharp.py")
    header_lines.append("    DO NOT EDIT MANUALLY")
    header_lines.append("")
    header_lines.append("    WebAssembly/Emscripten cannot marshal structs returned by value through P/Invoke.")
    header_lines.append("    These _internal helper functions work around this limitation by taking an output")
    header_lines.append("    pointer parameter instead.")
    header_lines.append("*/")
    header_lines.append("")
    header_lines.append("#ifndef SPINE_C_CSHARP_INTERNAL_WRAPPERS_H")
    header_lines.append("#define SPINE_C_CSHARP_INTERNAL_WRAPPERS_H")
    header_lines.append("")
    header_lines.append("#include <spine/spine.h>")
    header_lines.append("#include \"../sokol/util/sokol_spine.h\"")
    header_lines.append("")
    header_lines.append("// For Emscripten builds, these functions need to be exported")
    header_lines.append("#ifdef __EMSCRIPTEN__")
    header_lines.append("    #include <emscripten.h>")
    header_lines.append("    #define SPINE_EXPORT EMSCRIPTEN_KEEPALIVE")
    header_lines.append("#else")
    header_lines.append("    #define SPINE_EXPORT")
    header_lines.append("#endif")
    header_lines.append("")
    
    # Filter only spine-related functions
    for inp in all_inputs:
        prefix = inp['prefix']
        if prefix != 'sspine_':  # Only process spine functions
            continue
            
        module_name = inp['module']
        has_functions = False
        
        for decl in inp['decls']:
            if not decl['is_dep'] and decl['kind'] == 'func':
                c_func_name = decl['name']
                if c_func_name in web_wrapper_struct_return_functions:
                    if not has_functions:
                        header_lines.append(f"// ========== {module_name} ({prefix}) ==========")
                        header_lines.append("")
                        has_functions = True
                    
                    return_type = web_wrapper_struct_return_functions[c_func_name]
                    
                    # Build parameter list for C
                    params_c = []
                    for param in decl['params']:
                        param_type = check_type_override(c_func_name, param['name'], param['type'])
                        param_name = param['name']
                        params_c.append(f"{param_type} {param_name}")

                    params_str = ", ".join(params_c) if params_c else ""
                    if params_str:
                        params_str = ", " + params_str

                    # Build argument list for function call
                    args = [param['name'] for param in decl['params']]
                    args_str = ", ".join(args)

                    header_lines.append(f"SPINE_EXPORT void {c_func_name}_internal({return_type}* result{params_str}) {{")
                    header_lines.append(f"    *result = {c_func_name}({args_str});")
                    header_lines.append("}")
                    header_lines.append("")
    
    header_lines.append("#endif // SPINE_C_CSHARP_INTERNAL_WRAPPERS_H")
    header_lines.append("")
    
    return "\n".join(header_lines)

def gen_c_ozzutil_wrappers_header(all_inputs):
    """Generate C header file with ozzutil _internal wrapper function implementations."""
    header_lines = []
    header_lines.append("/*")
    header_lines.append("    AUTO-GENERATED OZZUTIL INTERNAL WRAPPER FUNCTIONS")
    header_lines.append("    This file is automatically generated by gen_csharp.py")
    header_lines.append("    DO NOT EDIT MANUALLY")
    header_lines.append("")
    header_lines.append("    WebAssembly/Emscripten cannot marshal structs returned by value through P/Invoke.")
    header_lines.append("    These _internal helper functions work around this limitation by taking an output")
    header_lines.append("    pointer parameter instead.")
    header_lines.append("*/")
    header_lines.append("")
    header_lines.append("#ifndef OZZUTIL_CSHARP_INTERNAL_WRAPPERS_H")
    header_lines.append("#define OZZUTIL_CSHARP_INTERNAL_WRAPPERS_H")
    header_lines.append("")
    header_lines.append("#include \"ozzutil.h\"")
    header_lines.append("")
    header_lines.append("// For Emscripten builds, these functions need to be exported")
    header_lines.append("#ifdef __EMSCRIPTEN__")
    header_lines.append("    #include <emscripten.h>")
    header_lines.append("    #define OZZUTIL_EXPORT EMSCRIPTEN_KEEPALIVE")
    header_lines.append("#else")
    header_lines.append("    #define OZZUTIL_EXPORT")
    header_lines.append("#endif")
    header_lines.append("")
    
    # Filter only ozz-related functions
    for inp in all_inputs:
        prefix = inp['prefix']
        if prefix != 'ozz_':  # Only process ozz functions
            continue
            
        module_name = inp['module']
        has_functions = False
        
        for decl in inp['decls']:
            if not decl['is_dep'] and decl['kind'] == 'func':
                c_func_name = decl['name']
                if c_func_name in web_wrapper_struct_return_functions:
                    if not has_functions:
                        header_lines.append(f"// ========== {module_name} ({prefix}) ==========")
                        header_lines.append("")
                        has_functions = True
                    
                    return_type = web_wrapper_struct_return_functions[c_func_name]
                    
                    # Build parameter list for C
                    params_c = []
                    for param in decl['params']:
                        param_type = check_type_override(c_func_name, param['name'], param['type'])
                        param_name = param['name']
                        params_c.append(f"{param_type} {param_name}")

                    params_str = ", ".join(params_c) if params_c else ""
                    if params_str:
                        params_str = ", " + params_str

                    # Build argument list for function call
                    args = [param['name'] for param in decl['params']]
                    args_str = ", ".join(args)

                    header_lines.append(f"OZZUTIL_EXPORT void {c_func_name}_internal({return_type}* result{params_str}) {{")
                    header_lines.append(f"    *result = {c_func_name}({args_str});")
                    header_lines.append("}")
                    header_lines.append("")
    
    header_lines.append("#endif // OZZUTIL_CSHARP_INTERNAL_WRAPPERS_H")
    header_lines.append("")
    
    return "\n".join(header_lines)

def gen_c_box2d_wrappers_header(all_inputs):
    """Generate C header file with box2d _internal wrapper function implementations."""
    header_lines = []
    header_lines.append("/*")
    header_lines.append("    AUTO-GENERATED BOX2D INTERNAL WRAPPER FUNCTIONS")
    header_lines.append("    This file is automatically generated by gen_csharp.py")
    header_lines.append("    DO NOT EDIT MANUALLY")
    header_lines.append("")
    header_lines.append("    WebAssembly/Emscripten cannot marshal structs returned by value through P/Invoke.")
    header_lines.append("    These _internal helper functions work around this limitation by taking an output")
    header_lines.append("    pointer parameter instead.")
    header_lines.append("*/")
    header_lines.append("")
    header_lines.append("#ifndef BOX2D_CSHARP_INTERNAL_WRAPPERS_H")
    header_lines.append("#define BOX2D_CSHARP_INTERNAL_WRAPPERS_H")
    header_lines.append("")
    header_lines.append("#include <box2d/box2d.h>")
    header_lines.append("#include <box2d/math_functions.h>")
    header_lines.append("")
    header_lines.append("// For Emscripten builds, these functions need to be exported")
    header_lines.append("#ifdef __EMSCRIPTEN__")
    header_lines.append("    #include <emscripten.h>")
    header_lines.append("    #define BOX2D_EXPORT EMSCRIPTEN_KEEPALIVE")
    header_lines.append("#else")
    header_lines.append("    #define BOX2D_EXPORT")
    header_lines.append("#endif")
    header_lines.append("")
    
    # Filter only box2d-related functions
    for inp in all_inputs:
        prefix = inp['prefix']
        if prefix != 'b2':  # Only process box2d functions
            continue
            
        module_name = inp['module']
        has_functions = False
        
        for decl in inp['decls']:
            if not decl['is_dep'] and decl['kind'] == 'func':
                c_func_name = decl['name']
                if c_func_name in web_wrapper_struct_return_functions:
                    if not has_functions:
                        header_lines.append(f"// ========== {module_name} ({prefix}) ===========")
                        header_lines.append("")
                        has_functions = True
                    
                    return_type = web_wrapper_struct_return_functions[c_func_name]
                    
                    # Build parameter list for C
                    params_c = []
                    for param in decl['params']:
                        param_type = check_type_override(c_func_name, param['name'], param['type'])
                        param_name = param['name']
                        params_c.append(f"{param_type} {param_name}")

                    params_str = ", ".join(params_c) if params_c else ""
                    if params_str:
                        params_str = ", " + params_str

                    # Build argument list for function call
                    args = [param['name'] for param in decl['params']]
                    args_str = ", ".join(args)

                    header_lines.append(f"BOX2D_EXPORT void {c_func_name}_internal({return_type}* result{params_str}) {{")
                    header_lines.append(f"    *result = {c_func_name}({args_str});")
                    header_lines.append("}")
                    header_lines.append("")
    
    header_lines.append("#endif // BOX2D_CSHARP_INTERNAL_WRAPPERS_H")
    header_lines.append("")
    
    return "\n".join(header_lines)

def gen_c_manifoldc_wrappers_header(all_inputs):
    """Generate C header file with manifoldc _internal wrapper function implementations."""
    header_lines = []
    header_lines.append("/*")
    header_lines.append("    AUTO-GENERATED MANIFOLDC INTERNAL WRAPPER FUNCTIONS")
    header_lines.append("    This file is automatically generated by gen_csharp.py")
    header_lines.append("    DO NOT EDIT MANUALLY")
    header_lines.append("")
    header_lines.append("    WebAssembly/Emscripten cannot marshal structs returned by value through P/Invoke.")
    header_lines.append("    These _internal helper functions work around this limitation by taking an output")
    header_lines.append("    pointer parameter instead.")
    header_lines.append("*/")
    header_lines.append("")
    header_lines.append("#ifndef MANIFOLDC_CSHARP_INTERNAL_WRAPPERS_H")
    header_lines.append("#define MANIFOLDC_CSHARP_INTERNAL_WRAPPERS_H")
    header_lines.append("")
    header_lines.append("#include <manifold/manifoldc.h>")
    header_lines.append("#include <manifold/types.h>")
    header_lines.append("")
    header_lines.append("// For Emscripten builds, these functions need to be exported")
    header_lines.append("#ifdef __EMSCRIPTEN__")
    header_lines.append("    #include <emscripten.h>")
    header_lines.append("    #define MANIFOLDC_EXPORT EMSCRIPTEN_KEEPALIVE")
    header_lines.append("#else")
    header_lines.append("    #define MANIFOLDC_EXPORT")
    header_lines.append("#endif")
    header_lines.append("")

    # Filter only manifoldc-related functions
    for inp in all_inputs:
        prefix = inp['prefix']
        if prefix != 'manifold_':  # Only process manifoldc functions
            continue

        module_name = inp['module']
        has_functions = False

        for decl in inp['decls']:
            if not decl['is_dep'] and decl['kind'] == 'func':
                c_func_name = decl['name']
                if c_func_name in web_wrapper_struct_return_functions:
                    if not has_functions:
                        header_lines.append(f"// ========== {module_name} ({prefix}) ===========")
                        header_lines.append("")
                        has_functions = True

                    return_type = web_wrapper_struct_return_functions[c_func_name]

                    # Build parameter list for C
                    params_c = []
                    for param in decl['params']:
                        param_type = check_type_override(c_func_name, param['name'], param['type'])
                        param_name = param['name']
                        params_c.append(f"{param_type} {param_name}")

                    params_str = ", ".join(params_c) if params_c else ""
                    if params_str:
                        params_str = ", " + params_str

                    # Build argument list for function call
                    args = [param['name'] for param in decl['params']]
                    args_str = ", ".join(args)

                    header_lines.append(f"MANIFOLDC_EXPORT void {c_func_name}_internal({return_type}* result{params_str}) {{")
                    header_lines.append(f"    *result = {c_func_name}({args_str});")
                    header_lines.append("}")
                    header_lines.append("")

    header_lines.append("#endif // MANIFOLDC_CSHARP_INTERNAL_WRAPPERS_H")
    header_lines.append("")

    return "\n".join(header_lines)

def gen_c_miniaudio_wrappers_header(all_inputs):
    """Generate C header file with miniaudio _internal wrapper function implementations."""
    header_lines = []
    header_lines.append("/*")
    header_lines.append("    AUTO-GENERATED MINIAUDIO INTERNAL WRAPPER FUNCTIONS")
    header_lines.append("    This file is automatically generated by gen_csharp.py")
    header_lines.append("    DO NOT EDIT MANUALLY")
    header_lines.append("")
    header_lines.append("    WebAssembly/Emscripten cannot marshal structs returned by value through P/Invoke.")
    header_lines.append("    These _internal helper functions work around this limitation by taking an output")
    header_lines.append("    pointer parameter instead.")
    header_lines.append("*/")
    header_lines.append("")
    header_lines.append("#ifndef MINIAUDIO_CSHARP_INTERNAL_WRAPPERS_H")
    header_lines.append("#define MINIAUDIO_CSHARP_INTERNAL_WRAPPERS_H")
    header_lines.append("")
    header_lines.append("#define MA_NO_IMPLEMENTATION")
    header_lines.append("#include \"miniaudio.h\"")
    header_lines.append("")
    header_lines.append("// For Emscripten builds, these functions need to be exported")
    header_lines.append("#ifdef __EMSCRIPTEN__")
    header_lines.append("    #include <emscripten.h>")
    header_lines.append("    #define MINIAUDIO_EXPORT EMSCRIPTEN_KEEPALIVE")
    header_lines.append("#else")
    header_lines.append("    #define MINIAUDIO_EXPORT")
    header_lines.append("#endif")
    header_lines.append("")

    # Filter only miniaudio-related functions
    for inp in all_inputs:
        prefix = inp['prefix']
        if prefix != 'ma_':  # Only process miniaudio functions
            continue

        module_name = inp['module']
        has_functions = False

        for decl in inp['decls']:
            if not decl['is_dep'] and decl['kind'] == 'func':
                c_func_name = decl['name']
                if c_func_name in web_wrapper_struct_return_functions:
                    if not has_functions:
                        header_lines.append(f"// ========== {module_name} ({prefix}) ===========")
                        header_lines.append("")
                        has_functions = True

                    return_type = web_wrapper_struct_return_functions[c_func_name]

                    params_c = []
                    for param in decl['params']:
                        param_type = check_type_override(c_func_name, param['name'], param['type'])
                        param_name = param['name']
                        params_c.append(f"{param_type} {param_name}")

                    params_str = ", ".join(params_c) if params_c else ""
                    if params_str:
                        params_str = ", " + params_str

                    args = [param['name'] for param in decl['params']]
                    args_str = ", ".join(args)

                    header_lines.append(f"MINIAUDIO_EXPORT void {c_func_name}_internal({return_type}* result{params_str}) {{")
                    header_lines.append(f"    *result = {c_func_name}({args_str});")
                    header_lines.append("}")
                    header_lines.append("")

    header_lines.append("#endif // MINIAUDIO_CSHARP_INTERNAL_WRAPPERS_H")
    header_lines.append("")

    return "\n".join(header_lines)

def gen_c_tsf_wrappers_header(all_inputs):
    """Generate C header file with TinySoundFont _internal wrapper function implementations."""
    header_lines = []
    header_lines.append("/*")
    header_lines.append("    AUTO-GENERATED TINYSOUNDFONT INTERNAL WRAPPER FUNCTIONS")
    header_lines.append("    This file is automatically generated by gen_csharp.py")
    header_lines.append("    DO NOT EDIT MANUALLY")
    header_lines.append("")
    header_lines.append("    WebAssembly/Emscripten cannot marshal structs returned by value through P/Invoke.")
    header_lines.append("    These _internal helper functions work around this limitation by taking an output")
    header_lines.append("    pointer parameter instead.")
    header_lines.append("*/")
    header_lines.append("")
    header_lines.append("#ifndef TSF_CSHARP_INTERNAL_WRAPPERS_H")
    header_lines.append("#define TSF_CSHARP_INTERNAL_WRAPPERS_H")
    header_lines.append("")
    header_lines.append("#include \"tsf.h\"")
    header_lines.append("#include \"tml.h\"")
    header_lines.append("")
    header_lines.append("// For Emscripten builds, these functions need to be exported")
    header_lines.append("#ifdef __EMSCRIPTEN__")
    header_lines.append("    #include <emscripten.h>")
    header_lines.append("    #define TSF_EXPORT EMSCRIPTEN_KEEPALIVE")
    header_lines.append("#else")
    header_lines.append("    #define TSF_EXPORT")
    header_lines.append("#endif")
    header_lines.append("")

    for inp in all_inputs:
        prefix = inp['prefix']
        if prefix not in ('tsf_', 'tml_'):
            continue

        module_name = inp['module']
        has_functions = False

        for decl in inp['decls']:
            if not decl['is_dep'] and decl['kind'] == 'func':
                c_func_name = decl['name']
                if c_func_name in web_wrapper_struct_return_functions:
                    if not has_functions:
                        header_lines.append(f"// ========== {module_name} ({prefix}) ===========")
                        header_lines.append("")
                        has_functions = True

                    return_type = web_wrapper_struct_return_functions[c_func_name]

                    params_c = []
                    for param in decl['params']:
                        param_type = check_type_override(c_func_name, param['name'], param['type'])
                        param_name = param['name']
                        params_c.append(f"{param_type} {param_name}")

                    params_str = ", ".join(params_c) if params_c else ""
                    if params_str:
                        params_str = ", " + params_str

                    args = [param['name'] for param in decl['params']]
                    args_str = ", ".join(args)

                    header_lines.append(f"TSF_EXPORT void {c_func_name}_internal({return_type}* result{params_str}) {{")
                    header_lines.append(f"    *result = {c_func_name}({args_str});")
                    header_lines.append("}")
                    header_lines.append("")

    header_lines.append("#endif // TSF_CSHARP_INTERNAL_WRAPPERS_H")
    header_lines.append("")

    return "\n".join(header_lines)

def gen_module(inp, dep_prefixes):
    l('// machine generated, do not edit')
    l('using System;')
    l('using System.Runtime.InteropServices;')
    l('using M = System.Runtime.InteropServices.MarshalAsAttribute;')
    l('using U = System.Runtime.InteropServices.UnmanagedType;')
    l('')
    gen_imports(inp, dep_prefixes)
    pre_parse(inp)
    prefix = inp['prefix']
    ns = namespace_names.get(prefix, default_namespace)
    l(f"namespace {ns}")
    l("{")
    # TinyEXR is excluded from web builds (CMakeLists.txt excludes tinyexr for Emscripten)
    if inp['module'] == 'TinyEXR':
        l("#if !WEB")
    l(f"public static unsafe partial class {inp['module']}")
    l("{")
    for extra_line in extra_declarations.get(prefix, []):
        l(extra_line)
    if extra_declarations.get(prefix):
        l("")
    for decl in inp['decls']:
        if not decl['is_dep']:
            kind = decl['kind']
            if kind == 'consts':
                gen_consts(decl, prefix)
            elif not check_name_ignore(decl['name']):
                if kind == 'struct':
                    gen_struct(decl, prefix)
                elif kind == 'enum':
                    gen_enum(decl, prefix)
                elif kind == 'func':
                    gen_func_c(decl, prefix)
                    gen_func_csharp(decl, prefix)
                elif kind == 'typedef_alias':
                    pass  # aliases are only used for type resolution, no code emitted
    # Generate _internal function declarations for WebAssembly
    gen_internal_functions(inp, prefix)
    l("}")
    # Close the #if !WEB directive for TinyEXR
    if inp['module'] == 'TinyEXR':
        l("#endif")
    l("}")

def prepare():
    print('Generating C# bindings:')

def gen(c_header_path, c_prefix, dep_c_prefixes):
    global current_library_name
    module_name = module_names[c_prefix]
    c_source_path = c_source_paths[c_prefix]
    print(f'  {c_header_path} => {module_name} (lib: {library_names.get(c_prefix, "sokol")})')
    reset_globals()
    current_library_name = library_names.get(c_prefix, 'sokol')  # Set library name AFTER reset_globals
    extra_dirs = extra_include_dirs.get(c_prefix, None)
    extra_api = extra_api_prefixes.get(c_prefix, None)
    ir = gen_ir.gen(c_header_path, c_source_path, module_name, c_prefix, dep_c_prefixes, extra_include_dirs=extra_dirs, extra_api_prefixes=extra_api)
    gen_module(ir, dep_c_prefixes)
    output_dir = output_dirs.get(c_prefix, default_output_dir)
    output_path = f"{output_dir}/{ir['module']}.cs"
    with open(output_path, 'w', newline='\n') as f_outp:
        f_outp.write(out_lines)
    return ir  # Return IR for header generation