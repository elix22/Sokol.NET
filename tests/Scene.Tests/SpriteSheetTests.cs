using System.Numerics;
using Frent;
using GameEditor.Framework.Assets;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Scene;
using Xunit;

namespace Scene.Tests
{
    /// <summary>
    /// 2D, step G1.3: the sprite sheet file (grid cells, listed frames, pivots, clips, members it does not know),
    /// the sheets a scene names, the SpriteAnim block, and the animator's frame for each game step.
    /// </summary>
    public sealed class SpriteSheetTests : IDisposable
    {
        static readonly ECSWorld World = ECSWorld.Instance;
        const string HeroSheet = "Sprites/hero.sprites";
        const float Step = 1f / 60f;

        readonly Dictionary<string, string> _files = new();

        public SpriteSheetTests()
        {
            World.Clear();
            AssetDatabase.Clear();
            SpriteLibrary.Clear();
            SpriteLibrary.Reader = key => _files.TryGetValue(key, out string? text) ? text : null;
        }

        public void Dispose()
        {
            SpriteLibrary.Reader = null;
            SpriteLibrary.Clear();
            AssetDatabase.Clear();
            World.Clear();
        }

        static SpriteSheet Read(string json, string key = HeroSheet)
        {
            SpriteSheet? sheet = SpriteSheet.Parse(key, json, out string? error);
            Assert.True(sheet != null, error);
            return sheet!;
        }

        const string Grid32 = """
            { "sprites": 1, "image": "Textures/hero.png", "ppu": 32, "filter": "nearest",
              "grid": { "w": 32, "h": 32, "margin": 0, "spacing": 0 } }
            """;

        // ── Frames ──────────────────────────────────────────────────────────────────────

        [Fact]
        public void A_grid_cuts_the_image_row_by_row_from_the_top_left()
        {
            SpriteSheet sheet = Read(Grid32);
            Assert.Equal(32f, sheet.PixelsPerUnit);
            Assert.True(sheet.Nearest);
            var frames = sheet.FramesFor(128, 64);
            Assert.Equal(8, frames.Count);
            Assert.Equal("hero_0", frames[0].Name);
            Assert.Equal((0, 0, 32, 32), (frames[0].X, frames[0].Y, frames[0].W, frames[0].H));
            Assert.Equal("hero_5", frames[5].Name);
            Assert.Equal((32, 32), (frames[5].X, frames[5].Y));          // second row, second column
            Assert.Equal(new Vector2(0.5f, 0.5f), frames[5].Pivot);
            Assert.True(sheet.TryGetFrame("hero_7", 128, 64, out var last));
            Assert.Equal((96, 32), (last.X, last.Y));
        }

        [Fact]
        public void A_cell_that_does_not_fit_whole_is_not_a_frame_and_another_image_size_gives_other_frames()
        {
            SpriteSheet sheet = Read(Grid32);
            Assert.Equal(6, sheet.FramesFor(100, 70).Count);     // 3 across, 2 down
            Assert.Single(sheet.FramesFor(32, 32));
            Assert.Empty(sheet.FramesFor(31, 64));
            Assert.Equal(8, sheet.FramesFor(128, 64).Count);     // asked again with the first size
        }

        [Fact]
        public void Margin_and_spacing_move_the_cells()
        {
            SpriteSheet sheet = Read("""{ "sprites": 1, "image": "a.png", "grid": { "w": 16, "h": 16, "margin": 2, "spacing": 1 } }""", "Sprites/a.sprites");
            // 2 + 16 + 1 + 16 + 1 + 16 + 2 = 54 across holds three cells; 2 + 16 + 1 + 16 + 2 = 37 down holds two.
            Assert.Equal((3, 2), sheet.GridCells(54, 37));
            Assert.Equal((2, 2), sheet.GridCells(53, 37));
            var frames = sheet.FramesFor(54, 37);
            Assert.Equal("a_4", frames[4].Name);
            Assert.Equal((19, 19, 16, 16), (frames[4].X, frames[4].Y, frames[4].W, frames[4].H));
        }

