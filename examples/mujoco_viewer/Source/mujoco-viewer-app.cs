// MuJoCo viewer: loads an MJCF model, steps mj_step in real time and draws every geom with Sokol.
// Models: Assets/mjcf/{humanoid,inverted_pendulum,hopper}.xml (keys 1-3) or a path given on the
// command line (absolute path: the app changes its working directory at start). The built-in
// models are compiled from memory through a MuJoCo VFS (no temp files).

using System.Numerics;
using System.Runtime.InteropServices;
using Mujoco;
using Sokol;
using static Mujoco.MujocoLib;
using static Sokol.SApp;
using static Sokol.SDebugText;
using static Sokol.SG;
using static Sokol.SGlue;
using static Sokol.SLog;
using static Sokol.SShape;
using static Sokol.Utils;
using static geom_shader_cs.Shaders;

public static unsafe class MujocoViewerApp
{
    static readonly string[] BuiltinModels = [.. new[] { "humanoid", "inverted_pendulum", "hopper" }
        .Select(n => Path.Combine(AppContext.BaseDirectory, "mjcf", n + ".xml"))];

    struct State
    {
        public sg_pass_action pass_action;
        public sg_pipeline pip;         // indexed sshape primitives
        public sg_pipeline mesh_pip;    // non-indexed mesh geoms
        public sg_bindings bind;
        public sshape_element_range_t box, sphere, cylinder, plane;
    }
    static State state;

    static string _modelPath = "";
    static string _loadError = "";
    static mjModel_* _m;
    static mjData_* _d;
    static sg_buffer[] _meshBuffers = [];
    static int[] _meshVertexCount = [];

    static bool _paused;
    static bool _randomCtrl;
    static bool _track = true;
    static int _key = -1;               // last keyframe applied by K, -1 = qpos0
    static double _wallTime;            // simulated time we owe, advanced by frame duration
    static readonly Random _rng = new(1);

    // orbit camera (MuJoCo is Z-up)
    static float _yaw = -MathF.PI / 2, _pitch = 0.3f, _distance = 3;
    static Vector3 _target;
    static bool _dragging;

    // --shots <dir>: steps each model to the given sim time (random ctrl from RandomFrom on; an
    // earlier time than the last shot resets first), writes <dir>/<Name>.png via ScreenCapture, quits. These are the step 2.4 acceptance views.
    static readonly (int Model, double Time, double RandomFrom, string Name)[] ShotScript =
    [
        (0, 0.0, double.PositiveInfinity, "1_humanoid_start"),
        (0, 4.0, double.PositiveInfinity, "2_humanoid_settled"),
        (1, 0.0, double.PositiveInfinity, "3_pendulum_start"),
        (1, 1.0, double.PositiveInfinity, "4_pendulum_zero_ctrl_1s"),
        (1, 3.0, double.PositiveInfinity, "5_pendulum_zero_ctrl_3s"),
        (1, 1.0, 0.0, "6_pendulum_random_ctrl_1s"),
        (2, 0.0, double.PositiveInfinity, "7_hopper_qpos0"),
        (2, 3.0, double.PositiveInfinity, "8_hopper_zero_ctrl_3s"),
    ];
    static string? _shotDir;
    static int _shot;

