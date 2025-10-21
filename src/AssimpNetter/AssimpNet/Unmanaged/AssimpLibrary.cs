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
    /// Singleton that governs access to the unmanaged Assimp library functions.
    /// </summary>
    public sealed class AssimpLibrary
    {
        private static readonly object s_sync = new object();
        private static AssimpLibrary s_instance;
        private bool m_enableVerboseLogging = false;

        /// <summary>
        /// Gets the AssimpLibrary instance.
        /// </summary>
        public static AssimpLibrary Instance
        {
            get
            {
                lock(s_sync)
                {
                    if(s_instance == null)
                        s_instance = new AssimpLibrary();

                    return s_instance;
                }
            }
        }

        /// <summary>
        /// Gets if the Assimp unmanaged library supports multithreading. If it was compiled for single threading only,
        /// then it will not utilize multiple threads during import.
        /// </summary>
        public bool IsMultithreadingSupported => !((GetCompileFlags() & CompileFlags.SingleThreaded) == CompileFlags.SingleThreaded);

        private AssimpLibrary() { }

        #region Import Methods

        /// <summary>
        /// Imports a file.
        /// </summary>
        /// <param name="file">Valid filename</param>
        /// <param name="flags">Post process flags specifying what steps are to be run after the import.</param>
        /// <param name="propStore">Property store containing config name-values, may be null.</param>
        /// <returns>Pointer to the unmanaged data structure.</returns>
        public IntPtr ImportFile(string file, PostProcessSteps flags, IntPtr propStore)
        {
            return ImportFile(file, flags, IntPtr.Zero, propStore);
        }

        /// <summary>
        /// Imports a file.
        /// </summary>
        /// <param name="file">Valid filename</param>
        /// <param name="flags">Post process flags specifying what steps are to be run after the import.</param>
        /// <param name="fileIO">Pointer to an instance of AiFileIO, a custom file IO system used to open the model and 
        /// any associated file the loader needs to open, passing NULL uses the default implementation.</param>
        /// <param name="propStore">Property store containing config name-values, may be null.</param>
        /// <returns>Pointer to the unmanaged data structure.</returns>
        public IntPtr ImportFile(string file, PostProcessSteps flags, IntPtr fileIO, IntPtr propStore)
        {
            if(propStore == IntPtr.Zero && fileIO == IntPtr.Zero)
            {
                return AssimpLibraryNative.aiImportFile(file, (uint) flags);
            }
            else if(propStore == IntPtr.Zero && fileIO != IntPtr.Zero)
            {
                return AssimpLibraryNative.aiImportFileEx(file, (uint) flags, fileIO);
            }
            else if(propStore != IntPtr.Zero)
            {
                return AssimpLibraryNative.aiImportFileExWithProperties(file, (uint) flags, fileIO, propStore);
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// Imports a scene from a stream. This uses the "aiImportFileFromMemory" function. The stream can be from anyplace,
        /// not just a memory stream. It is up to the caller to dispose of the stream.
        /// </summary>
        /// <param name="input">Stream containing the scene data</param>
        /// <param name="flags">Post processing flags</param>
        /// <param name="formatHint">A hint to Assimp to decide which importer to use to process the data</param>
        /// <param name="propStore">Property store containing the config name-values, may be null.</param>
        /// <returns>Pointer to the unmanaged data structure.</returns>
        public IntPtr ImportFileFromStream(System.IO.Stream input, PostProcessSteps flags, string formatHint, IntPtr propStore)
        {
            byte[] buffer = MemoryHelper.ReadStreamFully(input, 0);

            IntPtr memPtr = IntPtr.Zero;
            try
            {
                memPtr = MemoryHelper.AllocateMemory(buffer.Length);
                Marshal.Copy(buffer, 0, memPtr, buffer.Length);

                if(propStore == IntPtr.Zero)
                {
                    return AssimpLibraryNative.aiImportFileFromMemory(memPtr, (uint) buffer.Length, (uint) flags, formatHint);
                }
                else
                {
                    return AssimpLibraryNative.aiImportFileFromMemoryWithProperties(memPtr, (uint) buffer.Length, (uint) flags, formatHint, propStore);
                }
            }
            finally
            {
                if(memPtr != IntPtr.Zero)
                {
                    MemoryHelper.FreeMemory(memPtr);
                }
            }
        }

        /// <summary>
        /// Releases the unmanaged scene data structure. This should NOT be used for unmanaged scenes that were marshaled
        /// from the managed scene structure - only for scenes whose memory was allocated by the native library!
        /// </summary>
        /// <param name="scene">Pointer to the unmanaged scene data structure.</param>
        public void ReleaseImport(IntPtr scene)
        {
            if(scene != IntPtr.Zero)
            {
                AssimpLibraryNative.aiReleaseImport(scene);
            }
        }

        /// <summary>
        /// Applies a post-processing step on an already imported scene.
        /// </summary>
        /// <param name="scene">Pointer to the unmanaged scene data structure.</param>
        /// <param name="flags">Post processing steps to run.</param>
        /// <returns>Pointer to the unmanaged scene data structure.</returns>
        public IntPtr ApplyPostProcessing(IntPtr scene, PostProcessSteps flags)
        {
            if(scene == IntPtr.Zero)
                return IntPtr.Zero;

            return AssimpLibraryNative.aiApplyPostProcessing(scene, (uint) flags);
        }

        #endregion

        #region Logging Methods

        /// <summary>
        /// Attaches a log stream callback to catch Assimp messages.
        /// </summary>
        /// <param name="logStreamPtr">Pointer to an instance of AiLogStream.</param>
        public void AttachLogStream(IntPtr logStreamPtr)
        {
            AssimpLibraryNative.aiAttachLogStream(logStreamPtr);
        }

        /// <summary>
        /// Enables verbose logging.
        /// </summary>
        /// <param name="enable">True if verbose logging is to be enabled or not.</param>
        public void EnableVerboseLogging(bool enable)
        {
            AssimpLibraryNative.aiEnableVerboseLogging(enable);
            m_enableVerboseLogging = enable;
        }

        /// <summary>
        /// Gets if verbose logging is enabled.
        /// </summary>
        /// <returns>True if verbose logging is enabled, false otherwise.</returns>
        public bool GetVerboseLoggingEnabled()
        {
            return m_enableVerboseLogging;
        }

        /// <summary>
        /// Detaches a logstream callback.
        /// </summary>
        /// <param name="logStreamPtr">Pointer to an instance of AiLogStream.</param>
        /// <returns>A return code signifying if the function was successful or not.</returns>
        public ReturnCode DetachLogStream(IntPtr logStreamPtr)
        {
            return AssimpLibraryNative.aiDetachLogStream(logStreamPtr);
        }

        /// <summary>
        /// Detaches all logstream callbacks currently attached to Assimp.
        /// </summary>
        public void DetachAllLogStreams()
        {
            AssimpLibraryNative.aiDetachAllLogStreams();
        }

        #endregion

        #region Import Properties Setters

        /// <summary>
        /// Create an empty property store. Property stores are used to collect import settings.
        /// </summary>
        /// <returns>Pointer to property store</returns>
        public IntPtr CreatePropertyStore()
        {
            return AssimpLibraryNative.aiCreatePropertyStore();
        }

        /// <summary>
        /// Deletes a property store.
        /// </summary>
        /// <param name="propertyStore">Pointer to property store</param>
        public void ReleasePropertyStore(IntPtr propertyStore)
        {
            if(propertyStore == IntPtr.Zero)
                return;

            AssimpLibraryNative.aiReleasePropertyStore(propertyStore);
        }

        /// <summary>
        /// Sets an integer property value.
        /// </summary>
        /// <param name="propertyStore">Pointer to property store</param>
        /// <param name="name">Property name</param>
        /// <param name="value">Property value</param>
        public void SetImportPropertyInteger(IntPtr propertyStore, string name, int value)
        {
            if(propertyStore == IntPtr.Zero || string.IsNullOrEmpty(name))
                return;

            AssimpLibraryNative.aiSetImportPropertyInteger(propertyStore, name, value);
        }

        /// <summary>
        /// Sets a float property value.
        /// </summary>
        /// <param name="propertyStore">Pointer to property store</param>
        /// <param name="name">Property name</param>
        /// <param name="value">Property value</param>
        public void SetImportPropertyFloat(IntPtr propertyStore, string name, float value)
        {
            if(propertyStore == IntPtr.Zero || string.IsNullOrEmpty(name))
                return;

            AssimpLibraryNative.aiSetImportPropertyFloat(propertyStore, name, value);
        }

        /// <summary>
        /// Sets a string property value.
        /// </summary>
        /// <param name="propertyStore">Pointer to property store</param>
        /// <param name="name">Property name</param>
        /// <param name="value">Property value</param>
        public void SetImportPropertyString(IntPtr propertyStore, string name, string value)
        {
            if(propertyStore == IntPtr.Zero || string.IsNullOrEmpty(name))
                return;

            AiString str = new AiString();
            if(str.SetString(value))
                AssimpLibraryNative.aiSetImportPropertyString(propertyStore, name, ref str);
        }

        /// <summary>
        /// Sets a matrix property value.
        /// </summary>
        /// <param name="propertyStore">Pointer to property store</param>
        /// <param name="name">Property name</param>
        /// <param name="value">Property value</param>
        public void SetImportPropertyMatrix(IntPtr propertyStore, string name, Matrix4x4 value)
        {
            if(propertyStore == IntPtr.Zero || string.IsNullOrEmpty(name))
                return;

            AssimpLibraryNative.aiSetImportPropertyMatrix(propertyStore, name, ref value);
        }

        #endregion

        #region Material Getters

        /// <summary>
        /// Retrieves a color value from the material property table.
        /// </summary>
        /// <param name="mat">Material to retrieve the data from</param>
        /// <param name="key">Ai mat key (base) name to search for</param>
        /// <param name="texType">Texture Type semantic, always zero for non-texture properties</param>
        /// <param name="texIndex">Texture index, always zero for non-texture properties</param>
        /// <returns>The color if it exists. If not, the default Vector4 value is returned.</returns>
        public Vector4 GetMaterialColor(ref AiMaterial mat, string key, TextureType texType, uint texIndex)
        {
            IntPtr ptr = IntPtr.Zero;
            try
            {
                ptr = MemoryHelper.AllocateMemory(MemoryHelper.SizeOf<Vector4>());
                ReturnCode code = AssimpLibraryNative.aiGetMaterialColor(ref mat, key, (uint) texType, texIndex, ptr);
                Vector4 color = new Vector4();
                if(code == ReturnCode.Success && ptr != IntPtr.Zero)
                    color = MemoryHelper.Read<Vector4>(ptr);

                return color;
            }
            finally
            {
                if(ptr != IntPtr.Zero)
                    MemoryHelper.FreeMemory(ptr);
            }
        }

        /// <summary>
        /// Retrieves an array of float values with the specific key from the material.
        /// </summary>
        /// <param name="mat">Material to retrieve the data from</param>
        /// <param name="key">Ai mat key (base) name to search for</param>
        /// <param name="texType">Texture Type semantic, always zero for non-texture properties</param>
        /// <param name="texIndex">Texture index, always zero for non-texture properties</param>
        /// <param name="floatCount">The maximum number of floats to read. This may not accurately describe the data returned, as it may not exist or be smaller. If this value is less than
        /// the available floats, then only the requested number is returned (e.g. 1 or 2 out of a 4 float array).</param>
        /// <returns>The float array, if it exists</returns>
        public float[] GetMaterialFloatArray(ref AiMaterial mat, string key, TextureType texType, uint texIndex, uint floatCount)
        {
            IntPtr ptr = IntPtr.Zero;
            try
            {
                ptr = MemoryHelper.AllocateMemory(IntPtr.Size);
                ReturnCode code = AssimpLibraryNative.aiGetMaterialFloatArray(ref mat, key, (uint) texType, texIndex, ptr, ref floatCount);
                float[] array = null;
                if(code == ReturnCode.Success && floatCount > 0)
                {
                    array = new float[floatCount];
                    MemoryHelper.Read<float>(ptr, array, 0, (int) floatCount);
                }
                return array;
            }
            finally
            {
                if(ptr != IntPtr.Zero)
                {
                    MemoryHelper.FreeMemory(ptr);
                }
            }
        }

        /// <summary>
        /// Retrieves an array of integer values with the specific key from the material.
        /// </summary>
        /// <param name="mat">Material to retrieve the data from</param>
        /// <param name="key">Ai mat key (base) name to search for</param>
        /// <param name="texType">Texture Type semantic, always zero for non-texture properties</param>
        /// <param name="texIndex">Texture index, always zero for non-texture properties</param>
        /// <param name="intCount">The maximum number of integers to read. This may not accurately describe the data returned, as it may not exist or be smaller. If this value is less than
        /// the available integers, then only the requested number is returned (e.g. 1 or 2 out of a 4 float array).</param>
        /// <returns>The integer array, if it exists</returns>
        public int[] GetMaterialIntegerArray(ref AiMaterial mat, string key, TextureType texType, uint texIndex, uint intCount)
        {
            IntPtr ptr = IntPtr.Zero;
            try
            {
                ptr = MemoryHelper.AllocateMemory(IntPtr.Size);
                ReturnCode code = AssimpLibraryNative.aiGetMaterialIntegerArray(ref mat, key, (uint) texType, texIndex, ptr, ref intCount);
                int[] array = null;
                if(code == ReturnCode.Success && intCount > 0)
                {
                    array = new int[intCount];
                    MemoryHelper.Read<int>(ptr, array, 0, (int) intCount);
                }
                return array;
            }
            finally
            {
                if(ptr != IntPtr.Zero)
                {
                    MemoryHelper.FreeMemory(ptr);
                }
            }
        }

        /// <summary>
        /// Retrieves a material property with the specific key from the material.
        /// </summary>
        /// <param name="mat">Material to retrieve the property from</param>
        /// <param name="key">Ai mat key (base) name to search for</param>
        /// <param name="texType">Texture Type semantic, always zero for non-texture properties</param>
        /// <param name="texIndex">Texture index, always zero for non-texture properties</param>
        /// <returns>The material property, if found.</returns>
        public AiMaterialProperty GetMaterialProperty(ref AiMaterial mat, string key, TextureType texType, uint texIndex)
        {
            IntPtr ptr;
            ReturnCode code = AssimpLibraryNative.aiGetMaterialProperty(ref mat, key, (uint) texType, texIndex, out ptr);
            AiMaterialProperty prop = new AiMaterialProperty();
            if(code == ReturnCode.Success && ptr != IntPtr.Zero)
                prop = MemoryHelper.Read<AiMaterialProperty>(ptr);

            return prop;
        }

        /// <summary>
        /// Retrieves a string from the material property table.
        /// </summary>
        /// <param name="mat">Material to retrieve the data from</param>
        /// <param name="key">Ai mat key (base) name to search for</param>
        /// <param name="texType">Texture Type semantic, always zero for non-texture properties</param>
        /// <param name="texIndex">Texture index, always zero for non-texture properties</param>
        /// <returns>The string, if it exists. If not, an empty string is returned.</returns>
        public string GetMaterialString(ref AiMaterial mat, string key, TextureType texType, uint texIndex)
        {
            AiString str;
            ReturnCode code = AssimpLibraryNative.aiGetMaterialString(ref mat, key, (uint) texType, texIndex, out str);
            if(code == ReturnCode.Success)
                return str.GetString();

            return string.Empty;
        }

        /// <summary>
        /// Gets the number of textures contained in the material for a particular texture type.
        /// </summary>
        /// <param name="mat">Material to retrieve the data from</param>
        /// <param name="type">Texture Type semantic</param>
        /// <returns>The number of textures for the type.</returns>
        public uint GetMaterialTextureCount(ref AiMaterial mat, TextureType type)
        {
            return AssimpLibraryNative.aiGetMaterialTextureCount(ref mat, type);
        }

        /// <summary>
        /// Gets the texture filepath contained in the material.
        /// </summary>
        /// <param name="mat">Material to retrieve the data from</param>
        /// <param name="type">Texture type semantic</param>
        /// <param name="index">Texture index</param>
        /// <returns>The texture filepath, if it exists. If not an empty string is returned.</returns>
        public string GetMaterialTextureFilePath(ref AiMaterial mat, TextureType type, uint index)
        {
            AiString str;
            TextureMapping mapping;
            uint uvIndex;
            float blendFactor;
            TextureOperation texOp;
            TextureWrapMode[] wrapModes = new TextureWrapMode[2];
            uint flags;

            ReturnCode code = AssimpLibraryNative.aiGetMaterialTexture(ref mat, type, index, out str, out mapping, out uvIndex, out blendFactor, out texOp, wrapModes, out flags);

            if(code == ReturnCode.Success)
            {
                return str.GetString();
            }

            return string.Empty;
        }

        /// <summary>
        /// Gets all values pertaining to a particular texture from a material.
        /// </summary>
        /// <param name="mat">Material to retrieve the data from</param>
        /// <param name="type">Texture type semantic</param>
        /// <param name="index">Texture index</param>
        /// <returns>Returns the texture slot struct containing all the information.</returns>
        public TextureSlot GetMaterialTexture(ref AiMaterial mat, TextureType type, uint index)
        {
            AiString str;
            TextureMapping mapping;
            uint uvIndex;
            float blendFactor;
            TextureOperation texOp;
            TextureWrapMode[] wrapModes = new TextureWrapMode[2];
            uint flags;

            ReturnCode code = AssimpLibraryNative.aiGetMaterialTexture(ref mat, type, index, out str, out mapping, out uvIndex, out blendFactor, out texOp, wrapModes, out flags);

            return new TextureSlot(str.GetString(), type, (int) index, mapping, (int) uvIndex, blendFactor, texOp, wrapModes[0], wrapModes[1], (int) flags);
        }

        #endregion

        #region Error and Info Methods

        /// <summary>
        /// Gets the last error logged in Assimp.
        /// </summary>
        /// <returns>The last error message logged.</returns>
        public string GetErrorString()
        {
            IntPtr ptr = AssimpLibraryNative.aiGetErrorString();

            if(ptr == IntPtr.Zero)
                return string.Empty;

            return Marshal.PtrToStringAnsi(ptr);
        }

        /// <summary>
        /// Checks whether the model format extension is supported by Assimp.
        /// </summary>
        /// <param name="extension">Model format extension, e.g. ".3ds"</param>
        /// <returns>True if the format is supported, false otherwise.</returns>
        public bool IsExtensionSupported(string extension)
        {
            return AssimpLibraryNative.aiIsExtensionSupported(extension);
        }

        /// <summary>
        /// Gets all the model format extensions that are currently supported by Assimp.
        /// </summary>
        /// <returns>Array of supported format extensions</returns>
        public string[] GetExtensionList()
        {
            AiString aiString = new AiString();
            AssimpLibraryNative.aiGetExtensionList(ref aiString);
            return aiString.GetString().Split(new string[] { "*", ";*" }, StringSplitOptions.RemoveEmptyEntries);
        }

        /// <summary>
        /// Gets a collection of importer descriptions that detail metadata and feature support for each importer.
        /// </summary>
        /// <returns>Collection of importer descriptions</returns>
        public ImporterDescription[] GetImporterDescriptions()
        {
            int count = (int) AssimpLibraryNative.aiGetImportFormatCount().ToUInt32();
            ImporterDescription[] descrs = new ImporterDescription[count];

            for(int i = 0; i < count; i++)
            {
                IntPtr descrPtr = AssimpLibraryNative.aiGetImportFormatDescription(new UIntPtr((uint) i));
                if(descrPtr != IntPtr.Zero)
                {
                    ref AiImporterDesc descr = ref MemoryHelper.AsRef<AiImporterDesc>(descrPtr);
                    descrs[i] = new ImporterDescription(descr);
                }
            }

            return descrs;
        }

        /// <summary>
        /// Gets the memory requirements of the scene.
        /// </summary>
        /// <param name="scene">Pointer to the unmanaged scene data structure.</param>
        /// <returns>The memory information about the scene.</returns>
        public AiMemoryInfo GetMemoryRequirements(IntPtr scene)
        {
            AiMemoryInfo info = new AiMemoryInfo();
            if(scene != IntPtr.Zero)
            {
                AssimpLibraryNative.aiGetMemoryRequirements(scene, ref info);
            }

            return info;
        }

        #endregion

        #region Version Info

        /// <summary>
        /// Gets the Assimp legal info.
        /// </summary>
        /// <returns>String containing Assimp legal info.</returns>
        public string GetLegalString()
        {
            IntPtr ptr = AssimpLibraryNative.aiGetLegalString();

            if(ptr == IntPtr.Zero)
                return string.Empty;

            return Marshal.PtrToStringAnsi(ptr);
        }

        /// <summary>
        /// Gets the native Assimp DLL's minor version number.
        /// </summary>
        /// <returns>Assimp minor version number</returns>
        public uint GetVersionMinor()
        {
            return AssimpLibraryNative.aiGetVersionMinor();
        }

        /// <summary>
        /// Gets the native Assimp DLL's major version number.
        /// </summary>
        /// <returns>Assimp major version number</returns>
        public uint GetVersionMajor()
        {
            return AssimpLibraryNative.aiGetVersionMajor();
        }

        /// <summary>
        /// Gets the native Assimp DLL's revision version number.
        /// </summary>
        /// <returns>Assimp revision version number</returns>
        public uint GetVersionRevision()
        {
            return AssimpLibraryNative.aiGetVersionRevision();
        }

        /// <summary>
        /// Returns the branchname of the Assimp runtime.
        /// </summary>
        /// <returns>The current branch name.</returns>
        public string GetBranchName()
        {
            IntPtr ptr = AssimpLibraryNative.aiGetBranchName();

            if(ptr == IntPtr.Zero)
                return string.Empty;

            return Marshal.PtrToStringAnsi(ptr);
        }

        /// <summary>
        /// Gets the native Assimp DLL's current version number as "major.minor.revision" string. This is the
        /// version of Assimp that this wrapper is currently using.
        /// </summary>
        /// <returns>Unmanaged DLL version</returns>
        public string GetVersion()
        {
            uint major = GetVersionMajor();
            uint minor = GetVersionMinor();
            uint rev = GetVersionRevision();

            return $"{major}.{minor}.{rev}";
        }

        /// <summary>
        /// Gets the native Assimp DLL's current version number as a .NET version object.
        /// </summary>
        /// <returns>Unmanaged DLL version</returns>
        public Version GetVersionAsVersion()
        {
            return new Version((int) GetVersionMajor(), (int) GetVersionMinor(), 0, (int) GetVersionRevision());
        }

        /// <summary>
        /// Get the compilation flags that describe how the native Assimp DLL was compiled.
        /// </summary>
        /// <returns>Compilation flags</returns>
        public CompileFlags GetCompileFlags()
        {
            return (CompileFlags) AssimpLibraryNative.aiGetCompileFlags();
        }

        #endregion

        /// <summary>
        /// Gets an embedded texture.
        /// </summary>
        /// <param name="scene">Input asset.</param>
        /// <param name="filename">Texture path extracted from <see cref="GetMaterialString"/>.</param>
        /// <returns>An embedded texture, or nullptr.</returns>
        public IntPtr GetEmbeddedTexture(IntPtr scene, string filename)
        {
            if(scene == IntPtr.Zero)
                return IntPtr.Zero;

            return AssimpLibraryNative.aiGetEmbeddedTexture(scene, filename);
        }

        // Assimp's quaternions are WXYZ, C#'s are XYZW, we need to convert all of them.
        internal static Quaternion FixQuaternionFromAssimp(Quaternion quat) => new(quat.Y, quat.Z, quat.W, quat.X);
        internal static Quaternion FixQuaternionToAssimp(Quaternion quat) => new(quat.W, quat.X, quat.Y, quat.Z);
        
        internal static unsafe void FixQuaternionsInSceneFromAssimp(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero)
                return;

            var scene = (AiScene*)ptr;
            if (scene->NumAnimations == 0)
                return;

            for (uint i = 0; i < scene->NumAnimations; i++)
            {
                var anim = ((AiAnimation**)scene->Animations)[i];
                for (uint j = 0; j < anim->NumChannels; j++)
                {
                    var channel = ((AiNodeAnim**)anim->Channels)[j];
                    for (uint k = 0; k < channel->NumRotationKeys; k++)
                    {
                        ref var rotKey = ref ((QuaternionKey*)channel->RotationKeys)[k];
                        rotKey.Value = FixQuaternionFromAssimp(rotKey.Value);
                    }
                }
            }
        }
        
        internal static unsafe void FixQuaternionsInSceneToAssimp(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero)
                return;

            var scene = (AiScene*)ptr;
            if (scene->NumAnimations == 0)
                return;

            for (uint i = 0; i < scene->NumAnimations; i++)
            {
                var anim = ((AiAnimation**)scene->Animations)[i];
                for (uint j = 0; j < anim->NumChannels; j++)
                {
                    var channel = ((AiNodeAnim**)anim->Channels)[j];
                    for (uint k = 0; k < channel->NumRotationKeys; k++)
                    {
                        ref var rotKey = ref ((QuaternionKey*)channel->RotationKeys)[k];
                        rotKey.Value = FixQuaternionToAssimp(rotKey.Value);
                    }
                }
            }
        }
    }
}
