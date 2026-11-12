using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Frent;
using GameEditor.Framework.Assets;
using GameEditor.Framework.Core;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using MiniAudioNS;
using static MiniAudioNS.MiniAudio;

namespace GameEditor.Framework.Sound
{
    /// <summary>
    /// A game's sound: MiniAudio's mixer behind three things a game asks for — a sound played once
    /// (<see cref="PlaySound"/>), the sound of an entity (<see cref="AudioSource"/>: <see cref="Play"/>,
    /// <see cref="Stop"/>), and one track of music that goes on across scenes (<see cref="PlayMusic"/>).
    /// The mixer is made when the first sound is asked for. A sound whose file has not arrived yet (a built game
    /// reads its files as they come) starts when it has. Main thread only.
    /// </summary>
    public static unsafe class Audio
    {
        private const uint Rate = 48000, Channels = 2;

        private sealed class Voice
        {
            public ma_sound* Sound;
            public string Clip = "";
            public Entity Entity;
            public bool HasEntity, Music;
            public float Volume = 1f, Pitch = 1f;
        }

        private struct Wanted
        {
            public string Clip;
            public float Volume, Pitch;
            public Entity Entity;
            public bool HasEntity, Loop, Music;
        }

        private static ma_engine* _engine;
        private static ma_sound* _musicGroup, _soundGroup;
        private static bool _ready, _failed, _paused, _silentNow;
        private static double _owed;                                   // frames a silent mixer has not been asked for yet
        private static ulong _lastTime;                                // the mixer's clock when it was last seen to move
        private static float _still;                                   // seconds since then
        private static bool _everRan;
        private static float _volume = 1f, _musicVolume = 1f, _soundVolume = 1f;
        private static readonly List<Voice> _voices = new();
        private static readonly List<Wanted> _wanted = new();
        private static readonly HashSet<string> _registered = new(StringComparer.Ordinal);
        private static Voice? _music;
        private static string? _musicWanted;

        /// <summary>
        /// No sound device: the mixer runs and nothing is heard, and its time moves with <see cref="Update"/> —
        /// for tests, and for an editor that is driven by a script at night. Set before the first sound.
        /// </summary>
        public static bool Silent { get; set; }

        /// <summary>The mixer is there (a sound was asked for, and the device opened).</summary>
        public static bool Ready => _ready;

        /// <summary>
        /// With a <see cref="Silent"/> mixer: the loudest sample it mixed in the last <see cref="Update"/>, from 0
        /// to 1 — what a loudspeaker would have been given. 0 with a sound device (the device takes the sound itself).
        /// </summary>
        public static float Peak { get; private set; }

        /// <summary>
        /// The mixer's time moves: its sound goes to a device (or, with a <see cref="Silent"/> mixer, nowhere).
        /// False before the first sound, and in a browser until the player's first tap or key — a browser lets a
        /// page make no sound before that.
        /// </summary>
        public static bool Running { get; private set; }

        /// <summary>Everything, from 0 to 1.</summary>
        public static float Volume
        {
            get => _volume;
            set { _volume = Math.Clamp(value, 0f, 1f); if (_ready) ma_engine_set_volume(_engine, _volume); }
        }

        /// <summary>The music track and every looping <see cref="AudioSource"/>, from 0 to 1.</summary>
        public static float MusicVolume
        {
            get => _musicVolume;
            set { _musicVolume = Math.Clamp(value, 0f, 1f); if (_ready) ma_sound_group_set_volume(_musicGroup, _musicVolume); }
        }

        /// <summary>Every other sound, from 0 to 1.</summary>
        public static float SoundVolume
        {
            get => _soundVolume;
            set { _soundVolume = Math.Clamp(value, 0f, 1f); if (_ready) ma_sound_group_set_volume(_soundGroup, _soundVolume); }
        }

        /// <summary>Sounds that play now, the music track among them.</summary>
        public static int PlayingCount
        {
            get
            {
                int n = 0;
                foreach (Voice v in _voices)
                    if (ma_sound_is_playing(in *v.Sound) != 0) n++;
                return n;
            }
        }

        /// <summary>Sounds the mixer holds: those that play, and any that ended and have not been given back yet.</summary>
        public static int VoiceCount => _voices.Count;

        /// <summary>The key of the music track that plays, or was asked for; null for none.</summary>
        public static string? Music => _music?.Clip ?? _musicWanted;

        // ── The mixer ───────────────────────────────────────────────────────────────────

