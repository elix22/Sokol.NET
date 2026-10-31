using System.Numerics;
using Frent;
using GameEditor.Framework.Core;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Scene;
using Xunit;

namespace Scene.Tests
{
    /// <summary>ECSWorld.Detach / Reattach / Discard (editor undo of entity creation) and UndoStack's discard hook.</summary>
    public sealed class DetachTests
    {
        static readonly ECSWorld World = ECSWorld.Instance;

        public DetachTests() { World.Clear(); UndoStack.Clear(); }

        static Entity Make(GameEditor.Framework.Scene.Scene s, string name, Entity? parent = null, Vector3 pos = default)
        {
            var e = s.CreateEntity(name);
            var tr = World.GetComponent<Transform>(e);
            tr.Position = pos;
            tr.Parent = parent;
            World.AddComponent(e, tr);
            return e;
        }

        // A, B under A, C under B, then D (unrelated, created after) and E under A (after D), so B's subtree
        // sits in the middle of the entity list and A keeps another child.
        static (GameEditor.Framework.Scene.Scene s, Entity a, Entity b, Entity c, Entity d, Entity e) Build()
        {
            var s = new GameEditor.Framework.Scene.Scene("t");
            var a = Make(s, "A", null, new Vector3(1, 2, 3));
            var b = Make(s, "B", a, new Vector3(0, 1, 0));
            World.AddComponent(b, new MeshRenderer { MeshPath = "builtin:box", Visible = true });
            World.AddComponent(b, new TestLink { Target = a, Gain = 2.5f });
            var c = Make(s, "C", b, new Vector3(0, 0, 4));
            World.AddComponent(c, new LightComponent { Type = LightType.Point, Intensity = 3f, Enabled = true });
            var d = Make(s, "D");
            var e = Make(s, "E", a);
            return (s, a, b, c, d, e);
        }

        [Fact]
        public void DetachThenReattach_GivesTheSameSceneFile_HandlesAndIds()
        {
            var (s, a, b, c, d, _) = Build();
            string before = SceneSerializer.Serialize(s);
            int bId = World.GetSceneId(b), cId = World.GetSceneId(c);

            var det = World.Detach(b)!;
            Assert.NotNull(det);
            Assert.Equal(bId, det.RootSceneId);
            Assert.True(b.IsAlive && c.IsAlive);                                  // kept alive …
            Assert.DoesNotContain(b, World.Entities);                             // … but out of the scene
            Assert.DoesNotContain(c, World.Entities);
            Assert.False(World.TryGetEntityBySceneId(bId, out _));
            Assert.False(World.HasComponent<NameTag>(b));
            Assert.Empty(World.GetChildren(b));
            Assert.DoesNotContain(b, World.GetChildren(a));
            Assert.DoesNotContain(World.GetAllComponents<LightComponent>(), l => l.Intensity == 3f);
            string during = SceneSerializer.Serialize(s);
            Assert.DoesNotContain("\"B\"", during);
            Assert.DoesNotContain("\"C\"", during);

            Assert.True(World.Reattach(det));
            Assert.Equal(before, SceneSerializer.Serialize(s));                   // order, ids, components, nextId
            Assert.Equal(bId, World.GetSceneId(b));
            Assert.Equal(cId, World.GetSceneId(c));
            Assert.True(World.TryGetEntityBySceneId(cId, out var c2) && c2 == c);
            Assert.Equal(2.5f, World.GetComponent<TestLink>(b).Gain);
            Assert.Contains(b, World.GetChildren(a));                             // child index rebuilt
            Assert.Contains(c, World.GetChildren(b));

            World.DestroyEntity(a);                                               // and a delete still cascades through it
            Assert.False(b.IsAlive || c.IsAlive);
            Assert.True(d.IsAlive);
        }

        [Fact]
        public void IdNotHandedOutYet_IsReservedByDetach_AndNotGivenToANewcomer()
        {
            var s = new GameEditor.Framework.Scene.Scene("t");
            var x = Make(s, "X");                                                 // never saved: no scene id yet
            var det = World.Detach(x)!;
            var y = Make(s, "Y");
            Assert.NotEqual(det.RootSceneId, World.GetSceneId(y));
            Assert.True(World.Reattach(det));
            Assert.Equal(det.RootSceneId, World.GetSceneId(x));
        }

        [Fact]
        public void Discard_DeletesForGood_AndIsANoOpAfterAWorldClear()
        {
            var (_, _, b, c, _, _) = Build();
            var det = World.Detach(b)!;
            World.Discard(det);
            Assert.False(b.IsAlive || c.IsAlive);

            var (s2, _, b2, _, _, _) = Build();
            var det2 = World.Detach(b2)!;
            World.Clear();
            var fresh = new List<Entity>();
            for (int i = 0; i < 8; i++) fresh.Add(Make(s2, "New" + i));            // fills the slots the old handles used
            Assert.False(World.Reattach(det2));
            World.Discard(det2);
            Assert.All(fresh, f => Assert.True(f.IsAlive));
            Assert.Equal(8, World.Entities.Count);
        }

        [Fact]
        public void Reattach_Refuses_WhenTheParentIsGone()
        {
            var (_, a, b, _, _, _) = Build();
            var det = World.Detach(b)!;
            World.DestroyEntity(a);
            Assert.False(World.Reattach(det));
        }

        sealed class Probe : IDiscardableCommand
        {
            public int Discards;
            public string Description => "probe";
            public void Execute() { }
            public void Undo() { }
            public void Discard() => Discards++;
        }

        [Fact]
        public void UndoStack_DiscardsCommandsThatLeaveTheHistory()
        {
            var p1 = new Probe();
            UndoStack.RecordAlreadyExecuted(p1);
            UndoStack.Undo(); UndoStack.Redo(); UndoStack.Undo();
            Assert.Equal(0, p1.Discards);                                         // undo/redo keep it
            UndoStack.RecordAlreadyExecuted(new DelegateCommand("x", () => { }, () => { }));
            Assert.Equal(1, p1.Discards);                                         // redo stack cleared by a new action

            var p2 = new Probe();
            UndoStack.RecordAlreadyExecuted(p2);
            UndoStack.Clear();
            Assert.Equal(1, p2.Discards);

            var p3 = new Probe();
            UndoStack.RecordAlreadyExecuted(p3);
            for (int i = 0; i < 256; i++) UndoStack.RecordAlreadyExecuted(new DelegateCommand("x", () => { }, () => { }));
            Assert.Equal(1, p3.Discards);                                         // trimmed from the bottom
        }
    }
}
