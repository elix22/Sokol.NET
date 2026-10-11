using System;
using System.Collections.Generic;
using Sokol.GUI;
using static Sokol.SG;
using static Sokol.SGlue;
using static scene_shader_cs_r2d.Shaders;
using static blit_shader_cs_r2d.Shaders;
using static particle_shader_cs_r2d.Shaders;
using BloomEx   = bloom_extract_shader_cs_r2d.Shaders;   // both bloom shaders share bloom_src/smp names →
using BloomBlur = bloom_blur_shader_cs_r2d.Shaders;      // alias rather than `using static` (would clash)

namespace Sokol.Render2D;

/// <summary>
/// The shared GPU core of the Sokol.SG 2D path (<c>docs/RENDER2D.md §6</c>). Owns the offscreen
/// render target, a batched-triangle scene pipeline, a GPU-instanced particle pipeline, the fullscreen
/// blit, an ortho-by-viewport projection, and CPU accumulators. Everything is in <b>screen-pixel
/// space</b> (origin top-left, matching <see cref="Sokol.GUI.Renderer"/>) so the SG path matches the
/// NanoVG look 1:1.
/// <para>
/// Usage (Strategy C): <see cref="Begin"/> → push scene primitives + particle instances → <see cref="End"/>
/// renders them into the offscreen target (one pass: scene under particles); then, inside the swapchain
/// pass, <see cref="Blit"/> composites the target before the NanoVG HUD draws. Buffers grow on demand
/// (no silent geometry drop; growth is rare, so steady state allocates nothing).
/// </para>
/// </summary>
public sealed unsafe class Render2DSurface : IDisposable
{
    // Scene vertex: pixel position + straight RGBA (explicit floats → 24-byte GPU layout).
    struct Vtx { public float X, Y, R, G, B, A; }

    // Per-particle instance (56 bytes): pos, half-size, rot, rgba, uvRect, mode.
    struct Inst { public float PX, PY, SX, SY, Rot, R, G, B, A, U0, V0, U1, V1, Mode; }

    struct PartCmd { public BlendMode Blend; public sg_view Tex; public int Start, Count; }

    struct ViewportUB { public float W, H, Pad0, Pad1; }   // vec4 viewport (.xy = framebuffer px)
    struct Vec4UB     { public float X, Y, Z, W; }          // generic vec4 uniform (bloom params)

    const int InitVerts = 8 * 1024;
    const int InitInsts = 4 * 1024;

    // offscreen target — when Samples>1 the scene renders into an MSAA image that resolves down to the
    // single-sample colour image the blit + bloom sample; when Samples==1 it renders straight into it.
    /// <summary>Scene MSAA sample count (1 = off — for low-end Android / GLES that can't resolve, or to
    /// save fill). Must be set before the first frame (the pipelines bake it in).</summary>
    public int Samples { get; set; } = 4;
    bool Msaa => Samples > 1;
    sg_image       _colorImg;          // single-sample target (also the MSAA resolve target when Samples>1)
    sg_image       _msaaImg;           // MSAA scene render target
    sg_view        _colorAtt;          // colour-attachment view of _colorImg (bloom composite LOAD pass)
    sg_view        _msaaAtt;           // colour-attachment view of _msaaImg (scene render)
    sg_view        _resolveView;       // resolve-attachment view of _colorImg
    sg_view        _texView;           // texture view of _colorImg
    sg_attachments _sceneAttachments;  // scene pass: MSAA colour + resolve → _colorImg
    sg_attachments _colorAttachments;  // composite pass: _colorImg colour (single-sample)
    int _w, _h;

    // scene pipeline
    sg_shader   _sceneShader;
    sg_pipeline _scenePip;
    sg_buffer   _vbuf;
    int         _vbufCap;
    Vtx[]       _verts = new Vtx[InitVerts];
    int         _count;

    // particle pipeline
    sg_shader   _partShader;
    sg_pipeline _partPipAdd, _partPipNormal;
    sg_buffer   _quadVbuf;          // static unit quad [-1,1] (6 verts)
    sg_buffer   _instBuf;
    int         _instBufCap;
    Inst[]      _insts = new Inst[InitInsts];
    int         _instCount;
    sg_sampler  _partSampler;
    sg_image    _whiteImg;
    sg_view     _whiteView;
    readonly List<PartCmd> _cmds = new();
    int _curStart; BlendMode _curBlend; sg_view _curTex;

    // blit pipeline (→ swapchain)
    sg_shader   _blitShader;
    sg_pipeline _blitPip;
    sg_sampler  _blitSampler;

    // bloom (optional glow post — full-res, gl_FragCoord-safe, flag-gated)
    /// <summary>Enable the bloom glow post-pass (extract bright → separable blur → additive composite).</summary>
    public bool  Bloom { get; set; }
    public float BloomThreshold  { get; set; } = 0.35f;  // lower = more of the (additive) scene blooms
    public float BloomIntensity  { get; set; } = 1.4f;
    public float BloomSpread     { get; set; } = 6f;     // per-tap UV step scale (blur width)
    public int   BloomIterations { get; set; } = 3;      // wider, smoother glow
    sg_shader   _bloomExtractShader, _bloomBlurShader;
    sg_pipeline _bloomExtractPip, _bloomBlurPip, _bloomCompositePip;
    sg_image    _bloomImgA, _bloomImgB;
    sg_view     _bloomAttA, _bloomAttB, _bloomTexA, _bloomTexB;
    sg_attachments _bloomAttachA, _bloomAttachB;

    UIColor _clear;
    bool    _inited;

