using System;
using System.Numerics;
using Frent;
using GameEditor.Framework.Assets;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Scene;
using Sokol.Render2D;

namespace GameEditor.Framework.Renderer.Server.Sprites2D
{
    /// <summary>
    /// Draws the world's sprites and tile maps for one view: gathers the visible <see cref="SpriteRenderer"/>s and
    /// the layers of the visible <see cref="TilemapRenderer"/>s, sorts them back to front — by layer, then order,
    /// then distance from the camera — and hands them to the <see cref="SpriteBatch"/>, inside the pass that is
    /// open. A tile layer is drawn whole in its turn: the cells the camera sees, each a quad of the batch. Called
    /// by <see cref="RenderingServer.SubmitView"/> after the meshes. Steady state allocates nothing.
    /// </summary>
    internal static class SpriteRenderSystem
    {
        // Shown in place of a sprite whose image cannot be read, and of a tile number its sheet does not have.
        private const uint Magenta = 0xFFFF00FF;

        struct Item
        {
            public Sokol.SG.sg_view View;
            public bool Nearest;
            public Vector3 Origin, AxisX, AxisY;
            public Vector4 Uv;
            public uint Rgba;
            public int TileLayer;       // -1: a sprite; else the index of a layer in _tileLayers
        }

        // One layer of one tile map entity, for this view.
        struct LayerItem
        {
            public TileLayer Layer;
            public SpriteFrame[] Tiles;
            public Vector3 Origin, AxisX, AxisY;   // the corner of cell (0, 0), and a cell's two edges, in the world
            public uint Rgba;
        }

        private static readonly SpriteBatch _batch = new();
        private static Item[]  _items = new Item[1024];
        private static ulong[] _keys  = new ulong[1024];
        private static int[]   _order = new int[1024];
        private static LayerItem[] _tileLayers = new LayerItem[16];

        /// <summary>Quads (sprites and tiles) and draw calls of the last view drawn.</summary>
        public static int LastSprites, LastDrawCalls;

        public static void BeginFrame() => _batch.BeginFrame();

        public static void Shutdown() { WorldParticles.Shutdown(); _batch.Dispose(); }

        public static void Draw(ECSWorld world, in Matrix4x4 viewProj, bool offscreen)
        {
            LastSprites = 0; LastDrawCalls = 0;
            bool sprites = world.HasAnyComponent<SpriteRenderer>(), maps = world.HasAnyComponent<TilemapRenderer>();
            bool particles = WorldParticles.Alive > 0;
            if (!sprites && !maps && !particles) return;

            int count = 0, layers = 0;
            if (sprites)
                foreach (var row in world.Query<ActiveFlag, SpriteRenderer, Transform>()
                                         .EnumerateWithEntities<ActiveFlag, SpriteRenderer, Transform>())
                {
                    if (!row.Item1.Value.Active) continue;
                    ref readonly var sprite = ref row.Item2.Value;
                    if (!sprite.Visible || sprite.Color.W <= 0f) continue;

                    var answer = SpriteLibrary.Find(sprite.Texture, out SpriteFrame frame);
                    if (answer == SpriteLibrary.Answer.Loading) continue;
                    bool missing = answer == SpriteLibrary.Answer.Missing;
                    if (missing) frame = SpriteGeometry.Missing;

                    Matrix4x4 model = Transform.GetWorldMatrix(world, row.Item3.Value);
                    SpriteGeometry.Quad(in model, frame.Size, frame.Pivot, out Vector3 origin, out Vector3 axisX, out Vector3 axisY);

                    // uv of the quad's bottom-left and top-right corners: an image's first row is its top one.
                    float u0 = frame.Uv.X, u1 = frame.Uv.Z, vTop = frame.Uv.Y, vBottom = frame.Uv.W;
                    if (sprite.FlipX) (u0, u1) = (u1, u0);
                    if (sprite.FlipY) (vTop, vBottom) = (vBottom, vTop);

                    Room(count);
                    _items[count] = new Item
                    {
                        View    = missing ? default : frame.View,
                        Nearest = frame.Nearest,
                        Origin  = origin,
                        AxisX   = axisX,
                        AxisY   = axisY,
                        Uv      = new Vector4(u0, vBottom, u1, vTop),
                        Rgba    = missing ? Magenta : Pack(sprite.Color),
                        TileLayer = -1,
                    };
                    _keys[count]  = SpriteGeometry.DrawKey(sprite.Layer, sprite.Order, model.Translation, in viewProj);
                    _order[count] = count;
                    count++;
                }

            if (maps)
                foreach (var row in world.Query<ActiveFlag, TilemapRenderer, Transform>()
                                         .EnumerateWithEntities<ActiveFlag, TilemapRenderer, Transform>())
                {
                    if (!row.Item1.Value.Active) continue;
                    ref readonly var tilemap = ref row.Item2.Value;
                    if (!tilemap.Visible || tilemap.Color.W <= 0f) continue;
                    if (TilemapLibrary.Find(tilemap.Map, out TilemapData? map) != SpriteLibrary.Answer.Ready) continue;
                    if (SpriteLibrary.FindTiles(map!.Tiles, out SpriteFrame[] tiles) != SpriteLibrary.Answer.Ready) continue;

                    Matrix4x4 model = Transform.GetWorldMatrix(world, row.Item3.Value);
                    Vector3 axisX = Vector3.TransformNormal(new Vector3(map.Cell.X, 0f, 0f), model);
                    Vector3 axisY = Vector3.TransformNormal(new Vector3(0f, map.Cell.Y, 0f), model);
                    uint rgba = Pack(tilemap.Color);
                    for (int i = 0; i < map.Layers.Count; i++)
                    {
                        TileLayer layer = map.Layers[i];
                        if (layer.Width == 0 || layer.Height == 0) continue;
                        if (layers == _tileLayers.Length) Array.Resize(ref _tileLayers, layers * 2);
                        _tileLayers[layers] = new LayerItem { Layer = layer, Tiles = tiles, Origin = model.Translation, AxisX = axisX, AxisY = axisY, Rgba = rgba };
                        Room(count);
                        _items[count] = new Item { TileLayer = layers };
                        _keys[count]  = SpriteGeometry.DrawKey(tilemap.Layer, tilemap.Order + layer.Order, model.Translation, in viewProj);
                        _order[count] = count;
                        layers++;
                        count++;
                    }
                }
            if (count == 0 && !particles) return;

            Array.Sort(_keys, _order, 0, count);

            bool invertible = Matrix4x4.Invert(viewProj, out Matrix4x4 inverse);
            _batch.Begin();
            for (int i = 0; i < count; i++)
            {
                ref readonly Item it = ref _items[_order[i]];
                if (it.TileLayer < 0) _batch.Draw(it.View, it.Nearest, it.Origin, it.AxisX, it.AxisY, it.Uv, it.Rgba);
                else DrawLayer(in _tileLayers[it.TileLayer], in inverse, invertible);
            }
            // The particles after the sprites: quads facing the camera, a texture's in one call (docs/H5_DESIGN.md §16).
            if (particles) WorldParticles.Draw(_batch, in viewProj);
            _batch.Flush(in viewProj, offscreen);
            LastSprites = _batch.SpriteCount;
            LastDrawCalls = _batch.DrawCalls;
            Array.Clear(_tileLayers, 0, layers);     // the maps are not held on to between frames
        }

