
using Sokol;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Numerics;
using static System.Numerics.Matrix4x4;
using static Sokol.SG;
using static Sokol.SGlue;
using static Sokol.Utils;
using static Sokol.SApp;
using static Sokol.SG.sg_vertex_format;
using static Sokol.SG.sg_index_type;
using static Sokol.SG.sg_cull_mode;
using static Sokol.SG.sg_compare_func;
using static Sokol.SG.sg_load_action;
using static Sokol.SG.sg_vertex_step;

using static instancing_sapp_shader_cs.Shaders;
using System.Diagnostics;

public static unsafe class InstancingSApp
{

    static bool PauseUpdate = false;


    const int MAX_PARTICLES = 512 * 1024;
    const int NUM_PARTICLES_EMITTED_PER_FRAME = 10;

    private class ParticleState
    {

        public sg_pass_action pass_action;
        public sg_pipeline pip;
        public sg_bindings bind;
        public float ry;
        public int cur_num_particles;

        public Vector3[] pos = new Vector3[MAX_PARTICLES];
        public Vector3[] vel = new Vector3[MAX_PARTICLES];
    }
    private static ParticleState state = new ParticleState();




    [UnmanagedCallersOnly]
    private static unsafe void Init()
    {
        sg_setup(new sg_desc()
        {
            environment = sglue_environment(),
            logger = new sg_logger()
        });


        // default pass action: clear to blue-ish
        state.pass_action = default;
        state.pass_action.colors[0].load_action = SG_LOADACTION_CLEAR;
        state.pass_action.colors[0].clear_value = new sg_color() { r = 0f, g = 0f, b = 0f, a = 1.0f };

        // vertex buffer for static geometry, goes into vertex-buffer-slot 0
        const float r = 0.05f;
        float[] vertices = {
            // positions            colors
            0.0f,   -r, 0.0f,       1.0f, 0.0f, 0.0f, 1.0f,
            r, 0.0f, r,          0.0f, 1.0f, 0.0f, 1.0f,
            r, 0.0f, -r,         0.0f, 0.0f, 1.0f, 1.0f,
            -r, 0.0f, -r,         1.0f, 1.0f, 0.0f, 1.0f,
            -r, 0.0f, r,          0.0f, 1.0f, 1.0f, 1.0f,
            0.0f,    r, 0.0f,       1.0f, 0.0f, 1.0f, 1.0f
        };
        state.bind.vertex_buffers[0] = sg_make_buffer(new sg_buffer_desc()
        {
            data = SG_RANGE(vertices),
            label = "geometry-vertices"
        });

        // index buffer for static geometry
        UInt16[] indices = {
        0, 1, 2,    0, 2, 3,    0, 3, 4,    0, 4, 1,
        5, 1, 2,    5, 2, 3,    5, 3, 4,    5, 4, 1
    };
        state.bind.index_buffer = sg_make_buffer(new sg_buffer_desc()
        {
            usage = new sg_buffer_usage { index_buffer = true },
            data = SG_RANGE(indices),
            label = "geometry-indices"
        });

        // empty, dynamic instance-data vertex buffer, goes into vertex-buffer-slot 1
        state.bind.vertex_buffers[1] = sg_make_buffer(new sg_buffer_desc()
        {
            size = (nuint)(MAX_PARTICLES * Marshal.SizeOf<Vector3>()),
            usage = new sg_buffer_usage { stream_update = true},
            label = "instance-data"
        });

        // shader object
        sg_shader shd = sg_make_shader(instancing_shader_desc(sg_query_backend()));

        // pipeline object
        sg_pipeline_desc pipeline_desc = default;
        pipeline_desc.layout.buffers[1].step_func = SG_VERTEXSTEP_PER_INSTANCE;
        pipeline_desc.layout.attrs[ATTR_instancing_pos] = new sg_vertex_attr_state() { format = SG_VERTEXFORMAT_FLOAT3, buffer_index = 0 };
        pipeline_desc.layout.attrs[ATTR_instancing_color0] = new sg_vertex_attr_state() { format = SG_VERTEXFORMAT_FLOAT4, buffer_index = 0 };
        pipeline_desc.layout.attrs[ATTR_instancing_inst_pos] = new sg_vertex_attr_state() { format = SG_VERTEXFORMAT_FLOAT3, buffer_index = 1 };
        pipeline_desc.shader = shd;
        pipeline_desc.index_type = SG_INDEXTYPE_UINT16;
        pipeline_desc.cull_mode = SG_CULLMODE_BACK;
        pipeline_desc.depth.compare = SG_COMPAREFUNC_LESS_EQUAL;
        pipeline_desc.depth.write_enabled = true;
        state.pip = sg_make_pipeline(pipeline_desc);
    }


