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
using System.Numerics;
using NUnit.Framework;
using TK = OpenTK;

namespace Assimp.Test
{
    /// <summary>
    /// Helper for Assimp.NET testing.
    /// </summary>
    public static class TestHelper
    {
        public const float DEFAULT_TOLERANCE = 0.000001f;
        public static float Tolerance = DEFAULT_TOLERANCE;

        private static string m_rootPath = null;

        public static string RootPath
        {
            get
            {
                if(m_rootPath == null)
                {
                    /*
                    Assembly entryAssembly = Assembly.GetEntryAssembly();
                    String dirPath = String.Empty;

                    if(entryAssembly == null)
                        entryAssembly = Assembly.GetCallingAssembly();

                    if(entryAssembly != null)
                        dirPath = Path.GetDirectoryName(entryAssembly.Location);

                    m_rootPath = dirPath;*/

                    m_rootPath = AppContext.BaseDirectory;
                }

                return m_rootPath;
            }
        }

        public static void AssertEquals(double expected, double actual)
        {
            Assert.That(actual, Is.EqualTo(expected).Within(Tolerance));
        }

        public static void AssertEquals(double expected, double actual, string msg)
        {
            Assert.That(actual, Is.EqualTo(expected).Within(Tolerance), msg);
        }

        public static void AssertEquals(float expected, float actual)
        {
            Assert.That(actual, Is.EqualTo(expected).Within(Tolerance));
        }

        public static void AssertEquals(float expected, float actual, string msg)
        {
            Assert.That(actual, Is.EqualTo(expected).Within(Tolerance), msg);
        }

        public static void AssertEquals(float x, float y, Vector2 v)
        {
            AssertEquals(x, v.X);
            AssertEquals(y, v.Y);
        }

        public static void AssertEquals(float x, float y, Vector2 v, string msg)
        {
            AssertEquals(x, v.X, msg + $" => checking X component ({x} == {v.X}");
            AssertEquals(y, v.Y, msg + $" => checking Y component ({y} == {v.Y}");
        }

        public static void AssertEquals(float x, float y, float z, Vector3 v)
        {
            AssertEquals(x, v.X);
            AssertEquals(y, v.Y);
            AssertEquals(z, v.Z);
        }

        public static void AssertEquals(float x, float y, float z, Vector3 v, string msg)
        {
            AssertEquals(x, v.X, msg + $" => checking X component ({x} == {v.X}");
            AssertEquals(y, v.Y, msg + $" => checking Y component ({y} == {v.Y}");
            AssertEquals(z, v.Z, msg + $" => checking Z component ({z} == {v.Z}");
        }

        public static void AssertEquals(float r, float g, float b, float a, Vector4 c)
        {
            AssertEquals(r, c.X);
            AssertEquals(g, c.Y);
            AssertEquals(b, c.Z);
            AssertEquals(a, c.W);
        }

        public static void AssertEquals(float r, float g, float b, float a, Vector4 c, string msg)
        {
            AssertEquals(r, c.X, msg + $" => checking R component ({r} == {c.X}");
            AssertEquals(g, c.Y, msg + $" => checking G component ({g} == {c.Y}");
            AssertEquals(b, c.Z, msg + $" => checking B component ({b} == {c.Z}");
            AssertEquals(a, c.W, msg + $" => checking A component ({a} == {c.W}");
        }

        public static void AssertEquals(float x, float y, float z, float w, Quaternion q, string msg)
        {
            AssertEquals(x, q.X, msg + $" => checking X component ({x} == {q.X}");
            AssertEquals(y, q.Y, msg + $" => checking Y component ({y} == {q.Y}");
            AssertEquals(z, q.Z, msg + $" => checking Z component ({z} == {q.Z}");
            AssertEquals(w, q.W, msg + $" => checking W component ({w} == {q.W}");
        }

        public static void AssertEquals(TK.Matrix4 tkM, Matrix3x3 mat, string msg)
        {
            //Note: OpenTK 4x4 matrix is a row-vector matrix, so compare rows to AssimpNet Matrix3x3 columns
            TK.Vector4 row0 = tkM.Row0;
            TK.Vector4 row1 = tkM.Row1;
            TK.Vector4 row2 = tkM.Row2;

            AssertEquals(row0.X, row0.Y, row0.Z, new Vector3(mat.A1, mat.B1, mat.C1), msg + " => checking first column vector");
            AssertEquals(row1.X, row1.Y, row1.Z, new Vector3(mat.A2, mat.B2, mat.C2), msg + " => checking second column vector");
            AssertEquals(row2.X, row2.Y, row2.Z, new Vector3(mat.A3, mat.B3, mat.C3), msg + " => checking third column vector");
        }

        public static void AssertEquals(TK.Vector4 v1, TK.Vector4 v2, string msg)
        {
            AssertEquals(v1.X, v2.X, msg + $" => checking X component ({v1.X} == {v2.X}");
            AssertEquals(v1.Y, v2.Y, msg + $" => checking Y component ({v1.Y} == {v2.Y}");
            AssertEquals(v1.Z, v2.Z, msg + $" => checking Z component ({v1.Z} == {v2.Z}");
            AssertEquals(v1.W, v2.W, msg + $" => checking W component ({v1.W} == {v2.W}");
        }

        public static void AssertEquals(TK.Quaternion q1, Quaternion q2, string msg)
        {
            AssertEquals(q1.X, q2.X, msg + $" => checking X component ({q1.X} == {q2.X}");
            AssertEquals(q1.Y, q2.Y, msg + $" => checking Y component ({q1.Y} == {q2.Y}");
            AssertEquals(q1.Z, q2.Z, msg + $" => checking Z component ({q1.Z} == {q2.Z}");
            AssertEquals(q1.W, q2.W, msg + $" => checking W component ({q1.W} == {q2.W}");
        }

        public static void AssertEquals(TK.Matrix4 tkM, Matrix4x4 mat, string msg)
        {
            //Note: OpenTK 4x4 matrix is a row-vector matrix, so compare rows to AssimpNet Matrix4x4 columns
            AssertEquals(tkM.Row0, new TK.Vector4(mat.M11, mat.M21, mat.M31, mat.M41), msg + " => checking first column vector");
            AssertEquals(tkM.Row1, new TK.Vector4(mat.M12, mat.M22, mat.M32, mat.M42), msg + " => checking second column vector");
            AssertEquals(tkM.Row2, new TK.Vector4(mat.M13, mat.M23, mat.M33, mat.M43), msg + " => checking third column vector");
            AssertEquals(tkM.Row3, new TK.Vector4(mat.M14, mat.M24, mat.M34, mat.M44), msg + " => checking third column vector");
        }
    }
}