    [UnmanagedCallersOnly]
    static void Init()
    {
        sg_setup(new sg_desc
        {
            environment = sglue_environment(),
            logger = { func = &slog_func },
        });
        var dtx = new sdtx_desc_t();
        dtx.fonts[0] = sdtx_font_kc854();
        dtx.logger.func = &slog_func;
        sdtx_setup(dtx);

        state.pass_action.colors[0].load_action = sg_load_action.SG_LOADACTION_CLEAR;
        state.pass_action.colors[0].clear_value = new float[] { 0.12f, 0.14f, 0.18f, 1 };

        var pip = new sg_pipeline_desc
        {
            shader = sg_make_shader(geom_shader_desc(sg_query_backend())),
            index_type = sg_index_type.SG_INDEXTYPE_UINT16,
            cull_mode = sg_cull_mode.SG_CULLMODE_NONE,
            depth = { compare = sg_compare_func.SG_COMPAREFUNC_LESS_EQUAL, write_enabled = true },
        };
        pip.layout.buffers[0] = sshape_vertex_buffer_layout_state();
        pip.layout.attrs[ATTR_geom_position] = sshape_position_vertex_attr_state();
        pip.layout.attrs[ATTR_geom_normal] = sshape_normal_vertex_attr_state();
        state.pip = sg_make_pipeline(pip);
        pip.index_type = sg_index_type.SG_INDEXTYPE_NONE;
        state.mesh_pip = sg_make_pipeline(pip);

        BuildPrimitives();

        string[] args = Environment.GetCommandLineArgs();
        if (args.Length > 2 && args[1] == "--shots")
        {
            _shotDir = Path.GetFullPath(args[2]);
            Directory.CreateDirectory(_shotDir);
            LoadModel(BuiltinModels[ShotScript[0].Model]);
        }
        else
            LoadModel(args.Length > 1 ? args[1] : BuiltinModels[0]);
    }

    // Unit shapes in MuJoCo conventions: box edge 1, sphere radius 1, cylinder radius 1 / height 1
    // along +Z, plane 1x1 in XY. sokol_shape builds cylinders and planes around +Y, so rotate them.
    static void BuildPrimitives()
    {
        var vertices = new sshape_vertex_t[16 * 1024];
        var indices = new ushort[48 * 1024];
        Matrix4x4 yToZ = Matrix4x4.CreateRotationX(MathF.PI / 2);
        fixed (sshape_vertex_t* pv = vertices)
        fixed (ushort* pi = indices)
        {
            var buf = new sshape_buffer_t();
            buf.vertices.buffer = new sshape_range { ptr = pv, size = (nuint)(vertices.Length * sizeof(sshape_vertex_t)) };
            buf.indices.buffer = new sshape_range { ptr = pi, size = (nuint)(indices.Length * sizeof(ushort)) };

            buf = sshape_build_box(in buf, new sshape_box_t { width = 1, height = 1, depth = 1, tiles = 1 });
            state.box = sshape_make_element_range(buf);
            buf = sshape_build_sphere(in buf, new sshape_sphere_t { radius = 1, slices = 32, stacks = 16 });
            state.sphere = sshape_make_element_range(buf);
            buf = sshape_build_cylinder(in buf, new sshape_cylinder_t
            {
                radius = 1, height = 1, slices = 32, stacks = 1,
                transform = sshape_make_mat4(in yToZ.M11),
            });
            state.cylinder = sshape_make_element_range(buf);
            buf = sshape_build_plane(in buf, new sshape_plane_t
            {
                width = 1, depth = 1, tiles = 1,
                transform = sshape_make_mat4(in yToZ.M11),
            });
            state.plane = sshape_make_element_range(buf);

            state.bind.vertex_buffers[0] = sg_make_buffer(sshape_vertex_buffer_desc(buf));
            state.bind.index_buffer = sg_make_buffer(sshape_index_buffer_desc(buf));
        }
    }

    static void LoadModel(string path)
    {
        FreeModel();
        _modelPath = path;
        _loadError = "";
        try
        {
            // FR-M.5: the built-in models (no external assets) compile from memory; a model from the
            // command line loads from disk so MuJoCo resolves its mesh/texture files.
            _m = BuiltinModels.Contains(path)
                ? MjHelpers.LoadXml(Path.GetFileName(path), File.ReadAllBytes(path))
                : MjHelpers.LoadXml(path);
        }
        catch (Exception e) when (e is MujocoException or IOException)
        {
            _loadError = e.Message;
            Console.Error.WriteLine(e.Message);
            return;
        }
        _d = mj_makeData(_m);
        _key = -1;
        _paused = false;
        _randomCtrl = false;
        _wallTime = 0;
        _distance = (float)(_m->stat.extent * 2.0);
        _target = new Vector3((float)_m->stat.center[0], (float)_m->stat.center[1], (float)_m->stat.center[2]);
        mj_forward(_m, _d);
        BuildMeshes();
        Console.WriteLine($"{path}: nq={_m->nq} nv={_m->nv} nu={_m->nu} ngeom={_m->ngeom} nmesh={_m->nmesh} timestep={_m->opt.timestep}");
    }

