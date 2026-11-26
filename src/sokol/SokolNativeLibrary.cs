using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Sokol
{
    /// <summary>
    /// Windows, JIT builds of projects that reference the managed bindings as an assembly (through the Framework): that
    /// assembly is sokol.dll, the native library's own name, so the build puts the native library in native\
    /// (src/Framework/build/WindowsNativeSokol.targets) and DllImport("sokol") is pointed there. Nothing changes on any
    /// other platform, in a NativeAOT publish (the bindings are inside the executable, the native library beside it) or
    /// in a project that compiles the bindings in (no native folder).
    /// </summary>
    public static class SokolNativeLibrary
    {
        const string Name = "sokol";

#pragma warning disable CA2255 // the resolver must be in place before the bindings' first call
        [ModuleInitializer]
        internal static void Init() => Use(typeof(SokolNativeLibrary).Assembly);
#pragma warning restore CA2255

        /// <summary>Points the DllImport("sokol") calls of an assembly at native\sokol.dll when it is there (Windows).
        /// For an app whose own code calls the native library directly; the bindings' assembly does it itself.</summary>
        public static void Use(Assembly assembly)
        {
            if (!OperatingSystem.IsWindows()) return;
            string path = Path.Combine(AppContext.BaseDirectory, "native", Name + ".dll");
            if (!File.Exists(path)) return;
            try
            {
                NativeLibrary.SetDllImportResolver(assembly,
                    (name, _, _) => name == Name ? NativeLibrary.Load(path) : IntPtr.Zero);
            }
            catch (InvalidOperationException) { }   // the assembly has a resolver of its own already
        }
    }
}
