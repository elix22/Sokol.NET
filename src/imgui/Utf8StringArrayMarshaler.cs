using System.Runtime.InteropServices;
using System.Text;

namespace Imgui
{
    public class Utf8StringArrayMarshaler : ICustomMarshaler
    {
        private IntPtr nativeBlock;

        public void CleanUpManagedData(object ManagedObj) { }

        public void CleanUpNativeData(IntPtr pNativeData)
        {
            if (nativeBlock != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(nativeBlock);
                nativeBlock = IntPtr.Zero;
            }
        }

        public int GetNativeDataSize() => IntPtr.Size;

        public IntPtr MarshalManagedToNative(object ManagedObj)
        {
            var strings = ManagedObj as string[];
            if (strings == null || strings.Length == 0)
                return IntPtr.Zero;

            int n = strings.Length;
            int pointerArea = n * IntPtr.Size;

            // Calculate total size needed for all strings
            long totalBytes = 0;
            foreach (var s in strings)
                totalBytes += Encoding.UTF8.GetByteCount(s) + 1; // +1 null

            // Allocate a single block of memory
            long blockSize = pointerArea + totalBytes;
            nativeBlock = Marshal.AllocHGlobal((IntPtr)blockSize);

            IntPtr ptrsBase = nativeBlock;
            IntPtr dataBase = ptrsBase + pointerArea;
            long offset = 0;

            // Write pointers to the start of each string
            for (int i = 0; i < n; i++)
            {
                string s = strings[i];
                int bc = Encoding.UTF8.GetByteCount(s) + 1;

                unsafe
                {
                    byte* basePtr = (byte*)(dataBase + (nint)offset);
                    Span<byte> destSpan = new Span<byte>(basePtr, bc);
                    int written = Encoding.UTF8.GetBytes(s.AsSpan(), destSpan);
                    basePtr[written] = 0;
                }

                Marshal.WriteIntPtr(ptrsBase, i * IntPtr.Size,
                                   dataBase + (nint)offset);

                offset += bc;
            }

            return nativeBlock;
        }

        public object MarshalNativeToManaged(IntPtr pNativeData) =>
            throw new NotSupportedException();

        public static ICustomMarshaler GetInstance(string cookie) =>
            new Utf8StringArrayMarshaler();
    }
}
