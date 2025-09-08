
using Sokol;
using System.Runtime.InteropServices;
// TBD ELI
// It's a native binding for the PlMpeg code which is currently part of the sokol library , 
// It' should be seperated from sokol , but for now it will do
// It's used to interface with the PlMpeg native code from C#.
public static unsafe partial class PlMpegApp
{

#if __IOS__
      const string  sokol_lib = "@rpath/sokol.framework/sokol";
#else
    const string sokol_lib = "sokol";
#endif

    [DllImport(sokol_lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern int plm_decode(plm_t* self, double tick);

    [DllImport(sokol_lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern plm_buffer_t* plm_buffer_create_with_capacity(int capacity);

    [DllImport(sokol_lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void plm_buffer_set_load_callback(plm_buffer_t* self, IntPtr func, void* user);

    [DllImport(sokol_lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern plm_t* plm_create_with_buffer(plm_buffer_t* buffer, int destroy_when_done);

    [DllImport(sokol_lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void plm_set_video_decode_callback(plm_t* self, IntPtr func, void* user);

    [DllImport(sokol_lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void plm_set_audio_decode_callback(plm_t* self, IntPtr func, void* user);

    [DllImport(sokol_lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void plm_set_loop(plm_t* self, int loop);

    [DllImport(sokol_lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void plm_set_audio_enabled(plm_t* self, int enabled, int stream_index);

    [DllImport(sokol_lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void plm_set_audio_lead_time(plm_t* self, double lead_time);

    [DllImport(sokol_lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern int plm_get_num_audio_streams(plm_t* self);

    [DllImport(sokol_lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern int plm_get_samplerate(plm_t* self);

    [DllImport(sokol_lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void plm_buffer_destroy(plm_buffer_t* self);


    [DllImport(sokol_lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void plm_buffer_discard_read_bytes(plm_buffer_t* self);

    [DllImport(sokol_lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern byte* plm_buffer_get_bytes(plm_buffer_t* self);

    [DllImport(sokol_lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern int plm_buffer_get_length(plm_buffer_t* self);

    [DllImport(sokol_lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void plm_buffer_set_length(plm_buffer_t* self, int length);

    [DllImport(sokol_lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern int plm_buffer_get_capacity(plm_buffer_t* self);


}