/*
* Copyright (c) 2012-2020 AssimpNet - Nicholas Woodfield
* 
* Permission is hereby granted, free of charge, to any person obtaining a copy
* of this software and associated documentation files (the "Software"), to deal
* in the Software without restriction, including without limitation the rights
* to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
* copies of the Software, and to permit persons to whom the Software is
* furnished to do so, subject to the following conditions:
* 
* The above copyright notice and this permission notice shall be included in
* all copies or substantial portions of the Software.
* 
* THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
* IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
* FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
* AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
* LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
* OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
* THE SOFTWARE.
*/

using System;
using System.IO;
using System.Runtime.InteropServices;
using TeximpNet;
using Veldrid;
using SN = System.Numerics;

namespace Assimp.Sample
{
    //Packed color 
    [StructLayout(LayoutKind.Sequential, Size = 4)]
    public struct Color
    {
        public static int SizeInBytes => MemoryHelper.SizeOf<Color>();

        public byte R;
        public byte G;
        public byte B;
        public byte A;

        public static Color White => new Color() { R = 255, G = 255, B = 255, A = 255 };
    }

    public static class Helper
    {
        public static byte[] ReadEmbeddedAssetBytes(string name)
        {
            using(Stream stream = typeof(Helper).Assembly.GetManifestResourceStream(name))
            {
                byte[] bytes = new byte[stream.Length];
                using(MemoryStream ms = new MemoryStream(bytes))
                {
                    stream.CopyTo(ms);
                    return bytes;
                }
            }
        }

        public static Shader LoadShader(ResourceFactory factory, string set, ShaderStages stage, string entryPoint)
        {
            string name = $"{set}-{stage.ToString().ToLower()}.{GetExtension(factory.BackendType)}";
            return factory.CreateShader(new ShaderDescription(stage, ReadEmbeddedAssetBytes(name), entryPoint));
        }

        private static string GetExtension(GraphicsBackend backendType)
        {
            bool isMacOS = RuntimeInformation.OSDescription.Contains("Darwin");

            return (backendType == GraphicsBackend.Direct3D11)
                ? "hlsl.bytes"
                : (backendType == GraphicsBackend.Vulkan)
                    ? "450.glsl"
                    : (backendType == GraphicsBackend.Metal)
                        ? isMacOS ? "metallib" : "ios.metallib"
                        : (backendType == GraphicsBackend.OpenGL)
                            ? "330.glsl"
                            : "300.glsles";
        }

        public static void ToNumerics(in SN.Matrix4x4 matIn, out SN.Matrix4x4 matOut)
        {
            //Assimp matrices are column vector, so X,Y,Z axes are columns 1-3 and 4th column is translation.
            //Columns => Rows to make it compatible with numerics
            matOut = new System.Numerics.Matrix4x4(matIn.M11, matIn.M21, matIn.M31, matIn.M41, //X
                                                   matIn.M12, matIn.M22, matIn.M32, matIn.M42, //Y
                                                   matIn.M13, matIn.M23, matIn.M33, matIn.M43, //Z
                                                   matIn.M14, matIn.M24, matIn.M34, matIn.M44); //Translation
        }

        public static void FromNumerics(in SN.Matrix4x4 matIn, out SN.Matrix4x4 matOut)
        {
            //Numerics matrix are row vector, so X,Y,Z axes are rows 1-3 and 4th row is translation.
            //Rows => Columns to make it compatible with assimp

            //X
            matOut.M11 = matIn.M11;
            matOut.M21 = matIn.M12;
            matOut.M31 = matIn.M13;
            matOut.M41 = matIn.M14;

            //Y
            matOut.M12 = matIn.M21;
            matOut.M22 = matIn.M22;
            matOut.M32 = matIn.M23;
            matOut.M42 = matIn.M24;

            //Z
            matOut.M13 = matIn.M31;
            matOut.M23 = matIn.M32;
            matOut.M33 = matIn.M33;
            matOut.M43 = matIn.M34;

            //Translation
            matOut.M14 = matIn.M41;
            matOut.M24 = matIn.M42;
            matOut.M34 = matIn.M43;
            matOut.M44 = matIn.M44;
        }

