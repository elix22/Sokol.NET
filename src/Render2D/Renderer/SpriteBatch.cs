using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using static Sokol.SG;
using static Sokol.SGlue;
using static sprite_shader_cs_r2d.Shaders;

namespace Sokol.Render2D;

/// <summary>
/// Textured quads in WORLD units, drawn with a camera's view-projection matrix inside the pass that is
/// open — an offscreen target or the swapchain. Where <see cref="Render2DSurface"/> owns a target and works
/// in screen pixels, this class owns none: a 3D renderer calls it after its meshes, so a sprite is hidden
/// by a mesh in front of it (depth is tested, not written) and sprites blend over each other in the order
/// they were given.
/// <para>
/// Usage, per view: <see cref="Begin"/> → <see cref="Draw"/> for each sprite, back to front →
/// <see cref="Flush"/> inside the open pass. Once per frame, before the first view: <see cref="BeginFrame"/>.
/// A run of sprites with the same texture and filter is one draw call. Steady state allocates nothing.
/// </para>
/// </summary>
public sealed unsafe class SpriteBatch : IDisposable
{
    // One sprite: the quad is Origin + u * AxisX + v * AxisY for (u, v) in [0,1]². 56 bytes.
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct Inst
    {
        public Vector3 Origin, AxisX, AxisY;
        public Vector4 Uv;      // xy = uv at the quad's (0,0) corner, zw = uv at its (1,1) corner
        public uint Rgba;       // bytes R, G, B, A
    }

    struct Cmd { public sg_view Tex; public bool Nearest; public int Start, Count; }

    const int InitSprites = 4 * 1024;

    sg_shader   _shader;
    sg_pipeline _pipOffscreen, _pipSwapchain;
    sg_buffer   _quadVbuf, _instBuf;
    int         _instBufCap;         // sprites the GPU buffer holds per frame (all views together)
    int         _wantCap;            // > _instBufCap after a frame that did not fit
    sg_sampler  _linear, _nearest;
    sg_image    _whiteImg;
    sg_view     _whiteView;
    bool        _inited, _overflowLogged;

    Inst[] _insts = new Inst[InitSprites];
    int    _count;
    readonly List<Cmd> _cmds = new();

    /// <summary>Draw calls of the last <see cref="Flush"/>.</summary>
    public int DrawCalls { get; private set; }
    /// <summary>Sprites drawn by the last <see cref="Flush"/>.</summary>
    public int SpriteCount { get; private set; }

    /// <summary>A 1×1 white texture: draw with it for a quad of one colour.</summary>
    public sg_view WhiteView { get { Init(); return _whiteView; } }

    void Init()
    {
        if (_inited) return;
        _inited = true;

        _shader = sg_make_shader(r2d_sprite_shader_desc(sg_query_backend()));

        float[] quad = { 0f, 0f,  1f, 0f,  1f, 1f,   0f, 0f,  1f, 1f,  0f, 1f };
        fixed (float* q = quad)
            _quadVbuf = sg_make_buffer(new sg_buffer_desc { data = new sg_range { ptr = q, size = (nuint)(quad.Length * sizeof(float)) }, label = "r2d-sprite-quad" });

        _instBufCap = InitSprites;
        _instBuf = MakeInstBuffer(_instBufCap);

        _linear  = MakeSampler(sg_filter.SG_FILTER_LINEAR,  "r2d-sprite-linear");
        _nearest = MakeSampler(sg_filter.SG_FILTER_NEAREST, "r2d-sprite-nearest");

        uint white = 0xFFFFFFFFu;
        _whiteImg = sg_make_image(new sg_image_desc
        {
            width = 1, height = 1, pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA8,
            data = { mip_levels = { [0] = new sg_range { ptr = &white, size = 4 } } }, label = "r2d-sprite-white",
        });
        _whiteView = sg_make_view(new sg_view_desc { texture = { image = _whiteImg }, label = "r2d-sprite-white-view" });
    }

    static sg_sampler MakeSampler(sg_filter filter, string label) => sg_make_sampler(new sg_sampler_desc
    {
        min_filter = filter, mag_filter = filter,
        wrap_u = sg_wrap.SG_WRAP_CLAMP_TO_EDGE, wrap_v = sg_wrap.SG_WRAP_CLAMP_TO_EDGE,
        label = label,
    });

