using System.Collections.Generic;
using System.Numerics;
using Frent;
using GameEditor.Framework.Core;
using GameEditor.Framework.ECS;
using GameEditor.Framework.Renderer.Server.Animation;
using GameEditor.Framework.Scene;
using Xunit;

namespace Scene.Tests
{
    /// <summary>
    /// G6 A0: every clip of a character kept by a name that does not move, and the character's animator following its
    /// skinned mesh's playback settings — the clip, the speed, playing or holding, round and round or stopping at the end;
    /// the settings in the SkinnedMeshRenderer block only when they are not the defaults.
    /// </summary>
    public sealed class AnimationClipsTests : System.IDisposable
    {
        static readonly ECSWorld World = ECSWorld.Instance;
        readonly List<string> _warnings = new();

        public AnimationClipsTests()
        {
            World.Clear();
            SceneManager.NewScene("Clips");
            Logger.OnLog += OnLog;
        }

        public void Dispose()
        {
            Logger.OnLog -= OnLog;
            World.Clear();
        }

        void OnLog(LogLevel level, string text) { if (level == LogLevel.Warning) _warnings.Add(text); }

        // A clip of `seconds` with no bones: its time is all there is to see.
        static CGltfAnimation Clip(string name, float seconds) =>
            new(seconds, 1f, new CGltfNodeData { Transformation = Matrix4x4.Identity, Name = "root", Children = new() }, new()) { Name = name };

        static SkinnedCharacterRegistry.Entry Character(params CGltfAnimation[] clips)
        {
            var entry = new SkinnedCharacterRegistry.Entry
            {
                Animator = new CGltfAnimator(clips[0], new List<CGltfNode>(), 1),
            };
            entry.SetClips(clips);
            return entry;
        }

        static SkinnedMeshRenderer Settings(string clip = "", float speed = 1f, bool playing = true, bool loop = true) =>
            new() { CharacterKey = "Models/hero.glb", Visible = true, Clip = clip, Speed = speed, Playing = playing, Loop = loop };

        // One frame as the renderer gives it: the settings applied, the animator advanced when it is to move.
        static void Frame(SkinnedCharacterRegistry.Entry entry, in SkinnedMeshRenderer smr, float dt)
        {
            if (entry.Apply(smr)) entry.Animator!.UpdateAnimation(dt);
        }

        [Fact]
        public void Every_clip_is_kept_by_a_name_that_does_not_move()
        {
            var entry = Character(Clip("", 1f), Clip("walk", 1f), Clip("walk", 1f), Clip("", 1f), Clip("idle", 1f));
            Assert.Equal(new[] { "clip0", "walk", "walk~2", "clip3", "idle" }, entry.ClipNames);
            Assert.Equal("clip0", entry.CurrentClipName);                        // the first plays, as before
            Assert.NotNull(entry.FindClip("walk~2"));
            Assert.Null(entry.FindClip("run"));
        }

        [Fact]
        public void The_animator_follows_the_clip_the_speed_holding_and_the_loop()
        {
            var entry = Character(Clip("idle", 2f), Clip("walk", 1f));
            var anim = entry.Animator!;
            Frame(entry, Settings(), 0.5f);
            Assert.Equal(("idle", 0.5f), (entry.CurrentClipName, anim.GetCurrentTime()));

            Frame(entry, Settings("walk"), 0.25f);                                // another clip: it plays from its start
            Assert.Equal(("walk", 0.25f), (entry.CurrentClipName, anim.GetCurrentTime()));

            Frame(entry, Settings("walk", speed: 2f), 0.1f);                       // twice as fast
            Assert.Equal(0.45f, anim.GetCurrentTime(), 4);

            Frame(entry, Settings("walk", playing: false), 0.3f);                  // not playing: the pose holds
            Assert.Equal(0.45f, anim.GetCurrentTime(), 4);

            Frame(entry, Settings("walk"), 0.75f);                                 // round and round: past its end, from its start
            Assert.Equal(0.2f, anim.GetCurrentTime(), 4);

            Frame(entry, Settings("walk", loop: false), 0.9f);                     // no loop: stops at its end
            Assert.Equal(1f, anim.GetCurrentTime(), 4);
            Frame(entry, Settings("walk", loop: false), 0.5f);
            Assert.Equal(1f, anim.GetCurrentTime(), 4);

            Frame(entry, Settings("run"), 0.1f);                                   // no such clip: said once, the clip plays on
            Frame(entry, Settings("run"), 0.1f);
            Assert.Single(_warnings, w => w.Contains("has no clip 'run'"));
            Assert.Equal("walk", entry.CurrentClipName);
        }

        [Fact]
        public void The_playback_settings_are_in_the_block_only_when_they_are_not_the_defaults()
        {
            Entity plain = SceneManager.ActiveScene!.CreateEntity("Plain");
            World.AddComponent(plain, Settings());
            string json = SceneSerializer.SerializeEntity(plain);
            Assert.DoesNotContain("\"Clip\"", json);
            Assert.DoesNotContain("\"Speed\"", json);
            Assert.DoesNotContain("\"Playing\"", json);
            Assert.DoesNotContain("\"Loop\"", json);

            Entity set = SceneManager.ActiveScene!.CreateEntity("Set");
            World.AddComponent(set, Settings("walk", speed: 1.5f, playing: false, loop: false));
            json = SceneSerializer.SerializeEntity(set);
            Assert.Contains("\"Clip\":\"walk\",\"Speed\":1.5,\"Playing\":false,\"Loop\":false", json);
            World.RemoveComponent<SkinnedMeshRenderer>(set);
            SceneSerializer.ReadComponents(set, System.Text.Json.JsonDocument.Parse(json).RootElement.GetProperty("c"));
            var back = World.GetComponent<SkinnedMeshRenderer>(set);
            Assert.Equal(("walk", 1.5f, false, false), (back.Clip, back.Speed, back.Playing, back.Loop));

            // A block written before (no playback fields): the first clip, as made, playing, round and round.
            json = SceneSerializer.SerializeEntity(plain);
            World.RemoveComponent<SkinnedMeshRenderer>(plain);
            SceneSerializer.ReadComponents(plain, System.Text.Json.JsonDocument.Parse(json).RootElement.GetProperty("c"));
            back = World.GetComponent<SkinnedMeshRenderer>(plain);
            Assert.Equal(("", 1f, true, true), (back.Clip, back.Speed, back.Playing, back.Loop));
        }
    }
}
