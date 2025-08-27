
#define SOKOL_IMPL
#define SOKOL_DLL
#ifndef __ANDROID__
    #define SOKOL_NO_ENTRY
#endif
#define SOKOL_NO_DEPRECATED
#define SOKOL_TRACE_HOOKS
#define SOKOL_FETCH_API_DECL

#include "sokol_defines.h"
#include "sokol_app.h"
#include "sokol_gfx.h"
#include "sokol_glue.h"
#include "sokol_audio.h"
#include "sokol_time.h"
#include "sokol_log.h"
#include "sokol_shape.h"
#include "sokol_gl.h"
#include "sokol_fetch.h"
#define SOKOL_DEBUGTEXT_IMPL
#include "sokol_debugtext.h"
//TBD ELI #include "sokol_gp.h"
#define PL_MPEG_IMPLEMENTATION
#include "pl_mpeg/pl_mpeg.h"

// #include "basisu/sokol_basisu.h"
#define CGLTF_IMPLEMENTATION
#include "cgltf.h"

#define CIMGUI_DEFINE_ENUMS_AND_STRUCTS
#include "dcimgui/src/cimgui.h"

//TBD ELI , needs fixing
// #define SOKOL_IMGUI_IMPL
// #include "sokol_imgui.h"

int sdtx_print_wrapper(const char* str)
{
    return sdtx_printf("%s", str);
}


// TBD elix22
#ifdef __ANDROID__
sapp_desc sokol_main(int argc, char* argv[]) {
    // Your application initialization code here
    sapp_desc desc = {0};
    return desc;
}
#endif

