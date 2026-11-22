// C# port of meshoptimizer's overdraw optimizer (src/overdrawoptimizer.cpp), meshoptimizer v1.3,
// commit 8547fc321b782a2235dfc14c65a346a640544cd8 (2026-11-21).
// https://github.com/zeux/meshoptimizer
//
// Copyright (c) 2016-2026 Arseny Kapoulkine
// Licensed under the MIT License; see src/Framework/Renderer/Server/Resources/MeshOpt/LICENSE.md for the full text.
//
// This work is based on:
// Pedro Sander, Diego Nehab and Joshua Barczak. Fast Triangle Reordering for Vertex Locality and Reduced Overdraw. 2007

using System;
using System.Runtime.InteropServices;

namespace GameEditor.Framework.Renderer.Server.Resources.MeshOpt;

internal static unsafe class Overdraw
{
    static void CalculateSortData(float* sort_data, uint* indices, nuint index_count, float* vertex_positions, nuint vertex_count, nuint vertex_stride_float, uint* clusters, nuint cluster_count)
    {
        float* mesh_centroid = stackalloc float[3] { 0f, 0f, 0f };

        for (nuint i = 0; i < vertex_count; ++i)
        {
            float* p = vertex_positions + vertex_stride_float * i;

            mesh_centroid[0] += p[0];
            mesh_centroid[1] += p[1];
            mesh_centroid[2] += p[2];
        }

        mesh_centroid[0] /= (float)vertex_count;
        mesh_centroid[1] /= (float)vertex_count;
        mesh_centroid[2] /= (float)vertex_count;

        for (nuint cluster = 0; cluster < cluster_count; ++cluster)
        {
            nuint cluster_begin = (nuint)clusters[cluster] * 3;
            nuint cluster_end = (cluster + 1 < cluster_count) ? (nuint)clusters[cluster + 1] * 3 : index_count;

            float cluster_area = 0;
            float cx = 0, cy = 0, cz = 0;
            float nx = 0, ny = 0, nz = 0;

            for (nuint i = cluster_begin; i < cluster_end; i += 3)
            {
                float* p0 = vertex_positions + vertex_stride_float * indices[i + 0];
                float* p1 = vertex_positions + vertex_stride_float * indices[i + 1];
                float* p2 = vertex_positions + vertex_stride_float * indices[i + 2];

                float p10x = p1[0] - p0[0], p10y = p1[1] - p0[1], p10z = p1[2] - p0[2];
                float p20x = p2[0] - p0[0], p20y = p2[1] - p0[1], p20z = p2[2] - p0[2];

                float normalx = p10y * p20z - p10z * p20y;
                float normaly = p10z * p20x - p10x * p20z;
                float normalz = p10x * p20y - p10y * p20x;

                float area = MathF.Sqrt(normalx * normalx + normaly * normaly + normalz * normalz);

                cx += (p0[0] + p1[0] + p2[0]) * (area / 3);
                cy += (p0[1] + p1[1] + p2[1]) * (area / 3);
                cz += (p0[2] + p1[2] + p2[2]) * (area / 3);
                nx += normalx;
                ny += normaly;
                nz += normalz;
                cluster_area += area;
            }

            float inv_cluster_area = cluster_area == 0 ? 0 : 1 / cluster_area;

            cx *= inv_cluster_area;
            cy *= inv_cluster_area;
            cz *= inv_cluster_area;

            float cluster_normal_length = MathF.Sqrt(nx * nx + ny * ny + nz * nz);
            float inv_cluster_normal_length = cluster_normal_length == 0 ? 0 : 1 / cluster_normal_length;

            nx *= inv_cluster_normal_length;
            ny *= inv_cluster_normal_length;
            nz *= inv_cluster_normal_length;

            float vx = cx - mesh_centroid[0], vy = cy - mesh_centroid[1], vz = cz - mesh_centroid[2];

            sort_data[cluster] = vx * nx + vy * ny + vz * nz;
        }
    }

    static int QuantizeUnorm(float v, int N)
    {
        float scale = (float)((1 << N) - 1);

        v = (v >= 0) ? v : 0;
        v = (v <= 1) ? v : 1;

        return (int)(v * scale + 0.5f);
    }

