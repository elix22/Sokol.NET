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

using ShaderGen;
using SMath = ShaderGen.ShaderBuiltins;
using SN = System.Numerics;

[assembly: ShaderSet("SimpleTextured", "Assimp.Sample.SimpleTextured.VS", "Assimp.Sample.SimpleTextured.FS")]

namespace Assimp.Sample
{
    //Simple texture shader that has a single non-attenuating point light.
    public class SimpleTextured
    {
        [ResourceSet(0)]
        public SN.Matrix4x4 WorldViewProjection;

        [ResourceSet(0)]
        public SN.Matrix4x4 World;

        [ResourceSet(0)]
        public SN.Vector3 LightPosition;

        [ResourceSet(0)]
        public SN.Vector3 CameraPosition;

        [ResourceSet(1)]
        public SN.Vector4 DiffuseColor;

        [ResourceSet(1)]
        public Texture2DResource DiffuseTexture;

        [ResourceSet(1)]
        public SamplerResource DiffuseSampler;

        [VertexShader]
        public FragmentInput VS(VertexInput input)
        {
            SN.Vector4 worldPos = SMath.Mul(World, new SN.Vector4(input.Position, 1));
            SN.Vector4 worldNorm = SMath.Mul(World, new SN.Vector4(input.Normal, 0));

            FragmentInput output;
            output.SystemPosition = SMath.Mul(WorldViewProjection, new SN.Vector4(input.Position, 1));
            output.PositionWS = new SN.Vector3(worldPos.X, worldPos.Y, worldPos.Z);
            output.NormalWS = new SN.Vector3(worldNorm.X, worldNorm.Y, worldNorm.Z);
            output.TexCoords = input.TexCoords;

            return output;
        }

        [FragmentShader]
        public SN.Vector4 FS(FragmentInput input)
        {
            SN.Vector3 normalWS = SN.Vector3.Normalize(input.NormalWS);

            if(!SMath.IsFrontFace)
                normalWS = SN.Vector3.Negate(normalWS);

            SN.Vector3 v = SN.Vector3.Normalize(CameraPosition - input.PositionWS);
            SN.Vector3 L = SN.Vector3.Normalize(LightPosition - input.PositionWS);
            float nDotL = SMath.Saturate(SN.Vector3.Dot(normalWS, L));

            return SMath.Sample(DiffuseTexture, DiffuseSampler, input.TexCoords) * DiffuseColor * nDotL;
        }

        public struct VertexInput
        {
            [PositionSemantic]
            public SN.Vector3 Position;

            [NormalSemantic]
            public SN.Vector3 Normal;

            [TextureCoordinateSemantic]
            public SN.Vector2 TexCoords;
        }

        public struct FragmentInput
        {
            [SystemPositionSemantic]
            public SN.Vector4 SystemPosition;

            [PositionSemantic]
            public SN.Vector3 PositionWS;

            [NormalSemantic]
            public SN.Vector3 NormalWS;

            [TextureCoordinateSemantic]
            public SN.Vector2 TexCoords;
        }
    }
}