    /// <summary>Number of currently Init'd surfaces (each holds a set of sokol shaders/pipelines). Used by
    /// the test harness to assert screen teardown frees its surface — this must stay bounded, not grow with
    /// the number of game screens opened (an unfreed surface leaks toward SHADER_POOL_EXHAUSTED).</summary>
    public static int LiveCount { get; private set; }

    /// <summary>Monotonic count of every surface ever Init'd — lets a leak test confirm surfaces were
    /// actually created/churned (guards against a vacuous "count stayed low because nothing rendered").</summary>
    public static int TotalCreated { get; private set; }

    public bool IsValid => _colorImg.id != 0;
    /// <summary>A 1×1 white texture view — bind for non-textured (dynamic) particle batches.</summary>
    public sg_view WhiteView => _whiteView;

    /// <summary>A TEXTURE view of this surface's rendered colour target, for binding it as the source of a
    /// later draw — i.e. using one surface as an offscreen render-to-texture that another samples.
    /// <para>The intended use is a bake: <see cref="Begin"/> → draw → <see cref="End"/> on a dedicated
    /// surface, then pass this to <see cref="BeginParticleBatch"/> so the result is blitted as textured
    /// quads (glyph/sprite atlases built from the same vector code that would otherwise redraw the shapes
    /// every frame). ⛔ sokol_gfx forbids NESTED passes, so the bake must run BEFORE the consuming
    /// surface's <see cref="Begin"/>, never between its Begin and End.</para>
    /// <para>Zero until the surface has rendered at least once (the target is created on first
    /// <see cref="Begin"/>); check <see cref="IsValid"/>.</para></summary>
    public sg_view TextureView => _texView;

    public void Init()
    {
        if (_inited) return;
        _inited = true;
        LiveCount++; TotalCreated++;

        // ── scene pipeline ────────────────────────────────────────────────────────────────────────
        _sceneShader = sg_make_shader(r2d_scene_shader_desc(sg_query_backend()));
        var scenePip = new sg_pipeline_desc
        {
            shader         = _sceneShader,
            primitive_type = sg_primitive_type.SG_PRIMITIVETYPE_TRIANGLES,
            colors = { [0] = new sg_color_target_state { pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA8, blend = AlphaBlend() } },
            depth        = new sg_depth_state { pixel_format = sg_pixel_format.SG_PIXELFORMAT_NONE },
            sample_count = Samples,                      // renders into the (MSAA) scene target
            label        = "r2d-scene-pipeline",
        };
        scenePip.layout.attrs[ATTR_r2d_scene_pos]    = new sg_vertex_attr_state { format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT2, offset = 0 };
        scenePip.layout.attrs[ATTR_r2d_scene_color0] = new sg_vertex_attr_state { format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4, offset = 8 };
        _scenePip = sg_make_pipeline(scenePip);
        _vbufCap = InitVerts;
        _vbuf = MakeVtxBuffer(_vbufCap);

        // ── particle pipeline (instanced) ──────────────────────────────────────────────────────────
        _partShader = sg_make_shader(r2d_particle_shader_desc(sg_query_backend()));
        _partPipAdd    = MakeParticlePipeline(AdditiveBlend(), "r2d-particle-add");
        _partPipNormal = MakeParticlePipeline(AlphaBlend(),    "r2d-particle-normal");

        float[] quad = { -1f,-1f,  1f,-1f,  1f,1f,   -1f,-1f,  1f,1f,  -1f,1f };
        fixed (float* q = quad)
            _quadVbuf = sg_make_buffer(new sg_buffer_desc { data = new sg_range { ptr = q, size = (nuint)(quad.Length * sizeof(float)) }, label = "r2d-particle-quad" });

        _instBufCap = InitInsts;
        _instBuf = MakeInstBuffer(_instBufCap);

        _partSampler = sg_make_sampler(new sg_sampler_desc
        {
            min_filter = sg_filter.SG_FILTER_LINEAR, mag_filter = sg_filter.SG_FILTER_LINEAR,
            wrap_u = sg_wrap.SG_WRAP_CLAMP_TO_EDGE, wrap_v = sg_wrap.SG_WRAP_CLAMP_TO_EDGE,
            label = "r2d-particle-sampler",
        });
        uint white = 0xFFFFFFFFu;
        _whiteImg = sg_make_image(new sg_image_desc
        {
            width = 1, height = 1, pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA8,
            data = { mip_levels = { [0] = new sg_range { ptr = &white, size = 4 } } }, label = "r2d-white",
        });
        _whiteView = sg_make_view(new sg_view_desc { texture = { image = _whiteImg }, label = "r2d-white-view" });

        // ── blit pipeline (→ swapchain) ─────────────────────────────────────────────────────────────
        _blitShader = sg_make_shader(r2d_blit_shader_desc(sg_query_backend()));
        var sc = sglue_swapchain();
        _blitPip = sg_make_pipeline(new sg_pipeline_desc
        {
            shader         = _blitShader,
            primitive_type = sg_primitive_type.SG_PRIMITIVETYPE_TRIANGLES,
            colors = { [0] = new sg_color_target_state { pixel_format = sc.color_format } },
            depth  = new sg_depth_state { pixel_format = sc.depth_format, compare = sg_compare_func.SG_COMPAREFUNC_ALWAYS, write_enabled = false },
            sample_count = sc.sample_count,
            label = "r2d-blit-pipeline",
        });
        _blitSampler = sg_make_sampler(new sg_sampler_desc
        {
            min_filter = sg_filter.SG_FILTER_LINEAR, mag_filter = sg_filter.SG_FILTER_LINEAR,
            wrap_u = sg_wrap.SG_WRAP_CLAMP_TO_EDGE, wrap_v = sg_wrap.SG_WRAP_CLAMP_TO_EDGE,
            label = "r2d-blit-sampler",
        });

        // ── bloom pipelines (extract + separable blur = no blend; composite = blit shader, additive) ─
        _bloomExtractShader = sg_make_shader(BloomEx.r2d_bloom_extract_shader_desc(sg_query_backend()));
        _bloomBlurShader    = sg_make_shader(BloomBlur.r2d_bloom_blur_shader_desc(sg_query_backend()));
        _bloomExtractPip    = MakeFullscreenPip(_bloomExtractShader, default,         "r2d-bloom-extract");
        _bloomBlurPip       = MakeFullscreenPip(_bloomBlurShader,    default,         "r2d-bloom-blur");
        _bloomCompositePip  = MakeFullscreenPip(_blitShader,         AdditiveBlend(), "r2d-bloom-composite");
    }

