// C# port of meshoptimizer's vertex cache optimizer (src/vcacheoptimizer.cpp), meshoptimizer v1.3,
// commit 8547fc321b782a2235dfc14c65a346a640544cd8 (2026-11-21).
// https://github.com/zeux/meshoptimizer
//
// Copyright (c) 2016-2026 Arseny Kapoulkine
// Licensed under the MIT License; see src/Framework/Renderer/Server/Resources/MeshOpt/LICENSE.md for the full text.
//
// This work is based on:
// Tom Forsyth. Linear-Speed Vertex Cache Optimisation. 2006
// Pedro Sander, Diego Nehab and Joshua Barczak. Fast Triangle Reordering for Vertex Locality and Reduced Overdraw. 2007

using System;
using System.Runtime.InteropServices;

namespace GameEditor.Framework.Renderer.Server.Resources.MeshOpt;

internal static unsafe class VertexCache
{
    const int kCacheSizeMax = 16;
    const int kValenceMax = 8;

    // Tuned to minimize the ACMR of a GPU that has a cache profile similar to NVidia and AMD
    static readonly float[] kVertexScoreTableCache = { 0f, 0.779f, 0.791f, 0.789f, 0.981f, 0.843f, 0.726f, 0.847f, 0.882f, 0.867f, 0.799f, 0.642f, 0.613f, 0.600f, 0.568f, 0.372f, 0.234f };
    static readonly float[] kVertexScoreTableLive = { 0f, 0.995f, 0.713f, 0.450f, 0.404f, 0.059f, 0.005f, 0.147f, 0.006f };

    // Tuned to minimize the encoded index buffer size
    static readonly float[] kVertexScoreTableStripCache = { 0f, 1.000f, 1.000f, 1.000f, 0.453f, 0.561f, 0.490f, 0.459f, 0.179f, 0.526f, 0.000f, 0.227f, 0.184f, 0.490f, 0.112f, 0.050f, 0.131f };
    static readonly float[] kVertexScoreTableStripLive = { 0f, 0.956f, 0.786f, 0.577f, 0.558f, 0.618f, 0.549f, 0.499f, 0.489f };

    struct TriangleAdjacency
    {
        public uint* counts;
        public uint* offsets;
        public uint* data;
    }

    static void BuildTriangleAdjacency(ref TriangleAdjacency adjacency, uint* indices, nuint index_count, nuint vertex_count, MeshoptAllocator allocator)
    {
        nuint face_count = index_count / 3;

        // allocate arrays
        adjacency.counts = allocator.Allocate<uint>(vertex_count);
        adjacency.offsets = allocator.Allocate<uint>(vertex_count);
        adjacency.data = allocator.Allocate<uint>(index_count);

        // fill triangle counts
        NativeMemory.Clear(adjacency.counts, vertex_count * sizeof(uint));

        for (nuint i = 0; i < index_count; ++i)
            adjacency.counts[indices[i]]++;

        // fill offset table
        uint offset = 0;

        for (nuint i = 0; i < vertex_count; ++i)
        {
            adjacency.offsets[i] = offset;
            offset += adjacency.counts[i];
        }

        // fill triangle data
        for (nuint i = 0; i < face_count; ++i)
        {
            uint a = indices[i * 3 + 0], b = indices[i * 3 + 1], c = indices[i * 3 + 2];

            adjacency.data[adjacency.offsets[a]++] = (uint)i;
            adjacency.data[adjacency.offsets[b]++] = (uint)i;
            adjacency.data[adjacency.offsets[c]++] = (uint)i;
        }

        // fix offsets that have been disturbed by the previous pass
        for (nuint i = 0; i < vertex_count; ++i)
            adjacency.offsets[i] -= adjacency.counts[i];
    }

    static uint GetNextVertexDeadEnd(uint* dead_end, ref uint dead_end_top, ref uint input_cursor, uint* live_triangles, nuint vertex_count)
    {
        // check dead-end stack
        while (dead_end_top != 0)
        {
            uint vertex = dead_end[--dead_end_top];

            if (live_triangles[vertex] > 0)
                return vertex;
        }

        // input order
        while (input_cursor < vertex_count)
        {
            if (live_triangles[input_cursor] > 0)
                return input_cursor;

            ++input_cursor;
        }

        return ~0u;
    }

