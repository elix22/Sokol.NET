using System;
using System.Numerics;
using GameEditor.Framework.Assets;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Renderer.Server.Sprites2D;
using static Sokol.SG;

namespace GameEditor.Framework.Scene
{
    // Drawing the particles (examples/GameEditor/docs/PARTICLES_3D_DESIGN.md §3, P2): the Renderer module - Billboard, Stretched,
    // Horizontal, Vertical; Alpha or Additive; the emitter's sort - with the Texture Sheet's frame, the size and colour over the
    // life, into the particles' own batch, right after the sprites.
    public static partial class WorldParticles
    {
        private static readonly ParticleBatch _batch = new();
        private static Pool[] _sorted = new Pool[16];
        private static sg_image _dotImage;
        private static sg_view _dot;

        /// <summary>Quads and draw calls of the last view's particles.</summary>
        public static int LastQuads, LastDrawCalls;

        internal static void BeginFrame() => _batch.BeginFrame();

        /// <summary>The particles into the open pass, after the sprites, for the camera of <paramref name="viewProj"/>.</summary>
        internal static void Draw(in Matrix4x4 viewProj, bool offscreen)
        {
            LastQuads = 0; LastDrawCalls = 0;
            if (_pools.Count == 0) return;
            // The camera's right and up in the world: the first two columns of the view-projection (no skew in either kind of
            // camera); its forward, across them.
            Vector3 right = new(viewProj.M11, viewProj.M21, viewProj.M31), up = new(viewProj.M12, viewProj.M22, viewProj.M32);
            if (right.LengthSquared() < 1e-12f || up.LengthSquared() < 1e-12f) return;
            right = Vector3.Normalize(right); up = Vector3.Normalize(up);
            Vector3 forward = Vector3.Normalize(Vector3.Cross(up, right));

            // The emitters back to front (by the depth of their position).
            int n = 0;
            foreach (Pool pool in _pools.Values)
            {
                if (pool.Count == 0 && pool.LingerCount == 0) continue;
                pool.Depth = Depth(pool.Origin, in viewProj);
                if (n == _sorted.Length) Array.Resize(ref _sorted, n * 2);
                _sorted[n++] = pool;
            }
            for (int i = 1; i < n; i++)
            {
                Pool x = _sorted[i];
                int j = i - 1;
                while (j >= 0 && _sorted[j].Depth < x.Depth) { _sorted[j + 1] = _sorted[j]; j--; }
                _sorted[j + 1] = x;
            }

            _batch.Begin();
            for (int k = 0; k < n; k++) DrawPool(_sorted[k], in viewProj, right, up, forward);
            Array.Clear(_sorted, 0, n);
            _batch.Flush(in viewProj, offscreen);
            LastQuads = _batch.QuadCount;
            LastDrawCalls = _batch.DrawCalls;
        }

        private static float Depth(Vector3 at, in Matrix4x4 viewProj)
        {
            Vector4 clip = Vector4.Transform(new Vector4(at, 1f), viewProj);
            return clip.W != 0f ? clip.Z / clip.W : clip.Z;
        }