    // Fullscreen post pipeline (no vertex layout — the VS generates a triangle from gl_VertexIndex).
    sg_pipeline MakeFullscreenPip(sg_shader shader, sg_blend_state blend, string label) =>
        sg_make_pipeline(new sg_pipeline_desc
        {
            shader         = shader,
            primitive_type = sg_primitive_type.SG_PRIMITIVETYPE_TRIANGLES,
            colors = { [0] = new sg_color_target_state { pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA8, blend = blend } },
            depth        = new sg_depth_state { pixel_format = sg_pixel_format.SG_PIXELFORMAT_NONE },
            sample_count = 1,
            label        = label,
        });

    static sg_blend_state AlphaBlend() => new()
    {
        enabled = true,
        src_factor_rgb = sg_blend_factor.SG_BLENDFACTOR_SRC_ALPHA, dst_factor_rgb = sg_blend_factor.SG_BLENDFACTOR_ONE_MINUS_SRC_ALPHA,
        src_factor_alpha = sg_blend_factor.SG_BLENDFACTOR_ONE,     dst_factor_alpha = sg_blend_factor.SG_BLENDFACTOR_ONE_MINUS_SRC_ALPHA,
    };
    static sg_blend_state AdditiveBlend() => new()
    {
        enabled = true,
        src_factor_rgb = sg_blend_factor.SG_BLENDFACTOR_SRC_ALPHA, dst_factor_rgb = sg_blend_factor.SG_BLENDFACTOR_ONE,
        src_factor_alpha = sg_blend_factor.SG_BLENDFACTOR_ONE,     dst_factor_alpha = sg_blend_factor.SG_BLENDFACTOR_ONE,
    };

    sg_pipeline MakeParticlePipeline(sg_blend_state blend, string label)
    {
        var p = new sg_pipeline_desc
        {
            shader         = _partShader,
            primitive_type = sg_primitive_type.SG_PRIMITIVETYPE_TRIANGLES,
            colors = { [0] = new sg_color_target_state { pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA8, blend = blend } },
            depth        = new sg_depth_state { pixel_format = sg_pixel_format.SG_PIXELFORMAT_NONE },
            sample_count = Samples,                      // particles render into the (MSAA) scene target too
            label        = label,
        };
        p.layout.buffers[1].step_func = sg_vertex_step.SG_VERTEXSTEP_PER_INSTANCE;
        p.layout.attrs[ATTR_r2d_particle_corner]  = new sg_vertex_attr_state { format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT2, buffer_index = 0, offset = 0 };
        p.layout.attrs[ATTR_r2d_particle_i_pos]   = new sg_vertex_attr_state { format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT2, buffer_index = 1, offset = 0 };
        p.layout.attrs[ATTR_r2d_particle_i_size]  = new sg_vertex_attr_state { format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT2, buffer_index = 1, offset = 8 };
        p.layout.attrs[ATTR_r2d_particle_i_rot]   = new sg_vertex_attr_state { format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT,  buffer_index = 1, offset = 16 };
        p.layout.attrs[ATTR_r2d_particle_i_color] = new sg_vertex_attr_state { format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4, buffer_index = 1, offset = 20 };
        p.layout.attrs[ATTR_r2d_particle_i_uvrect]= new sg_vertex_attr_state { format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4, buffer_index = 1, offset = 36 };
        p.layout.attrs[ATTR_r2d_particle_i_mode]  = new sg_vertex_attr_state { format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT,  buffer_index = 1, offset = 52 };
        return sg_make_pipeline(p);
    }

    sg_buffer MakeVtxBuffer(int verts) => sg_make_buffer(new sg_buffer_desc
    { size = (nuint)(verts * sizeof(Vtx)), usage = new sg_buffer_usage { stream_update = true }, label = "r2d-scene-verts" });

    sg_buffer MakeInstBuffer(int insts) => sg_make_buffer(new sg_buffer_desc
    { size = (nuint)(insts * sizeof(Inst)), usage = new sg_buffer_usage { stream_update = true }, label = "r2d-particle-insts" });

