#ifndef SOKOL_CAPTURE_INCLUDED
/*
    sokol_capture.h -- read pixels back from a sokol-gfx render-target image

    Project URL: https://github.com/elix22/Sokol.NET

    Do this:
        #define SOKOL_IMPL or #define SOKOL_CAPTURE_IMPL
    before you include this file in *one* C or ObjC file to create the
    implementation.

    sokol_gfx.h must be included before sokol_capture.h.

    WHY THIS EXISTS
    ===============
    sokol_gfx.h deliberately has no readback API (a GPU->CPU copy forces a sync
    stall). But it does expose the native texture handles per backend, which is
    enough to implement readback outside of sokol_gfx itself -- no fork needed.
    This module wraps that per-backend code behind one function so an app can
    grab its own framebuffer and write a screenshot from the inside, with no OS
    screenshot tooling involved (which is the only option on a device Apple's
    lockdown tooling can no longer screenshot, and the only option at all for
    headless / CI / arbitrary-viewport captures).

    HOW TO USE
    ==========
    Render the frame into an offscreen render-target image instead of the
    swapchain, then, *after* sg_commit(), read that image back:

        uint8_t* pixels = malloc(w * h * 4);
        if (scap_read_image(color_img, w, h, pixels, w * h * 4)) {
            ... encode/write pixels ...
        } else {
            log(scap_error());
        }

    The image must be single-sample: with an MSAA pass, read back the *resolve*
    image, not the MSAA attachment.

    Output is always tightly-packed RGBA8 in top-down row order (first row is
    the top of the picture) regardless of backend, so it can be handed straight
    to a PNG encoder. GL render targets are stored bottom-up and Metal
    swapchain-format targets are BGRA8; both are normalised here.

    CALL ORDER
    ==========
    Call after sg_commit(). On Metal the readback is a blit on sokol's own
    command queue, and Metal only guarantees ordering by commit order -- reading
    before sg_commit() would race the render and capture garbage.

    BACKENDS
    ========
        SOKOL_GLCORE / SOKOL_GLES3 (incl. WebGL2)  supported
        SOKOL_METAL (macOS + iOS)                  supported
        SOKOL_D3D11                                supported (a staging copy, mapped; 8-bit RGBA/BGRA, not multisampled)
        SOKOL_WGPU                                 not implemented (scap_supported() == false)

    zlib/libpng license -- same terms as the sokol headers.
*/
#define SOKOL_CAPTURE_INCLUDED (1)

#if defined(SOKOL_IMPL) && !defined(SOKOL_CAPTURE_IMPL)
#define SOKOL_CAPTURE_IMPL
#endif

#include <stdint.h>
#include <stdbool.h>

#if !defined(SOKOL_GFX_INCLUDED)
#error "Please include sokol_gfx.h before sokol_capture.h"
#endif

#if defined(SOKOL_API_DECL) && !defined(SOKOL_CAPTURE_API_DECL)
#define SOKOL_CAPTURE_API_DECL SOKOL_API_DECL
#endif
#ifndef SOKOL_CAPTURE_API_DECL
#if defined(_WIN32) && defined(SOKOL_DLL) && defined(SOKOL_CAPTURE_IMPL)
#define SOKOL_CAPTURE_API_DECL __declspec(dllexport)
#elif defined(_WIN32) && defined(SOKOL_DLL)
#define SOKOL_CAPTURE_API_DECL __declspec(dllimport)
#else
#define SOKOL_CAPTURE_API_DECL extern
#endif
#endif

#ifdef __cplusplus
extern "C" {
#endif

/* true if the active sokol-gfx backend implements pixel readback */
SOKOL_CAPTURE_API_DECL bool scap_supported(void);

/* Read a single-sample colour render-target image back into out_rgba as
   tightly-packed, top-down RGBA8 (width*height*4 bytes needed).
   Must be called after sg_commit(). Returns false on failure; see scap_error(). */
SOKOL_CAPTURE_API_DECL bool scap_read_image(sg_image img, int width, int height, uint8_t* out_rgba, int out_size);

/* Reason the last scap_read_image() failed (never null, "" if none yet). */
SOKOL_CAPTURE_API_DECL const char* scap_error(void);

#ifdef __cplusplus
} /* extern "C" */
#endif
#endif /* SOKOL_CAPTURE_INCLUDED */

