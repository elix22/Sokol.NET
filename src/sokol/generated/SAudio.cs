// machine generated, do not edit
using System;
using System.Runtime.InteropServices;
using M = System.Runtime.InteropServices.MarshalAsAttribute;
using U = System.Runtime.InteropServices.UnmanagedType;

namespace Sokol
{
public static unsafe partial class SAudio
{
public enum saudio_log_item
{
    SAUDIO_LOGITEM_OK,
    SAUDIO_LOGITEM_MALLOC_FAILED,
    SAUDIO_LOGITEM_ALSA_SND_PCM_OPEN_FAILED,
    SAUDIO_LOGITEM_ALSA_FLOAT_SAMPLES_NOT_SUPPORTED,
    SAUDIO_LOGITEM_ALSA_REQUESTED_BUFFER_SIZE_NOT_SUPPORTED,
    SAUDIO_LOGITEM_ALSA_REQUESTED_CHANNEL_COUNT_NOT_SUPPORTED,
    SAUDIO_LOGITEM_ALSA_SND_PCM_HW_PARAMS_SET_RATE_NEAR_FAILED,
    SAUDIO_LOGITEM_ALSA_SND_PCM_HW_PARAMS_FAILED,
    SAUDIO_LOGITEM_ALSA_PTHREAD_CREATE_FAILED,
    SAUDIO_LOGITEM_WASAPI_CREATE_EVENT_FAILED,
    SAUDIO_LOGITEM_WASAPI_CREATE_DEVICE_ENUMERATOR_FAILED,
    SAUDIO_LOGITEM_WASAPI_GET_DEFAULT_AUDIO_ENDPOINT_FAILED,
    SAUDIO_LOGITEM_WASAPI_DEVICE_ACTIVATE_FAILED,
    SAUDIO_LOGITEM_WASAPI_AUDIO_CLIENT_INITIALIZE_FAILED,
    SAUDIO_LOGITEM_WASAPI_AUDIO_CLIENT_GET_BUFFER_SIZE_FAILED,
    SAUDIO_LOGITEM_WASAPI_AUDIO_CLIENT_GET_SERVICE_FAILED,
    SAUDIO_LOGITEM_WASAPI_AUDIO_CLIENT_SET_EVENT_HANDLE_FAILED,
    SAUDIO_LOGITEM_WASAPI_CREATE_THREAD_FAILED,
    SAUDIO_LOGITEM_AAUDIO_STREAMBUILDER_OPEN_STREAM_FAILED,
    SAUDIO_LOGITEM_AAUDIO_PTHREAD_CREATE_FAILED,
    SAUDIO_LOGITEM_AAUDIO_RESTARTING_STREAM_AFTER_ERROR,
    SAUDIO_LOGITEM_USING_AAUDIO_BACKEND,
    SAUDIO_LOGITEM_AAUDIO_CREATE_STREAMBUILDER_FAILED,
    SAUDIO_LOGITEM_COREAUDIO_NEW_OUTPUT_FAILED,
    SAUDIO_LOGITEM_COREAUDIO_ALLOCATE_BUFFER_FAILED,
    SAUDIO_LOGITEM_COREAUDIO_START_FAILED,
    SAUDIO_LOGITEM_BACKEND_BUFFER_SIZE_ISNT_MULTIPLE_OF_PACKET_SIZE,
    SAUDIO_LOGITEM_VITA_SCEAUDIO_OPEN_FAILED,
    SAUDIO_LOGITEM_VITA_PTHREAD_CREATE_FAILED,
}
[StructLayout(LayoutKind.Sequential)]
public struct saudio_logger
{
    public delegate* unmanaged<byte*, uint, uint, byte*, uint, byte*, void*, void> func;
    public void* user_data;
}
[StructLayout(LayoutKind.Sequential)]
public struct saudio_allocator
{
    public delegate* unmanaged<nuint, void*, void*> alloc_fn;
    public delegate* unmanaged<void*, void*, void> free_fn;
    public void* user_data;
}
[StructLayout(LayoutKind.Sequential)]
public struct saudio_desc
{
    public int sample_rate;
    public int num_channels;
    public int buffer_frames;
    public int packet_frames;
    public int num_packets;
    public delegate* unmanaged<float*, int, int, void> stream_cb;
    public delegate* unmanaged<float*, int, int, void*, void> stream_userdata_cb;
    public void* user_data;
    public saudio_allocator allocator;
    public saudio_logger logger;
}
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "saudio_setup", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "saudio_setup", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void saudio_setup(in saudio_desc desc);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "saudio_shutdown", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "saudio_shutdown", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void saudio_shutdown();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "saudio_isvalid", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "saudio_isvalid", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool saudio_isvalid();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "saudio_userdata", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "saudio_userdata", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void* saudio_userdata();

#if WEB
public static saudio_desc saudio_query_desc()
{
    saudio_desc result = default;
    saudio_query_desc_internal(ref result);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "saudio_query_desc", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "saudio_query_desc", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern saudio_desc saudio_query_desc();
#endif

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "saudio_sample_rate", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "saudio_sample_rate", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int saudio_sample_rate();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "saudio_buffer_frames", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "saudio_buffer_frames", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int saudio_buffer_frames();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "saudio_channels", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "saudio_channels", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int saudio_channels();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "saudio_suspended", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "saudio_suspended", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool saudio_suspended();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "saudio_expect", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "saudio_expect", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int saudio_expect();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "saudio_push", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "saudio_push", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int saudio_push(in float frames, int num_frames);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "saudio_query_desc_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "saudio_query_desc_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void saudio_query_desc_internal(ref saudio_desc result);

}
}
