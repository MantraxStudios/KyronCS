using System;
using System.IO;

public static class ByteFileLoader
{
    public static byte[] Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"File not found: {path}");

        return File.ReadAllBytes(path);
    }
}
