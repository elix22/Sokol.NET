import gen_csharp

tasks = [
    [ '../ext/sokol/sokol_log.h',            'slog_',     [] ],
    [ '../ext/sokol/sokol_gfx.h',            'sg_',       [] ],
    [ '../ext/sokol/sokol_app.h',            'sapp_',     [] ],
    [ '../ext/sokol/sokol_glue.h',           'sglue_',    ['sg_'] ],
    [ '../ext/sokol/sokol_time.h',           'stm_',      [] ],
    [ '../ext/sokol/sokol_audio.h',          'saudio_',   [] ],
    [ '../ext/sokol/sokol_fetch.h',          'sfetch_',   [] ],
    [ '../ext/sokol/util/sokol_gl.h',        'sgl_',      ['sg_'] ],
    [ '../ext/sokol/util/sokol_debugtext.h', 'sdtx_',     ['sg_'] ],
    [ '../ext/sokol/util/sokol_shape.h',     'sshape_',   ['sg_'] ],
    [ '../ext/sokol/util/sokol_spine.h',     'sspine_',   ['sg_'] ],
    [ '../ext/sokol_gp/sokol_gp.h',          'sgp_',      ['sg_'] ],
    [ '../ext/cgltf/cgltf.h',                'cgltf_',    [] ],
    [ '../ext/basisu/sokol_basisu.h',        'sbasisu_',  ['sg_'] ],
    [ '../ext/sokol/util/sokol_imgui.h',     'simgui_',   ['sg_','sapp_'] ],
    [ '../ext/sokol/util/sokol_gfx_imgui.h', 'sgimgui_',   ['sg_','sapp_'] ],
    [ '../ext/sokol/util/sokol_fontstash.h', 'sfons_',   ['sg_','sapp_'] ],
    [ '../ext/fontstash/fontstash.h',        'fons',     [] ],
    [ '../ext/stb/stb_image.h',              'stbi_',    [] ],
    [ '../ext/ozzutil/ozzutil.h',            'ozz_',     ['sg_'] ],
    [ '../ext/tinyexr/tinyexr.h',            'EXR',      [] ],
    [ '../ext/box2d/include/box2d/box2d.h',  'b2',      [] ],
    [ '../ext/camerac/include/camerac.h',    'cam',      [] ],
    [ '../ext/nearnet/include/nearnet.h',    'nn_',      [] ],
    [ '../ext/manifold/bindings/c/include/manifold/manifoldc.h', 'manifold_', [] ],
    [ '../ext/sokol/sokol_filesystem.h',     'sfs_',     [] ],
    [ '../ext/nanovg/src/sokol_nanovg.h',    'nvg',      ['sg_'] ],
    [ '../ext/nanosvg/src/nanosvg.h',        'nsvg',     [] ],
    [ '../ext/nanosvg/src/nanosvgrast.h',    'nsvg',     [] ],
    [ '../ext/miniaudio/miniaudio.h',        'ma_',      [] ],
    [ '../ext/TinySoundFont/tsf.h',          'tsf_',     [] ],
    [ '../ext/TinySoundFont/tml.h',          'tml_',     ['tsf_'] ],

]

#C Raw
gen_csharp.prepare()

# Clear the auto-detected struct return functions from previous runs
gen_csharp.web_wrapper_struct_return_functions = {}

all_irs = []
for task in tasks:
    [c_header_path, main_prefix, dep_prefixes] = task
    ir = gen_csharp.gen(c_header_path, main_prefix, dep_prefixes)
    all_irs.append(ir)

# Generate C header file with internal wrapper implementations
print('Generating C internal wrappers header...')
print(f'  Auto-detected {len(gen_csharp.web_wrapper_struct_return_functions)} functions returning structs by value')

# Generate sokol wrappers header (excludes spine-c)
sokol_header_content = gen_csharp.gen_c_internal_wrappers_header(all_irs)
sokol_header_output_path = '../ext/sokol_csharp_internal_wrappers.h'
with open(sokol_header_output_path, 'w', newline='\n') as f_header:
    f_header.write(sokol_header_content)
print(f'  Generated Sokol wrappers: {sokol_header_output_path}')

# Generate spine-c wrappers header (only spine-c functions)
spine_header_content = gen_csharp.gen_c_spine_wrappers_header(all_irs)
spine_header_output_path = '../ext/spine-c/spine_c_csharp_internal_wrappers.h'
with open(spine_header_output_path, 'w', newline='\n') as f_header:
    f_header.write(spine_header_content)
print(f'  Generated Spine-C wrappers: {spine_header_output_path}')

# Generate ozzutil wrappers header (only ozz functions)
ozzutil_header_content = gen_csharp.gen_c_ozzutil_wrappers_header(all_irs)
ozzutil_header_output_path = '../ext/ozzutil/ozzutil_csharp_internal_wrappers.h'
with open(ozzutil_header_output_path, 'w', newline='\n') as f_header:
    f_header.write(ozzutil_header_content)
print(f'  Generated OzzUtil wrappers: {ozzutil_header_output_path}')

# Generate box2d wrappers header (only box2d functions)
box2d_header_content = gen_csharp.gen_c_box2d_wrappers_header(all_irs)
box2d_header_output_path = '../ext/box2d/box2d_csharp_internal_wrappers.h'
with open(box2d_header_output_path, 'w', newline='\n') as f_header:
    f_header.write(box2d_header_content)
print(f'  Generated Box2D wrappers: {box2d_header_output_path}')

# Generate manifoldc wrappers header (only manifold functions)
manifoldc_header_content = gen_csharp.gen_c_manifoldc_wrappers_header(all_irs)
manifoldc_header_output_path = '../ext/manifold/bindings/c/include/manifoldc_csharp_internal_wrappers.h'
with open(manifoldc_header_output_path, 'w', newline='\n') as f_header:
    f_header.write(manifoldc_header_content)
print(f'  Generated Manifoldc wrappers: {manifoldc_header_output_path}')

# Generate miniaudio wrappers header (only miniaudio functions)
miniaudio_header_content = gen_csharp.gen_c_miniaudio_wrappers_header(all_irs)
miniaudio_header_output_path = '../ext/miniaudio/miniaudio_csharp_internal_wrappers.h'
with open(miniaudio_header_output_path, 'w', newline='\n') as f_header:
    f_header.write(miniaudio_header_content)
print(f'  Generated miniaudio wrappers: {miniaudio_header_output_path}')

# Generate TinySoundFont wrappers header (only tsf_/tml_ functions)
tsf_header_content = gen_csharp.gen_c_tsf_wrappers_header(all_irs)
tsf_header_output_path = '../ext/TinySoundFont/tsf_csharp_internal_wrappers.h'
with open(tsf_header_output_path, 'w', newline='\n') as f_header:
    f_header.write(tsf_header_content)
print(f'  Generated TinySoundFont wrappers: {tsf_header_output_path}')