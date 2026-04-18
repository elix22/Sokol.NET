using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using static Sokol.SFilesystem;
using static Sokol.SFilesystem.sfs_open_mode_t;

namespace Sokol.GUI;

/// <summary>
/// Persists a set of simple per-widget scalar values identified by widget id.
/// Useful for preserving things like a <see cref="TextBox"/>'s text, a
/// <see cref="CheckBox"/>'s state, or a <see cref="Slider"/>'s value across runs.
///
/// Widgets are *not* reflected over — the application decides what to save by
/// calling <see cref="Set"/> before <see cref="Save"/>, and reads back values
/// after <see cref="Load"/>.
/// </summary>
public sealed class WidgetStateSerializer
{
    private readonly Dictionary<string, string> _values = [];

    public void Set(string id, string value)  => _values[id] = value;
    public void Set(string id, bool value)    => _values[id] = value.ToString();
    public void Set(string id, int value)     => _values[id] = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    public void Set(string id, float value)   => _values[id] = value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public string? Get(string id) => _values.TryGetValue(id, out var v) ? v : null;

    public bool  GetBool (string id, bool fallback = false)
        => _values.TryGetValue(id, out var v) && bool.TryParse(v, out var r) ? r : fallback;
    public int   GetInt  (string id, int fallback = 0)
        => _values.TryGetValue(id, out var v) && int.TryParse(v, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var r) ? r : fallback;
    public float GetFloat(string id, float fallback = 0f)
        => _values.TryGetValue(id, out var v) && float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var r) ? r : fallback;

    public string Save()
        => JsonSerializer.Serialize(_values, WidgetStateJsonContext.Default.DictionaryStringString);

    public void Load(string json)
    {
        _values.Clear();
        if (string.IsNullOrWhiteSpace(json)) return;
        var parsed = JsonSerializer.Deserialize(json, WidgetStateJsonContext.Default.DictionaryStringString);
        if (parsed == null) return;
        foreach (var kv in parsed) _values[kv.Key] = kv.Value;
    }

    public unsafe void SaveToFile(string path)
    {
        var json = Save();
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        var fh = sfs_open_file(path, SFS_OPEN_CREATE_WRITE);
        if (fh == IntPtr.Zero) return;
        fixed (byte* p = bytes)
            sfs_write_file(fh, p, bytes.Length);
        sfs_flush_file(fh);
        sfs_close_file(fh);
    }

    public unsafe bool LoadFromFile(string path)
    {
        if (!sfs_is_file(path)) return false;
        var fh = sfs_open_file(path, SFS_OPEN_READ);
        if (fh == IntPtr.Zero) return false;
        long sz = sfs_get_file_size(fh);
        if (sz <= 0) { sfs_close_file(fh); return false; }
        var buf = new byte[sz];
        long n;
        fixed (byte* p = buf)
            n = sfs_read_file(fh, p, sz);
        sfs_close_file(fh);
        if (n <= 0) return false;
        Load(System.Text.Encoding.UTF8.GetString(buf, 0, (int)n));
        return true;
    }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal partial class WidgetStateJsonContext : JsonSerializerContext { }
