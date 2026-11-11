using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using GameEditor.Framework.Core;

namespace GameEditor.Framework.Assets
{
    /// <summary>What <see cref="AssetDatabase.Open"/> did to bring a project's <c>.meta</c> files in order.</summary>
    public sealed class AssetScanReport
    {
        /// <summary>Files that had no <c>.meta</c> and got one.</summary>
        public int Created;
        /// <summary>Files whose id another file had too (a file copied with its <c>.meta</c>): they got a new one.</summary>
        public int Duplicates;
        /// <summary>Files that took the <c>.meta</c> a moved or renamed file left behind.</summary>
        public int Adopted;
        /// <summary><c>.meta</c> files whose file is gone, removed.</summary>
        public int Orphans;
        /// <summary>The project folder could not be written: the new ids hold until the project is closed.</summary>
        public bool NotSaved;
        /// <summary>One line for each thing worth telling the user.</summary>
        public readonly List<string> Notes = new();
    }

    /// <summary>
    /// The assets of the project by id. Every file under <c>Assets</c>, and every script under <c>Source</c>, has
    /// an id kept in a <c>.meta</c> file beside it (<c>duck.obj.meta</c>: <c>{ "meta": 1, "id": "…" }</c>), so a
    /// file that is renamed or moved is still the same asset. This class is the list of ids and where their files
    /// are; <see cref="AssetRefs"/> uses it to bring the paths a scene, prefab or material names up to date.
    /// In an editor it is filled from the <c>.meta</c> files (<see cref="Open"/>); in a built game from the index
    /// the editor wrote (<see cref="LoadIndex"/>), which lists only the files that moved. Paths are relative to
    /// the project folder with forward slashes ("Assets/Models/duck.obj"); a file under Assets also has a key,
    /// its path below Assets ("Models/duck.obj"), which is what scenes store. Main thread only.
    /// </summary>
    public static class AssetDatabase
    {
        public const string MetaExtension = ".meta";
        /// <summary>
        /// One of the app's embedded files (<see cref="EmbeddedFiles"/>): for a built game, where the files are that
        /// some scene still names by an older path.
        /// </summary>
        public const string IndexFileName = "assets.index.json";

        private const string AssetsPrefix = "Assets/";
        private const string SourcePrefix = "Source/";

        private static readonly Dictionary<string, string> _pathOfId = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> _idOfPath = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> _idOfKey = new(StringComparer.Ordinal);
        // Keys files had before they were moved since the project was opened → their id.
        private static readonly Dictionary<string, string> _movedFrom = new(StringComparer.Ordinal);
        // Scripts by class: a script's class is its file's name ("Source/AI/Enemy.cs" = Enemy). Null: two files have the name.
        private static readonly Dictionary<string, string?> _idOfScript = new(StringComparer.Ordinal);
        // The names scripts had before they were renamed since the project was opened → their id.
        private static readonly Dictionary<string, string> _scriptMovedFrom = new(StringComparer.Ordinal);
        // In a game: the class a renamed script has now, by its id (from the index).
        private static readonly Dictionary<string, string> _scriptOfId = new(StringComparer.Ordinal);
        // Whether a script file declares the class of its name, by the file's time.
        private static readonly Dictionary<string, (DateTime time, bool declares)> _declares = new(StringComparer.Ordinal);
        private static string? _root;

        /// <summary>A file moved: its old and its new path. Raised once for each file of a folder that moved.</summary>
        public static event Action<string, string>? Moved;

        /// <summary>The project folder the database was opened on; null in a game and in a test that fills it by hand.</summary>
        public static string? Root => _root;

        /// <summary>True when a file was moved since the project was opened: a key from before may still be held somewhere.</summary>
        public static bool HasMoves => _movedFrom.Count > 0 || _scriptMovedFrom.Count > 0;

