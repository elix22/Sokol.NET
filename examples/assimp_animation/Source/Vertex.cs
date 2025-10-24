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
using System.Numerics;
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential)]
public struct Vertex
{
    public Vector3 Position;
    public Vector4 Color;

    public Vector2 TexCoord;

    public Vertex(Vector3 position, Vector4 color, Vector2 texCoord)
    {
        Position = position;
        Color = color;
        TexCoord = texCoord;
    }
}