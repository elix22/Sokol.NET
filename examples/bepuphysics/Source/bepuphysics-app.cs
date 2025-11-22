using System;
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
using static Sokol.SImgui;
using static Sokol.SG.sg_vertex_step;
using Imgui;
using static Imgui.ImguiNative;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.Constraints;
using BepuUtilities;
using BepuUtilities.Memory;
using static physics_demo_shader_cs.Shaders;

public static unsafe class BepuphysicsApp
{
    const int START_AMOUNT = 5000;
    const int MAX_INSTANCES = 512 * 1024;

    [StructLayout(LayoutKind.Sequential)]
    struct Vertex
    {
        public Vector3 position;
        public Vector3 normal;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct InstanceData
    {
        public Matrix4x4 model;
        public Vector3 color;
    }

    struct PhysicsBody
    {
        public BodyHandle handle;
        public Vector3 color;
        public bool isSphere;
    }

    struct _state
    {
        public sg_pass_action pass_action;
        public sg_pipeline pip_smooth;  // For spheres with smooth shading
        public sg_bindings cube_bind;
        public sg_bindings sphere_bind;

        public BufferPool bufferPool;
        public ThreadDispatcher threadDispatcher;
        public Simulation simulation;
        public Camera camera;

        public List<PhysicsBody> bodies;
        public float spawnTimer;
        public Random random;

        public int cubeCount;
        public int sphereCount;
        public bool showStats;

        // Instance data for GPU instancing
        public InstanceData[] cubeInstances;
        public InstanceData[] sphereInstances;

        // Shared shape indices for better performance
        public TypedIndex cubeShapeIndex;
        public TypedIndex sphereShapeIndex;
    }

    static _state state = new _state();


