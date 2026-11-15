using System;
using System.Text;
using System.Text.Json;
using Frent;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Scene;

namespace GameEditor.Framework.Diagnostics
{
    /// <summary>
    /// The running game's scene for an attached editor (G11.5, the GameEditor's docs/QOL_DESIGN.md; Godot's remote scene
    /// tree): its entities - scene id, parent, name, active - sent when asked and again when they change (checked 4 times a
    /// second); one entity's components (the scene file's own format, <see cref="SceneSerializer.SerializeEntity"/>); a
    /// change from the editor applied with <see cref="SceneSerializer.ReadComponents"/> - no reflection, as a scene loads.
    /// </summary>
    internal static class RemoteScene
    {
        static bool _watching;
        static ulong _lastHash;
        static double _nextCheck;
        static int _watchedEntity = -1;
        static ulong _lastEntityHash;

        internal static void Link()
        {
            PlayerLink.Received += OnLine;
            PlayerLink.Frame += Tick;
        }

        static void OnLine(string t, string line)
        {
            switch (t)
            {
                case "tree":
                    _watching = true; _lastHash = 0;   // sent at the next check
                    break;
                case "untree":
                    _watching = false; _watchedEntity = -1;
                    break;
                case "get":
                    using (var doc = JsonDocument.Parse(line))
                    {
                        _watchedEntity = doc.RootElement.GetProperty("id").GetInt32();
                        _lastEntityHash = 0;
                    }
                    break;
                case "set":
                    Set(line);
                    break;
            }
        }

        static void Set(string line)
        {
            int id = -1;
            string? error = null;
            try
            {
                using var doc = JsonDocument.Parse(line);
                id = doc.RootElement.GetProperty("id").GetInt32();
                if (!ECSWorld.Instance.TryGetEntityBySceneId(id, out Entity e)) error = $"no entity {id} in the running scene";
                else SceneSerializer.ReadComponents(e, doc.RootElement.GetProperty("c"));
            }
            catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException or FormatException or System.Collections.Generic.KeyNotFoundException)
            {
                error = ex.Message;
            }
            PlayerLink.Send("{\"t\":\"set\",\"id\":" + id + ",\"ok\":" + (error == null ? "true" : "false") + (error != null ? ",\"error\":" + PlayerLink.Str(error) : "") + "}");
            _lastEntityHash = 0;   // its values again
            _lastHash = 0;         // a name or a parent may have changed
        }

        // Once a frame, on the game's thread: the tree and the watched entity, when they changed.
        static void Tick()
        {
            if (!_watching || !PlayerLink.Connected) return;
            double now = Environment.TickCount64 / 1000.0;
            if (now < _nextCheck) return;
            _nextCheck = now + 0.25;
            var world = ECSWorld.Instance;
            string tree = Tree(world);
            ulong h = Hash(tree);
            if (h != _lastHash) { _lastHash = h; PlayerLink.Send(tree); }
            if (_watchedEntity >= 0)
            {
                string body = world.TryGetEntityBySceneId(_watchedEntity, out Entity e) ? SceneSerializer.SerializeEntity(e) : "null";
                ulong eh = Hash(body);
                if (eh != _lastEntityHash)
                {
                    _lastEntityHash = eh;
                    PlayerLink.Send("{\"t\":\"entity\",\"id\":" + _watchedEntity + ",\"e\":" + body + "}");
                }
            }
        }

        static string Tree(ECSWorld world)
        {
            var sb = new StringBuilder("{\"t\":\"tree\",\"e\":[");
            bool first = true;
            foreach (var row in world.Query<NameTag, Transform>().EnumerateWithEntities<NameTag, Transform>())
            {
                Entity e = row.Entity;
                int id = world.GetSceneId(e);
                int parent = row.Item2.Value.Parent is { } par && par.IsAlive ? world.GetSceneId(par) : 0;
                string name = row.Item1.Value.Name ?? "";
                bool active = !world.TryGetComponent<ActiveFlag>(e, out var a) || a.Active;
                if (!first) sb.Append(',');
                first = false;
                sb.Append('[').Append(id).Append(',').Append(parent).Append(',').Append(PlayerLink.Str(name)).Append(',').Append(active ? 1 : 0).Append(']');
            }
            return sb.Append("]}").ToString();
        }

        static ulong Hash(string s)
        {
            ulong h = 14695981039346656037UL;
            foreach (char c in s) h = (h ^ c) * 1099511628211UL;
            return h;
        }
    }
}
