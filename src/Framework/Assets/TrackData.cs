using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;

namespace GameEditor.Framework.Assets
{
    /// <summary>A place on a track's centre line: where it is, which way the track goes, its right and up, its half width.</summary>
    public readonly struct TrackFrame
    {
        public readonly Vector3 Position, Forward, Right, Up;
        public readonly float HalfWidth, Distance;
        /// <summary>The racing line's offset across the road here (metres, positive to the right).</summary>
        public readonly float Line;
        public TrackFrame(Vector3 position, Vector3 forward, Vector3 right, Vector3 up, float halfWidth, float distance, float line = 0f)
        { Position = position; Forward = forward; Right = right; Up = up; HalfWidth = halfWidth; Distance = distance; Line = line; }

        /// <summary>The racing line's point here.</summary>
        public Vector3 LinePoint => Position + Right * Line;
    }

    /// <summary>
    /// A race track (a ".track" file): a closed centre line through its points — a centripetal Catmull-Rom spline, which
    /// passes through every point and never loops between two close ones — a width at each point, a banking, walls at
    /// both edges, checkpoints every so many metres. The road and wall meshes, the checkpoints and the answers a race
    /// and a driver need (<see cref="Nearest"/>, <see cref="FrameAt"/>) are all made from it here.
    /// The track's forward at the start is from point 0 towards point 1; its right is forward × up (for a track going
    /// +Z on flat ground the right is -X, as a kart's).
    /// </summary>
    public sealed class TrackData
    {
        public const string Extension = ".track";
        /// <summary>The centre line is sampled about every this many metres.</summary>
        public const float Step = 1f;

        public string Key { get; }
        public List<Vector3> Points { get; } = new();
        /// <summary>One width (metres, edge to edge) per point.</summary>
        public List<float> Widths { get; } = new();
        /// <summary>One banking (degrees, positive lifts the left edge) per point.</summary>
        public List<float> Bank { get; } = new();
        /// <summary>The racing line: one offset across the road per point (metres, positive to the right; 0 = the centre).</summary>
        public List<float> Line { get; } = new();
        public float WallHeight { get; set; } = 1f;
        public float WallThickness { get; set; } = 0.4f;
        public float CheckpointEvery { get; set; } = 10f;

        private TrackFrame[] _frames = Array.Empty<TrackFrame>();
        private readonly List<TrackFrame> _checkpoints = new();

        /// <summary>The length of the centre line, metres.</summary>
        public float Length { get; private set; }
        /// <summary>The centre line sampled about every <see cref="Step"/> metres (the last one is not the first again).</summary>
        public IReadOnlyList<TrackFrame> Frames => _frames;
        /// <summary>The checkpoints in order, the first at the start line (distance 0).</summary>
        public IReadOnlyList<TrackFrame> Checkpoints => _checkpoints;

        public TrackData(string key) { Key = key; }

        public static bool IsTrackKey(string? text)
            => text != null && text.EndsWith(Extension, StringComparison.OrdinalIgnoreCase) && text.IndexOf('\n') < 0;

        /// <summary>
        /// The path of a mesh made from a track: its key, '#', the part — "Tracks/loop.track#road" or "#walls" (the key
        /// before a '#', as a sheet's frame is named, so a track file that moves is followed by the asset ids).
        /// </summary>
        public const string RoadPart = "road", WallsPart = "walls";

        public static string MeshPath(string key, string part) => key + "#" + part;

        /// <summary>The track key a text names — the key itself, or a track mesh's path — or null.</summary>
        public static string? KeyOf(string? text)
        {
            if (text == null) return null;
            int hash = text.IndexOf('#');
            string key = hash < 0 ? text : text.Substring(0, hash);
            return IsTrackKey(key) ? key : null;
        }

        /// <summary>The key and the part ("road" or "walls") of a track mesh's path; false for any other path.</summary>
        public static bool TryParseMeshPath(string? path, out string key, out string part)
        {
            key = part = "";
            if (path == null) return false;
            int hash = path.IndexOf('#');
            if (hash <= 0) return false;
            key = path.Substring(0, hash);
            part = path.Substring(hash + 1);
            return IsTrackKey(key) && (part == RoadPart || part == WallsPart);
        }

