

using System.Runtime.InteropServices;
using static Sokol.SFetch;
using static Sokol.SLog;
using static Sokol.Utils;

namespace Sokol
{
    /// <summary>
    /// File loading status for callbacks
    /// </summary>
    public enum FileLoadStatus
    {
        Success,
        Failed,
        NotFound,
        BufferTooSmall,
        Cancelled
    }

    /// <summary>
    /// Delegate signature for file load completion callbacks
    /// </summary>
    /// <param name="filePath">Path of the file that was loaded</param>
    /// <param name="buffer">Loaded file data (null if failed)</param>
    /// <param name="status">Load operation status</param>
    public delegate void FileLoadCallback(string filePath, byte[]? buffer, FileLoadStatus status);

    /// <summary>
    /// Internal structure to track pending file load requests
    /// </summary>
    internal struct FileLoadRequest
    {
        public string FilePath;
        public FileLoadCallback Callback;
        public SharedBuffer Buffer;
        public sfetch_handle_t Handle;
        public bool IsActive;
        public uint OriginalBufferSize;  // Track original requested buffer size
        public bool IsRetry;           // Track if this is a retry attempt
        public int RetryCount;         // Number of retry attempts
    }

    /// <summary>
    /// Singleton FileSystem class for managing asynchronous file loading using sokol-fetch
    /// Handles sequential processing of file load requests with callback notifications
    /// </summary>
    public unsafe class FileSystem
    {
        private static FileSystem? _instance;
        private static readonly object _lock = new object();

        // Sokol-fetch configuration
        private const int MAX_CONCURRENT_REQUESTS = 8;  // Number of simultaneous downloads (increased for texture loading)
        private const int NUM_CHANNELS = 2;             // Separate channels for different request types
        private const int NUM_LANES = 2;               // Lanes per channel
        private const int DEFAULT_BUFFER_SIZE = 1024 * 1024; // 1MB default buffer size
        private const int MAX_RETRY_ATTEMPTS = 4;      // Maximum retry attempts for buffer too small
        private const int RETRY_BUFFER_MULTIPLIER = 4; // Multiply buffer size by this factor on retry

        // Request management
        private readonly Queue<FileLoadRequest> _pendingRequests = new Queue<FileLoadRequest>();
        private readonly Dictionary<uint, FileLoadRequest> _activeRequests = new Dictionary<uint, FileLoadRequest>();
        private readonly List<SharedBuffer> _bufferPool = new List<SharedBuffer>();
        
        private bool _isInitialized = false;