    [UnmanagedCallersOnly]
    private static unsafe void Init()
    {
        sg_setup(new sg_desc()
        {
            environment = sglue_environment(),
            shader_pool_size = 64,
            buffer_pool_size = 4096 * 2,//increased to handle very large scene graphs
            sampler_pool_size = 512, // Reduced from 2048 - texture cache prevents duplicate samplers
            view_pool_size = 512, // Increased to handle many texture views (each texture needs a view)
            uniform_buffer_size = 64 * 1024 * 1024, // 64 MB - increased to handle very large scene graphs (2500+ nodes)
            logger = {
                func = &slog_func,
            }
        });

        state.pass_action = default;
        state.pass_action.colors[0].load_action = sg_load_action.SG_LOADACTION_CLEAR;
        state.pass_action.colors[0].clear_value = new sg_color { r = 0.1f, g = 0.1f, b = 0.15f, a = 1.0f };

        // Initialize BepuPhysics with multi-threading
        state.bufferPool = new BufferPool();

        // Create thread dispatcher using CPU threads minus 1 for main thread
        int threadCount = Math.Max(1, Environment.ProcessorCount - 1);
        state.threadDispatcher = new ThreadDispatcher(threadCount);

        // Create simulation with balanced solver iterations for stability with many objects
        // 6 velocity iterations, 2 substeps provides stable contacts without excessive overhead
        state.simulation = Simulation.Create(state.bufferPool, new NarrowPhaseCallbacks(),
            new PoseIntegratorCallbacks(new Vector3(0, -10, 0)), new SolveDescription(6, 2));

        // Create ground plane
        var groundShape = new Box(50, 5, 50);
        var groundShapeIndex = state.simulation.Shapes.Add(groundShape);
        var groundPose = new RigidPose(new Vector3(0, -2.5f, 0), Quaternion.Identity);
        state.simulation.Statics.Add(new StaticDescription(groundPose, groundShapeIndex));

        // Initialize state
        state.bodies = new List<PhysicsBody>();
        state.random = new Random();
        state.spawnTimer = 0;

        // Initialize instance arrays
        state.cubeInstances = new InstanceData[MAX_INSTANCES];
        state.sphereInstances = new InstanceData[MAX_INSTANCES];

        // Create shared shapes once for all bodies (much more efficient)
        var cubeShape = new Box(1, 1, 1);
        state.cubeShapeIndex = state.simulation.Shapes.Add(cubeShape);
        var sphereShape = new Sphere(0.5f);
        state.sphereShapeIndex = state.simulation.Shapes.Add(sphereShape);

        // Create some initial objects
        for (int i = 0; i < START_AMOUNT; i++)
        {
            SpawnCube(new Vector3(state.random.NextSingle() * 4 - 2, 10 + i * 2, state.random.NextSingle() * 4 - 2));
        }
        for (int i = 0; i < START_AMOUNT; i++)
        {
            SpawnSphere(new Vector3(state.random.NextSingle() * 4 - 2, 15 + i * 2, state.random.NextSingle() * 4 - 2));
        }

        // Create cube mesh
        CreateCubeMesh();

        // Create sphere mesh
        CreateSphereMesh();

        // Create shaders and pipelines
        var shd_smooth = sg_make_shader(physics_demo_smooth_shader_desc(sg_query_backend()));

        var pip_desc = default(sg_pipeline_desc);
        pip_desc.shader = shd_smooth;
        // Geometry attributes from buffer 0
        pip_desc.layout.attrs[ATTR_physics_demo_smooth_position] = new sg_vertex_attr_state() { format = SG_VERTEXFORMAT_FLOAT3, buffer_index = 0 };
        pip_desc.layout.attrs[ATTR_physics_demo_smooth_normal] = new sg_vertex_attr_state() { format = SG_VERTEXFORMAT_FLOAT3, buffer_index = 0 };
        // Instance data in buffer slot 1
        pip_desc.layout.buffers[1].step_func = SG_VERTEXSTEP_PER_INSTANCE;
        pip_desc.layout.buffers[1].stride = sizeof(InstanceData);
        pip_desc.layout.attrs[ATTR_physics_demo_smooth_inst_model_0] = new sg_vertex_attr_state() { format = SG_VERTEXFORMAT_FLOAT4, buffer_index = 1, offset = 0 };
        pip_desc.layout.attrs[ATTR_physics_demo_smooth_inst_model_1] = new sg_vertex_attr_state() { format = SG_VERTEXFORMAT_FLOAT4, buffer_index = 1, offset = 16 };
        pip_desc.layout.attrs[ATTR_physics_demo_smooth_inst_model_2] = new sg_vertex_attr_state() { format = SG_VERTEXFORMAT_FLOAT4, buffer_index = 1, offset = 32 };
        pip_desc.layout.attrs[ATTR_physics_demo_smooth_inst_model_3] = new sg_vertex_attr_state() { format = SG_VERTEXFORMAT_FLOAT4, buffer_index = 1, offset = 48 };
        pip_desc.layout.attrs[ATTR_physics_demo_smooth_inst_color] = new sg_vertex_attr_state() { format = SG_VERTEXFORMAT_FLOAT3, buffer_index = 1, offset = 64 };
        pip_desc.index_type = SG_INDEXTYPE_UINT16;
        pip_desc.cull_mode = SG_CULLMODE_BACK;
        pip_desc.face_winding = sg_face_winding.SG_FACEWINDING_CCW;
        pip_desc.depth.compare = SG_COMPAREFUNC_LESS_EQUAL;
        pip_desc.depth.write_enabled = true;
        state.pip_smooth = sg_make_pipeline(pip_desc);

        // Initialize camera
        state.camera = new Camera();
        state.camera.Init(new CameraDesc
        {
            Distance = 50,
            Latitude = 25,
            Longitude = 45,
            Center = new Vector3(0, 5, 0),
            Aspect = 60,
            NearZ = 0.1f,
            FarZ = 100.0f
        });
        state.camera.MoveSpeed = 10;
        state.camera.MouseSensitivity = 0.3f;

        // Initialize ImGui
        simgui_setup(new simgui_desc_t());

        // Initialize stats
        state.showStats = true;
        state.cubeCount = START_AMOUNT;
        state.sphereCount = START_AMOUNT;
    }


