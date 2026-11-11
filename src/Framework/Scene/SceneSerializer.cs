using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Frent;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Physics;
using GameEditor.Framework.Renderer.Server.Animation;

namespace GameEditor.Framework.Scene
{
    /// <summary>
    /// Serializes/deserializes scenes to/from a simple JSON format.
    /// Uses manual StringBuilder for writing (AOT-safe) and JsonDocument for reading.
    /// </summary>
    public static class SceneSerializer
    {
        // Component keys written/read by this file. Keep in sync when adding a built-in component:
        // any other key is handed to a registered extension or kept as a PreservedComponents block.
        private static readonly HashSet<string> BuiltInKeys = new(StringComparer.Ordinal)
        {
            "NameTag", "ActiveFlag", "Transform", "MeshRenderer", "SkinnedMeshRenderer", "Camera", "Light",
            "Rigidbody", "Script", "Scripts", "Constraint", "Character", "Vehicle", "WheelFollow",
        };

        // Root keys written/read by this file; any other root key is a Scene.Blocks entry.
        private static readonly HashSet<string> RootKeys = new(StringComparer.Ordinal) { "name", "physics", "entities", "nextId" };

        private static readonly Dictionary<string, (Func<ECSWorld, Entity, string?> Write,
                                                    Action<ECSWorld, Entity, JsonElement, Func<int, Entity>> Read)>
            _extensions = new(StringComparer.Ordinal);

        /// <summary>
        /// Adds a component block outside the built-in set (e.g. a plugin's components). Register at
        /// startup, before any scene loads.
        /// <paramref name="write"/> returns the block's JSON value for an entity, or null when the entity
        /// has none; it writes entity references as <see cref="ECSWorld.GetSceneId"/>, like the built-in ones.
        /// <paramref name="read"/> runs after every entity of the scene exists; its resolver maps a saved
        /// entity id to the loaded entity, or <see cref="Entity.Null"/> when no entity has that id (deleted,
        /// or never existed) — the reader skips such a reference.
        /// In a build without the extension the block is kept verbatim (<see cref="PreservedComponents"/>).
        /// Entities keep their scene ids through load and save, so references inside it stay valid.
        /// </summary>
        public static void RegisterComponentExtension(string key,
            Func<ECSWorld, Entity, string?> write,
            Action<ECSWorld, Entity, JsonElement, Func<int, Entity>> read)
        {
            if (BuiltInKeys.Contains(key))
                throw new ArgumentException($"'{key}' is a built-in component key.", nameof(key));
            _extensions[key] = (write, read);
        }

        public static string Serialize(Scene scene)
        {
            var world = ECSWorld.Instance;
            var sb = new StringBuilder();
            sb.Append("{\"name\":\"").Append(Esc(scene.Name)).Append('"');
            if (scene.PhysicsBackend != null)
                sb.Append(",\"physics\":\"").Append(Esc(scene.PhysicsBackend)).Append('"');
            sb.Append(",\"entities\":[");

            WriteEntities(sb, world, world.Entities);

            sb.Append(']'); // close entities
            // Ids are never reused within a scene: the counter travels with it (absent = 1, an empty new scene).
            if (world.NextSceneId > 1) sb.Append(",\"nextId\":").Append(world.NextSceneId);
            foreach (var kvp in scene.Blocks)
            {
                if (RootKeys.Contains(kvp.Key)) continue;
                sb.Append(",\"").Append(Esc(kvp.Key)).Append("\":").Append(kvp.Value);
            }
            sb.Append('}'); // close root
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
            scene.PhysicsBackend = root.TryGetProperty("physics", out var physicsProp) ? physicsProp.GetString() : null;

            var world = ECSWorld.Instance;
            if (root.TryGetProperty("nextId", out var nextIdEl) && nextIdEl.TryGetInt32(out int nextId))
                world.NextSceneId = nextId;
            foreach (var prop in root.EnumerateObject())
                if (!RootKeys.Contains(prop.Name)) scene.Blocks[prop.Name] = prop.Value.GetRawText();

            if (!root.TryGetProperty("entities", out var entitiesEl))
                return;

            // Map from saved-ID (int) → new runtime Entity (for parent fixup)
            var idMap = new Dictionary<int, Entity>();
            // References to other entities are saved ids: resolved once every entity exists
            var fx = new Fixups();

            // First pass — create entities and load all components
            foreach (var eEl in entitiesEl.EnumerateArray())
            {
                int savedId = eEl.TryGetProperty("id", out var idEl) ? idEl.GetInt32() : 0;
                Entity newId = world.CreateEntity(); // auto-adds NameTag/ActiveFlag/Transform
                idMap.TryAdd(savedId, newId);
                world.SetSceneId(newId, savedId);    // keeps the saved id (a duplicate or missing one gets a fresh id)

                if (!eEl.TryGetProperty("c", out var c)) continue;

                ReadBuiltIns(world, newId, c, fx);

                Dictionary<string, string>? preserved = null;
                foreach (var prop in c.EnumerateObject())
                {
                    if (BuiltInKeys.Contains(prop.Name)) continue;
                    if (_extensions.ContainsKey(prop.Name))
                        fx.Extensions.Add((newId, prop.Name, prop.Value));
                    else
                        (preserved ??= new Dictionary<string, string>())[prop.Name] = prop.Value.GetRawText();
                }
                if (preserved != null)
                    world.AddComponent(newId, new PreservedComponents { Blocks = preserved });
            }

            ApplyFixups(world, fx, savedId => idMap.TryGetValue(savedId, out Entity e) ? e : Entity.Null);
        }