        /// <summary>
        /// Get the singleton instance of FileSystem
        /// </summary>
        public static FileSystem Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new FileSystem();
                    }
                }
                return _instance;
            }
        }

        private FileSystem()
        {
            // Private constructor for singleton pattern
        }

        /// <summary>
        /// Initialize the FileSystem with sokol-fetch
        /// Must be called before any file operations
        /// </summary>
        public void Initialize()
        {
            if (_isInitialized)
                return;

            Info("FileSystem: Initializing sokol-fetch...");

            // Setup sokol-fetch with multiple channels and lanes for concurrent loading
            sfetch_setup(new sfetch_desc_t()
            {
                max_requests = MAX_CONCURRENT_REQUESTS,
                num_channels = NUM_CHANNELS,
                num_lanes = NUM_LANES,
                logger = {
                    func = &slog_func,
                }
            });

            // Pre-allocate buffer pool
            for (int i = 0; i < MAX_CONCURRENT_REQUESTS; i++)
            {
                _bufferPool.Add(SharedBuffer.Create(DEFAULT_BUFFER_SIZE));
            }

            _isInitialized = true;
            Info($"FileSystem: Initialized with {MAX_CONCURRENT_REQUESTS} max requests, {NUM_CHANNELS} channels, {NUM_LANES} lanes");
        }

        /// <summary>
        /// Shutdown the FileSystem and cleanup resources
        /// </summary>
        public void Shutdown()
        {
            if (!_isInitialized)
                return;

            Info("FileSystem: Shutting down...");

            // Cancel all active requests
            foreach (var request in _activeRequests.Values)
            {
                sfetch_cancel(request.Handle);
                request.Buffer.Dispose();
            }
            _activeRequests.Clear();

            // Clear pending requests and dispose their buffers
            while (_pendingRequests.Count > 0)
            {
                var request = _pendingRequests.Dequeue();
                request.Buffer.Dispose();
            }

            // Dispose buffer pool
            foreach (var buffer in _bufferPool)
            {
                buffer.Dispose();
            }
            _bufferPool.Clear();

            sfetch_shutdown();
            _isInitialized = false;
        }

        /// <summary>
        /// Request to load a file asynchronously
        /// Files are processed sequentially in the order they are requested
        /// </summary>
        /// <param name="filePath">Path to the file to load</param>
        /// <param name="callback">Callback to invoke when loading completes</param>
        /// <param name="bufferSize">Optional custom buffer size (uses default if not specified)</param>
        public void LoadFile(string filePath, FileLoadCallback callback, uint bufferSize = DEFAULT_BUFFER_SIZE)
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("FileSystem must be initialized before use. Call Initialize() first.");
            }

            if (string.IsNullOrEmpty(filePath))
            {
                callback?.Invoke(filePath ?? "", null, FileLoadStatus.Failed);
                return;
            }

            if (callback == null)
            {
                Info($"FileSystem: Warning - No callback provided for file: {filePath}");
                return;
            }

            // Create request with custom buffer size if needed
            var buffer = bufferSize == DEFAULT_BUFFER_SIZE && _bufferPool.Count > 0 
                ? GetPooledBuffer() 
                : SharedBuffer.Create(bufferSize);

            var request = new FileLoadRequest
            {
                FilePath = filePath,
                Callback = callback,
                Buffer = buffer,
                IsActive = false,
                OriginalBufferSize = bufferSize,
                IsRetry = false,
                RetryCount = 0
            };

            lock (_lock)
            {
                _pendingRequests.Enqueue(request);
                Info($"FileSystem: Queued file load request: {filePath} (Queue size: {_pendingRequests.Count})");
            }

            // Try to process immediately if we have capacity
            ProcessPendingRequests();
        }

        /// <summary>
        /// Load a file synchronously by blocking until the async operation completes
        /// This uses the async mechanism internally but blocks the calling thread
        /// WARNING: This will block the thread until the file is loaded. Use sparingly.
        /// NOTE: On WebAssembly/browser platforms, this may not work as expected due to threading limitations
        /// </summary>
        /// <param name="filePath">Path to the file to load</param>
        /// <param name="bufferSize">Optional custom buffer size (uses default if not specified)</param>
        /// <param name="timeoutMs">Timeout in milliseconds (default: 10000ms)</param>
        /// <returns>Tuple of (data buffer, load status)</returns>
        public (byte[]? data, FileLoadStatus status) LoadFileSync(string filePath, uint bufferSize = DEFAULT_BUFFER_SIZE, int timeoutMs = 10000)
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("FileSystem must be initialized before use. Call Initialize() first.");
            }

            if (string.IsNullOrEmpty(filePath))
            {
                return (null, FileLoadStatus.Failed);
            }

            byte[]? resultData = null;
            FileLoadStatus resultStatus = FileLoadStatus.Failed;
            bool completed = false;

            // Create callback that signals completion
            FileLoadCallback syncCallback = (path, data, status) =>
            {
                resultData = data;
                resultStatus = status;
                completed = true;
            };

            // Start the async load
            LoadFile(filePath, syncCallback, bufferSize);

            // Spin-wait with updates until completion or timeout
            var startTime = System.Diagnostics.Stopwatch.StartNew();
            while (!completed && startTime.ElapsedMilliseconds < timeoutMs)
            {
                // Process file system updates - this is critical for the async operation to complete
                Update();
                
                // Small delay to prevent busy-waiting from consuming too much CPU
                System.Threading.Thread.Sleep(1);
            }

            if (!completed)
            {
                Info($"FileSystem: Sync load timeout for {filePath} after {timeoutMs}ms");
                return (null, FileLoadStatus.Failed);
            }

            return (resultData, resultStatus);
        }

        /// <summary>
        /// Update the FileSystem - must be called each frame to process sokol-fetch work
        /// </summary>
        public void Update()
        {
            if (!_isInitialized)
                return;

            // Process sokol-fetch work
            sfetch_dowork();

            // Try to start new requests if we have capacity
            ProcessPendingRequests();
        }

        /// <summary>
        /// Get the number of pending requests in the queue
        /// </summary>
        public int PendingRequestCount
        {
            get
            {
                lock (_lock)
                {
                    return _pendingRequests.Count;
                }
            }
        }

        /// <summary>
        /// Get the number of currently active requests
        /// </summary>
        public int ActiveRequestCount
        {
            get
            {
                lock (_lock)
                {
                    return _activeRequests.Count;
                }
            }
        }

        private SharedBuffer GetPooledBuffer()
        {
            if (_bufferPool.Count > 0)
            {
                var buffer = _bufferPool[_bufferPool.Count - 1];
                _bufferPool.RemoveAt(_bufferPool.Count - 1);
                return buffer;
            }
            return SharedBuffer.Create(DEFAULT_BUFFER_SIZE);
        }

        private void ReturnPooledBuffer(SharedBuffer buffer)
        {
            if (buffer.Size == (uint)DEFAULT_BUFFER_SIZE && _bufferPool.Count < MAX_CONCURRENT_REQUESTS)
            {
                _bufferPool.Add(buffer);
            }
            else
            {
                // Dispose larger buffers immediately to preserve memory
                buffer.Dispose();
            }
        }

        private void ProcessPendingRequests()
        {
            lock (_lock)
            {
                // Start new requests if we have capacity and pending requests
                while (_activeRequests.Count < MAX_CONCURRENT_REQUESTS && _pendingRequests.Count > 0)
                {
                    var request = _pendingRequests.Dequeue();
                    StartRequest(request);
                }
            }
        }

        private void StartRequest(FileLoadRequest request)
        {
            // Convert to platform-specific path (iOS/Android need bundle paths)
            string platformPath = util_get_file_path(request.FilePath);
            Info($"FileSystem: Starting file load: {request.FilePath} -> {platformPath}");

            // Choose channel based on request count to distribute load
            uint channel = (uint)(_activeRequests.Count % NUM_CHANNELS);

            var sfetchRequest = new sfetch_request_t
            {
                path = platformPath,
                channel = channel,
                callback = &FileLoadCallback_Internal,
                buffer = SFETCH_RANGE(request.Buffer)
            };

            var handle = sfetch_send(sfetchRequest);
            
            if (sfetch_handle_valid(handle))
            {
                request.Handle = handle;
                request.IsActive = true;
                _activeRequests[handle.id] = request;
                Info($"FileSystem: Started request for {request.FilePath} (handle: {handle.id}, channel: {channel})");
            }
            else
            {
                Info($"FileSystem: Failed to start request for {request.FilePath}");
                request.Callback?.Invoke(request.FilePath, null, FileLoadStatus.Failed);
                ReturnPooledBuffer(request.Buffer);
            }
        }

        [UnmanagedCallersOnly]
        private static void FileLoadCallback_Internal(sfetch_response_t* response)
        {
            Instance.HandleFileLoadResponse(response);
        }

        private void HandleFileLoadResponse(sfetch_response_t* response)
        {
            if (!_activeRequests.TryGetValue(response->handle.id, out var request))
            {
                Info($"FileSystem: Received response for unknown handle: {response->handle.id}");
                return;
            }

            if (response->finished)
            {
                FileLoadStatus status;
                byte[]? buffer = null;

                if (response->failed)
                {
                    // Check if buffer was too small and we can retry
                    if (response->error_code == sfetch_error_t.SFETCH_ERROR_BUFFER_TOO_SMALL && 
                        request.RetryCount < MAX_RETRY_ATTEMPTS)
                    {
                        // Automatically retry with a larger buffer
                        RetryWithLargerBuffer(request);
                        return;
                    }

                    status = response->error_code switch
                    {
                        sfetch_error_t.SFETCH_ERROR_FILE_NOT_FOUND => FileLoadStatus.NotFound,
                        sfetch_error_t.SFETCH_ERROR_BUFFER_TOO_SMALL => FileLoadStatus.BufferTooSmall,
                        sfetch_error_t.SFETCH_ERROR_CANCELLED => FileLoadStatus.Cancelled,
                        _ => FileLoadStatus.Failed
                    };
                    Info($"FileSystem: Failed to load {request.FilePath}: {response->error_code}");
                }
                else
                {
                    status = FileLoadStatus.Success;
                    // Copy data to managed array
                    buffer = new byte[response->data.size];
                    Marshal.Copy((IntPtr)response->data.ptr, buffer, 0, (int)response->data.size);
                    Info($"FileSystem: Successfully loaded {request.FilePath} ({response->data.size} bytes)");
                }

                // Remove from active requests
                lock (_lock)
                {
                    _activeRequests.Remove(response->handle.id);
                }

                // Invoke callback
                try
                {
                    request.Callback?.Invoke(request.FilePath, buffer, status);
                }
                catch (Exception ex)
                {
                    Info($"FileSystem: Exception in callback for {request.FilePath}: {ex.Message}");
                }

                // Return buffer to pool (or dispose if large)
                ReturnPooledBuffer(request.Buffer);

                // Try to process more pending requests
                ProcessPendingRequests();
            }
            else if (response->fetched)
            {
                Info($"FileSystem: Fetching {request.FilePath} - {response->data.size} bytes received");
            }
        }

        private void RetryWithLargerBuffer(FileLoadRequest originalRequest)
        {
            // Calculate new buffer size (4x the current buffer size, not original)
            uint newBufferSize = (uint)(originalRequest.Buffer.Size * RETRY_BUFFER_MULTIPLIER);

            Info($"FileSystem: Retrying {originalRequest.FilePath} with larger buffer ({newBufferSize} bytes, attempt {originalRequest.RetryCount + 1})");

            // Create retry request
            var retryRequest = new FileLoadRequest
            {
                FilePath = originalRequest.FilePath,
                Callback = originalRequest.Callback,
                Buffer = SharedBuffer.Create(newBufferSize), // Always create new buffer for retry
                Handle = default,
                IsActive = false,
                OriginalBufferSize = originalRequest.OriginalBufferSize, // Keep original size for reference
                IsRetry = true,
                RetryCount = originalRequest.RetryCount + 1
            };

            // Remove original request from active requests
            lock (_lock)
            {
                _activeRequests.Remove(originalRequest.Handle.id);
            }

            // Return the original buffer to pool
            ReturnPooledBuffer(originalRequest.Buffer);

            // Add retry request to the front of the queue for immediate processing
            lock (_lock)
            {
                // Create a temporary list to hold all current pending requests
                var currentRequests = new List<FileLoadRequest>();
                while (_pendingRequests.Count > 0)
                {
                    currentRequests.Add(_pendingRequests.Dequeue());
                }
                
                // Add retry request first
                _pendingRequests.Enqueue(retryRequest);
                
                // Re-add all existing requests
                foreach (var req in currentRequests)
                {
                    _pendingRequests.Enqueue(req);
                }
            }
            
            // Try to process it immediately
            ProcessPendingRequests();
        }
    }
}
