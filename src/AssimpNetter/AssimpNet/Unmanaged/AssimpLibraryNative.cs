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
using System.Runtime.InteropServices;

namespace Assimp.Unmanaged
{
    /// <summary>
    /// Native Assimp library functions using direct DllImport (modern .NET approach).
    /// </summary>
    internal static unsafe class AssimpLibraryNative
    {
#if __IOS__
        private const string LibName = "@rpath/assimp.framework/assimp";
#else
        private const string LibName = "assimp";
#endif

        #region Import Functions

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr aiImportFile([In, MarshalAs(UnmanagedType.LPUTF8Str)] string file, uint flags);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr aiImportFileEx([In, MarshalAs(UnmanagedType.LPUTF8Str)] string file, uint flags, IntPtr fileIO);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr aiImportFileExWithProperties([In, MarshalAs(UnmanagedType.LPUTF8Str)] string file, uint flags, IntPtr fileIO, IntPtr propStore);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr aiImportFileFromMemory(IntPtr buffer, uint bufferLength, uint flags, [In, MarshalAs(UnmanagedType.LPUTF8Str)] string formatHint);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr aiImportFileFromMemoryWithProperties(IntPtr buffer, uint bufferLength, uint flags, [In, MarshalAs(UnmanagedType.LPUTF8Str)] string formatHint, IntPtr propStore);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void aiReleaseImport(IntPtr scene);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr aiApplyPostProcessing(IntPtr scene, uint Flags);

        #endregion

        #region Logging Functions

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void aiAttachLogStream(IntPtr logStreamPtr);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void aiEnableVerboseLogging([In, MarshalAs(UnmanagedType.Bool)] bool enable);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern ReturnCode aiDetachLogStream(IntPtr logStreamPtr);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void aiDetachAllLogStreams();

        #endregion

        #region Property Functions

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr aiCreatePropertyStore();

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void aiReleasePropertyStore(IntPtr propertyStore);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void aiSetImportPropertyInteger(IntPtr propertyStore, [In, MarshalAs(UnmanagedType.LPUTF8Str)] string name, int value);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void aiSetImportPropertyFloat(IntPtr propertyStore, [In, MarshalAs(UnmanagedType.LPUTF8Str)] string name, float value);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void aiSetImportPropertyString(IntPtr propertyStore, [In, MarshalAs(UnmanagedType.LPUTF8Str)] string name, ref AiString value);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void aiSetImportPropertyMatrix(IntPtr propertyStore, [In, MarshalAs(UnmanagedType.LPUTF8Str)] string name, ref Matrix4x4 value);

        #endregion

        #region Material Functions

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern ReturnCode aiGetMaterialColor(ref AiMaterial mat, [In, MarshalAs(UnmanagedType.LPUTF8Str)] string key, uint texType, uint texIndex, IntPtr colorOut);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern ReturnCode aiGetMaterialFloatArray(ref AiMaterial mat, [In, MarshalAs(UnmanagedType.LPUTF8Str)] string key, uint texType, uint texIndex, IntPtr ptrOut, ref uint valueCount);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern ReturnCode aiGetMaterialIntegerArray(ref AiMaterial mat, [In, MarshalAs(UnmanagedType.LPUTF8Str)] string key, uint texType, uint texIndex, IntPtr ptrOut, ref uint valueCount);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern ReturnCode aiGetMaterialProperty(ref AiMaterial mat, [In, MarshalAs(UnmanagedType.LPUTF8Str)] string key, uint texType, uint texIndex, out IntPtr propertyOut);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern ReturnCode aiGetMaterialString(ref AiMaterial mat, [In, MarshalAs(UnmanagedType.LPUTF8Str)] string key, uint texType, uint texIndex, out AiString str);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern ReturnCode aiGetMaterialTexture(ref AiMaterial mat, TextureType type, uint index, out AiString path, out TextureMapping mapping, out uint uvIndex, out float blendFactor, out TextureOperation textureOp, [In, Out] TextureWrapMode[] wrapModes, out uint flags);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint aiGetMaterialTextureCount(ref AiMaterial mat, TextureType type);

        #endregion

        #region Error and Info Functions

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr aiGetErrorString();

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void aiGetExtensionList(ref AiString extensionsOut);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void aiGetMemoryRequirements(IntPtr scene, ref AiMemoryInfo memoryInfo);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool aiIsExtensionSupported([In, MarshalAs(UnmanagedType.LPUTF8Str)] string extension);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern UIntPtr aiGetImportFormatCount();

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr aiGetImportFormatDescription(UIntPtr index);

        #endregion

        #region Version Info Functions

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr aiGetLegalString();

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint aiGetVersionMinor();

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint aiGetVersionMajor();

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint aiGetVersionRevision();
        
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr aiGetBranchName();

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint aiGetCompileFlags();

        #endregion

        #region Embedded Texture Functions

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr aiGetEmbeddedTexture(IntPtr scene, [In, MarshalAs(UnmanagedType.LPUTF8Str)] string filename);

        #endregion
    }
}
