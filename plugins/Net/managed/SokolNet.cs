// sokol_net — managed bindings: libdatachannel's C API (rtc.h, v0.24.6 + plugins/Net/patches) and the
// sokolnet_* helpers. Only the WebSocket + PeerConnection + DataChannel subset is bound (no media).
//
// ⛔ Threading: libdatachannel invokes every callback on its OWN threads (a pool, ordered per PeerConnection
// and per channel). Pass [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })] static methods
// (cast with `(IntPtr)(delegate* unmanaged[Cdecl]<…>)&Method`) that only copy the data and enqueue it;
// act on it from the app's own thread. Never call rtcCleanup from a callback.
//
// C `bool` fields are one byte → `byte` here (0 / 1). No function returns a struct by value.
#if __ANDROID__ || __IOS__ || __MACOS__
using System;
using System.Runtime.InteropServices;

public static class SokolNet
{
#if __IOS__
    const string Lib = "@rpath/sokol_net.framework/sokol_net";
#else
    const string Lib = "sokol_net";
#endif

    public const int RTC_ERR_SUCCESS = 0, RTC_ERR_INVALID = -1, RTC_ERR_FAILURE = -2, RTC_ERR_NOT_AVAIL = -3, RTC_ERR_TOO_SMALL = -4;

    public enum rtcState { New = 0, Connecting = 1, Connected = 2, Disconnected = 3, Failed = 4, Closed = 5 }
    public enum rtcIceState { New = 0, Checking = 1, Connected = 2, Completed = 3, Failed = 4, Disconnected = 5, Closed = 6 }
    public enum rtcGatheringState { New = 0, InProgress = 1, Complete = 2 }
    public enum rtcLogLevel { None = 0, Fatal = 1, Error = 2, Warning = 3, Info = 4, Debug = 5, Verbose = 6 }
    public enum rtcCertificateType { Default = 0, Ecdsa = 1, Rsa = 2 }
    public enum rtcTransportPolicy { All = 0, Relay = 1 }

