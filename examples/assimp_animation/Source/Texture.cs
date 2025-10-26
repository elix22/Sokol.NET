using static Sokol.SG.sg_index_type;
using static Sokol.SG.sg_cull_mode;
using static Sokol.SG.sg_compare_func;
using static Sokol.Utils;
using static Sokol.SLog;
using static Sokol.SDebugUI;

using static Sokol.SFetch;
using static Sokol.SDebugText;
using static Sokol.StbImage;
using static Sokol.SG;
using static Sokol.SG.sg_filter;
using static Sokol.SG.sg_wrap;
using Sokol;
using Assimp;


public unsafe class Texture
{
    public sg_image Image { get; private set; } = default;
    public sg_view View { get; private set; } = default;

    public sg_sampler Sampler { get; private set; } = default;

    public bool IsValid => Image.id != 0;

    public Texture(byte* data, int width, int height , string name ="sokol-texture")
    {
        Image = sg_make_image(new sg_image_desc()
        {
            width = width,
            height = height,
            pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA8,
            data = { mip_levels = { [0] = new sg_range() { ptr = data, size = (uint)(width * height * 4) } } },
            label = name
        });

        View = sg_make_view(new sg_view_desc()
        {
            texture = new sg_texture_view_desc { image = Image },
            label = $"{name}-view",
        });

        Sampler = sg_make_sampler(new sg_sampler_desc()
        {
            min_filter = SG_FILTER_LINEAR,
            mag_filter = SG_FILTER_LINEAR,
            // wrap_u = SG_WRAP_CLAMP_TO_EDGE,
            // wrap_v = SG_WRAP_CLAMP_TO_EDGE,
        });
    }

    public Texture(string filePath)
    {
        filePath = util_get_file_path(filePath);
        FileSystem.Instance.LoadFile(filePath, OnTextureLoaded);
    }

    private void Set(byte* data, int width, int height)
    {
        Image = sg_make_image(new sg_image_desc()
        {
            width = width,
            height = height,
            pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA8,
            data = { mip_levels = { [0] = new sg_range() { ptr = data, size = (uint)(width * height * 4) } } },
            label = "sokol-texture"
        });

        View = sg_make_view(new sg_view_desc()
        {
            texture = new sg_texture_view_desc { image = Image },
            label = "sokol-texture-view",
        });

        Sampler = sg_make_sampler(new sg_sampler_desc()
        {
            min_filter = SG_FILTER_LINEAR,
            mag_filter = SG_FILTER_LINEAR,
            // wrap_u = SG_WRAP_CLAMP_TO_EDGE,
            // wrap_v = SG_WRAP_CLAMP_TO_EDGE,
        });
    }

    unsafe void OnTextureLoaded(string filePath, byte[]? buffer, FileLoadStatus status)
    {
        if (status == FileLoadStatus.Success && buffer != null)
        {
            Console.WriteLine($"Assimp: Texture file '{filePath}' loaded successfully, size: {buffer.Length} bytes");
            // Further processing of the texture data would go here
            int png_width = 0, png_height = 0, channels = 0, desired_channels = 4;
            byte* pixels = stbi_load_flipped_csharp(
                in buffer[0],
                (int)buffer.Length,
                ref png_width,
                ref png_height,
                ref channels,
                desired_channels
            );

            if (pixels == null)
            {
                Console.WriteLine($"Assimp: Failed to decode texture file: {filePath}");
                return;
            }

            Set(pixels, png_width, png_height);

            // Free the native STB image data
            stbi_image_free_csharp(pixels);
        }
        else
        {
            Console.WriteLine($"Assimp: Failed to load texture file: {status}");
        }

    }

    public static unsafe List<Texture> LoadTextures(Scene scene, Assimp.Mesh mesh, string FilePath , TextureType textureTypetype = TextureType.Diffuse)
    {
        var material = scene.Materials[mesh.MaterialIndex];

        List<Texture> textures = new List<Texture>();
        int texture_diffuse_count = material.GetMaterialTextureCount(textureTypetype);
        if (texture_diffuse_count > 0)
        {
            TextureSlot texSlot;
            material.GetMaterialTexture(textureTypetype, 0, out texSlot);
            Console.WriteLine($"Assimp: Mesh uses diffuse texture: {texSlot.FilePath}");

            if (texSlot.FilePath[0] == '*')
            {
                // this is an embedded texture
                texSlot.FilePath = texSlot.FilePath.Substring(1);
                if (!int.TryParse(texSlot.FilePath, out int textureIndex))
                {
                    Console.WriteLine($"Assimp: Failed to parse embedded texture index from '{texSlot.FilePath}'");
                    textureIndex = -1;
                }
                else
                {
                    Console.WriteLine($"Assimp: Found embedded texture index: {textureIndex}");
                    if (textureIndex < scene.TextureCount)
                    {
                        EmbeddedTexture embeddedTexture = scene.Textures[textureIndex];
                        if (embeddedTexture.IsCompressed)
                        {
                            Console.WriteLine($"Assimp: Embedded texture is compressed, size: {embeddedTexture.CompressedData.Length} bytes");
                            int png_width = 0, png_height = 0, channels = 0, desired_channels = 4;

                            byte* pixels = stbi_load_flipped_csharp(
                                embeddedTexture.CompressedData[0],
                                embeddedTexture.CompressedData.Length,
                                ref png_width,
                                ref png_height,
                                ref channels,
                                desired_channels
                            );

                            if (pixels == null)
                            {
                                Console.WriteLine($"Assimp: Failed to decode embedded texture index: {textureIndex}");
                            }
                            else
                            {

                                textures.Add(new Texture(pixels, png_width, png_height));

                                // Free the native STB image data
                                stbi_image_free_csharp(pixels);
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Assimp: Embedded texture is uncompressed, size: {embeddedTexture.NonCompressedData.Length} texels");

                            // Convert Texel[] to byte[] (each Texel has 4 bytes: B, G, R, A)
                            var texelData = embeddedTexture.NonCompressedData;
                            byte[] imageBytes = new byte[texelData.Length * 4];

                            for (int i = 0; i < texelData.Length; i++)
                            {
                                var texel = texelData[i];
                                imageBytes[i * 4 + 0] = texel.R;  // Red
                                imageBytes[i * 4 + 1] = texel.G;  // Green
                                imageBytes[i * 4 + 2] = texel.B;  // Blue
                                imageBytes[i * 4 + 3] = texel.A;  // Alpha
                            }

                            fixed (byte* ptr = imageBytes)
                            {
                                textures.Add(new Texture(ptr, embeddedTexture.Width, embeddedTexture.Height));
                            }
                        }

                    }
                    else
                    {
                        Console.WriteLine($"Embedded texture index out of range: {textureIndex}");

                    }
                }
            }
            else
            {
                string filePath = util_get_file_path(texSlot.FilePath);
                string modelDirectory = Path.GetDirectoryName(FilePath) ?? "";
                string fullTexturePath = Path.Combine(modelDirectory, filePath);
                textures.Add(new Texture(fullTexturePath));
            }

        }

        return textures;
    }

}
