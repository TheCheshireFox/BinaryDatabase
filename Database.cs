using BinaryDatabase.Attributes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Utility;

namespace BinaryDatabase
{
    public delegate void ForEachAction<T>(ref T val);
    public delegate TKey KeyGetter<TKey>(long index);
    public delegate void KeySetter<TKey, T>(TKey key, ref T val) where T : struct;
    public delegate bool KeyValidator<TKey>(TKey rey);

    public unsafe class Database<TKey, T> : IDisposable
        where T : struct
        where TKey : IComparable
    {
        private LoggerInterface logger;
        private ProgressLogger progress;

        private Dictionary<TKey, long> index = new Dictionary<TKey, long>();
        private MemoryAccessor accessor = null;

        private readonly bool idIsIndex = false;
        private readonly bool sortable = false;
        private readonly long pageSize = 0;
        private readonly long offset = 0;
        private readonly string path = null;

        private readonly KeyGetter<TKey> getKey;
        private readonly KeySetter<TKey, T> setKey;
        private readonly KeyValidator<TKey> validateKey;
        private readonly int keyOffset = -1;

        private readonly int valueSize = 0;
        private long fileSize = 0;
        private long appendOffset = 0;

        public IEnumerable<TKey> Keys => index.Keys;

        private void StartSort(long ps)
        {
            logger.LogInfo("Copying temp file...");

            File.Copy(path, path + ".tmp");

            logger.LogInfo("Resize temp file...");
            var newFilesSize = (long)index.Count * valueSize + offset;
            logger.LogInfo($"New file size: {newFilesSize}");

            using (var f = File.Open(path + ".tmp", FileMode.OpenOrCreate, FileAccess.ReadWrite))
                f.SetLength(newFilesSize);

            logger.LogInfo("Sorting...");

            var tmpAccessor = new MemoryAccessor(path + ".tmp", 0, ps);
            var sortedIndex = new SortedDictionary<TKey, long>(index);
            var newIndex = new Dictionary<TKey, long>();

            var off = offset;
            foreach (var kv in sortedIndex)
            {
                Unsafe.Write(tmpAccessor.GetPtr(off), accessor.Get<T>(kv.Value));
                newIndex.Add(kv.Key, off);
                off += valueSize;
            }

            accessor.Dispose();
            tmpAccessor.Dispose();

            logger.LogInfo("Replace files...");
            File.Delete(path);
            File.Move(path + ".tmp", path);

            logger.LogInfo("Reload database...");
            accessor = new MemoryAccessor(path, 0, pageSize);
            fileSize = new FileInfo(path).Length;
            index = newIndex;
        }

        private void SetKey(TKey key, ref T val)
        {
            using (var p = new Pinner(val))
            {
                Unsafe.Write((p.Addr + keyOffset).ToPointer(), key);
            }
        }

        private void SetKeyString(string key, ref T val)
        {
            using (var p = new Pinner(val))
            {
                fixed (char* s = key)
                {
                    Unsafe.CopyBlock((p.Addr + keyOffset).ToPointer(), s, (uint)key.Length);
                }
            }
        }

        private void SetKeyArray(Array key, ref T val)
        {
            using (var p = new Pinner(val))
            {
                Unsafe.CopyBlock((p.Addr + keyOffset).ToPointer(), Unsafe.AsPointer(ref key), (uint)(key.Length * Marshal.SizeOf(key.GetType().GetElementType())));
            }
        }

        private long GetAbsoluteOffset(long index)
        {
            return index * valueSize + offset;
        }

        private ref T GetValue(long index)
        {
            return ref accessor.Get<T>(GetAbsoluteOffset(index));
        }

        private TKey GetKey(MemoryAccessor ma, long index)
        {
            return ma.Get<TKey>(GetAbsoluteOffset(index) + keyOffset);
        }

        private string GetKeyString(MemoryAccessor ma, long index, int count)
        {
            var sp = new Span<char>(ma.GetPtr(GetAbsoluteOffset(index) + keyOffset), count);
            return sp.Slice(0, sp.IndexOf('\0') < 0 ? count : sp.IndexOf('\0')).ToString();
        }

        private Array GetKeyArray(MemoryAccessor ma, long index, int count, Type elType)
        {
            var arr = Array.CreateInstance(elType, count);

            Unsafe.CopyBlock(Unsafe.AsPointer(ref arr), ma.GetPtr(GetAbsoluteOffset(index) + keyOffset), (uint)(count * Marshal.SizeOf(elType)));

            return arr;
        }

        public Database(string path, KeyValidator<TKey> keyValidator, long offset = 0, long pageSize = 0, bool sortable = true, LoggerInterface logger = null)
        {
            if (typeof(T).GetFields(BindingFlags.NonPublic).Any(f => !f.FieldType.IsValueType))
            {
                throw new Exception("Structure fields can be only value type.");
            }

            valueSize = Marshal.SizeOf<T>();
            fileSize = new FileInfo(path).Length;

            if ((fileSize - offset) % valueSize != 0)
            {
                throw new Exception("Database is corrupted.");
            }

            validateKey = keyValidator;

            this.sortable = sortable;
            this.pageSize = pageSize - pageSize % valueSize;
            this.offset = offset;
            this.path = path;
            this.logger = logger ?? new LoggerInterface();
            this.progress = new ProgressLogger(this.logger);

            accessor = new MemoryAccessor(path, 0, this.pageSize);

            idIsIndex = typeof(T).GetCustomAttribute<IdIsIndexAttribute>() != null;
            if (!idIsIndex)
            {
                var idField = typeof(T).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).First(f => f.GetCustomAttribute<IdAttribute>() != null);
                if (idField.GetCustomAttribute<FixedBufferAttribute>() != null)
                {
                    if (typeof(TKey) == typeof(string))
                    {
                        getKey = i => (TKey)(object)GetKeyString(accessor, i, idField.GetCustomAttribute<FixedBufferAttribute>().Length);
                        setKey = (TKey key, ref T val) => SetKeyString((string)(object)key, ref val);
                    }
                    else if (typeof(TKey) == typeof(Array))
                    {
                        getKey = i => (TKey)(object)GetKeyArray(accessor, i, idField.GetCustomAttribute<FixedBufferAttribute>().Length, idField.GetCustomAttribute<FixedBufferAttribute>().ElementType);
                        setKey = (TKey key, ref T val) => SetKeyArray((Array)(object)key, ref val);
                    }
                    else
                    {
                        throw new Exception("Fixed array key field should be String or Array.");
                    }
                }
                else
                {
                    getKey = i => GetKey(accessor, i);
                    setKey = (TKey key, ref T val) => SetKey(key, ref val);
                }

                keyOffset = Marshal.OffsetOf<T>(idField.Name).ToInt32();
            }
        }

