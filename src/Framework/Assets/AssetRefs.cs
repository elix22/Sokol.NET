using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace GameEditor.Framework.Assets
{
    /// <summary>
    /// The table of asset ids a file carries. A scene, a prefab, a material and the project settings name
    /// other files by key (their path below Assets, "Models/duck.obj"), as text, anywhere in their JSON. The
    /// file's root member <c>"assets"</c> holds the id of each file named:
    /// <code>"assets": { "Models/duck.obj": "9b1c…", "Prefabs/Enemy.prefab": "3f2a…" }</code>
    /// <see cref="Stamp"/> writes the table when the file is saved; <see cref="Resolve"/> reads it before the
    /// file is read, and puts the key a file has now in place of the key it had when the table was written.
    /// Both work on any JSON text whose root is an object, so a file of another kind can carry the table too.
    /// </summary>
    public static class AssetRefs
    {
        public const string Member = "assets";

        // ── Reading ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// The text with every key brought up to date: for each line of its table whose id is at another key
        /// now, every text value equal to the old key — alone, or before a '#' — gets the new one. All lines
        /// are applied at once, so two files that swapped names come out right. An id the database does not
        /// know leaves its key as it is. Returns the same text when nothing moved, which is the usual case.
        /// </summary>
        public static string Resolve(string json)
        {
            if (AssetDatabase.IsEmpty || json.IndexOf("\"" + Member + "\"", StringComparison.Ordinal) < 0) return json;
            Dictionary<string, string>? table = ReadTable(json);
            if (table == null) return json;

            Dictionary<string, string>? moved = null;
            foreach (var line in table)
                if (AssetDatabase.KeyOfId(line.Value) is { } now && now != line.Key)
                    (moved ??= new Dictionary<string, string>(StringComparer.Ordinal))[line.Key] = now;
            return moved == null ? json : Rewrite(json, moved);
        }

        /// <summary>The table a text carries (key → id), or null when it has none or is not a JSON object.</summary>
        public static Dictionary<string, string>? ReadTable(string json)
        {
            if (json.IndexOf("\"" + Member + "\"", StringComparison.Ordinal) < 0) return null;
            try
            {
                var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json), new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject) return null;
                while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
                {
                    bool mine = reader.ValueTextEquals(Member);
                    reader.Read();
                    if (!mine || reader.TokenType != JsonTokenType.StartObject) { reader.Skip(); continue; }

                    var table = new Dictionary<string, string>(StringComparer.Ordinal);
                    while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
                    {
                        string key = reader.GetString() ?? "";
                        reader.Read();
                        if (reader.TokenType == JsonTokenType.String && reader.GetString() is { Length: > 0 } id) table[key] = id;
                        else reader.Skip();
                    }
                    return table;
                }
            }
            catch (JsonException) { }
            return null;
        }

        /// <summary>
        /// The text with every key a file had before it was moved in this session replaced by the key it has now
        /// (<see cref="AssetDatabase.Current"/>) — for what is held in memory, which no table speaks for. Returns
        /// the same text when it holds no such key.
        /// </summary>
        public static string Current(string json)
        {
            if (!AssetDatabase.HasMoves) return json;
            Dictionary<string, string>? moved = null;
            try
            {
                using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                Find(doc.RootElement, ref moved);
            }
            catch (JsonException) { return json; }
            return moved == null ? json : Rewrite(json, moved);

            static void Find(JsonElement e, ref Dictionary<string, string>? moved)
            {
                switch (e.ValueKind)
                {
                    case JsonValueKind.Object:
                        foreach (var p in e.EnumerateObject()) Find(p.Value, ref moved);
                        break;
                    case JsonValueKind.Array:
                        foreach (var item in e.EnumerateArray()) Find(item, ref moved);
                        break;
                    case JsonValueKind.String:
                        string? value = e.GetString();
                        if (string.IsNullOrEmpty(value)) break;
                        int hash = value.IndexOf('#');
                        string key = hash < 0 ? value : value.Substring(0, hash);
                        if (key.Length == 0 || (moved != null && moved.ContainsKey(key))) break;
                        string now = AssetDatabase.Current(key);
                        if (now != key) (moved ??= new Dictionary<string, string>(StringComparer.Ordinal))[key] = now;
                        break;
                }
            }
        }

        // The text again with the moved keys replaced: in every text value, and in the names of the table's lines.
        private static string Rewrite(string json, Dictionary<string, string> moved)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            var buffer = new ArrayBufferWriter<byte>(bytes.Length + 256);
            try
            {
                var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                using var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
                int tableDepth = -1;        // the depth of the table's lines while the reader is inside the table
                bool nextIsTable = false;
                while (reader.Read())
                {
                    switch (reader.TokenType)
                    {
                        case JsonTokenType.StartObject:
                            writer.WriteStartObject();
                            if (nextIsTable) tableDepth = reader.CurrentDepth + 1;
                            nextIsTable = false;
                            break;
                        case JsonTokenType.EndObject:
                            writer.WriteEndObject();
                            if (tableDepth == reader.CurrentDepth + 1) tableDepth = -1;
                            break;
                        case JsonTokenType.StartArray: writer.WriteStartArray(); nextIsTable = false; break;
                        case JsonTokenType.EndArray: writer.WriteEndArray(); break;
                        case JsonTokenType.PropertyName:
                        {
                            string name = reader.GetString() ?? "";
                            if (reader.CurrentDepth == tableDepth && moved.TryGetValue(name, out string? renamed)) name = renamed;
                            nextIsTable = reader.CurrentDepth == 1 && name == Member;
                            writer.WritePropertyName(name);
                            break;
                        }
                        case JsonTokenType.String:
                        {
                            string value = reader.GetString() ?? "";
                            int hash = value.IndexOf('#');
                            if (hash < 0) writer.WriteStringValue(moved.TryGetValue(value, out string? now) ? now : value);
                            else writer.WriteStringValue(moved.TryGetValue(value.Substring(0, hash), out string? file) ? file + value.Substring(hash) : value);
                            nextIsTable = false;
                            break;
                        }
                        case JsonTokenType.Number:
                            writer.WriteRawValue(reader.ValueSpan, skipInputValidation: true);   // as written: a number is not read and formatted again
                            nextIsTable = false;
                            break;
                        case JsonTokenType.True: writer.WriteBooleanValue(true); nextIsTable = false; break;
                        case JsonTokenType.False: writer.WriteBooleanValue(false); nextIsTable = false; break;
                        case JsonTokenType.Null: writer.WriteNullValue(); nextIsTable = false; break;
                    }
                }
                writer.Flush();
            }
            catch (JsonException) { return json; }   // the reader of the file reports a text that is not JSON
            return Encoding.UTF8.GetString(buffer.WrittenSpan);
        }

        // ── Writing ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// The text with its table as it should be now: a line for every text value that is the key of a known
        /// asset, alone or before a '#', sorted by key. A line the text came with is kept while its key is still
        /// named and no file is known there — a file that is missing only for now keeps its id. A text that names
        /// no asset gets no table. Nothing else in the text changes: the table is the root's last member, laid
        /// out as the text is (on one line, or indented). Returns the same text when the table is right already.
        /// </summary>
        /// <param name="keep">For a file as it lies on disk, which may name files by keys they had before they were
        /// moved: every line it has stays as it is, and only lines that are missing are added.</param>
        public static string Stamp(string json, bool keep = false)
        {
            Dictionary<string, string>? old = ReadTable(json);
            if (AssetDatabase.IsEmpty && old == null) return json;

            var table = new SortedDictionary<string, string>(StringComparer.Ordinal);
            if (keep && old != null)
                foreach (var line in old) table[line.Key] = line.Value;
            try
            {
                using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                if (doc.RootElement.ValueKind != JsonValueKind.Object) return json;
                var notAssets = new HashSet<string>(StringComparer.Ordinal);
                foreach (var member in doc.RootElement.EnumerateObject())
                    if (!member.NameEquals(Member)) Collect(member.Value, old, table, notAssets);
            }
            catch (JsonException) { return json; }

            if (old == null ? table.Count == 0 : Same(old, table)) return json;
            return WithTable(json, table);
        }

        private static void Collect(JsonElement e, Dictionary<string, string>? old, SortedDictionary<string, string> table, HashSet<string> notAssets)
        {
            switch (e.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var p in e.EnumerateObject()) Collect(p.Value, old, table, notAssets);
                    break;
                case JsonValueKind.Array:
                    foreach (var item in e.EnumerateArray()) Collect(item, old, table, notAssets);
                    break;
                case JsonValueKind.String:
                    string? value = e.GetString();
                    if (string.IsNullOrEmpty(value)) break;
                    int hash = value.IndexOf('#');
                    string key = hash < 0 ? value : value.Substring(0, hash);
                    if (key.Length == 0 || table.ContainsKey(key) || notAssets.Contains(key)) break;
                    if (AssetDatabase.IdOfKey(key) is { } id) table[key] = id;
                    else if (old != null && old.TryGetValue(key, out string? kept)) table[key] = kept;
                    else notAssets.Add(key);
                    break;
            }
        }

        private static bool Same(Dictionary<string, string> a, SortedDictionary<string, string> b)
        {
            if (a.Count != b.Count) return false;
            foreach (var kv in b)
                if (!a.TryGetValue(kv.Key, out string? id) || id != kv.Value) return false;
            return true;
        }

        // The text with its table member taken out and the new one (when it has lines) put last. Every other
        // byte stays: the members are cut and joined where they are, not parsed and written again.
        private static string WithTable(string json, SortedDictionary<string, string> table)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            var members = new List<(int start, int end, bool mine)>();
            int open, close;
            try
            {
                var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject) return json;
                open = (int)reader.TokenStartIndex;
                while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
                {
                    int start = (int)reader.TokenStartIndex;
                    bool mine = reader.ValueTextEquals(Member);
                    reader.Read();
                    reader.Skip();
                    members.Add((start, (int)reader.BytesConsumed, mine));
                }
                if (reader.TokenType != JsonTokenType.EndObject) return json;
                close = (int)reader.TokenStartIndex;
            }
            catch (JsonException) { return json; }

            // How the text is laid out: what stands between '{' and the first member says it.
            string lead = members.Count > 0 ? Encoding.UTF8.GetString(bytes, open + 1, members[0].start - open - 1) : "";
            int lastBreak = lead.LastIndexOf('\n');
            bool indented = lastBreak >= 0;
            string newline = lead.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            string indent = indented ? lead.Substring(lastBreak + 1) : "";

            var sb = new StringBuilder(json.Length + 64 * table.Count + 32);
            sb.Append(Encoding.UTF8.GetString(bytes, 0, open + 1));
            bool any = false;
            int previousEnd = open + 1;
            for (int i = 0; i < members.Count; i++)
            {
                var m = members[i];
                if (!m.mine)
                {
                    // What stood before it — its comma, its line break — except for the first one kept, which takes the first member's.
                    sb.Append(any ? Encoding.UTF8.GetString(bytes, previousEnd, m.start - previousEnd) : lead);
                    sb.Append(Encoding.UTF8.GetString(bytes, m.start, m.end - m.start));
                    any = true;
                }
                previousEnd = m.end;
            }
            if (table.Count > 0)
            {
                if (any) sb.Append(',');
                if (indented) sb.Append(newline).Append(indent);
                sb.Append('"').Append(Member).Append(indented ? "\": {" : "\":{");
                bool first = true;
                foreach (var line in table)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    if (indented) sb.Append(newline).Append(indent).Append(indent);
                    sb.Append('"').Append(JsonEncodedText.Encode(line.Key).ToString()).Append(indented ? "\": \"" : "\":\"")
                      .Append(JsonEncodedText.Encode(line.Value).ToString()).Append('"');
                }
                if (indented) sb.Append(newline).Append(indent);
                sb.Append('}');
                any = true;
            }
            // What stood before '}' (a line break, in an indented text), then the rest as it was.
            sb.Append(Encoding.UTF8.GetString(bytes, previousEnd, close - previousEnd));
            sb.Append(Encoding.UTF8.GetString(bytes, close, bytes.Length - close));
            return sb.ToString();
        }
    }
}
