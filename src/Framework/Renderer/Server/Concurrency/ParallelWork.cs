using System;
#if !WEB
using System.Collections.Concurrent;
using System.Threading.Tasks;
#endif

namespace GameEditor.Framework.Renderer.Server.Concurrency
{
    /// <summary>
    /// Work over [0, count) cut into chunks: on the thread pool (native), in order on this thread on the web (one thread).
    /// The body gets a chunk's [from, to); chunks run at once, so a body writes only its own items.
    /// </summary>
    public static class ParallelWork
    {
        public static void For(int count, int chunk, Action<int, int> body)
        {
            if (count <= 0) return;
#if WEB
            body(0, count);
#else
            if (count <= chunk) { body(0, count); return; }
            Parallel.ForEach(Partitioner.Create(0, count, chunk), r => body(r.Item1, r.Item2));
#endif
        }
    }
}