    // One flat-shaded, non-indexed vertex buffer per MuJoCo mesh (vertices are in the geom frame).
    static void BuildMeshes()
    {
        int n = (int)_m->nmesh;
        _meshBuffers = new sg_buffer[n];
        _meshVertexCount = new int[n];
        for (int mesh = 0; mesh < n; mesh++)
        {
            float* vert = _m->mesh_vert + 3 * _m->mesh_vertadr[mesh];
            int* face = _m->mesh_face + 3 * _m->mesh_faceadr[mesh];
            int nface = _m->mesh_facenum[mesh];
            var verts = new sshape_vertex_t[3 * nface];
            for (int f = 0; f < nface; f++)
            {
                Vector3 a = V(vert, face[3 * f]), b = V(vert, face[3 * f + 1]), c = V(vert, face[3 * f + 2]);
                uint normal = PackNormal(Vector3.Normalize(Vector3.Cross(b - a, c - a)));
                verts[3 * f] = new sshape_vertex_t { x = a.X, y = a.Y, z = a.Z, normal = normal };
                verts[3 * f + 1] = new sshape_vertex_t { x = b.X, y = b.Y, z = b.Z, normal = normal };
                verts[3 * f + 2] = new sshape_vertex_t { x = c.X, y = c.Y, z = c.Z, normal = normal };
            }
            _meshVertexCount[mesh] = verts.Length;
            if (verts.Length == 0)
                continue;
            fixed (sshape_vertex_t* p = verts)
                _meshBuffers[mesh] = sg_make_buffer(new sg_buffer_desc
                {
                    data = new sg_range { ptr = p, size = (nuint)(verts.Length * sizeof(sshape_vertex_t)) },
                });
        }

        static Vector3 V(float* v, int i) => new(v[3 * i], v[3 * i + 1], v[3 * i + 2]);
        static uint PackNormal(Vector3 n) =>
            (uint)(byte)(sbyte)(n.X * 127) | (uint)(byte)(sbyte)(n.Y * 127) << 8 | (uint)(byte)(sbyte)(n.Z * 127) << 16;
    }

    static void FreeModel()
    {
        foreach (var b in _meshBuffers)
            if (b.id != 0)
                sg_destroy_buffer(b);
        _meshBuffers = [];
        _meshVertexCount = [];
        if (_d != null) { mj_deleteData(_d); _d = null; }
        if (_m != null) { mj_deleteModel(_m); _m = null; }
    }

    static void Reset(int key)
    {
        _key = key < (int)_m->nkey ? key : -1;
        if (_key >= 0)
            mj_resetDataKeyframe(_m, _d, _key);
        else
            mj_resetData(_m, _d);
        mj_forward(_m, _d);
        _wallTime = 0;
    }

    static void Step(double frameDt)
    {
        // Real time: step until the simulation has caught up with the wall clock, at most 1/15 s
        // of simulated time per frame so a slow frame never snowballs.
        _wallTime = Math.Min(_wallTime + frameDt, _d->time + 1.0 / 15);
        while (_d->time < _wallTime)
        {
            if (_randomCtrl)
                for (int i = 0; i < (int)_m->nu; i++)
                {
                    double lo = -1, hi = 1;
                    if (_m->actuator_ctrllimited[i] != 0)
                        (lo, hi) = (_m->actuator_ctrlrange[2 * i], _m->actuator_ctrlrange[2 * i + 1]);
                    _d->ctrl[i] = lo + (hi - lo) * _rng.NextDouble();
                }
            mj_step(_m, _d);
        }
    }

