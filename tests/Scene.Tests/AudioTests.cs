using System.Numerics;
using Frent;
using GameEditor.Framework.Core;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Scene;
using GameEditor.Framework.Scripting;
using GameEditor.Framework.Sound;
using Xunit;

namespace Scene.Tests
{
    /// <summary>
    /// A game's sound, with the real mixer and no sound device (its library is copied beside the tests): the mixer's
    /// time moves with the frames the tests step, and what it mixed is measured — a sound played once and gone, the
    /// music track round and round and across a scene's end, an entity's sound, how loud each is.
    /// </summary>
    public sealed class AudioTests : IDisposable
    {
        static readonly ECSWorld World = ECSWorld.Instance;
        const float Frame = 1f / 60f;

        readonly Dictionary<string, byte[]> _files = new();
        readonly List<string> _warnings = new();

        sealed class NoPhysics : ISceneSimulator
        {
            public void Start(ECSWorld world) { }
            public void Step(float dt) { }
            public void Stop() { }
        }

        public AudioTests()
        {
            World.Clear();
            Audio.Shutdown();
            Audio.Silent = true;
            Audio.Volume = Audio.MusicVolume = Audio.SoundVolume = 1f;
            AudioClips.Clear();
            AudioClips.Reader = key => _files.TryGetValue(key, out byte[]? bytes) ? bytes : null;
            SceneManager.SimulatorFactory = _ => new NoPhysics();
            SceneManager.NewScene("Loud");
            SceneManager.ActiveScene!.PhysicsBackend = "none";
            Logger.OnLog += OnLog;

            _files["Audio/blip.wav"] = Wav(0.25f, 0.5f);     // a quarter of a second, half as loud as can be
            _files["Audio/tune.wav"] = Wav(0.2f, 0.25f);
            _files["Audio/long.wav"] = Wav(3f, 0.5f);
            _files["Audio/notes.txt.wav"] = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        }

        public void Dispose()
        {
            Logger.OnLog -= OnLog;
            SceneManager.Stop();
            SceneManager.SimulatorFactory = null;
            Audio.Shutdown();
            Audio.Silent = false;
            AudioClips.Reader = null;
            AudioClips.Clear();
            World.Clear();
        }

        void OnLog(LogLevel level, string text) { if (level == LogLevel.Warning) _warnings.Add(text); }

        // A square wave as a 16-bit mono wav file at 48,000 samples a second: every sample is +level or -level.
        static byte[] Wav(float seconds, float level)
        {
            int samples = (int)(seconds * 48000);
            using var stream = new MemoryStream();
            using var w = new BinaryWriter(stream);
            w.Write("RIFF"u8); w.Write(36 + samples * 2); w.Write("WAVEfmt "u8); w.Write(16);
            w.Write((short)1); w.Write((short)1); w.Write(48000); w.Write(96000); w.Write((short)2); w.Write((short)16);
            w.Write("data"u8); w.Write(samples * 2);
            for (int i = 0; i < samples; i++) w.Write((short)((i / 48 % 2 == 0 ? 1 : -1) * level * 32767));
            w.Flush();
            return stream.ToArray();
        }

        static void Run(float seconds)
        {
            int frames = (int)MathF.Round(seconds / Frame);
            for (int i = 0; i < frames; i++) Audio.Update(World, Frame);
        }

        static Entity Source(string name, AudioSource source)
        {
            Entity e = SceneManager.ActiveScene!.CreateEntity(name);
            World.AddComponent(e, source);
            return e;
        }

