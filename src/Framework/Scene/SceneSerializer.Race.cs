using System;
using System.Text.Json;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;

namespace GameEditor.Framework.Scene
{
    // The scene blocks of a race: "Race" (laps, countdown, respawn), "Racer" (a name), "RaceHud" (what a text shows).
    public static partial class SceneSerializer
    {
        private static void RegisterRace()
        {
            RegisterComponentExtension("Race", WriteRace, ReadRace, static (w, e) => w.RemoveComponent<RaceComponent>(e));
            RegisterComponentExtension("Racer", WriteRacer, ReadRacer, static (w, e) => w.RemoveComponent<RacerComponent>(e));
            RegisterComponentExtension("RaceHud", WriteRaceHud, ReadRaceHud, static (w, e) => w.RemoveComponent<RaceHudComponent>(e));
        }

        private static string? WriteRace(ECSWorld world, Frent.Entity e)
        {
            if (!world.TryGetComponent<RaceComponent>(e, out var r)) return null;
            return "{\"Laps\":" + r.Laps + ",\"Countdown\":" + F(r.Countdown) + ",\"RespawnAfter\":" + F(r.RespawnAfter) + ",\"OffTrack\":" + F(r.OffTrack) + "}";
        }

        private static void ReadRace(ECSWorld world, Frent.Entity e, JsonElement el, Func<int, Frent.Entity> resolve)
        {
            var r = RaceComponent.Default;
            if (el.TryGetProperty("Laps", out var l) && l.ValueKind == JsonValueKind.Number) r.Laps = Math.Max(1, l.GetInt32());
            r.Countdown = Number(el, "Countdown", r.Countdown);
            r.RespawnAfter = Number(el, "RespawnAfter", r.RespawnAfter);
            r.OffTrack = Number(el, "OffTrack", r.OffTrack);
            world.AddComponent(e, r);
        }

        private static string? WriteRacer(ECSWorld world, Frent.Entity e)
        {
            if (!world.TryGetComponent<RacerComponent>(e, out var r)) return null;
            return "{\"Name\":\"" + Esc(r.Name ?? "") + "\"}";
        }

        private static void ReadRacer(ECSWorld world, Frent.Entity e, JsonElement el, Func<int, Frent.Entity> resolve)
        {
            var r = RacerComponent.Default;
            if (el.TryGetProperty("Name", out var n) && n.ValueKind == JsonValueKind.String) r.Name = n.GetString() ?? "";
            world.AddComponent(e, r);
        }

        private static string? WriteRaceHud(ECSWorld world, Frent.Entity e)
        {
            if (!world.TryGetComponent<RaceHudComponent>(e, out var h)) return null;
            return "{\"Shows\":\"" + h.Shows + "\"}";
        }

        private static void ReadRaceHud(ECSWorld world, Frent.Entity e, JsonElement el, Func<int, Frent.Entity> resolve)
        {
            var h = new RaceHudComponent();
            if (el.TryGetProperty("Shows", out var s) && s.ValueKind == JsonValueKind.String && Enum.TryParse(s.GetString(), out RaceHudShows shows)) h.Shows = shows;
            world.AddComponent(e, h);
        }
    }
}
