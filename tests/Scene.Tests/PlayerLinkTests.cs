using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using GameEditor.Framework.Core;
using GameEditor.Framework.Diagnostics;
using Xunit;

namespace Scene.Tests
{
    /// <summary>The player link (G11.1, examples/GameEditor/docs/QOL_DESIGN.md): its lines are JSON, a log line said before
    /// an editor connected reaches it, a second editor takes over and gets them again, ping answers pong.</summary>
    public sealed class PlayerLinkTests
    {
        [Fact]
        public void Strings_are_json_and_a_lines_kind_is_read()
        {
            string s = PlayerLink.Str("a \"q\" \\ \n\t\u0001é");
            Assert.Equal("a \"q\" \\ \n\t\u0001é", JsonDocument.Parse(s).RootElement.GetString());
            Assert.Equal("null", PlayerLink.Str(null));
            Assert.Equal("log", PlayerLink.TypeOf("{\"t\":\"log\",\"x\":\"\\\"t\\\":\\\"no\\\"\"}"));
            Assert.Equal("", PlayerLink.TypeOf("{\"x\":1}"));
        }

        static StreamReader Connect(out TcpClient c, out NetworkStream stream)
        {
            c = new TcpClient();
            c.Connect(IPAddress.Loopback, PlayerLink.Port);
            stream = c.GetStream();
            stream.ReadTimeout = 5000;
            return new StreamReader(stream, Encoding.UTF8);
        }

        static string Until(StreamReader r, Func<string, bool> want)
        {
            for (int i = 0; i < 2000; i++)
            {
                string? line = r.ReadLine();
                Assert.NotNull(line);
                Assert.True(JsonDocument.Parse(line!).RootElement.ValueKind == JsonValueKind.Object, line);
                if (want(line!)) return line!;
            }
            throw new Exception("not seen");
        }

        [Fact]
        public void An_editor_gets_the_hello_the_kept_lines_and_a_pong()
        {
            PlayerLink.Start("linktest");
            Assert.True(PlayerLink.Active);
            Assert.InRange(PlayerLink.Port, PlayerLink.BasePort, PlayerLink.BasePort + 9);
            Logger.Warning("before any editor \"quoted\"");

            var r = Connect(out var c, out var stream);
            using (c)
            {
                string hello = r.ReadLine()!;
                var h = JsonDocument.Parse(hello).RootElement;
                Assert.Equal("hello", h.GetProperty("t").GetString());
                Assert.Equal("linktest", h.GetProperty("app").GetString());
                Assert.Equal("desktop", h.GetProperty("platform").GetString());

                string kept = Until(r, l => l.Contains("before any editor"));
                var k = JsonDocument.Parse(kept).RootElement;
                Assert.Equal("W", k.GetProperty("l").GetString());
                Assert.Equal("before any editor \"quoted\"", k.GetProperty("x").GetString());

                Logger.Info("while connected");
                Until(r, l => l.Contains("while connected"));

                var ping = Encoding.UTF8.GetBytes("{\"t\":\"ping\"}\n");
                stream.Write(ping);
                string got = "";
                for (int i = 0; i < 200 && got.Length == 0; i++)
                {
                    PlayerLink.Update();   // the game's frame: the editor's lines handled
                    System.Threading.Thread.Sleep(10);
                    if (c.Available > 0) got = r.ReadLine()!;
                }
                Assert.Equal("pong", PlayerLink.TypeOf(got));
            }

            // A second editor: the hello and the kept lines again, from the start.
            var r2 = Connect(out var c2, out _);
            using (c2)
            {
                Assert.Equal("hello", PlayerLink.TypeOf(r2.ReadLine()!));
                Until(r2, l => l.Contains("before any editor"));
                Until(r2, l => l.Contains("while connected"));
            }
        }
    }
}