    void EnsureTarget(int w, int h)
    {
        if (_colorImg.id != 0 && _w == w && _h == h) return;
        DestroyTarget();
        _w = w; _h = h;
        _colorImg = sg_make_image(new sg_image_desc
        { width = w, height = h, pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA8, sample_count = 1, usage = { color_attachment = true, resolve_attachment = Msaa }, label = "r2d-offscreen-color" });
        _colorAtt = sg_make_view(new sg_view_desc { color_attachment = { image = _colorImg }, label = "r2d-color-att" });
        _texView  = sg_make_view(new sg_view_desc { texture          = { image = _colorImg }, label = "r2d-color-tex" });
        _colorAttachments = default;
        _colorAttachments.colors[0] = _colorAtt;
        _sceneAttachments = default;
        if (Msaa)   // scene renders into the MSAA image, resolving down to _colorImg
        {
            _msaaImg = sg_make_image(new sg_image_desc
            { width = w, height = h, pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA8, sample_count = Samples, usage = { color_attachment = true }, label = "r2d-offscreen-msaa" });
            _msaaAtt     = sg_make_view(new sg_view_desc { color_attachment   = { image = _msaaImg },  label = "r2d-msaa-att" });
            _resolveView = sg_make_view(new sg_view_desc { resolve_attachment = { image = _colorImg }, label = "r2d-resolve" });
            _sceneAttachments.colors[0]   = _msaaAtt;
            _sceneAttachments.resolves[0] = _resolveView;
        }
        else        // single-sample: scene renders straight into _colorImg
        {
            _sceneAttachments.colors[0] = _colorAtt;
        }

        // bloom ping-pong targets (full-res, single-sample)
        _bloomImgA = sg_make_image(new sg_image_desc { width = w, height = h, pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA8, sample_count = 1, usage = { color_attachment = true }, label = "r2d-bloom-a" });
        _bloomImgB = sg_make_image(new sg_image_desc { width = w, height = h, pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA8, sample_count = 1, usage = { color_attachment = true }, label = "r2d-bloom-b" });
        _bloomAttA = sg_make_view(new sg_view_desc { color_attachment = { image = _bloomImgA } });
        _bloomAttB = sg_make_view(new sg_view_desc { color_attachment = { image = _bloomImgB } });
        _bloomTexA = sg_make_view(new sg_view_desc { texture = { image = _bloomImgA } });
        _bloomTexB = sg_make_view(new sg_view_desc { texture = { image = _bloomImgB } });
        _bloomAttachA = default; _bloomAttachA.colors[0] = _bloomAttA;
        _bloomAttachB = default; _bloomAttachB.colors[0] = _bloomAttB;
    }

    void DestroyTarget()
    {
        if (_texView.id     != 0) sg_destroy_view(_texView);
        if (_colorAtt.id    != 0) sg_destroy_view(_colorAtt);
        if (_msaaAtt.id     != 0) sg_destroy_view(_msaaAtt);
        if (_resolveView.id != 0) sg_destroy_view(_resolveView);
        if (_colorImg.id    != 0) sg_destroy_image(_colorImg);
        if (_msaaImg.id     != 0) sg_destroy_image(_msaaImg);
        _texView = _colorAtt = _msaaAtt = _resolveView = default; _colorImg = _msaaImg = default;

        if (_bloomTexA.id != 0) sg_destroy_view(_bloomTexA);
        if (_bloomTexB.id != 0) sg_destroy_view(_bloomTexB);
        if (_bloomAttA.id != 0) sg_destroy_view(_bloomAttA);
        if (_bloomAttB.id != 0) sg_destroy_view(_bloomAttB);
        if (_bloomImgA.id != 0) sg_destroy_image(_bloomImgA);
        if (_bloomImgB.id != 0) sg_destroy_image(_bloomImgB);
        _bloomTexA = _bloomTexB = _bloomAttA = _bloomAttB = default; _bloomImgA = _bloomImgB = default;
    }

    // ── frame ───────────────────────────────────────────────────────────────────────────────────
    public void Begin(int pxW, int pxH, UIColor clear)
    {
        Init();
        EnsureTarget(pxW, pxH);
        _count = 0; _instCount = 0; _cmds.Clear();
        _clear = clear;
        _tScale = 1f; _tOrigin = default;   // content transform never leaks across frames
    }

    // ── content transform ───────────────────────────────────────────────────────────────────────
    float _tScale = 1f;
    Vector2 _tOrigin;

    /// <summary>Uniform scale + translation applied to every primitive pushed after this call
    /// (shapes and particles alike). Lets a widget lay out its content in local coordinates while
    /// the surface spans the full framebuffer: draw the full-bleed background first, then set the
    /// transform and draw the content. <see cref="Begin"/> resets it to identity.</summary>
    public void SetContentTransform(float scale, Vector2 origin) { _tScale = scale; _tOrigin = origin; }

    public void ResetContentTransform() { _tScale = 1f; _tOrigin = default; }

