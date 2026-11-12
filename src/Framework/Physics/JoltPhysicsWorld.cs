using System;
using System.Collections.Generic;
using System.Numerics;

namespace GameEditor.Framework.Physics
{
    public sealed class JoltPhysicsWorld : IPhysicsWorld
    {
        const ushort ObjLayerNonMoving = 0;
        const ushort ObjLayerMoving    = 1;

        JPH.TempAllocatorImpl?                          _tempAlloc;
        JPH.JobSystemThreadPool?                       _jobSystem;
        JPH.PhysicsSystem?                             _physics;
        JPH.BodyInterface?                             _bodyInterface;
        JPH.ContactListenerTrampolineManaged?          _contactListener;
        ICollisionListener?                            _gameListener;

        // These must outlive _physics: the native PhysicsSystem stores raw pointers to them.
        JPH.BroadPhaseLayerInterfaceTable?             _bpInterface;
        JPH.ObjectLayerPairFilterTable?                _pairFilter;
        JPH.ObjectVsBroadPhaseLayerFilterTable?        _objVsBP;

        int _nextHandle = 1;
        readonly Dictionary<int, JPH.BodyID>  _handleToBodyId  = new();
        readonly Dictionary<int, JPH.Body>    _handleToBody    = new();
        readonly Dictionary<uint, int>         _packedToHandle  = new();
        readonly Dictionary<uint, ushort>      _packedToLayer   = new();
        readonly Dictionary<uint, ushort>      _packedToMask    = new();
        readonly HashSet<uint>                 _sensors         = new();

        int _nextConstraintHandle = 1;
        readonly Dictionary<int, JPH.TwoBodyConstraint> _constraintMap = new();

        // ── Character controllers ─────────────────────────────────────────────
        JPH.TempAllocatorImpl? _charTempAlloc;
        int _nextCharHandle = 1;
        readonly Dictionary<int, CharState>    _charStates    = new();  // virtual
        readonly Dictionary<int, KinCharState> _kinCharStates = new();  // kinematic

        // ── Vehicle controllers ───────────────────────────────────────────────
        int _nextVehicleHandle = 1;
        readonly Dictionary<int, VehicleState> _vehicleStates = new();

