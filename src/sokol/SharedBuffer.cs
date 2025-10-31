// Copyright Eli Aloni A.K.A. elix22. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the Git repository root directory for full license information.
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

// Intended to be used in case there is a need of sharing data between the Managed and Unmanaged code.
namespace Sokol
{
    public class SharedBuffer : IDisposable
    {
        public byte[] Buffer { get; private set; }
        private GCHandle bufferHandle;
        private bool disposed;

        private static List<SharedBuffer> _sharedBuffers = new List<SharedBuffer>();

        public static SharedBuffer Create(uint bufferSize)
        {
            try
            {
                var newBuff = new SharedBuffer(bufferSize);
                _sharedBuffers.Add(newBuff);
                return newBuff;
            }
            catch (Exception ex)
            {
                // Handle or log the exception as needed
                throw new InvalidOperationException("Failed to create SharedBuffer.", ex);
            }
        }

        public static void DisposeAll()
        {
            foreach (var buff in _sharedBuffers)
            {
                buff.Dispose(true);
                GC.SuppressFinalize(buff);
            }
            _sharedBuffers.Clear();

        }
        
        
        public static void Dispose(SharedBuffer buffer)
        {
            buffer.Dispose();
            _sharedBuffers.Remove(buffer);
        }

        // private Constructor , use SharedBuffer.Create() instead
        private SharedBuffer(uint bufferSize)
        {
            Buffer = new byte[bufferSize];
            PinBuffer();
        }

        public void PinBuffer()
        {
            if (!bufferHandle.IsAllocated)
                bufferHandle = GCHandle.Alloc(Buffer, GCHandleType.Pinned);
        }

        public IntPtr GetBufferPointer()
        {
            if (!bufferHandle.IsAllocated)
                throw new InvalidOperationException("Buffer not pinned. Call PinBuffer() first.");
            return bufferHandle.AddrOfPinnedObject();
        }

        public uint Size => (uint)Buffer.Length;

        public void UnpinBuffer()
        {
            if (bufferHandle.IsAllocated)
                bufferHandle.Free();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
            _sharedBuffers.Remove(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (bufferHandle.IsAllocated)
                    bufferHandle.Free();
                disposed = true;
            }

            if(!disposing)
            {
                _sharedBuffers.Remove(this);
            }
        }

        ~SharedBuffer()
        {
            Dispose(false);
        }
    }

}