    [UnmanagedCallersOnly]
    static void Frame()
    {
        bool capture = false;
        if (_shotDir != null && _m != null)
        {
            // scripted shots: fixed 1/60 s per frame, capture once the sim time is reached
            var shot = ShotScript[_shot];
            _randomCtrl = _d->time >= shot.RandomFrom;
            if (_d->time + 1e-9 < shot.Time)
                Step(1.0 / 60);
            else
                capture = ScreenCapture.Request(Path.Combine(_shotDir, shot.Name + ".png"));
        }
        else if (_m != null && !_paused)
            Step(sapp_frame_duration());

        float w = sapp_widthf(), h = sapp_heightf();
        if (_m != null && _track)
        {
            // follow the first body under the world (the robot root), smoothed
            double* root = _d->xpos + 3 * Math.Min(1, (int)_m->nbody - 1);
            _target = Vector3.Lerp(_target, new Vector3((float)root[0], (float)root[1], (float)root[2]), 0.1f);
        }
        var eye = _target + _distance * new Vector3(
            MathF.Cos(_pitch) * MathF.Cos(_yaw), MathF.Cos(_pitch) * MathF.Sin(_yaw), MathF.Sin(_pitch));
        float far = _m != null ? (float)_m->stat.extent * 100 : 100;
        Matrix4x4 viewProj = Matrix4x4.CreateLookAt(eye, _target, Vector3.UnitZ)
                           * Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4, w / h, _distance * 0.01f, far);