    /// <summary>Flush scene triangles then particle instances into the offscreen target (one pass).</summary>
    public void End()
    {
        if (!IsValid) return;
        var action = default(sg_pass_action);
        action.colors[0].load_action  = sg_load_action.SG_LOADACTION_CLEAR;
        // ⛔ The clear's ALPHA is honoured, not forced to 1. A surface used as a render-to-texture source
        // (see TextureView) has to be able to clear TRANSPARENT — baking a glyph atlas onto a forced-opaque
        // clear produces a solid box behind every glyph, which is exactly what it looked like. Every
        // on-screen consumer passes an opaque colour, so this changes nothing for them.
        action.colors[0].clear_value  = new sg_color { r = _clear.R, g = _clear.G, b = _clear.B, a = _clear.A };
        if (Msaa) action.colors[0].store_action = sg_store_action.SG_STOREACTION_DONTCARE;   // MSAA resolved, not stored

        var vp = new ViewportUB { W = _w, H = _h };

        sg_begin_pass(new sg_pass { action = action, attachments = _sceneAttachments, label = "r2d-offscreen-pass" });

        // scene triangles
        if (_count > 0)
        {
            if (_count > _vbufCap) { sg_destroy_buffer(_vbuf); _vbufCap = _verts.Length; _vbuf = MakeVtxBuffer(_vbufCap); }
            fixed (Vtx* p = _verts) sg_update_buffer(_vbuf, new sg_range { ptr = p, size = (nuint)(_count * sizeof(Vtx)) });
            var bind = default(sg_bindings);
            bind.vertex_buffers[0] = _vbuf;
            sg_apply_pipeline(_scenePip);
            sg_apply_bindings(bind);
            sg_apply_uniforms(UB_r2d_scene_params, new sg_range { ptr = &vp, size = (nuint)sizeof(ViewportUB) });
            sg_draw(0, (uint)_count, 1);
        }

        // particle instances (batched by blend + texture)
        if (_instCount > 0 && _cmds.Count > 0)
        {
            if (_instCount > _instBufCap) { sg_destroy_buffer(_instBuf); _instBufCap = _insts.Length; _instBuf = MakeInstBuffer(_instBufCap); }
            fixed (Inst* p = _insts) sg_update_buffer(_instBuf, new sg_range { ptr = p, size = (nuint)(_instCount * sizeof(Inst)) });
            for (int i = 0; i < _cmds.Count; i++)
            {
                var cmd = _cmds[i];
                var bind = default(sg_bindings);
                bind.vertex_buffers[0] = _quadVbuf;
                bind.vertex_buffers[1] = _instBuf;
                bind.vertex_buffer_offsets[1] = cmd.Start * sizeof(Inst);
                bind.views[VIEW_r2d_part_tex]   = cmd.Tex.id != 0 ? cmd.Tex : _whiteView;
                bind.samplers[SMP_r2d_part_smp] = _partSampler;
                sg_apply_pipeline(cmd.Blend == BlendMode.Additive ? _partPipAdd : _partPipNormal);
                sg_apply_bindings(bind);
                sg_apply_uniforms(UB_r2d_particle_params, new sg_range { ptr = &vp, size = (nuint)sizeof(ViewportUB) });
                sg_draw(0, 6, (uint)cmd.Count);
            }
        }

        sg_end_pass();

        if (Bloom) RunBloom();
    }

    // Bright-pass → separable blur (ping-pong) → additive composite back onto the scene target.
    void RunBloom()
    {
        var clear = default(sg_pass_action);
        clear.colors[0].load_action = sg_load_action.SG_LOADACTION_CLEAR;
        clear.colors[0].clear_value = new sg_color { r = 0f, g = 0f, b = 0f, a = 1f };

        sg_begin_pass(new sg_pass { action = clear, attachments = _bloomAttachA, label = "r2d-bloom-extract" });
        DrawFullscreen(_bloomExtractPip, _texView, BloomEx.VIEW_r2d_bloom_src, BloomEx.SMP_r2d_bloom_smp,
                       BloomEx.UB_r2d_bloom_extract_params, new Vec4UB { X = BloomThreshold, Y = BloomIntensity });
        sg_end_pass();

        for (int it = 0; it < BloomIterations; it++)
        {
            sg_begin_pass(new sg_pass { action = clear, attachments = _bloomAttachB, label = "r2d-bloom-blur-h" });
            DrawFullscreen(_bloomBlurPip, _bloomTexA, BloomBlur.VIEW_r2d_bloom_src, BloomBlur.SMP_r2d_bloom_smp,
                           BloomBlur.UB_r2d_bloom_blur_params, new Vec4UB { X = BloomSpread / _w, Y = 0f });
            sg_end_pass();

            sg_begin_pass(new sg_pass { action = clear, attachments = _bloomAttachA, label = "r2d-bloom-blur-v" });
            DrawFullscreen(_bloomBlurPip, _bloomTexB, BloomBlur.VIEW_r2d_bloom_src, BloomBlur.SMP_r2d_bloom_smp,
                           BloomBlur.UB_r2d_bloom_blur_params, new Vec4UB { X = 0f, Y = BloomSpread / _h });
            sg_end_pass();
        }

        // additive composite into the scene target (LOAD keeps the scene); blit shader samples bloomA.
        var load = default(sg_pass_action);
        load.colors[0].load_action = sg_load_action.SG_LOADACTION_LOAD;
        sg_begin_pass(new sg_pass { action = load, attachments = _colorAttachments, label = "r2d-bloom-composite" });
        DrawFullscreen(_bloomCompositePip, _bloomTexA, VIEW_r2d_src_tex, SMP_r2d_src_smp, -1, default);
        sg_end_pass();
    }

    void DrawFullscreen(sg_pipeline pip, sg_view src, int viewSlot, int smpSlot, int ubSlot, Vec4UB ub)
    {
        sg_apply_pipeline(pip);
        var bind = default(sg_bindings);
        bind.views[viewSlot]    = src;
        bind.samplers[smpSlot]  = _blitSampler;
        sg_apply_bindings(bind);
        if (ubSlot >= 0) sg_apply_uniforms(ubSlot, new sg_range { ptr = &ub, size = (nuint)sizeof(Vec4UB) });
        sg_draw(0, 3, 1);
    }

    /// <summary>Composite the offscreen target into the active swapchain pass (fullscreen, replace).</summary>
    public void Blit()
    {
        if (!IsValid) return;
        var bind = default(sg_bindings);
        bind.views[VIEW_r2d_src_tex]   = _texView;
        bind.samplers[SMP_r2d_src_smp] = _blitSampler;
        sg_apply_pipeline(_blitPip);
        sg_apply_bindings(bind);
        sg_draw(0, 3, 1);
    }