        [Fact]
        public void Listed_frames_come_after_the_cells_and_one_named_as_a_cell_takes_its_place()
        {
            SpriteSheet sheet = Read("""
                { "sprites": 1, "image": "Textures/hero.png",
                  "grid": { "w": 32, "h": 32 },
                  "frames": [ { "name": "crouch", "x": 96, "y": 64, "w": 32, "h": 24, "pivot": [0.5, 0] },
                              { "name": "hero_1", "pivot": [0, 1] },
                              { "name": "hero_2", "x": 70, "y": 2, "w": 20, "h": 28 },
                              { "name": "nowhere" } ] }
                """);
            var frames = sheet.FramesFor(128, 96);
            Assert.Equal(13, frames.Count);                       // twelve cells and "crouch"; "nowhere" has no rectangle
            Assert.Equal("crouch", frames[12].Name);
            Assert.Equal((96, 64, 32, 24), (frames[12].X, frames[12].Y, frames[12].W, frames[12].H));
            Assert.Equal(new Vector2(0.5f, 0f), frames[12].Pivot);
            // Its own pivot, the cell's rectangle, and still the second frame.
            Assert.Equal("hero_1", frames[1].Name);
            Assert.Equal((32, 0, 32, 32), (frames[1].X, frames[1].Y, frames[1].W, frames[1].H));
            Assert.Equal(new Vector2(0f, 1f), frames[1].Pivot);
            // Its own rectangle, in the cell's place.
            Assert.Equal((70, 2, 20, 28), (frames[2].X, frames[2].Y, frames[2].W, frames[2].H));
            Assert.Equal("hero_3", frames[3].Name);
        }

        [Fact]
        public void A_frame_name_the_sheet_does_not_have_is_not_found()
        {
            SpriteSheet sheet = Read(Grid32);
            Assert.False(sheet.TryGetFrame("hero_8", 128, 64, out _));
            Assert.False(sheet.TryGetFrame("Hero_0", 128, 64, out _));
            Assert.False(sheet.TryGetFrame("", 128, 64, out _));
        }

        [Fact]
        public void The_cells_keep_their_names_when_the_sheet_file_is_renamed()
        {
            // Without a name in the grid the cells are named after the file; a written sheet carries the name.
            SpriteSheet first = Read(Grid32);
            Assert.Equal("hero_0", first.FramesFor(64, 32)[0].Name);
            string written = first.ToJson();
            Assert.Contains("\"name\": \"hero\"", written);
            SpriteSheet moved = Read(written, "Art/player.sprites");
            Assert.Equal("hero_0", moved.FramesFor(64, 32)[0].Name);
            Assert.Equal("player_0", Read(Grid32, "Art/player.sprites").FramesFor(64, 32)[0].Name);
        }

        // ── The file ────────────────────────────────────────────────────────────────────

        [Fact]
        public void A_text_that_is_not_a_sheet_is_refused_with_a_reason()
        {
            Assert.Null(SpriteSheet.Parse(HeroSheet, "{ \"image\": \"a.png\" }", out string? noMember));
            Assert.Contains("sprites", noMember);
            Assert.Null(SpriteSheet.Parse(HeroSheet, "[1, 2]", out _));
            Assert.Null(SpriteSheet.Parse(HeroSheet, "{ \"sprites\": 1, ", out string? broken));
            Assert.NotNull(broken);
            Assert.Null(SpriteSheet.Parse(HeroSheet, "{ \"sprites\": 1, \"grid\": { \"w\": \"wide\" } }", out _));
        }

        [Fact]
        public void A_sheet_with_only_its_image_has_the_defaults()
        {
            SpriteSheet sheet = Read("""{ "sprites": 1, "image": "Textures/logo.png" }""");
            Assert.Equal(100f, sheet.PixelsPerUnit);
            Assert.False(sheet.Nearest);
            Assert.False(sheet.HasGrid);
            Assert.Empty(sheet.FramesFor(64, 64));
            Assert.Empty(sheet.Clips);
        }