        private static bool Ensure()
        {
            if (_ready) return true;
            if (_failed) return false;
            _engine = (ma_engine*)NativeMemory.AllocZeroed((nuint)sizeof(ma_engine));
            ma_engine_config config = ma_engine_config_init();
            _silentNow = Silent;
            if (_silentNow)
            {
                config.noDevice = 1;
                config.channels = Channels;
                config.sampleRate = Rate;
            }
            ma_result result = ma_engine_init(in config, _engine);
            if (result != ma_result.MA_SUCCESS)
            {
                Logger.Warning($"[Audio] No sound: the sound device could not be opened ({result}).");
                NativeMemory.Free(_engine);
                _engine = null;
                _failed = true;
                return false;
            }
            _musicGroup = (ma_sound*)NativeMemory.AllocZeroed((nuint)sizeof(ma_sound));
            _soundGroup = (ma_sound*)NativeMemory.AllocZeroed((nuint)sizeof(ma_sound));
            ma_sound_group_init(_engine, 0, null, _musicGroup);
            ma_sound_group_init(_engine, 0, null, _soundGroup);
            _ready = true;
            SetSession();       // after the device is open: opening it chose a session of MiniAudio's own
            ma_engine_set_volume(_engine, _volume);
            ma_sound_group_set_volume(_musicGroup, _musicVolume);
            ma_sound_group_set_volume(_soundGroup, _soundVolume);
            AudioClips.Forgetting = LetGo;
            if (_paused && !_silentNow) ma_engine_stop(_engine);
            return true;
        }

        /// <summary>Gives the mixer and the device back: when the app ends, or a test is over.</summary>
        public static void Shutdown()
        {
            StopAll();
            _wanted.Clear();
            if (_ready)
            {
                ma_resource_manager* manager = ma_engine_get_resource_manager(_engine);
                foreach (string key in _registered) ma_resource_manager_unregister_data(manager, key);
                ma_sound_group_uninit(_musicGroup);
                ma_sound_group_uninit(_soundGroup);
                NativeMemory.Free(_musicGroup);
                NativeMemory.Free(_soundGroup);
                ma_engine_uninit(_engine);
                NativeMemory.Free(_engine);
                _engine = null;
            }
            _registered.Clear();
            _ready = false;
            _failed = false;
            _paused = false;
            _owed = 0;
            Peak = 0f;
            Running = false;
            _everRan = false;
            _lastTime = 0;
            _still = 0f;
        }

        // A clip's bytes are about to be given back: nothing may play from them, and the mixer forgets the name.
        private static void LetGo(AudioClips.Clip clip)
        {
            for (int i = _voices.Count - 1; i >= 0; i--)
                if (_voices[i].Clip == clip.Key) End(i);
            if (_ready && _registered.Remove(clip.Key))
                ma_resource_manager_unregister_data(ma_engine_get_resource_manager(_engine), clip.Key);
        }

        // ── Sounds ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Plays a sound file once ("Audio/coin.wav"). False when it cannot play: no sound device, or no such file
        /// (said in the log the first time). A file that has not arrived yet is played when it has.
        /// </summary>
        public static bool PlaySound(string key, float volume = 1f, float pitch = 1f)
            => Want(new Wanted { Clip = key, Volume = volume, Pitch = pitch });

        /// <summary>
        /// The music: one track that loops and goes on across scenes. The track that plays already is left
        /// playing (a scene that asks for its music again does not start it anew); another takes its place.
        /// </summary>
        public static bool PlayMusic(string key, float volume = 1f)
        {
            if (_music != null && _music.Clip == key) { SetVolume(_music, volume); return true; }
            if (_musicWanted == key) return true;
            StopMusic();
            _musicWanted = key;
            if (Want(new Wanted { Clip = key, Volume = volume, Pitch = 1f, Loop = true, Music = true })) return true;
            _musicWanted = null;
            return false;
        }

        /// <summary>Stops the music track.</summary>
        public static void StopMusic()
        {
            _musicWanted = null;
            _wanted.RemoveAll(static w => w.Music);
            if (_music != null) End(_voices.IndexOf(_music));
        }

        /// <summary>Plays the sound of an entity's <see cref="AudioSource"/>, from its beginning. False when it cannot play.</summary>
        public static bool Play(Entity entity)
        {
            ECSWorld world = ECSWorld.Instance;
            if (!world.TryGetComponent<AudioSource>(entity, out var source) || string.IsNullOrEmpty(source.Clip)) return false;
            Stop(entity);
            return Want(new Wanted { Clip = source.Clip, Volume = source.Volume, Pitch = source.Pitch, Loop = source.Loop, Entity = entity, HasEntity = true });
        }

        /// <summary>Stops the sound of an entity.</summary>
        public static void Stop(Entity entity)
        {
            for (int i = _wanted.Count - 1; i >= 0; i--)
                if (_wanted[i].HasEntity && _wanted[i].Entity == entity) _wanted.RemoveAt(i);
            int at = VoiceOf(entity);
            if (at >= 0) End(at);
        }

