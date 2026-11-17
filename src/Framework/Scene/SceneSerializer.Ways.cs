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
    // The scene blocks "WayGraph", "TrafficCar" and "Walker" (Phase H E4): a graph of roads or pavements (its points and its
    // one-way edges [from, to, speed, crossing]), a car of the traffic on it, a person walking it. What they do now is not saved.
    public static partial class SceneSerializer
    {
        private static void RegisterWays()
        {
            RegisterComponentExtension("WayGraph", WriteWayGraph, ReadWayGraph, static (w, e) => w.RemoveComponent<WayGraph>(e));
            RegisterComponentExtension("TrafficCar", WriteTrafficCar, ReadTrafficCar, static (w, e) => w.RemoveComponent<TrafficCar>(e));
            RegisterComponentExtension("Walker", WriteWalker, ReadWalker, static (w, e) => w.RemoveComponent<Walker>(e));
        }

        private static string? WriteWayGraph(ECSWorld world, Frent.Entity e)
        {
            if (!world.TryGetComponent<WayGraph>(e, out var g)) return null;
            var sb = new StringBuilder("{\"Kind\":\"").Append(g.Kind == WayKind.Pavement ? "Pavement" : "Road").Append("\",\"Points\":[");
            var pts = g.Points ?? Array.Empty<Vector3>();
            for (int i = 0; i < pts.Length; i++)
                sb.Append(i > 0 ? "," : "").Append('[').Append(F(pts[i].X)).Append(',').Append(F(pts[i].Y)).Append(',').Append(F(pts[i].Z)).Append(']');
            sb.Append("],\"Edges\":[");
            var eds = g.Edges ?? Array.Empty<WayEdge>();
            for (int i = 0; i < eds.Length; i++)
                sb.Append(i > 0 ? "," : "").Append('[').Append(eds[i].From.ToString(CultureInfo.InvariantCulture)).Append(',')
                  .Append(eds[i].To.ToString(CultureInfo.InvariantCulture)).Append(',').Append(F(eds[i].Speed)).Append(',').Append(eds[i].Crossing ? "1" : "0").Append(']');
            return sb.Append("]}").ToString();
        }

        private static void ReadWayGraph(ECSWorld world, Frent.Entity e, JsonElement el, Func<int, Frent.Entity> resolve)
        {
            var g = WayGraph.Empty(Text(el, "Kind", "Road") == "Pavement" ? WayKind.Pavement : WayKind.Road);
            var pts = new List<Vector3>();
            if (el.TryGetProperty("Points", out var pa) && pa.ValueKind == JsonValueKind.Array)
                foreach (var p in pa.EnumerateArray())
                    if (p.ValueKind == JsonValueKind.Array && p.GetArrayLength() == 3) pts.Add(new Vector3(p[0].GetSingle(), p[1].GetSingle(), p[2].GetSingle()));
            var eds = new List<WayEdge>();
            if (el.TryGetProperty("Edges", out var ea) && ea.ValueKind == JsonValueKind.Array)
                foreach (var x in ea.EnumerateArray())
                {
                    if (x.ValueKind != JsonValueKind.Array || x.GetArrayLength() < 2) continue;
                    int from = x[0].GetInt32(), to = x[1].GetInt32();
                    if ((uint)from >= (uint)pts.Count || (uint)to >= (uint)pts.Count) continue;
                    eds.Add(new WayEdge { From = from, To = to, Speed = x.GetArrayLength() > 2 ? x[2].GetSingle() : 8f, Crossing = x.GetArrayLength() > 3 && x[3].GetSingle() != 0f });
                }
            g.Points = pts.ToArray();
            g.Edges = eds.ToArray();
            world.AddComponent(e, g);
        }

        private static string? WriteTrafficCar(ECSWorld world, Frent.Entity e)
            => world.TryGetComponent<TrafficCar>(e, out var c)
                ? "{\"Edge\":" + c.Edge.ToString(CultureInfo.InvariantCulture) + ",\"Along\":" + F(c.Along) + ",\"MaxSpeed\":" + F(c.MaxSpeed) + ",\"Gap\":" + F(c.Gap)
                  + ",\"Accel\":" + F(c.Acceleration) + ",\"Seed\":" + c.Seed.ToString(CultureInfo.InvariantCulture) + "}" : null;

        private static void ReadTrafficCar(ECSWorld world, Frent.Entity e, JsonElement el, Func<int, Frent.Entity> resolve)
        {
            var c = TrafficCar.Default;
            c.Edge = Math.Max(0, (int)Number(el, "Edge", 0));
            c.Along = MathF.Max(0f, Number(el, "Along", 0f));
            c.MaxSpeed = MathF.Max(0f, Number(el, "MaxSpeed", c.MaxSpeed));
            c.Gap = MathF.Max(0f, Number(el, "Gap", c.Gap));
            c.Acceleration = MathF.Max(0.1f, Number(el, "Accel", c.Acceleration));
            c.Seed = (int)Number(el, "Seed", c.Seed);
            world.AddComponent(e, c);
        }

        private static string? WriteWalker(ECSWorld world, Frent.Entity e)
            => world.TryGetComponent<Walker>(e, out var w)
                ? "{\"Edge\":" + w.Edge.ToString(CultureInfo.InvariantCulture) + ",\"Along\":" + F(w.Along) + ",\"Speed\":" + F(w.Speed) + ",\"WaitRadius\":" + F(w.WaitRadius)
                  + ",\"WalkClip\":\"" + Esc(w.WalkClip ?? "") + "\",\"IdleClip\":\"" + Esc(w.IdleClip ?? "") + "\",\"Seed\":" + w.Seed.ToString(CultureInfo.InvariantCulture) + "}" : null;

        private static void ReadWalker(ECSWorld world, Frent.Entity e, JsonElement el, Func<int, Frent.Entity> resolve)
        {
            var w = Walker.Default;
            w.Edge = Math.Max(0, (int)Number(el, "Edge", 0));
            w.Along = MathF.Max(0f, Number(el, "Along", 0f));
            w.Speed = MathF.Max(0f, Number(el, "Speed", w.Speed));
            w.WaitRadius = MathF.Max(0f, Number(el, "WaitRadius", w.WaitRadius));
            w.WalkClip = Text(el, "WalkClip", w.WalkClip);
            w.IdleClip = Text(el, "IdleClip", w.IdleClip);
            w.Seed = (int)Number(el, "Seed", w.Seed);
            world.AddComponent(e, w);
        }
    }
}