        private static void DrawPool(Pool pool, in Matrix4x4 viewProj, Vector3 right, Vector3 up, Vector3 forward)
        {
            ref readonly ParticlesComponent c = ref pool.C;
            if (c.TrailsOn && pool.TrailCount.Length > 0) DrawTrails(pool, in c, forward);
            if (pool.Count == 0) return;
            sg_view view;
            bool nearest = false;
            Vector4 frameUv = new(0f, 0f, 1f, 1f);       // u0, vTop, u1, vBottom of the texture's frame
            if (string.IsNullOrEmpty(c.Texture)) view = Dot();
            else
            {
                if (SpriteLibrary.Find(c.Texture, out SpriteFrame f) != SpriteLibrary.Answer.Ready) return;
                view = f.View; nearest = f.Nearest;
                frameUv = f.Uv;
            }
            int cols = c.SheetOn ? Math.Max(1, c.SheetColumns) : 1, rows = c.SheetOn ? Math.Max(1, c.SheetRows) : 1;
            int frames = cols * rows;

            // The order: as made, or sorted by a key (the farthest first, or by age).
            int count = pool.Count;
            bool sorted = c.Sort != ParticleSort.None && count > 1;
            if (sorted)
            {
                for (int i = 0; i < count; i++)
                {
                    ref readonly Particle q = ref pool.Items[i];
                    pool.SortKeys[i] = c.Sort switch
                    {
                        ParticleSort.ByDistance => -Depth(WorldPos(pool, in q), in viewProj),
                        ParticleSort.OldestInFront => -q.Age,       // the oldest drawn last, over the others
                        _ => q.Age,
                    };
                    pool.SortOrder[i] = i;
                }
                Array.Sort(pool.SortKeys, pool.SortOrder, 0, count);
            }

            // Vertical: upright, facing the camera about the world's up.
            Vector3 flatRight = new(right.X, 0f, right.Z);
            flatRight = flatRight.LengthSquared() > 1e-8f ? Vector3.Normalize(flatRight) : Vector3.UnitX;
            const float Deg = MathF.PI / 180f;

            for (int s = 0; s < count; s++)
            {
                ref readonly Particle p = ref pool.Items[sorted ? pool.SortOrder[s] : s];
                float t = p.Life > 0f ? p.Age / p.Life : 1f;
                float size = SizeNow(in c, in p);
                if (size <= 0f) continue;
                Vector4 col = ColorNow(in c, in p);
                if (col.W <= 0f) continue;
                Vector3 centre = WorldPos(pool, in p);

                Vector3 ax, ay;
                float rot = p.Rot * Deg;
                switch (c.RenderMode)
                {
                    case ParticleRenderMode.Stretched:
                    {
                        // Along its speed, as long as LengthScale sizes plus SpeedScale times the speed, as wide as its size.
                        Vector3 vel = c.World ? p.Vel : Vector3.Transform(p.Vel, pool.Rotation);
                        float speed = vel.Length();
                        Vector3 along = speed > 1e-5f ? vel / speed : up;
                        Vector3 across = Vector3.Cross(along, forward);
                        across = across.LengthSquared() > 1e-10f ? Vector3.Normalize(across) : right;
                        ay = along * (size * MathF.Max(0f, c.LengthScale) + speed * MathF.Max(0f, c.SpeedScale));
                        ax = across * size;
                        break;
                    }
                    case ParticleRenderMode.Horizontal:
                    {
                        float cs = MathF.Cos(rot), sn = MathF.Sin(rot);
                        ax = new Vector3(cs, 0f, -sn) * size;
                        ay = new Vector3(sn, 0f, cs) * size;
                        break;
                    }
                    case ParticleRenderMode.Vertical:
                    {
                        float cs = MathF.Cos(rot), sn = MathF.Sin(rot);
                        ax = (flatRight * cs + Vector3.UnitY * sn) * size;
                        ay = (Vector3.UnitY * cs - flatRight * sn) * size;
                        break;
                    }
                    default:
                    {
                        if (rot == 0f) { ax = right * size; ay = up * size; }
                        else
                        {
                            float cs = MathF.Cos(rot), sn = MathF.Sin(rot);
                            ax = (right * cs + up * sn) * size;
                            ay = (up * cs - right * sn) * size;
                        }
                        break;
                    }
                }

                // The Texture Sheet's frame: a cell of the texture's frame, the first row at its top.
                Vector4 uv;
                if (frames > 1)
                {
                    float f = c.SheetTime == ParticleSheetTime.Fps ? p.Frame + p.Age * MathF.Max(0f, c.SheetFps)
                                                                   : p.Frame + MathF.Min(t, 0.99999f) * frames * MathF.Max(0f, c.SheetCycles);
                    int frame = (int)MathF.Floor(f) % frames;
                    if (frame < 0) frame += frames;
                    int col_ = frame % cols, row = frame / cols;
                    float du = (frameUv.Z - frameUv.X) / cols, dv = (frameUv.W - frameUv.Y) / rows;
                    float u0 = frameUv.X + du * col_, vTop = frameUv.Y + dv * row;
                    uv = new Vector4(u0, vTop + dv, u0 + du, vTop);
                }
                else uv = new Vector4(frameUv.X, frameUv.W, frameUv.Z, frameUv.Y);

                _batch.Draw(view, nearest, c.Blend, centre - (ax + ay) * 0.5f, ax, ay, uv, Pack(col));
            }
        }

        // The emitter's trails before its particles (G8.8 Q3): a quad a segment facing the camera, from the head (the particle now)
        // through its points to the oldest; its width the curve's along the trail times the particle's size, its colour the
        // gradients' (and the particle's).
        private static void DrawTrails(Pool pool, in ParticlesComponent c, Vector3 forward)
        {
            sg_view view = default;
            if (!string.IsNullOrEmpty(c.TrailTexture))
            {
                if (SpriteLibrary.Find(c.TrailTexture, out SpriteFrame f) != SpriteLibrary.Answer.Ready) return;
                view = f.View;
            }
            for (int i = 0; i < pool.Count; i++)
            {
                ref readonly Particle q = ref pool.Items[i];
                if (q.Trail == 0) continue;
                DrawTrail(pool, in c, q.Trail - 1, true, WorldPos(pool, in q), SizeNow(in c, in q), ColorNow(in c, in q),
                          MathF.Max(1e-4f, c.TrailLifetime * q.Life), view, forward);
            }
            for (int k = 0; k < pool.LingerCount; k++)
            {
                int slot = pool.Lingering[k];
                DrawTrail(pool, in c, slot, false, default, pool.SlotSize[slot], pool.SlotColor[slot], MathF.Max(1e-4f, c.TrailLifetime * pool.SlotLife[slot]), view, forward);
            }
        }

