using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;

namespace GameEditor.Framework.Scene
{
    /// <summary>
    /// Serializes/deserializes scenes to/from a simple JSON format.
    /// Uses manual StringBuilder for writing (AOT-safe) and JsonDocument for reading.
    /// </summary>
    public static class SceneSerializer
    {
        public static string Serialize(Scene scene)
        {
            var world = ECSWorld.Instance;
            var sb = new StringBuilder();
            sb.Append("{\"name\":\"").Append(Esc(scene.Name)).Append("\",\"entities\":[");

            bool firstEntity = true;
            foreach (int id in world.Entities)
            {
                if (!firstEntity) sb.Append(',');
                firstEntity = false;

                sb.Append("{\"id\":").Append(id).Append(",\"c\":{");
                bool fc = true;

                if (world.TryGetComponent<NameTag>(id, out var nt))
                {
                    Comma(sb, ref fc);
                    sb.Append("\"NameTag\":{\"Name\":\"").Append(Esc(nt.Name)).Append("\"}");
                }

                if (world.TryGetComponent<ActiveFlag>(id, out var af))
                {
                    Comma(sb, ref fc);
                    sb.Append("\"ActiveFlag\":{\"Active\":").Append(af.Active ? "true" : "false").Append('}');
                }

                if (world.TryGetComponent<Transform>(id, out var tr))
                {
                    Comma(sb, ref fc);
                    sb.Append("\"Transform\":{\"P\":").Append(V3(tr.Position))
                      .Append(",\"E\":").Append(V3(tr.EulerAngles))
                      .Append(",\"S\":").Append(V3(tr.Scale));
                    if (tr.Parent.HasValue) sb.Append(",\"Par\":").Append(tr.Parent.Value);
                    sb.Append('}');
                }

                if (world.TryGetComponent<MeshRenderer>(id, out var mr))
                {
                    Comma(sb, ref fc);
                    sb.Append("\"MeshRenderer\":{\"Path\":\"").Append(Esc(mr.MeshPath ?? ""))
                      .Append("\",\"Vis\":").Append(mr.Visible ? "true" : "false").Append('}');
                }

                if (world.TryGetComponent<CameraComponent>(id, out var cam))
                {
                    Comma(sb, ref fc);
                    sb.Append("\"Camera\":{\"Fov\":").Append(F(cam.Fov))
                      .Append(",\"Near\":").Append(F(cam.NearZ))
                      .Append(",\"Far\":").Append(F(cam.FarZ))
                      .Append(",\"Main\":").Append(cam.IsMain ? "true" : "false")
                      .Append(",\"Ortho\":").Append(cam.IsOrthographic ? "true" : "false")
                      .Append(",\"OrthoSize\":").Append(F(cam.OrthoSize)).Append('}');
                }

                if (world.TryGetComponent<LightComponent>(id, out var lt))
                {
                    Comma(sb, ref fc);
                    sb.Append("\"Light\":{\"Type\":\"").Append(lt.Type.ToString())
                      .Append("\",\"Color\":").Append(V3(lt.Color))
                      .Append(",\"Int\":").Append(F(lt.Intensity))
                      .Append(",\"Range\":").Append(F(lt.Range))
                      .Append(",\"Inner\":").Append(F(lt.InnerAngle))
                      .Append(",\"Outer\":").Append(F(lt.OuterAngle)).Append('}');
                }

                if (world.TryGetComponent<RigidbodyComponent>(id, out var rb))
                {
                    Comma(sb, ref fc);
                    sb.Append("\"Rigidbody\":{\"Static\":").Append(rb.IsStatic ? "true" : "false")
                      .Append(",\"Mass\":").Append(F(rb.Mass))
                      .Append(",\"Gravity\":").Append(rb.UseGravity ? "true" : "false").Append('}');
                }

                if (world.TryGetComponent<ScriptComponent>(id, out var sc))
                {
                    Comma(sb, ref fc);
                    sb.Append("\"Script\":{\"Type\":\"").Append(Esc(sc.TypeName ?? "")).Append('"');
                    if (sc.Properties != null && sc.Properties.Count > 0)
                    {
                        sb.Append(",\"Props\":{");
                        bool fp = true;
                        foreach (var kvp in sc.Properties)
                        {
                            if (!fp) sb.Append(',');
                            fp = false;
                            sb.Append('"').Append(Esc(kvp.Key)).Append("\":\"").Append(Esc(kvp.Value)).Append('"');
                        }
                        sb.Append('}');
                    }
                    sb.Append('}');
                }

                if (world.TryGetComponent<ScriptCollectionComponent>(id, out var scc)
                    && scc.Scripts != null && scc.Scripts.Count > 0)
                {
                    Comma(sb, ref fc);
                    sb.Append("\"Scripts\":[");
                    bool firstScript = true;
                    foreach (var sc2 in scc.Scripts)
                    {
                        if (!firstScript) sb.Append(',');
                        firstScript = false;
                        sb.Append("{\"Type\":\"").Append(Esc(sc2.TypeName ?? "")).Append('"');
                        if (sc2.Properties != null && sc2.Properties.Count > 0)
                        {
                            sb.Append(",\"Props\":{");
                            bool fp = true;
                            foreach (var kvp in sc2.Properties)
                            {
                                if (!fp) sb.Append(',');
                                fp = false;
                                sb.Append('"').Append(Esc(kvp.Key)).Append("\":\"").Append(Esc(kvp.Value)).Append('"');
                            }
                            sb.Append('}');
                        }
                        sb.Append('}');
                    }
                    sb.Append(']');
                }

                sb.Append("}}"); // close components + entity
            }

            sb.Append("]}"); // close entities + root
            return sb.ToString();
        }

