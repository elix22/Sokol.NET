// ClusterData.cs — Phase G8.2 (examples/GameEditor/docs/CLUSTERED_LIGHTING_DESIGN.md): Forward+'s data on the GPU.
//
// One RGBA32F texture a view, 64 texels wide: the view's lights first (4 texels a light, laid out as GpuLight), then
// each cluster's light mask (ClusterBuilder: 16 bits a float, 4 a texel). The shader reads it with texelFetch only
// (pbr_fs_uniforms.glsl, CLUSTERED). sokol updates a texture once a frame, so each view of a frame takes its own from a
// pool; a texture not used for a while is destroyed (the last one is kept).

using System;
using System.Collections.Generic;
using System.Numerics;
using static Sokol.SG;

namespace GameEditor.Framework.Renderer.Server.Lighting
{
    public sealed class ClusterData : IDisposable
    {
        public const int Width = 64;
        private const int KeepFrames = 300;

        /// <summary>
        /// The most lights a view holds in Forward+: 256 on desktop (Metal macOS, D3D11, GL), 32 on a phone and in a
        /// browser (GLES3 / WebGL2, Metal iOS) — the reference editor's limits.
        /// </summary>
        public static int MaxLightsFor(sg_backend backend) => backend switch
        {
            sg_backend.SG_BACKEND_METAL_MACOS or sg_backend.SG_BACKEND_D3D11 or sg_backend.SG_BACKEND_GLCORE => 256,
            _ => 32,
        };

        public readonly ClusterBuilder Builder;
        public int MaxLights => Builder.MaxLights;
        public readonly int Height;
        /// <summary>The first texel of the cluster masks (after the lights).</summary>
        public int FirstMaskTexel => MaxLights * 4;
        private readonly int _texelsPerCluster;
        private readonly float[] _texels;

        private sealed class Slot { public sg_image Image; public sg_view View; public int Frame = -1; }
        private readonly List<Slot> _pool = new();

        public ClusterData(int maxLights)
        {
            Builder = new ClusterBuilder(maxLights);
            _texelsPerCluster = (Builder.Words + 3) / 4;
            int texels = FirstMaskTexel + ClusterBuilder.Count * _texelsPerCluster;
            Height = (texels + Width - 1) / Width;
            _texels = new float[Width * Height * 4];
        }

        /// <summary>The shader's <c>cluster_params</c>: near, slice scale, first mask texel, 16-bit words a cluster.</summary>
        public Vector4 Params => new(Builder.Near, Builder.SliceScale, FirstMaskTexel, Builder.Words);

        /// <summary>
        /// Puts the view's lights and the masks <see cref="Builder"/> made for them in a texture not yet updated this
        /// <paramref name="frame"/>, and returns its view.
        /// </summary>
        public unsafe sg_view Upload(ReadOnlySpan<GpuLight> lights, int frame)
        {
            Array.Clear(_texels);
            int n = Math.Min(lights.Length, MaxLights);
            for (int i = 0; i < n; i++)
            {
                int o = i * 16;
                Put(o, lights[i].PositionType);
                Put(o + 4, lights[i].DirectionRange);
                Put(o + 8, lights[i].ColorIntensity);
                Put(o + 12, lights[i].SpotShadow);
            }
            var masks = Builder.Masks;
            int words = Builder.Words, first = FirstMaskTexel * 4;
            for (int c = 0; c < ClusterBuilder.Count; c++)
            {
                int src = c * words, dst = first + c * _texelsPerCluster * 4;
                for (int w = 0; w < words; w++) _texels[dst + w] = masks[src + w];
            }

            var slot = Take(frame);
            fixed (float* p = _texels)
            {
                var data = new sg_image_data();
                data.mip_levels[0].ptr = p;
                data.mip_levels[0].size = (nuint)(_texels.Length * sizeof(float));
                sg_update_image(slot.Image, in data);
            }
            return slot.View;
        }

        private void Put(int o, Vector4 v) { _texels[o] = v.X; _texels[o + 1] = v.Y; _texels[o + 2] = v.Z; _texels[o + 3] = v.W; }

        private Slot Take(int frame)
        {
            Slot? free = null;
            for (int i = _pool.Count - 1; i >= 0; i--)
            {
                var s = _pool[i];
                if (s.Frame == frame) continue;
                if (free == null) free = s;
                else if (frame - s.Frame > KeepFrames) { Destroy(s); _pool.RemoveAt(i); }   // unused for a while
            }
            if (free == null)
            {
                free = new Slot();
                free.Image = sg_make_image(new sg_image_desc
                {
                    width = Width, height = Height,
                    pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA32F,
                    usage = new sg_image_usage { stream_update = true },
                    label = "forward-plus-clusters",
                });
                free.View = sg_make_view(new sg_view_desc { texture = new sg_texture_view_desc { image = free.Image }, label = "forward-plus-clusters-view" });
                _pool.Add(free);
            }
            free.Frame = frame;
            return free;
        }

        private static void Destroy(Slot s)
        {
            if (s.View.id != 0) sg_destroy_view(s.View);
            if (s.Image.id != 0) sg_destroy_image(s.Image);
            s.View = default; s.Image = default;
        }

        /// <summary>Textures in the pool (a check: one a view drawn in Forward+ in a frame).</summary>
        public int Textures => _pool.Count;

        public void Dispose()
        {
            foreach (var s in _pool) Destroy(s);
            _pool.Clear();
        }
    }
}
