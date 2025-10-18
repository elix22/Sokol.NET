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
using static Sokol.SSpine;
namespace Sokol
{
    public static unsafe class Extensions
    {
        public static string String(this sspine_string str)
        {
            byte[] data = new byte[str.len];
            for (int i = 0; i < str.len; i++)
            {
            data[i] = (byte)str.cstr[i];
            }
            return Encoding.UTF8.GetString(data);
        }

    }

}