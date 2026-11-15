using System;
using System.Collections.Generic;
using Frent;
using GameEditor.Framework.ECS;
using GameEditor.Framework.Scripting;
using Xunit;

namespace Scene.Tests
{
    /// <summary>§6 (examples/GameEditor/docs/EDITOR_ENHANCEMENTS_DESIGN.md): scripts that work together - GetScript&lt;T&gt;,
    /// typed events that end with their script, a state machine.</summary>
    public sealed class ScriptApiTests : IDisposable
    {
        public readonly record struct Damaged(int Amount);
        public readonly record struct Healed(int Amount);

        public sealed class Health : GameBehaviour
        {
            public int Points = 10;
            public void Listen() => On<Damaged>(e => Points -= e.Amount);
        }

        public sealed class Mover : GameBehaviour
        {
            public Health? Mine() => GetScript<Health>();
            public Health? Theirs(Entity e) => GetScript<Health>(e);
            public bool Has(Entity e) => TryGetScript<Health>(e, out _);
        }

        // The editor's Play wraps a game's script: found as itself through the proxy.
        sealed class Proxy : GameBehaviour, IScriptProxy
        {
            readonly object _script;
            public Proxy(object script) { _script = script; }
            public object Script => _script;
        }

        static readonly ECSWorld World = ECSWorld.Instance;

        public ScriptApiTests()
        {
            ScriptSystem.StopAll();
            World.Clear();
        }

        public void Dispose()
        {
            ScriptSystem.StopAll();
            World.Clear();
        }

        static T Add<T>(Entity e, T script) where T : GameBehaviour
        {
            ScriptSystem.Register(e, script);
            return script;
        }

        [Fact]
        public void A_script_finds_its_own_entity_s_scripts_and_another_s()
        {
            Entity a = World.CreateEntity(), b = World.CreateEntity(), c = World.CreateEntity();
            var mover = Add(a, new Mover());
            var health = Add(a, new Health());
            var other = new Health();
            Add(b, new Proxy(other));
            Assert.Same(health, mover.Mine());
            Assert.Same(other, mover.Theirs(b));             // through the editor's wrapper
            Assert.Null(mover.Theirs(c));
            Assert.False(mover.Has(c));
            var all = new List<Health>();
            Add(a, new Health());
            Assert.Equal(2, ScriptSystem.FindAll(a, all));
        }

        [Fact]
        public void A_script_s_handlers_end_with_it()
        {
            Entity a = World.CreateEntity(), b = World.CreateEntity();
            var h1 = Add(a, new Health());
            var h2 = Add(b, new Health());
            h1.Listen();
            h2.Listen();
            int global = 0;
            Events.On<Damaged>(e => global += e.Amount);
            Events.Publish(new Damaged(3));
            Assert.Equal((7, 7, 3), (h1.Points, h2.Points, global));
            ScriptSystem.RemoveEntity(a);                     // a leaves the scene: its handler with it
            Events.Publish(new Damaged(2));
            Assert.Equal((7, 5, 5), (h1.Points, h2.Points, global));
            Assert.Equal(2, Events.Listeners<Damaged>());
            ScriptSystem.StopAll();                           // Play stops: none outlives the game
            Assert.Equal(0, Events.Listeners<Damaged>());
            Assert.Equal(0, Events.Listeners<Healed>());
        }

        [Fact]
        public void A_handler_taken_away_by_another_is_not_called_one_added_waits_for_the_next()
        {
            var order = new List<string>();
            Action<Healed>? second = null;
            Events.On<Healed>(e => { order.Add("first"); Events.Off(second!); Events.On<Healed>(_ => order.Add("late")); });
            second = Events.On<Healed>(e => order.Add("second"));
            Events.On<Healed>(e => throw new InvalidOperationException("a handler that throws"));
            Events.On<Healed>(e => order.Add("third"));
            Events.Publish(new Healed(1));
            Assert.Equal(new[] { "first", "third" }, order);  // second taken away before its turn; the thrower logged
            order.Clear();
            Events.Clear();
            Events.On<Healed>(e => order.Add("x"));
            Events.Publish(new Healed(1));
            Assert.Equal(new[] { "x" }, order);
        }

