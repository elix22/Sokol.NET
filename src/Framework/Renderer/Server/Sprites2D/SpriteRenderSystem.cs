using System;
using System.Numerics;
using Frent;
using GameEditor.Framework.Assets;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using Sokol.Render2D;

namespace GameEditor.Framework.Renderer.Server.Sprites2D
{
    /// <summary>
    /// Draws the world's sprites for one view: gathers the visible <see cref="SpriteRenderer"/>s, sorts them back
    /// to front — by layer, then order, then distance from the camera — and hands them to the
    /// <see cref="SpriteBatch"/>, inside the pass that is open. Called by <see cref="RenderingServer.SubmitView"/>
    /// after the meshes. Steady state allocates nothing.
    /// </summary>
    internal static class SpriteRenderSystem
    {
        // Shown in place of a sprite whose image cannot be read.
        private const uint Magenta = 0xFFFF00FF;

        struct Item
        {
            public Sokol.SG.sg_view View;
            public bool Nearest;
            public Vector3 Origin, AxisX, AxisY;
            public Vector4 Uv;
            public uint Rgba;
        }

        private static readonly SpriteBatch _batch = new();
        private static Item[]  _items = new Item[1024];
        private static ulong[] _keys  = new ulong[1024];
        private static int[]   _order = new int[1024];

        /// <summary>Sprites and draw calls of the last view drawn.</summary>
        public static int LastSprites, LastDrawCalls;

        public static void BeginFrame() => _batch.BeginFrame();

        public static void Shutdown() => _batch.Dispose();

        public static void Draw(ECSWorld world, in Matrix4x4 viewProj, bool offscreen)
        {
            LastSprites = 0; LastDrawCalls = 0;
            if (!world.HasAnyComponent<SpriteRenderer>()) return;

            int count = 0;
            foreach (var row in world.Query<ActiveFlag, SpriteRenderer, Transform>()
                                     .EnumerateWithEntities<ActiveFlag, SpriteRenderer, Transform>())
            {
                if (!row.Item1.Value.Active) continue;
                ref readonly var sprite = ref row.Item2.Value;
                if (!sprite.Visible || sprite.Color.W <= 0f) continue;

                var answer = SpriteLibrary.Find(sprite.Texture, out SpriteFrame frame);
                if (answer == SpriteLibrary.Answer.Loading) continue;
                bool missing = answer == SpriteLibrary.Answer.Missing;
                if (missing) frame = new SpriteFrame { Size = Vector2.One, Pivot = new Vector2(0.5f, 0.5f) };

                Matrix4x4 model = Transform.GetWorldMatrix(world, row.Item3.Value);
                var corner = new Vector3(-frame.Pivot.X * frame.Size.X, -frame.Pivot.Y * frame.Size.Y, 0f);

                // uv of the quad's bottom-left and top-right corners: an image's first row is its top one.
                float u0 = frame.Uv.X, u1 = frame.Uv.Z, vTop = frame.Uv.Y, vBottom = frame.Uv.W;
                if (sprite.FlipX) (u0, u1) = (u1, u0);
                if (sprite.FlipY) (vTop, vBottom) = (vBottom, vTop);

                if (count == _items.Length)
                {
                    Array.Resize(ref _items, count * 2);
                    Array.Resize(ref _keys, count * 2);
                    Array.Resize(ref _order, count * 2);
                }
                _items[count] = new Item
                {
                    View    = missing ? default : frame.View,
                    Nearest = frame.Nearest,
                    Origin  = Vector3.Transform(corner, model),
                    AxisX   = Vector3.TransformNormal(new Vector3(frame.Size.X, 0f, 0f), model),
                    AxisY   = Vector3.TransformNormal(new Vector3(0f, frame.Size.Y, 0f), model),
                    Uv      = new Vector4(u0, vBottom, u1, vTop),
                    Rgba    = missing ? Magenta : Pack(sprite.Color),
                };
                _keys[count]  = SortKey(sprite.Layer, sprite.Order, model.Translation, in viewProj);
                _order[count] = count;
                count++;
            }
            if (count == 0) return;

            Array.Sort(_keys, _order, 0, count);

            _batch.Begin();
            for (int i = 0; i < count; i++)
            {
                ref readonly Item it = ref _items[_order[i]];
                _batch.Draw(it.View, it.Nearest, it.Origin, it.AxisX, it.AxisY, it.Uv, it.Rgba);
            }
            _batch.Flush(in viewProj, offscreen);
            LastSprites = _batch.SpriteCount;
            LastDrawCalls = _batch.DrawCalls;
        }

        // Smaller keys are drawn first: lower layer, then lower order, then farther from the camera.
        private static ulong SortKey(int layer, int order, Vector3 position, in Matrix4x4 viewProj)
        {
            ulong l = (ulong)(Math.Clamp(layer, short.MinValue, short.MaxValue) - short.MinValue);
            ulong o = (ulong)(Math.Clamp(order, short.MinValue, short.MaxValue) - short.MinValue);
            Vector4 clip = Vector4.Transform(new Vector4(position, 1f), viewProj);
            float depth = clip.W != 0f ? clip.Z / clip.W : 0f;              // 0 at the near plane, 1 at the far one
            uint far = uint.MaxValue - (uint)(Math.Clamp(depth, 0f, 1f) * 4294967040f);
            return (l << 48) | (o << 32) | far;
        }

        private static uint Pack(Vector4 c)
        {
            static uint B(float v) => (uint)(Math.Clamp(v, 0f, 1f) * 255f + 0.5f);
            return B(c.X) | (B(c.Y) << 8) | (B(c.Z) << 16) | (B(c.W) << 24);
        }
    }
}
