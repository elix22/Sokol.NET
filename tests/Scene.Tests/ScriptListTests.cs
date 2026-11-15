using System;
using System.Collections.Generic;
using System.Numerics;
using Frent;
using GameEditor.Framework.ECS;
using GameEditor.Framework.Scripting;
using Xunit;

namespace Scene.Tests
{
    /// <summary>
    /// §8 (examples/GameEditor/docs/EDITOR_ENHANCEMENTS_DESIGN.md): a script's List&lt;T&gt; and T[] fields from a scene's
    /// text - one JSON array, each element the text a field of T has - set with no reflection (ScriptValue, what a game
    /// compiled ahead of time uses) and by the field's name (the editor's Play, the web page): the same text, the same list.
    /// An entity list's length is its text ("[null,null]"), its elements references "Targets[0]", "Targets[1]" ….
    /// </summary>
    public class ScriptListTests
    {
        public enum Gait { Walk, Run, Fly }

        public sealed class Probe : GameBehaviour
        {
            public List<float> Speeds = new();
            public int[] Counts = Array.Empty<int>();
            public List<string> Lines = new();
            public List<bool> Ticks = new();
            public List<Vector3> Points = new();
            public Gait[] Gaits = Array.Empty<Gait>();
            public List<Gait> GaitList = new();
            public List<Quaternion> Turns = new();
            public List<Entity> Targets = new();
            public Entity[] Others = Array.Empty<Entity>();
            public List<GameEditor.Framework.Scene.Prefab> Spawns = new();
        }

        // The setter as the builder writes it: the list's overloads picked by the field's type.
        static bool Set(GameBehaviour script, string field, in ScriptFieldValue value)
        {
            var s = (Probe)script;
            switch (field)
            {
                case "Speeds": return ScriptValue.Set(ref s.Speeds, value);
                case "Counts": return ScriptValue.Set(ref s.Counts, value);
                case "Lines": return ScriptValue.Set(ref s.Lines, value);
                case "Ticks": return ScriptValue.Set(ref s.Ticks, value);
                case "Points": return ScriptValue.Set(ref s.Points, value);
                case "Gaits": return ScriptValue.SetEnums(ref s.Gaits, value);
                case "GaitList": return ScriptValue.SetEnums(ref s.GaitList, value);
                case "Turns": return ScriptValue.Set(ref s.Turns, value);
                case "Targets": return ScriptValue.Set(ref s.Targets, value);
                case "Others": return ScriptValue.Set(ref s.Others, value);
                case "Spawns": return ScriptValue.Set(ref s.Spawns, value);
                default: return false;
            }
        }

        static readonly Dictionary<string, string> Texts = new()
        {
            ["Speeds"] = "[1.5,2,-0.25]",
            ["Counts"] = "[3,0,7]",
            ["Lines"] = "[\"one\",\"two \\\"quoted\\\"\",\"\"]",
            ["Ticks"] = "[true,false,true]",
            ["Points"] = "[[0,0,1],[2,0.5,1]]",
            ["Gaits"] = "[\"Walk\",\"fly\"]",
            ["GaitList"] = "[\"Run\"]",
            ["Turns"] = "[[0,0.70710677,0,0.70710677]]",
            ["Targets"] = "[null,null,null]",
            ["Others"] = "[null,null]",
        };

        static void Want(Probe p)
        {
            Assert.Equal(new List<float> { 1.5f, 2f, -0.25f }, p.Speeds);
            Assert.Equal(new[] { 3, 0, 7 }, p.Counts);
            Assert.Equal(new List<string> { "one", "two \"quoted\"", "" }, p.Lines);
            Assert.Equal(new List<bool> { true, false, true }, p.Ticks);
            Assert.Equal(new List<Vector3> { new(0, 0, 1), new(2, 0.5f, 1) }, p.Points);
            Assert.Equal(new[] { Gait.Walk, Gait.Fly }, p.Gaits);
            Assert.Equal(new List<Gait> { Gait.Run }, p.GaitList);
            Assert.Single(p.Turns);
            Assert.Equal(0.70710677f, p.Turns[0].Y, 5);
            Assert.Equal(3, p.Targets.Count);
            Assert.Equal(2, p.Others.Length);
        }

