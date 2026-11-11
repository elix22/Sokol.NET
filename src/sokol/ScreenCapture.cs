using System;
using System.IO;
using System.IO.Compression;
using static Sokol.SApp;
using static Sokol.SCapture;
using static Sokol.SG;
using static Sokol.SGlue;

namespace Sokol
{
    /// <summary>
    /// In-app screenshots: renders one frame into an offscreen render target instead of the swapchain,
    /// reads the pixels back off the GPU (<see cref="SCapture"/> / <c>ext/sokol_capture.h</c>) and writes
    /// a PNG. Works on every backend the shim implements (Metal, GL/GLES3/WebGL2) with no OS screenshot
    /// tooling involved — which is the only route on a device Apple's lockdown tooling can't screenshot,
    /// and the only route at all for headless/CI capture.
    ///
    /// Wire it into the frame callback by replacing the swapchain <c>sg_begin_pass</c> and <c>sg_commit</c>:
    /// <code>
    ///     ScreenCapture.BeginFrame(passAction);   // begins the swapchain pass, or the capture target
    ///     ... draw ...
    ///     sg_end_pass();
    ///     ScreenCapture.Commit();                 // sg_commit, then readback + PNG when capturing
    /// </code>
    /// The captured frame is not presented (the swapchain gets a plain clear), so a capture costs one
    /// black frame on screen. Capture frames are rare (harness/CI driven), hence no blit-back pass.
    /// </summary>
    public static unsafe class ScreenCapture
    {
        // Offscreen target, laid out to match the swapchain exactly — the app's pipelines were created
        // against the swapchain's colour/depth format and sample count, so anything else fails validation.
        static sg_image _colorImg;      // single-sample; the image read back (MSAA resolve target)
        static sg_image _msaaImg;       // MSAA colour attachment (only when sample_count > 1)
        static sg_image _depthImg;
        static sg_view _colorAtt, _msaaAtt, _resolveView, _depthAtt;
        static sg_attachments _attachments;
        static int _w, _h, _samples;
        static sg_pixel_format _colorFmt, _depthFmt;

        static string? _pendingPath;
        static bool _capturing;
        static byte[]? _pixels;

        /// <summary>Path written by the last successful capture, null if none.</summary>
        public static string? LastPath { get; private set; }

        /// <summary>Why the last capture failed, null if the last one succeeded.</summary>
        public static string? LastError { get; private set; }

        /// <summary>True while a capture is armed and has not been taken yet.</summary>
        public static bool Pending => _pendingPath != null;

        /// <summary>True if the active sokol-gfx backend implements pixel readback.</summary>
        public static bool Supported => scap_supported();

        /// <summary>
        /// Arm a capture: the next frame that goes through <see cref="BeginFrame"/> renders offscreen and
        /// is written to <paramref name="path"/> as a PNG. Returns false if the backend can't read pixels
        /// back or a capture is already armed.
        /// </summary>
        public static bool Request(string path)
        {
            if (!scap_supported()) { LastError = "backend does not support readback: " + scap_error(); return false; }
            if (_pendingPath != null) { LastError = "a capture is already pending"; return false; }
            _pendingPath = path;
            LastError = null;
            return true;
        }

        /// <summary>
        /// Begin the frame's render pass: the offscreen capture target when a capture is armed, otherwise
        /// the swapchain. Returns true if this frame is being captured.
        /// </summary>
        public static bool BeginFrame(sg_pass_action action)
        {
            var sc = sglue_swapchain();
            if (_pendingPath != null && !EnsureTarget(sc))
                _pendingPath = null;    // target unavailable (out of GPU memory?) — fail the request, don't wedge it
            if (_pendingPath == null)
            {
                sg_begin_pass(new sg_pass { action = action, swapchain = sc });
                return false;
            }
            // MSAA colour is resolved into _colorImg, so its own store is pointless.
            if (_samples > 1) action.colors[0].store_action = sg_store_action.SG_STOREACTION_DONTCARE;
            sg_begin_pass(new sg_pass { action = action, attachments = _attachments, label = "scap-capture-pass" });
            _capturing = true;
            return true;
        }

