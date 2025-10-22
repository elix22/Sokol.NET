using System;
using System.IO;
using Sokol;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Numerics;
using static Sokol.SApp;
using static Sokol.SG;
using static Sokol.SGlue;
using static Sokol.SG.sg_vertex_format;
using static Sokol.SG.sg_index_type;
using static Sokol.SG.sg_cull_mode;
using static Sokol.SG.sg_compare_func;
using static Sokol.Utils;
using System.Diagnostics;
using static Sokol.SLog;
using static Sokol.SDebugUI;
using Assimp.Configs;
using static Sokol.SFetch;
using static Sokol.SDebugText;
using static assimp_simple_app_shader_cs.Shaders;
using Assimp;

public static unsafe class AssimpSimpleApp
{
    private const int NUM_FONTS = 3;
    private const int FONT_KC854 = 0;
    private const int FONT_C64 = 1;
    private const int FONT_ORIC = 2;
    struct color_t
    {
        public byte r, g, b;
    };

    static readonly Random random = new Random();
    public static float NextRandom(float min, float max) { return (float)((random.NextDouble() * (max - min)) + min); }


    public class SimpleMesh
    {

        public SimpleMesh(float[] vertices, UInt16[] indices)
        {
            VertexBuffer = sg_make_buffer(new sg_buffer_desc()
            {
                data = SG_RANGE(vertices),
                label = "assimp-simple-vertex-buffer"
            });
            IndexBuffer = sg_make_buffer(new sg_buffer_desc()
            {
                usage = new sg_buffer_usage { index_buffer = true },
                data = SG_RANGE(indices),
                label = "assimp-simple-index-buffer"
            });

            VertexCount = vertices.Length / 7; // 3 pos + 4 color
            IndexCount = indices.Length;
        }

        public void Draw()
        {
            sg_bindings bind = default;
            bind.vertex_buffers[0] = VertexBuffer;
            bind.index_buffer = IndexBuffer;
            sg_apply_bindings(bind);
            sg_draw(0, (uint)IndexCount, 1);
        }
        
        public sg_buffer VertexBuffer;
        public sg_buffer IndexBuffer;

        public int VertexCount;
        public int IndexCount;
    }


    enum state_loading_enum
    {
        STATE_IDLE = 0,
        STATE_LOADING,
        STATE_LOADED,
        STATE_FAILED
    }

    class _state
    {
        public float rx, ry;
        public sg_pass_action pass_action;
        public color_t[] palette = new color_t[NUM_FONTS];
        public SharedBuffer fetch_buffer = SharedBuffer.Create(285 * 1024);
        public state_loading_enum state_loading = state_loading_enum.STATE_IDLE;

        public List<SimpleMesh> m_simpleMeshes;

        public sg_pipeline pip;
        public Sokol.Camera camera = new Sokol.Camera();

        public sg_buffer cube_vbuf;
        public sg_buffer cube_ibuf;
    }

    static _state state = new _state();