        [Fact]
        public void Members_the_sheet_does_not_know_are_kept_when_it_is_written()
        {
            const string text = """
                { "sprites": 1, "image": "Textures/hero.png", "ppu": 16, "filter": "nearest",
                  "tool": { "made by": "another program", "v": [1, 2] },
                  "grid": { "w": 16, "h": 16, "margin": 1, "spacing": 2, "guide": "#ff0000" },
                  "frames": [ { "name": "crouch", "x": 1, "y": 2, "w": 3, "h": 4, "pivot": [0.25, 0.75], "tag": "low" } ],
                  "clips": { "walk": { "frames": ["hero_0", "hero_1"], "fps": 12, "loop": false, "sound": "step.wav" } },
                  "note": "kept" }
                """;
            SpriteSheet sheet = Read(text);
            string written = sheet.ToJson();
            Assert.Contains("\"tool\": { \"made by\": \"another program\", \"v\": [1, 2] }", written);
            Assert.Contains("\"guide\": \"#ff0000\"", written);
            Assert.Contains("\"tag\": \"low\"", written);
            Assert.Contains("\"sound\": \"step.wav\"", written);
            Assert.Contains("\"note\": \"kept\"", written);

            // What is known comes back as it was, and writing a second time changes nothing.
            SpriteSheet again = Read(written);
            Assert.Equal(written, again.ToJson());
            Assert.Equal(16f, again.PixelsPerUnit);
            Assert.Equal((16, 16, 1, 2), (again.CellW, again.CellH, again.Margin, again.Spacing));
            Assert.Equal(new Vector2(0.25f, 0.75f), again.Frames[0].Pivot);
            SpriteClip walk = again.Clip("walk")!;
            Assert.Equal(new[] { "hero_0", "hero_1" }, walk.Frames);
            Assert.Equal(12f, walk.Fps);
            Assert.False(walk.Loop);
            Assert.Null(again.Clip("run"));
        }

        [Fact]
        public void A_sheet_finds_its_image_after_the_image_moved()
        {
            AssetDatabase.Set(1.ToString("x32"), "Assets/Textures/hero.png");
            string saved = AssetRefs.Stamp(Read(Grid32).ToJson());
            Assert.Single(AssetRefs.ReadTable(saved)!);

            AssetDatabase.Rename("Assets/Textures/hero.png", "Assets/Art/hero_sheet.png");
            SpriteSheet moved = Read(saved);
            Assert.Equal("Art/hero_sheet.png", moved.Image);
            Assert.Equal("hero_0", moved.FramesFor(64, 32)[0].Name);     // named by the sheet, not by the image
            string again = AssetRefs.Stamp(moved.ToJson());
            Assert.DoesNotContain("Textures/hero.png", again);
        }

        [Fact]
        public void The_sheets_a_scene_names_are_found_once_each_with_and_without_a_frame()
        {
            const string scene = """
                { "name": "s", "entities": [
                  { "id": 1, "c": { "Sprite": { "Tex": "Sprites/hero.sprites#hero_0" } } },
                  { "id": 2, "c": { "Sprite": { "Tex": "Sprites/hero.sprites#hero_3" }, "Script": { "Props": { "Tiles": "Sprites/tiles.SPRITES" } } } },
                  { "id": 3, "c": { "Sprite": { "Tex": "Textures/logo.png" }, "Note": "about.sprites.txt" } } ],
                  "assets": { "Sprites/old.sprites": "00000000000000000000000000000009" } }
                """;
            Assert.Equal(new[] { "Sprites/hero.sprites", "Sprites/tiles.SPRITES" }, SpriteLibrary.SheetKeysIn(scene));
            Assert.Empty(SpriteLibrary.SheetKeysIn("""{ "name": "s", "entities": [] }"""));
            Assert.Empty(SpriteLibrary.SheetKeysIn("not json .sprites"));
        }