/*=== IMPLEMENTATION =========================================================*/
#ifdef SOKOL_CAPTURE_IMPL
#define SOKOL_CAPTURE_IMPL_INCLUDED (1)

#include <string.h> /* memcpy */

static char _scap_error[256] = {0};

static bool _scap_fail(const char* msg) {
    strncpy(_scap_error, msg, sizeof(_scap_error) - 1);
    _scap_error[sizeof(_scap_error) - 1] = 0;
    return false;
}

/* swap row y with row (height-1-y); no allocation */
static void _scap_flip_y(uint8_t* p, int width, int height) {
    const size_t row = (size_t)width * 4;
    uint8_t tmp[1024];
    for (int y = 0; y < height / 2; y++) {
        uint8_t* a = p + (size_t)y * row;
        uint8_t* b = p + (size_t)(height - 1 - y) * row;
        size_t left = row;
        while (left > 0) {
            const size_t n = (left < sizeof(tmp)) ? left : sizeof(tmp);
            memcpy(tmp, a, n);
            memcpy(a, b, n);
            memcpy(b, tmp, n);
            a += n; b += n; left -= n;
        }
    }
}

static void _scap_bgra_to_rgba(uint8_t* p, int width, int height) {
    const size_t n = (size_t)width * (size_t)height;
    for (size_t i = 0; i < n; i++) {
        const uint8_t b = p[0];
        p[0] = p[2];
        p[2] = b;
        p += 4;
    }
}

/*--- GL / GLES3 / WebGL2 ---------------------------------------------------*/
#if defined(SOKOL_GLCORE) || defined(SOKOL_GLES3)

static bool _scap_read_image(sg_image img, int width, int height, uint8_t* out_rgba) {
    const sg_gl_image_info info = sg_gl_query_image_info(img);
    const GLuint tex = info.tex[info.active_slot];
    if (0 == tex) {
        return _scap_fail("sg_gl_query_image_info: no texture (invalid image?)");
    }
    GLint prev_fb = 0;
    glGetIntegerv(GL_FRAMEBUFFER_BINDING, &prev_fb);
    GLuint fb = 0;
    glGenFramebuffers(1, &fb);
    glBindFramebuffer(GL_FRAMEBUFFER, fb);
    glFramebufferTexture2D(GL_FRAMEBUFFER, GL_COLOR_ATTACHMENT0, info.tex_target, tex, 0);
    bool ok = true;
    if (GL_FRAMEBUFFER_COMPLETE != glCheckFramebufferStatus(GL_FRAMEBUFFER)) {
        ok = _scap_fail("scratch readback framebuffer incomplete");
    } else {
        glReadBuffer(GL_COLOR_ATTACHMENT0);
        glPixelStorei(GL_PACK_ALIGNMENT, 4);
        glReadPixels(0, 0, width, height, GL_RGBA, GL_UNSIGNED_BYTE, out_rgba);
        /* GL render targets are stored bottom-up */
        _scap_flip_y(out_rgba, width, height);
    }
    glBindFramebuffer(GL_FRAMEBUFFER, (GLuint)prev_fb);
    glDeleteFramebuffers(1, &fb);
    return ok;
}

#define _SCAP_SUPPORTED (1)

/*--- Metal (macOS + iOS) --------------------------------------------------*/
#elif defined(SOKOL_METAL)

#if __has_feature(objc_arc)
#define _SCAP_RELEASE(obj)
#else
#define _SCAP_RELEASE(obj) [obj release]
#endif