    [UnmanagedCallersOnly]
    private static unsafe void Init()
    {
        Console.WriteLine("Assimp: Init()");
        sg_setup(new sg_desc()
        {
            environment = sglue_environment(),
            logger = {
                func = &slog_func,
            }
        });

        state.camera.Init(new CameraDesc()
        {
            Aspect = 60.0f,
            NearZ = 0.01f,
            FarZ = 500.0f,
            Center = new Vector3(0.0f, 150f, 0.0f),
            Distance = 400.0f,
        });

        state.palette[FONT_KC854] = new color_t { r = 0xf4, g = 0x43, b = 0x36 };
        state.palette[FONT_C64] = new color_t { r = 0x21, g = 0x96, b = 0xf3 };
        state.palette[FONT_ORIC] = new color_t { r = 0x4c, g = 0xaf, b = 0x50 };

        sdtx_desc_t desc = default;
        desc.fonts[FONT_KC854] = sdtx_font_kc854();
        desc.fonts[FONT_C64] = sdtx_font_c64();
        desc.fonts[FONT_ORIC] = sdtx_font_oric();
        sdtx_setup(desc);

        // setup sokol-fetch with the minimal "resource limits"
        sfetch_setup(new sfetch_desc_t()
        {
            max_requests = 1,
            num_channels = 1,
            num_lanes = 1,
            logger = {
                func = &slog_func,
            }
        });


        state.pass_action = default;
        state.pass_action.colors[0].load_action = sg_load_action.SG_LOADACTION_CLEAR;
        state.pass_action.colors[0].clear_value = new sg_color { r = 0.25f, g = 0.5f, b = 0.75f, a = 1.0f };

        sg_shader shd = sg_make_shader(assimp_shader_desc(sg_query_backend()));

        var pipeline_desc = default(sg_pipeline_desc);
        pipeline_desc.layout.buffers[0].stride = 28;
        pipeline_desc.layout.attrs[ATTR_assimp_position].format = SG_VERTEXFORMAT_FLOAT3;
        pipeline_desc.layout.attrs[ATTR_assimp_color0].format = SG_VERTEXFORMAT_FLOAT4;

        pipeline_desc.shader = shd;
        pipeline_desc.index_type = SG_INDEXTYPE_UINT16;
        pipeline_desc.cull_mode = SG_CULLMODE_BACK;
        pipeline_desc.depth.write_enabled = true;
        pipeline_desc.depth.compare = SG_COMPAREFUNC_LESS_EQUAL;
        pipeline_desc.label = "assimp-simple-pipeline";

        state.pip = sg_make_pipeline(pipeline_desc);

        create_cube_buffers();

        sfetch_request_t request = default;
        // Use .collada extension to prevent iOS from converting the XML file to binary plist
        request.path = util_get_file_path("duck.collada");
        request.callback = &fetch_callback;
        request.buffer = SFETCH_RANGE(state.fetch_buffer);
        sfetch_send(request);

        state.state_loading = state_loading_enum.STATE_LOADING;

    }

    static void create_cube_buffers()
    {
/* cube vertex buffer */
        float[] vertices =  {
            -1.0f, -1.0f, -1.0f,   1.0f, 0.0f, 0.0f, 1.0f,
            1.0f, -1.0f, -1.0f,   1.0f, 0.0f, 0.0f, 1.0f,
            1.0f,  1.0f, -1.0f,   1.0f, 0.0f, 0.0f, 1.0f,
            -1.0f,  1.0f, -1.0f,   1.0f, 0.0f, 0.0f, 1.0f,

            -1.0f, -1.0f,  1.0f,   0.0f, 1.0f, 0.0f, 1.0f,
            1.0f, -1.0f,  1.0f,   0.0f, 1.0f, 0.0f, 1.0f,
            1.0f,  1.0f,  1.0f,   0.0f, 1.0f, 0.0f, 1.0f,
            -1.0f,  1.0f,  1.0f,   0.0f, 1.0f, 0.0f, 1.0f,

            -1.0f, -1.0f, -1.0f,   0.0f, 0.0f, 1.0f, 1.0f,
            -1.0f,  1.0f, -1.0f,   0.0f, 0.0f, 1.0f, 1.0f,
            -1.0f,  1.0f,  1.0f,   0.0f, 0.0f, 1.0f, 1.0f,
            -1.0f, -1.0f,  1.0f,   0.0f, 0.0f, 1.0f, 1.0f,

            1.0f, -1.0f, -1.0f,   1.0f, 0.5f, 0.0f, 1.0f,
            1.0f,  1.0f, -1.0f,   1.0f, 0.5f, 0.0f, 1.0f,
            1.0f,  1.0f,  1.0f,   1.0f, 0.5f, 0.0f, 1.0f,
            1.0f, -1.0f,  1.0f,   1.0f, 0.5f, 0.0f, 1.0f,

            -1.0f, -1.0f, -1.0f,   0.0f, 0.5f, 1.0f, 1.0f,
            -1.0f, -1.0f,  1.0f,   0.0f, 0.5f, 1.0f, 1.0f,
            1.0f, -1.0f,  1.0f,   0.0f, 0.5f, 1.0f, 1.0f,
            1.0f, -1.0f, -1.0f,   0.0f, 0.5f, 1.0f, 1.0f,

            -1.0f,  1.0f, -1.0f,   1.0f, 0.0f, 0.5f, 1.0f,
            -1.0f,  1.0f,  1.0f,   1.0f, 0.0f, 0.5f, 1.0f,
            1.0f,  1.0f,  1.0f,   1.0f, 0.0f, 0.5f, 1.0f,
            1.0f,  1.0f, -1.0f,   1.0f, 0.0f, 0.5f, 1.0f
        };

   
        fixed (float* ptr_vertices = vertices)
        {
            state.cube_vbuf = sg_make_buffer(new sg_buffer_desc()
            {
                data = SG_RANGE(vertices),
                label = "cube-vertices"
            }
            );
        }

        UInt16[] indices = {
                0, 1, 2,  0, 2, 3,
                6, 5, 4,  7, 6, 4,
                8, 9, 10,  8, 10, 11,
                14, 13, 12,  15, 14, 12,
                16, 17, 18,  16, 18, 19,
                22, 21, 20,  23, 22, 20
            };


        fixed (UInt16* ptr_indices = indices)
        {
            state.cube_ibuf = sg_make_buffer(new sg_buffer_desc()
            {
                usage = new sg_buffer_usage { index_buffer = true },
                data = SG_RANGE(indices),
                label = "cube-indices"
            }
            );
        }
    }
    