        /// <summary>True when no id is known: nothing a file names can have moved.</summary>
        public static bool IsEmpty => _pathOfId.Count == 0 && _scriptOfId.Count == 0;

        public static int Count => _pathOfId.Count;

        /// <summary>Every id with its file's path.</summary>
        public static IEnumerable<KeyValuePair<string, string>> All => _pathOfId;

        /// <summary>Forgets everything (another project is opened).</summary>
        public static void Clear()
        {
            _pathOfId.Clear();
            _idOfPath.Clear();
            _idOfKey.Clear();
            _movedFrom.Clear();
            _idOfScript.Clear();
            _scriptMovedFrom.Clear();
            _scriptOfId.Clear();
            _declares.Clear();
            _root = null;
        }

        // ── Lookups ─────────────────────────────────────────────────────────────────────

        public static string? IdOfPath(string path) => _idOfPath.TryGetValue(path, out string? id) ? id : null;
        public static string? PathOfId(string id) => _pathOfId.TryGetValue(id, out string? path) ? path : null;

        /// <summary>The id of the file under Assets with this key ("Models/duck.obj"), or null.</summary>
        public static string? IdOfKey(string key) => _idOfKey.TryGetValue(key, out string? id) ? id : null;

        /// <summary>The key of the file with this id, or null when the id is unknown or its file is not under Assets.</summary>
        public static string? KeyOfId(string id)
            => _pathOfId.TryGetValue(id, out string? path) ? KeyOfPath(path) : null;

        /// <summary>The path below Assets of a project-relative path, or null for a file elsewhere.</summary>
        public static string? KeyOfPath(string path)
            => path.StartsWith(AssetsPrefix, StringComparison.Ordinal) ? path.Substring(AssetsPrefix.Length) : null;

        /// <summary>
        /// The key a file has now, for a key it had before it was moved in this session; any other key comes back
        /// as it is. What follows a '#' (a glTF primitive, a material of a file) is kept.
        /// </summary>
        public static string Current(string key)
        {
            if (_movedFrom.Count == 0 || string.IsNullOrEmpty(key)) return key;
            int hash = key.IndexOf('#');
            string file = hash < 0 ? key : key.Substring(0, hash);
            if (_idOfKey.ContainsKey(file)) return key;
            if (!_movedFrom.TryGetValue(file, out string? id) || KeyOfId(id) is not { } now) return key;
            return hash < 0 ? now : now + key.Substring(hash);
        }

        // ── Filling it by hand, and in a game ───────────────────────────────────────────

        /// <summary>
        /// Says where the file with an id is, replacing what was known for the id and for the path. No file is
        /// touched: for a game's index, and for tests.
        /// </summary>
        public static void Set(string id, string path)
        {
            if (_pathOfId.TryGetValue(id, out string? old)) Forget(old);
            if (_idOfPath.TryGetValue(path, out string? otherId)) _pathOfId.Remove(otherId);
            _pathOfId[id] = path;
            _idOfPath[path] = id;
            if (KeyOfPath(path) is { } key) _idOfKey[key] = id;
            if (ScriptNameOf(path) is { } script) _idOfScript[script] = _idOfScript.TryGetValue(script, out string? other) && other != id ? null : id;
        }

        private static void Forget(string path)
        {
            _idOfPath.Remove(path);
            if (KeyOfPath(path) is { } key) _idOfKey.Remove(key);
            if (ScriptNameOf(path) is { } script)
            {
                // The name belongs to the files that still have it: one, or more than one (then to none).
                _idOfScript.Remove(script);
                foreach (var kv in _idOfPath)
                    if (ScriptNameOf(kv.Key) == script) _idOfScript[script] = _idOfScript.ContainsKey(script) ? null : kv.Value;
            }
        }

        // ── Scripts ─────────────────────────────────────────────────────────────────────
        //
        // A scene names a script by its class ("Type": "Enemy"), and a game finds the class by that name. A
        // script's class is its file's name; the file has an id like any asset, so a scene that also holds that
        // id (AssetRefs) finds the script after its file and class were renamed.