        /// <summary>One entity as the scene file writes it: <c>{"id":n,"c":{…}}</c>.</summary>
        public static string SerializeEntity(Entity entity)
        {
            var sb = new StringBuilder();
            WriteEntities(sb, ECSWorld.Instance, new List<Entity> { entity });
            return sb.ToString();
        }

        /// <summary>True for a component key this build reads: a built-in one or a registered extension's.</summary>
        public static bool IsComponentKey(string key) => BuiltInKeys.Contains(key) || _extensions.ContainsKey(key);

        /// <summary>
        /// Reads component blocks in the scene-file format (the value of an entity's "c") onto an existing
        /// entity of the open scene. Each block given replaces that component whole; components whose key is
        /// absent are left alone. References to other entities are scene ids (<see cref="ECSWorld.GetSceneId"/>);
        /// one that names no live entity is skipped, as on load.
        /// Throws on a key that is not a component (<see cref="IsComponentKey"/>) and on a malformed block —
        /// the entity may then be half-written, so capture it first (<see cref="ECSWorld.Capture"/>).
        /// </summary>
        public static void ReadComponents(Entity entity, JsonElement components)
        {
            foreach (var prop in components.EnumerateObject())
                if (!IsComponentKey(prop.Name)) throw new ArgumentException($"unknown component '{prop.Name}'");
            var world = ECSWorld.Instance;
            var fx = new Fixups();
            ReadBuiltIns(world, entity, components, fx);
            foreach (var prop in components.EnumerateObject())
                if (_extensions.ContainsKey(prop.Name)) fx.Extensions.Add((entity, prop.Name, prop.Value));
            ApplyFixups(world, fx, id => world.TryGetEntityBySceneId(id, out Entity e) ? e : Entity.Null);
        }