        public static void FromNumerics(in SN.Vector3 vIn, out SN.Vector3 vOut)
        {
            vOut.X = vIn.X;
            vOut.Y = vIn.Y;
            vOut.Z = vIn.Z;
        }

        public static void ToNumerics(in SN.Vector3 vIn, out SN.Vector3 vOut)
        {
            vOut.X = vIn.X;
            vOut.Y = vIn.Y;
            vOut.Z = vIn.Z;
        }

        public static Texture LoadTextureFromFile(string filePath, GraphicsDevice gd, ResourceFactory factory)
        {
            if(!File.Exists(filePath) || gd == null || factory == null)
                return null;

            try
            {
                //FreeImage can load DDS, but they return an uncompressed image, and only the first mip level, which is good enough for our purpose here.
                //See DDSContainer/DDSFile to load from DDS format all the different types of textures!
                using(Surface image = Surface.LoadFromFile(filePath))
                {
                    image.FlipVertically();

                    if(image.ImageType != ImageType.Bitmap || image.BitsPerPixel != 32)
                        image.ConvertTo(ImageConversion.To32Bits);

                    return CreateTextureFromSurface(image, gd, factory);
                }
            }
            catch(Exception) { }

            return null;
        }

        private static Texture CreateTextureFromSurface(Surface image, GraphicsDevice gd, ResourceFactory factory)
        {
            if(image.ImageType != ImageType.Bitmap || image.BitsPerPixel != 32 || gd == null || factory == null)
                return null;

            uint width = (uint) image.Width;
            uint height = (uint) image.Height;

            Texture staging = factory.CreateTexture(
                          TextureDescription.Texture2D(width, height, 1, 1, PixelFormat.R8_G8_B8_A8_UNorm, TextureUsage.Staging));

            Texture texture = factory.CreateTexture(
                TextureDescription.Texture2D(width, height, 1, 1, PixelFormat.R8_G8_B8_A8_UNorm, TextureUsage.Sampled));

            CommandList cl = gd.ResourceFactory.CreateCommandList();
            cl.Begin();

            MappedResource map = gd.Map(staging, MapMode.Write, 0);
            CopyColor(map.Data, image);
            gd.Unmap(staging, 0);

            cl.CopyTexture(staging, 0, 0, 0, 0, 0, texture, 0, 0, 0, 0, 0, width, height, 1, 1);
            cl.End();

            gd.SubmitCommands(cl);
            gd.DisposeWhenIdle(staging);
            gd.DisposeWhenIdle(cl);

            return texture;
        }

        //Shamelessly stolen from Tesla Graphics Engine. Copy from either BGRA or RGBA order to RGBA order
        private static unsafe void CopyColor(IntPtr dstPtr, Surface src)
        {
            int texelSize = Color.SizeInBytes;

            int width = src.Width;
            int height = src.Height;
            int dstPitch = width * texelSize;
            bool swizzle = Surface.IsBGRAOrder;

            int pitch = Math.Min(src.Pitch, dstPitch);

            if(swizzle)
            {
                //For each scanline...
                for(int row = 0; row < height; row++)
                {
                    Color* dPtr = (Color*) dstPtr.ToPointer();
                    Color* sPtr = (Color*) src.GetScanLine(row).ToPointer();

                    //Copy each pixel, swizzle components...
                    for(int count = 0; count < pitch; count += texelSize)
                    {
                        Color v = *(sPtr++);
                        byte temp = v.R;
                        v.R = v.B;
                        v.B = temp;

                        *(dPtr++) = v;
                    }

                    //Advance to next scanline...
                    dstPtr += dstPitch;
                }
            }
            else
            {
                //For each scanline...
                for(int row = 0; row < height; row++)
                {
                    IntPtr sPtr = src.GetScanLine(row);

                    //Copy entirely...
                    MemoryHelper.CopyMemory(dstPtr, sPtr, pitch);

                    //Advance to next scanline...
                    dstPtr += dstPitch;
                }
            }
        }
    }
}
