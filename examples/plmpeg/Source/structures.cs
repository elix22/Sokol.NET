
using Sokol;
using System.Runtime.InteropServices;
using static Sokol.SG;
public static unsafe partial class PlMpegApp
{
    public struct plm_buffer_t { };
    public struct plm_t { };

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct plm_plane_t
    {
        public uint width;
        public uint height;
        public byte* data;
    };

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct plm_frame_t
    {
        public double time;
        public uint width;
        public uint height;
        public plm_plane_t y;
        public plm_plane_t cr;
        public plm_plane_t cb;
    };

    const int PLM_AUDIO_SAMPLES_PER_FRAME = 1152;

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct plm_samples_t
    {
        public double time;
        public uint count;
        public fixed float interleaved[PLM_AUDIO_SAMPLES_PER_FRAME * 2];
    };

    const string filename = "bjork-all-is-full-of-love.mpg";

    const int BUFFER_SIZE = (1024 * 1024);
    const int CHUNK_SIZE = (128 * 1024);
    const int NUM_BUFFERS = (4);

    /*************************************************************************************************/
    // These buffers must be manually destroyed , because they are not managed by the Garbage Collector
    /*************************************************************************************************/
    static SharedBuffer[] sharedBuffer = new SharedBuffer[NUM_BUFFERS];

    const int RING_NUM_SLOTS = (NUM_BUFFERS + 1);

    class ring_t
    {
        public uint head;
        public uint tail;
        public int[] buf = new int[RING_NUM_SLOTS];
    };

    // a vertex with position, normal and texcoords
    [StructLayout(LayoutKind.Sequential)]
    struct vertex_t
    {
        public float x, y, z;
        public float nx, ny, nz;
        public float u, v;
    };


    public struct images_t
    {
        public int width;
        public int height;
        public ulong last_upd_frame;
        public sg_image img;
    };
}