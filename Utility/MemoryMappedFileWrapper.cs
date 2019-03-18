using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Text;

namespace Utility
{
    public class MemoryMappedFileWrapper : IDisposable
    {
        private MemoryMappedFile mmf;
        private string path;
        private object sync = new object();

        private MemoryMappedFile Open(long size = 0)
        {
            return MemoryMappedFile.CreateFromFile(path, FileMode.OpenOrCreate, Guid.NewGuid().ToString(), size);
        }

        public MemoryMappedFileWrapper(string path, long size = 0)
        {
            this.path = path;
            mmf = Open(size);
        }

        public void ExtendDangerous(long sizeToExtend)
        {
            lock (sync)
            {
                mmf.Dispose();
                mmf = Open(new FileInfo(path).Length + sizeToExtend);
            }
        }

        public MemoryMappedViewAccessor CreateViewAccessor(long offset, long size)
        {
            lock (sync)
            {
                return mmf.CreateViewAccessor(offset, size);
            }
        }

        public void Dispose()
        {
            mmf.Dispose();
        }
    }
}