        ScreenCapture.BeginFrame(state.pass_action); // swapchain pass, or the capture target when armed
        if (_m != null)
            DrawModel(viewProj);
        DrawOverlay(w, h);
        sdtx_draw();
        sg_end_pass();
        ScreenCapture.Commit();
        if (capture)
        {
            Console.WriteLine(ScreenCapture.LastError ?? $"wrote {ScreenCapture.LastPath}");
            if (_shotDir != null)
                NextShot();
        }
    }

    static void NextShot()
    {
        if (++_shot == ShotScript.Length)
        {
            sapp_request_quit();
            return;
        }
        if (ShotScript[_shot].Model != ShotScript[_shot - 1].Model)
            LoadModel(BuiltinModels[ShotScript[_shot].Model]);
        else if (ShotScript[_shot].Time < _d->time)
            Reset(-1);
    }

    static void DrawModel(Matrix4x4 viewProj)
    {
        var fsp = new fs_params_t { light_dir = new Vector4(Vector3.Normalize(new Vector3(-0.3f, 0.4f, -1f)), 0) };
        for (int g = 0; g < (int)_m->ngeom; g++)
        {
            if (_m->geom_group[g] >= 3)
                continue; // groups 3+ are hidden by default in MuJoCo's own viewer
            float* rgba = _m->geom_rgba + 4 * g;
            int mat = _m->geom_matid[g];
            if (mat >= 0 && rgba[0] == 0.5f && rgba[1] == 0.5f && rgba[2] == 0.5f && rgba[3] == 1f)
                rgba = _m->mat_rgba + 4 * mat; // default geom colour defers to the material
            if (rgba[3] == 0)
                continue;
            fsp.color = new Vector4(rgba[0], rgba[1], rgba[2], 1);

            double* p = _d->geom_xpos + 3 * g;
            double* r = _d->geom_xmat + 9 * g; // row-major R, world = R * local
            var pose = new Matrix4x4(
                (float)r[0], (float)r[3], (float)r[6], 0,
                (float)r[1], (float)r[4], (float)r[7], 0,
                (float)r[2], (float)r[5], (float)r[8], 0,
                (float)p[0], (float)p[1], (float)p[2], 1);
            double* s = _m->geom_size + 3 * g;
            float s0 = (float)s[0], s1 = (float)s[1], s2 = (float)s[2];

            switch ((mjtGeom)_m->geom_type[g])
            {
                case mjtGeom.mjGEOM_PLANE:
                    // size 0 = infinite plane; draw a large finite one
                    float ext = (float)_m->stat.extent * 20;
                    Draw(state.plane, Matrix4x4.CreateScale(s0 > 0 ? 2 * s0 : ext, s1 > 0 ? 2 * s1 : ext, 1) * pose, viewProj, fsp);
                    break;
                case mjtGeom.mjGEOM_SPHERE:
                    Draw(state.sphere, Matrix4x4.CreateScale(s0) * pose, viewProj, fsp);
                    break;
                case mjtGeom.mjGEOM_ELLIPSOID:
                    Draw(state.sphere, Matrix4x4.CreateScale(s0, s1, s2) * pose, viewProj, fsp);
                    break;
                case mjtGeom.mjGEOM_BOX:
                    Draw(state.box, Matrix4x4.CreateScale(2 * s0, 2 * s1, 2 * s2) * pose, viewProj, fsp);
                    break;
                case mjtGeom.mjGEOM_CYLINDER:
                    Draw(state.cylinder, Matrix4x4.CreateScale(s0, s0, 2 * s1) * pose, viewProj, fsp);
                    break;
                case mjtGeom.mjGEOM_CAPSULE:
                    Draw(state.cylinder, Matrix4x4.CreateScale(s0, s0, 2 * s1) * pose, viewProj, fsp);
                    Draw(state.sphere, Matrix4x4.CreateScale(s0) * Matrix4x4.CreateTranslation(0, 0, s1) * pose, viewProj, fsp);
                    Draw(state.sphere, Matrix4x4.CreateScale(s0) * Matrix4x4.CreateTranslation(0, 0, -s1) * pose, viewProj, fsp);
                    break;
                case mjtGeom.mjGEOM_MESH:
                    int mesh = _m->geom_dataid[g];
                    if (mesh >= 0 && _meshVertexCount[mesh] > 0)
                        DrawMesh(mesh, pose, viewProj, fsp);
                    break;
                // hfield and SDF geoms are not drawn
            }
        }

        // contact points: small red spheres
        fsp.color = new Vector4(0.9f, 0.15f, 0.1f, 1);
        float cr = (float)_m->stat.meansize * 0.15f;
        for (int i = 0; i < _d->ncon; i++)
        {
            double* cp = _d->contact[i].pos;
            Draw(state.sphere, Matrix4x4.CreateScale(cr) * Matrix4x4.CreateTranslation((float)cp[0], (float)cp[1], (float)cp[2]), viewProj, fsp);
        }
    }

    static void ApplyUniforms(Matrix4x4 model, Matrix4x4 viewProj, ref fs_params_t fsp)
    {
        Matrix4x4.Invert(model, out var inv);
        var vsp = new vs_params_t { mvp = model * viewProj, normal_mat = Matrix4x4.Transpose(inv) };
        sg_apply_uniforms(UB_vs_params, SG_RANGE(ref vsp));
        sg_apply_uniforms(UB_fs_params, SG_RANGE(ref fsp));
    }

    static void Draw(sshape_element_range_t range, Matrix4x4 model, Matrix4x4 viewProj, fs_params_t fsp)
    {
        sg_apply_pipeline(state.pip);
        sg_apply_bindings(state.bind);
        ApplyUniforms(model, viewProj, ref fsp);
        sg_draw(range.base_element, range.num_elements, 1);
    }

    static void DrawMesh(int mesh, Matrix4x4 model, Matrix4x4 viewProj, fs_params_t fsp)
    {
        sg_apply_pipeline(state.mesh_pip);
        var bind = new sg_bindings();
        bind.vertex_buffers[0] = _meshBuffers[mesh];
        sg_apply_bindings(bind);
        ApplyUniforms(model, viewProj, ref fsp);
        sg_draw(0, (uint)_meshVertexCount[mesh], 1);
    }

    static void DrawOverlay(float w, float h)
    {
        sdtx_canvas(w * 0.5f, h * 0.5f);
        sdtx_origin(1, 1);
        sdtx_color3f(1, 1, 1);
        sdtx_puts($"MuJoCo {MjHelpers.Version}  {Path.GetFileName(_modelPath)}\n");
        if (_m == null)
        {
            sdtx_color3f(1, 0.4f, 0.4f);
            sdtx_puts($"load failed: {_loadError}\n");
            return;
        }
        string key = _key >= 0 ? MjHelpers.Name(_m, mjtObj.mjOBJ_KEY, _key) ?? $"#{_key}" : "qpos0";
        sdtx_puts($"time {_d->time,8:F3} s  contacts {_d->ncon,3}  reset {key}\n");
        sdtx_puts($"{(_paused ? "PAUSED" : "running")}  ctrl {(_randomCtrl ? "random" : "zero")}  track {(_track ? "on" : "off")}\n");
        if (_m->nq <= 8)
            sdtx_puts($"qpos {string.Join(" ", MjHelpers.Span(_d->qpos, _m->nq).ToArray().Select(q => q.ToString("F3")))}\n");
        sdtx_puts("\n");
        sdtx_color3f(0.7f, 0.7f, 0.7f);
        sdtx_puts("1-3 model  space pause  S step  R reset\nK next keyframe  C random ctrl  T track\nP screenshot  drag orbit  wheel zoom\n");
    }

    [UnmanagedCallersOnly]
    static void Event(sapp_event* e)
    {
        switch (e->type)
        {
            case sapp_event_type.SAPP_EVENTTYPE_MOUSE_DOWN:
                _dragging = true;
                break;
            case sapp_event_type.SAPP_EVENTTYPE_MOUSE_UP:
                _dragging = false;
                break;
            case sapp_event_type.SAPP_EVENTTYPE_MOUSE_MOVE:
                if (_dragging)
                {
                    _yaw -= e->mouse_dx * 0.01f;
                    _pitch = Math.Clamp(_pitch + e->mouse_dy * 0.01f, -1.5f, 1.5f);
                }
                break;
            case sapp_event_type.SAPP_EVENTTYPE_MOUSE_SCROLL:
                _distance = Math.Clamp(_distance * MathF.Pow(1.1f, -e->scroll_y), 0.05f, 500f);
                break;
            case sapp_event_type.SAPP_EVENTTYPE_KEY_DOWN:
                OnKey(e->key_code);
                break;
        }
    }

    static void OnKey(sapp_keycode key)
    {
        switch (key)
        {
            case sapp_keycode.SAPP_KEYCODE_1: LoadModel(BuiltinModels[0]); return;
            case sapp_keycode.SAPP_KEYCODE_2: LoadModel(BuiltinModels[1]); return;
            case sapp_keycode.SAPP_KEYCODE_3: LoadModel(BuiltinModels[2]); return;
            case sapp_keycode.SAPP_KEYCODE_ESCAPE: sapp_request_quit(); return;
        }
        if (_m == null)
            return;
        switch (key)
        {
            case sapp_keycode.SAPP_KEYCODE_SPACE: _paused = !_paused; break;
            case sapp_keycode.SAPP_KEYCODE_S: if (_paused) mj_step(_m, _d); break;
            case sapp_keycode.SAPP_KEYCODE_R: Reset(-1); break;
            case sapp_keycode.SAPP_KEYCODE_K: Reset(_key + 1); break;
            case sapp_keycode.SAPP_KEYCODE_C:
                _randomCtrl = !_randomCtrl;
                if (!_randomCtrl)
                    MjHelpers.Span(_d->ctrl, _m->nu).Clear();
                break;
            case sapp_keycode.SAPP_KEYCODE_T: _track = !_track; break;
            case sapp_keycode.SAPP_KEYCODE_P:
                string shot = Path.Combine(AppContext.BaseDirectory, "screenshots", $"{DateTime.Now:yyyyMMdd_HHmmss}.png");
                Directory.CreateDirectory(Path.GetDirectoryName(shot)!);
                Console.WriteLine(ScreenCapture.Request(shot) ? $"screenshot: {shot}" : ScreenCapture.LastError);
                break;
        }
    }

    [UnmanagedCallersOnly]
    static void Cleanup()
    {
        FreeModel();
        ScreenCapture.Shutdown();
        sdtx_shutdown();
        sg_shutdown();
    }

    public static sapp_desc sokol_main() => new()
    {
        init_cb = &Init,
        frame_cb = &Frame,
        event_cb = &Event,
        cleanup_cb = &Cleanup,
        width = 1280,
        height = 800,
        sample_count = 4,
        window_title = "MuJoCo viewer (sokol-app)",
        icon = { sokol_default = true },
        logger = { func = &slog_func },
    };
}