        public static void Deserialize(string json, Scene scene)
        {
            scene.Clear();

            JsonDocument doc;
            try { doc = JsonDocument.Parse(json); }
            catch { return; }

            var root = doc.RootElement;
            if (root.TryGetProperty("name", out var nameProp))
                scene.Name = nameProp.GetString() ?? scene.Name;

            if (!root.TryGetProperty("entities", out var entitiesEl))
                return;

            var world = ECSWorld.Instance;
            // Map from saved-ID → new runtime ID (for parent fixup)
            var idMap = new Dictionary<int, int>();

            // First pass — create entities and load all components
            foreach (var eEl in entitiesEl.EnumerateArray())
            {
                int savedId = eEl.TryGetProperty("id", out var idEl) ? idEl.GetInt32() : 0;
                int newId = world.CreateEntity(); // auto-adds NameTag/ActiveFlag/Transform
                idMap[savedId] = newId;

                if (!eEl.TryGetProperty("c", out var c)) continue;

                if (c.TryGetProperty("NameTag", out var nEl))
                    world.AddComponent(newId, new NameTag { Name = nEl.GetProperty("Name").GetString() ?? "Entity" });

                if (c.TryGetProperty("ActiveFlag", out var aEl))
                    world.AddComponent(newId, new ActiveFlag { Active = aEl.GetProperty("Active").GetBoolean() });

                if (c.TryGetProperty("Transform", out var tEl))
                {
                    int? savedParent = tEl.TryGetProperty("Par", out var pEl) ? pEl.GetInt32() : null;
                    world.AddComponent(newId, new Transform
                    {
                        Position    = RV3(tEl, "P"),
                        EulerAngles = RV3(tEl, "E"),
                        Scale       = RV3(tEl, "S"),
                        Parent      = savedParent  // fixed up in second pass
                    });
                }

                if (c.TryGetProperty("MeshRenderer", out var mrEl))
                    world.AddComponent(newId, new MeshRenderer
                    {
                        MeshPath = mrEl.GetProperty("Path").GetString() ?? "",
                        Visible  = mrEl.GetProperty("Vis").GetBoolean()
                    });

                if (c.TryGetProperty("Camera", out var camEl))
                    world.AddComponent(newId, new CameraComponent
                    {
                        Fov            = camEl.GetProperty("Fov").GetSingle(),
                        NearZ          = camEl.GetProperty("Near").GetSingle(),
                        FarZ           = camEl.GetProperty("Far").GetSingle(),
                        IsMain         = camEl.GetProperty("Main").GetBoolean(),
                        IsOrthographic = camEl.TryGetProperty("Ortho", out var orthoEl) && orthoEl.GetBoolean(),
                        OrthoSize      = camEl.TryGetProperty("OrthoSize", out var osEl) ? osEl.GetSingle() : 5f
                    });

                if (c.TryGetProperty("Light", out var ltEl))
                    world.AddComponent(newId, new LightComponent
                    {
                        Type       = Enum.Parse<LightType>(ltEl.GetProperty("Type").GetString()!),
                        Color      = RV3(ltEl, "Color"),
                        Intensity  = ltEl.GetProperty("Int").GetSingle(),
                        Range      = ltEl.GetProperty("Range").GetSingle(),
                        InnerAngle = ltEl.TryGetProperty("Inner", out var innerEl) ? innerEl.GetSingle() : 25f,
                        OuterAngle = ltEl.TryGetProperty("Outer", out var outerEl) ? outerEl.GetSingle() : 35f
                    });

                if (c.TryGetProperty("Rigidbody", out var rbEl))
                    world.AddComponent(newId, new RigidbodyComponent
                    {
                        IsStatic   = rbEl.GetProperty("Static").GetBoolean(),
                        Mass       = rbEl.GetProperty("Mass").GetSingle(),
                        UseGravity = rbEl.GetProperty("Gravity").GetBoolean()
                    });

                if (c.TryGetProperty("Script", out var sEl))
                {
                    var sc = new ScriptComponent
                    {
                        TypeName = sEl.GetProperty("Type").GetString() ?? ""
                    };
                    if (sEl.TryGetProperty("Props", out var propsEl))
                    {
                        sc.Properties = new Dictionary<string, string>();
                        foreach (var p in propsEl.EnumerateObject())
                            sc.Properties[p.Name] = p.Value.GetString() ?? "";
                    }
                    world.AddComponent(newId, sc);
                }

                if (c.TryGetProperty("Scripts", out var saEl) && saEl.ValueKind == JsonValueKind.Array)
                {
                    var list = new List<ScriptComponent>();
                    foreach (var sxEl in saEl.EnumerateArray())
                    {
                        var sc = new ScriptComponent
                        {
                            TypeName = sxEl.TryGetProperty("Type", out var txEl) ? (txEl.GetString() ?? "") : ""
                        };
                        if (sxEl.TryGetProperty("Props", out var propsEl) && propsEl.ValueKind == JsonValueKind.Object)
                        {
                            sc.Properties = new Dictionary<string, string>();
                            foreach (var p in propsEl.EnumerateObject())
                                sc.Properties[p.Name] = p.Value.GetString() ?? "";
                        }
                        list.Add(sc);
                    }
                    if (list.Count > 0)
                        world.AddComponent(newId, new ScriptCollectionComponent { Scripts = list });
                }
            }

            // Second pass — remap parent IDs
            foreach (int newId in world.Entities)
            {
                if (!world.TryGetComponent<Transform>(newId, out var tr) || !tr.Parent.HasValue) continue;
                if (idMap.TryGetValue(tr.Parent.Value, out int remapped) && remapped != tr.Parent.Value)
                {
                    tr.Parent = remapped;
                    world.AddComponent(newId, tr);
                }
            }
        }

        // ── Helpers ─────────────────────────────────────────────────────────

        private static void Comma(StringBuilder sb, ref bool first)
        {
            if (!first) sb.Append(',');
            first = false;
        }

        private static string F(float v) => v.ToString("G", CultureInfo.InvariantCulture);

        private static string V3(Vector3 v) => $"[{F(v.X)},{F(v.Y)},{F(v.Z)}]";

        private static Vector3 RV3(JsonElement parent, string prop)
        {
            if (!parent.TryGetProperty(prop, out var el)) return Vector3.Zero;
            float x = 0, y = 0, z = 0;
            int i = 0;
            foreach (var n in el.EnumerateArray())
            {
                if      (i == 0) x = n.GetSingle();
                else if (i == 1) y = n.GetSingle();
                else if (i == 2) z = n.GetSingle();
                i++;
            }
            return new Vector3(x, y, z);
        }

        private static string Esc(string s) =>
            s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
    }
}