        private sealed class VehicleState : IDisposable
        {
            public required JPH.VehicleConstraint Constraint;
            public required JPH.Body              CarBody;
            public required JPH.BodyID            CarBodyId;
            /// <summary>Kept alive so Jolt refcount > 0 after SetVehicleCollisionTester. Dispose AFTER Constraint.</summary>
            public IDisposable?                   CollisionTester;
            public required ECS.Components.VehicleType Type;
            public int WheelCount;
            /// <summary>PhysicsBodyHandle.Value for the chassis — for external force/velocity queries.</summary>
            public int BodyHandleValue;
            /// <summary>Tracks previous forward direction so we can flip throttle sign (matches demo pattern).</summary>
            public float PreviousForward = 1f;
            private bool _disposed;

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                Constraint.Dispose();
                CollisionTester?.Dispose();
            }
        }
        private sealed class CharState : IDisposable
        {
            public required JPH.CharacterVirtual                       Character;
            public required JPH.RotatedTranslatedShape                 RotTransShape;
            public required JPH.CapsuleShape                           CapsuleShapeObj;
            public required JPH.RotatedTranslatedShape                 InnerRotTransShape;
            public required JPH.CapsuleShape                           InnerCapsuleShapeObj;
            public required JPH.CharacterVsCharacterCollisionSimple    Cvsc;
            public Vector3 DesiredVelocity;
            private bool _disposed;
            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                Cvsc.Dispose();
                Character.Dispose();
                RotTransShape.Dispose();
                CapsuleShapeObj.Dispose();
                InnerRotTransShape.Dispose();
                InnerCapsuleShapeObj.Dispose();
            }
        }

        private sealed class KinCharState : IDisposable
        {
            public required JPH.Character                 Character;
            public required JPH.RotatedTranslatedShape   RotTransShape;
            public required JPH.CapsuleShape             CapsuleShapeObj;
            public float CollisionTolerance;
            public Vector3 DesiredVelocity;
            private bool _disposed;
            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                Character.RemoveFromPhysicsSystem();
                Character.Dispose();
                RotTransShape.Dispose();
                CapsuleShapeObj.Dispose();
            }
        }

        enum EvKind { CollisionEnter, CollisionStay, CollisionExit, TriggerEnter, TriggerExit }
        readonly record struct CollEv(uint PackedA, uint PackedB, ContactPoint Contact, EvKind Kind);
        readonly List<CollEv> _pending = new();
        readonly object _pendingLock   = new object();

        float _accumulator;
        const float FixedDt = 1f / 60f;

        // Jolt global state (factory, registered types) is a process-lifetime singleton.
        // Init once on first use; never call Shutdown mid-session (mirrors JoltPhysicsDemo pattern).
        static bool _joltGlobalInitialized = false;

        // ---- IPhysicsWorld --------------------------------------------------

        public unsafe void Initialize(Vector3 gravity)
        {
            if (!_joltGlobalInitialized)
            {
                JPH.Const_JoltHelpers.Init();
                _joltGlobalInitialized = true;
            }

            _tempAlloc = new JPH.TempAllocatorImpl(64 * 1024 * 1024);
            int numThreads = Math.Max(1, Environment.ProcessorCount - 1);
            _jobSystem = new JPH.JobSystemThreadPool(2048, 8, numThreads);

            _bpInterface = new JPH.BroadPhaseLayerInterfaceTable(2, 2);
            using var bp0 = new JPH.BroadPhaseLayer(0);
            using var bp1 = new JPH.BroadPhaseLayer(1);
            _bpInterface.MapObjectToBroadPhaseLayer(ObjLayerNonMoving, bp0);
            _bpInterface.MapObjectToBroadPhaseLayer(ObjLayerMoving,    bp1);

            _pairFilter = new JPH.ObjectLayerPairFilterTable(2);
            _pairFilter.EnableCollision(ObjLayerMoving,    ObjLayerNonMoving);
            _pairFilter.EnableCollision(ObjLayerMoving,    ObjLayerMoving);

            _objVsBP = new JPH.ObjectVsBroadPhaseLayerFilterTable(_bpInterface, 2, _pairFilter, 2);

            _physics = new JPH.PhysicsSystem();
            _physics.Init(65536, 0, 65536, 65536, _bpInterface, _objVsBP, _pairFilter);
            _physics.SetGravity(new JPH.Vec3(gravity.X, gravity.Y, gravity.Z));
            _bodyInterface = _physics.GetBodyInterface();

            SetupContactListener();
        }

        public Action<float>? BeforeStep { get; set; }

        public void Step(float deltaTime)
        {
            if (_physics == null) return;

            _accumulator += deltaTime;
            while (_accumulator >= FixedDt)
            {
                // Virtual characters (CharacterVirtual): ExtendedUpdate BEFORE physics step.
                if (_charStates.Count > 0)
                    StepAllCharacters(FixedDt);
                // Kinematic characters (Character): PostSimulation + velocity BEFORE physics step.
                if (_kinCharStates.Count > 0)
                    StepKinematicCharacters(FixedDt);
                BeforeStep?.Invoke(FixedDt);
                _physics.Update(FixedDt, 1, _tempAlloc, _jobSystem);
                _accumulator -= FixedDt;
            }

            DispatchPendingEvents();
        }

        public void Shutdown()
        {
            if (_contactListener != null)
            {
                _physics?.SetContactListener(null);
                _contactListener.Dispose();
                _contactListener = null;
            }

            foreach (var (_, constraint) in _constraintMap)
                _physics?.RemoveConstraint(constraint);
            _constraintMap.Clear();

            foreach (var state in _charStates.Values)
                state.Dispose();
            _charStates.Clear();
            foreach (var state in _kinCharStates.Values)
                state.Dispose();
            _kinCharStates.Clear();
            _charTempAlloc?.Dispose(); _charTempAlloc = null;

            foreach (var state in _vehicleStates.Values)
            {
                _physics?.RemoveStepListener(state.Constraint);
                _physics?.RemoveConstraint(state.Constraint);
                _bodyInterface?.RemoveBody(state.CarBodyId);
                _bodyInterface?.DestroyBody(state.CarBodyId);
                state.Dispose();
            }
            _vehicleStates.Clear();

            foreach (var (handle, bodyId) in _handleToBodyId)
            {
                _bodyInterface?.RemoveBody(bodyId);
                _bodyInterface?.DestroyBody(bodyId);
            }

            _handleToBodyId.Clear();
            _handleToBody.Clear();
            _packedToHandle.Clear();
            _packedToLayer.Clear();
            _packedToMask.Clear();
            _sensors.Clear();

            _physics?.Dispose();   _physics    = null;
            _objVsBP?.Dispose();   _objVsBP    = null;
            _pairFilter?.Dispose(); _pairFilter = null;
            _bpInterface?.Dispose(); _bpInterface = null;
            _jobSystem?.Dispose(); _jobSystem  = null;
            _tempAlloc?.Dispose(); _tempAlloc  = null;
            _bodyInterface = null;
            // Note: JPH.Const_JoltHelpers.Shutdown() is intentionally NOT called here.
            // The factory and registered types are process-lifetime singletons; calling
            // Shutdown() mid-session corrupts global state on subsequent play cycles.
        }

        public unsafe PhysicsBodyHandle CreateBody(BodyDesc desc)
        {
            if (_bodyInterface == null)
                throw new InvalidOperationException("JoltPhysicsWorld not initialized.");

            using var cs = new JPH.BodyCreationSettings();

            // ── Shape ──────────────────────────────────────────────────────────
            var shapes = desc.Shapes;
            bool anyMesh = false;
            foreach (var se in shapes)
                if (se.Shape == ColliderShape.Mesh) { anyMesh = true; break; }

            if (shapes.Count == 1)
            {
                BuildShapeAndCall(shapes[0], desc.Scale, shapeSS => cs.SetShapeSettings(shapeSS));
            }
            else if (shapes.Count > 1)
            {
                using var scs = new JPH.StaticCompoundShapeSettings();
                JPH.CompoundShapeSettings compound = scs;
                foreach (var entry in shapes)
                {
                    var e = entry;
                    using var pos = new JPH.Vec3(e.Offset.X, e.Offset.Y, e.Offset.Z);
                    using var rot = new JPH.Quat(e.OffsetRotation.X, e.OffsetRotation.Y, e.OffsetRotation.Z, e.OffsetRotation.W);
                    var capturedPos = pos; var capturedRot = rot;
                    BuildShapeAndCall(e, desc.Scale, shapeSS => compound.AddShape(capturedPos, capturedRot, shapeSS));
                }
                cs.SetShapeSettings((JPH.Const_StaticCompoundShapeSettings)scs);
            }
            else
            {
                // Empty shapes list — fallback to unit box
                using var ss = new JPH.BoxShapeSettings(new JPH.Vec3(desc.Scale.X * 0.5f, desc.Scale.Y * 0.5f, desc.Scale.Z * 0.5f));
                cs.SetShapeSettings(ss);
            }

            // Position and rotation
            cs.mPosition.Set(desc.Position.X, desc.Position.Y, desc.Position.Z);
            cs.mRotation.Set(desc.Rotation.X, desc.Rotation.Y, desc.Rotation.Z, desc.Rotation.W);

            // Motion type and layer
            switch (desc.MotionType)
            {
                case RigidbodyMotionType.Static:
                    cs.mMotionType  = JPH.EMotionType.Static;
                    cs.mObjectLayer = ObjLayerNonMoving;
                    break;
                case RigidbodyMotionType.Kinematic:
                    cs.mMotionType  = JPH.EMotionType.Kinematic;
                    cs.mObjectLayer = ObjLayerMoving;
                    break;
                default:
                    cs.mMotionType  = JPH.EMotionType.Dynamic;
                    cs.mObjectLayer = ObjLayerMoving;
                    if (desc.ExactMass && desc.Mass > 0f)
                    {
                        cs.SetOverrideMassProperties(1);   // CalculateInertia: the mass given, the inertia from the shapes
                        cs.SetMassOverride(desc.Mass);
                    }
                    break;
            }

            // MeshShape is static-only in Jolt — override regardless of MotionType
            if (anyMesh)
            {
                cs.mMotionType  = JPH.EMotionType.Static;
                cs.mObjectLayer = ObjLayerNonMoving;
            }

            var activation = (cs.mMotionType == JPH.EMotionType.Static) ? JPH.EActivation.DontActivate : JPH.EActivation.Activate;

            if (!desc.UseGravity)
                cs.mGravityFactor = 0f;

            cs.mFriction = Math.Clamp(desc.Friction, 0f, 1f);
            cs.mRestitution = Math.Clamp(desc.Restitution, 0f, 1f);
            cs.mLinearDamping = Math.Max(0f, desc.LinearDamping);
            cs.mAngularDamping = Math.Max(0f, desc.AngularDamping);

            cs.mIsSensor = desc.IsTrigger;

            var body   = _bodyInterface.CreateBody(cs);
            if (body == null)
                throw new InvalidOperationException("JoltPhysicsWorld: CreateBody returned null.");
            var bodyId = body.GetID();
            _bodyInterface.AddBody(bodyId, activation);

            int handle = _nextHandle++;
            uint packed = bodyId.GetIndexAndSequenceNumber();
            _handleToBodyId[handle] = bodyId;
            _handleToBody[handle]   = body;
            _packedToHandle[packed] = handle;
            _packedToLayer[packed] = desc.Layer;
            _packedToMask[packed] = desc.LayerMask;
            if (desc.IsTrigger)
                _sensors.Add(packed);

            return new PhysicsBodyHandle(handle);
        }

        /// <summary>
        /// Builds a Jolt shape-settings object for <paramref name="entry"/> (scaled by
        /// <paramref name="scale"/>) and synchronously passes a non-owning
        /// <see cref="JPH.Const_ShapeSettings"/> view to <paramref name="consume"/>.
        /// The underlying native object is valid for the duration of the callback only.
        /// </summary>
        private static unsafe void BuildShapeAndCall(ShapeEntry entry, Vector3 scale, Action<JPH.Const_ShapeSettings> consume)
        {
            switch (entry.Shape)
            {
                case ColliderShape.Sphere:
                {
                    float r = entry.Radius * MathF.Max(scale.X, MathF.Max(scale.Y, scale.Z));
                    using var ss = new JPH.SphereShapeSettings(r);
                    consume(ss);
                    break;
                }
                case ColliderShape.Capsule:
                {
                    float radius     = entry.Radius * MathF.Max(scale.X, scale.Z);
                    float halfHeight = MathF.Max(0f, entry.HalfHeight * scale.Y);
                    using var ss = new JPH.CapsuleShapeSettings(halfHeight, radius);
                    consume(ss);
                    break;
                }
                case ColliderShape.Cylinder:
                {
                    float radius     = entry.Radius * MathF.Max(scale.X, scale.Z);
                    float halfHeight = entry.HalfHeight * scale.Y;
                    using var ss = new JPH.CylinderShapeSettings(halfHeight, radius);
                    consume(ss);
                    break;
                }
                case ColliderShape.Plane:
                {
                    using var plane = new JPH.Plane(new JPH.Vec4(0f, 1f, 0f, 0f));
                    using var ss = new JPH.PlaneShapeSettings(plane);
                    consume(ss);
                    break;
                }
                case ColliderShape.ConvexHull:
                {
                    Vector3 s = scale;
                    Vector3[] src = entry.MeshVertices ?? new Vector3[]
                    {
                        new(-0.5f,-0.5f,-0.5f), new(0.5f,-0.5f,-0.5f),
                        new( 0.5f, 0.5f,-0.5f), new(-0.5f, 0.5f,-0.5f),
                        new(-0.5f,-0.5f, 0.5f), new(0.5f,-0.5f, 0.5f),
                        new( 0.5f, 0.5f, 0.5f), new(-0.5f, 0.5f, 0.5f),
                    };
                    var pts = new JPH.Vec3f[src.Length];
                    for (int i = 0; i < src.Length; i++)
                        pts[i] = new JPH.Vec3f(src[i].X * s.X, src[i].Y * s.Y, src[i].Z * s.Z);
                    using var hull = JPH.ConvexHullShapeSettingsFromPoints(pts, 0.05f);
                    consume(hull);
                    break;
                }
                case ColliderShape.Mesh:
                {
                    if (entry.MeshVertices != null && entry.MeshIndices != null)
                    {
                        Vector3 s = scale;
                        var pts = new JPH.Vec3f[entry.MeshVertices.Length];
                        for (int i = 0; i < pts.Length; i++)
                        {
                            Vector3 v = entry.MeshVertices[i];
                            pts[i] = new JPH.Vec3f(v.X * s.X, v.Y * s.Y, v.Z * s.Z);
                        }
                        using var mesh = JPH.MeshShapeSettingsFromIndexedMesh(pts, entry.MeshIndices);
                        consume(mesh);
                    }
                    else
                    {
                        // Geometry not yet populated — fallback to box
                        using var ss = new JPH.BoxShapeSettings(new JPH.Vec3(scale.X * 0.5f, scale.Y * 0.5f, scale.Z * 0.5f));
                        consume(ss);
                    }
                    break;
                }
                default: // Box
                {
                    using var ss = new JPH.BoxShapeSettings(new JPH.Vec3(
                        entry.HalfExtent.X * scale.X,
                        entry.HalfExtent.Y * scale.Y,
                        entry.HalfExtent.Z * scale.Z));
                    consume(ss);
                    break;
                }
            }
        }

        public void DestroyBody(PhysicsBodyHandle handle)
        {
            if (!_handleToBodyId.TryGetValue(handle.Value, out var bodyId)) return;
            uint packed = bodyId.GetIndexAndSequenceNumber();
            _bodyInterface?.RemoveBody(bodyId);
            _bodyInterface?.DestroyBody(bodyId);
            _handleToBodyId.Remove(handle.Value);
            _handleToBody.Remove(handle.Value);
            _packedToHandle.Remove(packed);
            _packedToLayer.Remove(packed);
            _packedToMask.Remove(packed);
            _sensors.Remove(packed);
        }

        public ConstraintHandle CreateConstraint(ConstraintDesc desc)
        {
            if (_physics == null || _bodyInterface == null)
                throw new InvalidOperationException("JoltPhysicsWorld not initialized.");

            // Resolve bodies; BodyA/BodyB.Invalid means world anchor — use the static world body.
            if (!_handleToBody.TryGetValue(desc.BodyA.Value, out var bodyA) ||
                !_handleToBody.TryGetValue(desc.BodyB.Value, out var bodyB))
                return ConstraintHandle.Invalid;

            JPH.TwoBodyConstraint? constraint = null;

            using var a1 = new JPH.Vec3(desc.LocalAnchorA.X, desc.LocalAnchorA.Y, desc.LocalAnchorA.Z);
            using var a2 = new JPH.Vec3(desc.LocalAnchorB.X, desc.LocalAnchorB.Y, desc.LocalAnchorB.Z);
            using var ax1 = new JPH.Vec3(
                desc.LocalAxisA == System.Numerics.Vector3.Zero ? 0f : desc.LocalAxisA.X,
                desc.LocalAxisA == System.Numerics.Vector3.Zero ? 1f : desc.LocalAxisA.Y,
                desc.LocalAxisA == System.Numerics.Vector3.Zero ? 0f : desc.LocalAxisA.Z);
            using var ax2 = new JPH.Vec3(
                desc.LocalAxisB == System.Numerics.Vector3.Zero ? 0f : desc.LocalAxisB.X,
                desc.LocalAxisB == System.Numerics.Vector3.Zero ? 1f : desc.LocalAxisB.Y,
                desc.LocalAxisB == System.Numerics.Vector3.Zero ? 0f : desc.LocalAxisB.Z);

            switch (desc.Type)
            {
                case ConstraintType.Fixed:
                {
                    using var s = new JPH.FixedConstraintSettings();
                    s.mPoint1.Set(a1.GetX(), a1.GetY(), a1.GetZ());
                    s.mPoint2.Set(a2.GetX(), a2.GetY(), a2.GetZ());
                    s.mAxisX1.Set(1f, 0f, 0f); s.mAxisY1.Set(0f, 1f, 0f);
                    s.mAxisX2.Set(1f, 0f, 0f); s.mAxisY2.Set(0f, 1f, 0f);
                    constraint = s.Create(bodyA, bodyB);
                    break;
                }
                case ConstraintType.Point:
                {
                    using var s = new JPH.PointConstraintSettings();
                    s.mPoint1.Set(a1.GetX(), a1.GetY(), a1.GetZ());
                    s.mPoint2.Set(a2.GetX(), a2.GetY(), a2.GetZ());
                    constraint = s.Create(bodyA, bodyB);
                    break;
                }
                case ConstraintType.Hinge:
                {
                    using var s = new JPH.HingeConstraintSettings();
                    s.mPoint1.Set(a1.GetX(), a1.GetY(), a1.GetZ());
                    s.mPoint2.Set(a2.GetX(), a2.GetY(), a2.GetZ());
                    s.mHingeAxis1.Set(ax1.GetX(), ax1.GetY(), ax1.GetZ());
                    s.mHingeAxis2.Set(ax2.GetX(), ax2.GetY(), ax2.GetZ());
                    s.mLimitsMin = desc.MinLimit;
                    s.mLimitsMax = desc.MaxLimit;
                    constraint = s.Create(bodyA, bodyB);
                    break;
                }
                case ConstraintType.Slider:
                {
                    using var s = new JPH.SliderConstraintSettings();
                    s.mPoint1.Set(a1.GetX(), a1.GetY(), a1.GetZ());
                    s.mPoint2.Set(a2.GetX(), a2.GetY(), a2.GetZ());
                    s.mSliderAxis1.Set(ax1.GetX(), ax1.GetY(), ax1.GetZ());
                    s.mSliderAxis2.Set(ax2.GetX(), ax2.GetY(), ax2.GetZ());
                    s.mLimitsMin = desc.MinLimit;
                    s.mLimitsMax = desc.MaxLimit;
                    constraint = s.Create(bodyA, bodyB);
                    break;
                }
                case ConstraintType.Distance:
                {
                    using var s = new JPH.DistanceConstraintSettings();
                    s.mPoint1.Set(a1.GetX(), a1.GetY(), a1.GetZ());
                    s.mPoint2.Set(a2.GetX(), a2.GetY(), a2.GetZ());
                    s.mMinDistance = desc.MinLimit;
                    s.mMaxDistance = desc.MaxLimit < desc.MinLimit ? desc.MinLimit : desc.MaxLimit;
                    constraint = s.Create(bodyA, bodyB);
                    break;
                }
                case ConstraintType.Cone:
                {
                    using var s = new JPH.ConeConstraintSettings();
                    s.mPoint1.Set(a1.GetX(), a1.GetY(), a1.GetZ());
                    s.mPoint2.Set(a2.GetX(), a2.GetY(), a2.GetZ());
                    s.mTwistAxis1.Set(ax1.GetX(), ax1.GetY(), ax1.GetZ());
                    s.mTwistAxis2.Set(ax2.GetX(), ax2.GetY(), ax2.GetZ());
                    s.mHalfConeAngle = Math.Max(0f, desc.MaxLimit);
                    constraint = s.Create(bodyA, bodyB);
                    break;
                }
                case ConstraintType.SwingTwist:
                {
                    using var s = new JPH.SwingTwistConstraintSettings();
                    s.mPosition1.Set(a1.GetX(), a1.GetY(), a1.GetZ());
                    s.mPosition2.Set(a2.GetX(), a2.GetY(), a2.GetZ());
                    s.mTwistAxis1.Set(ax1.GetX(), ax1.GetY(), ax1.GetZ());
                    s.mTwistAxis2.Set(ax2.GetX(), ax2.GetY(), ax2.GetZ());
                    s.mNormalHalfConeAngle = Math.Max(0f, desc.MaxLimit);
                    s.mPlaneHalfConeAngle  = Math.Max(0f, desc.MaxLimit);
                    s.mTwistMinAngle = desc.MinLimit;
                    s.mTwistMaxAngle = desc.MaxLimit;
                    constraint = s.Create(bodyA, bodyB);
                    break;
                }
                case ConstraintType.SixDOF:
                {
                    using var s = new JPH.SixDOFConstraintSettings();
                    s.mPosition1.Set(a1.GetX(), a1.GetY(), a1.GetZ());
                    s.mPosition2.Set(a2.GetX(), a2.GetY(), a2.GetZ());
                    constraint = s.Create(bodyA, bodyB);
                    break;
                }
                default:
                    return ConstraintHandle.Invalid;
            }

            if (constraint == null) return ConstraintHandle.Invalid;

            _physics.AddConstraint(constraint);
            int ch = _nextConstraintHandle++;
            _constraintMap[ch] = constraint;
            return new ConstraintHandle(ch);
        }

        public void DestroyConstraint(ConstraintHandle handle)
        {
            if (!_constraintMap.TryGetValue(handle.Value, out var constraint)) return;
            _physics?.RemoveConstraint(constraint);
            _constraintMap.Remove(handle.Value);
        }

        public void SetConstraintEnabled(ConstraintHandle handle, bool enabled)
        {
            if (!_constraintMap.TryGetValue(handle.Value, out var constraint)) return;
            constraint.SetEnabled(enabled);
        }

        public void SetPosition(PhysicsBodyHandle handle, Vector3 position)
        {
            if (!_handleToBodyId.TryGetValue(handle.Value, out var bodyId)) return;
            using var pos = new JPH.Vec3(position.X, position.Y, position.Z);
            _bodyInterface?.SetPosition(bodyId, pos, JPH.EActivation.Activate);
        }

        public void SetRotation(PhysicsBodyHandle handle, Quaternion rotation)
        {
            if (!_handleToBodyId.TryGetValue(handle.Value, out var bodyId)) return;
            using var rot = new JPH.Quat(rotation.X, rotation.Y, rotation.Z, rotation.W);
            _bodyInterface?.SetRotation(bodyId, rot, JPH.EActivation.Activate);
        }

        public void MoveKinematic(PhysicsBodyHandle handle, Vector3 targetPosition, Quaternion targetRotation, float deltaTime)
        {
            if (!_handleToBodyId.TryGetValue(handle.Value, out var bodyId)) return;
            // MeshShape bodies are forced to Static — guard against calling MoveKinematic on them
            if (_bodyInterface?.GetMotionType(bodyId) != JPH.EMotionType.Kinematic) return;
            using var pos = new JPH.Vec3(targetPosition.X, targetPosition.Y, targetPosition.Z);
            using var rot = new JPH.Quat(targetRotation.X, targetRotation.Y, targetRotation.Z, targetRotation.W);
            _bodyInterface?.MoveKinematic(bodyId, pos, rot, deltaTime);
        }

        public Vector3 GetPosition(PhysicsBodyHandle handle)
        {
            if (!_handleToBodyId.TryGetValue(handle.Value, out var bodyId)) return Vector3.Zero;
            using var pos = _bodyInterface!.GetPosition(bodyId);
            return new Vector3(pos.GetX(), pos.GetY(), pos.GetZ());
        }

        public Quaternion GetRotation(PhysicsBodyHandle handle)
        {
            if (!_handleToBodyId.TryGetValue(handle.Value, out var bodyId)) return Quaternion.Identity;
            using var rot = _bodyInterface!.GetRotation(bodyId);
            return new Quaternion(rot.GetX(), rot.GetY(), rot.GetZ(), rot.GetW());
        }

        public void SetLinearVelocity(PhysicsBodyHandle handle, Vector3 velocity)
        {
            if (!_handleToBodyId.TryGetValue(handle.Value, out var bodyId)) return;
            using var v = new JPH.Vec3(velocity.X, velocity.Y, velocity.Z);
            _bodyInterface?.SetLinearVelocity(bodyId, v);
        }

        public Vector3 GetLinearVelocity(PhysicsBodyHandle handle)
        {
            if (!_handleToBodyId.TryGetValue(handle.Value, out var bodyId)) return Vector3.Zero;
            using var v = _bodyInterface!.GetLinearVelocity(bodyId);
            return new Vector3(v.GetX(), v.GetY(), v.GetZ());
        }

        public void SetAngularVelocity(PhysicsBodyHandle handle, Vector3 velocity)
        {
            if (!_handleToBodyId.TryGetValue(handle.Value, out var bodyId)) return;
            using var v = new JPH.Vec3(velocity.X, velocity.Y, velocity.Z);
            _bodyInterface?.SetAngularVelocity(bodyId, v);
        }

        public Vector3 GetAngularVelocity(PhysicsBodyHandle handle)
        {
            if (!_handleToBodyId.TryGetValue(handle.Value, out var bodyId)) return Vector3.Zero;
            using var v = _bodyInterface!.GetAngularVelocity(bodyId);
            return new Vector3(v.GetX(), v.GetY(), v.GetZ());
        }

        public void AddForce(PhysicsBodyHandle handle, Vector3 force)
        {
            if (!_handleToBodyId.TryGetValue(handle.Value, out var bodyId)) return;
            using var f = new JPH.Vec3(force.X, force.Y, force.Z);
            _bodyInterface?.AddForce(bodyId, f);
        }

        public void AddImpulse(PhysicsBodyHandle handle, Vector3 impulse)
        {
            if (!_handleToBodyId.TryGetValue(handle.Value, out var bodyId)) return;
            using var i = new JPH.Vec3(impulse.X, impulse.Y, impulse.Z);
            _bodyInterface?.AddImpulse(bodyId, i);
        }

        public void AddTorque(PhysicsBodyHandle handle, Vector3 torque)
        {
            if (!_handleToBodyId.TryGetValue(handle.Value, out var bodyId)) return;
            using var t = new JPH.Vec3(torque.X, torque.Y, torque.Z);
            _bodyInterface?.AddTorque(bodyId, t, JPH.EActivation.Activate);
        }

        public unsafe bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, out RaycastHit hit)
        {
            hit = default;
            if (_physics == null) return false;

            var dir = Vector3.Normalize(direction) * maxDistance;
            using var rayOrigin = new JPH.Vec3(origin.X, origin.Y, origin.Z);
            using var rayDir    = new JPH.Vec3(dir.X, dir.Y, dir.Z);
            using var ray       = new JPH.RRayCast(rayOrigin, rayDir);
            using var result    = new JPH.RayCastResult();

            var npq   = _physics.GetNarrowPhaseQuery();
            bool found = npq.CastRay(ray, result);
            if (!found) return false;

            uint packed = result.mBodyID.GetIndexAndSequenceNumber();
            _packedToHandle.TryGetValue(packed, out int handleId);

            float fraction = result.mFraction;
            Vector3 hitPoint = origin + Vector3.Normalize(direction) * (maxDistance * fraction);

            hit = new RaycastHit(new PhysicsBodyHandle(handleId), hitPoint, Vector3.Normalize(direction), maxDistance * fraction);
            return true;
        }

        public int OverlapSphere(Vector3 center, float radius, List<PhysicsBodyHandle> results, int maxResults = 64)
        {
            results.Clear();
            if (_bodyInterface == null || radius <= 0f || maxResults <= 0) return 0;

            using var c = new JPH.Vec3(center.X, center.Y, center.Z);
            using var query = new JPH.AABox(c, radius);
            return OverlapAabb(query, results, maxResults);
        }

        public int OverlapBox(Vector3 center, Vector3 halfExtents, Quaternion rotation, List<PhysicsBodyHandle> results, int maxResults = 64)
        {
            results.Clear();
            if (_bodyInterface == null || maxResults <= 0) return 0;

            var rotMat = Matrix4x4.CreateFromQuaternion(rotation);
            Vector3 hx = Vector3.TransformNormal(new Vector3(halfExtents.X, 0f, 0f), rotMat);
            Vector3 hy = Vector3.TransformNormal(new Vector3(0f, halfExtents.Y, 0f), rotMat);
            Vector3 hz = Vector3.TransformNormal(new Vector3(0f, 0f, halfExtents.Z), rotMat);
            Vector3 ext = Abs(hx) + Abs(hy) + Abs(hz);

            Vector3 min = center - ext;
            Vector3 max = center + ext;
            using var minV = new JPH.Vec3(min.X, min.Y, min.Z);
            using var maxV = new JPH.Vec3(max.X, max.Y, max.Z);
            using var query = new JPH.AABox(minV, maxV);
            return OverlapAabb(query, results, maxResults);
        }

        public void SetCollisionListener(ICollisionListener? listener)
        {
            _gameListener = listener;
        }

        // ── Character controller implementation ───────────────────────────────

        public CharacterHandle CreateCharacter(CharacterDesc desc)
        {
            if (_physics == null)
                throw new InvalidOperationException("JoltPhysicsWorld not initialized.");

            using var pos = new JPH.Vec3(desc.Position.X, desc.Position.Y, desc.Position.Z);
            using var rot = new JPH.Quat(desc.Rotation.X, desc.Rotation.Y, desc.Rotation.Z, desc.Rotation.W);

            // Build capsule: feet at the character's position, top at position + height.
            // RotatedTranslatedShape offsets the capsule so its base aligns with position,
            // plus any user-supplied ShapeOffset for fine-tuning.
            float halfHeight = MathF.Max(0f, desc.Height * 0.5f - desc.Radius);
            var capsule  = new JPH.CapsuleShape(halfHeight, desc.Radius);
            using var offset   = new JPH.Vec3(
                desc.ShapeOffset.X,
                halfHeight + desc.Radius + desc.ShapeOffset.Y,
                desc.ShapeOffset.Z);
            using var identRot = JPH.Quat.SIdentity();
            var rotTrans = new JPH.RotatedTranslatedShape(offset, identRot, capsule);

            int h = _nextCharHandle++;

            if (desc.IsKinematic)
            {
                using var settings = new JPH.CharacterSettings();
                settings.mLayer        = desc.Layer;
                settings.mFriction     = desc.Friction;
                settings.mGravityFactor = desc.GravityFactor;
                settings.mMass         = desc.Mass;
                settings.mMaxSlopeAngle = desc.MaxSlopeAngle;
                settings.SetShape((JPH.Const_RotatedTranslatedShape)rotTrans);

                var supVol = settings.mSupportingVolume;
                using var axisY = new JPH.Vec3(0f, 1f, 0f);
                supVol.SetNormal(axisY);
                supVol.SetConstant(-desc.Radius);

                var character = new JPH.Character(settings, pos, rot, 0, _physics);
                character.AddToPhysicsSystem(JPH.EActivation.Activate);

                _kinCharStates[h] = new KinCharState
                {
                    Character          = character,
                    RotTransShape      = rotTrans,
                    CapsuleShapeObj    = capsule,
                    CollisionTolerance = desc.CollisionTolerance > 0f ? desc.CollisionTolerance : 0.05f,
                    DesiredVelocity    = Vector3.Zero,
                };
            }
            else
            {
                _charTempAlloc ??= new JPH.TempAllocatorImpl(4 * 1024 * 1024);

                // Inner body shape: 90% scale of outer — used as the physics body for interactions.
                const float InnerFraction = 0.9f;
                float innerHH = halfHeight * InnerFraction;
                float innerR  = desc.Radius  * InnerFraction;
                var innerCapsule = new JPH.CapsuleShape(innerHH, innerR);
                var innerRts     = new JPH.RotatedTranslatedShape(offset, identRot, innerCapsule);

                using var settings = new JPH.CharacterVirtualSettings();
                settings.mMaxSlopeAngle  = desc.MaxSlopeAngle;
                settings.mMaxStrength    = desc.MaxStrength;
                settings.mMass           = desc.Mass;
                settings.mInnerBodyLayer = ObjLayerMoving;
                settings.SetShape((JPH.Const_RotatedTranslatedShape)rotTrans);
                settings.SetInnerBodyShape((JPH.Const_RotatedTranslatedShape)innerRts);

                var supVol = settings.mSupportingVolume;
                using var axisY2 = new JPH.Vec3(0f, 1f, 0f);
                supVol.SetNormal(axisY2);
                supVol.SetConstant(-desc.Radius);

                var character = new JPH.CharacterVirtual(settings, pos, rot, 0, _physics);

                var cvsc = new JPH.CharacterVsCharacterCollisionSimple();
                cvsc.Add(character);
                character.SetCharacterVsCharacterCollision(cvsc);

                _charStates[h] = new CharState
                {
                    Character            = character,
                    RotTransShape        = rotTrans,
                    CapsuleShapeObj      = capsule,
                    InnerRotTransShape   = innerRts,
                    InnerCapsuleShapeObj = innerCapsule,
                    Cvsc                 = cvsc,
                    DesiredVelocity      = Vector3.Zero,
                };
            }

            return new CharacterHandle(h);
        }

        public void DestroyCharacter(CharacterHandle handle)
        {
            if (_charStates.TryGetValue(handle.Value, out var state))
            {
                state.Dispose();
                _charStates.Remove(handle.Value);
                return;
            }
            if (_kinCharStates.TryGetValue(handle.Value, out var kstate))
            {
                kstate.Dispose();
                _kinCharStates.Remove(handle.Value);
            }
        }

        public void SetCharacterLinearVelocity(CharacterHandle handle, Vector3 velocity)
        {
            if (_charStates.TryGetValue(handle.Value, out var state))
                state.DesiredVelocity = velocity;
            else if (_kinCharStates.TryGetValue(handle.Value, out var kstate))
                kstate.DesiredVelocity = velocity;
        }

        public Vector3 GetCharacterPosition(CharacterHandle handle)
        {
            if (_charStates.TryGetValue(handle.Value, out var state))
            {
                using var p = state.Character.GetPosition();
                return new Vector3(p.GetX(), p.GetY(), p.GetZ());
            }
            if (_kinCharStates.TryGetValue(handle.Value, out var kstate))
            {
                using var p = kstate.Character.GetPosition();
                return new Vector3(p.GetX(), p.GetY(), p.GetZ());
            }
            return Vector3.Zero;
        }

        public Quaternion GetCharacterRotation(CharacterHandle handle)
        {
            if (_charStates.TryGetValue(handle.Value, out var state))
            {
                using var r = state.Character.GetRotation();
                return new Quaternion(r.GetX(), r.GetY(), r.GetZ(), r.GetW());
            }
            if (_kinCharStates.TryGetValue(handle.Value, out var kstate))
            {
                using var r = kstate.Character.GetRotation();
                return new Quaternion(r.GetX(), r.GetY(), r.GetZ(), r.GetW());
            }
            return Quaternion.Identity;
        }

        public void SetCharacterPosition(CharacterHandle handle, Vector3 position)
        {
            if (_charStates.TryGetValue(handle.Value, out var state))
            {
                using var p = new JPH.Vec3(position.X, position.Y, position.Z);
                state.Character.SetPosition(p);
                return;
            }
            if (_kinCharStates.TryGetValue(handle.Value, out var kstate))
            {
                using var p = new JPH.Vec3(position.X, position.Y, position.Z);
                using var r = kstate.Character.GetRotation();
                kstate.Character.SetPositionAndRotation(p, r, JPH.EActivation.Activate);
            }
        }

        public bool IsCharacterGrounded(CharacterHandle handle)
        {
            if (_charStates.TryGetValue(handle.Value, out var state))
                return state.Character.GetGroundState() == JPH.CharacterBase.EGroundState.OnGround;
            if (_kinCharStates.TryGetValue(handle.Value, out var kstate))
                return kstate.Character.GetGroundState() == JPH.CharacterBase.EGroundState.OnGround;
            return false;
        }

        public Vector3 GetCharacterGroundNormal(CharacterHandle handle)
        {
            if (_charStates.TryGetValue(handle.Value, out var state))
            {
                using var n = state.Character.GetGroundNormal();
                return new Vector3(n.GetX(), n.GetY(), n.GetZ());
            }
            if (_kinCharStates.TryGetValue(handle.Value, out var kstate))
            {
                using var n = kstate.Character.GetGroundNormal();
                return new Vector3(n.GetX(), n.GetY(), n.GetZ());
            }
            return Vector3.UnitY;
        }

        // ── Vehicle controllers ───────────────────────────────────────────────

        public VehicleHandle CreateVehicle(VehicleDesc desc)
        {
            if (_physics == null || _bodyInterface == null)
                throw new InvalidOperationException("JoltPhysicsWorld not initialized.");

            var type = desc.Type;

            // ── Build chassis shape ──────────────────────────────────────────
            using var bodyHE  = new JPH.Vec3(desc.ChassisHalfExtent.X, desc.ChassisHalfExtent.Y, desc.ChassisHalfExtent.Z);
            using var bodyBox = new JPH.BoxShapeSettings(bodyHE);

            // Lower centre-of-mass for stability
            using var comOff = new JPH.Vec3(0f, desc.COMOffsetY, 0f);
            using var comSS  = new JPH.OffsetCenterOfMassShapeSettings(comOff, bodyBox);

            // ── Create chassis body ──────────────────────────────────────────
            using var carCS = new JPH.BodyCreationSettings();
            carCS.SetShapeSettings(comSS);
            carCS.mPosition.Set(desc.Position.X, desc.Position.Y, desc.Position.Z);
            carCS.mRotation.Set(desc.Rotation.X,  desc.Rotation.Y,  desc.Rotation.Z, desc.Rotation.W);
            carCS.mMotionType  = JPH.EMotionType.Dynamic;
            carCS.mObjectLayer = ObjLayerMoving;
            carCS.SetOverrideMassProperties(1);
            carCS.SetMassOverride(desc.Mass);

            var carBody = _bodyInterface.CreateBody(carCS);
            if (carBody == null)
                throw new InvalidOperationException("Failed to create vehicle chassis body.");
            var carBodyId = carBody.GetID();
            _bodyInterface.AddBody(carBodyId, JPH.EActivation.Activate);

            // Register chassis in the standard handle maps so GetVehicleBodyHandle works
            // and scripts can apply external forces via AddForce / SetLinearVelocity.
            int bodyHandleValue = _nextHandle++;
            _handleToBodyId[bodyHandleValue]  = carBodyId;
            _handleToBody[bodyHandleValue]    = carBody;
            uint packedId = carBodyId.GetIndexAndSequenceNumber();
            _packedToHandle[packedId]  = bodyHandleValue;
            _packedToLayer[packedId]   = desc.Layer;
            _packedToMask[packedId]    = desc.LayerMask;

            // ── Vehicle constraint settings ──────────────────────────────────
            using var vehicleSettings = new JPH.VehicleConstraintSettings();
            vehicleSettings.mMaxPitchRollAngle = desc.MaxRollAngle;

            int wheelCount = desc.Wheels.Length;
            if (type == ECS.Components.VehicleType.Tracked)
            {
                // ── Tracked (tank) ───────────────────────────────────────────
                using var ctrlSettings = new JPH.TrackedVehicleControllerSettings();
                vehicleSettings.VehicleSettingsSetTrackedController(ctrlSettings);

                // Distribute wheels evenly across 2 tracks (left = 1, right = 0 by Jolt convention)
                int wheelsPerTrack = wheelCount / 2;
                for (int t = 0; t < 2; t++)
                {
                    var track = ctrlSettings.mTracks[t];
                    // Last wheel added to each track is the driven wheel
                    track.mDrivenWheel = (uint)(t * wheelsPerTrack + wheelsPerTrack - 1);

                    for (int w = 0; w < wheelsPerTrack; w++)
                    {
                        var wd = desc.Wheels[t * wheelsPerTrack + w];
                        using var ws = new JPH.WheelSettingsTV();
                        ws.mPosition.Set(wd.LocalPosition.X, wd.LocalPosition.Y, wd.LocalPosition.Z);
                        ws.mRadius              = wd.Radius;
                        ws.mWidth               = wd.Width;
                        ws.mSuspensionMinLength = wd.SuspMinLength;
                        ws.mSuspensionMaxLength = wd.SuspMaxLength;
                        ws.mSuspensionSpring.mFrequency = wd.SuspFrequency;

                        track.mWheels.PushBack((uint)(t * wheelsPerTrack + w));
                        vehicleSettings.VehicleSettingsAddWheelTV(ws);
                    }
                }

                // Ray tester suits tracks (flat contact, no cylinder)
                var constraint = new JPH.VehicleConstraint(carBody, vehicleSettings);
                var tester     = new JPH.VehicleCollisionTesterRay(ObjLayerMoving);
                constraint.SetVehicleCollisionTester(tester);
                _physics.AddConstraint(constraint);
                _physics.AddStepListener(constraint);

                int handle = _nextVehicleHandle++;
                _vehicleStates[handle] = new VehicleState
                {
                    Constraint       = constraint,
                    CarBody          = carBody,
                    CarBodyId        = carBodyId,
                    CollisionTester  = tester,
                    Type             = type,
                    WheelCount       = wheelCount,
                    BodyHandleValue  = bodyHandleValue,
                };
                return new VehicleHandle(handle);
            }
            else
            {
                // ── Wheeled or Motorcycle ────────────────────────────────────
                using var ctrlSettings = type == ECS.Components.VehicleType.Motorcycle
                    ? (JPH.WheeledVehicleControllerSettings)new JPH.MotorcycleControllerSettings()
                    : new JPH.WheeledVehicleControllerSettings();

                ctrlSettings.mEngine.mMaxTorque     = desc.MaxEngineTorque;
                ctrlSettings.mTransmission.mClutchStrength = desc.ClutchStrength;

                // Add wheels
                for (int i = 0; i < wheelCount; i++)
                {
                    var wd = desc.Wheels[i];
                    using var ws = new JPH.WheelSettingsWV();
                    ws.mPosition.Set(wd.LocalPosition.X, wd.LocalPosition.Y, wd.LocalPosition.Z);
                    ws.mRadius              = wd.Radius;
                    ws.mWidth               = wd.Width;
                    ws.mSuspensionMinLength = wd.SuspMinLength;
                    ws.mSuspensionMaxLength = wd.SuspMaxLength;
                    ws.mSuspensionSpring.mFrequency = wd.SuspFrequency;
                    ws.mSuspensionSpring.mDamping   = wd.SuspDamping;
                    ws.mMaxSteerAngle       = wd.MaxSteerAngle;
                    ws.mMaxHandBrakeTorque  = wd.MaxHandBrakeTorque;
                    vehicleSettings.VehicleSettingsAddWheel(ws);
                }
                vehicleSettings.VehicleSettingsSetController(ctrlSettings);

                // Build per-axle differentials from driven-wheel pairs
                var drivenLeft  = new List<int>();
                var drivenRight = new List<int>();
                for (int i = 0; i < wheelCount; i++)
                {
                    if (!desc.Wheels[i].IsDriven) continue;
                    if (desc.Wheels[i].LocalPosition.X <= 0f) drivenLeft.Add(i);
                    else                                        drivenRight.Add(i);
                }
                int axleCount = Math.Max(drivenLeft.Count, drivenRight.Count);
                if (axleCount == 0)
                {
                    // No driven wheels configured — drive all wheels on first axle as fallback
                    if (wheelCount >= 2)
                    {
                        using var d = new JPH.VehicleDifferentialSettings();
                        d.mLeftWheel  = 0;
                        d.mRightWheel = 1;
                        ctrlSettings.mDifferentials.PushBack(d);
                    }
                }
                else if (wheelCount == 2)
                {
                    // Motorcycle: single center chain drive
                    using var d = new JPH.VehicleDifferentialSettings();
                    d.mLeftWheel  = drivenLeft.Count > 0  ? drivenLeft[0]  : 1;
                    d.mRightWheel = drivenRight.Count > 0 ? drivenRight[0] : 1;
                    ctrlSettings.mDifferentials.PushBack(d);
                }
                else
                {
                    for (int a = 0; a < axleCount; a++)
                    {
                        using var d = new JPH.VehicleDifferentialSettings();
                        d.mLeftWheel  = a < drivenLeft.Count  ? drivenLeft[a]  : -1;
                        d.mRightWheel = a < drivenRight.Count ? drivenRight[a] : -1;
                        ctrlSettings.mDifferentials.PushBack(d);
                    }
                }

                // Anti-roll bar for four-wheel cars (skip for 2-wheelers)
                if (wheelCount >= 4)
                {
                    using var arbFront = new JPH.VehicleAntiRollBar();
                    arbFront.mLeftWheel  = 0;
                    arbFront.mRightWheel = 1;
                    vehicleSettings.mAntiRollBars.PushBack(arbFront);
                    using var arbRear = new JPH.VehicleAntiRollBar();
                    arbRear.mLeftWheel  = 2;
                    arbRear.mRightWheel = 3;
                    vehicleSettings.mAntiRollBars.PushBack(arbRear);
                }

                // Collision tester: cast cylinder (convex radius 0.05 m)
                var tester = new JPH.VehicleCollisionTesterCastCylinder(ObjLayerMoving, 0.05f);

                var constraint = new JPH.VehicleConstraint(carBody, vehicleSettings);
                constraint.SetVehicleCollisionTester(tester);
                _physics.AddConstraint(constraint);
                _physics.AddStepListener(constraint);

                int handle = _nextVehicleHandle++;
                _vehicleStates[handle] = new VehicleState
                {
                    Constraint      = constraint,
                    CarBody         = carBody,
                    CarBodyId       = carBodyId,
                    CollisionTester = tester,
                    Type            = type,
                    WheelCount      = wheelCount,
                    BodyHandleValue = bodyHandleValue,
                };
                return new VehicleHandle(handle);
            }
        }

        public void DestroyVehicle(VehicleHandle handle)
        {
            if (!_vehicleStates.TryGetValue(handle.Value, out var state)) return;
            _vehicleStates.Remove(handle.Value);

            _physics?.RemoveStepListener(state.Constraint);
            _physics?.RemoveConstraint(state.Constraint);

            // Remove chassis body from handle maps
            uint packedId = state.CarBodyId.GetIndexAndSequenceNumber();
            _handleToBodyId.Remove(state.BodyHandleValue);
            _handleToBody.Remove(state.BodyHandleValue);
            _packedToHandle.Remove(packedId);
            _packedToLayer.Remove(packedId);
            _packedToMask.Remove(packedId);

            _bodyInterface?.RemoveBody(state.CarBodyId);
            _bodyInterface?.DestroyBody(state.CarBodyId);
            state.Dispose();
        }

        public void SetVehicleInput(VehicleHandle handle, float steer, float throttle, float brake, float handBrake)
        {
            if (!_vehicleStates.TryGetValue(handle.Value, out var state)) return;
            var constraint = state.Constraint;

            if (state.Type == ECS.Components.VehicleType.Tracked)
            {
                var ctrl = (JPH.TrackedVehicleController?)constraint.GetController();
                if (ctrl == null) return;

                // Tank steering: differential throttle per track (matches Demo_Tank.cs)
                float leftR  = throttle - steer;
                float rightR = throttle + steer;

                ctrl.SetDriverInput(throttle, leftR, rightR, brake);
                if (MathF.Abs(throttle) > 0f || MathF.Abs(steer) > 0f)
                    _bodyInterface?.ActivateBody(state.CarBodyId);
            }
            else
            {
                var ctrl = constraint.GetWheeledController();
                if (ctrl == null) return;

                // Match Demo_VehicleConstraint.cs pattern: flip sign based on direction of travel
                float currentForward = 1f;
                using var curVelJph = _bodyInterface!.GetLinearVelocity(state.CarBodyId);
                using var rotJph    = _bodyInterface.GetRotation(state.CarBodyId);
                var rot     = new Quaternion(rotJph.GetX(), rotJph.GetY(), rotJph.GetZ(), rotJph.GetW());
                var fwdVec  = Vector3.Transform(Vector3.UnitZ, rot);
                var velVec  = new Vector3(curVelJph.GetX(), curVelJph.GetY(), curVelJph.GetZ());
                float dotFwd = Vector3.Dot(velVec, fwdVec);

                if (dotFwd < -0.1f)       currentForward = -1f;
                else if (dotFwd > 0.1f)   currentForward =  1f;
                else                      currentForward = state.PreviousForward;
                state.PreviousForward = currentForward;

                // When reversing, flip brake/throttle so the car doesn't fight itself
                float forward;
                float brakeVal;
                if (throttle > 0f)
                {
                    forward  = throttle;
                    brakeVal = currentForward < 0f ? brake + throttle : brake;
                }
                else if (throttle < 0f)
                {
                    forward  = throttle;
                    brakeVal = currentForward > 0f ? brake - throttle : brake;
                }
                else
                {
                    forward  = 0f;
                    brakeVal = brake;
                }

                ctrl.SetDriverInput(forward, steer, brakeVal, handBrake);
                if (MathF.Abs(throttle) > 0f || MathF.Abs(steer) > 0f || handBrake > 0f)
                    _bodyInterface?.ActivateBody(state.CarBodyId);
            }
        }

        public bool IsWheelOnGround(VehicleHandle handle, int wheelIndex)
        {
            if (!_vehicleStates.TryGetValue(handle.Value, out var state)) return false;
            var wheel = state.Constraint.GetWheel((uint)wheelIndex);
            if (wheel == null) return false;
            return wheel.HasContact();
        }

        public float GetWheelRotationSpeed(VehicleHandle handle, int wheelIndex)
        {
            if (!_vehicleStates.TryGetValue(handle.Value, out var state)) return 0f;
            var wheel = state.Constraint.GetWheel((uint)wheelIndex);
            if (wheel == null) return 0f;
            return wheel.GetAngularVelocity();
        }

        public int GetWheelCount(VehicleHandle handle)
        {
            if (!_vehicleStates.TryGetValue(handle.Value, out var state)) return 0;
            return state.WheelCount;
        }

        public Matrix4x4 GetWheelWorldTransform(VehicleHandle handle, int wheelIndex)
        {
            if (!_vehicleStates.TryGetValue(handle.Value, out var state))
                return Matrix4x4.Identity;

            using var axisY = new JPH.Vec3(0f, 1f, 0f);
            using var axisX = new JPH.Vec3(1f, 0f, 0f);
            using var mat   = state.Constraint.GetWheelWorldTransform((uint)wheelIndex, axisY, axisX);

            return new Matrix4x4(
                mat.GetColumn4(0).GetX(), mat.GetColumn4(0).GetY(), mat.GetColumn4(0).GetZ(), mat.GetColumn4(0).GetW(),
                mat.GetColumn4(1).GetX(), mat.GetColumn4(1).GetY(), mat.GetColumn4(1).GetZ(), mat.GetColumn4(1).GetW(),
                mat.GetColumn4(2).GetX(), mat.GetColumn4(2).GetY(), mat.GetColumn4(2).GetZ(), mat.GetColumn4(2).GetW(),
                mat.GetColumn4(3).GetX(), mat.GetColumn4(3).GetY(), mat.GetColumn4(3).GetZ(), mat.GetColumn4(3).GetW());
        }

        public Vector3 GetVehiclePosition(VehicleHandle handle)
        {
            if (!_vehicleStates.TryGetValue(handle.Value, out var state)) return Vector3.Zero;
            using var pos = _bodyInterface!.GetPosition(state.CarBodyId);
            return new Vector3(pos.GetX(), pos.GetY(), pos.GetZ());
        }

        public Quaternion GetVehicleRotation(VehicleHandle handle)
        {
            if (!_vehicleStates.TryGetValue(handle.Value, out var state)) return Quaternion.Identity;
            using var rot = _bodyInterface!.GetRotation(state.CarBodyId);
            return new Quaternion(rot.GetX(), rot.GetY(), rot.GetZ(), rot.GetW());
        }

        public PhysicsBodyHandle GetVehicleBodyHandle(VehicleHandle handle)
        {
            if (!_vehicleStates.TryGetValue(handle.Value, out var state)) return PhysicsBodyHandle.Invalid;
            return new PhysicsBodyHandle(state.BodyHandleValue);
        }

        private void StepAllCharacters(float dt)
        {
            if (_physics == null || _charTempAlloc == null) return;

            using var gravVec = _physics.GetGravity();
            var g  = new Vector3(gravVec.GetX(), gravVec.GetY(), gravVec.GetZ());
            var up = Vector3.UnitY;

            using var extSettings = new JPH.CharacterVirtual.ExtendedUpdateSettings();
            using var bpFilter    = _physics.GetDefaultBroadPhaseLayerFilter(ObjLayerMoving);
            using var layerFilter = _physics.GetDefaultLayerFilter(ObjLayerMoving);
            using var bodyFilter  = new JPH.BodyFilter();
            using var shapeFilter = new JPH.ShapeFilter();

            foreach (var state in _charStates.Values)
            {
                var ch = state.Character;

                ch.UpdateGroundVelocity();

                using var curVelJph = ch.GetLinearVelocity();
                var curVel  = new Vector3(curVelJph.GetX(), curVelJph.GetY(), curVelJph.GetZ());
                float vertV = Vector3.Dot(curVel, up);

                bool onGround = ch.GetGroundState() == JPH.CharacterBase.EGroundState.OnGround;

                Vector3 newVel;
                if (onGround && vertV <= 0f)
                {
                    // Follow ground platform velocity (moving platforms work automatically)
                    using var gVelJph = ch.GetGroundVelocity();
                    newVel = new Vector3(gVelJph.GetX(), gVelJph.GetY(), gVelJph.GetZ());
                }
                else
                {
                    // In air: preserve vertical, apply gravity
                    newVel  = vertV * up;
                    newVel += g * dt;
                }

                // Apply caller-supplied horizontal/jump velocity
                using var gNormJph = ch.GetGroundNormal();
                bool tooSteep = onGround && ch.IsSlopeTooSteep(gNormJph);
                if (!tooSteep)
                {
                    newVel.X += state.DesiredVelocity.X;
                    newVel.Z += state.DesiredVelocity.Z;
                    // Jump: caller sets positive Y via SetCharacterLinearVelocity
                    if (state.DesiredVelocity.Y > 0f)
                        newVel.Y = state.DesiredVelocity.Y;
                }

                using var newVelJph = new JPH.Vec3(newVel.X, newVel.Y, newVel.Z);
                ch.SetLinearVelocity(newVelJph);

                ch.ExtendedUpdate(dt, gravVec, extSettings, bpFilter, layerFilter, bodyFilter, shapeFilter, _charTempAlloc);

                // Desired velocity is consumed each physics step; caller refreshes it every frame.
                state.DesiredVelocity = Vector3.Zero;
            }
        }

        private void StepKinematicCharacters(float dt)
        {
            if (_physics == null) return;

            foreach (var state in _kinCharStates.Values)
            {
                var ch = state.Character;

                // PostSimulation refreshes ground-contact state from the previous physics step.
                ch.PostSimulation(state.CollisionTolerance);

                using var curVelJph = ch.GetLinearVelocity();
                var curVel  = new Vector3(curVelJph.GetX(), curVelJph.GetY(), curVelJph.GetZ());

                bool onGround = ch.GetGroundState() == JPH.CharacterBase.EGroundState.OnGround;

                Vector3 newVel;
                if (onGround)
                {
                    // Stick to moving platforms; blend horizontal toward desired.
                    using var gVelJph = ch.GetGroundVelocity();
                    var groundVel = new Vector3(gVelJph.GetX(), gVelJph.GetY(), gVelJph.GetZ());

                    // Smooth blend: 75% current XZ + 25% desired XZ (matches JoltPhysicsDemo).
                    newVel = new Vector3(
                        0.75f * curVel.X + 0.25f * state.DesiredVelocity.X,
                        groundVel.Y,
                        0.75f * curVel.Z + 0.25f * state.DesiredVelocity.Z);

                    // Jump: caller sets positive Y.
                    if (state.DesiredVelocity.Y > 0f)
                        newVel.Y = state.DesiredVelocity.Y;
                }
                else
                {
                    // In air: preserve Y (gravity is applied by the physics system).
                    // Blend XZ toward desired so player has some air control.
                    newVel = new Vector3(
                        0.75f * curVel.X + 0.25f * state.DesiredVelocity.X,
                        curVel.Y,
                        0.75f * curVel.Z + 0.25f * state.DesiredVelocity.Z);
                }

                using var newVelJph = new JPH.Vec3(newVel.X, newVel.Y, newVel.Z);
                ch.SetLinearVelocity(newVelJph);

                state.DesiredVelocity = Vector3.Zero;
            }
        }

        // ---- Contact listener -----------------------------------------------

        unsafe void SetupContactListener()
        {
            _contactListener = new JPH.ContactListenerTrampolineManaged();

            _contactListener.SetOnContactAdded((b1, b2, manifold, settings) =>
            {
                if (_gameListener == null) return;

                uint pA = b1.GetID().GetIndexAndSequenceNumber();
                uint pB = b2.GetID().GetIndexAndSequenceNumber();

                bool aIsSensor = _sensors.Contains(pA);
                bool bIsSensor = _sensors.Contains(pB);

                ContactPoint contact = default;
                if (manifold != null)
                {
                    using var n = manifold.mWorldSpaceNormal;
                    using var o = manifold.mBaseOffset;
                    contact = new ContactPoint(
                        new Vector3(o.GetX(), o.GetY(), o.GetZ()),
                        new Vector3(n.GetX(), n.GetY(), n.GetZ()),
                        0f);
                }

                lock (_pendingLock)
                {
                    if (aIsSensor || bIsSensor)
                        _pending.Add(new CollEv(pA, pB, contact, EvKind.TriggerEnter));
                    else
                        _pending.Add(new CollEv(pA, pB, contact, EvKind.CollisionEnter));
                }
            });

            _contactListener.SetOnContactPersisted((b1, b2, manifold, settings) =>
            {
                if (_gameListener == null) return;

                uint pA = b1.GetID().GetIndexAndSequenceNumber();
                uint pB = b2.GetID().GetIndexAndSequenceNumber();

                bool aIsSensor = _sensors.Contains(pA);
                bool bIsSensor = _sensors.Contains(pB);
                if (aIsSensor || bIsSensor) return;

                ContactPoint contact = default;
                if (manifold != null)
                {
                    using var n = manifold.mWorldSpaceNormal;
                    using var o = manifold.mBaseOffset;
                    contact = new ContactPoint(
                        new Vector3(o.GetX(), o.GetY(), o.GetZ()),
                        new Vector3(n.GetX(), n.GetY(), n.GetZ()),
                        0f);
                }

                lock (_pendingLock)
                {
                    _pending.Add(new CollEv(pA, pB, contact, EvKind.CollisionStay));
                }
            });

            _contactListener.SetOnContactRemoved(pair =>
            {
                if (_gameListener == null) return;

                uint pA = pair.GetBody1ID().GetIndexAndSequenceNumber();
                uint pB = pair.GetBody2ID().GetIndexAndSequenceNumber();

                bool aIsSensor = _sensors.Contains(pA);
                bool bIsSensor = _sensors.Contains(pB);

                lock (_pendingLock)
                {
                    if (aIsSensor || bIsSensor)
                        _pending.Add(new CollEv(pA, pB, default, EvKind.TriggerExit));
                    else
                        _pending.Add(new CollEv(pA, pB, default, EvKind.CollisionExit));
                }
            });

            _contactListener.SetOnContactValidate((b1, b2, baseOffset, collisionResult) =>
            {
                uint pA = b1.GetID().GetIndexAndSequenceNumber();
                uint pB = b2.GetID().GetIndexAndSequenceNumber();
                if (ShouldCollide(pA, pB))
                    return JPH.ValidateResult.AcceptAllContactsForThisBodyPair;
                return JPH.ValidateResult.RejectAllContactsForThisBodyPair;
            });

            _physics!.SetContactListener(_contactListener.Inner);
        }

        int OverlapAabb(JPH.Const_AABox queryAabb, List<PhysicsBodyHandle> results, int maxResults)
        {
            int count = 0;
            foreach (var pair in _handleToBodyId)
            {
                if (count >= maxResults) break;

                using var ts = _bodyInterface!.GetTransformedShape(pair.Value);
                using var bodyAabb = ts.GetWorldSpaceBounds();
                if (!bodyAabb.Overlaps(queryAabb))
                    continue;

                results.Add(new PhysicsBodyHandle(pair.Key));
                count++;
            }
            return count;
        }

        static Vector3 Abs(Vector3 v)
            => new Vector3(MathF.Abs(v.X), MathF.Abs(v.Y), MathF.Abs(v.Z));

        static ushort LayerBit(ushort layer)
            => (ushort)(1u << (layer & 15));

        bool ShouldCollide(uint packedA, uint packedB)
        {
            if (!_packedToLayer.TryGetValue(packedA, out ushort layerA) ||
                !_packedToLayer.TryGetValue(packedB, out ushort layerB))
                return true;

            _packedToMask.TryGetValue(packedA, out ushort maskA);
            _packedToMask.TryGetValue(packedB, out ushort maskB);
            if (maskA == 0) maskA = 0xFFFF;
            if (maskB == 0) maskB = 0xFFFF;

            bool aAcceptsB = (maskA & LayerBit(layerB)) != 0;
            bool bAcceptsA = (maskB & LayerBit(layerA)) != 0;
            return aAcceptsB && bAcceptsA;
        }

        void DispatchPendingEvents()
        {
            if (_gameListener == null) return;

            List<CollEv> toDispatch;
            lock (_pendingLock)
            {
                if (_pending.Count == 0) return;
                toDispatch = new List<CollEv>(_pending);
                _pending.Clear();
            }

            foreach (var ev in toDispatch)
            {
                _packedToHandle.TryGetValue(ev.PackedA, out int hA);
                _packedToHandle.TryGetValue(ev.PackedB, out int hB);
                var a = new PhysicsBodyHandle(hA);
                var b = new PhysicsBodyHandle(hB);

                switch (ev.Kind)
                {
                    case EvKind.CollisionEnter:  _gameListener.OnCollisionEnter(a, b, ev.Contact); break;
                    case EvKind.CollisionStay:   _gameListener.OnCollisionStay(a, b);               break;
                    case EvKind.CollisionExit:   _gameListener.OnCollisionExit(a, b);              break;
                    case EvKind.TriggerEnter:    _gameListener.OnTriggerEnter(a, b);               break;
                    case EvKind.TriggerExit:     _gameListener.OnTriggerExit(a, b);                break;
                }
            }
        }
    }
}
