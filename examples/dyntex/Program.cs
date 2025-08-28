

using System.Runtime.InteropServices;
using Sokol;

public static unsafe class MainClass
{
    // Host platform entry point
    public static unsafe void Main()
    {
        SApp.sapp_run(DynTextApp.sokol_main());
    }

    static IntPtr _descPtr = IntPtr.Zero;
    [UnmanagedCallersOnly(EntryPoint = "AndroidMain")]
    public static unsafe IntPtr AndroidMain()
    {
        Console.WriteLine(" AndroidMain() Enter");
        SApp.sapp_desc desc = DynTextApp.sokol_main();
        _descPtr = Marshal.AllocHGlobal(Marshal.SizeOf(desc));
        Marshal.StructureToPtr(desc, _descPtr, false);
        return _descPtr;
    }

    [UnmanagedCallersOnly(EntryPoint = "IOSMain")]
    public static unsafe void IOSMain()
    {
        Console.WriteLine(" IOSMain() Enter");
        SApp.sapp_run(DynTextApp.sokol_main());
    }

}


