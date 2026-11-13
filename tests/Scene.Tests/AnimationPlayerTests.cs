using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using GameEditor.Framework.Animation;
using GameEditor.Framework.Assets;
using GameEditor.Framework.Core;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Scene;
using Xunit;

namespace Scene.Tests
{
    /// <summary>
    /// G6 A2 (ANIMATION_SYSTEM_IMPLEMENTATION_PLAN.md §18): an animation player — a clip played after the scripts,
    /// in game time, wrapped by the clip's wrap mode, its events given to the entity's scripts; the scene block.
    /// </summary>
    [Collection("ClipLibrary")]
    public sealed class AnimationPlayerTests : IDisposable
    {
        static readonly ECSWorld World = ECSWorld.Instance;
        readonly List<string> _warnings = new();
        readonly List<(float Time, string Fn)> _events = new();
        readonly Dictionary<string, string> _files = new();
        float _clock;

        public AnimationPlayerTests()
        {
            World.Clear();
            SceneManager.NewScene("Players");
            ClipLibrary.Clear();
            ClipLibrary.Reader = key => _files.TryGetValue(key, out var t) ? t : null;
            AnimationSystem.Reset();
            AnimationSystem.EventSink = (e, ev) => _events.Add((_clock, ev.FunctionName));
            Logger.OnLog += OnLog;
        }

        public void Dispose()
        {
            Logger.OnLog -= OnLog;
            ClipLibrary.Reader = null;
            ClipLibrary.Clear();
            AnimationSystem.EventSink = null;
            AnimationSystem.Reset();
        }

        void OnLog(LogLevel level, string msg)
        {
            if (level == LogLevel.Warning) _warnings.Add(msg);
        }

        // x straight from 0 to 3 over 1.5 s; an event at 0.5 s.
        void Clip(string key, string wrap) => _files[key] = $$"""
            { "sokolnetAnim": 1, "name": "Slide", "length": 1.5, "wrap": "{{wrap}}",
              "curves": [ { "path": "", "component": "Transform", "property": "LocalPosition.x",
                            "keys": [ { "t": 0, "v": 0, "in": 2, "out": 2, "mi": "Free", "mo": "Free" },
                                      { "t": 1.5, "v": 3, "in": 2, "out": 2, "mi": "Free", "mo": "Free" } ] } ],
              "events": [ { "t": 0.5, "fn": "Half", "s": "", "f": 0, "i": 0 } ] }
            """;

        Frent.Entity Door(string clip, bool auto = true, float speed = 1f)
        {
            var e = World.CreateEntity("Door");
            var p = AnimationPlayer.Default;
            p.Clip = clip;
            p.PlayAutomatically = auto;
            p.Playing = auto;
            p.Speed = speed;
            World.AddComponent(e, p);
            return e;
        }

        float X(Frent.Entity e) => World.GetComponent<Transform>(e).Position.X;
        ref AnimationPlayer P(Frent.Entity e) => ref World.GetComponent<AnimationPlayer>(e);

        void Run(float seconds, float step = 0.1f)
        {
            for (float t = 0; t < seconds - 1e-4f; t += step)
            {
                _clock += step;
                AnimationSystem.Update(World, step);
            }
        }

        static void Near(float want, float got, float tol = 1e-3f) => Assert.True(MathF.Abs(want - got) <= tol, $"want {want}, got {got}");

        [Fact]
        public void Once_PlaysToTheEnd_StopsThere_StartsOverWhenPlayedAgain()
        {
            Clip("A/slide.anim.json", "Once");
            var door = Door("A/slide.anim.json");
            AnimationSystem.Update(World, 0.1f);                     // the first frame: the clip at 0
            Near(0, X(door));
            Run(0.5f);
            Near(1, X(door));                                         // x = 2 × time
            Assert.Single(_events);
            Run(1.5f);
            Near(3, X(door));
            Assert.False(P(door).Playing);                            // Once: stopped, the last pose kept
            var t = World.GetComponent<Transform>(door);
            t.Position.X = 7;
            World.AddComponent(door, t);
            Run(0.5f);
            Near(7, X(door));                                         // standing: what a script does stays
            P(door).Playing = true;
            Run(0.2f);
            Near(0.4f, X(door));                                      // over again from 0
        }

