// Shared helpers for the C# port of meshoptimizer v1.3 (commit 8547fc321b782a2235dfc14c65a346a640544cd8, 2026-11-21).
// https://github.com/zeux/meshoptimizer
//
// Copyright (c) 2016-2026 Arseny Kapoulkine
// Licensed under the MIT License; see src/Framework/Renderer/Server/Resources/MeshOpt/LICENSE.md for the full text.
//
// meshoptimizer copies these helpers into each source file (hashBuckets2/hashLookup2, hashBuckets/hashLookup, ...);
// the copies are identical, so the port shares one: meshopt_Allocator, open addressing with quadratic probing.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace GameEditor.Framework.Renderer.Server.Resources.MeshOpt;

// stack-ordered native scratch allocator, mirrors meshopt_Allocator; everything is freed in Dispose
internal sealed unsafe class MeshoptAllocator : IDisposable
{
    readonly IntPtr[] blocks = new IntPtr[32];
    int count;

    public T* Allocate<T>(nuint size) where T : unmanaged
    {
        T* result = (T*)NativeMemory.Alloc(size == 0 ? 1 : size, (nuint)sizeof(T));
        blocks[count++] = (IntPtr)result;
        return result;
    }

    public void Deallocate(void* ptr)
    {
        NativeMemory.Free(ptr);
        count--;
    }

    public void Dispose()
    {
        for (int i = count; i > 0; --i)
            NativeMemory.Free((void*)blocks[i - 1]);
        count = 0;
    }
}

internal interface IMeshoptHasher
{
    uint Hash(uint key);
    bool Equal(uint lhs, uint rhs);
}

internal static unsafe class MeshoptHash
{
    public const uint Empty = ~0u;

    public static nuint Buckets(nuint count)
    {
        nuint buckets = 1;
        while (buckets < count + count / 4)
            buckets *= 2;

        return buckets;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint* Lookup<THash>(uint* table, nuint buckets, in THash hash, uint key, uint empty) where THash : struct, IMeshoptHasher
    {
        nuint hashmod = buckets - 1;
        nuint bucket = hash.Hash(key) & hashmod;

        for (nuint probe = 0; probe <= hashmod; ++probe)
        {
            uint* item = table + bucket;

            if (*item == empty)
                return item;

            if (hash.Equal(*item, key))
                return item;

            // hash collision, quadratic probing
            bucket = (bucket + probe + 1) & hashmod;
        }

        throw new InvalidOperationException("Hash table is full"); // unreachable
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckIndices(ReadOnlySpan<uint> indices, int vertexCount, string paramName)
    {
        for (int i = 0; i < indices.Length; ++i)
            if (indices[i] >= (uint)vertexCount)
                throw new ArgumentOutOfRangeException(paramName, "Index out of range of vertexCount.");
    }

    public static void CheckStridedFloats(int length, int vertexCount, int stride, int components, string paramName)
    {
        if (stride < components || stride > 64)
            throw new ArgumentOutOfRangeException(paramName, $"Stride (in floats) must be in [{components}, 64].");
        if (vertexCount < 0 || (vertexCount > 0 && (long)(vertexCount - 1) * stride + components > length))
            throw new ArgumentException("Buffer is too small for vertexCount/stride.", paramName);
    }

    public static void CheckStridedBytes(int length, int vertexCount, int stride, int size, string paramName)
    {
        if (size <= 0 || size > 256 || stride < size)
            throw new ArgumentOutOfRangeException(paramName, "Vertex size must be in [1, 256] and not exceed the stride.");
        if (vertexCount < 0 || (vertexCount > 0 && (long)(vertexCount - 1) * stride + size > length))
            throw new ArgumentException("Buffer is too small for vertexCount/stride.", paramName);
    }
}
