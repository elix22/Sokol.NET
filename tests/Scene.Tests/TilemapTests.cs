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
    /// 2D, step G1.4: the tile map file (rows as the map looks, an origin anywhere, members it does not know),
    /// cells set, filled and resized, the maps a scene names, and the Tilemap block.
    /// </summary>
    public sealed class TilemapTests : IDisposable
    {
        static readonly ECSWorld World = ECSWorld.Instance;
        const string Level = "Tilemaps/level1.tilemap";

        readonly Dictionary<string, string> _files = new();

        public TilemapTests()
        {
            World.Clear();
            AssetDatabase.Clear();
            TilemapLibrary.Clear();
            TilemapLibrary.Reader = key => _files.TryGetValue(key, out string? text) ? text : null;
        }

        public void Dispose()
        {
            TilemapLibrary.Reader = null;
            TilemapLibrary.Clear();
            AssetDatabase.Clear();
            World.Clear();
        }

        static TilemapData Read(string json, string key = Level)
        {
            TilemapData? map = TilemapData.Parse(key, json, out string? error);
            Assert.True(map != null, error);
            return map!;
        }

        const string Room = """
            { "tilemap": 1, "tiles": "Sprites/terrain.sprites", "cell": [1, 1],
              "layers": [
                { "name": "ground", "order": 0, "origin": [-2, -1], "solid": true,
                  "rows": [ [0, 0, 0, 0, 0, 0],
                            [0, 0, 7, 0, 0, 0],
                            [1, 2, 2, 2, 2, 3] ] } ] }
            """;

        // ── Rows and cells ──────────────────────────────────────────────────────────────

        [Fact]
        public void The_first_row_written_is_the_top_one_and_the_origin_is_the_bottom_left_cell()
        {
            TilemapData map = Read(Room);
            Assert.Equal("Sprites/terrain.sprites", map.Tiles);
            Assert.Equal(Vector2.One, map.Cell);
            TileLayer ground = map.Layer("ground")!;
            Assert.True(ground.Solid);
            Assert.Equal((-2, -1, 6, 3), (ground.OriginX, ground.OriginY, ground.Width, ground.Height));
            // The bottom row sits at y = -1: 1 at the left end (x = -2), 3 at the right (x = 3).
            Assert.Equal(1, ground.Get(-2, -1));
            Assert.Equal(2, ground.Get(-1, -1));
            Assert.Equal(3, ground.Get(3, -1));
            Assert.Equal(7, ground.Get(0, 0));          // the middle row, third column
            Assert.Equal(0, ground.Get(0, 1));          // the top row
            Assert.Equal(0, ground.Get(-3, -1));        // outside
            Assert.Equal(0, ground.Get(0, 2));
            Assert.Null(map.Layer("sky"));
        }

        [Fact]
        public void A_map_written_and_read_again_is_the_same_text_and_the_text_looks_like_the_map()
        {
            TilemapData map = Read(Room);
            string written = map.ToJson();
            Assert.Contains("        [0, 0, 0, 0, 0, 0],\n        [0, 0, 7, 0, 0, 0],\n        [1, 2, 2, 2, 2, 3]", written);
            Assert.Contains("\"origin\": [-2, -1]", written);
            Assert.Equal(written, Read(written).ToJson());

            // Numbers of two digits keep their columns.
            map.Layers[0].Set(1, 1, 12);
            Assert.Contains("        [ 0,  0,  0, 12,  0,  0],\n        [ 0,  0,  7,  0,  0,  0],\n        [ 1,  2,  2,  2,  2,  3]", map.ToJson());
            Assert.Equal(12, Read(map.ToJson()).Layers[0].Get(1, 1));
        }

        [Fact]
        public void Rows_of_unequal_length_end_in_empty_cells_and_a_layer_with_no_rows_is_empty()
        {
            TilemapData map = Read("""
                { "tilemap": 1, "tiles": "t.sprites", "layers": [
                  { "name": "a", "rows": [ [1], [2, 3, 4], [] ] }, { "name": "b" }, { "name": "c", "rows": [] } ] }
                """);
            TileLayer a = map.Layers[0];
            Assert.Equal((3, 3), (a.Width, a.Height));
            Assert.Equal(1, a.Get(0, 2));
            Assert.Equal(0, a.Get(1, 2));
            Assert.Equal(4, a.Get(2, 1));
            Assert.Equal(0, a.Get(0, 0));
            Assert.Equal((0, 0), (map.Layers[1].Width, map.Layers[1].Height));
            Assert.False(map.Layers[1].Used(out _, out _, out _, out _));
            Assert.Equal(map.ToJson(), Read(map.ToJson()).ToJson());
        }

        [Fact]
        public void A_cell_set_outside_the_layer_makes_it_larger_and_what_was_there_stays_in_its_cell()
        {
            TileLayer ground = Read(Room).Layers[0];
            Assert.False(ground.Set(0, 0, 7));                // as it is
            Assert.False(ground.Set(10, 10, 0));              // nothing put outside
            Assert.Equal(6, ground.Width);

            Assert.True(ground.Set(-5, 4, 9));                // above and left of the layer
            Assert.Equal((-5, -1, 9, 6), (ground.OriginX, ground.OriginY, ground.Width, ground.Height));
            Assert.Equal(9, ground.Get(-5, 4));
            Assert.Equal(7, ground.Get(0, 0));
            Assert.Equal(1, ground.Get(-2, -1));
            Assert.Equal(3, ground.Get(3, -1));

            Assert.True(ground.Set(6, -3, 5));                // below and right
            Assert.Equal((-5, -3, 12, 8), (ground.OriginX, ground.OriginY, ground.Width, ground.Height));
            Assert.Equal(5, ground.Get(6, -3));
            Assert.Equal(9, ground.Get(-5, 4));
            Assert.Equal(7, ground.Get(0, 0));

            var empty = new TileLayer();
            Assert.True(empty.Set(-3, 2, 4));
            Assert.Equal((-3, 2, 1, 1), (empty.OriginX, empty.OriginY, empty.Width, empty.Height));
        }

        [Fact]
        public void A_rectangle_is_filled_and_emptied_and_the_count_is_of_the_cells_that_changed()
        {
            TileLayer ground = Read(Room).Layers[0];
            Assert.Equal(5, ground.Fill(-1, 0, 3, 2, 7));     // six cells, one of them a 7 already
            for (int y = 0; y < 2; y++)
                for (int x = -1; x < 2; x++) Assert.Equal(7, ground.Get(x, y));
            Assert.Equal(0, ground.Get(2, 0));
            Assert.Equal(6, ground.Width);

            Assert.Equal(4, ground.Fill(3, 1, 2, 2, 4));      // half of it outside: the layer grows once
            Assert.Equal((-2, -1, 7, 4), (ground.OriginX, ground.OriginY, ground.Width, ground.Height));
            Assert.Equal(4, ground.Get(4, 2));

            Assert.Equal(6, ground.Fill(-1, 0, 3, 2, 0));     // emptied
            Assert.Equal(0, ground.Get(0, 0));
            Assert.Equal(0, ground.Fill(20, 20, 3, 3, 0));    // emptying outside changes nothing, and nothing grows
            Assert.Equal(7, ground.Width);
            Assert.Equal(0, ground.Fill(0, 0, 0, 5, 3));
        }

        [Fact]
        public void Resize_keeps_what_lies_in_both_rectangles_and_Used_is_the_rectangle_of_the_tiles()
        {
            TileLayer ground = Read(Room).Layers[0];
            Assert.True(ground.Used(out int x, out int y, out int w, out int h));
            Assert.Equal((-2, -1, 6, 2), (x, y, w, h));       // the top row is empty

            ground.Resize(0, -1, 10, 5);                      // cut on the left, larger to the right and up
            Assert.Equal((0, -1, 10, 5), (ground.OriginX, ground.OriginY, ground.Width, ground.Height));
            Assert.Equal(7, ground.Get(0, 0));
            Assert.Equal(2, ground.Get(0, -1));
            Assert.Equal(3, ground.Get(3, -1));
            Assert.Equal(0, ground.Get(-1, -1));              // was 2: cut away
            Assert.Equal(0, ground.Get(9, 3));

            ground.Resize(0, 0, 0, 0);
            Assert.False(ground.Used(out _, out _, out _, out _));
        }

        // ── The file ────────────────────────────────────────────────────────────────────

        [Fact]
        public void A_text_that_is_not_a_tile_map_is_refused_with_a_reason()
        {
            Assert.Null(TilemapData.Parse(Level, "{ \"tiles\": \"a.sprites\" }", out string? noMember));
            Assert.Contains("tilemap", noMember);
            Assert.Null(TilemapData.Parse(Level, "{ \"tilemap\": 1, \"layers\": [ { \"rows\": [ [\"grass\"] ] } ] }", out _));
            Assert.Null(TilemapData.Parse(Level, "{ \"tilemap\": 1, ", out _));
        }

        [Fact]
        public void Members_the_map_does_not_know_are_kept_and_a_negative_tile_is_empty()
        {
            TilemapData map = Read("""
                { "tilemap": 1, "tiles": "t.sprites", "cell": [0.5, 0.25], "editor": { "zoom": 2 },
                  "layers": [ { "name": "a", "tint": "#ff0000", "rows": [ [ -4, 5 ] ] } ], "note": "kept" }
                """);
            Assert.Equal(new Vector2(0.5f, 0.25f), map.Cell);
            Assert.Equal(0, map.Layers[0].Get(0, 0));
            string written = map.ToJson();
            Assert.Contains("\"editor\": { \"zoom\": 2 }", written);
            Assert.Contains("\"tint\": \"#ff0000\"", written);
            Assert.Contains("\"note\": \"kept\"", written);
            Assert.Contains("\"cell\": [0.5, 0.25]", written);
            Assert.Equal(written, Read(written).ToJson());
        }

        [Fact]
        public void A_map_finds_its_sheet_after_the_sheet_moved()
        {
            AssetDatabase.Set(1.ToString("x32"), "Assets/Sprites/terrain.sprites");
            string saved = AssetRefs.Stamp(Read(Room).ToJson());
            Assert.Single(AssetRefs.ReadTable(saved)!);
            AssetDatabase.Rename("Assets/Sprites/terrain.sprites", "Assets/Art/ground.sprites");
            Assert.Equal("Art/ground.sprites", Read(saved).Tiles);
        }

        [Fact]
        public void The_maps_a_scene_names_are_found_once_each_and_the_library_reads_and_forgets()
        {
            const string scene = """
                { "name": "s", "entities": [
                  { "id": 1, "c": { "Tilemap": { "Map": "Tilemaps/level1.tilemap" } } },
                  { "id": 2, "c": { "Tilemap": { "Map": "Tilemaps/level1.tilemap" }, "Script": { "Props": { "Next": "Tilemaps/level2.TILEMAP" } } } } ],
                  "note": "level1.tilemap.bak" }
                """;
            Assert.Equal(new[] { "Tilemaps/level1.tilemap", "Tilemaps/level2.TILEMAP" }, TilemapLibrary.KeysIn(scene));
            Assert.Empty(TilemapLibrary.KeysIn("""{ "name": "s", "entities": [] }"""));

            _files[Level] = Room;
            int version = TilemapLibrary.Version;
            TilemapData first = TilemapLibrary.Map(Level)!;
            Assert.Same(first, TilemapLibrary.Map(Level));
            Assert.True(TilemapLibrary.Version > version);
            Assert.Null(TilemapLibrary.Map("Tilemaps/none.tilemap"));
            Assert.Equal(SpriteLibrary.Answer.Missing, TilemapLibrary.Find("", out _));

            _files[Level] = Room.Replace("\"cell\": [1, 1]", "\"cell\": [2, 2]");
            Assert.Equal(Vector2.One, TilemapLibrary.Map(Level)!.Cell);          // still the one that was read
            TilemapLibrary.Forget(Level);
            Assert.Equal(new Vector2(2f, 2f), TilemapLibrary.Map(Level)!.Cell);

            // A map handed over as it is — an editor's, being painted — is the one that is found.
            var painted = Read(Room);
            painted.Layers[0].Set(0, 0, 3);
            TilemapLibrary.Put(painted);
            Assert.Same(painted, TilemapLibrary.Map(Level));
        }

        // ── The cells a view shows ──────────────────────────────────────────────────────

        static Matrix4x4 InverseOrtho(Vector3 eye, float halfHeight, float aspect)
        {
            Matrix4x4 view = Matrix4x4.CreateLookAt(eye, new Vector3(eye.X, eye.Y, 0f), Vector3.UnitY);
            Matrix4x4 proj = Matrix4x4.CreateOrthographic(2f * halfHeight * aspect, 2f * halfHeight, 0.1f, 100f);
            Assert.True(Matrix4x4.Invert(view * proj, out Matrix4x4 inverse));
            return inverse;
        }

        [Fact]
        public void The_cells_in_view_are_those_the_pictures_corners_reach()
        {
            // A camera of size 4.75 on a 16:9 picture shows x from -8.44 to 8.44 and y from -4.75 to 4.75.
            Matrix4x4 inverse = InverseOrtho(new Vector3(0f, 0f, 10f), 4.75f, 16f / 9f);
            Assert.True(GameEditor.Framework.Renderer.Server.Sprites2D.SpriteGeometry.CellsInView(in inverse, Vector3.Zero, Vector3.UnitX, Vector3.UnitY, out int x0, out int y0, out int x1, out int y1));
            Assert.Equal((-9, -5, 8, 4), (x0, y0, x1, y1));

            // The map moved to (2, 1) with cells of half a unit: twice as many cells, counted from its own corner.
            Assert.True(GameEditor.Framework.Renderer.Server.Sprites2D.SpriteGeometry.CellsInView(in inverse, new Vector3(2f, 1f, 0f), Vector3.UnitX * 0.5f, Vector3.UnitY * 0.5f, out x0, out y0, out x1, out y1));
            Assert.Equal((-21, -12, 12, 7), (x0, y0, x1, y1));

            // The camera moved: the range moves with it.
            inverse = InverseOrtho(new Vector3(100f, -50f, 10f), 1.75f, 1f);
            Assert.True(GameEditor.Framework.Renderer.Server.Sprites2D.SpriteGeometry.CellsInView(in inverse, Vector3.Zero, Vector3.UnitX, Vector3.UnitY, out x0, out y0, out x1, out y1));
            Assert.Equal((98, -52, 101, -49), (x0, y0, x1, y1));
        }

        [Fact]
        public void A_view_along_the_maps_plane_or_past_it_gives_no_range()
        {
            Matrix4x4 inverse = InverseOrtho(new Vector3(0f, 0f, 10f), 5f, 1f);
            // A map standing edge-on to the camera (its plane holds the line of sight).
            Assert.False(GameEditor.Framework.Renderer.Server.Sprites2D.SpriteGeometry.CellsInView(in inverse, Vector3.Zero, Vector3.UnitX, Vector3.UnitZ, out _, out _, out _, out _));
            // A map beyond the far plane.
            Assert.False(GameEditor.Framework.Renderer.Server.Sprites2D.SpriteGeometry.CellsInView(in inverse, new Vector3(0f, 0f, -500f), Vector3.UnitX, Vector3.UnitY, out _, out _, out _, out _));
            // Cells of no size.
            Assert.False(GameEditor.Framework.Renderer.Server.Sprites2D.SpriteGeometry.CellsInView(in inverse, Vector3.Zero, Vector3.Zero, Vector3.UnitY, out _, out _, out _, out _));
        }

        // ── The Tilemap block ───────────────────────────────────────────────────────────

        [Fact]
        public void The_Tilemap_block_round_trips_and_one_with_only_its_map_has_the_defaults()
        {
            World.Clear();
            var scene = new GameEditor.Framework.Scene.Scene("t");
            Entity e = scene.CreateEntity("Level");
            World.AddComponent(e, new TilemapRenderer { Map = Level, Layer = 2, Order = -3, Color = new Vector4(1f, 0.5f, 0.25f, 1f), Visible = false });
            string json = SceneSerializer.Serialize(scene);
            Assert.Contains("\"Tilemap\":{\"Map\":\"Tilemaps/level1.tilemap\",\"Layer\":2,\"Order\":-3,\"Color\":[1,0.5,0.25,1],\"Vis\":false}", json);

            var loaded = new GameEditor.Framework.Scene.Scene("x");
            SceneSerializer.Deserialize(json, loaded);
            Assert.Equal(json, SceneSerializer.Serialize(loaded));

            SceneSerializer.Deserialize("""{"name":"s","entities":[{"id":1,"name":"A","c":{"Tilemap":{"Map":"m.tilemap"}}}]}""", loaded);
            foreach (Entity only in World.Entities)
            {
                TilemapRenderer t = World.GetComponent<TilemapRenderer>(only);
                Assert.Equal(("m.tilemap", 0, 0, Vector4.One, true), (t.Map, t.Layer, t.Order, t.Color, t.Visible));
            }
        }
    }
}