        private static void DrawTrail(Pool pool, in ParticlesComponent c, int slot, bool hasHead, Vector3 head, float size, Vector4 color, float maxAge,
                                      sg_view view, Vector3 forward)
        {
            int b = slot * TrailPoints, n = pool.TrailCount[slot];
            Span<Vector3> pts = stackalloc Vector3[TrailPoints + 1];
            Span<float> ages = stackalloc float[TrailPoints + 1];
            int m = 0;
            if (hasHead) { pts[0] = head; ages[0] = 0f; m = 1; }
            for (int k = n - 1; k >= 0; k--)
            {
                Vector3 v = pool.TrailPos[b + k];
                pts[m] = c.TrailWorldSpace ? v : pool.Origin + Vector3.Transform(v, pool.Rotation);
                ages[m] = pool.TrailAge[b + k];
                m++;
            }
            if (m < 2) return;
            float total = 0f;
            for (int i = 1; i < m; i++) total += Vector3.Distance(pts[i - 1], pts[i]);
            if (total < 1e-5f) return;
            Vector4 tint = c.TrailInheritColor ? color : Vector4.One;
            float done = 0f;
            for (int i = 1; i < m; i++)
            {
                Vector3 a = pts[i - 1], seg = pts[i] - a;
                float len = seg.Length();
                if (len < 1e-6f) continue;
                float fa = done / total, fb = (done + len) / total;
                done += len;
                float w = size * 0.5f * (c.TrailWidth.Evaluate(fa) + c.TrailWidth.Evaluate(fb));
                if (w <= 0f) continue;
                Vector3 across = Vector3.Cross(seg, forward);
                if (across.LengthSquared() < 1e-12f) continue;
                across = Vector3.Normalize(across) * w;
                float age = Math.Clamp(0.5f * (ages[i - 1] + ages[i]) / maxAge, 0f, 1f);
                Vector4 col = tint * c.TrailColorOverTrail.Evaluate(0.5f * (fa + fb)) * c.TrailColorOverLifetime.Evaluate(age);
                if (col.W <= 0f) continue;
                float va = fa, vb = fb;
                if (c.TrailTextureMode == ParticleTrailTexture.Tile)
                {
                    // Repeated every width: the segment from its start's place in a repeat (the shader keeps uv inside a quad's
                    // rectangle, the sampler clamps: a segment shows at most one repeat).
                    float start = (done - len) / w;
                    va = start - MathF.Floor(start);
                    vb = MathF.Min(1f, va + len / w);
                }
                _batch.Draw(view, false, c.Blend, a - across * 0.5f, across, seg, new Vector4(0f, va, 1f, vb), Pack(col));
            }
        }

        private static uint Pack(Vector4 c)
        {
            static uint B(float v) => (uint)(Math.Clamp(v, 0f, 1f) * 255f + 0.5f);
            return B(c.X) | (B(c.Y) << 8) | (B(c.Z) << 16) | (B(c.W) << 24);
        }

        // A soft round dot, white, 32 x 32: the texture of an emitter that names none. Made once, at the first draw.
        private static unsafe sg_view Dot()
        {
            if (_dot.id != 0) return _dot;
            const int N = 32;
            byte* px = stackalloc byte[N * N * 4];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                    float a = Math.Clamp(1f - MathF.Sqrt(dx * dx + dy * dy), 0f, 1f);
                    a = a * a * (3f - 2f * a);                                  // smooth to the edge
                    int i = (y * N + x) * 4;
                    px[i] = 255; px[i + 1] = 255; px[i + 2] = 255; px[i + 3] = (byte)(a * 255f + 0.5f);
                }
            var desc = new sg_image_desc { width = N, height = N, pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA8, label = "particles-dot" };
            desc.data.mip_levels[0] = new sg_range { ptr = px, size = N * N * 4 };
            _dotImage = sg_make_image(desc);
            _dot = sg_make_view(new sg_view_desc { texture = new sg_texture_view_desc { image = _dotImage }, label = "particles-dot" });
            return _dot;
        }

        /// <summary>The batch and the dot's texture given back (the renderer shuts down).</summary>
        internal static void Shutdown()
        {
            _batch.Dispose();
            if (_dot.id != 0) sg_destroy_view(_dot);
            if (_dotImage.id != 0) sg_destroy_image(_dotImage);
            _dot = default; _dotImage = default;
            _pools.Clear();
        }
    }
}