        private static void Room(int count)
        {
            if (count < _items.Length) return;
            Array.Resize(ref _items, count * 2);
            Array.Resize(ref _keys, count * 2);
            Array.Resize(ref _order, count * 2);
        }

        // The cells of a layer that the view shows, each a quad as large as its cell.
        private static void DrawLayer(in LayerItem item, in Matrix4x4 inverseViewProj, bool invertible)
        {
            TileLayer layer = item.Layer;
            int x0 = layer.OriginX, y0 = layer.OriginY, x1 = x0 + layer.Width - 1, y1 = y0 + layer.Height - 1;
            if (invertible && SpriteGeometry.CellsInView(in inverseViewProj, item.Origin, item.AxisX, item.AxisY, out int vx0, out int vy0, out int vx1, out int vy1))
            {
                x0 = Math.Max(x0, vx0); y0 = Math.Max(y0, vy0);
                x1 = Math.Min(x1, vx1); y1 = Math.Min(y1, vy1);
            }
            SpriteFrame[] tiles = item.Tiles;
            int[] cells = layer.Cells;
            for (int y = y0; y <= y1; y++)
            {
                int row = (y - layer.OriginY) * layer.Width - layer.OriginX;
                Vector3 rowOrigin = item.Origin + item.AxisY * y;
                for (int x = x0; x <= x1; x++)
                {
                    int tile = cells[row + x];
                    if (tile <= 0) continue;
                    Vector3 corner = rowOrigin + item.AxisX * x;
                    if (tile > tiles.Length) { _batch.Draw(default, false, corner, item.AxisX, item.AxisY, new Vector4(0f, 0f, 1f, 1f), Magenta); continue; }
                    ref readonly SpriteFrame f = ref tiles[tile - 1];
                    _batch.Draw(f.View, f.Nearest, corner, item.AxisX, item.AxisY, new Vector4(f.Uv.X, f.Uv.W, f.Uv.Z, f.Uv.Y), item.Rgba);
                }
            }
        }

        private static uint Pack(Vector4 c)
        {
            static uint B(float v) => (uint)(Math.Clamp(v, 0f, 1f) * 255f + 0.5f);
            return B(c.X) | (B(c.Y) << 8) | (B(c.Z) << 16) | (B(c.W) << 24);
        }
    }
}
