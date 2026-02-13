using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace KrayonCompiler
{
    // ─────────────────────────────────────────────────────────────
    //  Struct de entrada de asset
    // ─────────────────────────────────────────────────────────────

    struct AssetEntry
    {
        public ulong Id;
        public long Offset;
        public int Size;
    }

    // ─────────────────────────────────────────────────────────────
    //  Utilidades de hash
    // ─────────────────────────────────────────────────────────────

    static class HashUtil
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong Hash(string text)
        {
            text = Normalize(text);
            ulong hash = 14695981039346656037UL;
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            foreach (byte b in bytes)
            {
                hash ^= b;
                hash *= 1099511628211UL;
            }
            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Normalize(string assetName)
        {
            return assetName.Replace('\\', '/').ToLowerInvariant().TrimStart('/');
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  Crypto XOR
    // ─────────────────────────────────────────────────────────────

    static class Crypto
    {
        private const byte KEY = 0xAC;

        public static void Apply(byte[] data, int offset, int count)
        {
            int end = offset + count;
            for (int i = offset; i < end; i++)
                data[i] ^= KEY;
        }

        public static void Apply(byte[] data)
        {
            Apply(data, 0, data.Length);
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  Compilador de PAK
    // ─────────────────────────────────────────────────────────────

    public class KRCompiler
    {
        public static void Build(string pakPath, Dictionary<string, string> assets)
        {
            Dictionary<ulong, string> hashToName = new Dictionary<ulong, string>();

            foreach (KeyValuePair<string, string> pair in assets)
            {
                string normalized = HashUtil.Normalize(pair.Key);
                ulong id = HashUtil.Hash(pair.Key);

                string existing;
                if (hashToName.TryGetValue(id, out existing))
                {
                    if (existing != normalized)
                        throw new Exception(
                            string.Format("Hash collision entre '{0}' y '{1}'", existing, normalized));
                    else
                        Console.WriteLine(
                            string.Format("[KRCompiler] Warning: asset duplicado '{0}', omitido.", normalized));
                    continue;
                }
                hashToName[id] = normalized;
            }

            FileStream fs = new FileStream(pakPath, FileMode.Create,
                                 FileAccess.Write, FileShare.None, 65536);
            BinaryWriter bw = new BinaryWriter(fs);

            try
            {
                bw.Write(Encoding.ASCII.GetBytes("MPAK"));
                bw.Write(1);
                bw.Write(assets.Count);
                bw.Write((long)0);

                List<AssetEntry> toc = new List<AssetEntry>(assets.Count);

                foreach (KeyValuePair<string, string> pair in assets)
                {
                    if (!File.Exists(pair.Value))
                    {
                        Console.WriteLine(
                            string.Format("[KRCompiler] Warning: archivo no encontrado '{0}', omitido.", pair.Value));
                        continue;
                    }

                    byte[] data = File.ReadAllBytes(pair.Value);
                    Crypto.Apply(data);

                    long offset = fs.Position;
                    bw.Write(data);

                    AssetEntry entry = new AssetEntry();
                    entry.Id = HashUtil.Hash(pair.Key);
                    entry.Offset = offset;
                    entry.Size = data.Length;
                    toc.Add(entry);
                }

                long tocOffset = fs.Position;

                foreach (AssetEntry entry in toc)
                {
                    bw.Write(entry.Id);
                    bw.Write(entry.Offset);
                    bw.Write(entry.Size);
                }

                fs.Seek(4 + 4, SeekOrigin.Begin);
                bw.Write(toc.Count);
                bw.Write(tocOffset);
            }
            finally
            {
                bw.Dispose();
                fs.Dispose();
            }
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  Caché LRU thread-safe
    // ─────────────────────────────────────────────────────────────

    sealed class LruCache
    {
        private sealed class Node
        {
            public ulong Key;
            public byte[] Data;
            public Node Prev;
            public Node Next;
        }

        private readonly long _maxBytes;
        private long _usedBytes;
        private readonly ReaderWriterLockSlim _rwLock;
        private readonly Dictionary<ulong, Node> _map;
        private Node _head;
        private Node _tail;

        public LruCache(long maxBytes)
        {
            _maxBytes = maxBytes;
            _usedBytes = 0;
            _rwLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
            _map = new Dictionary<ulong, Node>();
            _head = null;
            _tail = null;
        }

        public bool TryGet(ulong key, out byte[] copy)
        {
            _rwLock.EnterReadLock();
            try
            {
                Node node;
                if (!_map.TryGetValue(key, out node))
                {
                    copy = null;
                    return false;
                }
                byte[] src = node.Data;
                byte[] dst = new byte[src.Length];
                Buffer.BlockCopy(src, 0, dst, 0, src.Length);
                copy = dst;
                return true;
            }
            finally
            {
                _rwLock.ExitReadLock();
            }
        }

        public void Put(ulong key, byte[] data)
        {
            _rwLock.EnterWriteLock();
            try
            {
                Node existing;
                if (_map.TryGetValue(key, out existing))
                {
                    MoveToHead(existing);
                    return;
                }

                Node node = new Node();
                node.Key = key;
                node.Data = data;

                AddToHead(node);
                _map[key] = node;
                _usedBytes += data.Length;

                while (_usedBytes > _maxBytes && _tail != null)
                    Evict(_tail);
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }
        }

        public void Invalidate(ulong key)
        {
            _rwLock.EnterWriteLock();
            try
            {
                Node node;
                if (_map.TryGetValue(key, out node))
                    Evict(node);
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }
        }

        public void Clear()
        {
            _rwLock.EnterWriteLock();
            try
            {
                _map.Clear();
                _head = null;
                _tail = null;
                _usedBytes = 0;
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }
        }

        private void AddToHead(Node n)
        {
            n.Next = _head;
            n.Prev = null;
            if (_head != null)
                _head.Prev = n;
            _head = n;
            if (_tail == null)
                _tail = n;
        }

        private void MoveToHead(Node n)
        {
            if (n == _head) return;
            Remove(n);
            AddToHead(n);
        }

        private void Remove(Node n)
        {
            if (n.Prev != null)
                n.Prev.Next = n.Next;
            else
                _head = n.Next;

            if (n.Next != null)
                n.Next.Prev = n.Prev;
            else
                _tail = n.Prev;
        }

        private void Evict(Node n)
        {
            Remove(n);
            _map.Remove(n.Key);
            _usedBytes -= n.Data.Length;
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  PakFile  (version optimizada)
    // ─────────────────────────────────────────────────────────────

    public sealed class PakFile : IDisposable
    {
        // Asignar ANTES de la primera llamada a Load(). Por defecto 128 MB.
        public long CacheMaxBytes
        {
            get { return _cacheMaxBytes; }
            set { _cacheMaxBytes = value; }
        }

        private long _cacheMaxBytes;
        private MemoryMappedFile _mmf;
        private readonly Dictionary<ulong, AssetEntry> _assets;
        private LruCache _cache;
        private int _disposed;

        public PakFile(string pakPath)
        {
            if (!File.Exists(pakPath))
                throw new FileNotFoundException("PAK no encontrado", pakPath);

            _cacheMaxBytes = 128L * 1024 * 1024;
            _assets = new Dictionary<ulong, AssetEntry>();
            _cache = null;
            _disposed = 0;

            FileStream fileStream = new FileStream(
                pakPath, FileMode.Open,
                FileAccess.Read, FileShare.Read,
                4096, FileOptions.RandomAccess);

            _mmf = MemoryMappedFile.CreateFromFile(
                fileStream, null, 0,
                MemoryMappedFileAccess.Read,
                HandleInheritability.None,
                false);

            MemoryMappedViewAccessor acc = _mmf.CreateViewAccessor(
                0, 0, MemoryMappedFileAccess.Read);

            try
            {
                byte[] magic = new byte[4];
                acc.ReadArray<byte>(0, magic, 0, 4);

                if (Encoding.ASCII.GetString(magic) != "MPAK")
                    throw new Exception("Archivo PAK invalido (magic incorrecto)");

                int version = acc.ReadInt32(4);
                int count = acc.ReadInt32(8);
                long tocOffset = acc.ReadInt64(12);

                long pos = tocOffset;
                for (int i = 0; i < count; i++)
                {
                    AssetEntry entry = new AssetEntry();
                    entry.Id = acc.ReadUInt64(pos); pos += 8;
                    entry.Offset = acc.ReadInt64(pos); pos += 8;
                    entry.Size = acc.ReadInt32(pos); pos += 4;

                    if (_assets.ContainsKey(entry.Id))
                        Console.WriteLine(
                            string.Format("[PakFile] Warning: hash duplicado {0:X16} en TOC.", entry.Id));

                    _assets[entry.Id] = entry;
                }
            }
            finally
            {
                acc.Dispose();
            }

            Console.WriteLine(
                string.Format("[PakFile] Cargado: {0} assets desde '{1}'", _assets.Count, pakPath));
        }

        // ── Carga sincrona ────────────────────────────────────────

        public byte[] Load(string assetName)
        {
            ThrowIfDisposed();
            EnsureCache();

            ulong id = HashUtil.Hash(assetName);

            byte[] cached;
            if (_cache.TryGet(id, out cached))
                return cached;

            AssetEntry entry;
            if (!_assets.TryGetValue(id, out entry))
            {
                Console.WriteLine(string.Format(
                    "[PakFile] Asset no encontrado: '{0}' (norm: '{1}', hash: {2:X16})",
                    assetName, HashUtil.Normalize(assetName), id));
                return null;
            }

            byte[] data = ReadEntry(ref entry);

            byte[] forCache = new byte[data.Length];
            Buffer.BlockCopy(data, 0, forCache, 0, data.Length);
            _cache.Put(id, forCache);

            return data;
        }

        // ── Carga asincrona ───────────────────────────────────────

        public Task<byte[]> LoadAsync(string assetName)
        {
            return LoadAsync(assetName, CancellationToken.None);
        }

        public Task<byte[]> LoadAsync(string assetName, CancellationToken ct)
        {
            ThrowIfDisposed();
            EnsureCache();

            ulong id = HashUtil.Hash(assetName);
            byte[] cached;

            if (_cache.TryGet(id, out cached))
                return Task.FromResult(cached);

            return Task.Run(delegate { return Load(assetName); }, ct);
        }

        // ── Prefetch en background ────────────────────────────────

        public Task PrefetchAsync(IEnumerable<string> assetNames)
        {
            return PrefetchAsync(assetNames, CancellationToken.None);
        }

        public Task PrefetchAsync(IEnumerable<string> assetNames, CancellationToken ct)
        {
            return Task.Run(delegate
            {
                foreach (string name in assetNames)
                {
                    ct.ThrowIfCancellationRequested();
                    Load(name);
                }
            }, ct);
        }

        // ── Consulta de existencia ────────────────────────────────

        public bool Contains(string assetName)
        {
            ulong id = HashUtil.Hash(assetName);
            return _assets.ContainsKey(id);
        }

        // ── Control de caché ──────────────────────────────────────

        public void InvalidateCache(string assetName)
        {
            if (_cache == null) return;
            _cache.Invalidate(HashUtil.Hash(assetName));
        }

        public void ClearCache()
        {
            if (_cache != null)
                _cache.Clear();
        }

        // ── Helpers privados ──────────────────────────────────────

        private void EnsureCache()
        {
            if (_cache == null)
                _cache = new LruCache(_cacheMaxBytes);
        }

        private byte[] ReadEntry(ref AssetEntry entry)
        {
            byte[] rented = ArrayPool<byte>.Shared.Rent(entry.Size);
            try
            {
                MemoryMappedViewStream stream = _mmf.CreateViewStream(
                    entry.Offset, entry.Size, MemoryMappedFileAccess.Read);
                try
                {
                    int total = 0;
                    while (total < entry.Size)
                    {
                        int read = stream.Read(rented, total, entry.Size - total);
                        if (read == 0)
                            throw new EndOfStreamException("PAK truncado inesperadamente.");
                        total += read;
                    }
                }
                finally
                {
                    stream.Dispose();
                }

                Crypto.Apply(rented, 0, entry.Size);

                byte[] result = new byte[entry.Size];
                Buffer.BlockCopy(rented, 0, result, 0, entry.Size);
                return result;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ThrowIfDisposed()
        {
            if (_disposed != 0)
                throw new ObjectDisposedException("PakFile");
        }

        // ── Dispose ───────────────────────────────────────────────

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            if (_mmf != null)
            {
                _mmf.Dispose();
                _mmf = null;
            }
        }
    }
}