        [Fact]
        public void The_library_reads_a_sheet_once_and_again_after_it_is_forgotten()
        {
            _files[HeroSheet] = Grid32;
            Assert.Equal(SpriteLibrary.Answer.Ready, SpriteLibrary.FindSheet(HeroSheet, out SpriteSheet? first));
            Assert.Same(first, SpriteLibrary.Sheet(HeroSheet));
            Assert.Equal(SpriteLibrary.Answer.Missing, SpriteLibrary.FindSheet("Sprites/none.sprites", out _));

            _files[HeroSheet] = Grid32.Replace("\"ppu\": 32", "\"ppu\": 8");
            Assert.Equal(32f, SpriteLibrary.Sheet(HeroSheet)!.PixelsPerUnit);        // still the one that was read
            SpriteLibrary.Forget(HeroSheet);
            Assert.Equal(8f, SpriteLibrary.Sheet(HeroSheet)!.PixelsPerUnit);

            _files["Sprites/bad.sprites"] = "{ }";
            Assert.Null(SpriteLibrary.Sheet("Sprites/bad.sprites"));
            Assert.NotNull(SpriteLibrary.PutSheet("Sprites/bad.sprites", Grid32));   // handed over: replaces what was known
            Assert.NotNull(SpriteLibrary.Sheet("Sprites/bad.sprites"));
        }

        // ── The SpriteAnim block ────────────────────────────────────────────────────────

        [Fact]
        public void The_SpriteAnim_block_round_trips_and_what_a_running_game_keeps_is_not_written()
        {
            World.Clear();
            var scene = new GameEditor.Framework.Scene.Scene("t");
            Entity e = scene.CreateEntity("Walker");
            World.AddComponent(e, new SpriteAnimator { Clip = "walk \"fast\"", Speed = 1.5f, Playing = false, Frame = 3, Time = 0.4f, Current = "walk", Sheet = HeroSheet });
            string json = SceneSerializer.Serialize(scene);
            Assert.Contains("\"SpriteAnim\":{\"Clip\":\"walk \\\"fast\\\"\",\"Speed\":1.5,\"Playing\":false}", json);

            var loaded = new GameEditor.Framework.Scene.Scene("x");
            SceneSerializer.Deserialize(json, loaded);
            SpriteAnimator a = World.GetComponent<SpriteAnimator>(Only());
            Assert.Equal(("walk \"fast\"", 1.5f, false), (a.Clip, a.Speed, a.Playing));
            Assert.Equal((0, 0f), (a.Frame, a.Time));
            Assert.Null(a.Current);
            Assert.Equal(json, SceneSerializer.Serialize(loaded));
        }

        [Fact]
        public void A_SpriteAnim_block_with_no_members_plays_at_speed_one()
        {
            var loaded = new GameEditor.Framework.Scene.Scene("x");
            SceneSerializer.Deserialize("""{"name":"s","entities":[{"id":1,"name":"A","c":{"SpriteAnim":{}}}]}""", loaded);
            SpriteAnimator a = World.GetComponent<SpriteAnimator>(Only());
            Assert.Equal(("", 1f, true), (a.Clip, a.Speed, a.Playing));
        }

        static Entity Only()
        {
            foreach (Entity e in World.Entities) return e;
            throw new Xunit.Sdk.XunitException("the world is empty");
        }

        // ── The animator ────────────────────────────────────────────────────────────────

        const string Clips = """
            { "sprites": 1, "image": "Textures/hero.png", "grid": { "w": 32, "h": 32 },
              "clips": { "walk":  { "frames": ["hero_4", "hero_5", "hero_6", "hero_7"], "fps": 10, "loop": true },
                         "jump":  { "frames": ["hero_1", "hero_2", "hero_3"], "fps": 20, "loop": false },
                         "still": { "frames": ["hero_0"], "fps": 0 },
                         "empty": { "frames": [] } } }
            """;

        Entity Animated(string clip, string tex = HeroSheet + "#hero_0", float speed = 1f, bool playing = true)
        {
            _files[HeroSheet] = Clips;
            World.Clear();
            Entity e = World.CreateEntity("Walker");
            var sprite = SpriteRenderer.Default; sprite.Texture = tex;
            World.AddComponent(e, sprite);
            World.AddComponent(e, new SpriteAnimator { Clip = clip, Speed = speed, Playing = playing });
            return e;
        }

        static string Shown(Entity e) => World.GetComponent<SpriteRenderer>(e).Texture;
        static string Frame(string name) => HeroSheet + "#" + name;