        private static void WriteEntities(StringBuilder sb, ECSWorld world, List<Entity> entities)
        {
            bool firstEntity = true;
            foreach (Entity id in entities)
            {
                if (!firstEntity) sb.Append(',');
                firstEntity = false;

                sb.Append("{\"id\":").Append(world.GetSceneId(id)).Append(",\"c\":{");
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
                    // "E" (degrees) is for people and older readers; "Q" is the exact rotation, read first —
                    // Euler degrees in float drift a rotation by ~1e-7 per save/load (and per Play → Stop).
                    sb.Append("\"Transform\":{\"P\":").Append(V3(tr.Position))
                      .Append(",\"E\":").Append(V3(tr.EulerAngles))
                      .Append(",\"Q\":").Append(Q4(tr.Rotation))
                      .Append(",\"S\":").Append(V3(tr.Scale));
                    if (tr.Parent.HasValue) sb.Append(",\"Par\":").Append(world.GetSceneId(tr.Parent.Value));
                    sb.Append('}');
                }

                if (world.TryGetComponent<MeshRenderer>(id, out var mr))
                {
                    Comma(sb, ref fc);
                    sb.Append("\"MeshRenderer\":{\"Path\":\"").Append(Esc(mr.MeshPath ?? ""))
                        .Append("\",\"Mat\":\"").Append(Esc(mr.MaterialPath ?? ""))
                                            .Append("\",\"Vis\":").Append(mr.Visible ? "true" : "false")
                                            .Append(",\"RecvSh\":").Append(mr.ReceivesShadows ? "true" : "false")
                                            .Append(",\"CastSh\":").Append(mr.CastsShadows ? "true" : "false").Append('}');
                }

                // Skinned characters hold only registry keys (live GPU + animator live in
                // SkinnedCharacterRegistry), so they serialize like any other component and survive
                // the Play→Stop snapshot — the registry persists and the keys re-resolve on load.
                if (world.TryGetComponent<SkinnedMeshRenderer>(id, out var skmr))
                {
                    Comma(sb, ref fc);
                    sb.Append("\"SkinnedMeshRenderer\":{\"Key\":\"").Append(Esc(skmr.CharacterKey ?? ""))
                        .Append("\",\"Prim\":").Append(skmr.PrimIndex)
                        .Append(",\"Mat\":\"").Append(Esc(skmr.MaterialKey ?? ""))
                        .Append("\",\"Vis\":").Append(skmr.Visible ? "true" : "false")
                        .Append(",\"RecvSh\":").Append(skmr.ReceivesShadows ? "true" : "false")
                        .Append(",\"CastSh\":").Append(skmr.CastsShadows ? "true" : "false").Append('}');
                }

                if (world.TryGetComponent<CameraComponent>(id, out var cam))
                {
                    Comma(sb, ref fc);
                    sb.Append("\"Camera\":{\"Fov\":").Append(F(cam.Fov))
                      .Append(",\"Near\":").Append(F(cam.NearZ))
                      .Append(",\"Far\":").Append(F(cam.FarZ))
                      .Append(",\"Main\":").Append(cam.IsMain ? "true" : "false")
                      .Append(",\"Ortho\":").Append(cam.IsOrthographic ? "true" : "false")
                      .Append(",\"OrthoSize\":").Append(F(cam.OrthoSize))
                      .Append(",\"Enabled\":").Append(cam.Enabled ? "true" : "false").Append('}');
                }

                if (world.TryGetComponent<LightComponent>(id, out var lt))
                {
                    Comma(sb, ref fc);
                    sb.Append("\"Light\":{\"Type\":\"").Append(lt.Type.ToString())
                      .Append("\",\"Color\":").Append(V3(lt.Color))
                      .Append(",\"Int\":").Append(F(lt.Intensity))
                      .Append(",\"Range\":").Append(F(lt.Range))
                      .Append(",\"Inner\":").Append(F(lt.InnerAngle))
                        .Append(",\"Outer\":").Append(F(lt.OuterAngle))
                      .Append(",\"CastSh\":").Append(lt.CastsShadows ? "true" : "false")
                        .Append(",\"Enabled\":").Append(lt.Enabled ? "true" : "false").Append('}');
                }

                if (world.TryGetComponent<RigidbodyComponent>(id, out var rb))
                {
                    Comma(sb, ref fc);
                                        sb.Append("\"Rigidbody\":{\"MotionType\":\"").Append(rb.MotionType.ToString())
                                            .Append("\",\"Static\":").Append(rb.IsStatic ? "true" : "false")
                      .Append(",\"Mass\":").Append(F(rb.Mass))
                        .Append(",\"Gravity\":").Append(rb.UseGravity ? "true" : "false")
                                                .Append(",\"Friction\":").Append(F(rb.Friction))
                                                .Append(",\"Restitution\":").Append(F(rb.Restitution))
                                                .Append(",\"LinearDamping\":").Append(F(rb.LinearDamping))
                                                .Append(",\"AngularDamping\":").Append(F(rb.AngularDamping));
                    // Shapes array (geometry is not serialised — repopulated at play time from MeshRenderer)
                    sb.Append(",\"Shapes\":[");
                    var shapes = rb.Shapes;
                    if (shapes != null)
                    {
                        for (int si = 0; si < shapes.Count; si++)
                        {
                            if (si > 0) sb.Append(',');
                            var se = shapes[si];
                            sb.Append("{\"S\":\"").Append(se.Shape.ToString()).Append('"')
                              .Append(",\"HX\":").Append(F(se.HalfExtent.X))
                              .Append(",\"HY\":").Append(F(se.HalfExtent.Y))
                              .Append(",\"HZ\":").Append(F(se.HalfExtent.Z))
                              .Append(",\"R\":").Append(F(se.Radius))
                              .Append(",\"H\":").Append(F(se.HalfHeight))
                              .Append(",\"OX\":").Append(F(se.Offset.X))
                              .Append(",\"OY\":").Append(F(se.Offset.Y))
                              .Append(",\"OZ\":").Append(F(se.Offset.Z))
                              .Append('}');
                        }
                    }
                    sb.Append(']');
                    sb.Append(",\"Trigger\":").Append(rb.IsTrigger ? "true" : "false")
                        .Append(",\"Layer\":").Append(rb.Layer)
                        .Append(",\"LayerMask\":").Append(rb.LayerMask)
                        .Append('}');
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

                if (world.TryGetComponent<ConstraintComponent>(id, out var con))
                {
                    Comma(sb, ref fc);
                    sb.Append("\"Constraint\":{\"T\":\"").Append(con.Type.ToString()).Append('"')
                      .Append(",\"A\":").Append(!con.BodyA.IsNull ? world.GetSceneId(con.BodyA) : -1)
                      .Append(",\"B\":").Append(!con.BodyB.IsNull ? world.GetSceneId(con.BodyB) : -1)
                      .Append(",\"AA\":").Append(V3(con.LocalAnchorA))
                      .Append(",\"AB\":").Append(V3(con.LocalAnchorB))
                      .Append(",\"XA\":").Append(V3(con.LocalAxisA))
                      .Append(",\"XB\":").Append(V3(con.LocalAxisB))
                      .Append(",\"Min\":").Append(F(con.MinLimit))
                      .Append(",\"Max\":").Append(F(con.MaxLimit))
                      .Append('}');
                }

                if (world.TryGetComponent<CharacterComponent>(id, out var chComp))
                {
                    Comma(sb, ref fc);
                    sb.Append("\"Character\":{\"Mode\":").Append((int)chComp.Mode)
                      .Append(",\"Height\":").Append(F(chComp.Height))
                      .Append(",\"Radius\":").Append(F(chComp.Radius))
                      .Append(",\"Slope\":").Append(F(chComp.MaxSlopeAngle))
                      .Append(",\"Strength\":").Append(F(chComp.MaxStrength))
                      .Append(",\"Mass\":").Append(F(chComp.Mass))
                      .Append(",\"Friction\":").Append(F(chComp.Friction))
                      .Append(",\"Gravity\":").Append(F(chComp.GravityFactor))
                      .Append(",\"CtTol\":").Append(F(chComp.CollisionTolerance))
                      .Append(",\"OfsX\":").Append(F(chComp.ShapeOffset.X))
                      .Append(",\"OfsY\":").Append(F(chComp.ShapeOffset.Y))
                      .Append(",\"OfsZ\":").Append(F(chComp.ShapeOffset.Z))
                      .Append(",\"Layer\":").Append(chComp.Layer)
                      .Append(",\"Mask\":").Append(chComp.LayerMask)
                      .Append('}');
                }

                if (world.TryGetComponent<VehicleComponent>(id, out var vehComp))
                {
                    Comma(sb, ref fc);
                    sb.Append("\"Vehicle\":{\"Type\":").Append((int)vehComp.Type)
                      .Append(",\"HX\":").Append(F(vehComp.ChassisHalfExtent.X))
                      .Append(",\"HY\":").Append(F(vehComp.ChassisHalfExtent.Y))
                      .Append(",\"HZ\":").Append(F(vehComp.ChassisHalfExtent.Z))
                      .Append(",\"Mass\":").Append(F(vehComp.Mass))
                      .Append(",\"COM\":").Append(F(vehComp.COMOffsetY))
                      .Append(",\"Torque\":").Append(F(vehComp.MaxEngineTorque))
                      .Append(",\"Clutch\":").Append(F(vehComp.ClutchStrength))
                      .Append(",\"Roll\":").Append(F(vehComp.MaxRollAngle))
                      .Append(",\"Friction\":").Append(F(vehComp.Friction))
                      .Append(",\"Layer\":").Append(vehComp.Layer)
                      .Append(",\"Mask\":").Append(vehComp.LayerMask)
                      .Append(",\"Wheels\":[");
                    for (int wi = 0; wi < vehComp.Wheels.Length; wi++)
                    {
                        if (wi > 0) sb.Append(',');
                        var w = vehComp.Wheels[wi];
                        sb.Append("{\"PX\":").Append(F(w.Position.X))
                          .Append(",\"PY\":").Append(F(w.Position.Y))
                          .Append(",\"PZ\":").Append(F(w.Position.Z))
                          .Append(",\"R\":").Append(F(w.Radius))
                          .Append(",\"W\":").Append(F(w.Width))
                          .Append(",\"SMin\":").Append(F(w.SuspMinLength))
                          .Append(",\"SMax\":").Append(F(w.SuspMaxLength))
                          .Append(",\"SFreq\":").Append(F(w.SuspFrequency))
                          .Append(",\"SDamp\":").Append(F(w.SuspDamping))
                          .Append(",\"Steer\":").Append(F(w.MaxSteerAngle))
                          .Append(",\"HBrake\":").Append(F(w.MaxHandBrakeTorque))
                          .Append(",\"Driven\":").Append(w.IsDriven ? "true" : "false")
                          .Append('}');
                    }
                    sb.Append("]}");
                }

                if (world.TryGetComponent<WheelFollowerComponent>(id, out var wf))
                {
                    Comma(sb, ref fc);
                    sb.Append("\"WheelFollow\":{\"V\":")
                      .Append(!wf.VehicleEntity.IsNull ? world.GetSceneId(wf.VehicleEntity) : -1)
                      .Append(",\"WI\":").Append(wf.WheelIndex)
                      .Append('}');
                }

                foreach (var (key, ext) in _extensions)
                {
                    string? block = ext.Write(world, id);
                    if (block == null) continue;
                    Comma(sb, ref fc);
                    sb.Append('"').Append(Esc(key)).Append("\":").Append(block);
                }

                if (world.TryGetComponent<PreservedComponents>(id, out var pres) && pres.Blocks != null)
                {
                    foreach (var kvp in pres.Blocks)
                    {
                        if (_extensions.ContainsKey(kvp.Key)) continue; // the extension owns this key now
                        Comma(sb, ref fc);
                        sb.Append('"').Append(Esc(kvp.Key)).Append("\":").Append(kvp.Value);
                    }
                }

                sb.Append("}}"); // close components + entity
            }
        }