    [UnmanagedCallersOnly]
    private static unsafe void Frame()
    {
        float deltaTime = (float)sapp_frame_duration();
        int width = sapp_width();
        int height = sapp_height();

        // Update camera
        state.camera.Update(width, height, deltaTime);

        // Step physics simulation with multi-threading
        state.simulation.Timestep(deltaTime, state.threadDispatcher);

        // Render
        sg_begin_pass(new sg_pass { action = state.pass_action, swapchain = sglue_swapchain() });

        // Prepare instance data for all bodies
        int cubeInstanceCount = 0;
        int sphereInstanceCount = 0;

        // Add ground plane as a cube instance
        var groundModel = Matrix4x4.CreateScale(new Vector3(50, 5, 50)) *
                         Matrix4x4.CreateTranslation(new Vector3(0, -2.5f, 0));
        state.cubeInstances[cubeInstanceCount++] = new InstanceData
        {
            model = groundModel,
            color = new Vector3(0.9f, 0.7f, 0.3f)
        };

        // Gather all cube and sphere instances
        foreach (var body in state.bodies)
        {
            var bodyReference = state.simulation.Bodies.GetBodyReference(body.handle);
            var pose = bodyReference.Pose;

            var rotation = Matrix4x4.CreateFromQuaternion(pose.Orientation);
            var translation = Matrix4x4.CreateTranslation(pose.Position);
            var model = rotation * translation;

            if (body.isSphere)
            {
                if (sphereInstanceCount < MAX_INSTANCES)
                {
                    state.sphereInstances[sphereInstanceCount++] = new InstanceData
                    {
                        model = model,
                        color = body.color
                    };
                }
            }
            else
            {
                if (cubeInstanceCount < MAX_INSTANCES)
                {
                    state.cubeInstances[cubeInstanceCount++] = new InstanceData
                    {
                        model = model,
                        color = body.color
                    };
                }
            }
        }

        // Render all cubes with instancing (flat shading)
        if (cubeInstanceCount > 0)
        {
            sg_apply_pipeline(state.pip_smooth);
            RenderCubesInstanced(cubeInstanceCount);
        }

        // Render all spheres with instancing (smooth shading)
        if (sphereInstanceCount > 0)
        {
            sg_apply_pipeline(state.pip_smooth);
            RenderSpheresInstanced(sphereInstanceCount);
        }

        // Render ImGui
        DrawStatsWindow();
        simgui_render();


        sg_end_pass();
        sg_commit();
    }


    [UnmanagedCallersOnly]
    private static unsafe void Event(sapp_event* e)
    {
        if (simgui_handle_event(*e))
            return;

        state.camera.HandleEvent(e);
    }

    [UnmanagedCallersOnly]
    static void Cleanup()
    {
        simgui_shutdown();
        state.simulation.Dispose();
        state.threadDispatcher?.Dispose();
        state.bufferPool.Clear();

        sg_shutdown();

        // Force a complete shutdown if debugging
        if (Debugger.IsAttached)
        {
            Environment.Exit(0);
        }
    }

    static void SpawnCube(Vector3 position)
    {
        var cubeShape = new Box(1, 1, 1);
        var cubeInertia = cubeShape.ComputeInertia(1);

        // Use shared shape index instead of creating new one
        var bodyDescription = BodyDescription.CreateDynamic(
            new RigidPose(position, Quaternion.Identity),
            cubeInertia,
            state.cubeShapeIndex,
            0.01f);

        var handle = state.simulation.Bodies.Add(bodyDescription);

        state.bodies.Add(new PhysicsBody
        {
            handle = handle,
            color = new Vector3(state.random.NextSingle() * 0.5f + 0.5f,
                               state.random.NextSingle() * 0.5f + 0.5f,
                               state.random.NextSingle() * 0.5f + 0.5f),
            isSphere = false
        });

        state.cubeCount++;
    }

    static void SpawnSphere(Vector3 position)
    {
        var sphereShape = new Sphere(0.5f);
        var sphereInertia = sphereShape.ComputeInertia(1);

        // Use shared shape index instead of creating new one
        var bodyDescription = BodyDescription.CreateDynamic(
            new RigidPose(position, Quaternion.Identity),
            sphereInertia,
            state.sphereShapeIndex,
            0.01f);

        var handle = state.simulation.Bodies.Add(bodyDescription);
        
        // Add random angular velocity for spinning/bumpiness
        ref var bodyVelocity = ref state.simulation.Bodies.GetBodyReference(handle).Velocity;
        bodyVelocity.Angular = new Vector3(
            (state.random.NextSingle() - 0.5f) * 4f,
            (state.random.NextSingle() - 0.5f) * 4f,
            (state.random.NextSingle() - 0.5f) * 4f);
        
        state.bodies.Add(new PhysicsBody
        {
            handle = handle,
            color = new Vector3(state.random.NextSingle() * 0.5f + 0.5f, 
                               state.random.NextSingle() * 0.5f + 0.5f, 
                               state.random.NextSingle() * 0.5f + 0.5f),
            isSphere = true
        });
        
        state.sphereCount++;
    }