    static sg_buffer MakeInstBuffer(int sprites) => sg_make_buffer(new sg_buffer_desc
    {
        size = (nuint)(sprites * sizeof(Inst)),
        usage = { vertex_buffer = true, stream_update = true },
        label = "r2d-sprite-instances",
    });

    // A pipeline for each kind of target: an offscreen one (RGBA8 + depth, one sample — what the 3D
    // renderer's offscreen targets are) and the swapchain as the app set it up.
    sg_pipeline Pipeline(bool offscreen)
    {
        ref sg_pipeline pip = ref (offscreen ? ref _pipOffscreen : ref _pipSwapchain);
        if (pip.id != 0) return pip;

        var desc = new sg_pipeline_desc
        {
            shader         = _shader,
            primitive_type = sg_primitive_type.SG_PRIMITIVETYPE_TRIANGLES,
            cull_mode      = sg_cull_mode.SG_CULLMODE_NONE,     // a flipped or turned sprite is still seen
            depth          = new sg_depth_state { compare = sg_compare_func.SG_COMPAREFUNC_LESS_EQUAL, write_enabled = false },
            label          = offscreen ? "r2d-sprite-offscreen" : "r2d-sprite-swapchain",
        };
        desc.colors[0].blend = new sg_blend_state
        {
            enabled = true,
            src_factor_rgb   = sg_blend_factor.SG_BLENDFACTOR_SRC_ALPHA,
            dst_factor_rgb   = sg_blend_factor.SG_BLENDFACTOR_ONE_MINUS_SRC_ALPHA,
            src_factor_alpha = sg_blend_factor.SG_BLENDFACTOR_ONE,
            dst_factor_alpha = sg_blend_factor.SG_BLENDFACTOR_ONE_MINUS_SRC_ALPHA,
        };
        if (offscreen)
        {
            desc.colors[0].pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA8;
            desc.depth.pixel_format     = sg_pixel_format.SG_PIXELFORMAT_DEPTH;
            desc.sample_count           = 1;
        }
        else
        {
            var sc = sglue_swapchain();
            desc.colors[0].pixel_format = sc.color_format;
            desc.depth.pixel_format     = sc.depth_format;
            desc.sample_count           = sc.sample_count;
        }

        desc.layout.buffers[0].stride = 2 * sizeof(float);
        desc.layout.buffers[1].stride = sizeof(Inst);
        desc.layout.buffers[1].step_func = sg_vertex_step.SG_VERTEXSTEP_PER_INSTANCE;
        desc.layout.attrs[ATTR_r2d_sprite_corner]   = new sg_vertex_attr_state { buffer_index = 0, offset = 0,  format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT2 };
        desc.layout.attrs[ATTR_r2d_sprite_i_origin] = new sg_vertex_attr_state { buffer_index = 1, offset = 0,  format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT3 };
        desc.layout.attrs[ATTR_r2d_sprite_i_axis_x] = new sg_vertex_attr_state { buffer_index = 1, offset = 12, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT3 };
        desc.layout.attrs[ATTR_r2d_sprite_i_axis_y] = new sg_vertex_attr_state { buffer_index = 1, offset = 24, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT3 };
        desc.layout.attrs[ATTR_r2d_sprite_i_uvrect] = new sg_vertex_attr_state { buffer_index = 1, offset = 36, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };
        desc.layout.attrs[ATTR_r2d_sprite_i_color]  = new sg_vertex_attr_state { buffer_index = 1, offset = 52, format = sg_vertex_format.SG_VERTEXFORMAT_UBYTE4N };
        pip = sg_make_pipeline(desc);
        return pip;
    }

    /// <summary>
    /// Once per frame, before the first view. A frame whose sprites did not fit the GPU buffer gets a larger
    /// one here (a buffer cannot be replaced while a frame has drawn from it).
    /// </summary>
    public void BeginFrame()
    {
        if (!_inited || _wantCap <= _instBufCap) return;
        sg_destroy_buffer(_instBuf);
        _instBufCap = _wantCap;
        _instBuf = MakeInstBuffer(_instBufCap);
        _overflowLogged = false;
    }