    static uint GetNextVertexNeighbor(uint* next_candidates_begin, uint* next_candidates_end, uint* live_triangles, uint* cache_timestamps, uint timestamp, uint cache_size)
    {
        uint best_candidate = ~0u;
        int best_priority = -1;

        for (uint* next_candidate = next_candidates_begin; next_candidate != next_candidates_end; ++next_candidate)
        {
            uint vertex = *next_candidate;

            // otherwise we don't need to process it
            if (live_triangles[vertex] > 0)
            {
                int priority = 0;

                // will it be in cache after fanning?
                if (2 * live_triangles[vertex] + timestamp - cache_timestamps[vertex] <= cache_size)
                {
                    priority = (int)(timestamp - cache_timestamps[vertex]); // position in cache
                }

                if (priority > best_priority)
                {
                    best_candidate = vertex;
                    best_priority = priority;
                }
            }
        }

        return best_candidate;
    }

    static float VertexScore(float* cache, float* live, int cache_position, uint live_triangles)
    {
        uint live_triangles_clamped = live_triangles < kValenceMax ? live_triangles : kValenceMax;

        return cache[1 + cache_position] + live[live_triangles_clamped];
    }

    static uint GetNextTriangleDeadEnd(ref uint input_cursor, byte* emitted_flags, nuint face_count)
    {
        // input order
        while (input_cursor < face_count)
        {
            if (emitted_flags[input_cursor] == 0)
                return input_cursor;

            ++input_cursor;
        }

        return ~0u;
    }

    static void Validate(Span<uint> destination, ReadOnlySpan<uint> indices, int vertexCount)
    {
        if (indices.Length % 3 != 0)
            throw new ArgumentException("Index count must be a multiple of 3.", nameof(indices));
        if (destination.Length < indices.Length)
            throw new ArgumentException("Destination must hold at least indices.Length elements.", nameof(destination));
        if (vertexCount < 0)
            throw new ArgumentOutOfRangeException(nameof(vertexCount));
        MeshoptHash.CheckIndices(indices, vertexCount, nameof(indices));
    }

