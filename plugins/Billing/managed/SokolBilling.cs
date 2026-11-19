#if __ANDROID__ || __IOS__
using System;
using System.Runtime.InteropServices;

internal static class SokolBilling
{
#if __IOS__
    const string Lib = "@rpath/sokol_billing.framework/sokol_billing";
#else
    const string Lib = "sokol_billing";
#endif

    /* Mirrors sokolbilling_event in sokol_billing.h.
       String pointers stay valid until the next PollEvent call. */
    [StructLayout(LayoutKind.Sequential)]
    internal struct Event
    {
        public int type;
        public int code;
        public IntPtr sku;
        public IntPtr price;
        public IntPtr proof;
        public IntPtr signature;
    }

    [DllImport(Lib, EntryPoint = "sokolbilling_init",
               CallingConvention = CallingConvention.Cdecl)]
    internal static extern void Init();

    [DllImport(Lib, EntryPoint = "sokolbilling_query_product",
               CallingConvention = CallingConvention.Cdecl)]
    internal static extern void QueryProduct(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string sku);

    [DllImport(Lib, EntryPoint = "sokolbilling_purchase",
               CallingConvention = CallingConvention.Cdecl)]
    internal static extern void Purchase(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string sku);

    [DllImport(Lib, EntryPoint = "sokolbilling_restore",
               CallingConvention = CallingConvention.Cdecl)]
    internal static extern void Restore();

    [DllImport(Lib, EntryPoint = "sokolbilling_sync",
               CallingConvention = CallingConvention.Cdecl)]
    internal static extern void Sync();

    [DllImport(Lib, EntryPoint = "sokolbilling_consume",
               CallingConvention = CallingConvention.Cdecl)]
    internal static extern void Consume(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string sku);

    [DllImport(Lib, EntryPoint = "sokolbilling_finish_consumable",
               CallingConvention = CallingConvention.Cdecl)]
    internal static extern void FinishConsumable(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string sku);

    [DllImport(Lib, EntryPoint = "sokolbilling_poll_event",
               CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool PollEvent(out Event evt);
}
#endif