        [Fact]
        public void A_sound_played_once_is_heard_for_its_length_and_then_gone()
        {
            Assert.Equal(0, Audio.PlayingCount);
            Assert.True(Audio.PlaySound("Audio/blip.wav"));
            Assert.True(Audio.Ready);
            Assert.Equal(1, Audio.PlayingCount);
            Run(0.1f);
            Assert.Equal(0.5f, Audio.Peak, 2);                          // what the file holds comes out of the mixer
            Run(0.3f);
            Assert.Equal(0, Audio.PlayingCount);
            Assert.Equal(0, Audio.VoiceCount);                          // and the mixer has given it back
            Assert.Equal(0f, Audio.Peak);

            // Twice at once: two voices, and twice as loud; half as loud by its volume.
            Audio.PlaySound("Audio/blip.wav", volume: 0.5f);
            Audio.PlaySound("Audio/blip.wav", volume: 0.5f);
            Assert.Equal(2, Audio.PlayingCount);
            Run(0.1f);
            Assert.Equal(0.5f, Audio.Peak, 2);
        }

        [Fact]
        public void A_file_that_is_not_there_or_is_no_sound_is_said_and_plays_nothing()
        {
            Assert.False(Audio.PlaySound("Audio/nothing.wav"));
            Assert.Single(_warnings, w => w.Contains("Audio/nothing.wav"));
            Assert.False(Audio.PlaySound("Audio/nothing.wav"));
            Assert.Single(_warnings, w => w.Contains("Audio/nothing.wav"));          // said once
            Assert.False(Audio.PlaySound("Audio/notes.txt.wav"));
            Assert.Contains(_warnings, w => w.Contains("Audio/notes.txt.wav") && w.Contains("not a sound file"));
            Assert.Equal(0, Audio.PlayingCount);
        }

        [Fact]
        public void The_music_goes_round_and_round_and_is_not_started_anew_when_asked_for_again()
        {
            Assert.True(Audio.PlayMusic("Audio/tune.wav"));
            Assert.Equal("Audio/tune.wav", Audio.Music);
            Run(0.5f);                                                  // two and a half times its length
            Assert.Equal(1, Audio.PlayingCount);
            Assert.Equal(0.25f, Audio.Peak, 2);
            Assert.InRange(Audio.MusicTime, 0.05f, 0.15f);              // into its third round

            float at = Audio.MusicTime;
            Assert.True(Audio.PlayMusic("Audio/tune.wav"));             // the next scene asks for its music
            Assert.Equal(1, Audio.PlayingCount);
            Assert.Equal(at, Audio.MusicTime);

            Assert.True(Audio.PlayMusic("Audio/long.wav"));             // another track takes its place
            Assert.Equal(1, Audio.PlayingCount);
            Assert.Equal("Audio/long.wav", Audio.Music);
            Run(0.1f);
            Assert.Equal(0.5f, Audio.Peak, 2);
            Audio.StopMusic();
            Assert.Equal(0, Audio.PlayingCount);
            Assert.Null(Audio.Music);
        }

        [Fact]
        public void The_three_volumes_are_each_their_own()
        {
            Audio.PlayMusic("Audio/long.wav");
            Run(0.1f);
            Assert.Equal(0.5f, Audio.Peak, 2);
            Audio.MusicVolume = 0.5f;
            Run(0.1f);
            Assert.Equal(0.25f, Audio.Peak, 2);
            Audio.SoundVolume = 0f;                                     // the music is not a sound
            Run(0.1f);
            Assert.Equal(0.25f, Audio.Peak, 2);
            Audio.Volume = 0.5f;
            Run(0.1f);
            Assert.Equal(0.125f, Audio.Peak, 2);

            Audio.StopMusic();
            Audio.Volume = 1f;
            Audio.PlaySound("Audio/long.wav");
            Run(0.1f);
            Assert.Equal(0f, Audio.Peak, 2);                            // sounds are off
            Audio.SoundVolume = 1f;
            Run(0.1f);
            Assert.Equal(0.5f, Audio.Peak, 2);
        }