        // Saved entity ids found while reading components, resolved by ApplyFixups.
        private sealed class Fixups
        {
            public readonly Dictionary<Entity, int> Parent = new();
            public readonly Dictionary<Entity, (int, int)> Constraint = new();
            public readonly Dictionary<Entity, int> WheelFollow = new();
            // Registered extension blocks, read once every entity exists (they may reference others)
            public readonly List<(Entity, string, JsonElement)> Extensions = new();
        }

        // Reads the built-in component blocks present in c onto newId (a new entity, or an existing one being edited).
        private static void ReadBuiltIns(ECSWorld world, Entity newId, JsonElement c, Fixups fx)
        {
            if (c.TryGetProperty("NameTag", out var nEl))
                world.AddComponent(newId, new NameTag { Name = nEl.GetProperty("Name").GetString() ?? "Entity" });

            if (c.TryGetProperty("ActiveFlag", out var aEl))
                world.AddComponent(newId, new ActiveFlag { Active = aEl.GetProperty("Active").GetBoolean() });

            if (c.TryGetProperty("Transform", out var tEl))
            {
                if (tEl.TryGetProperty("Par", out var pEl))
                    fx.Parent[newId] = pEl.GetInt32();
                var tr = new Transform
                {
                    Position    = RV3(tEl, "P"),
                    EulerAngles = RV3(tEl, "E"),
                    Scale       = RV3(tEl, "S"),
                    Parent      = null  // fixed up in second pass
                };
                if (tEl.TryGetProperty("Q", out var qEl) && qEl.GetArrayLength() == 4)
                    tr.Rotation = new Quaternion(qEl[0].GetSingle(), qEl[1].GetSingle(), qEl[2].GetSingle(), qEl[3].GetSingle());
                world.AddComponent(newId, tr);
            }

            if (c.TryGetProperty("MeshRenderer", out var mrEl))
                world.AddComponent(newId, new MeshRenderer
                {
                    MeshPath     = mrEl.GetProperty("Path").GetString() ?? "",
                    MaterialPath = mrEl.TryGetProperty("Mat", out var matEl) ? (matEl.GetString() ?? "") : "",
                    Visible      = mrEl.GetProperty("Vis").GetBoolean(),
                    ReceivesShadows = !mrEl.TryGetProperty("RecvSh", out var recvShEl) || recvShEl.GetBoolean(),
                    CastsShadows = !mrEl.TryGetProperty("CastSh", out var castShEl) || castShEl.GetBoolean(),
                });

            if (c.TryGetProperty("SkinnedMeshRenderer", out var skEl))
                world.AddComponent(newId, new SkinnedMeshRenderer
                {
                    CharacterKey    = skEl.GetProperty("Key").GetString() ?? "",
                    PrimIndex       = skEl.TryGetProperty("Prim", out var primEl) ? primEl.GetInt32() : 0,
                    MaterialKey     = skEl.TryGetProperty("Mat", out var smatEl) ? (smatEl.GetString() ?? "") : "",
                    Visible         = !skEl.TryGetProperty("Vis", out var svisEl) || svisEl.GetBoolean(),
                    ReceivesShadows = !skEl.TryGetProperty("RecvSh", out var srecvEl) || srecvEl.GetBoolean(),
                    CastsShadows    = !skEl.TryGetProperty("CastSh", out var scastEl) || scastEl.GetBoolean(),
                });

            if (c.TryGetProperty("Camera", out var camEl))
                world.AddComponent(newId, new CameraComponent
                {
                    Fov            = camEl.GetProperty("Fov").GetSingle(),
                    NearZ          = camEl.GetProperty("Near").GetSingle(),
                    FarZ           = camEl.GetProperty("Far").GetSingle(),
                    IsMain         = camEl.GetProperty("Main").GetBoolean(),
                    IsOrthographic = camEl.TryGetProperty("Ortho", out var orthoEl) && orthoEl.GetBoolean(),
                    OrthoSize      = camEl.TryGetProperty("OrthoSize", out var osEl) ? osEl.GetSingle() : 5f,
                    Enabled        = !camEl.TryGetProperty("Enabled", out var camEnEl) || camEnEl.GetBoolean(),
                });

            if (c.TryGetProperty("Light", out var ltEl))
                world.AddComponent(newId, new LightComponent
                {
                    Type       = Enum.Parse<LightType>(ltEl.GetProperty("Type").GetString()!),
                    Color      = RV3(ltEl, "Color"),
                    Intensity  = ltEl.GetProperty("Int").GetSingle(),
                    Range      = ltEl.GetProperty("Range").GetSingle(),
                    InnerAngle = ltEl.TryGetProperty("Inner", out var innerEl) ? innerEl.GetSingle() : 25f,
                    OuterAngle = ltEl.TryGetProperty("Outer", out var outerEl) ? outerEl.GetSingle() : 35f,
                    CastsShadows = !ltEl.TryGetProperty("CastSh", out var castShEl) || castShEl.GetBoolean(),
                    Enabled      = !ltEl.TryGetProperty("Enabled", out var ltEnEl) || ltEnEl.GetBoolean(),
                });

            if (c.TryGetProperty("Rigidbody", out var rbEl))
            {
                List<ShapeEntry> loadedShapes;
                if (rbEl.TryGetProperty("Shapes", out var shapesEl) && shapesEl.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    loadedShapes = new List<ShapeEntry>();
                    foreach (var seEl in shapesEl.EnumerateArray())
                    {
                        var se = new ShapeEntry
                        {
                            Shape      = seEl.TryGetProperty("S", out var sEl2) ? Enum.Parse<GameEditor.Framework.Physics.ColliderShape>(sEl2.GetString() ?? "Box") : GameEditor.Framework.Physics.ColliderShape.Box,
                            HalfExtent = new System.Numerics.Vector3(
                                seEl.TryGetProperty("HX", out var hxEl) ? hxEl.GetSingle() : 0.5f,
                                seEl.TryGetProperty("HY", out var hyEl) ? hyEl.GetSingle() : 0.5f,
                                seEl.TryGetProperty("HZ", out var hzEl) ? hzEl.GetSingle() : 0.5f),
                            Radius     = seEl.TryGetProperty("R", out var rEl) ? rEl.GetSingle() : 0.5f,
                            HalfHeight = seEl.TryGetProperty("H", out var hEl) ? hEl.GetSingle() : 0.5f,
                            Offset     = new System.Numerics.Vector3(
                                seEl.TryGetProperty("OX", out var oxEl) ? oxEl.GetSingle() : 0f,
                                seEl.TryGetProperty("OY", out var oyEl) ? oyEl.GetSingle() : 0f,
                                seEl.TryGetProperty("OZ", out var ozEl) ? ozEl.GetSingle() : 0f),
                            OffsetRotation = System.Numerics.Quaternion.Identity,
                        };
                        loadedShapes.Add(se);
                    }
                    if (loadedShapes.Count == 0)
                        loadedShapes.Add(ShapeEntry.Default());
                }
                else
                {
                    // Backward compat: old format had a single "Shape" string field
                    var legacyShape = rbEl.TryGetProperty("Shape", out var shapeEl)
                        ? Enum.Parse<GameEditor.Framework.Physics.ColliderShape>(shapeEl.GetString() ?? "Box")
                        : GameEditor.Framework.Physics.ColliderShape.Box;
                    loadedShapes = new List<ShapeEntry> { ShapeEntry.Default(legacyShape) };
                }
                world.AddComponent(newId, new RigidbodyComponent
                {
                    MotionType = ReadMotionType(rbEl),
                    Mass       = rbEl.GetProperty("Mass").GetSingle(),
                    UseGravity = rbEl.GetProperty("Gravity").GetBoolean(),
                    Friction   = rbEl.TryGetProperty("Friction", out var frictionEl) ? frictionEl.GetSingle() : 0.5f,
                    Restitution = rbEl.TryGetProperty("Restitution", out var restitutionEl) ? restitutionEl.GetSingle() : 0f,
                    LinearDamping = rbEl.TryGetProperty("LinearDamping", out var linearDampingEl) ? linearDampingEl.GetSingle() : 0f,
                    AngularDamping = rbEl.TryGetProperty("AngularDamping", out var angularDampingEl) ? angularDampingEl.GetSingle() : 0.05f,
                    Shapes     = loadedShapes,
                    IsTrigger  = rbEl.TryGetProperty("Trigger", out var triggerEl) && triggerEl.GetBoolean(),
                    Layer      = rbEl.TryGetProperty("Layer", out var layerEl) ? (ushort)layerEl.GetInt32() : (ushort)1,
                    LayerMask  = rbEl.TryGetProperty("LayerMask", out var maskEl) ? (ushort)maskEl.GetInt32() : (ushort)0xFFFF
                });
            }

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
                else
                    world.RemoveComponent<ScriptCollectionComponent>(newId);   // an existing entity whose list is emptied
            }

