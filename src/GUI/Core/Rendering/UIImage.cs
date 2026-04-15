using System;
using static Sokol.NanoVG;

namespace Sokol.GUI;

/// <summary>A NanoVG image handle. Dispose to free GPU resources.</summary>
public sealed class UIImage : IDisposable
{
    private int     _nvgImageId;
    private IntPtr  _vg;
    private bool    _disposed;

    public int  Width  { get; private set; }
    public int  Height { get; private set; }
    public int  Id     => _nvgImageId;
    public bool IsValid => _nvgImageId > 0 && !_disposed;

    private UIImage(IntPtr vg, int imageId)
    {
        _vg          = vg;
        _nvgImageId  = imageId;
        int w = 0, h = 0;
        nvgImageSize(vg, imageId, ref w, ref h);
        Width  = w;
        Height = h;
    }

    /// <summary>
    /// Create a UIImage from raw file bytes (JPEG, PNG, etc.).
    /// Returns null if NanoVG cannot decode the data.
    /// </summary>
    public static unsafe UIImage? LoadFromMemory(IntPtr vg, byte[] data,
        NVGimageFlags flags = 0)
    {
        if (data == null || data.Length == 0) return null;
        fixed (byte* ptr = data)
        {
            int id = nvgCreateImageMem(vg, (int)flags, ptr, data.Length);
            return id > 0 ? new UIImage(vg, id) : null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_vg != IntPtr.Zero && _nvgImageId > 0)
            nvgDeleteImage(_vg, _nvgImageId);
        _nvgImageId = 0;
    }
}