        // ── The file ──────────────────────────────────────────────────────────────────────────

        /// <summary>Reads a ".track" file's text; null with <paramref name="error"/> when it is not one.</summary>
        public static TrackData? Parse(string key, string json, out string? error)
        {
            error = null;
            var t = new TrackData(key);
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (!root.TryGetProperty("points", out var pts) || pts.ValueKind != JsonValueKind.Array)
                { error = "no \"points\""; return null; }
                foreach (var p in pts.EnumerateArray())
                {
                    if (p.ValueKind != JsonValueKind.Array || p.GetArrayLength() != 3) { error = "a point is not [x, y, z]"; return null; }
                    t.Points.Add(new Vector3(p[0].GetSingle(), p[1].GetSingle(), p[2].GetSingle()));
                }
                float width = root.TryGetProperty("width", out var w) ? w.GetSingle() : 8f;
                if (root.TryGetProperty("widths", out var ws) && ws.ValueKind == JsonValueKind.Array)
                    foreach (var x in ws.EnumerateArray()) t.Widths.Add(x.GetSingle());
                if (root.TryGetProperty("bank", out var bs) && bs.ValueKind == JsonValueKind.Array)
                    foreach (var x in bs.EnumerateArray()) t.Bank.Add(x.GetSingle());
                if (root.TryGetProperty("line", out var ls) && ls.ValueKind == JsonValueKind.Array)
                    foreach (var x in ls.EnumerateArray()) t.Line.Add(x.GetSingle());
                if (root.TryGetProperty("wall", out var wall))
                {
                    if (wall.TryGetProperty("height", out var h)) t.WallHeight = h.GetSingle();
                    if (wall.TryGetProperty("thickness", out var th)) t.WallThickness = th.GetSingle();
                }
                if (root.TryGetProperty("checkpointEvery", out var ce)) t.CheckpointEvery = ce.GetSingle();
                while (t.Widths.Count < t.Points.Count) t.Widths.Add(width);
                while (t.Bank.Count < t.Points.Count) t.Bank.Add(0f);
                while (t.Line.Count < t.Points.Count) t.Line.Add(0f);
                if (t.Line.Count > t.Points.Count) t.Line.RemoveRange(t.Points.Count, t.Line.Count - t.Points.Count);
                if (t.Widths.Count > t.Points.Count) t.Widths.RemoveRange(t.Points.Count, t.Widths.Count - t.Points.Count);
                if (t.Bank.Count > t.Points.Count) t.Bank.RemoveRange(t.Points.Count, t.Bank.Count - t.Points.Count);
            }
            catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException)
            {
                error = e.Message;
                return null;
            }
            error = t.Rebuild();
            return error == null ? t : null;
        }

        /// <summary>The file's text.</summary>
        public string ToJson()
        {
            var sb = new StringBuilder();
            static string N(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
            sb.Append("{\n  \"points\": [");
            for (int i = 0; i < Points.Count; i++)
                sb.Append(i == 0 ? "" : ", ").Append('[').Append(N(Points[i].X)).Append(", ").Append(N(Points[i].Y)).Append(", ").Append(N(Points[i].Z)).Append(']');
            sb.Append("],\n  \"widths\": [");
            for (int i = 0; i < Widths.Count; i++) sb.Append(i == 0 ? "" : ", ").Append(N(Widths[i]));
            sb.Append("],\n  \"bank\": [");
            for (int i = 0; i < Bank.Count; i++) sb.Append(i == 0 ? "" : ", ").Append(N(Bank[i]));
            sb.Append("],\n  \"line\": [");
            for (int i = 0; i < Line.Count; i++) sb.Append(i == 0 ? "" : ", ").Append(N(Line[i]));
            sb.Append("],\n  \"wall\": {\"height\": ").Append(N(WallHeight)).Append(", \"thickness\": ").Append(N(WallThickness)).Append("},\n");
            sb.Append("  \"checkpointEvery\": ").Append(N(CheckpointEvery)).Append("\n}\n");
            return sb.ToString();
        }

        // ── The centre line ───────────────────────────────────────────────────────────────────

        /// <summary>Samples the centre line again after a change to the points, widths or banking; the reason it cannot, or null.</summary>
        public string? Rebuild()
        {
            int n = Points.Count;
            if (n < 4) return "a track needs at least 4 points";
            while (Widths.Count < n) Widths.Add(Widths.Count > 0 ? Widths[^1] : 8f);
            while (Bank.Count < n) Bank.Add(0f);
            while (Line.Count < n) Line.Add(0f);
            if (Line.Count > n) Line.RemoveRange(n, Line.Count - n);
            for (int i = 0; i < n; i++)
            {
                if (Widths[i] <= 0f) return $"point {i}: a width must be more than 0";
                if (Vector3.DistanceSquared(Points[i], Points[(i + 1) % n]) < 1e-4f) return $"points {i} and {(i + 1) % n} are at one place";
            }

            // A dense run along each segment, for the length of the line.
            const int Dense = 48;
            var pos = new List<Vector3>(n * Dense + 1);
            var at = new List<float>(n * Dense + 1);          // segment index + fraction, for widths and banking
            for (int s = 0; s < n; s++)
                for (int k = 0; k < Dense; k++)
                {
                    float u = k / (float)Dense;
                    pos.Add(CatmullRom(Points[(s - 1 + n) % n], Points[s], Points[(s + 1) % n], Points[(s + 2) % n], u));
                    at.Add(s + u);
                }
            int m = pos.Count;
            var dist = new float[m + 1];
            for (int i = 0; i < m; i++) dist[i + 1] = dist[i] + Vector3.Distance(pos[i], pos[(i + 1) % m]);
            Length = dist[m];

            // Even samples along the length.
            int count = Math.Max(8, (int)MathF.Round(Length / Step));
            float step = Length / count;
            var frames = new TrackFrame[count];
            int j = 0;
            for (int i = 0; i < count; i++)
            {
                float d = i * step;
                while (j < m - 1 && dist[j + 1] < d) j++;
                float f = (d - dist[j]) / MathF.Max(dist[j + 1] - dist[j], 1e-6f);
                Vector3 p = Vector3.Lerp(pos[j], pos[(j + 1) % m], f);
                float a = at[j] + f / Dense;
                frames[i] = new TrackFrame(p, Vector3.Zero, Vector3.Zero, Vector3.Zero, 0.5f * Blend(Widths, a), d, Blend(Line, a));
            }
            // Forward from the neighbours; up the world's turned by the banking; right = forward × up.
            for (int i = 0; i < count; i++)
            {
                Vector3 fwd = Vector3.Normalize(frames[(i + 1) % count].Position - frames[(i - 1 + count) % count].Position);
                float bank = Blend(Bank, AtOf(i)) * MathF.PI / 180f;
                Vector3 flatRight = Vector3.Cross(fwd, Vector3.UnitY);
                if (flatRight.LengthSquared() < 1e-6f) flatRight = -Vector3.UnitX;
                flatRight = Vector3.Normalize(flatRight);
                Vector3 up = Vector3.Normalize(Vector3.Cross(flatRight, fwd));
                up = Vector3.Transform(up, Quaternion.CreateFromAxisAngle(fwd, bank));
                Vector3 right = Vector3.Normalize(Vector3.Cross(fwd, up));
                frames[i] = new TrackFrame(frames[i].Position, fwd, right, up, frames[i].HalfWidth, frames[i].Distance, frames[i].Line);
            }
            _frames = frames;

            _checkpoints.Clear();
            float every = CheckpointEvery > 0.5f ? CheckpointEvery : 10f;
            int cps = Math.Max(2, (int)MathF.Floor(Length / every));
            for (int c = 0; c < cps; c++) _checkpoints.Add(FrameAt(c * Length / cps));
            return null;

            float AtOf(int i)
            {
                float d = frames[i].Distance;
                int k = Array.BinarySearch(dist, d);
                if (k < 0) k = Math.Max(0, ~k - 1);
                k = Math.Min(k, m - 1);
                return at[k];
            }
        }

        private float Blend(List<float> values, float at)
        {
            int n = values.Count;
            int i = (int)MathF.Floor(at) % n;
            float f = at - MathF.Floor(at);
            float s = f * f * (3f - 2f * f);    // eased, so a change of width has no corner at a point
            return values[i] + (values[(i + 1) % n] - values[i]) * s;
        }

        // Centripetal Catmull-Rom (alpha 0.5) between p1 and p2.
        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float u)
        {
            static float Knot(float t, Vector3 a, Vector3 b) => t + MathF.Max(MathF.Sqrt(Vector3.Distance(a, b)), 1e-4f);
            float t0 = 0f, t1 = Knot(t0, p0, p1), t2 = Knot(t1, p1, p2), t3 = Knot(t2, p2, p3);
            float t = t1 + (t2 - t1) * u;
            Vector3 a1 = (t1 - t) / (t1 - t0) * p0 + (t - t0) / (t1 - t0) * p1;
            Vector3 a2 = (t2 - t) / (t2 - t1) * p1 + (t - t1) / (t2 - t1) * p2;
            Vector3 a3 = (t3 - t) / (t3 - t2) * p2 + (t - t2) / (t3 - t2) * p3;
            Vector3 b1 = (t2 - t) / (t2 - t0) * a1 + (t - t0) / (t2 - t0) * a2;
            Vector3 b2 = (t3 - t) / (t3 - t1) * a2 + (t - t1) / (t3 - t1) * a3;
            return (t2 - t) / (t2 - t1) * b1 + (t - t1) / (t2 - t1) * b2;
        }

        // ── Questions a race and a driver ask ────────────────────────────────────────────────

        /// <summary>The centre line at <paramref name="distance"/> metres from the start (any number: it goes round).</summary>
        public TrackFrame FrameAt(float distance)
        {
            int count = _frames.Length;
            if (count == 0) return default;
            float d = distance % Length;
            if (d < 0f) d += Length;
            float step = Length / count;
            int i = Math.Min((int)(d / step), count - 1);
            int k = (i + 1) % count;
            float f = (d - i * step) / step;
            TrackFrame a = _frames[i], b = _frames[k];
            return new TrackFrame(Vector3.Lerp(a.Position, b.Position, f),
                                  Vector3.Normalize(Vector3.Lerp(a.Forward, b.Forward, f)),
                                  Vector3.Normalize(Vector3.Lerp(a.Right, b.Right, f)),
                                  Vector3.Normalize(Vector3.Lerp(a.Up, b.Up, f)),
                                  a.HalfWidth + (b.HalfWidth - a.HalfWidth) * f, d, a.Line + (b.Line - a.Line) * f);
        }

        /// <summary>
        /// Where <paramref name="position"/> is beside the track: the distance along the centre line of the nearest
        /// place on it, and the offset across (positive to the track's right).
        /// </summary>
        public float Nearest(Vector3 position, out float offset)
        {
            int count = _frames.Length;
            offset = 0f;
            if (count == 0) return 0f;
            int best = 0;
            float bestD = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                float d = Vector3.DistanceSquared(_frames[i].Position, position);
                if (d < bestD) { bestD = d; best = i; }
            }
            // From the nearest sample, along the line: each step projects onto the frame found, until it moves no more.
            float distance = _frames[best].Distance;
            TrackFrame f = _frames[best];
            for (int step = 0; step < 6; step++)
            {
                float along = Vector3.Dot(position - f.Position, f.Forward);
                distance += along;
                f = FrameAt(distance);
                if (MathF.Abs(along) < 1e-3f) break;
            }
            offset = Vector3.Dot(position - f.Position, f.Right);
            return f.Distance;
        }

        // ── The meshes ───────────────────────────────────────────────────────────────────────

        /// <summary>The triangles of the road or of the walls (<see cref="RoadPart"/>, <see cref="WallsPart"/>), for a collider.</summary>
        public (Vector3[] Vertices, uint[] Indices) Triangles(string part)
        {
            var p = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tg = new List<Vector4>(); var ix = new List<uint>();
            if (part == RoadPart) RoadMesh(p, n, uv, tg, ix); else WallMesh(p, n, uv, tg, ix);
            return (p.ToArray(), ix.ToArray());
        }

        /// <summary>
        /// The road: a ribbon over the centre line, its top facing the track's up. U goes across (0 at the left edge),
        /// V along (one unit every width). Positions, normals, UVs, tangents (along U) and the triangles (counter-clockwise
        /// seen from above, as every mesh of the engine).
        /// </summary>
        public void RoadMesh(List<Vector3> positions, List<Vector3> normals, List<Vector2> uvs, List<Vector4> tangents, List<uint> indices)
        {
            int count = _frames.Length;
            for (int i = 0; i <= count; i++)        // the last pair again at the first place, for V to run on
            {
                TrackFrame f = _frames[i % count];
                float d = i == count ? Length : f.Distance;
                float v = d / MathF.Max(2f * f.HalfWidth, 0.1f);
                foreach (float side in new[] { -1f, 1f })
                {
                    positions.Add(f.Position + f.Right * (side * f.HalfWidth));
                    normals.Add(f.Up);
                    uvs.Add(new Vector2(side < 0 ? 0f : 1f, v));
                    tangents.Add(new Vector4(f.Right, 1f));
                }
            }
            for (int i = 0; i < count; i++)
            {
                uint l0 = (uint)(2 * i), r0 = l0 + 1, l1 = l0 + 2, r1 = l0 + 3;
                indices.Add(l0); indices.Add(r0); indices.Add(l1);
                indices.Add(l1); indices.Add(r0); indices.Add(r1);
            }
        }

        /// <summary>
        /// The walls: at each edge a wall <see cref="WallHeight"/> high and <see cref="WallThickness"/> thick, standing
        /// outside the road — its face towards the road, its top, its face away. Nothing when the height is 0.
        /// </summary>
        public void WallMesh(List<Vector3> positions, List<Vector3> normals, List<Vector2> uvs, List<Vector4> tangents, List<uint> indices)
        {
            if (WallHeight <= 0f) return;
            int count = _frames.Length;
            foreach (float side in new[] { -1f, 1f })
            {
                // Three faces, each its own strip of vertices (each with its own normal).
                for (int face = 0; face < 3; face++)
                {
                    uint first = (uint)positions.Count;
                    for (int i = 0; i <= count; i++)
                    {
                        TrackFrame f = _frames[i % count];
                        Vector3 outward = f.Right * side;
                        Vector3 edge = f.Position + outward * f.HalfWidth;
                        Vector3 top = f.Up * WallHeight;
                        Vector3 thick = outward * WallThickness;
                        Vector3 a, b, n;
                        switch (face)
                        {
                            case 0: a = edge; b = edge + top; n = -outward; break;                   // towards the road
                            case 1: a = edge + top; b = edge + top + thick; n = f.Up; break;          // the top
                            default: a = edge + top + thick; b = edge + thick; n = outward; break;    // away from the road
                        }
                        float v = (i == count ? Length : f.Distance) / 2f;
                        positions.Add(a); normals.Add(n); uvs.Add(new Vector2(0f, v)); tangents.Add(new Vector4(f.Forward, 1f));
                        positions.Add(b); normals.Add(n); uvs.Add(new Vector2(1f, v)); tangents.Add(new Vector4(f.Forward, 1f));
                    }
                    for (int i = 0; i < count; i++)
                    {
                        uint a0 = first + (uint)(2 * i), b0 = a0 + 1, a1 = a0 + 2, b1 = a0 + 3;
                        // Wind each quad so its triangles face its normal (checked by the tests).
                        Vector3 nrm = normals[(int)a0];
                        Vector3 cross = Vector3.Cross(positions[(int)b0] - positions[(int)a0], positions[(int)a1] - positions[(int)a0]);
                        if (Vector3.Dot(cross, nrm) >= 0f) { indices.Add(a0); indices.Add(b0); indices.Add(a1); indices.Add(a1); indices.Add(b0); indices.Add(b1); }
                        else { indices.Add(a0); indices.Add(a1); indices.Add(b0); indices.Add(a1); indices.Add(b1); indices.Add(b0); }
                    }
                }
            }
        }
    }
}