    // ── draw recording (DrawRecorder) ─────────────────────────────────────────────────────────────
    // Composite primitives (rounded rects, gradients) record ONE entry and suppress the quads/fans
    // they are built from. Rects are recorded post-content-transform, i.e. in the same surface-pixel
    // space the vertices land in. Zero cost when DrawRecorder.Recording is off: one static-bool test
    // per public primitive (plus a bool save/restore in the composites).
    bool _recSup;

    void Rec(float x0, float y0, float x1, float y1, string tag)
    {
        if (!DrawRecorder.Recording || _recSup) return;
        DrawRecorder.Add(new Rect(x0 * _tScale + _tOrigin.X, y0 * _tScale + _tOrigin.Y,
                                  (x1 - x0) * _tScale, (y1 - y0) * _tScale),
                         DrawRecorder.Kind.GpuShape, tag);
    }

    void Rec(Vector2 a, Vector2 b, Vector2 c, string tag) =>
        Rec(MathF.Min(a.X, MathF.Min(b.X, c.X)), MathF.Min(a.Y, MathF.Min(b.Y, c.Y)),
            MathF.Max(a.X, MathF.Max(b.X, c.X)), MathF.Max(a.Y, MathF.Max(b.Y, c.Y)), tag);

    void Rec(Vector2 a, Vector2 b, Vector2 c, Vector2 d, string tag) =>
        Rec(MathF.Min(MathF.Min(a.X, b.X), MathF.Min(c.X, d.X)), MathF.Min(MathF.Min(a.Y, b.Y), MathF.Min(c.Y, d.Y)),
            MathF.Max(MathF.Max(a.X, b.X), MathF.Max(c.X, d.X)), MathF.Max(MathF.Max(a.Y, b.Y), MathF.Max(c.Y, d.Y)), tag);

    // ── scene primitives (screen-pixel space; mirror Sokol.GUI.Renderer names) ────────────────────
    void Push(Vector2 p, UIColor c)
    {
        if (_count >= _verts.Length) Array.Resize(ref _verts, _verts.Length * 2);   // grow, don't drop
        _verts[_count] = new Vtx { X = p.X * _tScale + _tOrigin.X, Y = p.Y * _tScale + _tOrigin.Y,
                                   R = c.R, G = c.G, B = c.B, A = c.A };
        _count++;
    }

    public void FillTri(Vector2 a, Vector2 b, Vector2 c, UIColor col) { Rec(a, b, c, "tri"); Push(a, col); Push(b, col); Push(c, col); }

    public void FillTriVc(Vector2 a, UIColor ca, Vector2 b, UIColor cb, Vector2 c, UIColor cc) { Rec(a, b, c, "tri"); Push(a, ca); Push(b, cb); Push(c, cc); }

    public void FillQuad(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, UIColor col)
    { Rec(p0, p1, p2, p3, "quad"); Push(p0, col); Push(p1, col); Push(p2, col); Push(p0, col); Push(p2, col); Push(p3, col); }

    public void FillRect(Rect r, UIColor col)
    {
        Rec(r.X, r.Y, r.X + r.Width, r.Y + r.Height, "rect");
        bool sup = _recSup; _recSup = true;
        FillQuad(new(r.X, r.Y), new(r.X + r.Width, r.Y), new(r.X + r.Width, r.Y + r.Height), new(r.X, r.Y + r.Height), col);
        _recSup = sup;
    }

    public void FillVerticalGradient(Rect r, UIColor top, UIColor bottom)
    {
        Rec(r.X, r.Y, r.X + r.Width, r.Y + r.Height, "grad");
        Vector2 tl = new(r.X, r.Y), tr = new(r.X + r.Width, r.Y);
        Vector2 br = new(r.X + r.Width, r.Y + r.Height), bl = new(r.X, r.Y + r.Height);
        bool sup = _recSup; _recSup = true;
        FillTriVc(tl, top, tr, top, br, bottom);
        FillTriVc(tl, top, br, bottom, bl, bottom);
        _recSup = sup;
    }

    // Per-vertex-colour quad (two triangles p0-p1-p2 / p0-p2-p3) — lets the circle helpers fade a feather edge.
    void QuadVc(Vector2 p0, UIColor c0, Vector2 p1, UIColor c1, Vector2 p2, UIColor c2, Vector2 p3, UIColor c3)
    { Push(p0, c0); Push(p1, c1); Push(p2, c2); Push(p0, c0); Push(p2, c2); Push(p3, c3); }

    /// <summary>Filled circle with a ~1px transparent feather at the rim, so the edge is anti-aliased even
    /// when the offscreen target has no MSAA (Samples==1, e.g. low-end Android) — matching the NanoVG look.</summary>
    public void FillCircle(Vector2 center, float radius, UIColor col)
    {
        if (radius <= 0f) return;
        Rec(center.X - radius, center.Y - radius, center.X + radius, center.Y + radius, "circle");
        int seg = Math.Clamp((int)(radius * 0.8f) + 12, 16, 128);   // tessellation (roundness); the feather below does the edge AA
        float step = MathF.Tau / seg;
        float aa = MathF.Min(1f / _tScale, radius);   // ~1 PHYSICAL px: under a content scale (retina) 1 unit is 2-3 px of blur
        float rs = radius - aa;                       // solid-core radius; [rs, radius] is the feather
        UIColor edge = col.WithAlpha(0f);
        Vector2 dPrev = new(1f, 0f);
        for (int i = 1; i <= seg; i++)
        {
            Vector2 dCur = new(MathF.Cos(i * step), MathF.Sin(i * step));
            if (rs > 0f) { Push(center, col); Push(center + dPrev * rs, col); Push(center + dCur * rs, col); }      // opaque core
            QuadVc(center + dPrev * rs, col, center + dCur * rs, col, center + dCur * radius, edge, center + dPrev * radius, edge);   // feather
            dPrev = dCur;
        }
    }