static bool _scap_read_image(sg_image img, int width, int height, uint8_t* out_rgba) {
    const sg_mtl_image_info info = sg_mtl_query_image_info(img);
    id<MTLTexture> tex = (__bridge id<MTLTexture>) info.tex[info.active_slot];
    if (nil == tex) {
        return _scap_fail("sg_mtl_query_image_info: no texture (invalid image?)");
    }
    id<MTLDevice> dev = (__bridge id<MTLDevice>) sg_mtl_device();
    id<MTLCommandQueue> queue = (__bridge id<MTLCommandQueue>) sg_mtl_command_queue();
    if ((nil == dev) || (nil == queue)) {
        return _scap_fail("no Metal device/queue (sg_setup() not called?)");
    }
    const MTLPixelFormat fmt = tex.pixelFormat;
    const bool is_bgra = (MTLPixelFormatBGRA8Unorm == fmt) || (MTLPixelFormatBGRA8Unorm_sRGB == fmt);
    const bool is_rgba = (MTLPixelFormatRGBA8Unorm == fmt) || (MTLPixelFormatRGBA8Unorm_sRGB == fmt);
    if (!is_bgra && !is_rgba) {
        return _scap_fail("image is not an 8-bit RGBA/BGRA format");
    }
    bool ok = true;
    @autoreleasepool {
        const NSUInteger row_bytes = (NSUInteger)width * 4;
        const NSUInteger total = row_bytes * (NSUInteger)height;
        id<MTLBuffer> staging = [dev newBufferWithLength:total options:MTLResourceStorageModeShared];
        if (nil == staging) {
            ok = _scap_fail("newBufferWithLength failed");
        } else {
            id<MTLCommandBuffer> cmd_buf = [queue commandBuffer];
            id<MTLBlitCommandEncoder> blit = [cmd_buf blitCommandEncoder];
            [blit copyFromTexture:tex
                     sourceSlice:0
                     sourceLevel:0
                    sourceOrigin:MTLOriginMake(0, 0, 0)
                      sourceSize:MTLSizeMake((NSUInteger)width, (NSUInteger)height, 1)
                        toBuffer:staging
               destinationOffset:0
          destinationBytesPerRow:row_bytes
        destinationBytesPerImage:total];
            [blit endEncoding];
            [cmd_buf commit];
            [cmd_buf waitUntilCompleted];
            if (nil != cmd_buf.error) {
                ok = _scap_fail("readback blit failed");
            } else {
                memcpy(out_rgba, staging.contents, total);
                if (is_bgra) {
                    _scap_bgra_to_rgba(out_rgba, width, height);
                }
            }
            _SCAP_RELEASE(staging);
        }
    }
    return ok;
}

#define _SCAP_SUPPORTED (1)

/*--- D3D11 -------------------------------------------------------------------*/
#elif defined(SOKOL_D3D11)

/* COM calls from C or C++ (as sokol_gfx.h's own D3D11 backend makes them) */
#if defined(__cplusplus)
#define _SCAP_D3D11_CALL0(obj, fn) (obj)->fn()
#define _SCAP_D3D11_CALL(obj, fn, ...) (obj)->fn(__VA_ARGS__)
#else
#define _SCAP_D3D11_CALL0(obj, fn) (obj)->lpVtbl->fn(obj)
#define _SCAP_D3D11_CALL(obj, fn, ...) (obj)->lpVtbl->fn(obj, __VA_ARGS__)
#endif