    [UnmanagedCallersOnly]
    static void fetch_callback(sfetch_response_t* response)
    {
        if (response->fetched)
        {
            Console.WriteLine($"Assimp: File fetched, size: {response->data.size} bytes");
        }

        if (response->finished)
        {
            if (response->failed)
            {
                Console.WriteLine($"Assimp: Failed to fetch file: {response->error_code}");
                state.state_loading = state_loading_enum.STATE_FAILED;
                return;
            }

            Console.WriteLine($"Assimp: Fetch completed successfully, parsing {response->data.size} bytes");

            // Check first few bytes to verify data is XML (not binary plist)
            Console.WriteLine($"Assimp: First 10 bytes: {BitConverter.ToString(state.fetch_buffer.Buffer, 0, Math.Min(10, (int)response->data.size))}");

            // File successfully loaded, now parse with Assimp
            var stream = new MemoryStream(state.fetch_buffer.Buffer, 0, (int)response->data.size);
            PostProcessSteps ppSteps = PostProcessPreset.TargetRealTimeQuality | PostProcessSteps.FlipWindingOrder;
            AssimpContext importer = new AssimpContext();
            importer.SetConfig(new NormalSmoothingAngleConfig(66f));

            // Extract file extension from the path to use as format hint
            string path = response->path != IntPtr.Zero ? Marshal.PtrToStringUTF8((IntPtr)response->path) ?? "" : "";
            string formatHint = Path.GetExtension(path).TrimStart('.'); // Get extension without the dot (e.g., "dae", "obj", "fbx")

            Scene scene = importer.ImportFileFromStream(stream, ppSteps, formatHint);
            if (scene != null)
            {
                state.state_loading = state_loading_enum.STATE_LOADED;
                Console.WriteLine($"Assimp: Successfully loaded model (format: {formatHint}).");
                ProcesScene(scene);
            }
            else
            {
                state.state_loading = state_loading_enum.STATE_FAILED;
                Console.WriteLine($"Assimp: Failed to load model (format: {formatHint}).");
            }
        }
    }

    private static void ProcesScene(Scene scene)
    {

        state.m_simpleMeshes = new List<SimpleMesh>();


         var   m_sceneMin = new Vector3(1e10f, 1e10f, 1e10f);
         var   m_sceneMax = new Vector3(-1e10f, -1e10f, -1e10f);
      
        foreach (Mesh m in scene.Meshes)
        {
            List<Vector3> verts = m.Vertices;
            List<Vector3> norms = (m.HasNormals) ? m.Normals : null;
            List<Vector3> uvs = m.HasTextureCoords(0) ? m.TextureCoordinateChannels[0] : null;

            float[] float_vertices = new float[verts.Count * 7]; // 8 floats per vertex
            UInt16[] int_indices = new UInt16[m.FaceCount * 3];
              
            for (int i = 0; i < verts.Count; i++)
            {
                Vector3 pos = verts[i];
                Vector3 norm = (norms != null) ? norms[i] : new Vector3(0, 0, 0);
                Vector3 uv = (uvs != null) ? uvs[i] : new Vector3(0, 0, 0);

                float_vertices[i * 7 + 0] = pos.X;
                float_vertices[i * 7 + 1] = pos.Y;
                float_vertices[i * 7 + 2] = pos.Z;
                float_vertices[i * 7 + 3] = NextRandom(0.0f, 1.0f);
                float_vertices[i * 7 + 4] = NextRandom(0.0f, 1.0f);
                float_vertices[i * 7 + 5] = NextRandom(0.0f, 1.0f);
                float_vertices[i * 7 + 6] = 1.0f;

                m_sceneMin.X = Math.Min(m_sceneMin.X, pos.X);
                m_sceneMin.Y = Math.Min(m_sceneMin.Y, pos.Y);
                m_sceneMin.Z = Math.Min(m_sceneMin.Z, pos.Z);

                m_sceneMax.X = Math.Max(m_sceneMax.X, pos.X);
                m_sceneMax.Y = Math.Max(m_sceneMax.Y, pos.Y);
                m_sceneMax.Z = Math.Max(m_sceneMax.Z, pos.Z);
            }

            List<Face> faces = m.Faces;
            for (int i = 0; i < faces.Count; i++)
            {
                Face f = faces[i];

                //Ignore non-triangle faces
                if (f.IndexCount != 3)
                {
                    continue;
                }

                int_indices[i * 3 + 0] = (UInt16)(f.Indices[0]);
                int_indices[i * 3 + 1] = (UInt16)(f.Indices[1]);
                int_indices[i * 3 + 2] = (UInt16)(f.Indices[2]);
            }

            state.m_simpleMeshes.Add(new SimpleMesh(float_vertices, int_indices));
        }


    }


