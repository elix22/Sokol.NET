using GameEditor.Framework.Assets;
using Xunit;

namespace Scene.Tests
{
    /// <summary>
    /// A Blender file under Assets is a model with its own key; what is read is the GLB an editor exported beside it.
    /// The GLB and Blender's backups are not assets of their own.
    /// </summary>
    public sealed class BlendKeyTests
    {
        [Theory]
        [InlineData("Models/ship.blend", "Models/ship.blend.glb")]
        [InlineData("Models/SHIP.BLEND", "Models/SHIP.BLEND.glb")]
        [InlineData("ship.blend", "ship.blend.glb")]
        [InlineData("Models/ship.glb", "Models/ship.glb")]
        [InlineData("Models/ship.gltf", "Models/ship.gltf")]
        [InlineData("Models/duck.obj", "Models/duck.obj")]
        [InlineData("Models/ship.blend.glb", "Models/ship.blend.glb")]   // the export itself, asked for by name
        public void A_model_is_read_from_its_own_file_and_a_blend_from_the_glb_beside_it(string key, string file)
            => Assert.Equal(file, AssetLibrary.FileOf(key));

        [Theory]
        [InlineData("Models/ship.blend", true)]
        [InlineData("Models/ship.Blend", true)]
        [InlineData("Models/ship.glb", true)]
        [InlineData("Models/ship.gltf", true)]
        [InlineData("Models/duck.obj", false)]
        [InlineData("Models/ship.blend1", false)]
        [InlineData("Models/blend", false)]
        public void A_blend_is_a_gltf_model(string key, bool gltf) => Assert.Equal(gltf, AssetLibrary.IsGltf(key));

        [Theory]
        [InlineData("ship.blend.glb", true)]
        [InlineData("Models/ship.BLEND.GLB", true)]
        [InlineData("ship.blend1", true)]
        [InlineData("ship.blend32", true)]
        [InlineData("ship.blend", false)]
        [InlineData("ship.glb", false)]
        [InlineData("blend1", false)]          // no name before the ending is still Blender's pattern only with ".blend"
        [InlineData("ship.blender1", false)]
        [InlineData("level1", false)]
        [InlineData("1", false)]
        [InlineData("", false)]
        public void The_export_and_the_backups_beside_a_blend_are_told_apart(string name, bool beside)
            => Assert.Equal(beside, AssetLibrary.IsBesideBlend(name));

        [Fact]
        public void Only_the_blend_is_an_asset_with_an_id()
        {
            Assert.True(AssetDatabase.IsAssetPath("Assets/Models/ship.blend"));
            Assert.False(AssetDatabase.IsAssetPath("Assets/Models/ship.blend.glb"));
            Assert.False(AssetDatabase.IsAssetPath("Assets/Models/ship.blend1"));
            Assert.True(AssetDatabase.IsAssetPath("Assets/Models/ship.glb"));
        }
    }
}