        [Fact]
        public void An_entitys_sound_plays_when_its_scene_starts_follows_its_component_and_ends_with_its_entity()
        {
            Entity hum = Source("Hum", new AudioSource { Clip = "Audio/tune.wav", Volume = 1f, Pitch = 1f, Loop = true, PlayOnStart = true });
            Entity bell = Source("Bell", new AudioSource { Clip = "Audio/blip.wav", Volume = 1f, Pitch = 1f, PlayOnStart = false });
            SceneManager.Play();
            Assert.True(Audio.IsPlaying(hum));
            Assert.False(Audio.IsPlaying(bell));                        // waits for a script
            ScriptSystem.UpdateAll(Frame);                              // the frame moves the mixer on
            Run(0.3f);
            Assert.True(Audio.IsPlaying(hum));                          // round and round
            Assert.Equal(0.25f, Audio.Peak, 2);

            var source = World.GetComponent<AudioSource>(hum);
            source.Volume = 0.5f;
            World.AddComponent(hum, source);
            Run(0.1f);
            Assert.Equal(0.125f, Audio.Peak, 2);

            Assert.True(Audio.Play(bell));
            Run(0.1f);
            Assert.InRange(Audio.TimeOf(bell), 0.08f, 0.12f);
            Assert.True(Audio.Play(bell));                              // again: from its beginning
            Assert.Equal(0f, Audio.TimeOf(bell));
            Run(0.4f);
            Assert.False(Audio.IsPlaying(bell));                        // played once, and over

            SceneManager.Destroy(hum);
            ScriptSystem.UpdateAll(Frame);
            Run(Frame);
            Assert.False(Audio.IsPlaying(hum));
            Assert.Equal(0, Audio.PlayingCount);
        }

        [Fact]
        public void A_scene_that_is_left_takes_its_entities_sounds_with_it_and_the_music_and_a_sound_played_once_go_on()
        {
            // The scene the game will go to: an empty one. Then the scene it starts in, with the Hum.
            SceneManager.NewScene("Next");
            SceneManager.ActiveScene!.PhysicsBackend = "none";
            string next = SceneSerializer.Serialize(SceneManager.ActiveScene);
            SceneManager.NewScene("Loud");
            SceneManager.ActiveScene!.PhysicsBackend = "none";
            Entity hum = Source("Hum", new AudioSource { Clip = "Audio/long.wav", Volume = 1f, Pitch = 1f, Loop = true, PlayOnStart = true });
            SceneManager.SceneReader = key => key == "Scenes/Next.scene.json" ? next : null;
            try
            {
                SceneManager.Play();
                Audio.PlayMusic("Audio/tune.wav");
                Audio.PlaySound("Audio/long.wav");                      // a jingle that is still sounding when the level ends
                Assert.Equal(3, Audio.PlayingCount);
                SceneManager.RequestScene("Scenes/Next.scene.json");
                ScriptSystem.UpdateAll(Frame);
                Assert.Equal("Scenes/Next.scene.json", SceneManager.CurrentSceneKey);
                ScriptSystem.UpdateAll(Frame);
                Assert.False(Audio.IsPlaying(hum));                     // its entity is gone
                Assert.Equal(2, Audio.PlayingCount);
                Assert.Equal("Audio/tune.wav", Audio.Music);

                SceneManager.Stop();                                    // the game ends: everything stops
                Assert.Equal(0, Audio.PlayingCount);
                Assert.Null(Audio.Music);
            }
            finally { SceneManager.SceneReader = null; }
        }

        [Fact]
        public void A_higher_pitch_is_a_shorter_sound()
        {
            Audio.PlaySound("Audio/blip.wav", pitch: 2f);               // a quarter of a second, twice as fast
            Run(0.1f);
            Assert.Equal(1, Audio.PlayingCount);
            Run(0.08f);
            Assert.Equal(0, Audio.PlayingCount);
        }