            if (c.TryGetProperty("Constraint", out var conEl))
            {
                int savedA = conEl.TryGetProperty("A", out var conAEl) ? conAEl.GetInt32() : -1;
                int savedB = conEl.TryGetProperty("B", out var conBEl) ? conBEl.GetInt32() : -1;
                world.AddComponent(newId, new ConstraintComponent
                {
                    Type         = conEl.TryGetProperty("T", out var tEl2) &&
                                   Enum.TryParse(tEl2.GetString(), out GameEditor.Framework.Physics.ConstraintType ct)
                                   ? ct : GameEditor.Framework.Physics.ConstraintType.Fixed,
                    LocalAnchorA = RV3(conEl, "AA"),
                    LocalAnchorB = RV3(conEl, "AB"),
                    LocalAxisA   = RV3(conEl, "XA"),
                    LocalAxisB   = RV3(conEl, "XB"),
                    MinLimit     = conEl.TryGetProperty("Min", out var minEl) ? minEl.GetSingle() : 0f,
                    MaxLimit     = conEl.TryGetProperty("Max", out var maxEl) ? maxEl.GetSingle() : 0f,
                });
                if (savedA >= 0 || savedB >= 0)
                    fx.Constraint[newId] = (savedA, savedB);
            }

            if (c.TryGetProperty("Character", out var charEl))
            {
                world.AddComponent(newId, new CharacterComponent
                {
                    Mode               = charEl.TryGetProperty("Mode",     out var modeEl) ? (CharacterMode)modeEl.GetInt32() : CharacterComponent.Default.Mode,
                    Height             = charEl.TryGetProperty("Height",   out var hEl)    ? hEl.GetSingle()                  : CharacterComponent.Default.Height,
                    Radius             = charEl.TryGetProperty("Radius",   out var rEl)    ? rEl.GetSingle()                  : CharacterComponent.Default.Radius,
                    MaxSlopeAngle      = charEl.TryGetProperty("Slope",    out var slEl2)  ? slEl2.GetSingle()                : CharacterComponent.Default.MaxSlopeAngle,
                    MaxStrength        = charEl.TryGetProperty("Strength", out var stEl)   ? stEl.GetSingle()                 : CharacterComponent.Default.MaxStrength,
                    Mass               = charEl.TryGetProperty("Mass",     out var mEl)    ? mEl.GetSingle()                  : CharacterComponent.Default.Mass,
                    Friction           = charEl.TryGetProperty("Friction", out var frEl)   ? frEl.GetSingle()                 : CharacterComponent.Default.Friction,
                    GravityFactor      = charEl.TryGetProperty("Gravity",  out var gfEl)   ? gfEl.GetSingle()                 : CharacterComponent.Default.GravityFactor,
                    CollisionTolerance = charEl.TryGetProperty("CtTol",    out var ctEl)   ? ctEl.GetSingle()                 : CharacterComponent.Default.CollisionTolerance,
                    ShapeOffset        = new System.Numerics.Vector3(
                        charEl.TryGetProperty("OfsX", out var oxEl) ? oxEl.GetSingle() : 0f,
                        charEl.TryGetProperty("OfsY", out var oyEl) ? oyEl.GetSingle() : 0f,
                        charEl.TryGetProperty("OfsZ", out var ozEl) ? ozEl.GetSingle() : 0f),
                    Layer              = charEl.TryGetProperty("Layer",    out var lEl)    ? (ushort)lEl.GetInt32()           : CharacterComponent.Default.Layer,
                    LayerMask          = charEl.TryGetProperty("Mask",     out var mkEl)   ? (ushort)mkEl.GetInt32()          : CharacterComponent.Default.LayerMask,
                });
            }