        [Fact]
        public void A_looping_clip_shows_each_frame_for_its_time_and_starts_over()
        {
            Entity e = Animated("walk");
            // 10 frames a second at 60 steps a second: six steps a frame. The first step shows the first frame.
            string[] names = { "hero_4", "hero_5", "hero_6", "hero_7" };
            for (int step = 1; step <= 60; step++)
            {
                SpriteAnimSystem.Update(World, Step);
                int frame = (step - 1) / 6 % 4;
                Assert.Equal(Frame(names[frame]), Shown(e));
                Assert.Equal(frame, World.GetComponent<SpriteAnimator>(e).Frame);
            }
            Assert.True(World.GetComponent<SpriteAnimator>(e).Playing);
        }

        [Fact]
        public void A_frame_changes_at_its_step_when_the_steps_summed_fall_a_hair_short_of_the_moment()
        {
            // Fifty steps a second: five steps a frame. Five steps of 1/50 s summed as floats are a hair less than
            // a tenth of a second; the frame changes at the sixth step all the same.
            Entity e = Animated("walk");
            string[] names = { "hero_4", "hero_5", "hero_6", "hero_7" };
            for (int step = 1; step <= 100; step++)
            {
                SpriteAnimSystem.Update(World, 1f / 50f);
                Assert.Equal(Frame(names[(step - 1) / 5 % 4]), Shown(e));
            }
        }

        [Fact]
        public void A_clip_that_does_not_loop_stops_on_its_last_frame_after_showing_it_and_plays_again_when_asked()
        {
            Entity e = Animated("jump");
            // 20 frames a second: three steps a frame, nine steps in all.
            string[] names = { "hero_1", "hero_2", "hero_3" };
            for (int step = 1; step <= 9; step++)
            {
                SpriteAnimSystem.Update(World, Step);
                Assert.Equal(Frame(names[(step - 1) / 3]), Shown(e));
                Assert.True(World.GetComponent<SpriteAnimator>(e).Playing, $"step {step}");
            }
            SpriteAnimSystem.Update(World, Step);
            Assert.False(World.GetComponent<SpriteAnimator>(e).Playing);
            for (int step = 0; step < 20; step++) SpriteAnimSystem.Update(World, Step);
            Assert.Equal(Frame("hero_3"), Shown(e));
            Assert.Equal(2, World.GetComponent<SpriteAnimator>(e).Frame);

            World.GetComponent<SpriteAnimator>(e).Playing = true;
            SpriteAnimSystem.Update(World, Step);
            Assert.Equal(Frame("hero_1"), Shown(e));
            Assert.True(World.GetComponent<SpriteAnimator>(e).Playing);
        }

        [Fact]
        public void Another_clip_starts_at_its_first_frame()
        {
            Entity e = Animated("walk");
            for (int step = 0; step < 10; step++) SpriteAnimSystem.Update(World, Step);
            Assert.Equal(Frame("hero_5"), Shown(e));

            World.GetComponent<SpriteAnimator>(e).Clip = "jump";
            SpriteAnimSystem.Update(World, Step);
            Assert.Equal(Frame("hero_1"), Shown(e));
            for (int step = 0; step < 3; step++) SpriteAnimSystem.Update(World, Step);
            Assert.Equal(Frame("hero_2"), Shown(e));

            World.GetComponent<SpriteAnimator>(e).Clip = "walk";
            SpriteAnimSystem.Update(World, Step);
            Assert.Equal(Frame("hero_4"), Shown(e));
        }

        [Fact]
        public void Speed_scales_the_time_and_a_negative_one_plays_a_looping_clip_backwards()
        {
            Entity fast = Animated("walk", speed: 2f);
            for (int step = 1; step <= 4; step++) SpriteAnimSystem.Update(World, Step);
            Assert.Equal(Frame("hero_5"), Shown(fast));          // three steps a frame now

            Entity back = Animated("walk", speed: -1f);
            SpriteAnimSystem.Update(World, Step);
            Assert.Equal(Frame("hero_4"), Shown(back));
            SpriteAnimSystem.Update(World, Step);
            Assert.Equal(Frame("hero_7"), Shown(back));
            for (int step = 0; step < 6; step++) SpriteAnimSystem.Update(World, Step);
            Assert.Equal(Frame("hero_6"), Shown(back));
        }

