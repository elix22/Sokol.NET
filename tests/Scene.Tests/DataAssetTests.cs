using System;
using System.Collections.Generic;
using GameEditor.Framework.Scripting;
using Xunit;

namespace Scene.Tests
{
    /// <summary>§6 part 4 (examples/GameEditor/docs/EDITOR_ENHANCEMENTS_DESIGN.md §6.2): data assets - one shared instance
    /// per key, set by the generated setter (a game compiled ahead of time) or by name, the keys a scene names.</summary>
    public sealed class DataAssetTests : IDisposable
    {
        [DataAsset]
        public sealed class Tuning
        {
            public float Speed = 3;
            public int Hp = 10;
            public List<string> Names = new();
        }

        static readonly Dictionary<string, string> Files = new()
        {
            ["Data/Fast.asset.json"] = "{\"Type\": \"Tuning\", \"Props\": {\"Speed\": \"6.5\", \"Names\": \"[\\\"Ann\\\",\\\"Bo\\\"]\"}}",
            ["Data/Bell.asset.json"] = "{\"Type\": \"EventChannel\", \"Props\": {}}",
            ["Data/Broken.asset.json"] = "{\"Type\": ",
        };

        public DataAssetTests()
        {
            DataAssets.Clear();
            DataAssets.Reader = key => Files.TryGetValue(key, out var t) ? t : null;
        }

        public void Dispose()
        {
            DataAssets.Reader = null;
            DataAssets.Clear();
        }

        [Fact]
        public void One_instance_per_key_its_fields_from_the_file_by_name()
        {
            var a = DataAssets.Get<Tuning>("Data/Fast.asset.json");
            var b = DataAssets.Get<Tuning>("Data/Fast.asset.json");
            Assert.NotNull(a);
            Assert.Same(a, b);                                    // shared: both scripts hold the same one
            Assert.Equal(6.5f, a!.Speed);
            Assert.Equal(10, a.Hp);                               // not in the file: the class's default
            Assert.Equal(new List<string> { "Ann", "Bo" }, a.Names);
            DataAssets.ForgetInstances();                         // Play stopped: the next Play reads the file again
            Assert.NotSame(a, DataAssets.Get<Tuning>("Data/Fast.asset.json"));
        }

        [Fact]
        public void The_generated_setter_sets_its_fields_with_no_reflection()
        {
            int called = 0;
            DataAssets.Register<Tuning>((object asset, string field, in ScriptFieldValue value) =>
            {
                called++;
                var s = (Tuning)asset;
                switch (field)
                {
                    case "Speed": return ScriptValue.Set(ref s.Speed, value);
                    case "Names": return ScriptValue.Set(ref s.Names, value);
                    default: return false;
                }
            });
            var a = DataAssets.Get<Tuning>("Data/Fast.asset.json")!;
            Assert.Equal(2, called);
            Assert.Equal(6.5f, a.Speed);
            Assert.Equal(2, a.Names.Count);
        }

        [Fact]
        public void A_script_s_field_and_a_list_of_them_get_the_shared_asset_none_a_missing_or_wrong_one()
        {
            Tuning? field = null;
            Assert.True(ScriptValue.SetData(ref field, new ScriptFieldValue("Data/Fast.asset.json")));
            Assert.Same(DataAssets.Get<Tuning>("Data/Fast.asset.json"), field);
            var list = new List<Tuning>();
            Assert.True(ScriptValue.Set(ref list, new ScriptFieldValue("[\"Data/Fast.asset.json\",\"\"]")));
            Assert.Same(field, list[0]);
            Assert.Null(list[1]);
            Assert.Null(DataAssets.Get<Tuning>("Data/Bell.asset.json"));      // an EventChannel, not a Tuning
            Assert.Null(DataAssets.Get<Tuning>("Data/Missing.asset.json"));
            Assert.Null(DataAssets.Get<Tuning>("Data/Broken.asset.json"));
            Assert.Null(DataAssets.Get<Tuning>(""));
        }

        [Fact]
        public void The_engine_s_event_channel_is_shared_and_raised()
        {
            var bell = DataAssets.Get<EventChannel>("Data/Bell.asset.json")!;
            int heard = 0;
            DataAssets.Get<EventChannel>("Data/Bell.asset.json")!.Raised += () => heard++;
            bell.Raise();
            Assert.Equal(1, heard);
        }

        [Fact]
        public void A_scene_s_keys_are_its_texts_ending_in_asset_json_lists_of_them_too()
        {
            string scene = "{\"Entities\":[{\"Script\":{\"Type\":\"W\",\"Props\":{\"Tune\":\"Data/Fast.asset.json\",\"All\":\"[\\\"Data/A.asset.json\\\",\\\"Data/B.asset.json\\\"]\",\"N\":\"3\"}}}]}";
            Assert.Equal(new List<string> { "Data/Fast.asset.json", "Data/A.asset.json", "Data/B.asset.json" }, DataAssets.KeysIn(scene));
        }
    }
}
