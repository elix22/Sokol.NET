using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GameEditor.Framework.Core;
#if WEB
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
#else
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
#endif

namespace GameEditor.Framework.Diagnostics
{
    /// <summary>
    /// The player link (G11.1, the GameEditor's docs/QOL_DESIGN.md): a development build of a game talks to the editor -
    /// its log lines, and later its profiler frames, its scene and its tests - one JSON object a line, both ways.
    /// A native game (desktop, Android, iOS) listens on 127.0.0.1:<see cref="BasePort"/> (the next ports when taken) and
    /// the editor connects (on a phone through USB: adb forward, the iPhone's port proxy); a web game connects to the
    /// editor that served its page (a WebSocket on /link). Written and read by hand: no reflection (it runs in built games).
    /// </summary>
    public static class PlayerLink
    {
        public const int BasePort = 55000;
        const int Ports = 10, Kept = 1000;

        /// <summary>True once <see cref="Start"/> ran (a development build).</summary>
        public static bool Active { get; private set; }
        /// <summary>The port listened on (native), or 0.</summary>
        public static int Port { get; private set; }
        /// <summary>True while an editor is connected.</summary>
        public static bool Connected => _connected;
        /// <summary>A line the editor sent, on the game's thread (in <see cref="Update"/>): its "t" and the whole line.</summary>
        public static event Action<string, string>? Received;

        static string _hello = "";
        static volatile bool _connected;
        // What a connecting editor is sent first: the lines said so far (the last Kept), then what is said after.
        static readonly object _lock = new();
        static readonly List<string> _kept = new();
        static long _keptFirst;                               // the sequence number of _kept[0]
        static readonly ConcurrentQueue<string> _transient = new();   // sent only to a connected editor (frames)
        static readonly ConcurrentQueue<string> _in = new();

        /// <summary>desktop, android, ios or web: what the hello says.</summary>
        public static string Platform =>
#if WEB
            "web";
#else
            // An Android build is NativeAOT for linux-bionic, where OperatingSystem.IsAndroid() is false: its runtime id says it.
            OperatingSystem.IsAndroid() || System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier.Contains("bionic", StringComparison.Ordinal) ? "android"
            : OperatingSystem.IsIOS() ? "ios" : "desktop";
#endif

        /// <summary>Opens the link (once): the game's log lines go to an editor from now on.</summary>
        public static void Start(string app)
        {
            if (Active) return;
            Active = true;
            _hello = "{\"t\":\"hello\",\"app\":" + Str(app) + ",\"platform\":" + Str(Platform) +
                     ",\"os\":" + Str(System.Runtime.InteropServices.RuntimeInformation.OSDescription) + ",\"version\":1}";
            Logger.OnLog += (level, text) => Keep("{\"t\":\"log\",\"l\":\"" + (level == LogLevel.Error ? "E" : level == LogLevel.Warning ? "W" : "I") + "\",\"x\":" + Str(text) + "}");
            Open();
        }

        /// <summary>A line kept for an editor that connects later too (a log line).</summary>
        public static void Keep(string json)
        {
            if (!Active) return;
            lock (_lock)
            {
                _kept.Add(json);
                _seq++;
                int over = _kept.Count - Kept;
                if (over > 0) { _kept.RemoveRange(0, over); _keptFirst += over; }
            }
        }
        static long _seq;                                     // the sequence number the next kept line gets

        /// <summary>A line for the connected editor only (dropped when none is).</summary>
        public static void Send(string json)
        {
            if (Active && _connected) _transient.Enqueue(json);
        }

        /// <summary>Once a frame, on the game's thread: the editor's lines handed to <see cref="Received"/>.</summary>
        public static void Update()
        {
            if (!Active) return;
#if WEB
            Pump();
#endif
            while (_in.TryDequeue(out var line))
            {
                string t = TypeOf(line);
                if (t == "ping") { Send("{\"t\":\"pong\"}"); continue; }
#if WEB
                if (t == "replay") { _first.Enqueue(_hello); _sent = 0; continue; }   // an editor attached after the page connected
#endif
                try { Received?.Invoke(t, line); }
                catch (Exception e) { Logger.Error($"[PlayerLink] {t}: {e.Message}"); }
            }
        }

        // The lines kept from `from` on, and the next sequence number.
        static long TakeKept(long from, List<string> into)
        {
            lock (_lock)
            {
                long start = Math.Max(from, _keptFirst);
                for (long s = start; s < _seq; s++) into.Add(_kept[(int)(s - _keptFirst)]);
                return _seq;
            }
        }

        /// <summary>A JSON string literal of <paramref name="s"/>.</summary>
        public static string Str(string? s)
        {
            if (s == null) return "null";
            var sb = new StringBuilder(s.Length + 2).Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }

        /// <summary>A line's "t" (its kind), or "".</summary>
        public static string TypeOf(string line)
        {
            int i = line.IndexOf("\"t\":\"", StringComparison.Ordinal);
            if (i < 0) return "";
            int j = line.IndexOf('"', i + 5);
            return j < 0 ? "" : line.Substring(i + 5, j - i - 5);
        }

#if WEB
        // ── The web: a WebSocket to the editor that served the page ─────────────────────
        static ClientWebSocket? _ws;
        static Task? _connecting, _sending;
        static Task<WebSocketReceiveResult>? _receiving;
        static readonly byte[] _buffer = new byte[64 * 1024];
        static readonly StringBuilder _partial = new();
        static long _sent;
        static double _retryAt;

