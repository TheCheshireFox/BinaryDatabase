using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Utility
{
    public unsafe class MemoryAccessor : IDisposable
    {
        private MemoryMappedFileWrapper mmf;
        private MemoryMappedViewAccessor mmfAccessor;

        private readonly string path;
        private readonly long pageSize;
        private readonly long offset;
        private long pageOffset;
        private long currentPageSize;
        private long fileSize;
        private byte* ptr = null;
        private readonly bool ownMmf = true;

        public MemoryAccessor(string path, long offset, long pageSize)
            : this(new MemoryMappedFileWrapper(path), path, offset, pageSize)
        {
            ownMmf = true;
        }

        public MemoryAccessor(MemoryMappedFileWrapper mmf, string path, long offset, long pageSize)
        {
            this.fileSize = new FileInfo(path).Length;
            if (fileSize < pageSize)
            {
                currentPageSize = fileSize - offset;
            }
            else
            {
                currentPageSize = pageSize;
            }

            this.path = path;
            this.mmf = mmf;
            this.mmfAccessor = mmf.CreateViewAccessor(offset, currentPageSize);
            this.mmfAccessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
            this.offset = offset;
            this.pageOffset = this.offset;
            this.pageSize = pageSize;
            this.ownMmf = false;
        }

        public ref readonly MemoryMappedFileWrapper GetMemoryMappedFileWrapper() => ref mmf;

        public ref T2 Get<T2>(long absoluteOffset)
        {
            return ref Unsafe.AsRef<T2>(GetPtr(absoluteOffset));
        }

        public byte* GetPtr(long absoluteOffset, bool extend = false)
        {
            var relativeOffset = absoluteOffset % pageSize;
            absoluteOffset += offset;

            if (absoluteOffset < pageOffset || absoluteOffset >= pageOffset + currentPageSize)
            {
                pageOffset = (absoluteOffset / pageSize) * pageSize;

                mmfAccessor.SafeMemoryMappedViewHandle.ReleasePointer();
                mmfAccessor.Dispose();

                currentPageSize = pageSize;
                if (fileSize - pageOffset < pageSize)
                {
                    if (extend)
                    {
                        if (!ownMmf) throw new Exception("Unable to extend not owning memory mapped file.");
                        mmf.ExtendDangerous(pageSize);
                        fileSize += pageSize;
                    }
                    else
                    {
                        currentPageSize = fileSize - pageOffset;
                        if (currentPageSize < 0)
                        {
                            throw new ArgumentException($"Absolute offset {absoluteOffset} without extend is out of file size {fileSize}.");
                        }
                    }
                }

                mmfAccessor = mmf.CreateViewAccessor(pageOffset, currentPageSize);
                mmfAccessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
            }

            return ptr + relativeOffset + mmfAccessor.PointerOffset;
        }

        public void Dispose()
        {
            mmfAccessor.SafeMemoryMappedViewHandle.ReleasePointer();
            mmfAccessor.Dispose();
            if (ownMmf)
            {
                mmf.Dispose();
            }
        }
    }
}