        [Fact]
        public void Every_kind_of_list_is_set_with_no_reflection()
        {
            var p = new Probe();
            foreach (var (field, text) in Texts) Assert.True(Set(p, field, new ScriptFieldValue(text)), field);
            Want(p);
        }

        [Fact]
        public void By_the_field_s_name_the_same_text_is_the_same_list()
        {
            var p = new Probe();
            p.ApplySerializedProperties(new Dictionary<string, string>(Texts));
            Want(p);
        }

        [Fact]
        public void An_entity_list_s_elements_come_as_its_references()
        {
            var world = ECSWorld.Instance;
            world.Clear();
            Entity a = world.CreateEntity(), b = world.CreateEntity();
            var p = new Probe();
            Assert.True(Set(p, "Targets", new ScriptFieldValue("[null,null,null]")));
            Assert.True(Set(p, "Targets", new ScriptFieldValue(b, 0)));
            Assert.True(Set(p, "Targets", new ScriptFieldValue(a, 2)));
            Assert.True(Set(p, "Others", new ScriptFieldValue(a, 3)));      // past its end: made long enough
            Assert.Equal(new List<Entity> { b, default, a }, p.Targets);
            Assert.Equal(4, p.Others.Length);
            Assert.Equal(a, p.Others[3]);

            var q = new Probe();
            q.ApplySerializedProperties(new Dictionary<string, string> { ["Targets"] = "[null,null,null]", ["Others"] = "[null]" });
            q.ApplySerializedReferences(new Dictionary<string, Entity> { ["Targets[0]"] = b, ["Targets[2]"] = a, ["Others[0]"] = a, ["Others[5]"] = b });
            Assert.Equal(new List<Entity> { b, default, a }, q.Targets);
            Assert.Equal(new[] { a }, q.Others);                              // by name: an element past the text's length is not made
            world.Clear();
        }

        [Fact]
        public void What_is_no_list_of_the_element_s_type_is_refused_and_the_field_kept()
        {
            var p = new Probe { Speeds = new() { 9f } };
            Assert.False(Set(p, "Speeds", new ScriptFieldValue("[1,\"x\"]")));
            Assert.False(Set(p, "Speeds", new ScriptFieldValue("1.5")));
            Assert.False(Set(p, "Gaits", new ScriptFieldValue("[\"Swim\"]")));
            Assert.False(Set(p, "Points", new ScriptFieldValue("[[1,2]]")));
            Assert.False(Set(p, "Speeds", new ScriptFieldValue(default(Entity), 0)));   // a reference for a list of numbers
            Assert.Equal(new List<float> { 9f }, p.Speeds);
        }

        [Fact]
        public void A_prefab_that_is_none_or_not_found_is_an_empty_slot_on_both_ways()
        {
            var p = new Probe();
            Assert.True(Set(p, "Spawns", new ScriptFieldValue("[\"\",\"Prefabs/Missing.prefab\"]")));
            Assert.Equal(2, p.Spawns.Count);
            Assert.All(p.Spawns, x => Assert.Null(x));
            var q = new Probe();
            q.ApplySerializedProperties(new Dictionary<string, string> { ["Spawns"] = "[\"\",\"Prefabs/Missing.prefab\"]" });
            Assert.Equal(2, q.Spawns.Count);
        }

        [Fact]
        public void Elements_are_each_element_s_own_text()
        {
            Assert.Equal(new List<string?> { "1.5", "true", "a,b", "[0,1.5,2]", null }, ScriptValue.Elements("[1.5, true, \"a,b\", [0, 1.5, 2], null]"));
            Assert.Empty(ScriptValue.Elements("[]")!);
            Assert.Null(ScriptValue.Elements("{\"a\":1}"));
            Assert.Null(ScriptValue.Elements("[1,"));
            Assert.Equal(("Targets", 12), ScriptValue.ElementKey("Targets[12]"));
            Assert.Equal(("Target", -1), ScriptValue.ElementKey("Target"));
            Assert.Equal(("A[x]", -1), ScriptValue.ElementKey("A[x]"));
        }
    }
}
