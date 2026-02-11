using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

namespace KrayonCompiler
{
    struct AssetEntry
    {
        public ulong Id;
        public long Offset;
        public int Size;
    }

    static class HashUtil
    {
        public static ulong Hash(string text)
        {
            ulong hash = 14695981039346656037UL;
            foreach (char c in text)
            {
                hash ^= (byte)c;
                hash *= 1099511628211UL;
            }
            return hash;
        }
    }

    static class Crypto
    {
        private const byte KEY = 0xAC;

        public static void Apply(byte[] data)
        {
            for (int i = 0; i < data.Length; i++)
                data[i] ^= KEY;
        }
    }

    public class KRCompiler
    {
        public static void Build(string pakPath, Dictionary<string, string> assets)
        {
            using var fs = new FileStream(pakPath, FileMode.Create);
            using var bw = new BinaryWriter(fs);

            bw.Write(Encoding.ASCII.GetBytes("MPAK"));
            bw.Write(1);
            bw.Write(assets.Count);
            bw.Write((long)0);

            List<AssetEntry> toc = new();

            foreach (var pair in assets)
            {
                string assetName = pair.Key;
                string filePath = pair.Value;

                byte[] data = File.ReadAllBytes(filePath);
                Crypto.Apply(data);

                long offset = fs.Position;
                bw.Write(data);

                toc.Add(new AssetEntry
                {
                    Id = HashUtil.Hash(assetName),
                    Offset = offset,
                    Size = data.Length
                });
            }

            long tocOffset = fs.Position;
            foreach (var entry in toc)
            {
                bw.Write(entry.Id);
                bw.Write(entry.Offset);
                bw.Write(entry.Size);
            }

            fs.Seek(4 + 4 + 4, SeekOrigin.Begin);
            bw.Write(tocOffset);
        }
    }

    public class PakFile : IDisposable
    {
        private FileStream _fs;
        private BinaryReader _br;
        private Dictionary<ulong, AssetEntry> _assets = new();

        public PakFile(string pakPath)
        {
            _fs = new FileStream(pakPath, FileMode.Open, FileAccess.Read);
            _br = new BinaryReader(_fs);

            string magic = Encoding.ASCII.GetString(_br.ReadBytes(4));
            if (magic != "MPAK")
                throw new Exception("Invalid PAK file");

            int version = _br.ReadInt32();
            int count = _br.ReadInt32();
            long tocOffset = _br.ReadInt64();

            _fs.Seek(tocOffset, SeekOrigin.Begin);
            for (int i = 0; i < count; i++)
            {
                AssetEntry entry = new AssetEntry
                {
                    Id = _br.ReadUInt64(),
                    Offset = _br.ReadInt64(),
                    Size = _br.ReadInt32()
                };
                _assets[entry.Id] = entry;
            }
        }

        public byte[] Load(string assetName)
        {
            ulong id = HashUtil.Hash(assetName);
            if (!_assets.TryGetValue(id, out var entry))
                throw new Exception($"Asset not found: {assetName}");

            _fs.Seek(entry.Offset, SeekOrigin.Begin);
            byte[] data = _br.ReadBytes(entry.Size);
            Crypto.Apply(data);

            return data;
        }

        public void Dispose()
        {
            _br?.Dispose();
            _fs?.Dispose();
        }
    }
}