        /// <summary>The class of a script file ("Source/AI/Enemy.cs" = "Enemy"), or null for a path that is not one.</summary>
        public static string? ScriptNameOf(string path)
            => path.StartsWith(SourcePrefix, StringComparison.Ordinal) && path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                ? Path.GetFileNameWithoutExtension(path) : null;

        /// <summary>The id of the script file of a class, or null when there is none, or more than one file of that name.</summary>
        public static string? IdOfScript(string type) => _idOfScript.TryGetValue(type, out string? id) ? id : null;

        /// <summary>True when one or more script files have this name.</summary>
        public static bool IsScriptName(string type) => _idOfScript.ContainsKey(type);

        /// <summary>
        /// The class of the script file with an id: its file's name, when the file declares a class of that name
        /// (in a game: what the index says). Null when the id is unknown or its file is no such script.
        /// </summary>
        public static string? ScriptOfId(string id)
        {
            if (_scriptOfId.TryGetValue(id, out string? indexed)) return indexed;
            if (!_pathOfId.TryGetValue(id, out string? path) || ScriptNameOf(path) is not { } name) return null;
            if (_idOfScript.TryGetValue(name, out string? owner) && owner != id) return null;   // two files of the name
            return _root == null || Declares(path, name) ? name : null;
        }

        /// <summary>
        /// The class a script has now, for a name it had before it was renamed in this session; any other name
        /// comes back as it is.
        /// </summary>
        public static string CurrentScript(string type)
        {
            if (_scriptMovedFrom.Count == 0 || string.IsNullOrEmpty(type) || _idOfScript.ContainsKey(type)) return type;
            return _scriptMovedFrom.TryGetValue(type, out string? id) && ScriptOfId(id) is { } now ? now : type;
        }

        /// <summary>True when a C# text has the words <c>class Name</c> (the name not part of a longer one).</summary>
        public static bool DeclaresClass(string code, string name)
        {
            for (int at = code.IndexOf(name, StringComparison.Ordinal); at >= 0; at = code.IndexOf(name, at + 1, StringComparison.Ordinal))
            {
                int end = at + name.Length;
                if (end < code.Length && (char.IsLetterOrDigit(code[end]) || code[end] == '_')) continue;
                int before = at;
                while (before > 0 && char.IsWhiteSpace(code[before - 1])) before--;
                if (before == at || before < 5 || string.CompareOrdinal(code, before - 5, "class", 0, 5) != 0) continue;
                if (before == 5 || !(char.IsLetterOrDigit(code[before - 6]) || code[before - 6] == '_')) return true;
            }
            return false;
        }

        private static bool Declares(string path, string name)
        {
            try
            {
                string file = Abs(path);
                DateTime time = File.GetLastWriteTimeUtc(file);
                if (_declares.TryGetValue(path, out var known) && known.time == time) return known.declares;
                bool declares = DeclaresClass(File.ReadAllText(file), name);
                _declares[path] = (time, declares);
                return declares;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
        }

        /// <summary>
        /// Takes the index a built game ships with (<see cref="IndexFileName"/>): for each id listed, where its
        /// file is now. Returns false for a text that is not an index.
        /// </summary>
        public static bool LoadIndex(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Object) return false;
                if (doc.RootElement.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Object)
                    foreach (var p in assets.EnumerateObject())
                        if (p.Value.ValueKind == JsonValueKind.String && p.Value.GetString() is { Length: > 0 } key)
                            Set(p.Name, AssetsPrefix + key);
                if (doc.RootElement.TryGetProperty("scripts", out var scripts) && scripts.ValueKind == JsonValueKind.Object)
                    foreach (var p in scripts.EnumerateObject())
                        if (p.Value.ValueKind == JsonValueKind.String && p.Value.GetString() is { Length: > 0 } type)
                            _scriptOfId[p.Name] = type;
                return true;
            }
            catch (JsonException) { return false; }
        }