            if (c.TryGetProperty("Vehicle", out var vehEl))
            {
                var def = VehicleComponent.DefaultCar;
                var vType = vehEl.TryGetProperty("Type", out var vtEl) ? (VehicleType)vtEl.GetInt32() : def.Type;
                VehicleWheelSettings[] wheels;
                if (vehEl.TryGetProperty("Wheels", out var wheelsEl) && wheelsEl.ValueKind == JsonValueKind.Array)
                {
                    var wList = new List<VehicleWheelSettings>();
                    var dw = def.Wheels.Length > 0 ? def.Wheels[0] : default;
                    foreach (var we in wheelsEl.EnumerateArray())
                    {
                        wList.Add(new VehicleWheelSettings
                        {
                            Position          = new System.Numerics.Vector3(
                                we.TryGetProperty("PX", out var px) ? px.GetSingle() : 0f,
                                we.TryGetProperty("PY", out var py) ? py.GetSingle() : 0f,
                                we.TryGetProperty("PZ", out var pz) ? pz.GetSingle() : 0f),
                            Radius            = we.TryGetProperty("R",     out var wr)   ? wr.GetSingle()   : dw.Radius,
                            Width             = we.TryGetProperty("W",     out var ww)   ? ww.GetSingle()   : dw.Width,
                            SuspMinLength     = we.TryGetProperty("SMin",  out var sm)   ? sm.GetSingle()   : dw.SuspMinLength,
                            SuspMaxLength     = we.TryGetProperty("SMax",  out var sx)   ? sx.GetSingle()   : dw.SuspMaxLength,
                            SuspFrequency     = we.TryGetProperty("SFreq", out var sf)   ? sf.GetSingle()   : dw.SuspFrequency,
                            SuspDamping       = we.TryGetProperty("SDamp", out var sd)   ? sd.GetSingle()   : dw.SuspDamping,
                            MaxSteerAngle     = we.TryGetProperty("Steer", out var st)   ? st.GetSingle()   : dw.MaxSteerAngle,
                            MaxHandBrakeTorque= we.TryGetProperty("HBrake",out var hb)   ? hb.GetSingle()   : dw.MaxHandBrakeTorque,
                            IsDriven          = we.TryGetProperty("Driven",out var dr) && dr.GetBoolean(),
                        });
                    }
                    wheels = wList.ToArray();
                }
                else
                {
                    wheels = vType == VehicleType.Motorcycle ? VehicleComponent.DefaultMotorcycle.Wheels :
                             vType == VehicleType.Tracked    ? VehicleComponent.DefaultTank.Wheels :
                                                                VehicleComponent.DefaultCar.Wheels;
                }
                world.AddComponent(newId, new VehicleComponent
                {
                    Type              = vType,
                    ChassisHalfExtent = new System.Numerics.Vector3(
                        vehEl.TryGetProperty("HX", out var hxEl) ? hxEl.GetSingle() : def.ChassisHalfExtent.X,
                        vehEl.TryGetProperty("HY", out var hyEl) ? hyEl.GetSingle() : def.ChassisHalfExtent.Y,
                        vehEl.TryGetProperty("HZ", out var hzEl) ? hzEl.GetSingle() : def.ChassisHalfExtent.Z),
                    Mass              = vehEl.TryGetProperty("Mass",    out var vmEl)  ? vmEl.GetSingle()  : def.Mass,
                    COMOffsetY        = vehEl.TryGetProperty("COM",     out var comEl) ? comEl.GetSingle() : def.COMOffsetY,
                    MaxEngineTorque   = vehEl.TryGetProperty("Torque",  out var tqEl)  ? tqEl.GetSingle()  : def.MaxEngineTorque,
                    ClutchStrength    = vehEl.TryGetProperty("Clutch",  out var clEl)  ? clEl.GetSingle()  : def.ClutchStrength,
                    MaxRollAngle      = vehEl.TryGetProperty("Roll",    out var rlEl)  ? rlEl.GetSingle()  : def.MaxRollAngle,
                    Friction          = vehEl.TryGetProperty("Friction", out var vfEl) ? vfEl.GetSingle()  : def.Friction,
                    Layer             = vehEl.TryGetProperty("Layer",   out var vlEl)  ? (ushort)vlEl.GetInt32() : def.Layer,
                    LayerMask         = vehEl.TryGetProperty("Mask",    out var vmkEl) ? (ushort)vmkEl.GetInt32(): def.LayerMask,
                    Wheels            = wheels,
                });
            }

