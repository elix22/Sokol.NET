using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using static Sokol.SFetch;
using static Sokol.SLog;
using static Sokol.Utils;
namespace GameEditor.Framework.Core
{
    /// <summary>
    /// Cross-platform file loading system using sokol_fetch for async I/O.
    /// On desktop the same sfetch calls work synchronously (one Update pump suffices).
    /// On WebAssembly they execute asynchronously — call Update() every frame.
    ///
    /// Usage:
    ///   GameFileSystem.Instance.Initialize();   // in Init()
    ///   GameFileSystem.Instance.Update();        // in Frame(), before rendering
    ///   GameFileSystem.Instance.Shutdown();      // in Cleanup()
    ///   GameFileSystem.Instance.LoadFile("path", (p, data, status) => { ... });
    /// </summary>
    public sealed unsafe class GameFileSystem
    {
        // ── Singleton ────────────────────────────────────────────────────────
        private static GameFileSystem? _instance;
        public static GameFileSystem Instance => _instance ??= new GameFileSystem();

        // ── Configuration ────────────────────────────────────────────────────
        private const int MaxRequests = 64;
        private const int NumChannels = 2;
        private const int NumLanes = 4;
        private const int DefaultBufSz = 4 * 1024 * 1024; // 4 MB

        // ── State ────────────────────────────────────────────────────────────
        private bool _initialized;

        // Active requests indexed by sfetch handle id
        private readonly Dictionary<uint, PendingLoad> _active = new();
        private readonly Queue<PendingLoad> _queue = new();

        private GameFileSystem() { }

        // ── Public API ───────────────────────────────────────────────────────

        public bool IsInitialized => _initialized;

        /// <summary>Set up sokol_fetch.  Must be called once from your Init() callback.</summary>
        public void Initialize()
        {
            if (_initialized) return;
            sfetch_setup(new sfetch_desc_t
            {
                max_requests = MaxRequests,
                num_channels = NumChannels,
                num_lanes = NumLanes,
                logger = { func = &slog_func }
            });
            _initialized = true;
            Logger.Info("[GameFileSystem] Initialized.");
        }

        /// <summary>
        /// Pump sokol_fetch and start any queued requests.
        /// Must be called once per frame from your Frame() callback.
        /// </summary>
        public void Update()
        {
            if (!_initialized) return;
            sfetch_dowork();
            FlushQueue();
        }

        /// <summary>
        /// Shut down sokol_fetch.  Call once from your Cleanup() callback.
        /// </summary>
        public void Shutdown()
        {
            if (!_initialized) return;
            sfetch_shutdown();
            _active.Clear();
            _queue.Clear();
            _initialized = false;
            Logger.Info("[GameFileSystem] Shut down.");
        }

        /// <summary>
        /// Request an async file load.  <paramref name="callback"/> is invoked on the main
        /// thread (inside the next <see cref="Update"/> call) when the load finishes.
        /// </summary>
        public void LoadFile(string filePath, FileLoadCallback callback,
                             int bufferSize = DefaultBufSz)
        {
            if (!_initialized)
                throw new InvalidOperationException("[GameFileSystem] Call Initialize() first.");

            _queue.Enqueue(new PendingLoad
            {
                FilePath = filePath,
                Callback = callback,
                BufferSize = bufferSize
            });
            FlushQueue();
        }

        // ── Private helpers ──────────────────────────────────────────────────

        private void FlushQueue()
        {
            while (_queue.Count > 0 && _active.Count < MaxRequests)
            {
                var load = _queue.Dequeue();
                StartLoad(load);
            }
        }

        private void StartLoad(PendingLoad load)
        {
            var buf = Sokol.SharedBuffer.Create((uint)load.BufferSize);
            var bufPtr = (void*)buf.GetBufferPointer();
            string platformPath = util_get_file_path(load.FilePath);
            var handle = sfetch_send(new sfetch_request_t
            {
                path = platformPath,
                callback = &FetchCallback,
                buffer = new sfetch_range_t { ptr = bufPtr, size = (nuint)load.BufferSize },
            });

            // Store PendingLoad keyed by handle id
            load.Buffer = buf;
            load.Handle = handle;
            _active[handle.id] = load;
        }

        [UnmanagedCallersOnly]
        private static void FetchCallback(sfetch_response_t* resp)
        {
            var fs = Instance;
            if (!fs._active.TryGetValue(resp->handle.id, out var load))
                return;

            fs._active.Remove(resp->handle.id);

            FileLoadStatus status;
            byte[]? data = null;

            if (resp->fetched)
            {
                status = FileLoadStatus.Success;
                int len = (int)resp->data.size;
                data = new byte[len];
                Marshal.Copy((IntPtr)resp->data.ptr, data, 0, len);
            }
            else if (resp->failed)
            {
                status = FileLoadStatus.Failed;
                Logger.Warning($"[GameFileSystem] Failed to load: {load.FilePath}");
            }
            else
            {
                status = FileLoadStatus.Cancelled;
            }

            load.Buffer?.Dispose();
            load.Callback?.Invoke(load.FilePath, data, status);
        }

        // ── Inner types ──────────────────────────────────────────────────────
        private class PendingLoad
        {
            public string FilePath = "";
            public FileLoadCallback? Callback;
            public int BufferSize = DefaultBufSz;
            public Sokol.SharedBuffer? Buffer;
            public sfetch_handle_t Handle;
        }
    }

    // ── Public enums / delegates ─────────────────────────────────────────────

    public enum FileLoadStatus { Success, Failed, NotFound, Cancelled }

    /// <summary>Callback signature for <see cref="GameFileSystem.LoadFile"/>.</summary>
    public delegate void FileLoadCallback(string filePath, byte[]? data, FileLoadStatus status);
}