static bool _scap_read_image(sg_image img, int width, int height, uint8_t* out_rgba) {
    const sg_d3d11_image_info info = sg_d3d11_query_image_info(img);
    ID3D11Texture2D* tex = (ID3D11Texture2D*) info.tex2d;
    ID3D11Device* dev = (ID3D11Device*) sg_d3d11_device();
    ID3D11DeviceContext* ctx = (ID3D11DeviceContext*) sg_d3d11_device_context();
    if (0 == tex) {
        return _scap_fail("sg_d3d11_query_image_info: no 2D texture (invalid image?)");
    }
    if ((0 == dev) || (0 == ctx)) {
        return _scap_fail("no D3D11 device/context (sg_setup() not called?)");
    }
    D3D11_TEXTURE2D_DESC desc;
    _SCAP_D3D11_CALL(tex, GetDesc, &desc);
    const bool is_bgra = (DXGI_FORMAT_B8G8R8A8_UNORM == desc.Format) || (DXGI_FORMAT_B8G8R8A8_UNORM_SRGB == desc.Format);
    const bool is_rgba = (DXGI_FORMAT_R8G8B8A8_UNORM == desc.Format) || (DXGI_FORMAT_R8G8B8A8_UNORM_SRGB == desc.Format);
    if (!is_bgra && !is_rgba) {
        return _scap_fail("image is not an 8-bit RGBA/BGRA format");
    }
    if (desc.SampleDesc.Count > 1) {
        return _scap_fail("multisampled image (resolve it first)");
    }
    if (((UINT)width > desc.Width) || ((UINT)height > desc.Height)) {
        return _scap_fail("width/height larger than the image");
    }
    /* a CPU-readable copy of mip 0, slice 0 */
    D3D11_TEXTURE2D_DESC sdesc;
    memset(&sdesc, 0, sizeof(sdesc));
    sdesc.Width = (UINT)width;
    sdesc.Height = (UINT)height;
    sdesc.MipLevels = 1;
    sdesc.ArraySize = 1;
    sdesc.Format = desc.Format;
    sdesc.SampleDesc.Count = 1;
    sdesc.Usage = D3D11_USAGE_STAGING;
    sdesc.CPUAccessFlags = D3D11_CPU_ACCESS_READ;
    ID3D11Texture2D* staging = 0;
    if (FAILED(_SCAP_D3D11_CALL(dev, CreateTexture2D, &sdesc, NULL, &staging)) || (0 == staging)) {
        return _scap_fail("CreateTexture2D (staging) failed");
    }
    D3D11_BOX box;
    box.left = 0; box.top = 0; box.front = 0;
    box.right = (UINT)width; box.bottom = (UINT)height; box.back = 1;
    _SCAP_D3D11_CALL(ctx, CopySubresourceRegion, (ID3D11Resource*)staging, 0, 0, 0, 0, (ID3D11Resource*)tex, 0, &box);
    bool ok = true;
    D3D11_MAPPED_SUBRESOURCE mapped;
    if (FAILED(_SCAP_D3D11_CALL(ctx, Map, (ID3D11Resource*)staging, 0, D3D11_MAP_READ, 0, &mapped))) {
        ok = _scap_fail("Map (staging) failed");
    } else {
        /* D3D11 rows are top-down, as the result's: copied row by row (the pitch may be wider) */
        const size_t row = (size_t)width * 4;
        for (int y = 0; y < height; y++) {
            memcpy(out_rgba + (size_t)y * row, (const uint8_t*)mapped.pData + (size_t)y * mapped.RowPitch, row);
        }
        _SCAP_D3D11_CALL(ctx, Unmap, (ID3D11Resource*)staging, 0);
        if (is_bgra) {
            _scap_bgra_to_rgba(out_rgba, width, height);
        }
    }
    _SCAP_D3D11_CALL0(staging, Release);
    return ok;
}

#define _SCAP_SUPPORTED (1)

/*--- unimplemented backends ------------------------------------------------*/
#else

static bool _scap_read_image(sg_image img, int width, int height, uint8_t* out_rgba) {
    (void)img; (void)width; (void)height; (void)out_rgba;
    return _scap_fail("pixel readback is not implemented for this sokol-gfx backend");
}

#define _SCAP_SUPPORTED (0)

#endif

SOKOL_API_IMPL bool scap_supported(void) {
    return _SCAP_SUPPORTED ? true : false;
}

SOKOL_API_IMPL const char* scap_error(void) {
    return _scap_error;
}

SOKOL_API_IMPL bool scap_read_image(sg_image img, int width, int height, uint8_t* out_rgba, int out_size) {
    if ((width <= 0) || (height <= 0)) {
        return _scap_fail("width/height must be > 0");
    }
    if (NULL == out_rgba) {
        return _scap_fail("out_rgba is null");
    }
    if (out_size < (width * height * 4)) {
        return _scap_fail("out_size too small (need width*height*4)");
    }
    _scap_error[0] = 0;
    return _scap_read_image(img, width, height, out_rgba);
}

#endif /* SOKOL_CAPTURE_IMPL */
