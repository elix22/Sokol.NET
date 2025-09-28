
#ifdef __EMSCRIPTEN__
// Emscripten utility functions for EM_JS code in sokol_app.h
// These are provided by the JavaScript library sokol_js_lib.js
// and linked automatically when building with Emscripten

#include <emscripten.h>

// Forward declarations - actual implementations are in sokol_js_lib.js
extern int stringToUTF8OnStack(const char* str);
extern void withStackSave(void (*func)(void));
extern void* findCanvasEventTarget(const char* target);

#endif

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
#include "cimgui/cimgui.h"
#ifndef ImTextureID_Invalid
#define ImTextureID_Invalid     ((ImTextureID)0)
#endif
#define SOKOL_IMGUI_IMPL
#include "sokol_imgui.h"

#define SOKOL_GFX_IMGUI_IMPL
#include "sokol_gfx_imgui.h"


int sdtx_print_wrapper(const char* str)
{
    return sdtx_printf("%s", str);
}

static sgimgui_t sgimgui_ctx = {0};

SOKOL_API_IMPL sgimgui_t * sgimgui_init_csharp(void) {
 
    sgimgui_init(&sgimgui_ctx, &(sgimgui_desc_t){0});
    return &sgimgui_ctx;
}


// TBD elix22
#ifdef __ANDROID__
sapp_desc sokol_main(int argc, char* argv[]) {
    // Your application initialization code here
    sapp_desc desc = {0};
    return desc;
}
#endif