    [StructLayout(LayoutKind.Sequential)]
    public struct rtcConfiguration
    {
        public IntPtr iceServers;              // const char** — "stun:host:port", "turn:user:pass@host:port?transport=udp"
        public int iceServersCount;
        public IntPtr proxyServer;             // libnice only
        public IntPtr bindAddress;             // NULL = any
        public rtcCertificateType certificateType;
        public rtcTransportPolicy iceTransportPolicy;
        public byte enableIceTcp;
        public byte enableIceUdpMux;
        public byte disableAutoNegotiation;
        public byte forceMediaTransport;
        public ushort portRangeBegin;          // 0 = automatic
        public ushort portRangeEnd;
        public int mtu;                        // <= 0 = automatic
        public int maxMessageSize;             // <= 0 = default
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct rtcReliability
    {
        public byte unordered;
        public byte unreliable;
        public uint maxPacketLifeTime;         // ms, used when unreliable and > 0
        public uint maxRetransmits;            // used when unreliable and maxPacketLifeTime == 0
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct rtcDataChannelInit
    {
        public rtcReliability reliability;
        public IntPtr protocol;                // NULL = ""
        public byte negotiated;
        public byte manualStream;
        public ushort stream;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct rtcWsConfiguration
    {
        public byte disableTlsVerification;
        public IntPtr proxyServer;
        public IntPtr protocols;               // const char**
        public int protocolsCount;
        public int connectionTimeoutMs;        // 0 = default, < 0 = disabled
        public int pingIntervalMs;             // 0 = default, < 0 = disabled
        public int maxOutstandingPings;        // 0 = default, < 0 = disabled
        public int maxMessageSize;             // <= 0 = default
        public IntPtr caCertificatePemFile;    // PEM file path or PEM content; sokolnet_ca_bundle() for the bundled roots
    }

    // ── sokol_net helpers ────────────────────────────────────────────────────────────────────────────────────
    /// <summary>The bundled CA roots (Mozilla's, PEM, NUL-terminated, owned by the library) for
    /// <see cref="rtcWsConfiguration.caCertificatePemFile"/>.</summary>
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr sokolnet_ca_bundle();
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int sokolnet_version();

    // ── global ───────────────────────────────────────────────────────────────────────────────────────────────
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern void rtcInitLogger(rtcLogLevel level, IntPtr cb);   // cb(rtcLogLevel, const char*)
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern void rtcPreload();
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern void rtcCleanup();
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int rtcSetThreadPoolSize(uint count);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern void rtcSetUserPointer(int id, IntPtr ptr);

    // ── PeerConnection ───────────────────────────────────────────────────────────────────────────────────────
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int rtcCreatePeerConnection(in rtcConfiguration config);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int rtcClosePeerConnection(int pc);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int rtcDeletePeerConnection(int pc);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int rtcSetLocalDescriptionCallback(int pc, IntPtr cb);  // cb(int pc, const char* sdp, const char* type, void*)
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int rtcSetLocalCandidateCallback(int pc, IntPtr cb);    // cb(int pc, const char* cand, const char* mid, void*)
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int rtcSetStateChangeCallback(int pc, IntPtr cb);       // cb(int pc, rtcState, void*)
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int rtcSetIceStateChangeCallback(int pc, IntPtr cb);    // cb(int pc, rtcIceState, void*)
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int rtcSetDataChannelCallback(int pc, IntPtr cb);       // cb(int pc, int dc, void*)
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int rtcSetLocalDescription(int pc, [MarshalAs(UnmanagedType.LPUTF8Str)] string? type);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int rtcSetRemoteDescription(int pc, [MarshalAs(UnmanagedType.LPUTF8Str)] string sdp, [MarshalAs(UnmanagedType.LPUTF8Str)] string? type);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int rtcAddRemoteCandidate(int pc, [MarshalAs(UnmanagedType.LPUTF8Str)] string cand, [MarshalAs(UnmanagedType.LPUTF8Str)] string? mid);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int rtcGetSelectedCandidatePair(int pc, IntPtr local, int localSize, IntPtr remote, int remoteSize);

    // ── DataChannel ──────────────────────────────────────────────────────────────────────────────────────────
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int rtcCreateDataChannelEx(int pc, [MarshalAs(UnmanagedType.LPUTF8Str)] string label, in rtcDataChannelInit init);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int rtcGetDataChannelLabel(int dc, IntPtr buffer, int size);

    // ── WebSocket (client) ───────────────────────────────────────────────────────────────────────────────────
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int rtcCreateWebSocketEx([MarshalAs(UnmanagedType.LPUTF8Str)] string url, in rtcWsConfiguration config);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int rtcDeleteWebSocket(int ws);

    // ── channels (DataChannel or WebSocket id) ───────────────────────────────────────────────────────────────
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int rtcSetOpenCallback(int id, IntPtr cb);              // cb(int id, void*)
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int rtcSetClosedCallback(int id, IntPtr cb);            // cb(int id, void*)
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int rtcSetErrorCallback(int id, IntPtr cb);             // cb(int id, const char* error, void*)
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int rtcSetMessageCallback(int id, IntPtr cb);           // cb(int id, const char* data, int size, void*) — size >= 0 binary, < 0 text
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int rtcSetBufferedAmountLowCallback(int id, IntPtr cb);  // cb(int id, void*)
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int rtcSendMessage(int id, IntPtr data, int size);       // size >= 0 binary, -1 = NUL-terminated text
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int rtcClose(int id);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int rtcDelete(int id);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] [return: MarshalAs(UnmanagedType.I1)] public static extern bool rtcIsOpen(int id);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int rtcGetBufferedAmount(int id);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] public static extern int rtcSetBufferedAmountLowThreshold(int id, int amount);

    /// <summary>Send bytes on a DataChannel or WebSocket.</summary>
    public static unsafe int Send(int id, ReadOnlySpan<byte> data)
    {
        fixed (byte* p = data) return rtcSendMessage(id, (IntPtr)p, data.Length);
    }
}
#endif
