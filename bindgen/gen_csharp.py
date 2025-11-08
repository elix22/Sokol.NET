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
    # 'sgp_':     'SGP',
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
}

name_ignores = [
    'sdtx_printf',
    'sdtx_vprintf',
    'sg_install_trace_hooks',
    'sg_trace_hooks',
    'cgltf_camera',
    'sg_color', # will be create manually inorder to support additional vonversion to Vector3,Vector4,float[] , Span
    'sgimgui_init',
    'sgimgui_t', # struct
    'fonsSetErrorCallback', # function pointer callback not supported

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
}


prim_types = {
    'int':          'int',
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
    'sgimgui_t *': 'IntPtr',
    'unsigned char': 'byte',
    'unsigned char *': 'byte*',
    'EXRHeader *': 'IntPtr',
    'const char ***': 'IntPtr',
    'const EXRImage *': 'IntPtr',
    'const char **': 'IntPtr',
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
out_lines = ''
current_library_name = 'sokol'  # Default library name, will be set per module

def reset_globals():
    global struct_types
    global enum_types
    global enum_items
    global out_lines
    global web_wrapper_struct_return_functions
    global current_library_name
    struct_types = []
    enum_types = []
    enum_items = {}
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
    return s

# prefix_bla_blub(_t) => (dep.)BlaBlub
def as_csharp_enum_type(s, prefix):
    return s

def check_type_override(func_or_struct_name, field_or_arg_name, orig_type):
    s = f"{func_or_struct_name}.{field_or_arg_name}"
    if s in type_overrides:
        return type_overrides[s]
    else:
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
    return s in struct_types

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
        if s == f"{struct_type} *":
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
    # Remove 'const' and pointer markers to get the base type
    # e.g., "const unsigned char *" -> "unsigned char"
    s = s.replace('const', '').replace('*', '').strip()
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
        # not a bug, pass const structs by value
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
    l(f"[StructLayout(LayoutKind.Sequential)]")
    l(f"public struct {csharp_type}")
    l("{")
    for field in decl['fields']:
        field_name = as_pascal_case(check_name_override(field['name']), "")
        field_type = field['type']
        field_type = check_type_override(struct_name, field_name, field_type)
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
        elif is_struct_ptr(field_type):
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
            l(f"// FIXME: {field_name}: {field_type};")
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
    
    # After parsing types, detect struct-returning functions for WebAssembly
    detect_struct_return_functions(inp)

def gen_imports(inp, dep_prefixes):
    for dep_prefix in dep_prefixes:
        dep_module_name = module_names[dep_prefix]
        l(f'using static Sokol.{dep_module_name};')
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
    
    # Group functions by module/prefix (excluding spine-c and ozz)
    module_functions = {}
    for inp in all_inputs:
        prefix = inp['prefix']
        if prefix == 'sspine_':  # Skip spine functions - they go in separate header
            continue
        if prefix == 'ozz_':  # Skip ozz functions - they go in separate header
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
                    
                    header_lines.append(f"OZZUTIL_EXPORT void {c_func_name}_internal({return_type}* result{params_str}) {{")
                    header_lines.append(f"    *result = {c_func_name}({args_str});")
                    header_lines.append("}")
                    header_lines.append("")
    
    header_lines.append("#endif // OZZUTIL_CSHARP_INTERNAL_WRAPPERS_H")
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
    l("namespace Sokol")
    l("{")
    l(f"public static unsafe partial class {inp['module']}")
    l("{")
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
    # Generate _internal function declarations for WebAssembly
    gen_internal_functions(inp, prefix)
    l("}")
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
    ir = gen_ir.gen(c_header_path, c_source_path, module_name, c_prefix, dep_c_prefixes)
    gen_module(ir, dep_c_prefixes)
    output_path = f"../src/sokol/generated/{ir['module']}.cs"
    with open(output_path, 'w', newline='\n') as f_outp:
        f_outp.write(out_lines)
    return ir  # Return IR for header generation