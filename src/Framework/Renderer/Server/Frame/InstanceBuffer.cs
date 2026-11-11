using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static Sokol.SG;
using static Sokol.Utils;

namespace GameEditor.Framework.Renderer.Server
{
    /// <summary>
    /// Append-style instance data buffer.
    /// Uses sg_append_buffer so multiple views can append within the same Sokol frame
    /// (sg_update_buffer only allows one call per buffer per frame; sg_append_buffer allows many).
    /// Staging memory is allocated once via NativeMemory.Alloc (no GC pin, no LOH pressure).
    /// </summary>
    public sealed unsafe class InstanceBuffer : IDisposable
    {
        private static readonly int InstanceSize = Unsafe.SizeOf<InstanceData>();

        // Single buffer sized for MAX_VIEWS concurrent views per Sokol frame.
        private sg_buffer     _buf;
        private InstanceData* _stagingPtr;
        private readonly int  _maxInstancesPerView;
        private int           _stagingCount;
        private bool          _disposed;

        public sg_buffer Buffer => _buf;

        public InstanceBuffer(int maxInstancesPerView = RenderingConstants.MAX_DRAWS)
        {
            _maxInstancesPerView = maxInstancesPerView;
            int stagingBytes     = maxInstancesPerView * InstanceSize;

            // Allocate unmanaged staging memory — one view's worth, reused each Flush.
            _stagingPtr = (InstanceData*)NativeMemory.Alloc((nuint)stagingBytes);

            // Single buffer large enough for all views in one Sokol frame (MAX_VIEWS × per-view).
            // Not without sokol_gfx: RenderingServer creates its instance buffers when it is first touched, and a
            // tool with no window (the editor's headless command line) touches its registries and settings.
            int gpuBytes = RenderingConstants.MAX_VIEWS * stagingBytes;
            if (!sg_isvalid()) return;
            _buf = sg_make_buffer(new sg_buffer_desc
            {
                size  = (nuint)gpuBytes,
                usage = new sg_buffer_usage { vertex_buffer = true, stream_update = true },
                label = "instance-append"
            });
        }

        /// <summary>Reset staging count at the start of a new CPU frame (no ring rotation needed).</summary>
        public void BeginFrame() => _stagingCount = 0;

        /// <summary>Write one InstanceData into the staging buffer.</summary>
        public bool TryAppend(in InstanceData data)
        {
            if (_stagingCount >= _maxInstancesPerView) return false;
            _stagingPtr[_stagingCount++] = data;
            return true;
        }

        /// <summary>
        /// Append the staged data into the GPU buffer via sg_append_buffer.
        /// Returns the byte offset of the first instance within the buffer.
        /// Safe to call multiple times per Sokol frame (once per view).
        /// </summary>
        public int Flush()
        {
            if (_stagingCount == 0) return 0;
            int byteCount = _stagingCount * InstanceSize;
            return sg_append_buffer(_buf, new sg_range
            {
                ptr  = _stagingPtr,
                size = (nuint)byteCount
            });
        }

        /// <summary>Number of instances staged since last BeginFrame / ResetStaging.</summary>
        public int StagedCount => _stagingCount;

        /// <summary>Reset staged count without starting a new frame (use between views).</summary>
        public void ResetStaging() => _stagingCount = 0;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_buf.id != 0)
            {
                sg_destroy_buffer(_buf);
                _buf = default;
            }

            if (_stagingPtr != null)
            {
                NativeMemory.Free(_stagingPtr);
                _stagingPtr = null;
            }
        }
    }
}
