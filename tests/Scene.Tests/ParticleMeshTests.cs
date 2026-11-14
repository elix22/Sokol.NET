using System.Numerics;
using Frent;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Scene;
using Xunit;

namespace Scene.Tests
{
    /// <summary>Mesh particles (G8.8 Q6, examples/GameEditor/docs/PARTICLES_3D_DESIGN.md §6.7): each a model matrix - its size the
    /// scale, its rotation about its own Y, along its velocity when aligned so - and its colour the tint; the block.</summary>
    public sealed class ParticleMeshTests : IDisposable
    {
        static readonly ECSWorld World = ECSWorld.Instance;

        public ParticleMeshTests()
        {
            World.Clear();
            WorldParticles.Clear();
            SceneManager.NewScene("Meshes");
        }

        public void Dispose()
        {
            WorldParticles.Clear();
            World.Clear();
        }

        static Entity Emitter(ParticlesComponent p, Vector3 at = default)
        {
            Entity e = SceneManager.ActiveScene!.CreateEntity("Emitter");
            var t = World.GetComponent<Transform>(e);
            t.Position = at;
            World.AddComponent(e, t);
            World.AddComponent(e, p);
            return e;
        }

        static ParticlesComponent Cubes()
        {
            var p = ParticlesComponent.Base;
            p.Rate = 0f; p.Bursts.Add(ParticleBurst.At(0f, 4)); p.Speed = new Vector2(2f, 2f); p.Direction = Vector3.UnitX; p.Spread = 0f; p.Radius = 0f;
            p.World = true; p.Life = new Vector2(10f, 10f); p.StartSize = new Vector2(0.5f, 0.5f); p.StartRotation = new Vector2(90f, 90f);
            p.StartColor = p.StartColor2 = new Vector4(1f, 0.5f, 0.25f, 1f);
            p.RenderMode = ParticleRenderMode.Mesh; p.MeshKey = "primitive:box"; p.MeshMaterial = "Materials/Red.pbrmat";
            return p;
        }

        [Fact]
        public void Each_mesh_particle_is_its_size_its_rotation_its_place_and_its_colour()
        {
            Entity e = Emitter(Cubes(), new Vector3(1, 2, 3));
            WorldParticles.Step(World, 0.5f);
            Assert.Equal(1, WorldParticles.GatherMeshes());
            var batch = WorldParticles.MeshBatches[0];
            Assert.Equal(("primitive:box", "Materials/Red.pbrmat", 4, true), (batch.Mesh, batch.Material, batch.Count, batch.CastShadows));
            var m = WorldParticles.Meshes[batch.Start];
            Vector3 x = new(m.Model.M11, m.Model.M12, m.Model.M13);
            Assert.Equal(0.5f, x.Length(), 4);                                  // the size
            Assert.True(Vector3.Distance(Vector3.Normalize(x), new Vector3(0, 0, -1)) < 1e-4f, x.ToString());   // 90 degrees about Y: +X to -Z
            Assert.Equal(new Vector4(1f, 0.5f, 0.25f, 1f), m.Tint);
            var pos = new Vector3[4];
            WorldParticles.CopyPositions(e, pos);
            Assert.Equal(pos[0], m.Model.Translation);
        }

        [Fact]
        public void Aligned_with_the_velocity_a_mesh_s_z_points_where_it_goes_and_billboards_give_no_meshes()
        {
            var p = Cubes(); p.MeshAlignment = ParticleMeshAlignment.Velocity; p.StartRotation = Vector2.Zero; p.Direction = new Vector3(1, 1, 0);
            Emitter(p);
            var b = Cubes(); b.RenderMode = ParticleRenderMode.Billboard;
            Emitter(b, new Vector3(0, 5, 0));
            WorldParticles.Step(World, 0.1f);
            Assert.Equal(1, WorldParticles.GatherMeshes());
            var m = WorldParticles.Meshes[0].Model;
            Vector3 z = Vector3.Normalize(new Vector3(m.M31, m.M32, m.M33));
            Assert.True(Vector3.Dot(z, Vector3.Normalize(new Vector3(1, 1, 0))) > 0.9999f, z.ToString());
        }

        [Fact]
        public void The_mesh_fields_and_Lit_are_written_and_read_back()
        {
            var p = Cubes(); p.MeshAlignment = ParticleMeshAlignment.Velocity; p.MeshCastShadows = false; p.Lit = true;
            Emitter(p);
            string json = SceneSerializer.Serialize(SceneManager.ActiveScene!);
            World.Clear();
            SceneSerializer.Deserialize(json, SceneManager.ActiveScene!);
            ParticlesComponent read = default;
            foreach (Entity e in World.Entities)
                if (World.TryGetComponent<ParticlesComponent>(e, out var got)) read = got;
            Assert.Equal(p, read);
        }
    }
}
