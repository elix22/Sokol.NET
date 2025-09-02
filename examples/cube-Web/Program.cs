

using System.Runtime.InteropServices;
using Sokol;

public static unsafe class MainClass
{
    // Host platform entry point
    public static unsafe void Main()
    {
        Console.WriteLine(" Main() Enter");
        // elix22 - some hack that is needed in case that the application is published as NativeAOT on an desktop platform
#if !WEB
        var applicationPath = System.IO.Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);
        System.IO.Directory.SetCurrentDirectory(applicationPath);
#endif
        Console.WriteLine(" Before sapp_run()");
        SApp.sapp_run(CreateAppDesc());
        Console.WriteLine(" After sapp_run()");
    }

    //ANDROID
    static IntPtr _descPtr = IntPtr.Zero;
    [UnmanagedCallersOnly(EntryPoint = "AndroidMain")]
    public static unsafe IntPtr AndroidMain()
    {
        Console.WriteLine(" AndroidMain() Enter");
        SApp.sapp_desc desc = CreateAppDesc();
        _descPtr = Marshal.AllocHGlobal(Marshal.SizeOf(desc));
        Marshal.StructureToPtr(desc, _descPtr, false);
        return _descPtr;
    }

    // IOS
    [UnmanagedCallersOnly(EntryPoint = "IOSMain")]
    public static unsafe void IOSMain()
    {
        Console.WriteLine(" IOSMain() Enter");
        SApp.sapp_run(CreateAppDesc());
    }


    public static SApp.sapp_desc CreateAppDesc()
    {
        return CubeSapp.sokol_main();
    }

}


