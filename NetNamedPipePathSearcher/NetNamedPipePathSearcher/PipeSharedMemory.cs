using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;
using System.Globalization;
using System.Runtime.InteropServices;
using System.ServiceModel;
using System.Text;

namespace NetNamedPipePathSearcher
{
    internal unsafe class PipeSharedMemory : IDisposable
    {
        internal const string PipePrefix = @"\\.\pipe\";
        internal const string PipeLocalPrefix = @"\\.\pipe\Local\";
        private SafeFileMappingHandle? _fileMapping;
        private string? _pipeName;
        private string? _pipeNameGuidPart;
        private Uri _pipeUri;
        private ILogger _logger;

        private PipeSharedMemory(ILogger logger, SafeFileMappingHandle fileMapping, Uri pipeUri)
        {
            _pipeName = null;
            _fileMapping = fileMapping;
            _pipeUri = pipeUri;
            _logger = logger;
        }

        public static PipeSharedMemory? Open(ILogger logger, string sharedMemoryName, Uri pipeUri)
        {
            logger.LogTrace("Attempting to open shared memory with name {SharedMemoryName}", sharedMemoryName);
            SafeFileMappingHandle fileMapping = UnsafeNativeMethods.OpenFileMapping(UnsafeNativeMethods.FILE_MAP_READ, false, sharedMemoryName);
            if (fileMapping.IsInvalid)
            {
                int error = Marshal.GetLastWin32Error();
                fileMapping.SetHandleAsInvalid();
                if (error == UnsafeNativeMethods.ERROR_FILE_NOT_FOUND)
                {
                    return null;
                }

                logger.LogTrace("Unable to open file mapping for shared memory name {SharedMemoryName} with error code {ErrorCode}", sharedMemoryName, error);
                throw CreatePipeNameCannotBeAccessedException(error, pipeUri);
            }

            logger.LogTrace("Successully opened file mapping for shared memory name {SharedMemoryName}", sharedMemoryName);
            return new PipeSharedMemory(logger, fileMapping, pipeUri);
        }

        public void Dispose()
        {
            if (_fileMapping != null)
            {
                _fileMapping.Close();
                _fileMapping = null;
            }
        }

        public string? PipeName
        {
            get
            {
                if (_pipeName == null)
                {
                    SafeViewOfFileHandle view = GetView(_logger);
                    try
                    {
                        SharedMemoryContents* contents = (SharedMemoryContents*)view.DangerousGetHandle();
                        if (contents->isInitialized)
                        {
                            Thread.MemoryBarrier();
                            _pipeNameGuidPart = contents->pipeGuid.ToString();
                            _pipeName = BuildPipeName(_pipeNameGuidPart);
                            _logger.LogTrace("Read from shared memory pipe GUID {PipeGuid} which translates to pipe name {PipeName}", _pipeNameGuidPart, _pipeName);
                        }
                        else
                        {
                            _logger.LogTrace("Shared memory uninitialized");
                        }
                    }
                    finally
                    {
                        view.Close();
                    }
                }
                return _pipeName;
            }
        }

        internal string? GetPipeName()
        {
            return PipeName;
        }

        private static Exception CreatePipeNameCannotBeAccessedException(int error, Uri pipeUri)
        {
            Exception innerException = new PipeException($"PipeNameCanNotBeAccessed, {PipeError.GetErrorString(error)}", error);
            return new AddressAccessDeniedException($"PipeNameCanNotBeAccessed2, {pipeUri.AbsoluteUri}", innerException);
        }

        private SafeViewOfFileHandle GetView(ILogger logger)
        {
            if (_fileMapping == null)
            {
                throw new ObjectDisposedException(nameof(PipeSharedMemory));
            }

            logger.LogTrace("Creating mapped view of file");
            SafeViewOfFileHandle handle = UnsafeNativeMethods.MapViewOfFile(_fileMapping, UnsafeNativeMethods.FILE_MAP_READ,
                0, 0, (IntPtr)sizeof(SharedMemoryContents));
            if (handle.IsInvalid)
            {
                int error = Marshal.GetLastWin32Error();
                logger.LogTrace("Unable to create mapped view of file, error: {Error}", error);
                handle.SetHandleAsInvalid();
                throw CreatePipeNameCannotBeAccessedException(error, _pipeUri);
            }

            return handle;
        }

        private static string BuildPipeName(string pipeGuid)
        {
            return PipePrefix + pipeGuid;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SharedMemoryContents
        {
            public bool isInitialized;
            public Guid pipeGuid;
        }
    }

    internal static class PipeError
    {
        public static string GetErrorString(int error)
        {
            StringBuilder stringBuilder = new StringBuilder(512);
            if (UnsafeNativeMethods.FormatMessage(UnsafeNativeMethods.FORMAT_MESSAGE_IGNORE_INSERTS |
                UnsafeNativeMethods.FORMAT_MESSAGE_FROM_SYSTEM | UnsafeNativeMethods.FORMAT_MESSAGE_ARGUMENT_ARRAY,
                IntPtr.Zero, error, CultureInfo.CurrentCulture.LCID, stringBuilder, stringBuilder.Capacity, IntPtr.Zero) != 0)
            {
                stringBuilder = stringBuilder.Replace("\n", "");
                stringBuilder = stringBuilder.Replace("\r", "");
                return $"PipeKnownWin32Error, {stringBuilder.ToString()}, {error.ToString(CultureInfo.InvariantCulture)}, {Convert.ToString(error, 16)}";
            }
            else
            {
                return $"PipeUnknownWin32Error, {error.ToString(CultureInfo.InvariantCulture)}, {Convert.ToString(error, 16)}";
            }
        }
    }

    internal sealed class SafeFileMappingHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        internal SafeFileMappingHandle() : base(true)
        {
        }

        override protected bool ReleaseHandle()
        {
            return UnsafeNativeMethods.CloseHandle(handle) != 0;
        }
    }

    internal sealed class SafeViewOfFileHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        internal SafeViewOfFileHandle() : base(true)
        {
        }

        override protected bool ReleaseHandle()
        {
            if (UnsafeNativeMethods.UnmapViewOfFile(handle) != 0)
            {
                handle = IntPtr.Zero;
                return true;
            }
            return false;
        }
    }
}