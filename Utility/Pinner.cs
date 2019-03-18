using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Utility
{
    public unsafe class Pinner : IDisposable
    {
        private GCHandle handle;

        public Pinner(object o) => handle = GCHandle.Alloc(o, GCHandleType.Pinned);

        public IntPtr Addr => handle.AddrOfPinnedObject();
        public void* Ptr => handle.AddrOfPinnedObject().ToPointer();

        public void Dispose()
        {
            handle.Free();
        }
    }
}
