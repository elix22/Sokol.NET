using System;
using System.IO;
using Sokol;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Numerics;
using static Sokol.SApp;
using static Sokol.SG;
using static Sokol.SGlue;
using static Sokol.SG.sg_vertex_format;
using static Sokol.SG.sg_index_type;
using static Sokol.SG.sg_cull_mode;
using static Sokol.SG.sg_compare_func;
using static Sokol.Utils;
using System.Diagnostics;
using static Sokol.SLog;
using static Sokol.SDebugUI;
using Assimp.Configs;
using static Sokol.SFetch;
using static Sokol.SDebugText;
using static assimp_simple_app_shader_cs.Shaders;
using Assimp;

public class SimpleMesh
{

    public SimpleMesh(SimpleModel  parentModel , float[] vertices, UInt16[] indices , List<Texture> textures)
    {
        _parentModel = parentModel;

        Textures = textures;

        VertexBuffer = sg_make_buffer(new sg_buffer_desc()
        {
            data = SG_RANGE(vertices),
            label = "assimp-simple-vertex-buffer"
        });
        IndexBuffer = sg_make_buffer(new sg_buffer_desc()
        {
            usage = new sg_buffer_usage { index_buffer = true },
            data = SG_RANGE(indices),
            label = "assimp-simple-index-buffer"
        });

        VertexCount = vertices.Length / 7; // 3 pos + 4 color
        IndexCount = indices.Length;
    }

    public void Draw()
    {
        if (Textures.Count == 0 || Textures[0] == null)
        {
            return;
        }
        if (Textures[0].IsValid == false)
        {
            return;
        }
        
        sg_bindings bind = default;
        bind.vertex_buffers[0] = VertexBuffer;
        bind.index_buffer = IndexBuffer;
        bind.views[0] = Textures[0].View;
        bind.samplers[0] = Textures[0].Sampler;
        sg_apply_bindings(bind);
        sg_draw(0, (uint)IndexCount, 1);
    }
    


    public sg_buffer VertexBuffer;
    public sg_buffer IndexBuffer;

    public int VertexCount;
    public int IndexCount;
    SimpleModel _parentModel;

    public List<Texture> Textures = new List<Texture>();
}

