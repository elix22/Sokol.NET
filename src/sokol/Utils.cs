using Sokol;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Numerics;
using static Sokol.SG;
using static Sokol.SShape;
using static Sokol.SGlue;
using static Sokol.SFetch;
using static Sokol.SG.sg_vertex_format;
using static Sokol.SG.sg_index_type;
using static Sokol.SG.sg_cull_mode;
using static Sokol.SG.sg_compare_func;
using System.Text;
using static Sokol.CGltf;

namespace Sokol
{
    public static unsafe class Utils
    {
#if __IOS__
      const string  sokol_lib = "@rpath/sokol.framework/sokol";
#else
        const string sokol_lib = "sokol";
#endif

        [DllImport(sokol_lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern byte* fileutil_get_path(string filename, byte* buf, int buf_size);

        static Random _random = new Random();

        public static int random()
        {
            return _random.Next();
        }

        public static int rand()
        {
            return _random.Next();
        }

        public static sg_range SG_RANGE<T>(Span<T> span) where T : unmanaged
        {
            return new sg_range()
            {
                ptr = Unsafe.AsPointer(ref span[0]),
                size = (uint)(span.Length * Marshal.SizeOf<T>())
            };
        }
       
        public static sg_range SG_RANGE<T>(ReadOnlySpan<T> span) where T : unmanaged
        {
            return new sg_range()
            {
                ptr = Unsafe.AsPointer(ref MemoryMarshal.GetReference(span)),
                size = (uint)(span.Length * Marshal.SizeOf<T>())
            };
        }

        public static sg_range SG_RANGE<T>(T[] array) where T : unmanaged
        {
            fixed (T* ptr = array)
            {
                return new sg_range()
                {
                    ptr = ptr,
                    size = (uint)(array.Length * Marshal.SizeOf<T>())
                };
            }
        }

        public static sg_range SG_RANGE<T>(ref T value) where T : unmanaged
        {
            return new sg_range()
            {
                ptr = Unsafe.AsPointer(ref value),
                size = (uint)Marshal.SizeOf<T>()
            };
        }


        public static sg_range SG_RANGE(float[] vertices)
        {
            var result = new sg_range()
            {
                ptr = Unsafe.AsPointer(ref vertices[0]),
                size = (uint)vertices.Length * sizeof(float)
            };
            return result;
        }
        public static sg_range SG_RANGE(UInt16[] indices)
        {
            var result = new sg_range()
            {
                ptr = Unsafe.AsPointer(ref indices[0]),
                size = (uint)indices.Length * sizeof(UInt16)
            };
            return result;
        }

        public static sg_range SG_RANGE(byte[] indices)
        {
            var result = new sg_range()
            {
                ptr = Unsafe.AsPointer(ref indices[0]),
                size = (uint)indices.Length
            };
            return result;
        }




        public static sshape_range SSHAPE_RANGE(UInt16[] indices)
        {
            var result = new sshape_range()
            {
                ptr = Unsafe.AsPointer(ref indices[0]),
                size = (uint)indices.Length * sizeof(UInt16)
            };
            return result;
        }

        public static sshape_range SSHAPE_RANGE(sshape_vertex_t[] vertices)
        {
            var result = new sshape_range()
            {
                ptr = Unsafe.AsPointer(ref vertices[0]),
                size = (nuint)(vertices.Length * Marshal.SizeOf<sshape_vertex_t>())
            };
            return result;
        }
        //

        public static sg_range SG_RANGE(int[] indices)
        {
            var result = new sg_range()
            {
                ptr = Unsafe.AsPointer(ref indices[0]),
                size = (uint)indices.Length * sizeof(int)
            };
            return result;
        }

        public static sg_range SG_RANGE(uint[] indices)
        {
            var result = new sg_range()
            {
                ptr = Unsafe.AsPointer(ref indices[0]),
                size = (uint)indices.Length * sizeof(uint)
            };
            return result;
        }
        public static sg_range SG_RANGE(uint[,] indices)
        {
            int total = indices.GetLength(0) * indices.GetLength(1);
            var result = new sg_range()
            {
                ptr = Unsafe.AsPointer(ref indices[0, 0]),
                size = (uint)total * sizeof(uint)
            };
            return result;
        }

        public static sg_range SG_RANGE(uint* pixels, int length)
        {
            var result = new sg_range()
            {
                ptr = pixels,
                size = (uint)length * sizeof(uint)
            };
            return result;
        }

        public static sfetch_range_t SFETCH_RANGE(byte[] data)
        {
            var result = new sfetch_range_t()
            {
                ptr = Unsafe.AsPointer(ref data[0]),
                size = (uint)data.Length
            };
            return result;
        }

        public static sfetch_range_t SFETCH_RANGE(SharedBuffer buffer)
        {
            return SFETCH_RANGE(buffer.Buffer);
        }

        public static string util_get_file_path(string filename)
        {
            // Remove problematic path prefixes that can cause platform-specific loading issues
            
            // Remove "./" prefix (current directory reference)
            if (filename.StartsWith("./"))
            {
                filename = filename.Substring(2);
            }
            // Remove ".\\" prefix (Windows current directory reference)
            else if (filename.StartsWith(".\\"))
            {
                filename = filename.Substring(2);
            }
            // Remove leading "/" (absolute path - convert to relative)
            else if (filename.StartsWith("/") && !filename.StartsWith("//"))
            {
                filename = filename.Substring(1);
            }
            // Remove leading "\" (Windows absolute path - convert to relative)
            else if (filename.StartsWith("\\") && !filename.StartsWith("\\\\"))
            {
                filename = filename.Substring(1);
            }
            
            string fullPath = "";
            byte[] temp_buf = new byte[1024];
            fixed (byte* buf_ptr = &temp_buf[0])
            {
                byte* result = fileutil_get_path(filename, buf_ptr, 1024);
                fullPath = Marshal.PtrToStringUTF8((IntPtr)result);
            }
            return fullPath;
        }

        /// <summary>
        /// Allocates a new byte buffer sized for 'count' elements of type T
        /// and returns a Span<T> view over it.
        /// </summary>
        public static Span<T> CreateSpan<T>(int count) where T : unmanaged
        {
            int totalBytes = Marshal.SizeOf<T>() * count;
            byte[] byteBuffer = new byte[totalBytes];
            return MemoryMarshal.Cast<byte, T>(byteBuffer.AsSpan());
        }

        public static string String(this nint ptr)
        {
            return Marshal.PtrToStringUTF8((IntPtr)ptr);
        }

        /// <summary>
        /// Returns a reference to the first float field (M11) of the Matrix4x4.
        /// WARNING: This method does not work correctly on Web (.NET 8) builds.
        /// For cross-platform compatibility, use 'in matrix.M11' instead.
        /// </summary>
#if NET8_0 && WEB
        [Obsolete("AsFloat() does not work correctly on Web (.NET 8). Use 'in matrix.M11' instead. Example: sshape_make_mat4(in matrix.M11)", error: true)]
#else
        [Obsolete("AsFloat() may not work on all platforms. For cross-platform compatibility, prefer 'in matrix.M11'. Example: sshape_make_mat4(in matrix.M11)", error: true)]
#endif
        public static ref float AsFloat(this Matrix4x4 matrix)
        {
            // This reinterprets the Matrix4x4 as a float.
            return ref Unsafe.As<Matrix4x4, float>(ref matrix);
        }

        /// <summary>
        /// Returns a reference to the first float field (M11) of the Matrix3x2.
        /// WARNING: This method does not work correctly on Web (.NET 8) builds.
        /// For cross-platform compatibility, use 'in matrix.M11' instead.
        /// </summary>
#if NET8_0 && WEB
        [Obsolete("AsFloat() does not work correctly on Web (.NET 8). Use 'in matrix.M11' instead.", error: true)]
#else
        [Obsolete("AsFloat() may not work on all platforms. For cross-platform compatibility, prefer 'in matrix.M11'.", error: true)]
#endif
        public static ref float AsFloat(this Matrix3x2 matrix)
        {
            // This reinterprets the Matrix3x2 as a float.
            return ref Unsafe.As<Matrix3x2, float>(ref matrix);
        }

        /// <summary>
        /// Returns a reference to the first float field (X) of the Vector2.
        /// WARNING: This method does not work correctly on Web (.NET 8) builds.
        /// For cross-platform compatibility, use 'in vector.X' instead.
        /// </summary>
#if NET8_0 && WEB
        [Obsolete("AsFloat() does not work correctly on Web (.NET 8). Use 'in vector.X' instead.", error: true)]
#else
        [Obsolete("AsFloat() may not work on all platforms. For cross-platform compatibility, prefer 'in vector.X'.", error: true)]
#endif
        public static ref float AsFloat(this Vector2 vector)
        {
            // This reinterprets the Vector2 as a float.
            return ref Unsafe.As<Vector2, float>(ref vector);
        }

        /// <summary>
        /// Returns a reference to the first float field (X) of the Vector3.
        /// WARNING: This method does not work correctly on Web (.NET 8) builds.
        /// For cross-platform compatibility, use 'in vector.X' instead.
        /// </summary>
#if NET8_0 && WEB
        [Obsolete("AsFloat() does not work correctly on Web (.NET 8). Use 'in vector.X' instead.", error: true)]
#else
        [Obsolete("AsFloat() may not work on all platforms. For cross-platform compatibility, prefer 'in vector.X'.", error: true)]
#endif
        public static ref float AsFloat(this Vector3 vector)
        {
            // This reinterprets the Vector3 as a float.
            return ref Unsafe.As<Vector3, float>(ref vector);
        }

        /// <summary>
        /// Returns a reference to the first float field (X) of the Vector4.
        /// WARNING: This method does not work correctly on Web (.NET 8) builds.
        /// For cross-platform compatibility, use 'in vector.X' instead.
        /// </summary>
#if NET8_0 && WEB
        [Obsolete("AsFloat() does not work correctly on Web (.NET 8). Use 'in vector.X' instead.", error: true)]
#else
        [Obsolete("AsFloat() may not work on all platforms. For cross-platform compatibility, prefer 'in vector.X'.", error: true)]
#endif
        public static ref float AsFloat(this Vector4 vector)
        {
            // This reinterprets the Vector4 as a float.
            return ref Unsafe.As<Vector4, float>(ref vector);
        }

        /// <summary>
        /// Returns a reference to the first float field (X) of the Quaternion.
        /// WARNING: This method does not work correctly on Web (.NET 8) builds.
        /// For cross-platform compatibility, use 'in quaternion.X' instead.
        /// </summary>
#if NET8_0 && WEB
        [Obsolete("AsFloat() does not work correctly on Web (.NET 8). Use 'in quaternion.X' instead.", error: true)]
#else
        [Obsolete("AsFloat() may not work on all platforms. For cross-platform compatibility, prefer 'in quaternion.X'.", error: true)]
#endif
        public static ref float AsFloat(this Quaternion quaternion)
        {
            // This reinterprets the Quaternion as a float.
            return ref Unsafe.As<Quaternion, float>(ref quaternion);
        }

        /// <summary>
        /// Returns a reference to the first float field (Normal.X) of the Plane.
        /// WARNING: This method does not work correctly on Web (.NET 8) builds.
        /// For cross-platform compatibility, use 'in plane.Normal.X' instead.
        /// </summary>
#if NET8_0 && WEB
        [Obsolete("AsFloat() does not work correctly on Web (.NET 8). Use 'in plane.Normal.X' instead.", error: true)]
#else
        [Obsolete("AsFloat() may not work on all platforms. For cross-platform compatibility, prefer 'in plane.Normal.X'.", error: true)]
#endif
        public static ref float AsFloat(this Plane plane)
        {
            // This reinterprets the Plane as a float.
            return ref Unsafe.As<Plane, float>(ref plane);
        }
    }

}