    [UnmanagedCallersOnly]
    private static unsafe void Frame()
    {
        if (PauseUpdate) return;

        float frame_time = (float)sapp_frame_duration();

        // emit new particles
        for (int i = 0; i < NUM_PARTICLES_EMITTED_PER_FRAME; i++)
        {
            if (state.cur_num_particles < MAX_PARTICLES)
            {
                state.pos[state.cur_num_particles] = new Vector3(0.0f, 0.0f, 0.0f);
                state.vel[state.cur_num_particles] = new Vector3(
                    ((float)(rand() & 0x7FFF) / 0x7FFF) - 0.5f,
                    ((float)(rand() & 0x7FFF) / 0x7FFF) * 0.5f + 2.0f,
                    ((float)(rand() & 0x7FFF) / 0x7FFF) - 0.5f);

                state.cur_num_particles++;
            }
            else
            {
                break;
            }
        }

        // update particle positions
        for (int i = 0; i < state.cur_num_particles; i++)
        {
            state.vel[i].Y -= 1.0f * frame_time;
            state.pos[i].X += state.vel[i].X * frame_time;
            state.pos[i].Y += state.vel[i].Y * frame_time;
            state.pos[i].Z += state.vel[i].Z * frame_time;
            // bounce back from 'ground'
            if (state.pos[i].Y < -2.0f)
            {
                state.pos[i].Y = -1.8f;
                state.vel[i].Y = -state.vel[i].Y;
                state.vel[i].X *= 0.8f; state.vel[i].Y *= 0.8f; state.vel[i].Z *= 0.8f;
            }
        }

        // lock the state.pos buffer inorder to avoid GC tampering with the buffer
        fixed (Vector3* ptr_state_pos = state.pos)
        {
            //update instance data
            sg_update_buffer(state.bind.vertex_buffers[1], new sg_range()
            {
                ptr = ptr_state_pos,
                size = (uint)(state.cur_num_particles * Marshal.SizeOf<Vector3>())
            });

            // model-view-projection matrix
            Matrix4x4 proj = CreatePerspectiveFieldOfView(60.0f * (float)Math.PI / 180.0f, sapp_widthf() / sapp_heightf(), 0.01f, 50.0f);
            Matrix4x4 view = CreateLookAt(new Vector3(0.0f, 1.5f, 12.0f), new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, 1.0f, 0.0f));
            Matrix4x4 view_proj = view * proj;
            state.ry += 1.0f * frame_time;
            vs_params_t vs_params = default;
            vs_params.mvp = CreateFromAxisAngle(new Vector3(0.0f, 1.0f, 0.0f), state.ry) * view_proj;

            // ...and draw
            sg_begin_pass(new sg_pass()
            {
                action = state.pass_action,
                swapchain = sglue_swapchain(),
                label = "swapchain-pass",
            });
            sg_apply_pipeline(state.pip);
            sg_apply_bindings(state.bind);
            sg_apply_uniforms(UB_vs_params, SG_RANGE<vs_params_t>(ref vs_params));
            sg_draw(0, 24, (uint)state.cur_num_particles);

            sg_end_pass();

        } // free fixed block state.pos , GC can now tamper with the buffer


        sg_commit();


    }

    [UnmanagedCallersOnly]
    static void Cleanup()
    {
        sg_shutdown();

        if (Debugger.IsAttached)
        {
            Environment.Exit(0);
        }
    }

    [UnmanagedCallersOnly]
    private static unsafe void Event(SApp.sapp_event* e)
    {
        if (e->type == SApp.sapp_event_type.SAPP_EVENTTYPE_KEY_UP)
        {
            PauseUpdate = !PauseUpdate;
        }
    }

    public static SApp.sapp_desc sokol_main()
    {
        return new SApp.sapp_desc()
        {
            init_cb = &Init,
            frame_cb = &Frame,
            event_cb = &Event,
            cleanup_cb = &Cleanup,
            width = 800,
            height = 600,
            sample_count = 4,
            window_title = "Instancing (sokol-app)",
            icon = { sokol_default = true },
        };
    }

}