    static unsafe void CreateCubeMesh()
    {
        // Cube vertices with normals (24 vertices, 6 faces)
        Vertex[] vertices = new Vertex[24];

        // Front face (Z+)
        vertices[0] = new Vertex { position = new Vector3(-0.5f, -0.5f, 0.5f), normal = new Vector3(0, 0, 1) };
        vertices[1] = new Vertex { position = new Vector3(0.5f, -0.5f, 0.5f), normal = new Vector3(0, 0, 1) };
        vertices[2] = new Vertex { position = new Vector3(0.5f, 0.5f, 0.5f), normal = new Vector3(0, 0, 1) };
        vertices[3] = new Vertex { position = new Vector3(-0.5f, 0.5f, 0.5f), normal = new Vector3(0, 0, 1) };

        // Back face (Z-)
        vertices[4] = new Vertex { position = new Vector3(0.5f, -0.5f, -0.5f), normal = new Vector3(0, 0, -1) };
        vertices[5] = new Vertex { position = new Vector3(-0.5f, -0.5f, -0.5f), normal = new Vector3(0, 0, -1) };
        vertices[6] = new Vertex { position = new Vector3(-0.5f, 0.5f, -0.5f), normal = new Vector3(0, 0, -1) };
        vertices[7] = new Vertex { position = new Vector3(0.5f, 0.5f, -0.5f), normal = new Vector3(0, 0, -1) };

        // Top face (Y+)
        vertices[8] = new Vertex { position = new Vector3(-0.5f, 0.5f, 0.5f), normal = new Vector3(0, 1, 0) };
        vertices[9] = new Vertex { position = new Vector3(0.5f, 0.5f, 0.5f), normal = new Vector3(0, 1, 0) };
        vertices[10] = new Vertex { position = new Vector3(0.5f, 0.5f, -0.5f), normal = new Vector3(0, 1, 0) };
        vertices[11] = new Vertex { position = new Vector3(-0.5f, 0.5f, -0.5f), normal = new Vector3(0, 1, 0) };

        // Bottom face (Y-)
        vertices[12] = new Vertex { position = new Vector3(-0.5f, -0.5f, -0.5f), normal = new Vector3(0, -1, 0) };
        vertices[13] = new Vertex { position = new Vector3(0.5f, -0.5f, -0.5f), normal = new Vector3(0, -1, 0) };
        vertices[14] = new Vertex { position = new Vector3(0.5f, -0.5f, 0.5f), normal = new Vector3(0, -1, 0) };
        vertices[15] = new Vertex { position = new Vector3(-0.5f, -0.5f, 0.5f), normal = new Vector3(0, -1, 0) };

        // Right face (X+)
        vertices[16] = new Vertex { position = new Vector3(0.5f, -0.5f, 0.5f), normal = new Vector3(1, 0, 0) };
        vertices[17] = new Vertex { position = new Vector3(0.5f, -0.5f, -0.5f), normal = new Vector3(1, 0, 0) };
        vertices[18] = new Vertex { position = new Vector3(0.5f, 0.5f, -0.5f), normal = new Vector3(1, 0, 0) };
        vertices[19] = new Vertex { position = new Vector3(0.5f, 0.5f, 0.5f), normal = new Vector3(1, 0, 0) };

        // Left face (X-)
        vertices[20] = new Vertex { position = new Vector3(-0.5f, -0.5f, -0.5f), normal = new Vector3(-1, 0, 0) };
        vertices[21] = new Vertex { position = new Vector3(-0.5f, -0.5f, 0.5f), normal = new Vector3(-1, 0, 0) };
        vertices[22] = new Vertex { position = new Vector3(-0.5f, 0.5f, 0.5f), normal = new Vector3(-1, 0, 0) };
        vertices[23] = new Vertex { position = new Vector3(-0.5f, 0.5f, -0.5f), normal = new Vector3(-1, 0, 0) };

        ushort[] indices = new ushort[36]
        {
            0, 1, 2,  0, 2, 3,    // Front
            4, 5, 6,  4, 6, 7,    // Back
            8, 9, 10, 8, 10, 11,  // Top
            12, 13, 14, 12, 14, 15, // Bottom
            16, 17, 18, 16, 18, 19, // Right
            20, 21, 22, 20, 22, 23  // Left
        };

        state.cube_bind.vertex_buffers[0] = sg_make_buffer(new sg_buffer_desc
        {
            data = SG_RANGE<Vertex>(vertices)
        });

        state.cube_bind.index_buffer = sg_make_buffer(new sg_buffer_desc
        {
            usage = new sg_buffer_usage { index_buffer = true },
            data = SG_RANGE(indices)
        });

        // Create instance buffer for cubes
        state.cube_bind.vertex_buffers[1] = sg_make_buffer(new sg_buffer_desc
        {
            size = (nuint)(MAX_INSTANCES * sizeof(InstanceData)),
            usage = new sg_buffer_usage { stream_update = true },
            label = "cube-instances"
        });

    }

