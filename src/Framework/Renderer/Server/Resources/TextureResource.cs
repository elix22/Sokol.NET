using static Sokol.SG;

namespace GameEditor.Framework.Renderer.Server.Resources
{
    /// <summary>
    /// A loaded texture: image + view + sampler triple.
    /// Ref-counted by TextureCache; destroyed when ref-count reaches zero.
    /// </summary>
    public sealed class TextureResource
    {
        public sg_image   Image;
        public sg_view    View;
        public sg_sampler Sampler;

        /// <summary>Reference count maintained by TextureCache.</summary>
        public int RefCount;

        /// <summary>Cache key (usually the file path, or a built-in name like "builtin:white").</summary>
        /// <summary>The image's size in pixels (0 for a texture made before its size was kept).</summary>
        public int Width, Height;

        public string Key = "";

        public bool IsValid => Image.id != 0;

        public void Destroy()
        {
            if (Image.id   != 0) { sg_destroy_image(Image);    Image   = default; }
            if (View.id    != 0) { sg_destroy_view(View);      View    = default; }
            // The sampler is the cache's shared one (TextureCache.DefaultSampler): it is not this texture's to destroy.
            Sampler = default;
        }
    }
}