        /// <summary>
        /// End the frame (replaces <c>sg_commit</c>). When capturing, presents a cleared swapchain, commits,
        /// then reads the target back and writes the PNG — readback has to happen after the commit, because
        /// Metal only orders work by commit order and would otherwise race the render.
        /// </summary>
        public static void Commit()
        {
            if (!_capturing) { sg_commit(); return; }
            _capturing = false;

            // The captured frame never reaches the screen; clear the swapchain so the drawable is defined.
            var clear = default(sg_pass_action);
            clear.colors[0].load_action = sg_load_action.SG_LOADACTION_CLEAR;
            clear.colors[0].clear_value = new sg_color { r = 0f, g = 0f, b = 0f, a = 1f };
            sg_begin_pass(new sg_pass { action = clear, swapchain = sglue_swapchain(), label = "scap-present-pass" });
            sg_end_pass();
            sg_commit();

            string path = _pendingPath!;
            _pendingPath = null;
            try
            {
                int need = _w * _h * 4;
                if (_pixels == null || _pixels.Length < need) _pixels = new byte[need];
                bool ok;
                fixed (byte* p = _pixels) ok = scap_read_image(_colorImg, _w, _h, p, need);
                if (!ok) { LastError = "scap_read_image: " + scap_error(); return; }

                string? dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllBytes(path, EncodePng(_pixels, _w, _h));
                LastPath = path;
                LastError = null;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
            }
            finally
            {
                // The target is only needed during a capture, and it is not cheap: MSAA colour + depth at
                // full resolution is ~100 MB on a phone. Give it back and rebuild it for the next shot
                // (sokol recycles the pool slots, so a long sweep does not leak them).
                DestroyTarget();
            }
        }

        /// <summary>Disarm a request that never got captured (the frame loop never reached
        /// <see cref="BeginFrame"/>), so a later <see cref="Request"/> isn't refused forever.</summary>
        public static void Cancel()
        {
            _pendingPath = null;
            _capturing = false;
        }

        /// <summary>Free the capture target (call from the app's cleanup, or to reclaim the memory).</summary>
        public static void Shutdown()
        {
            DestroyTarget();
            _pixels = null;
            _pendingPath = null;
            _capturing = false;
        }

        // ── capture target ──────────────────────────────────────────────────────────────────────────
        static bool EnsureTarget(sg_swapchain sc)
        {
            if (sc.width <= 0 || sc.height <= 0) { LastError = "swapchain has no size yet"; return false; }
            if (_colorImg.id != 0 && _w == sc.width && _h == sc.height &&
                _samples == sc.sample_count && _colorFmt == sc.color_format && _depthFmt == sc.depth_format)
                return true;

            DestroyTarget();
            _w = sc.width; _h = sc.height; _samples = sc.sample_count;
            _colorFmt = sc.color_format; _depthFmt = sc.depth_format;
            bool msaa = _samples > 1;

            _colorImg = sg_make_image(new sg_image_desc
            {
                width = _w, height = _h, pixel_format = _colorFmt, sample_count = 1,
                usage = { color_attachment = true, resolve_attachment = msaa }, label = "scap-color",
            });
            _colorAtt = sg_make_view(new sg_view_desc { color_attachment = { image = _colorImg }, label = "scap-color-att" });
            _attachments = default;
            if (msaa)
            {
                _msaaImg = sg_make_image(new sg_image_desc
                {
                    width = _w, height = _h, pixel_format = _colorFmt, sample_count = _samples,
                    usage = { color_attachment = true }, label = "scap-msaa",
                });
                _msaaAtt     = sg_make_view(new sg_view_desc { color_attachment   = { image = _msaaImg },  label = "scap-msaa-att" });
                _resolveView = sg_make_view(new sg_view_desc { resolve_attachment = { image = _colorImg }, label = "scap-resolve" });
                _attachments.colors[0]   = _msaaAtt;
                _attachments.resolves[0] = _resolveView;
            }
            else
            {
                _attachments.colors[0] = _colorAtt;
            }
            if (_depthFmt != sg_pixel_format.SG_PIXELFORMAT_NONE)
            {
                _depthImg = sg_make_image(new sg_image_desc
                {
                    width = _w, height = _h, pixel_format = _depthFmt, sample_count = _samples,
                    usage = { depth_stencil_attachment = true }, label = "scap-depth",
                });
                _depthAtt = sg_make_view(new sg_view_desc { depth_stencil_attachment = { image = _depthImg }, label = "scap-depth-att" });
                _attachments.depth_stencil = _depthAtt;
            }

            // Everything must be valid before the pass is begun: an exhausted image/view pool yields an
            // invalid handle, and sokol's validation layer *aborts* on a pass built from one.
            if (!Valid(_colorImg) || (msaa && !Valid(_msaaImg)) ||
                (_depthFmt != sg_pixel_format.SG_PIXELFORMAT_NONE && !Valid(_depthImg)) ||
                !Valid(_attachments.colors[0]) ||
                (msaa && !Valid(_attachments.resolves[0])) ||
                (_depthFmt != sg_pixel_format.SG_PIXELFORMAT_NONE && !Valid(_attachments.depth_stencil)))
            {
                LastError = "could not create the capture target (out of GPU memory, or a sokol pool is full)";
                DestroyTarget();
                return false;
            }
            return true;
        }