    static void CalculateSortOrderRadix(uint* sort_order, float* sort_data, ushort* sort_keys, nuint cluster_count)
    {
        // compute sort data bounds and renormalize, using fixed point snorm
        float sort_data_max = 1e-3f;

        for (nuint i = 0; i < cluster_count; ++i)
        {
            float dpa = MathF.Abs(sort_data[i]);

            sort_data_max = (sort_data_max < dpa) ? dpa : sort_data_max;
        }

        const int sort_bits = 11;

        for (nuint i = 0; i < cluster_count; ++i)
        {
            // note that we flip distribution since high dot product should come first
            float sort_key = 0.5f - 0.5f * (sort_data[i] / sort_data_max);

            sort_keys[i] = (ushort)(QuantizeUnorm(sort_key, sort_bits) & ((1 << sort_bits) - 1));
        }

        // fill histogram for counting sort
        uint* histogram = stackalloc uint[1 << sort_bits];
        NativeMemory.Clear(histogram, (1 << sort_bits) * sizeof(uint));

        for (nuint i = 0; i < cluster_count; ++i)
            histogram[sort_keys[i]]++;

        // compute offsets based on histogram data
        nuint histogram_sum = 0;

        for (int i = 0; i < 1 << sort_bits; ++i)
        {
            nuint count = histogram[i];
            histogram[i] = (uint)histogram_sum;
            histogram_sum += count;
        }

        // compute sort order based on offsets
        for (nuint i = 0; i < cluster_count; ++i)
            sort_order[histogram[sort_keys[i]]++] = (uint)i;
    }

    static uint UpdateCache(uint a, uint b, uint c, uint cache_size, uint* cache_timestamps, ref uint timestamp)
    {
        uint cache_misses = 0;

        // if vertex is not in cache, put it in cache
        if (timestamp - cache_timestamps[a] > cache_size)
        {
            cache_timestamps[a] = timestamp++;
            cache_misses++;
        }

        if (timestamp - cache_timestamps[b] > cache_size)
        {
            cache_timestamps[b] = timestamp++;
            cache_misses++;
        }

        if (timestamp - cache_timestamps[c] > cache_size)
        {
            cache_timestamps[c] = timestamp++;
            cache_misses++;
        }

        return cache_misses;
    }

    static nuint GenerateHardBoundaries(uint* destination, uint* indices, nuint index_count, nuint vertex_count, uint cache_size, uint* cache_timestamps)
    {
        NativeMemory.Clear(cache_timestamps, vertex_count * sizeof(uint));

        uint timestamp = cache_size + 1;

        nuint face_count = index_count / 3;

        nuint result = 0;

        for (nuint i = 0; i < face_count; ++i)
        {
            uint m = UpdateCache(indices[i * 3 + 0], indices[i * 3 + 1], indices[i * 3 + 2], cache_size, cache_timestamps, ref timestamp);

            // when all three vertices are not in the cache it's usually relatively safe to assume that this is a new patch in the mesh
            // that is disjoint from previous vertices; sometimes it might come back to reference existing vertices but that frequently
            // suggests an inefficiency in the vertex cache optimization algorithm
            // usually the first triangle has 3 misses unless it's degenerate - thus we make sure the first cluster always starts with 0
            if (i == 0 || m == 3)
                destination[result++] = (uint)i;
        }

        return result;
    }

    static nuint GenerateSoftBoundaries(uint* destination, uint* indices, nuint index_count, nuint vertex_count, uint* clusters, nuint cluster_count, uint cache_size, float threshold, uint* cache_timestamps)
    {
        NativeMemory.Clear(cache_timestamps, vertex_count * sizeof(uint));

        uint timestamp = 0;

        nuint result = 0;

        for (nuint it = 0; it < cluster_count; ++it)
        {
            nuint start = clusters[it];
            nuint end = (it + 1 < cluster_count) ? clusters[it + 1] : index_count / 3;

            // reset cache
            timestamp += cache_size + 1;

            // measure cluster ACMR
            uint cluster_misses = 0;

            for (nuint i = start; i < end; ++i)
            {
                uint m = UpdateCache(indices[i * 3 + 0], indices[i * 3 + 1], indices[i * 3 + 2], cache_size, cache_timestamps, ref timestamp);

                cluster_misses += m;
            }

            float cluster_threshold = threshold * ((float)cluster_misses / (float)(end - start));

            // first cluster always starts from the hard cluster boundary
            destination[result++] = (uint)start;

            // reset cache
            timestamp += cache_size + 1;

            uint running_misses = 0;
            uint running_faces = 0;

            for (nuint i = start; i < end; ++i)
            {
                uint m = UpdateCache(indices[i * 3 + 0], indices[i * 3 + 1], indices[i * 3 + 2], cache_size, cache_timestamps, ref timestamp);

                running_misses += m;
                running_faces += 1;

                if ((float)running_misses / (float)running_faces <= cluster_threshold)
                {
                    // we have reached the target ACMR with the current triangle so we need to start a new cluster on the next one
                    // note that this may mean that we add 'end` to destination for the last triangle, which will imply that the last
                    // cluster is empty; however, the 'pop_back' after the loop will clean it up
                    destination[result++] = (uint)(i + 1);

                    // reset cache
                    timestamp += cache_size + 1;

                    running_misses = 0;
                    running_faces = 0;
                }
            }

            // each time we reach the target ACMR we flush the cluster
            // this means that the last cluster is by definition not very good - there are frequent cases where we are left with a few triangles
            // in the last cluster, producing a very bad ACMR and significantly penalizing the overall results
            // thus we remove the last cluster boundary, merging the last complete cluster with the last incomplete one
            // there are sometimes cases when the last cluster is actually good enough - in which case the code above would have added 'end'
            // to the cluster boundary array which we need to remove anyway - this code will do that automatically
            if (destination[result - 1] != start)
                result--;
        }

        return result;
    }