        public void Load()
        {
            var count = (fileSize - offset) / valueSize;
            index.Clear();
            for (long i = 0; i < count; i++)
            {
                var currentOffset = GetAbsoluteOffset(i);
                var key = getKey(i);
                if (validateKey(key))
                {
                    index.Add(key, currentOffset);
                }
            }

            appendOffset = index.Values.DefaultIfEmpty(-valueSize).Max() + valueSize;
        }

        public void Write(T[] vals)
        {
            index.Clear();
            accessor.Dispose();
            using (var f = File.OpenWrite(path))
            {
                f.SetLength(offset + vals.Length * valueSize);
            }
            accessor = new MemoryAccessor(path, 0, pageSize);

            using (var p = new Pinner(vals))
            {
                for (int i = 0; i < vals.Length; i++)
                {
                    var ptr = (p.Addr + i * valueSize);
                    /// FIXME: possible fuckup if TKey is not POD type (e.g. string or array)
                    Add(Unsafe.AsRef<TKey>((ptr + keyOffset).ToPointer()), ref Unsafe.AsRef<T>(ptr.ToPointer()));
                }
            }
        }

        public void Add(TKey key, T value)
        {
            Add(key, ref value);
        }

        public void Add(TKey key, ref T value)
        {
            index.Add(key, appendOffset);
            var ptr = accessor.GetPtr(appendOffset, true);
            Unsafe.Write(ptr, value);

            appendOffset += valueSize;
        }

        public void Update(TKey key, T value)
        {
            var ptr = accessor.GetPtr(index[key], true);
            Unsafe.Write(ptr, value);
        }

        public void Remove(TKey key)
        {
            Unsafe.InitBlock(ref Unsafe.AsRef<byte>(accessor.GetPtr(index[key])), 0, (uint)valueSize);
            index.Remove(key);
        }

        public void Optimize()
        {
            if (sortable)
            {
                var ps = (pageSize / 2) - (pageSize / 2) % valueSize;
                ps = ps < valueSize ? valueSize : ps;

                progress.StartProgress("Sorting", 0);
                StartSort(ps);
                progress.StopProgress("Sorting complete.");
            }

            GC.Collect(3, GCCollectionMode.Forced, true);
        }

        public IDictionary<TKey, TKey> Merge(Database<TKey, T> from, MergeConflictResolution resolv = MergeConflictResolution.ERROR)
        {
            Func<TKey, TKey> nextKey = null;
            TKey lastKey = default;
            if (resolv == MergeConflictResolution.NEXT)
            {
                var p = Expression.Parameter(typeof(TKey));
                nextKey = Expression.Lambda<Func<TKey, TKey>>(Expression.PreIncrementAssign(p), p).Compile();
                lastKey = index.Keys.DefaultIfEmpty().Max();
            }

            Dictionary<TKey, TKey> replaces = new Dictionary<TKey, TKey>();
            progress.StartProgress("Merging", from.index.LongCount());

            foreach (var kv in from.index)
            {
                if (index.ContainsKey(kv.Key))
                {
                    switch (resolv)
                    {
                        case MergeConflictResolution.ERROR:
                            throw new Exception($"Conflict detected {kv.Key}");
                        case MergeConflictResolution.NEXT:
                            lastKey = nextKey(lastKey);
                            var val = from.accessor.Get<T>(kv.Value);
                            setKey(lastKey, ref val);

                            Add(lastKey, val);
                            replaces.Add(kv.Key, lastKey);
                            break;
                        case MergeConflictResolution.REPLACE:
                            Update(kv.Key, from.accessor.Get<T>(kv.Value));
                            break;
                        case MergeConflictResolution.SKIP:
                            logger.LogInfo($"Conflict detected {kv.Key}. Skipping...");
                            break;
                    }
                }
                else
                {
                    Add(kv.Key, from.accessor.Get<T>(kv.Value));
                }

                progress.EmitIncrement();
            }

            progress.StopProgress("Merge complete.");

            return replaces;
        }

        public void Dispose()
        {
            try
            {
                accessor.Dispose();
            }
            catch (ObjectDisposedException) { }

            /// FIXME: Very bad hack
            var newSize = offset + valueSize * Keys.Count();
            using (var f = File.Open(path, FileMode.Open))
            {
                f.SetLength(newSize);
            }
        }

        public void ForEach(ForEachAction<T> act)
        {
            for (int i = 0; i < index.Count; i++)
            {
                act(ref GetValue(i));
            }
        }

        public IEnumerable<T> ToEnumerable()
        {
            for (int i = 0; i < index.Count; i++)
            {
                yield return GetValue(i);
            }
        }

        public int Count => index.Count;
    }
}