    static void OptimizeVertexCacheTable(Span<uint> destinationSpan, ReadOnlySpan<uint> indicesSpan, int vertexCount, float[] cacheTable, float[] liveTable)
    {
        Validate(destinationSpan, indicesSpan, vertexCount);

        nuint index_count = (nuint)indicesSpan.Length;
        nuint vertex_count = (nuint)vertexCount;

        using MeshoptAllocator allocator = new MeshoptAllocator();

        // guard for empty meshes
        if (index_count == 0 || vertex_count == 0)
            return;

        fixed (uint* destination = destinationSpan)
        fixed (uint* indicesIn = indicesSpan)
        fixed (float* table_cache = cacheTable)
        fixed (float* table_live = liveTable)
        {
            uint* indices = indicesIn;

            // support in-place optimization
            if (destinationSpan.Overlaps(indicesSpan))
            {
                uint* indices_copy = allocator.Allocate<uint>(index_count);
                Buffer.MemoryCopy(indices, indices_copy, index_count * sizeof(uint), index_count * sizeof(uint));
                indices = indices_copy;
            }

            uint cache_size = 16;

            nuint face_count = index_count / 3;

            // build adjacency information
            TriangleAdjacency adjacency = default;
            BuildTriangleAdjacency(ref adjacency, indices, index_count, vertex_count, allocator);

            // live triangle counts; note, we alias adjacency.counts as we remove triangles after emitting them so the counts always match
            uint* live_triangles = adjacency.counts;

            // emitted flags
            byte* emitted_flags = allocator.Allocate<byte>(face_count);
            NativeMemory.Clear(emitted_flags, face_count);

            // compute initial vertex scores
            float* vertex_scores = allocator.Allocate<float>(vertex_count);

            for (nuint i = 0; i < vertex_count; ++i)
                vertex_scores[i] = VertexScore(table_cache, table_live, -1, live_triangles[i]);

            // compute triangle scores
            float* triangle_scores = allocator.Allocate<float>(face_count);

            for (nuint i = 0; i < face_count; ++i)
            {
                uint a = indices[i * 3 + 0];
                uint b = indices[i * 3 + 1];
                uint c = indices[i * 3 + 2];

                triangle_scores[i] = vertex_scores[a] + vertex_scores[b] + vertex_scores[c];
            }

            uint* cache_holder = stackalloc uint[2 * (kCacheSizeMax + 4)];
            uint* cache = cache_holder;
            uint* cache_new = cache_holder + kCacheSizeMax + 4;
            nuint cache_count = 0;

            uint current_triangle = 0;
            uint input_cursor = 1;

            uint output_triangle = 0;

            while (current_triangle != ~0u)
            {
                uint a = indices[current_triangle * 3 + 0];
                uint b = indices[current_triangle * 3 + 1];
                uint c = indices[current_triangle * 3 + 2];

                // output indices
                destination[output_triangle * 3 + 0] = a;
                destination[output_triangle * 3 + 1] = b;
                destination[output_triangle * 3 + 2] = c;
                output_triangle++;

                // update emitted flags
                emitted_flags[current_triangle] = 1;
                triangle_scores[current_triangle] = 0;

                // new triangle
                nuint cache_write = 0;
                cache_new[cache_write++] = a;
                cache_new[cache_write++] = b;
                cache_new[cache_write++] = c;

                // old triangles
                for (nuint i = 0; i < cache_count; ++i)
                {
                    uint index = cache[i];

                    cache_new[cache_write] = index;
                    cache_write += (uint)((index != a ? 1 : 0) & (index != b ? 1 : 0) & (index != c ? 1 : 0));
                }

                uint* cache_temp = cache;
                cache = cache_new;
                cache_new = cache_temp;
                cache_count = cache_write > cache_size ? cache_size : cache_write;

                // remove emitted triangle from adjacency data
                // this makes sure that we spend less time traversing these lists on subsequent iterations
                // live triangle counts are updated as a byproduct of these adjustments
                for (nuint k = 0; k < 3; ++k)
                {
                    uint index = indices[current_triangle * 3 + k];

                    uint* neighbors = adjacency.data + adjacency.offsets[index];
                    nuint neighbors_size = adjacency.counts[index];

                    for (nuint i = 0; i < neighbors_size; ++i)
                    {
                        uint tri = neighbors[i];

                        if (tri == current_triangle)
                        {
                            neighbors[i] = neighbors[neighbors_size - 1];
                            adjacency.counts[index]--;
                            break;
                        }
                    }
                }

                uint best_triangle = ~0u;
                float best_score = 0;

                // update cache positions, vertex scores and triangle scores, and find next best triangle
                for (nuint i = 0; i < cache_write; ++i)
                {
                    uint index = cache[i];

                    // no need to update scores if we are never going to use this vertex
                    if (adjacency.counts[index] == 0)
                        continue;

                    int cache_position = i >= cache_size ? -1 : (int)i;

                    // update vertex score
                    float score = VertexScore(table_cache, table_live, cache_position, live_triangles[index]);
                    float score_diff = score - vertex_scores[index];

                    vertex_scores[index] = score;

                    // update scores of vertex triangles
                    uint* neighbors_begin = adjacency.data + adjacency.offsets[index];
                    uint* neighbors_end = neighbors_begin + adjacency.counts[index];

                    for (uint* it = neighbors_begin; it != neighbors_end; ++it)
                    {
                        uint tri = *it;

                        float tri_score = triangle_scores[tri] + score_diff;

                        best_triangle = best_score < tri_score ? tri : best_triangle;
                        best_score = best_score < tri_score ? tri_score : best_score;

                        triangle_scores[tri] = tri_score;
                    }
                }

                // step through input triangles in order if we hit a dead-end
                current_triangle = best_triangle;

                if (current_triangle == ~0u)
                    current_triangle = GetNextTriangleDeadEnd(ref input_cursor, emitted_flags, face_count);
            }
        }
    }

    /// <summary>
    /// Vertex transform cache optimizer (meshopt_optimizeVertexCache). Reorders triangles to reduce the number of GPU vertex shader invocations.
    /// destination must hold indices.Length elements and may alias indices.
    /// </summary>
    public static void OptimizeVertexCache(Span<uint> destination, ReadOnlySpan<uint> indices, int vertexCount)
    {
        OptimizeVertexCacheTable(destination, indices, vertexCount, kVertexScoreTableCache, kVertexScoreTableLive);
    }

    /// <summary>
    /// Vertex transform cache optimizer for strip-like caches (meshopt_optimizeVertexCacheStrip). Produces inferior results for GPU vertex caches
    /// but is better for index buffer compression. destination must hold indices.Length elements and may alias indices.
    /// </summary>
    public static void OptimizeVertexCacheStrip(Span<uint> destination, ReadOnlySpan<uint> indices, int vertexCount)
    {
        OptimizeVertexCacheTable(destination, indices, vertexCount, kVertexScoreTableStripCache, kVertexScoreTableStripLive);
    }