    /// <summary>Starts a view's list of sprites.</summary>
    public void Begin() { _count = 0; _cmds.Clear(); }

    /// <summary>
    /// Adds a sprite: the quad <paramref name="origin"/> + u·<paramref name="axisX"/> + v·<paramref name="axisY"/>
    /// in world units, showing the part of <paramref name="texture"/> between the uv of its (0,0) corner
    /// (<paramref name="uv"/>.XY) and of its (1,1) corner (.ZW), times <paramref name="rgba"/> (bytes R, G, B, A
    /// from the lowest). Sprites are drawn in the order they are added.
    /// </summary>
    public void Draw(sg_view texture, bool nearest, Vector3 origin, Vector3 axisX, Vector3 axisY, Vector4 uv, uint rgba)
    {
        if (_count >= _insts.Length) Array.Resize(ref _insts, _insts.Length * 2);   // grow, don't drop
        _insts[_count] = new Inst { Origin = origin, AxisX = axisX, AxisY = axisY, Uv = uv, Rgba = rgba };

        int last = _cmds.Count - 1;
        if (last >= 0 && _cmds[last].Tex.id == texture.id && _cmds[last].Nearest == nearest)
        {
            var c = _cmds[last]; c.Count++; _cmds[last] = c;
        }
        else _cmds.Add(new Cmd { Tex = texture, Nearest = nearest, Start = _count, Count = 1 });
        _count++;
    }

    /// <summary>
    /// Draws the view's sprites into the open pass. <paramref name="offscreen"/>: the pass's target is an
    /// offscreen one (RGBA8 colour, a depth image, one sample); false = the swapchain.
    /// </summary>
    public void Flush(in Matrix4x4 viewProj, bool offscreen)
    {
        DrawCalls = 0; SpriteCount = 0;
        if (_count == 0) return;
        Init();

        // All the views of a frame share the buffer, each appending its part. What does not fit is not
        // drawn this frame, and the buffer is larger from the next one.
        if (sg_query_buffer_will_overflow(_instBuf, (nuint)(_count * sizeof(Inst))))
        {
            _wantCap = Math.Max(_wantCap, Math.Max(_instBufCap * 2, _count * 2));
            if (!_overflowLogged) { _overflowLogged = true; Console.Error.WriteLine($"[SpriteBatch] {_count} sprites did not fit this frame; the buffer grows to {_wantCap}."); }
            return;
        }
        int offset;
        fixed (Inst* p = _insts) offset = sg_append_buffer(_instBuf, new sg_range { ptr = p, size = (nuint)(_count * sizeof(Inst)) });

        Matrix4x4 vp = viewProj;
        sg_apply_pipeline(Pipeline(offscreen));
        for (int i = 0; i < _cmds.Count; i++)
        {
            var cmd = _cmds[i];
            var bind = default(sg_bindings);
            bind.vertex_buffers[0] = _quadVbuf;
            bind.vertex_buffers[1] = _instBuf;
            bind.vertex_buffer_offsets[1] = offset + cmd.Start * sizeof(Inst);
            bind.views[VIEW_r2d_sprite_tex]    = cmd.Tex.id != 0 ? cmd.Tex : _whiteView;
            bind.samplers[SMP_r2d_sprite_smp]  = cmd.Nearest ? _nearest : _linear;
            sg_apply_bindings(bind);
            sg_apply_uniforms(UB_r2d_sprite_params, new sg_range { ptr = &vp, size = (nuint)sizeof(Matrix4x4) });
            sg_draw(0, 6, (uint)cmd.Count);
            DrawCalls++;
        }
        SpriteCount = _count;
    }

    public void Dispose()
    {
        if (!_inited) return;
        _inited = false;
        if (_pipOffscreen.id != 0) sg_destroy_pipeline(_pipOffscreen);
        if (_pipSwapchain.id != 0) sg_destroy_pipeline(_pipSwapchain);
        sg_destroy_shader(_shader);
        sg_destroy_buffer(_quadVbuf);
        sg_destroy_buffer(_instBuf);
        sg_destroy_sampler(_linear);
        sg_destroy_sampler(_nearest);
        sg_destroy_view(_whiteView);
        sg_destroy_image(_whiteImg);
        _pipOffscreen = _pipSwapchain = default;
    }
}