        [Fact]
        public void An_animator_that_is_not_playing_stands_still_and_leaves_the_sprite_to_a_script()
        {
            Entity e = Animated("walk");
            for (int step = 0; step < 8; step++) SpriteAnimSystem.Update(World, Step);
            Assert.Equal(Frame("hero_5"), Shown(e));

            World.GetComponent<SpriteAnimator>(e).Playing = false;
            World.GetComponent<SpriteRenderer>(e).Texture = Frame("hero_0");
            for (int step = 0; step < 30; step++) SpriteAnimSystem.Update(World, Step);
            Assert.Equal(Frame("hero_0"), Shown(e));

            // Playing again goes on from where the clip stood: the eighth step's time, plus this one.
            World.GetComponent<SpriteAnimator>(e).Playing = true;
            SpriteAnimSystem.Update(World, Step);
            Assert.Equal(Frame("hero_5"), Shown(e));
        }

        [Fact]
        public void An_animator_that_starts_not_playing_shows_its_clips_first_frame()
        {
            Entity e = Animated("walk", playing: false);
            for (int step = 0; step < 30; step++) SpriteAnimSystem.Update(World, Step);
            Assert.Equal(Frame("hero_4"), Shown(e));
        }

        [Fact]
        public void What_cannot_be_played_leaves_the_sprite_as_it_is()
        {
            Entity noClip = Animated("fly");
            SpriteAnimSystem.Update(World, Step);
            Assert.Equal(Frame("hero_0"), Shown(noClip));

            Entity empty = Animated("empty");
            SpriteAnimSystem.Update(World, Step);
            Assert.Equal(Frame("hero_0"), Shown(empty));

            Entity image = Animated("walk", tex: "Textures/hero.png");
            SpriteAnimSystem.Update(World, Step);
            Assert.Equal("Textures/hero.png", Shown(image));

            Entity noSheet = Animated("walk", tex: "Sprites/none.sprites#a");
            SpriteAnimSystem.Update(World, Step);
            Assert.Equal("Sprites/none.sprites#a", Shown(noSheet));

            Entity inactive = Animated("walk");
            World.AddComponent(inactive, new ActiveFlag { Active = false });
            SpriteAnimSystem.Update(World, Step);
            Assert.Equal(Frame("hero_0"), Shown(inactive));
        }

        [Fact]
        public void A_clip_of_no_speed_shows_its_first_frame_and_a_sprite_naming_only_the_sheet_is_animated()
        {
            Entity still = Animated("still");
            for (int step = 0; step < 5; step++) SpriteAnimSystem.Update(World, Step);
            Assert.Equal(Frame("hero_0"), Shown(still));

            Entity whole = Animated("walk", tex: HeroSheet);
            SpriteAnimSystem.Update(World, Step);
            Assert.Equal(Frame("hero_4"), Shown(whole));
        }

        [Fact]
        public void A_sprite_given_another_sheet_plays_that_sheets_clip_from_its_start()
        {
            Entity e = Animated("walk");
            _files["Sprites/foe.sprites"] = Clips.Replace("hero_4", "foe_9");
            for (int step = 0; step < 10; step++) SpriteAnimSystem.Update(World, Step);
            Assert.Equal(Frame("hero_5"), Shown(e));

            World.GetComponent<SpriteRenderer>(e).Texture = "Sprites/foe.sprites#foe_0";
            SpriteAnimSystem.Update(World, Step);
            Assert.Equal("Sprites/foe.sprites#foe_9", Shown(e));
        }

        [Fact]
        public void Playing_allocates_nothing_once_the_sheet_is_read()
        {
            Entity e = Animated("walk");
            for (int step = 0; step < 30; step++) SpriteAnimSystem.Update(World, Step);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int step = 0; step < 600; step++) SpriteAnimSystem.Update(World, Step);
            Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
            Assert.StartsWith(HeroSheet + "#hero_", Shown(e));
        }
    }
}