    /// <summary>
    /// Overdraw optimizer (meshopt_optimizeOverdraw). Reorders triangles to reduce overdraw while maintaining vertex cache efficiency.
    /// indices must be the result of <see cref="VertexCache.OptimizeVertexCache"/>; threshold 1.05 allows up to 5% worse ACMR.
    /// destination must hold indices.Length elements and may alias indices; positionStride is in floats.
    /// </summary>
    public static void OptimizeOverdraw(Span<uint> destinationSpan, ReadOnlySpan<uint> indicesSpan, ReadOnlySpan<float> positions, int vertexCount, int positionStride, float threshold)
    {
        if (indicesSpan.Length % 3 != 0)
            throw new ArgumentException("Index count must be a multiple of 3.", nameof(indicesSpan));
        if (destinationSpan.Length < indicesSpan.Length)
            throw new ArgumentException("Destination must hold at least indices.Length elements.", nameof(destinationSpan));
        MeshoptHash.CheckStridedFloats(positions.Length, vertexCount, positionStride, 3, nameof(positions));
        MeshoptHash.CheckIndices(indicesSpan, vertexCount, nameof(indicesSpan));

        nuint index_count = (nuint)indicesSpan.Length;
        nuint vertex_count = (nuint)vertexCount;

        using MeshoptAllocator allocator = new MeshoptAllocator();

        // guard for empty meshes
        if (index_count == 0 || vertex_count == 0)
            return;

        fixed (uint* destination = destinationSpan)
        fixed (uint* indicesIn = indicesSpan)
        fixed (float* vertex_positions = positions)
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

            uint* cache_timestamps = allocator.Allocate<uint>(vertex_count);

            // generate hard boundaries from full-triangle cache misses
            uint* hard_clusters = allocator.Allocate<uint>(index_count / 3);
            nuint hard_cluster_count = GenerateHardBoundaries(hard_clusters, indices, index_count, vertex_count, cache_size, cache_timestamps);

            // generate soft boundaries
            uint* soft_clusters = allocator.Allocate<uint>(index_count / 3 + 1);
            nuint soft_cluster_count = GenerateSoftBoundaries(soft_clusters, indices, index_count, vertex_count, hard_clusters, hard_cluster_count, cache_size, threshold, cache_timestamps);

            uint* clusters = soft_clusters;
            nuint cluster_count = soft_cluster_count;

            // fill sort data
            float* sort_data = allocator.Allocate<float>(cluster_count);
            CalculateSortData(sort_data, indices, index_count, vertex_positions, vertex_count, (nuint)positionStride, clusters, cluster_count);

            // sort clusters using sort data
            ushort* sort_keys = allocator.Allocate<ushort>(cluster_count);
            uint* sort_order = allocator.Allocate<uint>(cluster_count);
            CalculateSortOrderRadix(sort_order, sort_data, sort_keys, cluster_count);

            // fill output buffer
            nuint offset = 0;

            for (nuint it = 0; it < cluster_count; ++it)
            {
                uint cluster = sort_order[it];

                nuint cluster_begin = (nuint)clusters[cluster] * 3;
                nuint cluster_end = (cluster + 1 < cluster_count) ? (nuint)clusters[cluster + 1] * 3 : index_count;

                nuint bytes = (cluster_end - cluster_begin) * sizeof(uint);
                Buffer.MemoryCopy(indices + cluster_begin, destination + offset, bytes, bytes);
                offset += cluster_end - cluster_begin;
            }
        }
    }
}