        // ── A project's files ───────────────────────────────────────────────────────────

        /// <summary>True for a path that is given an id: a file under Assets, or a script under Source.</summary>
        public static bool IsAssetPath(string path)
        {
            string name = path.Substring(path.LastIndexOf('/') + 1);
            if (name.Length == 0 || name[0] == '.' || name.EndsWith(MetaExtension, StringComparison.OrdinalIgnoreCase)) return false;
            foreach (string part in path.Split('/'))
                if (part.Length > 0 && part[0] == '.') return false;   // inside a hidden folder
            if (path.StartsWith(AssetsPrefix, StringComparison.Ordinal))
                return path != AssetsPrefix + ConfigManager.ConfigFileName;
            if (path.StartsWith(SourcePrefix, StringComparison.Ordinal))
                return name.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                    && !name.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)
                    && !path.Contains("/bin/", StringComparison.Ordinal) && !path.Contains("/obj/", StringComparison.Ordinal);
            return false;
        }

        /// <summary>
        /// Reads the <c>.meta</c> files of a project and brings them in order: a file with none gets one; of two
        /// files with one id the second gets a new one; a file with none takes the <c>.meta</c> a moved or renamed
        /// file left behind when it is clear which one that is; a <c>.meta</c> whose file is gone is removed.
        /// </summary>
        public static AssetScanReport Open(string projectFolder)
        {
            Clear();
            _root = Path.GetFullPath(projectFolder);
            return Sync();
        }

        /// <summary>
        /// Looks at the open project's files again and takes in what changed since: a new file gets its id; a file
        /// that moved — with its <c>.meta</c>, or without it when it is clear which file it is — keeps its id
        /// (<see cref="Moved"/> is raised for it); a file that is gone is forgotten. A file that is where it was is
        /// not read again.
        /// </summary>
        public static AssetScanReport Sync()
        {
            var report = new AssetScanReport();
            if (_root == null) return report;

            var files = new List<string>();
            var metas = new HashSet<string>(StringComparer.Ordinal);   // by the path of the file they belong to
            foreach (string top in new[] { "Assets", "Source" })
            {
                string dir = Path.Combine(_root, top);
                if (!Directory.Exists(dir)) continue;
                foreach (string abs in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                {
                    string rel = Path.GetRelativePath(_root, abs).Replace('\\', '/');
                    if (rel.EndsWith(MetaExtension, StringComparison.OrdinalIgnoreCase))
                    {
                        string owner = rel.Substring(0, rel.Length - MetaExtension.Length);
                        if (IsAssetPath(owner)) metas.Add(owner);
                    }
                    else if (IsAssetPath(rel)) files.Add(rel);
                }
            }
            files.Sort(StringComparer.Ordinal);
            var fileSet = new HashSet<string>(files, StringComparer.Ordinal);

            // Known files that are not where they were: moved, or gone.
            var vanished = new Dictionary<string, string>(StringComparer.Ordinal);   // id → the path it had
            foreach (var kv in _pathOfId)
                if (!fileSet.Contains(kv.Value)) vanished[kv.Key] = kv.Value;
            var moves = new List<(string, string)>();

            // Files not known yet, with a .meta: the first in path order keeps an id two of them have.
            var without = new List<string>();
            foreach (string file in files)
            {
                if (_idOfPath.TryGetValue(file, out string? knownId))
                {
                    if (!metas.Contains(file) && !WriteMeta(file, knownId)) report.NotSaved = true;   // its .meta was taken away: written again
                    continue;
                }
                if (!metas.Contains(file)) { without.Add(file); continue; }
                string? id = ReadMeta(file);
                if (id == null) { without.Add(file); metas.Remove(file); continue; }   // not a .meta this version reads: written anew
                if (vanished.Remove(id, out string? was))
                {
                    moves.Add((was, file));   // the file, moved with its .meta
                    continue;
                }
                if (_pathOfId.TryGetValue(id, out string? first))
                {
                    string fresh = NewId();
                    report.Duplicates++;
                    report.Notes.Add($"{file} had the id of {first} (a file copied with its .meta): it was given a new one.");
                    if (!ReplaceId(file, id, fresh)) report.NotSaved = true;
                    id = fresh;
                }
                Set(id, file);
            }

            // .meta files whose file is gone, and files with none: a moved or a renamed file, when it is clear which.
            var orphans = new List<string>();
            foreach (string owner in metas)
                if (!fileSet.Contains(owner)) orphans.Add(owner);
            orphans.Sort(StringComparer.Ordinal);
            foreach (var (orphan, file) in Pair(orphans, without))
            {
                string? id = ReadMeta(orphan);
                if (id == null) continue;
                bool known = _pathOfId.TryGetValue(id, out string? at);
                if (known && (at != orphan || !vanished.ContainsKey(id))) continue;   // the id is another file's
                try { File.Move(MetaFile(orphan), MetaFile(file)); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { report.NotSaved = true; }
                if (known) { vanished.Remove(id); moves.Add((orphan, file)); }
                else Set(id, file);
                orphans.Remove(orphan);
                without.Remove(file);
                report.Adopted++;
                report.Notes.Add($"{file} is taken as {orphan}, moved or renamed without its .meta.");
            }
            foreach (string orphan in orphans)
            {
                try { File.Delete(MetaFile(orphan)); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { report.NotSaved = true; continue; }
                report.Orphans++;
                report.Notes.Add($"{orphan}{MetaExtension} had no file beside it and was removed.");
            }
            foreach (var gone in vanished)
            {
                Forget(gone.Value);
                _pathOfId.Remove(gone.Key);
            }

            foreach (string file in without)
            {
                string id = NewId();
                if (!WriteMeta(file, id)) report.NotSaved = true;
                Set(id, file);
                report.Created++;
            }
            if (report.NotSaved)
                report.Notes.Add("The project folder could not be written: the new asset ids hold until the project is closed.");

            foreach (var (from, to) in moves) Rename(from, to, raise: false);
            foreach (var (from, to) in moves) Moved?.Invoke(from, to);
            return report;
        }

        // An orphan .meta and a file with none belong together when exactly one of each has that file name
        // (moved to another folder), or they are the only two of one extension in one folder (renamed there).
        private static List<(string orphan, string file)> Pair(List<string> orphans, List<string> without)
        {
            var pairs = new List<(string, string)>();
            if (orphans.Count == 0 || without.Count == 0) return pairs;
            var usedOrphans = new HashSet<string>(StringComparer.Ordinal);
            var usedFiles = new HashSet<string>(StringComparer.Ordinal);

            foreach (Func<string, string> group in new Func<string, string>[] { NameOf, FolderAndExtensionOf })
            {
                var byOrphan = new Dictionary<string, List<string>>(StringComparer.Ordinal);
                var byFile = new Dictionary<string, List<string>>(StringComparer.Ordinal);
                foreach (string o in orphans) if (!usedOrphans.Contains(o)) Add(byOrphan, group(o), o);
                foreach (string f in without) if (!usedFiles.Contains(f)) Add(byFile, group(f), f);
                foreach (var kv in byOrphan)
                {
                    if (kv.Value.Count != 1 || !byFile.TryGetValue(kv.Key, out var match) || match.Count != 1) continue;
                    pairs.Add((kv.Value[0], match[0]));
                    usedOrphans.Add(kv.Value[0]);
                    usedFiles.Add(match[0]);
                }
            }
            return pairs;

            static void Add(Dictionary<string, List<string>> map, string k, string v)
            {
                if (!map.TryGetValue(k, out var list)) map[k] = list = new List<string>();
                list.Add(v);
            }
            static string NameOf(string path) => path.Substring(path.LastIndexOf('/') + 1);
            static string FolderAndExtensionOf(string path)
                => path.Substring(0, path.LastIndexOf('/') + 1) + "*" + Path.GetExtension(path).ToLowerInvariant();
        }

        /// <summary>
        /// The id of a file of the open project, giving it a <c>.meta</c> when it has none — for a file that was
        /// just made. Null for a path that gets no id, or a file that is not there.
        /// </summary>
        public static string? Register(string path)
        {
            if (_root == null || !IsAssetPath(path)) return null;
            if (_idOfPath.TryGetValue(path, out string? known)) return known;
            if (!File.Exists(Abs(path))) return null;
            string id = ReadMeta(path) is { } read && !_pathOfId.ContainsKey(read) ? read : NewId();
            if (ReadMeta(path) != id) WriteMeta(path, id);
            Set(id, path);
            return id;
        }

        /// <summary>
        /// Moves or renames a file with its <c>.meta</c>, or a folder with everything in it. Nothing else is
        /// written: what names the file holds its id. Returns an error, or null.
        /// </summary>
        public static string? Move(string from, string to)
        {
            if (_root == null) return "no project is open";
            from = from.TrimEnd('/'); to = to.TrimEnd('/');
            if (from == to) return null;
            string absFrom = Abs(from), absTo = Abs(to);
            bool folder = Directory.Exists(absFrom);
            if (!folder && !File.Exists(absFrom)) return $"{from} was not found";
            // On a file system that does not tell cases apart, a new spelling of the same name is not "taken".
            bool sameFile = string.Equals(from, to, StringComparison.OrdinalIgnoreCase)
                            && string.Equals(Path.GetFullPath(absFrom), Path.GetFullPath(absTo), StringComparison.OrdinalIgnoreCase)
                            && (folder ? Directory.Exists(absTo) : File.Exists(absTo));
            if (!sameFile && (File.Exists(absTo) || Directory.Exists(absTo))) return $"{to} already exists";
            if (folder && (to + "/").StartsWith(from + "/", StringComparison.Ordinal)) return $"{from} cannot be moved into itself";

            var moved = new List<(string, string)>();
            try
            {
                string? parent = Path.GetDirectoryName(absTo);
                if (parent != null) Directory.CreateDirectory(parent);
                if (folder)
                {
                    Directory.Move(absFrom, absTo);
                    string prefix = from + "/";
                    foreach (var kv in _idOfPath)
                        if (kv.Key.StartsWith(prefix, StringComparison.Ordinal))
                            moved.Add((kv.Key, to + "/" + kv.Key.Substring(prefix.Length)));
                }
                else
                {
                    if (!sameFile && File.Exists(MetaFile(to)) && !File.Exists(absTo)) File.Delete(MetaFile(to));   // a .meta left there by a file that is gone
                    File.Move(absFrom, absTo);
                    if (File.Exists(MetaFile(from))) File.Move(MetaFile(from), MetaFile(to));
                    if (_idOfPath.ContainsKey(from)) moved.Add((from, to));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return $"{from} could not be moved: {ex.Message}";
            }

            moved.Sort((a, b) => string.CompareOrdinal(a.Item1, b.Item1));
            foreach (var (oldPath, newPath) in moved) Rename(oldPath, newPath, raise: false);
            if (!folder && moved.Count == 0) Register(to);   // a file the database did not know yet
            foreach (var (oldPath, newPath) in moved) Moved?.Invoke(oldPath, newPath);
            return null;
        }

        /// <summary>
        /// Tells the database a file is at another path now, without touching a file (the files were moved by
        /// other means, or there are none: a test).
        /// </summary>
        public static void Rename(string oldPath, string newPath) => Rename(oldPath, newPath, raise: true);

        private static void Rename(string oldPath, string newPath, bool raise)
        {
            if (!_idOfPath.TryGetValue(oldPath, out string? id)) return;
            Forget(oldPath);
            if (_idOfPath.TryGetValue(newPath, out string? replaced)) _pathOfId.Remove(replaced);
            _pathOfId[id] = newPath;
            _idOfPath[newPath] = id;
            if (KeyOfPath(newPath) is { } newKey)
            {
                _idOfKey[newKey] = id;
                _movedFrom.Remove(newKey);   // the file is back where it was
            }
            if (ScriptNameOf(newPath) is { } script) _idOfScript[script] = _idOfScript.TryGetValue(script, out string? other) && other != id ? null : id;
            if (KeyOfPath(oldPath) is { } oldKey) _movedFrom[oldKey] = id;
            if (ScriptNameOf(oldPath) is { } oldName && ScriptNameOf(newPath) is { } newName && oldName != newName)
            {
                _scriptMovedFrom[oldName] = id;
                _scriptMovedFrom.Remove(newName);   // the script has the name again that it once had
            }
            if (raise) Moved?.Invoke(oldPath, newPath);
        }

        /// <summary>Deletes a file with its <c>.meta</c>, or a folder with everything in it. Returns an error, or null.</summary>
        public static string? Delete(string path)
        {
            if (_root == null) return "no project is open";
            path = path.TrimEnd('/');
            string abs = Abs(path);
            try
            {
                if (Directory.Exists(abs))
                {
                    Directory.Delete(abs, recursive: true);
                    string prefix = path + "/";
                    var gone = new List<string>();
                    foreach (var kv in _idOfPath)
                        if (kv.Key.StartsWith(prefix, StringComparison.Ordinal)) gone.Add(kv.Key);
                    foreach (string p in gone) Remove(p);
                }
                else if (File.Exists(abs))
                {
                    File.Delete(abs);
                    if (File.Exists(MetaFile(path))) File.Delete(MetaFile(path));
                    Remove(path);
                }
                else return $"{path} was not found";
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return $"{path} could not be deleted: {ex.Message}";
            }
            return null;

            static void Remove(string p)
            {
                if (!_idOfPath.TryGetValue(p, out string? id)) return;
                Forget(p);
                _pathOfId.Remove(id);
            }
        }

        // ── .meta files ─────────────────────────────────────────────────────────────────

        public static string NewId() => Guid.NewGuid().ToString("N");

        /// <summary>True for a text an id can be: 32 hex digits.</summary>
        public static bool IsId(string? s)
        {
            if (s == null || s.Length != 32) return false;
            foreach (char c in s)
                if (!(c >= '0' && c <= '9') && !(c >= 'a' && c <= 'f')) return false;
            return true;
        }

        private static string Abs(string path) => Path.Combine(_root!, path.Replace('/', Path.DirectorySeparatorChar));
        private static string MetaFile(string path) => Abs(path) + MetaExtension;

        // The id a file's .meta holds, or null when there is none or it is not one this version reads.
        private static string? ReadMeta(string path)
        {
            try
            {
                string file = MetaFile(path);
                if (!File.Exists(file)) return null;
                using var doc = JsonDocument.Parse(File.ReadAllText(file));
                if (doc.RootElement.ValueKind == JsonValueKind.Object
                    && doc.RootElement.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String
                    && IsId(id.GetString())) return id.GetString();
                return null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return null; }
        }

        private static bool WriteMeta(string path, string id)
        {
            try { File.WriteAllText(MetaFile(path), "{ \"meta\": 1, \"id\": \"" + id + "\" }\n"); return true; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
        }

        // A new id in a .meta, everything else in it kept as it is.
        private static bool ReplaceId(string path, string oldId, string newId)
        {
            try
            {
                string file = MetaFile(path);
                File.WriteAllText(file, File.ReadAllText(file).Replace(oldId, newId));
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
        }
    }
}
