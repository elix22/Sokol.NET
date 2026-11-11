using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Frent;
using GameEditor.Framework.Core;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Physics;
using GameEditor.Framework.Scene;
using Xunit;

namespace Scene.Tests
{
    /// <summary>
    /// Editing one entity of an open scene in the scene-file format: SceneSerializer.SerializeEntity /
    /// ReadComponents, and ECSWorld.Capture / Restore (the undo of such an edit).
    /// </summary>
    public sealed class ComponentEditTests
    {
        static readonly ECSWorld World = ECSWorld.Instance;

        public ComponentEditTests()
        {
            RuntimeHelpers.RunClassConstructor(typeof(SceneHookTests).TypeHandle);   // registers the "TestLink" extension
            World.Clear();
            UndoStack.Clear();
        }

        static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

        // Ground (static body), Crate under Ground (dynamic body, two scripts, a link to Ground), Sun, Cam, Hinge.
        static (GameEditor.Framework.Scene.Scene s, Entity ground, Entity crate, Entity sun, Entity cam, Entity hinge) Build()
        {
            var s = new GameEditor.Framework.Scene.Scene("t");
            var ground = s.CreateEntity("Ground");
            World.AddComponent(ground, new MeshRenderer { MeshPath = "prim:plane", MaterialPath = "", Visible = true, ReceivesShadows = true });
            World.AddComponent(ground, new RigidbodyComponent { MotionType = RigidbodyMotionType.Static, Mass = 1f, Shapes = [ShapeEntry.Default()], Layer = 1, LayerMask = 0xFFFF });

            var crate = s.CreateEntity("Crate");
            var tr = World.GetComponent<Transform>(crate);
            tr.Position = new Vector3(0.1f, 2.5f, -3f);
            tr.Rotation = Quaternion.CreateFromYawPitchRoll(0.3f, 0.2f, 0.1f);
            tr.Parent = ground;
            World.AddComponent(crate, tr);
            World.AddComponent(crate, new MeshRenderer { MeshPath = "prim:box", MaterialPath = "", Visible = true, CastsShadows = true });
            World.AddComponent(crate, new RigidbodyComponent { MotionType = RigidbodyMotionType.Dynamic, Mass = 2.5f, UseGravity = true, Friction = 0.4f, Shapes = [ShapeEntry.Default()], Layer = 2, LayerMask = 0x00FF });
            World.AddComponent(crate, new ScriptComponent { TypeName = "Spin", Properties = new() { ["Speed"] = "3" } });
            World.AddComponent(crate, new ScriptCollectionComponent { Scripts = [new ScriptComponent { TypeName = "Bob" }] });
            World.AddComponent(crate, new TestLink { Target = ground, Gain = 1.5f });

            var sun = s.CreateEntity("Sun");
            World.AddComponent(sun, new LightComponent { Type = LightType.Directional, Color = new Vector3(1f, 0.9f, 0.8f), Intensity = 2f, Range = 10f, InnerAngle = 25f, OuterAngle = 35f, CastsShadows = true, Enabled = true });
            var cam = s.CreateEntity("Cam");
            World.AddComponent(cam, new CameraComponent { Fov = 55f, NearZ = 0.1f, FarZ = 500f, IsMain = true, OrthoSize = 5f, Enabled = true });
            var hinge = s.CreateEntity("Hinge");
            World.AddComponent(hinge, new ConstraintComponent { Type = ConstraintType.Hinge, BodyA = ground, BodyB = crate, LocalAxisA = Vector3.UnitY, LocalAxisB = Vector3.UnitY, MinLimit = -1f, MaxLimit = 1f });
            return (s, ground, crate, sun, cam, hinge);
        }

        [Fact]
        public void SerializeEntity_IsTheEntitysEntryInTheSceneFile()
        {
            var (s, _, crate, _, _, _) = Build();
            using var doc = JsonDocument.Parse(SceneSerializer.Serialize(s));
            int id = World.GetSceneId(crate);
            foreach (var ent in doc.RootElement.GetProperty("entities").EnumerateArray())
                if (ent.GetProperty("id").GetInt32() == id)
                {
                    Assert.Equal(ent.GetRawText(), SceneSerializer.SerializeEntity(crate));
                    return;
                }
            Assert.Fail("entity not in the scene file");
        }

        [Fact]
        public void ReadingAnEntitysOwnBlocksBack_ChangesNothing()
        {
            var (s, ground, crate, sun, cam, hinge) = Build();
            string before = SceneSerializer.Serialize(s);
            foreach (var e in new[] { ground, crate, sun, cam, hinge })
                SceneSerializer.ReadComponents(e, Json(SceneSerializer.SerializeEntity(e)).GetProperty("c"));
            Assert.Equal(before, SceneSerializer.Serialize(s));
            Assert.Equal(ground, World.GetComponent<Transform>(crate).Parent);          // references resolved by scene id
            Assert.Equal(crate, World.GetComponent<ConstraintComponent>(hinge).BodyB);
            Assert.Equal(ground, World.GetComponent<TestLink>(crate).Target);
            Assert.Equal([crate], World.GetChildren(ground));
        }