    static unsafe void CreateSphereMesh()
    {
        // Create sphere with subdivision
        int segments = 16;
        int rings = 8;
        List<Vertex> vertices = new List<Vertex>();
        List<ushort> indices = new List<ushort>();

        // Generate vertices
        for (int ring = 0; ring <= rings; ring++)
        {
            float phi = MathF.PI * ring / rings;
            for (int seg = 0; seg <= segments; seg++)
            {
                float theta = 2.0f * MathF.PI * seg / segments;

                float x = MathF.Sin(phi) * MathF.Cos(theta);
                float y = MathF.Cos(phi);
                float z = MathF.Sin(phi) * MathF.Sin(theta);

                vertices.Add(new Vertex
                {
                    position = new Vector3(x * 0.5f, y * 0.5f, z * 0.5f),
                    normal = new Vector3(x, y, z)
                });
            }
        }

        // Generate indices (CCW winding when viewed from outside)
        for (int ring = 0; ring < rings; ring++)
        {
            for (int seg = 0; seg < segments; seg++)
            {
                int current = ring * (segments + 1) + seg;
                int next = current + segments + 1;

                // First triangle (CCW from outside)
                indices.Add((ushort)current);
                indices.Add((ushort)(current + 1));
                indices.Add((ushort)next);

                // Second triangle (CCW from outside)
                indices.Add((ushort)(current + 1));
                indices.Add((ushort)(next + 1));
                indices.Add((ushort)next);
            }
        }


        state.sphere_bind.vertex_buffers[0] = sg_make_buffer(new sg_buffer_desc
        {
            data = SG_RANGE<Vertex>(vertices.ToArray())
        });

        state.sphere_bind.index_buffer = sg_make_buffer(new sg_buffer_desc
        {
            usage = new sg_buffer_usage { index_buffer = true },
            data = SG_RANGE<ushort>(indices.ToArray())
        });

        // Create instance buffer for spheres
        state.sphere_bind.vertex_buffers[1] = sg_make_buffer(new sg_buffer_desc
        {
            size = (nuint)(MAX_INSTANCES * sizeof(InstanceData)),
            usage = new sg_buffer_usage { stream_update = true },
            label = "sphere-instances"
        });

    }

    static unsafe void RenderCubesInstanced(int instanceCount)
    {
        fixed (InstanceData* instancePtr = state.cubeInstances)
        {
            sg_update_buffer(state.cube_bind.vertex_buffers[1], new sg_range
            {
                ptr = instancePtr,
                size = (nuint)(instanceCount * sizeof(InstanceData))
            });
        }

        var vs_params = new vs_params_t { vp = state.camera.ViewProj };
        var fs_params = new fs_params_t
        {
            light_dir = Vector3.Normalize(new Vector3(0.5f, 1, 0.3f)),
            view_pos = state.camera.EyePos
        };

        sg_apply_bindings(state.cube_bind);
        sg_apply_uniforms(UB_vs_params, SG_RANGE<vs_params_t>(ref vs_params));
        sg_apply_uniforms(UB_fs_params, SG_RANGE<fs_params_t>(ref fs_params));
        sg_draw(0, 36, (uint)instanceCount);
    }

    static unsafe void RenderSpheresInstanced(int instanceCount)
    {
        fixed (InstanceData* instancePtr = state.sphereInstances)
        {
            sg_update_buffer(state.sphere_bind.vertex_buffers[1], new sg_range
            {
                ptr = instancePtr,
                size = (nuint)(instanceCount * sizeof(InstanceData))
            });
        }

        var vs_params = new vs_params_t { vp = state.camera.ViewProj };
        var fs_params = new fs_params_t
        {
            light_dir = Vector3.Normalize(new Vector3(0.5f, 1, 0.3f)),
            view_pos = state.camera.EyePos
        };

        sg_apply_bindings(state.sphere_bind);
        sg_apply_uniforms(UB_vs_params, SG_RANGE<vs_params_t>(ref vs_params));
        sg_apply_uniforms(UB_fs_params, SG_RANGE<fs_params_t>(ref fs_params));

        int segments = 16;
        int rings = 8;
        uint indexCount = (uint)(rings * segments * 6);
        sg_draw(0, indexCount, (uint)instanceCount);
    }

