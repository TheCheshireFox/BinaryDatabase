using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Utility
{
    public unsafe class FixedArrayAccessor<T> : IEnumerable<T>
    {
        static readonly int sz = Marshal.SizeOf<T>();
        WeakReference parent;
        readonly int offset;
        readonly int count;

        public FixedArrayAccessor(object parent, string field, int count)
        {
            this.parent = new WeakReference(parent);
            offset = Marshal.OffsetOf<T>(field).ToInt32();
            this.count = count;
        }

        public T this[int index]
        {
            get
            {
                if (parent.Target == null) throw new ObjectDisposedException("Parent is disposed.");
                using (var p = new Pinner(parent.Target))
                {
                    return Unsafe.Read<T>((p.Addr + offset + index * sz).ToPointer());
                }
            }
            set
            {
                if (parent.Target == null) throw new ObjectDisposedException("Parent is disposed.");
                using (var p = new Pinner(parent.Target))
                {
                    Unsafe.Write((p.Addr + offset + index * sz).ToPointer(), value);
                }
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < count; i++) yield return this[i];
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
