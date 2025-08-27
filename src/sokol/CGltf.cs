// machine generated, do not edit
using System;
using System.Runtime.InteropServices;
using M = System.Runtime.InteropServices.MarshalAsAttribute;
using U = System.Runtime.InteropServices.UnmanagedType;

namespace Sokol
{
    public static unsafe partial class CGltf
    {
        [StructLayout(LayoutKind.Explicit)]
        public struct cgltf_camera_data_union
        {
            [FieldOffset(0)]
            public cgltf_camera_perspective perspective;
            [FieldOffset(0)]
            public cgltf_camera_orthographic orthographic;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct cgltf_camera
        {
            public IntPtr name;
            public cgltf_camera_type type;
             public cgltf_camera_data_union data;
            public cgltf_extras extras;
            public nuint extensions_count;
            public cgltf_extension* extensions;
        }
    }

}