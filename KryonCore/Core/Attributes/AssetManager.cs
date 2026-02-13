using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace KrayonCore.Core.Attributes
{
    public static class AssetManager
    {
        public static string BasePath = "MainProyect/Content/";
        public static string DataBase = "MainProyect/DataBaseFromAssets.json";
        public static string CompilerPath = "MainProyect/CompileData/";
        public static string ClientDLLPath = "MainProyect/bin/Debug/net10.0/";
        public static string GamePak = "MainProyect/CompileData/Game.Pak";
        public static string VSProyect = "MainProyect/";
        public static string CSProj = "MainProyect/KrayonClient.csproj";
        private static Dictionary<Guid, AssetRecord> _assets = new();
        private static Dictionary<Guid, FolderRecord> _folders = new();


        public static void Initialize()
        {
            LoadDatabase();

            if (!AppInfo.IsCompiledGame)
            {
                ScanFileSystem();
                SaveDatabase();
            }
            Console.WriteLine($"AssetManager initialized. Assets: {_assets.Count}, Folders: {_folders.Count}");
        }

        public static bool Exists(Guid guid)
            => _assets.ContainsKey(guid);

        public static AssetRecord Get(Guid guid)
            => _assets.TryGetValue(guid, out var asset) ? asset : null;

        public static byte[] GetBytes(Guid guid)
        {
            if (!_assets.TryGetValue(guid, out var asset))
            {
                Console.WriteLine($"Asset {guid} not found");
                return null;
            }

            try
            {
                if (AppInfo.IsCompiledGame)
                {
                    using var pak = new KrayonCompiler.PakFile(GamePak);
                    return pak.Load(asset.Guid.ToString());
                }

                string fullPath = Path.Combine(BasePath, asset.Path);
                if (!File.Exists(fullPath))
                {
                    Console.WriteLine($"File not found: {fullPath}");
                    return null;
                }
                return File.ReadAllBytes(fullPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading asset bytes: {ex.Message}");
                return null;
            }
        }

        public static IEnumerable<AssetRecord> All()
            => _assets.Values;

        public static AssetRecord FindByPath(string relativePath)
            => _assets.Values.FirstOrDefault(a => a.Path == relativePath);

        public static bool FolderExists(Guid guid)
            => _folders.ContainsKey(guid);

        public static FolderRecord GetFolder(Guid guid)
            => _folders.TryGetValue(guid, out var folder) ? folder : null;

        public static IEnumerable<FolderRecord> AllFolders()
            => _folders.Values;

        public static FolderRecord FindFolderByPath(string relativePath)
            => _folders.Values.FirstOrDefault(f => f.Path == relativePath);

        public static bool IsFolderRegistered(string folderPath)
            => _folders.Values.Any(f => f.Path == folderPath);

        public static Guid? Import(string relativePath)
        {
            try
            {
                relativePath = relativePath.Replace("\\", "/");
                string fullPath = Path.Combine(BasePath, relativePath);

                if (!File.Exists(fullPath))
                {
                    Console.WriteLine($"File not found: {fullPath}");
                    return null;
                }

                var existing = FindByPath(relativePath);
                if (existing != null)
                {
                    Console.WriteLine($"Already registered: {relativePath}");
                    return existing.Guid;
                }

                var record = new AssetRecord
                {
                    Guid = Guid.NewGuid(),
                    Path = relativePath,
                    Type = DetectType(fullPath)
                };

                _assets.Add(record.Guid, record);
                SaveDatabase();

                Console.WriteLine($"Imported: {relativePath} ({record.Type})");
                return record.Guid;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error importing: {ex.Message}");
                return null;
            }
        }

        public static void Refresh()
        {
            ScanFileSystem();
            SaveDatabase();
            Console.WriteLine($"Refreshed. Assets: {_assets.Count}, Folders: {_folders.Count}");
        }

        public static bool MoveAsset(Guid guid, string newFolderPath)
        {
            if (!_assets.TryGetValue(guid, out var asset))
            {
                Console.WriteLine($"Asset {guid} not found");
                return false;
            }

            try
            {
                string oldFullPath = Path.Combine(BasePath, asset.Path);
                string fileName = Path.GetFileName(asset.Path);
                string newRelativePath = string.IsNullOrEmpty(newFolderPath)
                    ? fileName
                    : $"{newFolderPath}/{fileName}";
                string newFullPath = Path.Combine(BasePath, newRelativePath);

                if (File.Exists(newFullPath))
                {
                    Console.WriteLine($"File already exists at destination: {newRelativePath}");
                    return false;
                }

                string newDirectory = Path.GetDirectoryName(newFullPath);
                if (!string.IsNullOrEmpty(newDirectory) && !Directory.Exists(newDirectory))
                    Directory.CreateDirectory(newDirectory);

                File.Move(oldFullPath, newFullPath);
                asset.Path = newRelativePath;
                SaveDatabase();

                Console.WriteLine($"Moved asset: {oldFullPath} -> {newFullPath}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error moving asset: {ex.Message}");
                return false;
            }
        }

        public static bool MoveFolder(string sourceFolderPath, string destinationFolderPath)
        {
            try
            {
                sourceFolderPath = sourceFolderPath.Trim('/');
                destinationFolderPath = destinationFolderPath?.Trim('/') ?? "";

                string folderName = sourceFolderPath.Split('/').Last();
                string newFolderPath = string.IsNullOrEmpty(destinationFolderPath)
                    ? folderName
                    : $"{destinationFolderPath}/{folderName}";

                if (newFolderPath.StartsWith(sourceFolderPath + "/"))
                {
                    Console.WriteLine("Cannot move folder into itself or its subfolder");
                    return false;
                }

                var assetsToMove = _assets.Values
                    .Where(a => a.Path.StartsWith(sourceFolderPath + "/"))
                    .ToList();

                var foldersToMove = _folders.Values
                    .Where(f => f.Path.StartsWith(sourceFolderPath + "/") || f.Path == sourceFolderPath)
                    .ToList();

                foreach (var asset in assetsToMove)
                {
                    string oldFullPath = Path.Combine(BasePath, asset.Path);
                    string relativePart = asset.Path.Substring(sourceFolderPath.Length + 1);
                    string newRelativePath = string.IsNullOrEmpty(newFolderPath)
                        ? relativePart
                        : $"{newFolderPath}/{relativePart}";
                    string newFullPath = Path.Combine(BasePath, newRelativePath);

                    string newDirectory = Path.GetDirectoryName(newFullPath);
                    if (!string.IsNullOrEmpty(newDirectory) && !Directory.Exists(newDirectory))
                        Directory.CreateDirectory(newDirectory);

                    if (File.Exists(oldFullPath))
                    {
                        File.Move(oldFullPath, newFullPath);
                        asset.Path = newRelativePath;
                    }
                }

                foreach (var folder in foldersToMove)
                {
                    if (folder.Path == sourceFolderPath)
                    {
                        folder.Path = newFolderPath;
                    }
                    else
                    {
                        string relativePart = folder.Path.Substring(sourceFolderPath.Length + 1);
                        folder.Path = $"{newFolderPath}/{relativePart}";
                    }
                }

                string oldFolderFullPath = Path.Combine(BasePath, sourceFolderPath);
                if (Directory.Exists(oldFolderFullPath))
                    DeleteEmptyDirectories(oldFolderFullPath);

                SaveDatabase();
                Console.WriteLine($"Moved folder: {sourceFolderPath} -> {newFolderPath}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error moving folder: {ex.Message}");
                return false;
            }
        }

        public static bool RenameAsset(Guid guid, string newName)
        {
            if (!_assets.TryGetValue(guid, out var asset))
            {
                Console.WriteLine($"Asset {guid} not found");
                return false;
            }

            try
            {
                string oldFullPath = Path.Combine(BasePath, asset.Path);
                string directory = Path.GetDirectoryName(asset.Path)?.Replace("\\", "/") ?? "";
                string extension = Path.GetExtension(asset.Path);

                if (!newName.EndsWith(extension))
                    newName += extension;

                string newRelativePath = string.IsNullOrEmpty(directory)
                    ? newName
                    : $"{directory}/{newName}";
                string newFullPath = Path.Combine(BasePath, newRelativePath);

                if (File.Exists(newFullPath))
                {
                    Console.WriteLine($"File already exists: {newRelativePath}");
                    return false;
                }

                File.Move(oldFullPath, newFullPath);
                asset.Path = newRelativePath;
                SaveDatabase();

                Console.WriteLine($"Renamed asset: {asset.Path} -> {newRelativePath}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error renaming asset: {ex.Message}");
                return false;
            }
        }

        public static bool RenameFolder(string folderPath, string newName)
        {
            try
            {
                folderPath = folderPath.Trim('/');

                string parentPath = "";
                if (folderPath.Contains('/'))
                    parentPath = string.Join("/", folderPath.Split('/').SkipLast(1));

                string newFolderPath = string.IsNullOrEmpty(parentPath)
                    ? newName
                    : $"{parentPath}/{newName}";

                var assetsToRename = _assets.Values
                    .Where(a => a.Path.StartsWith(folderPath + "/"))
                    .ToList();

                var foldersToRename = _folders.Values
                    .Where(f => f.Path.StartsWith(folderPath + "/") || f.Path == folderPath)
                    .ToList();

                foreach (var asset in assetsToRename)
                {
                    string oldFullPath = Path.Combine(BasePath, asset.Path);
                    string relativePart = asset.Path.Substring(folderPath.Length + 1);
                    string newRelativePath = $"{newFolderPath}/{relativePart}";
                    string newFullPath = Path.Combine(BasePath, newRelativePath);

                    string newDirectory = Path.GetDirectoryName(newFullPath);
                    if (!string.IsNullOrEmpty(newDirectory) && !Directory.Exists(newDirectory))
                        Directory.CreateDirectory(newDirectory);

                    if (File.Exists(oldFullPath))
                    {
                        File.Move(oldFullPath, newFullPath);
                        asset.Path = newRelativePath;
                    }
                }

                foreach (var folder in foldersToRename)
                {
                    if (folder.Path == folderPath)
                    {
                        folder.Path = newFolderPath;
                    }
                    else
                    {
                        string relativePart = folder.Path.Substring(folderPath.Length + 1);
                        folder.Path = $"{newFolderPath}/{relativePart}";
                    }
                }

                string oldFolderFullPath = Path.Combine(BasePath, folderPath);
                if (Directory.Exists(oldFolderFullPath))
                    Directory.Delete(oldFolderFullPath, true);

                SaveDatabase();
                Console.WriteLine($"Renamed folder: {folderPath} -> {newFolderPath}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error renaming folder: {ex.Message}");
                return false;
            }
        }

        public static bool DeleteAsset(Guid guid)
        {
            if (!_assets.TryGetValue(guid, out var asset))
            {
                Console.WriteLine($"Asset {guid} not found");
                return false;
            }

            try
            {
                string fullPath = Path.Combine(BasePath, asset.Path);
                if (File.Exists(fullPath))
                    File.Delete(fullPath);

                _assets.Remove(guid);
                SaveDatabase();

                Console.WriteLine($"Deleted asset: {asset.Path}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting asset: {ex.Message}");
                return false;
            }
        }

        public static bool DeleteFolder(string folderPath)
        {
            try
            {
                folderPath = folderPath.Trim('/');

                var assetsToDelete = _assets.Values
                    .Where(a => a.Path.StartsWith(folderPath + "/"))
                    .Select(a => a.Guid)
                    .ToList();

                foreach (var guid in assetsToDelete)
                    DeleteAsset(guid);

                var foldersToDelete = _folders.Values
                    .Where(f => f.Path.StartsWith(folderPath + "/") || f.Path == folderPath)
                    .Select(f => f.Guid)
                    .ToList();

                foreach (var guid in foldersToDelete)
                    _folders.Remove(guid);

                string fullPath = Path.Combine(BasePath, folderPath);
                if (Directory.Exists(fullPath))
                    Directory.Delete(fullPath, true);

                SaveDatabase();
                Console.WriteLine($"Deleted folder: {folderPath}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting folder: {ex.Message}");
                return false;
            }
        }

        public static bool CreateFolder(string parentFolderPath, string folderName)
        {
            try
            {
                string newFolderPath = string.IsNullOrEmpty(parentFolderPath)
                    ? folderName
                    : $"{parentFolderPath}/{folderName}";

                string fullPath = Path.Combine(BasePath, newFolderPath);

                if (Directory.Exists(fullPath))
                {
                    Console.WriteLine($"Folder already exists: {newFolderPath}");
                    return false;
                }

                Directory.CreateDirectory(fullPath);

                var folderRecord = new FolderRecord
                {
                    Guid = Guid.NewGuid(),
                    Path = newFolderPath,
                    CreatedAt = DateTime.Now
                };

                _folders.Add(folderRecord.Guid, folderRecord);
                SaveDatabase();

                Console.WriteLine($"Created folder: {newFolderPath}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating folder: {ex.Message}");
                return false;
            }
        }

        public static Guid? ImportAsset(string fullPath, string assetType)
        {
            try
            {
                if (!File.Exists(fullPath))
                {
                    Console.WriteLine($"File not found: {fullPath}");
                    return null;
                }

                string relativePath = Path.GetRelativePath(BasePath, fullPath)
                    .Replace("\\", "/");

                var existing = FindByPath(relativePath);
                if (existing != null)
                {
                    Console.WriteLine($"Asset already registered: {relativePath}");
                    return existing.Guid;
                }

                relativePath = ResolveNameConflict(relativePath);

                var record = new AssetRecord
                {
                    Guid = Guid.NewGuid(),
                    Path = relativePath,
                    Type = assetType
                };

                _assets.Add(record.Guid, record);
                SaveDatabase();

                Console.WriteLine($"Imported asset: {relativePath} as {assetType}");
                return record.Guid;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error importing asset: {ex.Message}");
                return null;
            }
        }

        private static void ScanFileSystem()
        {
            if (!Directory.Exists(BasePath))
                Directory.CreateDirectory(BasePath);

            var files = Directory.GetFiles(BasePath, "*.*", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                var relativePath = Path.GetRelativePath(BasePath, file)
                    .Replace("\\", "/");

                if (_assets.Values.Any(a => a.Path == relativePath))
                    continue;

                relativePath = ResolveNameConflict(relativePath, file);

                var record = new AssetRecord
                {
                    Guid = Guid.NewGuid(),
                    Path = relativePath,
                    Type = DetectType(file)
                };

                _assets.Add(record.Guid, record);
                Console.WriteLine($"New asset detected: {relativePath}");
            }

            var directories = Directory.GetDirectories(BasePath, "*", SearchOption.AllDirectories);

            foreach (var directory in directories)
            {
                var relativePath = Path.GetRelativePath(BasePath, directory)
                    .Replace("\\", "/");

                if (_folders.Values.Any(f => f.Path == relativePath))
                    continue;

                var folderRecord = new FolderRecord
                {
                    Guid = Guid.NewGuid(),
                    Path = relativePath,
                    CreatedAt = DateTime.Now
                };

                _folders.Add(folderRecord.Guid, folderRecord);
                Console.WriteLine($"New folder detected: {relativePath}");
            }

            var assetsToRemove = _assets.Values
                .Where(a => !File.Exists(Path.Combine(BasePath, a.Path)))
                .ToList();

            foreach (var asset in assetsToRemove)
            {
                _assets.Remove(asset.Guid);
                Console.WriteLine($"Removed missing asset: {asset.Path}");
            }

            var foldersToRemove = _folders.Values
                .Where(f => !Directory.Exists(Path.Combine(BasePath, f.Path)))
                .ToList();

            foreach (var folder in foldersToRemove)
            {
                _folders.Remove(folder.Guid);
                Console.WriteLine($"Removed missing folder: {folder.Path}");
            }
        }

        private static string ResolveNameConflict(string relativePath, string physicalFile = null)
        {
            string directory = Path.GetDirectoryName(relativePath)?.Replace("\\", "/") ?? "";
            string nameWithoutExt = Path.GetFileNameWithoutExtension(relativePath);
            string extension = Path.GetExtension(relativePath);

            string fileName = Path.GetFileName(relativePath);

            bool nameExists = _assets.Values.Any(a =>
                Path.GetFileName(a.Path).Equals(fileName, StringComparison.OrdinalIgnoreCase));

            if (!nameExists)
                return relativePath;

            string candidateName = nameWithoutExt + "_clone";
            int counter = 1;

            while (_assets.Values.Any(a =>
                Path.GetFileName(a.Path).Equals(candidateName + extension, StringComparison.OrdinalIgnoreCase)))
            {
                candidateName = nameWithoutExt + "_clone" + counter;
                counter++;
            }

            string newRelativePath = string.IsNullOrEmpty(directory)
                ? candidateName + extension
                : $"{directory}/{candidateName}{extension}";

            if (physicalFile != null)
            {
                string newFullPath = Path.Combine(BasePath, newRelativePath);
                string newDir = Path.GetDirectoryName(newFullPath);
                if (!string.IsNullOrEmpty(newDir) && !Directory.Exists(newDir))
                    Directory.CreateDirectory(newDir);

                File.Move(physicalFile, newFullPath);
                Console.WriteLine($"Duplicate name detected, renamed: {fileName} -> {candidateName}{extension}");
            }

            return newRelativePath;
        }

        private static void LoadDatabase()
        {
            if (!File.Exists(DataBase))
                return;

            try
            {
                var json = File.ReadAllText(DataBase);
                var data = JsonSerializer.Deserialize<DatabaseContainer>(json);

                if (data == null)
                    return;

                if (data.Assets != null)
                {
                    foreach (var asset in data.Assets)
                    {
                        if (!_assets.ContainsKey(asset.Guid))
                            _assets.Add(asset.Guid, asset);
                    }
                }

                if (data.Folders != null)
                {
                    foreach (var folder in data.Folders)
                    {
                        if (!_folders.ContainsKey(folder.Guid))
                            _folders.Add(folder.Guid, folder);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading database: {ex.Message}");
            }
        }

        private static void SaveDatabase()
        {
            try
            {
                var container = new DatabaseContainer
                {
                    Assets = _assets.Values.ToList(),
                    Folders = _folders.Values.ToList()
                };

                var json = JsonSerializer.Serialize(
                    container,
                    new JsonSerializerOptions { WriteIndented = true }
                );

                File.WriteAllText(DataBase, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving database: {ex.Message}");
            }
        }

        public static void SaveDatabasePublic() => SaveDatabase();

        private static string DetectType(string file)
        {
            return Path.GetExtension(file).ToLower() switch
            {
                ".png" or ".jpg" or ".jpeg" => "Texture",
                ".fbx" or ".obj" => "Model",
                ".wav" or ".mp3" => "Audio",
                ".frag" or ".vert" => "Shader",
                ".js" => "GameScript",
                ".mat" => "Material",
                ".cs" => "Script",
                _ => "Unknown"
            };
        }

        private static void DeleteEmptyDirectories(string directory)
        {
            try
            {
                foreach (var subDir in Directory.GetDirectories(directory))
                    DeleteEmptyDirectories(subDir);

                if (!Directory.EnumerateFileSystemEntries(directory).Any())
                    Directory.Delete(directory);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting empty directory {directory}: {ex.Message}");
            }
        }

        private class DatabaseContainer
        {
            public List<AssetRecord> Assets { get; set; } = new();
            public List<FolderRecord> Folders { get; set; } = new();
        }
    }
}