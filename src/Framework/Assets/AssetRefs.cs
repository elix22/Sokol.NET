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
    /// Scripts are named by their class, not by a file: a second table, <c>"scripts"</c>, holds the id of the file
    /// behind each class named in a <c>"Script"</c> or <c>"Scripts"</c> block (<c>"Type": "Enemy"</c>), so a script
    /// whose file and class were renamed is found too.
    /// </summary>
    public static class AssetRefs
    {
        public const string Member = "assets";
        public const string ScriptsMember = "scripts";

        // ── Reading ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// The text with every key brought up to date: for each line of its table whose id is at another key
        /// now, every text value equal to the old key — alone, or before a '#' — gets the new one. All lines
        /// are applied at once, so two files that swapped names come out right. An id the database does not
        /// know leaves its key as it is. Returns the same text when nothing moved, which is the usual case.
        /// </summary>
        public static string Resolve(string json)
        {
            if (AssetDatabase.IsEmpty) return json;
            Dictionary<string, string>? table = ReadTable(json);
            Dictionary<string, string>? scripts = ReadTable(json, ScriptsMember);
            if (table == null && scripts == null) return json;

            Dictionary<string, string>? moved = null, renamed = null;
            if (table != null)
                foreach (var line in table)
                    if (AssetDatabase.KeyOfId(line.Value) is { } now && now != line.Key)
                        (moved ??= new Dictionary<string, string>(StringComparer.Ordinal))[line.Key] = now;
            if (scripts != null)
                foreach (var line in scripts)
                    if (AssetDatabase.ScriptOfId(line.Value) is { } now && now != line.Key)
                        (renamed ??= new Dictionary<string, string>(StringComparer.Ordinal))[line.Key] = now;
            return moved == null && renamed == null ? json : Rewrite(json, moved, renamed);
        }

        /// <summary>The table a text carries (key → id), or null when it has none or is not a JSON object.</summary>
        public static Dictionary<string, string>? ReadTable(string json) => ReadTable(json, Member);

        /// <summary>One of the two tables a text carries: <see cref="Member"/> (key → id) or <see cref="ScriptsMember"/> (class → id).</summary>
        public static Dictionary<string, string>? ReadTable(string json, string member)
        {
            if (json.IndexOf("\"" + member + "\"", StringComparison.Ordinal) < 0) return null;
            try
            {
                var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json), new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject) return null;
                while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
                {
                    bool mine = reader.ValueTextEquals(member);
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
            Dictionary<string, string>? moved = null, renamed = null;
            try
            {
                using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                Find(doc.RootElement, ref moved, ref renamed);
            }
            catch (JsonException) { return json; }
            return moved == null && renamed == null ? json : Rewrite(json, moved, renamed);

            static void Find(JsonElement e, ref Dictionary<string, string>? moved, ref Dictionary<string, string>? renamed)
            {
                switch (e.ValueKind)
                {
                    case JsonValueKind.Object:
                        foreach (var p in e.EnumerateObject())
                        {
                            foreach (string type in ScriptTypes(p))
                            {
                                string named = AssetDatabase.CurrentScript(type);
                                if (named != type) (renamed ??= new Dictionary<string, string>(StringComparer.Ordinal))[type] = named;
                            }
                            Find(p.Value, ref moved, ref renamed);
                        }
                        break;
                    case JsonValueKind.Array:
                        foreach (var item in e.EnumerateArray()) Find(item, ref moved, ref renamed);
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

        // The classes a member names when it is a "Script" block ({"Type": …}) or a "Scripts" list of such blocks.
        private static IEnumerable<string> ScriptTypes(JsonProperty member)
        {
            if (member.NameEquals("Script") && member.Value.ValueKind == JsonValueKind.Object)
            {
                if (member.Value.TryGetProperty("Type", out var t) && t.ValueKind == JsonValueKind.String && t.GetString() is { Length: > 0 } type) yield return type;
            }
            else if (member.NameEquals("Scripts") && member.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in member.Value.EnumerateArray())
                    if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("Type", out var t) && t.ValueKind == JsonValueKind.String
                        && t.GetString() is { Length: > 0 } type) yield return type;
            }
        }

        // The text again with the moved keys replaced — in every text value, and in the names of the table's lines
        // — and the renamed classes, in the "Type" of script blocks and in the names of the script table's lines.
        private static string Rewrite(string json, Dictionary<string, string>? moved, Dictionary<string, string>? renamed)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            var buffer = new ArrayBufferWriter<byte>(bytes.Length + 256);
            try
            {
                var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                using var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
                // What each open object or array is the value of: a member's name; "" for an element of an array, and for the root.
                var holders = new List<string>();
                string member = "";   // the name the next value belongs to
                while (reader.Read())
                {
                    switch (reader.TokenType)
                    {
                        case JsonTokenType.StartObject: writer.WriteStartObject(); holders.Add(member); member = ""; break;
                        case JsonTokenType.StartArray: writer.WriteStartArray(); holders.Add(member); member = ""; break;
                        case JsonTokenType.EndObject: writer.WriteEndObject(); holders.RemoveAt(holders.Count - 1); member = ""; break;
                        case JsonTokenType.EndArray: writer.WriteEndArray(); holders.RemoveAt(holders.Count - 1); member = ""; break;
                        case JsonTokenType.PropertyName:
                        {
                            string name = reader.GetString() ?? "";
                            if (holders.Count == 2)   // a line of one of the root's tables
                            {
                                if (holders[1] == Member && moved != null && moved.TryGetValue(name, out string? key)) name = key;
                                else if (holders[1] == ScriptsMember && renamed != null && renamed.TryGetValue(name, out string? type)) name = type;
                            }
                            member = name;
                            writer.WritePropertyName(name);
                            break;
                        }
                        case JsonTokenType.String:
                        {
                            string value = reader.GetString() ?? "";
                            if (renamed != null && member == "Type" && InScriptBlock(holders) && renamed.TryGetValue(value, out string? type)) value = type;
                            else if (moved != null)
                            {
                                int hash = value.IndexOf('#');
                                if (hash < 0) { if (moved.TryGetValue(value, out string? now)) value = now; }
                                else if (moved.TryGetValue(value.Substring(0, hash), out string? file)) value = file + value.Substring(hash);
                            }
                            writer.WriteStringValue(value);
                            member = "";
                            break;
                        }
                        case JsonTokenType.Number:
                            writer.WriteRawValue(reader.ValueSpan, skipInputValidation: true);   // as written: a number is not read and formatted again
                            member = "";
                            break;
                        case JsonTokenType.True: writer.WriteBooleanValue(true); member = ""; break;
                        case JsonTokenType.False: writer.WriteBooleanValue(false); member = ""; break;
                        case JsonTokenType.Null: writer.WriteNullValue(); member = ""; break;
                    }
                }
                writer.Flush();
            }
            catch (JsonException) { return json; }   // the reader of the file reports a text that is not JSON
            return Encoding.UTF8.GetString(buffer.WrittenSpan);

            // The innermost open object is a "Script" block, or an element of a "Scripts" list.
            static bool InScriptBlock(List<string> holders)
            {
                int n = holders.Count;
                return n >= 1 && (holders[n - 1] == "Script" || (holders[n - 1] == "" && n >= 2 && holders[n - 2] == "Scripts"));
            }
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
            Dictionary<string, string>? oldScripts = ReadTable(json, ScriptsMember);
            if (AssetDatabase.IsEmpty && old == null && oldScripts == null) return json;

            var table = new SortedDictionary<string, string>(StringComparer.Ordinal);
            var scripts = new SortedDictionary<string, string>(StringComparer.Ordinal);
            if (keep)
            {
                if (old != null) foreach (var line in old) table[line.Key] = line.Value;
                if (oldScripts != null) foreach (var line in oldScripts) scripts[line.Key] = line.Value;
            }
            try
            {
                using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                if (doc.RootElement.ValueKind != JsonValueKind.Object) return json;
                var notAssets = new HashSet<string>(StringComparer.Ordinal);
                foreach (var member in doc.RootElement.EnumerateObject())
                    if (!member.NameEquals(Member) && !member.NameEquals(ScriptsMember)) Collect(member, old, table, notAssets, oldScripts, scripts);
            }
            catch (JsonException) { return json; }

            if ((old == null ? table.Count == 0 : Same(old, table)) && (oldScripts == null ? scripts.Count == 0 : Same(oldScripts, scripts))) return json;
            return WithTables(json, table, scripts);
        }

        private static void Collect(JsonProperty member, Dictionary<string, string>? old, SortedDictionary<string, string> table, HashSet<string> notAssets,
                                    Dictionary<string, string>? oldScripts, SortedDictionary<string, string> scripts)
        {
            foreach (string type in ScriptTypes(member))
            {
                if (scripts.ContainsKey(type)) continue;
                if (AssetDatabase.IdOfScript(type) is { } id) scripts[type] = id;
                else if (oldScripts != null && oldScripts.TryGetValue(type, out string? kept)) scripts[type] = kept;
            }
            Collect(member.Value, old, table, notAssets, oldScripts, scripts);
        }

        private static void Collect(JsonElement e, Dictionary<string, string>? old, SortedDictionary<string, string> table, HashSet<string> notAssets,
                                    Dictionary<string, string>? oldScripts, SortedDictionary<string, string> scripts)
        {
            switch (e.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var p in e.EnumerateObject()) Collect(p, old, table, notAssets, oldScripts, scripts);
                    break;
                case JsonValueKind.Array:
                    foreach (var item in e.EnumerateArray()) Collect(item, old, table, notAssets, oldScripts, scripts);
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

        // The text with its table members taken out and the new ones (those that have lines) put last. Every other
        // byte stays: the members are cut and joined where they are, not parsed and written again.
        private static string WithTables(string json, SortedDictionary<string, string> table, SortedDictionary<string, string> scripts)
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
                    bool mine = reader.ValueTextEquals(Member) || reader.ValueTextEquals(ScriptsMember);
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
            foreach (var (name, lines) in new[] { (Member, table), (ScriptsMember, scripts) })
            {
                if (lines.Count == 0) continue;
                if (any) sb.Append(',');
                if (indented) sb.Append(newline).Append(indent);
                sb.Append('"').Append(name).Append(indented ? "\": {" : "\":{");
                bool first = true;
                foreach (var line in lines)
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