    [UnmanagedCallersOnly]
    private static unsafe void Frame()
    {
        sfetch_dowork();

        float deltaSeconds = (float)(Sokol.SApp.sapp_frame_duration());

        state.rx += 1.0f * deltaSeconds;
        state.ry += 2.0f * deltaSeconds;


        vs_params_t vs_params = default;

        var rotationMatrixX = Matrix4x4.CreateFromAxisAngle(Vector3.UnitX, state.rx);
        var rotationMatrixY = Matrix4x4.CreateFromAxisAngle(Vector3.UnitY, state.ry);
        var modelMatrix = rotationMatrixX * rotationMatrixY;
        modelMatrix = Matrix4x4.Identity;

        var width = SApp.sapp_widthf();
        var height = SApp.sapp_heightf();

        state.camera.Update(sapp_width(), sapp_height());

        var projectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(
            (float)(60.0f * Math.PI / 180),
            width / height,
            0.01f,
            500.0f);
        var viewMatrix = Matrix4x4.CreateLookAt(
            new Vector3(0.0f, 50f, 260.0f),
            Vector3.Zero,
            Vector3.UnitY);

        // vs_params.mvp = modelMatrix * viewMatrix * projectionMatrix;
        vs_params.mvp = modelMatrix  * state.camera.ViewProj;

        sdtx_canvas(sapp_width() * 0.5f, sapp_height() * 0.5f);
        sdtx_origin(3.0f, 3.0f);
        color_t color = state.palette[0];
        sdtx_color3b(color.r, color.g, color.b);
        sdtx_font((uint)0);
        sdtx_print("Assimp Simple App\n");
        sdtx_print("Loading State: {0}\n", state.state_loading.ToString());

        sg_begin_pass(new sg_pass { action = state.pass_action, swapchain = sglue_swapchain() });
        sg_apply_pipeline(state.pip);


        sg_apply_bindings(new sg_bindings
        {
            vertex_buffers = { [0] = state.cube_vbuf },
            index_buffer = state.cube_ibuf
        });
        sg_apply_uniforms(UB_vs_params, SG_RANGE<vs_params_t>(ref vs_params));
        sg_draw(0, 36, 1);

        if (state.m_simpleMeshes != null)
        {
            foreach (var simpleMesh in state.m_simpleMeshes)
            {
                sg_apply_uniforms(UB_vs_params, SG_RANGE<vs_params_t>(ref vs_params));
                simpleMesh.Draw();
            }
        }
    

        sdtx_draw();
        sg_end_pass();
        sg_commit();
    }


    [UnmanagedCallersOnly]
    private static unsafe void Event(sapp_event* e)
    {
           state.camera.HandleEvent(e);
    }

    [UnmanagedCallersOnly]
    static void Cleanup()
    {
        state.fetch_buffer.Dispose();
        sfetch_shutdown();
        sdtx_shutdown();
        sg_shutdown();

        // Force a complete shutdown if debugging
        if (Debugger.IsAttached)
        {
            Environment.Exit(0);
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
            width = 0,
            height = 0,
            sample_count = 4,
            window_title = "Clear (sokol-app)",
            icon = { sokol_default = true },
            logger = {
                func = &slog_func,
            }
        };
    }

}
