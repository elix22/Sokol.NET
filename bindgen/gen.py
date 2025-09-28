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
    
]

#C Raw
gen_csharp.prepare()
for task in tasks:
    [c_header_path, main_prefix, dep_prefixes] = task
    gen_csharp.gen(c_header_path, main_prefix, dep_prefixes)