using GameEditor.Framework.Core;
using Xunit;

namespace Scene.Tests
{
    /// <summary>
    /// A game's saved data: values by name, written by Save and there again when the file is read; what a file
    /// that is not one does; what is written when nothing changed.
    /// </summary>
    public sealed class SaveDataTests : IDisposable
    {
        string? _file;
        int _writes;
        readonly List<string> _warnings = new();

        public SaveDataTests()
        {
            SaveData.Reader = () => _file;
            SaveData.Writer = text => { _file = text; _writes++; };
            SaveData.Forget();
            Logger.OnLog += OnLog;
        }

        public void Dispose()
        {
            Logger.OnLog -= OnLog;
            SaveData.Reader = null;
            SaveData.Writer = null;
            SaveData.Forget();
        }

        void OnLog(LogLevel level, string text) { if (level == LogLevel.Warning) _warnings.Add(text); }

        [Fact]
        public void Values_are_kept_by_name_and_are_there_again_after_Save_and_a_new_start()
        {
            Assert.False(SaveData.Has("best"));
            Assert.Equal(7, SaveData.GetInt("best", 7));                         // nothing saved: what the game asks for
            SaveData.SetInt("best", 12);
            SaveData.SetFloat("time", 31.25f);
            SaveData.SetString("name", "Dana \"the fast\" דנה");
            SaveData.SetBool("sound", false);
            Assert.True(SaveData.Changed);
            Assert.Null(_file);                                                  // in memory until Save
            Assert.True(SaveData.Save());
            Assert.False(SaveData.Changed);
            Assert.Equal(1, _writes);

            SaveData.Forget();                                                   // the app starts again
            Assert.Equal(12, SaveData.GetInt("best"));
            Assert.Equal(31.25f, SaveData.GetFloat("time"));
            Assert.Equal("Dana \"the fast\" דנה", SaveData.GetString("name"));
            Assert.False(SaveData.GetBool("sound", true));
            Assert.Equal(new[] { "best", "name", "sound", "time" }, SaveData.Keys().ToArray());
        }

        [Fact]
        public void The_file_is_text_with_the_names_sorted_and_Save_writes_only_what_changed()
        {
            SaveData.SetInt("b", 2);
            SaveData.SetInt("a", -1);
            SaveData.Save();
            Assert.Equal("{\n  \"save\": 1,\n  \"values\": {\n    \"a\": -1,\n    \"b\": 2\n  }\n}\n", _file);
            SaveData.Save();
            SaveData.SetInt("a", -1);                                            // the value it has
            SaveData.Save();
            Assert.Equal(1, _writes);

            SaveData.Delete("a");
            SaveData.Delete("never");
            SaveData.Save();
            Assert.Equal(2, _writes);
            Assert.False(SaveData.Has("a"));
            SaveData.Clear();
            SaveData.Save();
            Assert.Equal("{\n  \"save\": 1,\n  \"values\": {}\n}\n", _file);
        }

        [Fact]
        public void A_value_of_another_kind_is_not_there_for_the_kind_asked()
        {
            SaveData.SetString("best", "twelve");
            SaveData.SetFloat("ratio", 2.6f);
            SaveData.SetFloat("bad", float.NaN);
            Assert.Equal(5, SaveData.GetInt("best", 5));
            Assert.Equal(3, SaveData.GetInt("ratio"));                           // a number asked for whole is the nearest
            Assert.Equal("x", SaveData.GetString("ratio", "x"));
            Assert.True(SaveData.GetBool("ratio", true));
            Assert.Equal(0f, SaveData.GetFloat("bad", 9f));                      // what is not a number is kept as 0
        }

        [Fact]
        public void A_file_that_is_not_saved_data_is_said_and_the_game_starts_with_no_values()
        {
            _file = "{ \"values\": [1, 2";
            Assert.Equal(0, SaveData.GetInt("best"));
            Assert.Contains(_warnings, w => w.Contains("save.json.bad"));
            SaveData.SetInt("best", 1);
            Assert.True(SaveData.Save());
            SaveData.Forget();
            Assert.Equal(1, SaveData.GetInt("best"));
        }

        [Fact]
        public void A_write_that_fails_is_said_and_the_values_wait_for_the_next_Save()
        {
            SaveData.Writer = _ => throw new IOException("the disk is full");
            SaveData.SetInt("best", 3);
            Assert.False(SaveData.Save());
            Assert.Contains(_warnings, w => w.Contains("the disk is full"));
            Assert.True(SaveData.Changed);
            SaveData.Writer = text => _file = text;
            Assert.True(SaveData.Save());
            Assert.Contains("\"best\": 3", _file);
        }

        [Fact]
        public unsafe void The_engine_saves_when_the_app_goes_to_the_background_or_is_asked_to_quit()
        {
            foreach (var type in new[] { Sokol.SApp.sapp_event_type.SAPP_EVENTTYPE_SUSPENDED, Sokol.SApp.sapp_event_type.SAPP_EVENTTYPE_QUIT_REQUESTED,
                                         Sokol.SApp.sapp_event_type.SAPP_EVENTTYPE_UNFOCUSED })
            {
                SaveData.SetInt("coins", _writes + 5);                           // set, and never saved by the game
                var e = new Sokol.SApp.sapp_event { type = type };
                GameApplication.DispatchEvent(&e);
                Assert.False(SaveData.Changed);
            }
            Assert.Equal(3, _writes);
            var key = new Sokol.SApp.sapp_event { type = Sokol.SApp.sapp_event_type.SAPP_EVENTTYPE_KEY_DOWN };
            SaveData.SetInt("coins", 99);
            GameApplication.DispatchEvent(&key);
            Assert.Equal(3, _writes);                                            // any other event writes nothing
        }

        [Fact]
        public void A_projects_name_is_made_a_folders_name()
        {
            Assert.Equal("My_Game_2", SaveData.AppName(" My Game/2 "));
            Assert.Equal("Game", SaveData.AppName(null));
            Assert.Equal("Game", SaveData.AppName("   "));
        }
    }
}