        static void Open() { }

        static void Pump()
        {
            double now = Environment.TickCount64 / 1000.0;
            if (_ws == null || _ws.State is WebSocketState.Closed or WebSocketState.Aborted)
            {
                _connected = false;
                if (now < _retryAt) return;
                _retryAt = now + 2.0;
                try
                {
                    var location = System.Runtime.InteropServices.JavaScript.JSHost.GlobalThis.GetPropertyAsJSObject("location");
                    string host = location?.GetPropertyAsString("host") ?? "";
                    if (host.Length == 0) return;
                    _ws?.Dispose();
                    _ws = new ClientWebSocket();
                    _connecting = _ws.ConnectAsync(new Uri("ws://" + host + "/link"), CancellationToken.None);
                    _sending = null; _receiving = null; _sent = 0;
                }
                catch { _ws = null; }
                return;
            }
            if (_connecting is { IsCompleted: false }) return;
            if (_ws.State != WebSocketState.Open) return;
            if (!_connected) { _connected = true; Queue(_hello); }
            if (_receiving == null)
                _receiving = _ws.ReceiveAsync(new ArraySegment<byte>(_buffer), CancellationToken.None);
            else if (_receiving.IsCompleted)
            {
                if (_receiving.IsCompletedSuccessfully)
                {
                    var r = _receiving.Result;
                    _partial.Append(Encoding.UTF8.GetString(_buffer, 0, r.Count));
                    if (r.EndOfMessage) { foreach (var l in _partial.ToString().Split('\n')) if (l.Length > 0) _in.Enqueue(l); _partial.Clear(); }
                }
                _receiving = null;
            }
            if (_sending is { IsCompleted: false }) return;
            var batch = new List<string>();
            while (_first.TryDequeue(out var f)) batch.Add(f);
            _sent = TakeKept(_sent, batch);
            while (_transient.TryDequeue(out var t)) batch.Add(t);
            if (batch.Count == 0) return;
            var bytes = Encoding.UTF8.GetBytes(string.Join("\n", batch));
            _sending = _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        static readonly ConcurrentQueue<string> _first = new();
        static void Queue(string line) => _first.Enqueue(line);
#else
        // ── Native: listen on the loopback; one editor at a time (a new one takes over) ─────────
        static TcpListener? _listener;
        static TcpClient? _client;

        static void Open()
        {
            for (int p = BasePort; p < BasePort + Ports; p++)
            {
                try { var l = new TcpListener(IPAddress.Loopback, p); l.Start(); _listener = l; Port = p; break; }
                catch (SocketException) { }
            }
            if (_listener == null) { Logger.Warning($"[PlayerLink] no port free in {BasePort}-{BasePort + Ports - 1}"); return; }
            Logger.Info($"[PlayerLink] listening on 127.0.0.1:{Port}");
            new Thread(Accept) { IsBackground = true, Name = "player-link" }.Start();
        }

        static void Accept()
        {
            while (true)
            {
                TcpClient c;
                try { c = _listener!.AcceptTcpClient(); }
                catch { return; }
                var old = Interlocked.Exchange(ref _client, c);
                try { old?.Close(); } catch { }
                new Thread(() => Serve(c)) { IsBackground = true, Name = "player-link-client" }.Start();
            }
        }

        static void Serve(TcpClient c)
        {
            try
            {
                c.NoDelay = true;
                var stream = c.GetStream();
                var writer = new StreamWriter(stream, new UTF8Encoding(false)) { NewLine = "\n", AutoFlush = false };
                writer.WriteLine(_hello);
                long sent = 0;
                var batch = new List<string>();
                var inbox = new StringBuilder();
                var buffer = new byte[16 * 1024];
                while (_client == c)
                {
                    _connected = true;
                    batch.Clear();
                    sent = TakeKept(sent, batch);
                    while (_transient.TryDequeue(out var t)) batch.Add(t);
                    foreach (var line in batch) writer.WriteLine(line);
                    if (batch.Count > 0) writer.Flush();
                    while (c.Available > 0)
                    {
                        int n = stream.Read(buffer, 0, buffer.Length);
                        if (n <= 0) break;
                        inbox.Append(Encoding.UTF8.GetString(buffer, 0, n));
                        string all = inbox.ToString();
                        int nl;
                        while ((nl = all.IndexOf('\n')) >= 0) { string line = all[..nl].TrimEnd('\r'); if (line.Length > 0) _in.Enqueue(line); all = all[(nl + 1)..]; }
                        inbox.Clear().Append(all);
                    }
                    // A closed editor shows as readable with nothing to read.
                    if (c.Client.Poll(0, SelectMode.SelectRead) && c.Available == 0) break;
                    Thread.Sleep(15);
                }
            }
            catch { }
            finally
            {
                if (Interlocked.CompareExchange(ref _client, null, c) == c) _connected = false;
                try { c.Close(); } catch { }
            }
        }
#endif
    }
}