        /// <summary>Does the entity's sound play now?</summary>
        public static bool IsPlaying(Entity entity)
        {
            int at = VoiceOf(entity);
            return at >= 0 && ma_sound_is_playing(in *_voices[at].Sound) != 0;
        }

        /// <summary>Seconds into the entity's sound (a looping sound starts from 0 again each time round); 0 when it does not play.</summary>
        public static float TimeOf(Entity entity)
        {
            int at = VoiceOf(entity);
            return at >= 0 ? Seconds(_voices[at]) : 0f;
        }

        /// <summary>Seconds into the music track; 0 when none plays.</summary>
        public static float MusicTime => _music != null ? Seconds(_music) : 0f;

        private static float Seconds(Voice voice)
        {
            float seconds = 0f;
            ma_sound_get_cursor_in_seconds(in *voice.Sound, ref seconds);
            return seconds;
        }

        private static int VoiceOf(Entity entity)
        {
            for (int i = 0; i < _voices.Count; i++)
                if (_voices[i].HasEntity && _voices[i].Entity == entity) return i;
            return -1;
        }

        private static bool Want(Wanted wanted)
        {
#if WEB
            // A browser's mixer stands still until the player's first tap or key. A sound played once before that
            // is not kept for then (it would be heard, late, with every other one): it is dropped. Music waits.
            if (!wanted.Loop && !_everRan) { Ensure(); return true; }
#endif
            switch (AudioClips.Find(wanted.Clip, out AudioClips.Clip? clip))
            {
                case SpriteLibrary.Answer.Ready: return Begin(wanted, clip!);
                case SpriteLibrary.Answer.Loading: _wanted.Add(wanted); return true;
                default: return false;
            }
        }

        private static bool Begin(Wanted wanted, AudioClips.Clip clip)
        {
            if (!Ensure()) return false;
            if (_registered.Add(clip.Key))
                ma_resource_manager_register_encoded_data(ma_engine_get_resource_manager(_engine), clip.Key, clip.Data, (nuint)clip.Size);

            // A sound played once is decoded whole, once, and every play of it shares that; a loop (music) is
            // decoded as it plays, from the file's bytes — three minutes of music stay the size of their file.
            uint flags = (uint)ma_sound_flags.MA_SOUND_FLAG_NO_SPATIALIZATION | (wanted.Loop ? 0u : (uint)ma_sound_flags.MA_SOUND_FLAG_DECODE);
            var sound = (ma_sound*)NativeMemory.AllocZeroed((nuint)sizeof(ma_sound));
            ma_result result = ma_sound_init_from_file(_engine, clip.Key, flags, wanted.Loop ? _musicGroup : _soundGroup, null, sound);
            if (result != ma_result.MA_SUCCESS)
            {
                Logger.Warning($"[Audio] '{clip.Key}' is not a sound file the mixer can play ({result}).");
                NativeMemory.Free(sound);
                return false;
            }
            var voice = new Voice { Sound = sound, Clip = clip.Key, Entity = wanted.Entity, HasEntity = wanted.HasEntity, Music = wanted.Music };
            ma_sound_set_looping(sound, wanted.Loop ? 1u : 0u);
            SetVolume(voice, wanted.Volume);
            SetPitch(voice, wanted.Pitch);
            ma_sound_start(sound);
            _voices.Add(voice);
            if (wanted.Music) { _music = voice; _musicWanted = null; }
            return true;
        }

        private static void SetVolume(Voice voice, float volume)
        {
            voice.Volume = volume;
            ma_sound_set_volume(voice.Sound, MathF.Max(volume, 0f));
        }

        private static void SetPitch(Voice voice, float pitch)
        {
            voice.Pitch = pitch;
            ma_sound_set_pitch(voice.Sound, pitch > 0f ? pitch : 1f);
        }

        private static void End(int index)
        {
            if (index < 0) return;
            Voice voice = _voices[index];
            ma_sound_stop(voice.Sound);
            ma_sound_uninit(voice.Sound);
            NativeMemory.Free(voice.Sound);
            _voices.RemoveAt(index);
            if (ReferenceEquals(voice, _music)) _music = null;
        }

        // ── A scene's sounds ────────────────────────────────────────────────────────────

        /// <summary>A scene starts to play: every <see cref="AudioSource"/> with PlayOnStart plays.</summary>
        public static void StartScene(ECSWorld world)
        {
            foreach (Entity e in world.Entities) AddEntity(world, e);
        }

        /// <summary>An entity came into a playing scene: its <see cref="AudioSource"/> plays when it has PlayOnStart.</summary>
        public static void AddEntity(ECSWorld world, Entity entity)
        {
            if (world.TryGetComponent<AudioSource>(entity, out var source) && source.PlayOnStart) Play(entity);
        }

