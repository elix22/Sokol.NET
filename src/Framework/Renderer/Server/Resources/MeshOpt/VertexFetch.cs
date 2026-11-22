// C# port of meshoptimizer's vertex fetch optimizer (src/vfetchoptimizer.cpp), meshoptimizer v1.3,
// commit 8547fc321b782a2235dfc14c65a346a640544cd8 (2026-11-21).
// https://github.com/zeux/meshoptimizer
//
// Copyright (c) 2016-2026 Arseny Kapoulkine
// Licensed under the MIT License; see src/Framework/Renderer/Server/Resources/MeshOpt/LICENSE.md for the full text.

using System;

namespace GameEditor.Framework.Renderer.Server.Resources.MeshOpt;

internal static class VertexFetch
{
    /// <summary>
    /// Generates a vertex remap table for vertex fetch efficiency (meshopt_optimizeVertexFetchRemap): vertices are numbered in the order
    /// the index buffer first references them; unreferenced vertices map to ~0u. Returns the number of unique (referenced) vertices.
    /// </summary>
    /// <param name="destination">Must hold vertexCount elements.</param>
    public static int OptimizeVertexFetchRemap(Span<uint> destination, ReadOnlySpan<uint> indices, int vertexCount)
    {
        if (destination.Length < vertexCount)
            throw new ArgumentException("Destination must hold vertexCount elements.", nameof(destination));

        Span<uint> remap = destination.Slice(0, vertexCount);
        remap.Fill(~0u);

        uint next_vertex = 0;

        for (int i = 0; i < indices.Length; ++i)
        {
            uint index = indices[i];
            if (index >= (uint)vertexCount)
                throw new ArgumentOutOfRangeException(nameof(indices), "Index out of range of vertexCount.");

            if (remap[(int)index] == ~0u)
                remap[(int)index] = next_vertex++;
        }

        return (int)next_vertex;
    }

    /// <summary>
    /// Reorders vertices and changes indices to reduce the amount of GPU memory fetches during vertex processing (meshopt_optimizeVertexFetch);
    /// also compacts away unreferenced vertices. indices are rewritten in place; returns the number of unique vertices written to destination.
    /// </summary>
    /// <param name="destination">Must hold vertexCount * vertexSize elements; may be the same buffer as vertices (in-place).</param>
    /// <param name="vertexSize">Elements of T per vertex (use T = byte and the vertex size in bytes for the exact C API).</param>
    public static int OptimizeVertexFetch<T>(Span<T> destination, Span<uint> indices, ReadOnlySpan<T> vertices, int vertexCount, int vertexSize) where T : unmanaged
    {
        if (vertexSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(vertexSize));
        if (vertices.Length < (long)vertexCount * vertexSize || destination.Length < (long)vertexCount * vertexSize)
            throw new ArgumentException("Vertex buffers must hold vertexCount * vertexSize elements.");

        // support in-place optimization
        if (destination.Overlaps(vertices))
            vertices = vertices.Slice(0, vertexCount * vertexSize).ToArray();

        uint[] vertex_remap = GC.AllocateUninitializedArray<uint>(vertexCount);
        vertex_remap.AsSpan().Fill(~0u);

        uint next_vertex = 0;

        for (int i = 0; i < indices.Length; ++i)
        {
            uint index = indices[i];
            if (index >= (uint)vertexCount)
                throw new ArgumentOutOfRangeException(nameof(indices), "Index out of range of vertexCount.");

            ref uint remap = ref vertex_remap[index];

            if (remap == ~0u) // vertex was not added to destination VB
            {
                // add vertex
                vertices.Slice((int)index * vertexSize, vertexSize).CopyTo(destination.Slice((int)next_vertex * vertexSize, vertexSize));

                remap = next_vertex++;
            }

            // modify indices in place
            indices[i] = remap;
        }

        return (int)next_vertex;
    }
}
