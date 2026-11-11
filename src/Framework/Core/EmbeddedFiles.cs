using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace GameEditor.Framework.Core
{
    /// <summary>
    /// The files of a project's <c>Embedded</c> folder, which are part of the app's assembly: read by name, at once,
    /// on every platform — no file system and no waiting. For small files the game needs before anything else is
    /// loaded. The project's build embeds the folder with one line:
    /// <code>&lt;EmbeddedResource Include="Embedded/**/*" LogicalName="Embedded/%(RecursiveDir)%(Filename)%(Extension)" /&gt;</code>
    /// A file is named by its path below the folder, with forward slashes ("assets.index.json", "Levels/1.json").
    /// </summary>
    public static class EmbeddedFiles
    {
        public const string Folder = "Embedded";
        private const string Prefix = Folder + "/";

        private static Assembly? _assembly;
        // Name below the folder → the assembly that holds it and the resource's own name.
        private static Dictionary<string, (Assembly assembly, string resource)>? _files;

        /// <summary>
        /// Says which assembly holds the app's embedded files. Not needed where the files are found without it
        /// (the assemblies that are loaded are looked through); call it before the first read.
        /// </summary>
        public static void Use(Assembly assembly)
        {
            _assembly = assembly;
            _files = null;
        }

        /// <summary>The names of the embedded files.</summary>
        public static IReadOnlyCollection<string> Names => Files().Keys;

        public static bool Exists(string name) => Files().ContainsKey(name);

        /// <summary>A file's bytes, or null when there is no such file.</summary>
        public static byte[]? ReadBytes(string name)
        {
            if (!Files().TryGetValue(name, out var file)) return null;
            using Stream? stream = file.assembly.GetManifestResourceStream(file.resource);
            if (stream == null) return null;
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            return buffer.ToArray();
        }

        /// <summary>A file's text (UTF-8), or null when there is no such file.</summary>
        public static string? ReadText(string name)
        {
            byte[]? bytes = ReadBytes(name);
            if (bytes == null) return null;
            int start = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;   // a byte order mark is not text
            return System.Text.Encoding.UTF8.GetString(bytes, start, bytes.Length - start);
        }

        private static Dictionary<string, (Assembly assembly, string resource)> Files()
        {
            if (_files != null) return _files;
            var files = new Dictionary<string, (Assembly, string)>(StringComparer.Ordinal);
            if (_assembly != null) Add(_assembly, files);
            else
            {
                // The app's own assembly first, then whatever else is loaded.
                if (Assembly.GetEntryAssembly() is { } entry) Add(entry, files);
                try
                {
                    foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) Add(assembly, files);
                }
                catch (Exception ex) when (ex is NotSupportedException or PlatformNotSupportedException) { }
            }
            return _files = files;
        }

        private static void Add(Assembly assembly, Dictionary<string, (Assembly, string)> files)
        {
            string[] names;
            try { names = assembly.GetManifestResourceNames(); }
            catch (Exception ex) when (ex is NotSupportedException or FileNotFoundException or FileLoadException) { return; }   // an assembly made at run time has none
            foreach (string resource in names)
            {
                string name = resource.Replace('\\', '/');   // a build on Windows writes the folders of a name with '\'
                if (name.StartsWith(Prefix, StringComparison.Ordinal)) files.TryAdd(name.Substring(Prefix.Length), (assembly, resource));
            }
        }
    }
}