        [Fact]
        public void The_AudioSource_block_round_trips_and_one_with_only_its_clip_has_the_defaults()
        {
            Source("Hum", new AudioSource { Clip = "Audio/tune.wav", Volume = 0.4f, Pitch = 1.5f, Loop = true, PlayOnStart = false });
            string text = SceneSerializer.Serialize(SceneManager.ActiveScene!);
            Assert.Contains("\"AudioSource\":{\"Clip\":\"Audio/tune.wav\",\"Volume\":0.4,\"Pitch\":1.5,\"Loop\":true,\"PlayOnStart\":false}", text);
            SceneSerializer.Deserialize(text, SceneManager.ActiveScene!);
            var read = World.GetComponent<AudioSource>(World.Entities.First(e => World.GetComponent<NameTag>(e).Name == "Hum"));
            Assert.Equal(("Audio/tune.wav", 0.4f, 1.5f, true, false), (read.Clip, read.Volume, read.Pitch, read.Loop, read.PlayOnStart));

            SceneSerializer.Deserialize(text.Replace("\"Volume\":0.4,\"Pitch\":1.5,\"Loop\":true,\"PlayOnStart\":false", "\"Clip\":\"Audio/tune.wav\"").Replace("{\"Clip\":\"Audio/tune.wav\",\"Clip\"", "{\"Clip\""),
                                         SceneManager.ActiveScene!);
            read = World.GetComponent<AudioSource>(World.Entities.First(e => World.GetComponent<NameTag>(e).Name == "Hum"));
            Assert.Equal(("Audio/tune.wav", 1f, 1f, false, true), (read.Clip, read.Volume, read.Pitch, read.Loop, read.PlayOnStart));
        }

        [Fact]
        public void The_sound_files_a_scene_names_are_found_once_each()
        {
            string text = """{ "a": "Audio/coin.wav", "b": [ { "Clip": "Audio/Music.OGG" }, "Audio/coin.wav", "Textures/coin.png", "song.mp3", "x.flac" ] }""";
            Assert.Equal(new[] { "Audio/coin.wav", "Audio/Music.OGG", "song.mp3", "x.flac" }, AudioClips.KeysIn(text).ToArray());
            Assert.Empty(AudioClips.KeysIn("""{ "a": "Textures/coin.png" }"""));
            Assert.True(AudioClips.IsClipKey("a.WAV"));
            Assert.False(AudioClips.IsClipKey("wav"));
        }
        // ── G3.1: sound in space ───────────────────────────────────────────────────────

        [Fact]
        public void A_spatial_sound_falls_off_with_distance_by_its_rolloff_and_is_panned_to_its_side()
        {
            Assert.Equal(1f, Audio.Rolloff(0.5f, 1, 21, AudioRolloff.Linear));
            Assert.Equal(0.5f, Audio.Rolloff(11, 1, 21, AudioRolloff.Linear), 4);
            Assert.Equal(0.25f, Audio.Rolloff(4, 1, 21, AudioRolloff.Inverse), 4);
            Assert.Equal(0f, Audio.Rolloff(30, 1, 21, AudioRolloff.Inverse));
            var (gain, pan) = Audio.Heard(new Vector3(3, 0, 4), Vector3.Zero, Vector3.UnitX, 1, 50, AudioRolloff.Inverse);
            Assert.Equal(0.2f, gain, 4);                                  // 5 away: 1 / 5
            Assert.Equal(0.6f, pan, 4);                                   // 3 of its 5 to the right
            Assert.Equal((1f, 0f), Audio.Heard(Vector3.Zero, Vector3.Zero, Vector3.UnitX, 1, 50, AudioRolloff.Linear));
        }

        [Fact]
        public void A_spatial_source_is_heard_from_the_listener_quieter_away_and_from_its_side()
        {
            _files["Audio/hum.wav"] = Wav(2f, 0.5f);
            // The main camera listens when there is no listener: at the origin, turned to face −Z, so +X is its right (the
            // engine's cameras face their entity's +Z: unturned, +X would be its left).
            Entity cam = SceneManager.ActiveScene!.CreateEntity("Camera");
            World.AddComponent(cam, new CameraComponent { IsMain = true, NearZ = 0.1f, FarZ = 100f, Fov = 60f });
            var ct = World.GetComponent<Transform>(cam);
            ct.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI);
            World.AddComponent(cam, ct);
            var hum = AudioSource.Default; hum.Clip = "Audio/hum.wav"; hum.Loop = true; hum.Spatial = true; hum.MinDistance = 1f; hum.MaxDistance = 21f; hum.Rolloff = AudioRolloff.Linear;
            Entity source = Source("Hum", hum);
            Assert.True(Audio.Play(source));
            Run(0.1f);
            Assert.True(Audio.GainOf(source, out float gain, out float pan));
            Assert.Equal((1f, 0f), (gain, pan));                           // at the listener: full, both sides
            Assert.Equal(0.5f, Audio.PeakLeft, 2);
            Assert.Equal(0.5f, Audio.PeakRight, 2);

