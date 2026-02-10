using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace KrayonCompiler
{
    public readonly struct AssetEntry
    {
        public ulong Id { get; init; }
        public long Offset { get; init; }
        public int Size { get; init; }
    }

    public sealed class AssetNotFoundException : Exception
    {
        public AssetNotFoundException(string assetName)
            : base($"Asset not found: '{assetName}'") { }
    }

    public sealed class InvalidPakException : Exception
    {
        public InvalidPakException(string message) : base(message) { }
    }

    public static class FnvHash
    {
        private const ulong OffsetBasis = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        public static ulong Compute(string value)
        {
            ArgumentNullException.ThrowIfNull(value);

            ulong hash = OffsetBasis;
            foreach (char c in value)
            {
                hash ^= (byte)c;
                hash *= Prime;
            }
            return hash;
        }
    }

    public static class XorCipher
    {
        private const byte Key = 0xAC;

        public static void Apply(Span<byte> data)
        {
            for (int i = 0; i < data.Length; i++)
                data[i] ^= Key;
        }
    }

    public sealed class PakFile : IDisposable
    {
        private const string Magic = "MPAK";

        private readonly FileStream _stream;
        private readonly BinaryReader _reader;
        private readonly Dictionary<ulong, AssetEntry> _assets;
        private bool _disposed;

        public int AssetCount => _assets.Count;

        public PakFile(string path)
        {
            ArgumentNullException.ThrowIfNull(path);

            _stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            _reader = new BinaryReader(_stream, Encoding.ASCII, leaveOpen: false);
            _assets = new Dictionary<ulong, AssetEntry>();

            ReadHeader();
        }

        private void ReadHeader()
        {
            string magic = Encoding.ASCII.GetString(_reader.ReadBytes(4));
            if (magic != Magic)
                throw new InvalidPakException($"Invalid magic: expected '{Magic}', got '{magic}'.");

            _ = _reader.ReadInt32();
            int count = _reader.ReadInt32();
            long tocOffset = _reader.ReadInt64();

            ReadTableOfContents(tocOffset, count);
        }

        private void ReadTableOfContents(long offset, int count)
        {
            _stream.Seek(offset, SeekOrigin.Begin);

            for (int i = 0; i < count; i++)
            {
                var entry = new AssetEntry
                {
                    Id = _reader.ReadUInt64(),
                    Offset = _reader.ReadInt64(),
                    Size = _reader.ReadInt32()
                };
                _assets[entry.Id] = entry;
            }
        }

        public byte[] Load(string name)
        {
            ArgumentNullException.ThrowIfNull(name);
            ObjectDisposedException.ThrowIf(_disposed, this);

            ulong id = FnvHash.Compute(name);
            if (!_assets.TryGetValue(id, out var entry))
                throw new AssetNotFoundException(name);

            _stream.Seek(entry.Offset, SeekOrigin.Begin);
            byte[] data = _reader.ReadBytes(entry.Size);
            XorCipher.Apply(data);
            return data;
        }

        public bool TryLoad(string name, out byte[]? data)
        {
            try { data = Load(name); return true; }
            catch (AssetNotFoundException) { data = null; return false; }
        }

        public bool Contains(string name)
        {
            ArgumentNullException.ThrowIfNull(name);
            return _assets.ContainsKey(FnvHash.Compute(name));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _reader.Dispose();
            _stream.Dispose();
            _disposed = true;
        }
    }
}