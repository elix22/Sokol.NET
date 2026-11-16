using System;
using System.Buffers.Binary;
using System.Linq;
using System.Text;
using System.Text.Json;
using GameEditor.Framework.Renderer.Server.Assets;
using Xunit;

namespace Scene.Tests
{
    /// <summary>A glTF whose scene lists a child node as a root (UniGLTF's export: Kenney's nature kit) is read from that node's
    /// root; a file whose scenes list roots is left alone.</summary>
    public class GltfSceneRootsTests
    {
        const string ChildAsRoot = "{\"asset\":{\"version\":\"2.0\"},\"scene\":0,\"scenes\":[{\"nodes\":[2,1]}]," +
                                   "\"nodes\":[{\"name\":\"tmpParent\",\"children\":[1]},{\"name\":\"tree\",\"children\":[2]},{\"name\":\"leaf\"}]}";

        static byte[] Glb(string json, byte[] bin)
        {
            var j = Encoding.UTF8.GetBytes(json);
            int jp = (j.Length + 3) & ~3, bp = (bin.Length + 3) & ~3;
            var o = new byte[12 + 8 + jp + 8 + bp];
            Encoding.ASCII.GetBytes("glTF").CopyTo(o, 0);
            BinaryPrimitives.WriteUInt32LittleEndian(o.AsSpan(4), 2);
            BinaryPrimitives.WriteUInt32LittleEndian(o.AsSpan(8), (uint)o.Length);
            BinaryPrimitives.WriteUInt32LittleEndian(o.AsSpan(12), (uint)jp);
            BinaryPrimitives.WriteUInt32LittleEndian(o.AsSpan(16), 0x4E4F534Au);
            j.CopyTo(o, 20);
            o.AsSpan(20 + j.Length, jp - j.Length).Fill((byte)' ');
            BinaryPrimitives.WriteUInt32LittleEndian(o.AsSpan(20 + jp), (uint)bp);
            BinaryPrimitives.WriteUInt32LittleEndian(o.AsSpan(24 + jp), 0x004E4942u);   // "BIN\0"
            bin.CopyTo(o, 28 + jp);
            return o;
        }

        static int[] SceneNodes(byte[] json) =>
            JsonDocument.Parse(json).RootElement.GetProperty("scenes")[0].GetProperty("nodes").EnumerateArray().Select(e => e.GetInt32()).ToArray();

        [Fact]
        public void A_child_listed_as_a_root_is_replaced_by_its_topmost_ancestor_once()
        {
            var o = GltfSceneRoots.Fixed(Encoding.UTF8.GetBytes(ChildAsRoot));
            Assert.NotNull(o);
            Assert.Equal(new[] { 0 }, SceneNodes(o!));
            // Everything else as it was.
            var nodes = JsonDocument.Parse(o!).RootElement.GetProperty("nodes");
            Assert.Equal(3, nodes.GetArrayLength());
            Assert.Equal("tree", nodes[1].GetProperty("name").GetString());
        }

        [Fact]
        public void A_glb_keeps_its_binary_chunk_and_its_lengths_add_up()
        {
            var bin = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };
            var glb = Glb(ChildAsRoot, bin);
            var o = GltfSceneRoots.Fixed(glb);
            Assert.NotNull(o);
            Assert.Equal((uint)o!.Length, BinaryPrimitives.ReadUInt32LittleEndian(o.AsSpan(8)));
            int jl = (int)BinaryPrimitives.ReadUInt32LittleEndian(o.AsSpan(12));
            Assert.Equal(0, jl % 4);
            Assert.Equal(new[] { 0 }, SceneNodes(o.AsSpan(20, jl).ToArray()));
            Assert.Equal(0x004E4942u, BinaryPrimitives.ReadUInt32LittleEndian(o.AsSpan(24 + jl)));
            Assert.Equal(bin, o.AsSpan(28 + jl, bin.Length).ToArray());
        }

        [Fact]
        public void A_file_whose_scenes_list_roots_is_left_alone()
        {
            const string ok = "{\"asset\":{\"version\":\"2.0\"},\"scenes\":[{\"nodes\":[0]}],\"nodes\":[{\"children\":[1]},{}]}";
            Assert.Null(GltfSceneRoots.Fixed(Encoding.UTF8.GetBytes(ok)));
            Assert.Null(GltfSceneRoots.Fixed(Glb(ok, new byte[4])));
            Assert.Null(GltfSceneRoots.Fixed(Encoding.UTF8.GetBytes("not json")));
        }
    }
}