        /// <summary>The game ends: everything stops.</summary>
        public static void StopAll()
        {
            _wanted.Clear();
            _musicWanted = null;
            for (int i = _voices.Count - 1; i >= 0; i--) End(i);
            Resume();
        }

        /// <summary>The app is out of sight, or the game is paused: nothing is heard, and every sound waits where it is.</summary>
        public static void Pause()
        {
            if (_paused) return;
            _paused = true;
            if (_ready && !_silentNow) ma_engine_stop(_engine);
        }

        /// <summary>Every sound goes on from where it waited.</summary>
        public static void Resume()
        {
            if (!_paused) return;
            _paused = false;
            if (_ready && !_silentNow) ma_engine_start(_engine);
        }

        public static bool Paused => _paused;

        /// <summary>
        /// Once a frame of play: sounds played once that have ended are given back, sounds whose file has arrived
        /// start, an entity's sound follows its <see cref="AudioSource"/> (volume and pitch; it stops when the
        /// entity or the component is gone), and a silent mixer is moved on by the frame's time.
        /// </summary>
        public static void Update(ECSWorld world, float deltaTime)
        {
            for (int i = _wanted.Count - 1; i >= 0; i--)
            {
                Wanted wanted = _wanted[i];
                SpriteLibrary.Answer answer = AudioClips.Find(wanted.Clip, out AudioClips.Clip? clip);
                if (answer == SpriteLibrary.Answer.Loading) continue;
                _wanted.RemoveAt(i);
                if (answer == SpriteLibrary.Answer.Ready) Begin(wanted, clip!);
                else if (wanted.Music) _musicWanted = null;
            }

            for (int i = _voices.Count - 1; i >= 0; i--)
            {
                Voice voice = _voices[i];
                if (voice.HasEntity)
                {
                    if (!world.TryGetComponent<AudioSource>(voice.Entity, out var source)) { End(i); continue; }
                    if (source.Volume != voice.Volume) SetVolume(voice, source.Volume);
                    if (source.Pitch != voice.Pitch) SetPitch(voice, source.Pitch);
                }
                else if (!voice.Music && ma_sound_at_end(in *voice.Sound) != 0) End(i);
            }

            if (_ready)
            {
                ulong time = ma_engine_get_time(in *_engine);
                _still = _silentNow || time != _lastTime ? 0f : _still + deltaTime;
                _lastTime = time;
                Running = _still < 0.25f;
                _everRan |= Running && (_silentNow || time != 0);
            }

            if (_ready && _silentNow && deltaTime > 0f)
            {
                // No device asks for the mixed sound: it is asked for here, as much as the frame is long.
                _owed += (double)deltaTime * Rate;
                float* scratch = stackalloc float[1024 * (int)Channels];
                float peak = 0f;
                while (_owed >= 1.0)
                {
                    ulong frames = (ulong)Math.Min(_owed, 1024.0), read = 0;
                    ma_engine_read_pcm_frames(_engine, scratch, frames, ref read);
                    for (int i = 0; i < (int)read * (int)Channels; i++) peak = MathF.Max(peak, MathF.Abs(scratch[i]));
                    _owed -= frames;
                }
                Peak = peak;
            }
        }

        // ── iOS: the game's sound is ambient ────────────────────────────────────────────
        // MiniAudio's own choice on an iPhone is the play-and-record session, which silences the player's music
        // and ignores the mute switch. A game's sound is ambient: it mixes with other apps and obeys the switch.

#if __IOS__
        [DllImport("/usr/lib/libobjc.dylib")] private static extern IntPtr objc_getClass(string name);
        [DllImport("/usr/lib/libobjc.dylib")] private static extern IntPtr sel_registerName(string name);
        [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")] private static extern IntPtr Send(IntPtr receiver, IntPtr selector);
        [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")] private static extern IntPtr Send(IntPtr receiver, IntPtr selector, [MarshalAs(UnmanagedType.LPUTF8Str)] string text);
        [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")] private static extern byte Send(IntPtr receiver, IntPtr selector, IntPtr a, IntPtr b);

        private static void SetSession()
        {
            IntPtr session = Send(objc_getClass("AVAudioSession"), sel_registerName("sharedInstance"));
            IntPtr ambient = Send(objc_getClass("NSString"), sel_registerName("stringWithUTF8String:"), "AVAudioSessionCategoryAmbient");
            if (session != IntPtr.Zero && ambient != IntPtr.Zero) Send(session, sel_registerName("setCategory:error:"), ambient, IntPtr.Zero);
        }
#else
        private static void SetSession() { }
#endif
    }
}