            Move(source, new Vector3(11, 0, 0));                           // 11 to the right: half by Linear, all on the right
            Run(0.1f);
            Audio.GainOf(source, out gain, out pan);
            Assert.Equal((0.5f, 1f), (gain, pan));
            Assert.Equal(0f, Audio.PeakLeft, 2);
            Assert.Equal(0.25f, Audio.PeakRight, 2);

            Move(source, new Vector3(-11, 0, 0));                          // to the left
            Run(0.1f);
            Audio.GainOf(source, out gain, out pan);
            Assert.Equal(-1f, pan);
            Assert.Equal(0.25f, Audio.PeakLeft, 2);
            Assert.Equal(0f, Audio.PeakRight, 2);

            // A listener of its own wins over the camera: placed beside the source, the source is full again and ahead.
            Entity ears = SceneManager.ActiveScene!.CreateEntity("Ears");
            World.AddComponent(ears, new AudioListener());
            Move(ears, new Vector3(-11, 0, 0.5f));
            Run(0.1f);
            Audio.GainOf(source, out gain, out pan);
            Assert.Equal(1f, gain);
            Assert.Equal(0f, pan, 3);

            Move(source, new Vector3(-11, 0, 40));                         // beyond its Max: silent, still playing
            Run(0.1f);
            Audio.GainOf(source, out gain, out _);
            Assert.Equal(0f, gain);
            Assert.Equal(0f, Audio.Peak, 3);
            Assert.Equal(1, Audio.PlayingCount);

            hum.Spatial = false; World.AddComponent(source, hum);         // not spatial: as it was, wherever it is
            Run(0.1f);
            Audio.GainOf(source, out gain, out pan);
            Assert.Equal((1f, 0f), (gain, pan));
            Assert.Equal(0.5f, Audio.Peak, 2);
        }

        static void Move(Entity e, Vector3 at)
        {
            var t = World.GetComponent<Transform>(e);
            t.Position = at;
            World.AddComponent(e, t);
        }

        [Fact]
        public void A_spatial_source_and_a_listener_are_written_in_their_blocks_and_read_back()
        {
            var a = AudioSource.Default; a.Clip = "Audio/hum.wav";
            Entity plain = Source("Plain", a);
            Assert.DoesNotContain("Spatial", SceneSerializer.SerializeEntity(plain));   // not spatial: the block as it was
            a.Spatial = true; a.MinDistance = 2f; a.MaxDistance = 30f; a.Rolloff = AudioRolloff.Linear;
            Entity spatial = Source("Spatial", a);
            World.AddComponent(spatial, new AudioListener());
            string json = SceneSerializer.SerializeEntity(spatial);
            Assert.Contains("\"Spatial\":true,\"Min\":2,\"Max\":30,\"Rolloff\":\"Linear\"", json);
            Assert.Contains("\"AudioListener\":{}", json);
            World.RemoveComponent<AudioSource>(spatial);
            World.RemoveComponent<AudioListener>(spatial);
            SceneSerializer.ReadComponents(spatial, System.Text.Json.JsonDocument.Parse(json).RootElement.GetProperty("c"));
            var back = World.GetComponent<AudioSource>(spatial);
            Assert.Equal((true, 2f, 30f, AudioRolloff.Linear), (back.Spatial, back.MinDistance, back.MaxDistance, back.Rolloff));
            Assert.True(World.HasComponent<AudioListener>(spatial));
        }
    }
}