    /// <summary>Anti-aliased ring/outline of the given <paramref name="thickness"/> centred on
    /// <paramref name="radius"/> (a ~1px transparent feather on each edge → smooth at any sample count).</summary>
    public void StrokeCircle(Vector2 center, float radius, float thickness, UIColor col)
    {
        if (radius <= 0f || thickness <= 0f) return;
        float ro0 = radius + MathF.Min(thickness * 0.5f, radius);
        Rec(center.X - ro0, center.Y - ro0, center.X + ro0, center.Y + ro0, "circle-stroke");
        float half = MathF.Min(thickness * 0.5f, radius);
        float aa = MathF.Min(1f / _tScale, half);    // ~1 PHYSICAL px, as FillCircle
        float roT = radius + half, roS = roT - aa;            // outer: transparent rim → solid
        float riT = MathF.Max(0f, radius - half), riS = riT + aa;   // inner: transparent rim → solid
        if (riS > roS) riS = roS = (riT + roT) * 0.5f;        // very thin ring → collapse the solid band
        int seg = Math.Clamp((int)(roT * 0.7f) + 10, 16, 128);
        float step = MathF.Tau / seg;
        UIColor e = col.WithAlpha(0f);
        Vector2 dPrev = new(1f, 0f);
        for (int i = 1; i <= seg; i++)
        {
            Vector2 dCur = new(MathF.Cos(i * step), MathF.Sin(i * step));
            QuadVc(center + dPrev * riT, e, center + dCur * riT, e, center + dCur * riS, col, center + dPrev * riS, col);   // inner feather
            QuadVc(center + dPrev * riS, col, center + dCur * riS, col, center + dCur * roS, col, center + dPrev * roS, col); // solid band
            QuadVc(center + dPrev * roS, col, center + dCur * roS, col, center + dCur * roT, e, center + dPrev * roT, e);     // outer feather
            dPrev = dCur;
        }
    }

    /// <summary>Filled rounded rectangle (centre cross + 4 edge strips + 4 corner fans).</summary>
    public void FillRoundedRect(Rect r, float radius, UIColor col)
    {
        Rec(r.X, r.Y, r.X + r.Width, r.Y + r.Height, "rrect");
        bool sup = _recSup; _recSup = true;
        FillRoundedRectRaw(r, radius, col);
        _recSup = sup;
    }

    void FillRoundedRectRaw(Rect r, float radius, UIColor col)
    {
        radius = MathF.Min(radius, MathF.Min(r.Width, r.Height) * 0.5f);
        if (radius <= 0.5f) { FillRect(r, col); return; }
        float x0 = r.X, y0 = r.Y, x1 = r.X + r.Width, y1 = r.Y + r.Height;
        FillQuad(new(x0 + radius, y0), new(x1 - radius, y0), new(x1 - radius, y1), new(x0 + radius, y1), col); // centre column
        FillQuad(new(x0, y0 + radius), new(x0 + radius, y0 + radius), new(x0 + radius, y1 - radius), new(x0, y1 - radius), col); // left
        FillQuad(new(x1 - radius, y0 + radius), new(x1, y0 + radius), new(x1, y1 - radius), new(x1 - radius, y1 - radius), col); // right
        const float HP = MathF.PI * 0.5f;
        CornerFan(new(x0 + radius, y0 + radius), radius, MathF.PI,     MathF.PI + HP, col);  // top-left
        CornerFan(new(x1 - radius, y0 + radius), radius, MathF.PI + HP, MathF.PI * 2f, col);  // top-right
        CornerFan(new(x1 - radius, y1 - radius), radius, 0f,           HP,            col);  // bottom-right
        CornerFan(new(x0 + radius, y1 - radius), radius, HP,           MathF.PI,      col);  // bottom-left
    }

    void CornerFan(Vector2 c, float radius, float a0, float a1, UIColor col)
    {
        int seg = Math.Clamp((int)(radius * 0.5f) + 3, 4, 16);
        float step = (a1 - a0) / seg;
        Vector2 prev = c + new Vector2(MathF.Cos(a0), MathF.Sin(a0)) * radius;
        for (int i = 1; i <= seg; i++)
        {
            Vector2 cur = c + new Vector2(MathF.Cos(a0 + i * step), MathF.Sin(a0 + i * step)) * radius;
            Push(c, col); Push(prev, col); Push(cur, col);
            prev = cur;
        }
    }

    /// <summary>Outline of a rounded rectangle, <paramref name="thickness"/> wide, centred on the path
    /// (4 straight edge strips inset by the corner radius + 4 quarter-ring corners). Relies on MSAA for
    /// edge AA, like <see cref="FillRoundedRect"/>.</summary>
    public void StrokeRoundedRect(Rect r, float radius, float thickness, UIColor col)
    {
        if (thickness <= 0f) return;
        float h0 = thickness * 0.5f;
        Rec(r.X - h0, r.Y - h0, r.X + r.Width + h0, r.Y + r.Height + h0, "rrect-stroke");
        bool sup = _recSup; _recSup = true;
        StrokeRoundedRectRaw(r, radius, thickness, col);
        _recSup = sup;
    }

