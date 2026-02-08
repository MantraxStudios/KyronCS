using System;
using System.IO;

namespace KrayonCore.Utilities
{
    public static class PathUtils
    {
        public static string GetPathAfterContent(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath))
                return string.Empty;

            fullPath = fullPath.Replace("\\", "/");

            const string marker = "Content/";
            int index = fullPath.IndexOf(marker, StringComparison.OrdinalIgnoreCase);

            if (index == -1)
                return string.Empty;

            return fullPath.Substring(index + marker.Length);
        }

        public static string FindFileByName(string basePath, string fileName)
        {
            if (string.IsNullOrEmpty(basePath) || string.IsNullOrEmpty(fileName))
                return string.Empty;

            if (!Directory.Exists(basePath))
                return string.Empty;

            try
            {
                var files = Directory.GetFiles(basePath, fileName, SearchOption.AllDirectories);
                return files.Length > 0 ? files[0].Replace("\\", "/") : string.Empty;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return string.Empty;
            }
        }

        public static bool FileExists(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            return File.Exists(path);
        }

        public static bool DirectoryExists(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            return Directory.Exists(path);
        }

        public static string GetFileNameWithExtension(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            return Path.GetFileName(path);
        }

        public static string GetFileNameWithoutExtension(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            return Path.GetFileNameWithoutExtension(path);
        }
    }
}