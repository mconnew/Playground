using System.Runtime.InteropServices;
using System.Text;

namespace NetNamedPipePathSearcher
{
    internal class UnsafeNativeMethods
    {
        public const string KERNEL32 = "kernel32.dll";
        public const int ERROR_SUCCESS = 0;
        public const int ERROR_FILE_NOT_FOUND = 2;

        public const int FORMAT_MESSAGE_IGNORE_INSERTS = 0x00000200;
        public const int FORMAT_MESSAGE_FROM_SYSTEM = 0x00001000;
        public const int FORMAT_MESSAGE_ARGUMENT_ARRAY = 0x00002000;
        //public const int FILE_MAP_WRITE = 2;
        public const int FILE_MAP_READ = 4;

        [DllImport(KERNEL32)]
        internal static extern int CloseHandle(IntPtr handle);

        [DllImport(KERNEL32, SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern SafeFileMappingHandle OpenFileMapping(int access, bool inheritHandle, string name);

        [DllImport(KERNEL32, CharSet = CharSet.Unicode)]
        internal static extern int FormatMessage(int dwFlags, IntPtr lpSource, int dwMessageId, int dwLanguageId,
                                                 StringBuilder lpBuffer, int nSize, IntPtr arguments);

        [DllImport(KERNEL32, ExactSpelling = true)]
        internal static extern int UnmapViewOfFile(IntPtr lpBaseAddress);

        [DllImport(KERNEL32, SetLastError = true)]
        internal static extern SafeViewOfFileHandle MapViewOfFile(SafeFileMappingHandle handle, int dwDesiredAccess, int dwFileOffsetHigh, int dwFileOffsetLow, IntPtr dwNumberOfBytesToMap);
    }
}