    void StrokeRoundedRectRaw(Rect r, float radius, float thickness, UIColor col)
    {
        radius = MathF.Max(0f, MathF.Min(radius, MathF.Min(r.Width, r.Height) * 0.5f));
        float half = thickness * 0.5f;
        float x0 = r.X, y0 = r.Y, x1 = r.X + r.Width, y1 = r.Y + r.Height;
        FillQuad(new(x0 + radius, y0 - half), new(x1 - radius, y0 - half), new(x1 - radius, y0 + half), new(x0 + radius, y0 + half), col); // top
        FillQuad(new(x0 + radius, y1 - half), new(x1 - radius, y1 - half), new(x1 - radius, y1 + half), new(x0 + radius, y1 + half), col); // bottom
        FillQuad(new(x0 - half, y0 + radius), new(x0 + half, y0 + radius), new(x0 + half, y1 - radius), new(x0 - half, y1 - radius), col); // left
        FillQuad(new(x1 - half, y0 + radius), new(x1 + half, y0 + radius), new(x1 + half, y1 - radius), new(x1 - half, y1 - radius), col); // right
        if (radius > 0.5f)
        {
            const float HP = MathF.PI * 0.5f;
            StrokeArc(new(x0 + radius, y0 + radius), radius, half, MathF.PI,      MathF.PI + HP, col);  // top-left
            StrokeArc(new(x1 - radius, y0 + radius), radius, half, MathF.PI + HP, MathF.PI * 2f, col);  // top-right
            StrokeArc(new(x1 - radius, y1 - radius), radius, half, 0f,            HP,            col);   // bottom-right
            StrokeArc(new(x0 + radius, y1 - radius), radius, half, HP,            MathF.PI,      col);   // bottom-left
        }
    }

    void StrokeArc(Vector2 c, float radius, float half, float a0, float a1, UIColor col)
    {
        float ri = MathF.Max(0f, radius - half), ro = radius + half;
        int seg = Math.Clamp((int)(ro * 0.5f) + 3, 4, 24);
        float step = (a1 - a0) / seg;
        Vector2 dPrev = new(MathF.Cos(a0), MathF.Sin(a0));
        for (int i = 1; i <= seg; i++)
        {
            Vector2 dCur = new(MathF.Cos(a0 + i * step), MathF.Sin(a0 + i * step));
            FillQuad(c + dPrev * ri, c + dCur * ri, c + dCur * ro, c + dPrev * ro, col);
            dPrev = dCur;
        }
    }

    // ── particle batch (called by SgParticleRenderer) ─────────────────────────────────────────────
    public void BeginParticleBatch(BlendMode blend, sg_view tex) { _curBlend = blend; _curTex = tex; _curStart = _instCount; }

    public void PushParticle(Vector2 pos, float halfW, float halfH, float rot, UIColor col, float u0, float v0, float u1, float v1, int mode)
    {
        if (_instCount >= _insts.Length) Array.Resize(ref _insts, _insts.Length * 2);
        _insts[_instCount] = new Inst
        {
            PX = pos.X * _tScale + _tOrigin.X, PY = pos.Y * _tScale + _tOrigin.Y,
            SX = halfW * _tScale, SY = halfH * _tScale, Rot = rot,
            R = col.R, G = col.G, B = col.B, A = col.A,
            U0 = u0, V0 = v0, U1 = u1, V1 = v1, Mode = mode,
        };
        _instCount++;
    }

    public void EndParticleBatch()
    {
        int n = _instCount - _curStart;
        if (n > 0) _cmds.Add(new PartCmd { Blend = _curBlend, Tex = _curTex, Start = _curStart, Count = n });
    }

    public void Dispose()
    {
        DestroyTarget();
        if (_vbuf.id != 0)        sg_destroy_buffer(_vbuf);
        if (_instBuf.id != 0)     sg_destroy_buffer(_instBuf);
        if (_quadVbuf.id != 0)    sg_destroy_buffer(_quadVbuf);
        if (_scenePip.id != 0)    sg_destroy_pipeline(_scenePip);
        if (_sceneShader.id != 0) sg_destroy_shader(_sceneShader);
        if (_partPipAdd.id != 0)    sg_destroy_pipeline(_partPipAdd);
        if (_partPipNormal.id != 0) sg_destroy_pipeline(_partPipNormal);
        if (_partShader.id != 0)  sg_destroy_shader(_partShader);
        if (_partSampler.id != 0) sg_destroy_sampler(_partSampler);
        if (_whiteView.id != 0)   sg_destroy_view(_whiteView);
        if (_whiteImg.id != 0)    sg_destroy_image(_whiteImg);
        if (_blitPip.id != 0)     sg_destroy_pipeline(_blitPip);
        if (_blitShader.id != 0)  sg_destroy_shader(_blitShader);
        if (_blitSampler.id != 0) sg_destroy_sampler(_blitSampler);
        if (_bloomExtractPip.id != 0)   sg_destroy_pipeline(_bloomExtractPip);
        if (_bloomBlurPip.id != 0)      sg_destroy_pipeline(_bloomBlurPip);
        if (_bloomCompositePip.id != 0) sg_destroy_pipeline(_bloomCompositePip);
        if (_bloomExtractShader.id != 0) sg_destroy_shader(_bloomExtractShader);
        if (_bloomBlurShader.id != 0)    sg_destroy_shader(_bloomBlurShader);
        _vbuf = default; _instBuf = default; _quadVbuf = default;
        _scenePip = default; _sceneShader = default; _partPipAdd = default; _partPipNormal = default;
        _partShader = default; _partSampler = default; _whiteView = default; _whiteImg = default;
        _blitPip = default; _blitShader = default; _blitSampler = default;
        _bloomExtractPip = default; _bloomBlurPip = default; _bloomCompositePip = default;
        _bloomExtractShader = default; _bloomBlurShader = default;
        if (_inited) LiveCount--;
        _inited = false;
    }
}