        [Fact]
        public void ReadComponents_ReplacesOnlyTheBlocksGiven()
        {
            var (_, _, crate, sun, _, _) = Build();
            SceneSerializer.ReadComponents(sun, Json("""{"Light":{"Type":"Point","Color":[0,1,0],"Int":7,"Range":4}}"""));
            var lt = World.GetComponent<LightComponent>(sun);
            Assert.Equal(LightType.Point, lt.Type);
            Assert.Equal(7f, lt.Intensity);
            Assert.Equal("Sun", World.GetComponent<NameTag>(sun).Name);                 // not named → untouched

            SceneSerializer.ReadComponents(crate, Json("""{"NameTag":{"Name":"Box"},"Scripts":[]}"""));
            Assert.Equal("Box", World.GetComponent<NameTag>(crate).Name);
            Assert.False(World.HasComponent<ScriptCollectionComponent>(crate));         // an emptied list removes the collection
            Assert.Equal(2.5f, World.GetComponent<RigidbodyComponent>(crate).Mass);
        }

        [Fact]
        public void ReadComponents_TransformParent_FollowsTheBlock_AndTheChildIndex()
        {
            var (_, ground, crate, sun, _, _) = Build();
            int sunId = World.GetSceneId(sun);
            SceneSerializer.ReadComponents(crate, Json("""{"Transform":{"P":[1,2,3],"E":[0,0,0],"S":[1,1,1],"Par":SUN}}""".Replace("SUN", sunId.ToString())));
            Assert.Equal(sun, World.GetComponent<Transform>(crate).Parent);
            Assert.Empty(World.GetChildren(ground));
            Assert.Equal([crate], World.GetChildren(sun));

            SceneSerializer.ReadComponents(crate, Json("""{"Transform":{"P":[1,2,3],"E":[0,0,0],"S":[1,1,1]}}"""));   // no "Par" = a root
            Assert.Null(World.GetComponent<Transform>(crate).Parent);
            Assert.Empty(World.GetChildren(sun));
        }

        [Fact]
        public void ReadComponents_UnknownKey_ThrowsBeforeChangingAnything()
        {
            var (s, _, crate, _, _, _) = Build();
            string before = SceneSerializer.Serialize(s);
            Assert.Throws<ArgumentException>(() =>
                SceneSerializer.ReadComponents(crate, Json("""{"NameTag":{"Name":"X"},"Rigidbdy":{"Mass":9}}""")));
            Assert.Equal(before, SceneSerializer.Serialize(s));
            Assert.True(SceneSerializer.IsComponentKey("Rigidbody"));
            Assert.True(SceneSerializer.IsComponentKey("TestLink"));
            Assert.False(SceneSerializer.IsComponentKey("Rigidbdy"));
        }

        [Fact]
        public void Restore_BringsBackTheCapturedComponents_AfterAddRemoveChangeAndReparent()
        {
            var (s, ground, crate, sun, _, _) = Build();
            string before = SceneSerializer.Serialize(s);
            var snap = World.Capture(crate);

            World.RemoveComponent<RigidbodyComponent>(crate);                           // removed
            World.AddComponent(crate, new LightComponent { Type = LightType.Spot });    // added
            var tr = World.GetComponent<Transform>(crate);                              // changed + reparented
            tr.Position = Vector3.One;
            tr.Parent = sun;
            World.AddComponent(crate, tr);
            Assert.NotEqual(before, SceneSerializer.Serialize(s));
            var after = World.Capture(crate);

            World.Restore(crate, snap);
            Assert.Equal(before, SceneSerializer.Serialize(s));
            Assert.False(World.HasComponent<LightComponent>(crate));
            Assert.Equal([crate], World.GetChildren(ground));
            Assert.Empty(World.GetChildren(sun));

            World.Restore(crate, after);                                                // and forward again (redo)
            Assert.True(World.HasComponent<LightComponent>(crate));
            Assert.False(World.HasComponent<RigidbodyComponent>(crate));
            Assert.Equal([crate], World.GetChildren(sun));
            Assert.Empty(World.GetChildren(ground));
        }

        [Fact]
        public void UndoStack_TellsWhatUndoAndRedoWouldDo()
        {
            Assert.Null(UndoStack.UndoDescription);
            Assert.Null(UndoStack.RedoDescription);
            UndoStack.Record(new DelegateCommand("First", () => { }, () => { }));
            UndoStack.Record(new DelegateCommand("Second", () => { }, () => { }));
            Assert.Equal("Second", UndoStack.UndoDescription);
            UndoStack.Undo();
            Assert.Equal("First", UndoStack.UndoDescription);
            Assert.Equal("Second", UndoStack.RedoDescription);
            UndoStack.Clear();
        }
    }
}
