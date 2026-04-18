using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using static Sokol.SFilesystem;
using static Sokol.SFilesystem.sfs_open_mode_t;
using static Sokol.SFilesystem.sfs_result_t;

namespace Sokol.GUI;

/// <summary>
/// Saves and loads dock layouts. Applications supply a
/// <see cref="PanelContentResolver"/> delegate mapping panel-id → content
/// widget so the tree can be rehydrated without serializing widgets.
/// </summary>
public sealed class LayoutManager
{
    public delegate Widget? PanelContentResolver(string panelId);

    private readonly DockManager _dock;
    private readonly PanelContentResolver _resolver;

    public LayoutManager(DockManager dock, PanelContentResolver resolver)
    {
        _dock     = dock ?? throw new ArgumentNullException(nameof(dock));
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    }

    // ─── Capture ─────────────────────────────────────────────────────────────

    public LayoutData Capture()
    {
        var panelData = new List<DockPanelData>();
        foreach (var p in _dock.AllPanels)
            if (!p.IsFloating) panelData.Add(ToData(p));

        var floating = new List<DockPanelData>();
        foreach (var p in _dock.FloatingHost.Panels)
            floating.Add(ToData(p));

        return new LayoutData
        {
            Version  = 1,
            Root     = ToData(_dock.RootDockSpace.Root),
            Panels   = panelData,
            Floating = floating,
        };
    }

    private static DockPanelData ToData(DockPanel p) => new()
    {
        Id         = p.Id,
        Title      = p.Title,
        IsFloating = p.IsFloating,
        FloatingX  = p.FloatingBounds.X,
        FloatingY  = p.FloatingBounds.Y,
        FloatingW  = p.FloatingBounds.Width,
        FloatingH  = p.FloatingBounds.Height,
    };

    private static DockNodeData ToData(DockNode n)
    {
        var ids = new List<string>(n.Panels.Count);
        foreach (var p in n.Panels) ids.Add(p.Id);
        return new DockNodeData
        {
            Id               = n.Id,
            Type             = n.Type,
            SplitRatio       = n.SplitRatio,
            ActivePanelIndex = n.ActivePanelIndex,
            PanelIds         = ids,
            First            = n.First  != null ? ToData(n.First)  : null,
            Second           = n.Second != null ? ToData(n.Second) : null,
        };
    }

    // ─── Apply ───────────────────────────────────────────────────────────────

    public void Apply(LayoutData data)
    {
        if (data.Root == null) return;

        // Build panels map from serialized metadata; content comes from resolver.
        var panelsById = new Dictionary<string, DockPanel>();
        foreach (var pd in data.Panels) panelsById[pd.Id] = Materialize(pd);
        foreach (var pd in data.Floating) panelsById[pd.Id] = Materialize(pd);

        // Reset the docking tree and floating host.
        // Removing panels one by one triggers TreeChanged events — acceptable.
        foreach (var existing in new List<DockPanel>(_dock.AllPanels))
            _dock.Close(existing);
        foreach (var existing in new List<DockPanel>(_dock.FloatingHost.Panels))
            _dock.FloatingHost.Remove(existing);

        // Reconstruct dock tree into the existing root DockSpace.
        var newRoot = RebuildNode(data.Root, panelsById);
        // Replace root contents in place.
        var live = _dock.RootDockSpace.Root;
        CopyInto(newRoot, live);
        _dock.RootDockSpace.InvalidateLayout();
        _dock.RootDockSpace.RaiseTreeChanged();

        // Register every panel with the manager list so future Close/Float lookups work.
        foreach (var p in panelsById.Values)
        {
            if (p.IsFloating)
                _dock.FloatingHost.Add(p, p.FloatingBounds);
            else
                _dock.RegisterPanel(p);
        }
    }

    private DockPanel Materialize(DockPanelData pd)
    {
        var content = _resolver(pd.Id)
            ?? new Panel(); // placeholder so layout still binds
        var panel = new DockPanel(pd.Id, pd.Title, content)
        {
            IsFloating     = pd.IsFloating,
            FloatingBounds = new Rect(pd.FloatingX, pd.FloatingY, pd.FloatingW, pd.FloatingH),
        };
        return panel;
    }

    private static DockNode RebuildNode(DockNodeData nd, Dictionary<string, DockPanel> panels)
    {
        var node = new DockNode
        {
            Id         = nd.Id,
            Type       = nd.Type,
            SplitRatio = nd.SplitRatio,
            ActivePanelIndex = nd.ActivePanelIndex,
        };
        if (nd.Type == DockNodeType.Leaf)
        {
            foreach (var id in nd.PanelIds)
                if (panels.TryGetValue(id, out var p) && !p.IsFloating)
                    node.AddPanel(p);
        }
        else
        {
            if (nd.First  != null) { node.First  = RebuildNode(nd.First,  panels); node.First.Parent  = node; }
            if (nd.Second != null) { node.Second = RebuildNode(nd.Second, panels); node.Second.Parent = node; }
        }
        return node;
    }

    private static void CopyInto(DockNode src, DockNode dst)
    {
        dst.Id               = src.Id;
        dst.Type             = src.Type;
        dst.SplitRatio       = src.SplitRatio;
        dst.ActivePanelIndex = src.ActivePanelIndex;
        dst.First            = src.First;
        dst.Second           = src.Second;
        if (dst.First  != null) dst.First.Parent  = dst;
        if (dst.Second != null) dst.Second.Parent = dst;
        dst.Panels.Clear();
        foreach (var p in src.Panels) { p.Owner = dst; dst.Panels.Add(p); }
    }

    // ─── File IO (cross-platform via SFilesystem) ──────────────────────────

    public unsafe void SaveToFile(string path)
    {
        var json = LayoutSerializer.Save(Capture());
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
        var json = System.Text.Encoding.UTF8.GetString(buf, 0, (int)n);
        var data = LayoutSerializer.Load(json);
        if (data == null) return false;
        Apply(data);
        return true;
    }

    // ─── Default user-prefs location ─────────────────────────────────────────

    public static string DefaultLayoutPath(string appName = "Sokol.GUI", string file = "layout.json")
    {
        var ptr = sfs_get_pref_path("SokolNET", appName);
        if (ptr == IntPtr.Zero) return "";
        var dir = Marshal.PtrToStringUTF8(ptr) ?? "";
        sfs_free_path(ptr);
        if (dir.Length == 0) return "";
        sfs_create_directory(dir);
        return dir + file;
    }

    public void SaveToUserPrefs(string appName = "Sokol.GUI")
    {
        var path = DefaultLayoutPath(appName);
        if (path.Length > 0) SaveToFile(path);
    }

    public bool LoadFromUserPrefs(string appName = "Sokol.GUI")
    {
        var path = DefaultLayoutPath(appName);
        return path.Length > 0 && LoadFromFile(path);
    }
}