    static void DrawStatsWindow()
    {
        if (!state.showStats)
            return;

        simgui_new_frame(new simgui_frame_desc_t
        {
            width = sapp_width(),
            height = sapp_height(),
            delta_time = (float)sapp_frame_duration(),
            dpi_scale = 1// doesn;t look god on Android sapp_dpi_scale()
        });

        igSetNextWindowSize(new Vector2(250, 250), ImGuiCond.Once);
        igSetNextWindowPos(new Vector2(30, 30), ImGuiCond.Once, Vector2.Zero);
        byte open = 1;
        if (igBegin("Statistics", ref open, ImGuiWindowFlags.None))
        {

            // FPS
            float fps = 1.0f / (float)sapp_frame_duration();
            igText($"FPS: {fps:F1}");
            igText($"Frame Time: {sapp_frame_duration() * 1000:F2} ms");

            igSeparator();

            // Rendering stats
            int drawCalls = (state.cubeCount > 0 ? 1 : 0) + (state.sphereCount > 0 ? 1 : 0);
            igText($"Draw Calls: {drawCalls}");
            igText($"Instanced: Yes");

            igSeparator();

            // Object counts
            igText($"Total Bodies: {state.bodies.Count}");
            igText($"Cubes: {state.cubeCount}");
            igText($"Spheres: {state.sphereCount}");

            igSeparator();

            // Physics info
            igText($"Static Bodies: 1 (Ground)");
            igText($"Active Bodies: {state.simulation.Bodies.ActiveSet.Count}");
            int threadCount = state.threadDispatcher?.ThreadCount ?? 1;
            igText($"Physics Threads: {threadCount}");

            igSeparator();

            // Camera info
            Vector3 camPos = state.camera.EyePos;
            igText($"Camera: ({camPos.X:F1}, {camPos.Y:F1}, {camPos.Z:F1})");
        }
        igEnd();
    }

    struct NarrowPhaseCallbacks : INarrowPhaseCallbacks
    {
        public void Initialize(Simulation simulation) { }
        public bool AllowContactGeneration(int workerIndex, CollidableReference a, CollidableReference b, ref float speculativeMargin) => true;
        public bool AllowContactGeneration(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB) => true;
        public bool ConfigureContactManifold<TManifold>(int workerIndex, CollidablePair pair, ref TManifold manifold, out PairMaterialProperties pairMaterial) where TManifold : unmanaged, IContactManifold<TManifold>
        {
            pairMaterial.FrictionCoefficient = 0.2f;  // Low friction reduces sliding
            pairMaterial.MaximumRecoveryVelocity = 3f;  // Increased to allow bouncing
            pairMaterial.SpringSettings = new SpringSettings(25, 1);  // Higher frequency for responsive bounce
            return true;
        }
        public bool ConfigureContactManifold(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB, ref ConvexContactManifold manifold) => true;
        public void Dispose() { }
    }

    struct PoseIntegratorCallbacks : IPoseIntegratorCallbacks
    {
        public Vector3 Gravity;
        public PoseIntegratorCallbacks(Vector3 gravity) => Gravity = gravity;
        public readonly AngularIntegrationMode AngularIntegrationMode => AngularIntegrationMode.Nonconserving;
        public readonly bool AllowSubstepsForUnconstrainedBodies => false;
        public readonly bool IntegrateVelocityForKinematics => false;
        public void Initialize(Simulation simulation) { }
        public void PrepareForIntegration(float dt) { }
        public void IntegrateVelocity(Vector<int> bodyIndices, Vector3Wide position, QuaternionWide orientation, BodyInertiaWide localInertia, Vector<int> integrationMask, int workerIndex, Vector<float> dt, ref BodyVelocityWide velocity)
        {
            velocity.Linear.Y += Gravity.Y * dt;
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
            width = 1024,
            height = 768,
            sample_count = 4,
            window_title = "BepuPhysics Demo (Sokol.NET)",
            icon = { sokol_default = true },
            logger = {
                func = &slog_func,
            }
        };
    }

}