        static bool Valid(sg_image img) =>
            img.id != 0 && sg_query_image_state(img) == sg_resource_state.SG_RESOURCESTATE_VALID;

        static bool Valid(sg_view view) =>
            view.id != 0 && sg_query_view_state(view) == sg_resource_state.SG_RESOURCESTATE_VALID;

        static void DestroyTarget()
        {
            if (_colorAtt.id    != 0) sg_destroy_view(_colorAtt);
            if (_msaaAtt.id     != 0) sg_destroy_view(_msaaAtt);
            if (_resolveView.id != 0) sg_destroy_view(_resolveView);
            if (_depthAtt.id    != 0) sg_destroy_view(_depthAtt);
            if (_colorImg.id    != 0) sg_destroy_image(_colorImg);
            if (_msaaImg.id     != 0) sg_destroy_image(_msaaImg);
            if (_depthImg.id    != 0) sg_destroy_image(_depthImg);
            _colorAtt = _msaaAtt = _resolveView = _depthAtt = default;
            _colorImg = _msaaImg = _depthImg = default;
            _attachments = default;
            _w = _h = _samples = 0;
        }

        // ── PNG encoding ────────────────────────────────────────────────────────────────────────────
        // Screenshots are opaque, so the alpha channel is dropped: 8-bit RGB (colour type 2), one
        // filter-0 scanline per row, deflated with the in-box zlib.
        public static byte[] EncodePng(byte[] rgba, int w, int h)
        {
            var raw = new MemoryStream((w * 3 + 1) * h);
            for (int y = 0; y < h; y++)
            {
                raw.WriteByte(0);                       // filter: none
                int src = y * w * 4;
                for (int x = 0; x < w; x++, src += 4)
                {
                    raw.WriteByte(rgba[src]);
                    raw.WriteByte(rgba[src + 1]);
                    raw.WriteByte(rgba[src + 2]);
                }
            }

            var deflated = new MemoryStream();
            using (var z = new ZLibStream(deflated, CompressionLevel.Fastest, leaveOpen: true))
                z.Write(raw.GetBuffer(), 0, (int)raw.Length);

            var png = new MemoryStream();
            png.Write(new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A });
            var ihdr = new byte[13];
            WriteBE32(ihdr, 0, w);
            WriteBE32(ihdr, 4, h);
            ihdr[8] = 8;    // bit depth
            ihdr[9] = 2;    // colour type: truecolour RGB
            WriteChunk(png, "IHDR", ihdr, ihdr.Length);
            WriteChunk(png, "IDAT", deflated.GetBuffer(), (int)deflated.Length);
            WriteChunk(png, "IEND", Array.Empty<byte>(), 0);
            return png.ToArray();
        }

        static void WriteBE32(byte[] dst, int at, int v)
        {
            dst[at]     = (byte)(v >> 24);
            dst[at + 1] = (byte)(v >> 16);
            dst[at + 2] = (byte)(v >> 8);
            dst[at + 3] = (byte)v;
        }

        static void WriteChunk(Stream s, string type, byte[] data, int len)
        {
            var be = new byte[4];
            WriteBE32(be, 0, len);
            s.Write(be, 0, 4);
            var tag = new byte[4];
            for (int i = 0; i < 4; i++) tag[i] = (byte)type[i];
            s.Write(tag, 0, 4);
            s.Write(data, 0, len);
            uint crc = Crc32(0xFFFFFFFFu, tag, 4);
            crc = Crc32(crc, data, len) ^ 0xFFFFFFFFu;
            WriteBE32(be, 0, (int)crc);
            s.Write(be, 0, 4);
        }

        static uint[]? _crcTable;

        static uint Crc32(uint crc, byte[] data, int len)
        {
            if (_crcTable == null)
            {
                _crcTable = new uint[256];
                for (uint n = 0; n < 256; n++)
                {
                    uint c = n;
                    for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                    _crcTable[n] = c;
                }
            }
            for (int i = 0; i < len; i++) crc = _crcTable[(crc ^ data[i]) & 0xFF] ^ (crc >> 8);
            return crc;
        }
    }
}