        public sealed class Judge : GameBehaviour
        {
            public Health? Target;
            public List<Health> Squad = new();
            public Health[] Pair = Array.Empty<Health>();
        }

        // The setter as the builder writes it for Judge.
        static bool SetJudge(GameBehaviour script, string field, in ScriptFieldValue value)
        {
            var s = (Judge)script;
            switch (field)
            {
                case "Target": return ScriptValue.SetScript(ref s.Target, value);
                case "Squad": return ScriptValue.SetScripts(ref s.Squad, value);
                case "Pair": return ScriptValue.SetScripts(ref s.Pair, value);
                default: return false;
            }
        }

        [Fact]
        public void A_field_of_a_script_s_type_is_that_entity_s_script_made_after_it_or_before_on_both_ways()
        {
            foreach (bool generated in new[] { true, false })
            {
                ScriptSystem.StopAll();
                ScriptSystem.ClearFactories();
                World.Clear();
                if (generated) ScriptSystem.RegisterType<Judge>(SetJudge); else ScriptSystem.RegisterType<Judge>();
                ScriptSystem.RegisterType<Health>();
                Entity judge = World.CreateEntity(), a = World.CreateEntity(), b = World.CreateEntity(), none = World.CreateEntity();
                // The judge is made first: the scripts it points to come after it.
                World.AddComponent(judge, new GameEditor.Framework.ECS.Components.ScriptComponent
                {
                    TypeName = nameof(Judge),
                    Properties = new() { ["Squad"] = "[null,null]", ["Pair"] = "[null,null]" },
                    Refs = new() { ["Target"] = b, ["Squad[0]"] = a, ["Squad[1]"] = b, ["Pair[0]"] = none, ["Pair[1]"] = a },
                });
                World.AddComponent(a, new GameEditor.Framework.ECS.Components.ScriptComponent { TypeName = nameof(Health) });
                World.AddComponent(b, new GameEditor.Framework.ECS.Components.ScriptComponent { TypeName = nameof(Health) });
                ScriptSystem.PopulateFromScene(World);
                var j = ScriptSystem.Find<Judge>(judge)!;
                Health ha = ScriptSystem.Find<Health>(a)!, hb = ScriptSystem.Find<Health>(b)!;
                Assert.Same(hb, j.Target);
                Assert.Equal(2, j.Squad.Count);
                Assert.Same(ha, j.Squad[0]);
                Assert.Same(hb, j.Squad[1]);
                Assert.Null(j.Pair[0]);                       // an entity without the script: none
                Assert.Same(ha, j.Pair[1]);
            }
            ScriptSystem.ClearFactories();
        }

        enum Mode { Idle, Chase, Attack }

        [Fact]
        public void A_state_machine_enters_updates_and_leaves_its_states_in_order()
        {
            var log = new List<string>();
            var sm = new StateMachine<Mode>(Mode.Idle);
            sm.In(Mode.Idle, enter: () => log.Add("enter Idle"), update: dt => log.Add("Idle"), exit: () => log.Add("exit Idle"))
              .In(Mode.Chase, enter: () => log.Add("enter Chase"), update: dt => { log.Add("Chase"); if (sm.TimeInState > 0.25f) sm.Go(Mode.Attack); },
                  exit: () => log.Add("exit Chase"));
            sm.Update(0.1f);
            Assert.Equal(new[] { "enter Idle", "Idle" }, log);
            log.Clear();
            sm.Go(Mode.Chase);
            Assert.True(sm.Is(Mode.Chase));
            Assert.Equal(Mode.Idle, sm.Previous);
            Assert.Equal(0f, sm.TimeInState);
            sm.Update(0.2f);
            sm.Update(0.2f);
            Assert.Equal(new[] { "exit Idle", "enter Chase", "Chase", "Chase", "exit Chase" }, log);
            Assert.Equal(Mode.Attack, sm.State);              // a state with no handlers is fine
            Assert.Equal(0f, sm.TimeInState);
        }
    }
}