    /// <summary>
    /// Vertex transform cache optimizer for FIFO caches (meshopt_optimizeVertexCacheFifo). ~3x faster than <see cref="OptimizeVertexCache"/>
    /// with inferior results. cacheSize should be less than the actual GPU cache size (16 is a good default); must be at least 3.
    /// </summary>
    public static void OptimizeVertexCacheFifo(Span<uint> destinationSpan, ReadOnlySpan<uint> indicesSpan, int vertexCount, uint cacheSize)
    {
        Validate(destinationSpan, indicesSpan, vertexCount);
        if (cacheSize < 3)
            throw new ArgumentOutOfRangeException(nameof(cacheSize));

        nuint index_count = (nuint)indicesSpan.Length;
        nuint vertex_count = (nuint)vertexCount;
        uint cache_size = cacheSize;

        using MeshoptAllocator allocator = new MeshoptAllocator();

        // guard for empty meshes
        if (index_count == 0 || vertex_count == 0)
            return;

        fixed (uint* destination = destinationSpan)
        fixed (uint* indicesIn = indicesSpan)
        {
            uint* indices = indicesIn;

            // support in-place optimization
            if (destinationSpan.Overlaps(indicesSpan))
            {
                uint* indices_copy = allocator.Allocate<uint>(index_count);
                Buffer.MemoryCopy(indices, indices_copy, index_count * sizeof(uint), index_count * sizeof(uint));
                indices = indices_copy;
            }

            nuint face_count = index_count / 3;

            // build adjacency information
            TriangleAdjacency adjacency = default;
            BuildTriangleAdjacency(ref adjacency, indices, index_count, vertex_count, allocator);

            // live triangle counts
            uint* live_triangles = allocator.Allocate<uint>(vertex_count);
            Buffer.MemoryCopy(adjacency.counts, live_triangles, vertex_count * sizeof(uint), vertex_count * sizeof(uint));

            // cache time stamps
            uint* cache_timestamps = allocator.Allocate<uint>(vertex_count);
            NativeMemory.Clear(cache_timestamps, vertex_count * sizeof(uint));

            // dead-end stack
            uint* dead_end = allocator.Allocate<uint>(index_count);
            uint dead_end_top = 0;

            // emitted flags
            byte* emitted_flags = allocator.Allocate<byte>(face_count);
            NativeMemory.Clear(emitted_flags, face_count);

            uint current_vertex = 0;

            uint timestamp = cache_size + 1;
            uint input_cursor = 1; // vertex to restart from in case of dead-end

            uint output_triangle = 0;

            while (current_vertex != ~0u)
            {
                uint* next_candidates_begin = dead_end + dead_end_top;

                // emit all vertex neighbors
                uint* neighbors_begin = adjacency.data + adjacency.offsets[current_vertex];
                uint* neighbors_end = neighbors_begin + adjacency.counts[current_vertex];

                for (uint* it = neighbors_begin; it != neighbors_end; ++it)
                {
                    uint triangle = *it;

                    if (emitted_flags[triangle] == 0)
                    {
                        uint a = indices[triangle * 3 + 0], b = indices[triangle * 3 + 1], c = indices[triangle * 3 + 2];

                        // output indices
                        destination[output_triangle * 3 + 0] = a;
                        destination[output_triangle * 3 + 1] = b;
                        destination[output_triangle * 3 + 2] = c;
                        output_triangle++;

                        // update dead-end stack
                        dead_end[dead_end_top + 0] = a;
                        dead_end[dead_end_top + 1] = b;
                        dead_end[dead_end_top + 2] = c;
                        dead_end_top += 3;

                        // update live triangle counts
                        live_triangles[a]--;
                        live_triangles[b]--;
                        live_triangles[c]--;

                        // update cache info
                        // if vertex is not in cache, put it in cache
                        if (timestamp - cache_timestamps[a] > cache_size)
                            cache_timestamps[a] = timestamp++;

                        if (timestamp - cache_timestamps[b] > cache_size)
                            cache_timestamps[b] = timestamp++;

                        if (timestamp - cache_timestamps[c] > cache_size)
                            cache_timestamps[c] = timestamp++;

                        // update emitted flags
                        emitted_flags[triangle] = 1;
                    }
                }

                // next candidates are the ones we pushed to dead-end stack just now
                uint* next_candidates_end = dead_end + dead_end_top;

                // get next vertex
                current_vertex = GetNextVertexNeighbor(next_candidates_begin, next_candidates_end, live_triangles, cache_timestamps, timestamp, cache_size);

                if (current_vertex == ~0u)
                    current_vertex = GetNextVertexDeadEnd(dead_end, ref dead_end_top, ref input_cursor, live_triangles, vertex_count);
            }
        }
    }
}
