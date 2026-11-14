using System;
using System.Collections.Generic;
using System.Numerics;
using Frent;
using GameEditor.Framework.Core;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Renderer.Server;
using GameEditor.Framework.Renderer.Server.Materials;
using GameEditor.Framework.Renderer.Server.Materials.Graph;
using GameEditor.Framework.Scene;

namespace GameEditor.Framework.Scripting
{
    /// <summary>
    /// A script sets a shader graph material's properties (G8.10, docs SHADER_GRAPH_DESIGN.md §3.3): the material the
    /// entity's MeshRenderer draws with - shared by every entity that uses it (the reference editor's sharedMaterial) - used
    /// at the next frame, no allocation once its name is known. In the editor, Stop puts back the values of before Play.
    /// </summary>
    public static class MaterialProperties
    {
        private static readonly Dictionary<GraphBinding, Dictionary<string, Vector4?>> _beforePlay = new();

        static MaterialProperties()
        {
            EventBus.PlayModeChanged += state =>
            {
                if (state != PlayModeState.Stopped) return;
                foreach (var (binding, values) in _beforePlay)
                    foreach (var (name, value) in values)
                    {
                        if (value is { } v) binding.Set(name, v);
                        else { binding.Values.Remove(name); binding.Set(name, binding.Get(name)); }
                    }
                _beforePlay.Clear();
            };
        }

        /// <summary>The graph binding of the entity's material, or null (no MeshRenderer, a material without a graph).</summary>
        public static GraphBinding? Of(Entity entity)
        {
            var world = ECSWorld.Instance;
            if (entity.IsNull || !world.TryGetComponent<MeshRenderer>(entity, out var mr) || string.IsNullOrEmpty(mr.MaterialPath)) return null;
            return RenderingServer.Materials.GetByKey(mr.MaterialPath) is PbrMaterial { Graph: { } g } ? g : null;
        }

        /// <summary>Sets a float property; false when the entity's material has no graph property of that name.</summary>
        public static bool SetFloat(Entity entity, string name, float value) => Set(entity, name, new Vector4(value, 0f, 0f, 0f));

        /// <summary>Sets a vector property (a Vector2 / Vector3 property takes its first components).</summary>
        public static bool SetVector(Entity entity, string name, Vector4 value) => Set(entity, name, value);

        /// <summary>Sets a colour property (r, g, b, a).</summary>
        public static bool SetColor(Entity entity, string name, Vector4 rgba) => Set(entity, name, rgba);

        /// <summary>A property's value now (the material's, else the graph's default); zero when there is none.</summary>
        public static Vector4 Get(Entity entity, string name) => Of(entity)?.Get(name) ?? Vector4.Zero;

        private static bool Set(Entity entity, string name, Vector4 value)
        {
            if (Of(entity) is not { } g) return false;
            if (SceneManager.PlayMode != PlayModeState.Stopped)
            {
                if (!_beforePlay.TryGetValue(g, out var saved)) _beforePlay[g] = saved = new Dictionary<string, Vector4?>(StringComparer.Ordinal);
                if (!saved.ContainsKey(name)) saved[name] = g.Values.TryGetValue(name, out var was) ? was : null;
            }
            return g.Set(name, value);
        }
    }
}
