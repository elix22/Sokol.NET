using System;
using System.Numerics;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Renderer.Server.Assets;
using Xunit;

namespace Scene.Tests
{
    /// <summary>A glTF file's lights and cameras as the engine's components: the numbers, and which way they look.</summary>
    public class GltfScenePartsTests
    {
        static void Near(Vector3 want, Vector3 got, float eps = 1e-4f) =>
            Assert.True((want - got).Length() < eps, $"wanted {want}, got {got}");

        [Fact]
        public void ASunsLuxIsBlendersStrength()
        {
            Assert.Equal(3f, GltfSceneParts.SunIntensity(2049f), 4);          // Blender writes strength 3 as 2,049 lux
            Assert.Equal(1f, GltfSceneParts.SunIntensity(683f), 4);
        }

        [Fact]
        public void ALampsCandelaLightsAWallAsItsWattsDo()
        {
            // Blender writes a 1,000 W point light as 683 * 1000 / 4π candela; at a distance d it gives 1000 / (4π² d²).
            float candela = 683f * 1000f / (4f * MathF.PI);
            float intensity = GltfSceneParts.LampIntensity(candela);
            Assert.Equal(1000f / (4f * MathF.PI * MathF.PI), intensity, 3);
            Assert.Equal(1.583f, intensity / (4f * 4f), 2);                    // at 4 m: as a sun of strength 1.58
        }

        [Fact]
        public void ALampsRangeIsTheFilesOrWhereItHasFaded()
        {
            Assert.Equal(7f, GltfSceneParts.Range(7f, 25f));
            Assert.Equal(50f, GltfSceneParts.Range(0f, 25f), 3);              // 25 / d² = 0.01 at d = 50
            Assert.Equal(5f, GltfSceneParts.Range(0f, 0.001f));
            Assert.Equal(100f, GltfSceneParts.Range(0f, 100000f));
        }

        [Fact]
        public void TheEntityLooksDownPlusZWhereTheNodeLooksDownMinusZ()
        {
            // A node that is not turned looks down -Z.
            Near(new Vector3(0, 0, -1), Vector3.Transform(Vector3.UnitZ, GltfSceneParts.Turned(Quaternion.Identity)));
            // Blender's lamp pointing straight down: the node is turned a quarter about X.
            var down = new Quaternion(-0.70710677f, 0f, 0f, 0.70710677f);
            Near(new Vector3(0, -1, 0), Vector3.Transform(-Vector3.UnitZ, down));                       // the file's own word
            Near(new Vector3(0, -1, 0), Vector3.Transform(Vector3.UnitZ, GltfSceneParts.Turned(down))); // the entity's
            // Up stays up: the turn is about the node's own Y.
            Near(Vector3.Transform(Vector3.UnitY, down), Vector3.Transform(Vector3.UnitY, GltfSceneParts.Turned(down)));
        }

        [Fact]
        public void AChildOfATurnedEntityStaysWhereTheFileHasIt()
        {
            var node = Quaternion.CreateFromYawPitchRoll(0.4f, -0.9f, 0.2f);
            Vector3 place = new(1f, 2f, 3f);
            Quaternion turn = Quaternion.CreateFromYawPitchRoll(1.1f, 0.3f, -0.5f);
            Vector3 inWorld = Vector3.Transform(place, node);                  // under the node, as the file has it
            Quaternion facing = node * turn;

            Vector3 p = place;
            Quaternion r = turn;
            GltfSceneParts.UnderTurned(ref p, ref r);
            Quaternion entity = GltfSceneParts.Turned(node);
            Near(inWorld, Vector3.Transform(p, entity));
            Near(Vector3.Transform(Vector3.UnitX, facing), Vector3.Transform(Vector3.UnitX, entity * r));
            Near(Vector3.Transform(Vector3.UnitZ, facing), Vector3.Transform(Vector3.UnitZ, entity * r));
        }

        [Fact]
        public void ALightsKindColourAndAngles()
        {
            var sun = GltfSceneParts.Light(0, new Vector3(1f, 0.9f, 0.8f), 2049f, 0f, 0f, 0f);
            Assert.Equal(LightType.Directional, sun.Type);
            Assert.Equal(3f, sun.Intensity, 4);
            Assert.Equal(new Vector3(1f, 0.9f, 0.8f), sun.Color);
            Assert.True(sun.CastsShadows && sun.Enabled);

            var lamp = GltfSceneParts.Light(1, Vector3.One, 683f * MathF.PI * 2f, 0f, 0f, 0f);
            Assert.Equal(LightType.Point, lamp.Type);
            Assert.Equal(2f, lamp.Intensity, 4);
            Assert.False(lamp.CastsShadows);

            var spot = GltfSceneParts.Light(2, Vector3.One, 683f * MathF.PI, 12f, 0.41887903f, 0.5235988f);
            Assert.Equal(LightType.Spot, spot.Type);
            Assert.Equal(12f, spot.Range);
            Assert.Equal(24f, spot.InnerAngle, 3);
            Assert.Equal(30f, spot.OuterAngle, 3);
        }

        [Fact]
        public void ACamerasAngleAndDepths()
        {
            var seen = GltfSceneParts.Camera(false, 0.39959648f, 0.1f, 100f);  // Blender's 50 mm lens
            Assert.Equal(22.895f, seen.Fov, 2);
            Assert.Equal(0.1f, seen.NearZ);
            Assert.Equal(100f, seen.FarZ);
            Assert.False(seen.IsMain);
            Assert.True(seen.Enabled && !seen.IsOrthographic);

            Assert.Equal(1000f, GltfSceneParts.Camera(false, 1f, 0.1f, 0f).FarZ);   // the file gives no far depth
            var flat = GltfSceneParts.Camera(true, 3.5f, 0.1f, 50f);
            Assert.True(flat.IsOrthographic);
            Assert.Equal(3.5f, flat.OrthoSize);
        }
    }
}
