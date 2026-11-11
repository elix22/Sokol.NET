using System;
using GameEditor.Framework.Assets;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;

namespace GameEditor.Framework.Scene
{
    /// <summary>
    /// Plays the <see cref="SpriteAnimator"/>s of the world: sets each one's sprite to the frame of its clip for
    /// the game time passed. Run once a game frame, after the scripts (so a clip a script chose shows its first
    /// frame the same frame), only while the game plays — a paused game stands still, a stepped frame advances by
    /// its step. Allocates nothing once the sheets are read.
    /// </summary>
    public static class SpriteAnimSystem
    {
        // A frame that is due within a ten-thousandth of a frame is shown: steps of 1/60 s summed up do not land
        // a hair before the moment a frame changes on one machine and a hair after it on another.
        private const float Due = 1e-4f;

        public static void Update(ECSWorld world, float deltaTime)
        {
            if (!world.HasAnyComponent<SpriteAnimator>()) return;

            foreach (var row in world.Query<ActiveFlag, SpriteAnimator, SpriteRenderer>()
                                     .EnumerateWithEntities<ActiveFlag, SpriteAnimator, SpriteRenderer>())
            {
                if (!row.Item1.Value.Active) continue;
                ref SpriteAnimator anim = ref row.Item2.Value;
                ref SpriteRenderer sprite = ref row.Item3.Value;
                if (string.IsNullOrEmpty(anim.Clip) || string.IsNullOrEmpty(sprite.Texture)) continue;

                // The sheet is the one the sprite shows: the part of its Tex before the '#'.
                if (anim.Sheet == null || !Shows(sprite.Texture, anim.Sheet))
                {
                    int hash = sprite.Texture.IndexOf('#');
                    ReadOnlySpan<char> file = hash < 0 ? sprite.Texture : sprite.Texture.AsSpan(0, hash);
                    if (!file.EndsWith(SpriteSheet.Extension, StringComparison.OrdinalIgnoreCase)) continue;
                    anim.Sheet = file.ToString();
                    anim.Current = null;
                }

                SpriteClip? clip = SpriteLibrary.Sheet(anim.Sheet)?.Clip(anim.Clip);
                if (clip == null || clip.Keys.Length == 0) continue;
                int count = clip.Keys.Length;
                float fps = MathF.Max(clip.Fps, 0f);
                float length = fps > 0f ? count / fps : 0f;

                bool starts = anim.Current != anim.Clip;
                // A clip that ran to its end and is set playing again starts over.
                if (!starts && anim.Playing && !clip.Loop && length > 0f && anim.Speed >= 0f && anim.Time >= length) starts = true;

                if (starts) { anim.Current = anim.Clip; anim.Time = 0f; }
                else if (!anim.Playing) continue;          // stands still; a script may show a frame of its own
                else anim.Time += deltaTime * anim.Speed;

                int frame = (int)MathF.Floor(anim.Time * fps + Due);
                if (clip.Loop)
                {
                    frame %= count;
                    if (frame < 0) frame += count;
                    if (length > 0f && (anim.Time >= length || anim.Time < 0f))
                        anim.Time -= MathF.Floor(anim.Time / length) * length;
                }
                else if (frame >= count) { frame = count - 1; anim.Time = length; anim.Playing = false; }
                else if (frame < 0)      { frame = 0;         anim.Time = 0f;     anim.Playing = false; }

                anim.Frame = frame;
                string shown = clip.Keys[frame];
                if (!ReferenceEquals(sprite.Texture, shown)) sprite.Texture = shown;
            }
        }

        // tex is "<sheet>" or "<sheet>#<frame>".
        private static bool Shows(string tex, string sheet)
            => tex.StartsWith(sheet, StringComparison.Ordinal) && (tex.Length == sheet.Length || tex[sheet.Length] == '#');
    }
}