        [Fact]
        public void Loop_GoesRound_TheEventOncePerRound_EvenWhenAFrameSkipsARound()
        {
            Clip("A/loop.anim.json", "Loop");
            var door = Door("A/loop.anim.json");
            AnimationSystem.Update(World, 0.1f);
            Run(1.7f);
            Near(0.4f, X(door));                                      // 1.7 s: 0.2 s into the second round
            Near(1.7f, P(door).Time);                                 // the player's time keeps counting
            Assert.Single(_events);
            AnimationSystem.Update(World, 3.1f);                      // one long frame: 1.7 → 4.8, past 2.0 and 3.5
            Assert.Equal(3, _events.Count);
            Near(ClipX(4.8f % 1.5f), X(door));
        }

        static float ClipX(float clipTime) => 2f * clipTime;

        [Fact]
        public void PingPong_ComesBack_ItsEventsBothWays()
        {
            Clip("A/pp.anim.json", "PingPong");
            var door = Door("A/pp.anim.json");
            AnimationSystem.Update(World, 0.1f);
            Run(2.0f);
            Near(2, X(door));                                          // 2.0 s: back at 1.0 s of the clip
            Assert.Single(_events);
            Run(0.6f);                                                 // 2.6 s: clip 0.4 — passed 0.5 coming back
            Assert.Equal(2, _events.Count);
        }

        [Fact]
        public void Speed_AnotherClip_NotAutomatic()
        {
            Clip("A/a.anim.json", "Loop");
            Clip("A/b.anim.json", "Once");
            var fast = Door("A/a.anim.json", speed: 2f);
            var idle = Door("A/a.anim.json", auto: false);
            AnimationSystem.Update(World, 0.1f);
            Run(0.5f);
            Near(2, X(fast));                                          // twice as fast: 1 s of the clip
            Near(0, X(idle));                                          // not playing automatically: it stands at the start
            P(fast).Clip = "A/b.anim.json";
            AnimationSystem.Update(World, 0.1f);
            Near(0, P(fast).Time);                                     // another clip starts from 0
            Near(0, X(fast));
        }

        [Fact]
        public void AMissingClip_WarnedOnce_NothingBreaks()
        {
            var door = Door("A/none.anim.json");
            Run(1f);
            Assert.Single(_warnings, w => w.Contains("A/none.anim.json"));
            Near(0, X(door));
        }

        [Fact]
        public void TheBlock_DefaultsLeftOut_PlayingFromPlayAutomatically()
        {
            var door = Door("A/slide.anim.json");
            var scene = SceneManager.ActiveScene!;
            string json = SceneSerializer.Serialize(scene);
            Assert.Contains("\"AnimationPlayer\":{\"Clip\":\"A/slide.anim.json\"}", json);
            P(door).PlayAutomatically = false;
            P(door).Speed = 0.5f;
            json = SceneSerializer.Serialize(scene);
            Assert.Contains("\"AnimationPlayer\":{\"Clip\":\"A/slide.anim.json\",\"Auto\":false,\"Speed\":0.5}", json);
            World.Clear();
            SceneSerializer.Deserialize(json, scene);
            var e = World.Entities.First(x => World.HasComponent<AnimationPlayer>(x));
            var p = World.GetComponent<AnimationPlayer>(e);
            Assert.False(p.PlayAutomatically);
            Assert.False(p.Playing);
            Assert.Equal(0.5f, p.Speed);
        }

        [Fact]
        public void KeysIn_TheClipsASceneNames()
        {
            var keys = ClipLibrary.KeysIn("""{"entities":[{"c":{"AnimationPlayer":{"Clip":"Animations/Door.anim.json"}}},{"c":{"Script":{"F":{"next":"Animations/Run.anim.json"}}}}]}""");
            Assert.Equal(new[] { "Animations/Door.anim.json", "Animations/Run.anim.json" }, keys);
        }
    }
}
