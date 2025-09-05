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
using static Sokol.SSpine;
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

        public static string util_get_file_path(string filename)
        {
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
            return MemoryMarshal.Cast<byte, T>(byteBuffer);
        }

        public static string String(this sspine_string str)
        {
            byte[] data = new byte[str.len];
            for (int i = 0; i < str.len; i++)
            {
                data[i] = (byte)str.cstr[i];
            }
            return Encoding.UTF8.GetString(data);
        }

        public static string String(this nint ptr)
        {
            return Marshal.PtrToStringUTF8((IntPtr)ptr);
        }

        /// <summary>
        /// Returns a reference to the first float field (M11) of the Matrix4x4.
        /// </summary>
        public static ref float AsFloat(this Matrix4x4 matrix)
        {
            // This reinterprets the Matrix4x4 as a float.
            return ref Unsafe.As<Matrix4x4, float>(ref matrix);
        }
    }

}