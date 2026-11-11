using System;
using System.Collections.Generic;
using System.Numerics;
using GameEditor.Framework.ECS.Components;
using Sokol;

namespace GameEditor.Framework.Assets
{
    /// <summary>What a sprite draws: a part of a texture, and how large it is in the world.</summary>
    public struct SpriteFrame
    {
        public SG.sg_view View;
        /// <summary>Sample the texture without smoothing (pixel art).</summary>
        public bool Nearest;
        /// <summary>The part of the texture, in uv: X, Y = its top-left corner, Z, W = its bottom-right corner.</summary>
        public Vector4 Uv;
        /// <summary>Size in world units at scale 1.</summary>
        public Vector2 Size;
        /// <summary>The point of the frame that sits on the entity's position: (0, 0) bottom-left, (1, 1) top-right.</summary>
        public Vector2 Pivot;
    }

    /// <summary>
    /// Turns what a <see cref="SpriteRenderer"/> names — an image's key today — into the frame to draw. Answers
    /// are kept, so asking every frame for every sprite costs a lookup. Main thread only.
    /// </summary>
    public static class SpriteLibrary
    {
        public enum Answer { Ready, Loading, Missing }

        private static readonly Dictionary<string, SpriteFrame> _frames = new(StringComparer.Ordinal);
        private static readonly HashSet<string> _missing = new(StringComparer.Ordinal);

        /// <summary>The frame <paramref name="texture"/> names. Loading: the image is on its way, ask again.</summary>
        public static Answer Find(string? texture, out SpriteFrame frame)
        {
            frame = default;
            if (string.IsNullOrEmpty(texture)) return Answer.Missing;
            if (_frames.TryGetValue(texture, out frame)) return Answer.Ready;
            if (_missing.Contains(texture)) return Answer.Missing;

            // A frame of a sprite sheet ("<sheet>#<frame>") is not read yet; a whole image is.
            if (texture.IndexOf('#') < 0 && AssetLibrary.Texture(texture) is { } tex && tex.Width > 0 && tex.Height > 0)
            {
                frame = new SpriteFrame
                {
                    View   = tex.View,
                    Uv     = new Vector4(0f, 0f, 1f, 1f),
                    Size   = new Vector2(tex.Width, tex.Height) / SpriteRenderer.DefaultPixelsPerUnit,
                    Pivot  = new Vector2(0.5f, 0.5f),
                };
                _frames[texture] = frame;
                return Answer.Ready;
            }
            if (texture.IndexOf('#') < 0 && !AssetLibrary.IsTextureMissing(texture)) return Answer.Loading;
            _missing.Add(texture);
            return Answer.Missing;
        }

        /// <summary>Forgets every answer and the image under <paramref name="key"/>: after its file changed, moved or came back.</summary>
        public static void Forget(string key)
        {
            _frames.Clear();
            _missing.Clear();
            AssetLibrary.ForgetTexture(key);
        }

        /// <summary>Forgets every answer (the images stay loaded): when another project or scene is opened.</summary>
        public static void Clear()
        {
            _frames.Clear();
            _missing.Clear();
        }
    }
}