            if (c.TryGetProperty("WheelFollow", out var wfEl))
            {
                int savedV = wfEl.TryGetProperty("V",  out var vEl)  ? vEl.GetInt32()  : -1;
                int wi     = wfEl.TryGetProperty("WI", out var wiEl) ? wiEl.GetInt32() : 0;
                world.AddComponent(newId, new WheelFollowerComponent { WheelIndex = wi });
                if (savedV >= 0) fx.WheelFollow[newId] = savedV;
            }
        }

        private static void ApplyFixups(ECSWorld world, Fixups fx, Func<int, Entity> resolve)
        {
            // Parent ids → live Entities
            foreach (var (entity, savedParentId) in fx.Parent)
            {
                Entity parentEntity = resolve(savedParentId);
                if (parentEntity.IsNull) continue;
                if (world.TryGetComponent<Transform>(entity, out var tr))
                {
                    tr.Parent = parentEntity;
                    world.AddComponent(entity, tr);
                }
            }

            // Constraint BodyA/BodyB ids → live Entities
            foreach (var (entity, savedIds) in fx.Constraint)
            {
                if (!world.TryGetComponent<ConstraintComponent>(entity, out var cc)) continue;
                if (savedIds.Item1 >= 0 && resolve(savedIds.Item1) is { IsNull: false } ea)
                    cc.BodyA = ea;
                if (savedIds.Item2 >= 0 && resolve(savedIds.Item2) is { IsNull: false } eb)
                    cc.BodyB = eb;
                world.AddComponent(entity, cc);
            }

            // WheelFollowerComponent.VehicleEntity ids → live Entities
            foreach (var (entity, savedVId) in fx.WheelFollow)
            {
                if (!world.TryGetComponent<WheelFollowerComponent>(entity, out var wfc)) continue;
                if (resolve(savedVId) is { IsNull: false } ve)
                    wfc.VehicleEntity = ve;
                world.AddComponent(entity, wfc);
            }

            // Registered extensions, with saved ids resolvable to live Entities
            foreach (var (entity, key, value) in fx.Extensions)
                _extensions[key].Read(world, entity, value, resolve);
        }

        // ── Helpers ─────────────────────────────────────────────────────────

        private static void Comma(StringBuilder sb, ref bool first)
        {
            if (!first) sb.Append(',');
            first = false;
        }

        private static string F(float v) => v.ToString("G", CultureInfo.InvariantCulture);

        private static string V3(Vector3 v) => $"[{F(v.X)},{F(v.Y)},{F(v.Z)}]";
        private static string Q4(Quaternion q) => $"[{F(q.X)},{F(q.Y)},{F(q.Z)},{F(q.W)}]";

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

        private static GameEditor.Framework.Physics.RigidbodyMotionType ReadMotionType(JsonElement rbEl)
        {
            if (rbEl.TryGetProperty("MotionType", out var mtEl))
            {
                string? mt = mtEl.GetString();
                if (!string.IsNullOrEmpty(mt) && Enum.TryParse(mt, true, out GameEditor.Framework.Physics.RigidbodyMotionType parsed))
                    return parsed;
            }

            if (rbEl.TryGetProperty("Static", out var staticEl) && staticEl.ValueKind == JsonValueKind.True)
                return GameEditor.Framework.Physics.RigidbodyMotionType.Static;

            return GameEditor.Framework.Physics.RigidbodyMotionType.Dynamic;
        }
    }
}
