using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace KrayonCore.Core.Attributes
{
    public static class AssetManager
    {
        // DONT TOUCH THIS VAR. THIS WORK WITH START EDITOR
        public static string TotalBase = "MainProyect/";

        public static string BasePath => $"{TotalBase}Content/";
        public static string CompilerPath => $"{TotalBase}CompileData/";
        public static string ClientDLLPath => $"{TotalBase}bin/Debug/net10.0/";
        public static string GamePak => AppInfo.IsCompiledGame ? "Game.pak" : $"{TotalBase}CompileData/Game.Pak";
        public static string DefaultScene => "/DefaultScene.scene";
        public static string VSProyect => $"{TotalBase}";
        public static string CSProj => $"{TotalBase}KrayonClient.csproj";
        public static string MaterialsPath => $"{TotalBase}EngineMaterials.json";
        public static string VFXPath => $"{TotalBase}VFXData.json";

        private const string DataExt = ".data";
        public static string EngineDataPath => $"{TotalBase}EngineData.json";


        private static Dictionary<Guid, AssetRecord> _assets = new();
        private static Dictionary<Guid, FolderRecord> _folders = new();


        // ─────────────────────────────────────────────
        //  INIT
        // ─────────────────────────────────────────────

        public static void Initialize()
        {
            if (!AppInfo.IsCompiledGame)
                ScanFileSystem();
            else
                LoadFromPak();

            Console.WriteLine($"AssetManager initialized. Assets: {_assets.Count}, Folders: {_folders.Count}");
        }


        // ─────────────────────────────────────────────
        //  QUERY
        // ─────────────────────────────────────────────

        public static bool Exists(Guid guid) => _assets.ContainsKey(guid);
        public static AssetRecord Get(Guid guid) => _assets.TryGetValue(guid, out var a) ? a : null;
        public static IEnumerable<AssetRecord> All() => _assets.Values;
        public static AssetRecord FindByPath(string path) => _assets.Values.FirstOrDefault(a => a.Path == path);

        public static bool FolderExists(Guid guid) => _folders.ContainsKey(guid);
        public static FolderRecord GetFolder(Guid guid) => _folders.TryGetValue(guid, out var f) ? f : null;
        public static IEnumerable<FolderRecord> AllFolders() => _folders.Values;
        public static FolderRecord FindFolderByPath(string p) => _folders.Values.FirstOrDefault(f => f.Path == p);
        public static bool IsFolderRegistered(string p) => _folders.Values.Any(f => f.Path == p);


        // ─────────────────────────────────────────────
        //  READ BYTES
        // ─────────────────────────────────────────────

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

        public static byte[] GetBytes(string key)
        {
            try
            {
                if (AppInfo.IsCompiledGame)
                {
                    using var pak = new KrayonCompiler.PakFile(GamePak);
                    return pak.Load(key);
                }

                string fullPath = Path.Combine(BasePath, key);
                if (!File.Exists(fullPath))
                {
                    Console.WriteLine($"File not found: {fullPath}");
                    return null;
                }
                return File.ReadAllBytes(fullPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading bytes: {ex.Message}");
                return null;
            }
        }


        // ─────────────────────────────────────────────
        //  IMPORT
        // ─────────────────────────────────────────────

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
                    Type = DetectType(fullPath),
                    ImportedAt = DateTime.Now
                };

                _assets.Add(record.Guid, record);
                WriteSidecar(record);

                Console.WriteLine($"Imported: {relativePath} ({record.Type})");
                return record.Guid;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error importing: {ex.Message}");
                return null;
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

                string relativePath = Path.GetRelativePath(BasePath, fullPath).Replace("\\", "/");

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
                    Type = assetType,
                    ImportedAt = DateTime.Now
                };

                _assets.Add(record.Guid, record);
                WriteSidecar(record);

                Console.WriteLine($"Imported asset: {relativePath} as {assetType}");
                return record.Guid;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error importing asset: {ex.Message}");
                return null;
            }
        }


        // ─────────────────────────────────────────────
        //  REFRESH
        // ─────────────────────────────────────────────

        public static void Refresh()
        {
            ScanFileSystem();
            Console.WriteLine($"Refreshed. Assets: {_assets.Count}, Folders: {_folders.Count}");
        }


        // ─────────────────────────────────────────────
        //  MOVE
        // ─────────────────────────────────────────────

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
                string newRelative = string.IsNullOrEmpty(newFolderPath) ? fileName : $"{newFolderPath}/{fileName}";
                string newFullPath = Path.Combine(BasePath, newRelative);

                if (File.Exists(newFullPath))
                {
                    Console.WriteLine($"File already exists at destination: {newRelative}");
                    return false;
                }

                EnsureDirectory(newFullPath);
                File.Move(oldFullPath, newFullPath);
                MoveSidecar(asset.Path, newRelative);

                asset.Path = newRelative;
                WriteSidecar(asset);

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

                var assetsToMove = _assets.Values.Where(a => a.Path.StartsWith(sourceFolderPath + "/")).ToList();
                var foldersToMove = _folders.Values
                    .Where(f => f.Path.StartsWith(sourceFolderPath + "/") || f.Path == sourceFolderPath)
                    .ToList();

                foreach (var asset in assetsToMove)
                {
                    string oldRelative = asset.Path;
                    string relativePart = asset.Path.Substring(sourceFolderPath.Length + 1);
                    string newRelative = $"{newFolderPath}/{relativePart}";
                    string oldFullPath = Path.Combine(BasePath, oldRelative);
                    string newFullPath = Path.Combine(BasePath, newRelative);

                    EnsureDirectory(newFullPath);

                    if (File.Exists(oldFullPath))
                    {
                        File.Move(oldFullPath, newFullPath);
                        MoveSidecar(oldRelative, newRelative);
                        asset.Path = newRelative;
                        WriteSidecar(asset);
                    }
                }

                foreach (var folder in foldersToMove)
                {
                    folder.Path = folder.Path == sourceFolderPath
                        ? newFolderPath
                        : $"{newFolderPath}/{folder.Path.Substring(sourceFolderPath.Length + 1)}";
                }

                string oldFolderFull = Path.Combine(BasePath, sourceFolderPath);
                if (Directory.Exists(oldFolderFull))
                    DeleteEmptyDirectories(oldFolderFull);

                Console.WriteLine($"Moved folder: {sourceFolderPath} -> {newFolderPath}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error moving folder: {ex.Message}");
                return false;
            }
        }


        // ─────────────────────────────────────────────
        //  RENAME
        // ─────────────────────────────────────────────

        public static bool RenameAsset(Guid guid, string newName)
        {
            if (!_assets.TryGetValue(guid, out var asset))
            {
                Console.WriteLine($"Asset {guid} not found");
                return false;
            }

            try
            {
                string oldRelative = asset.Path;
                string oldFullPath = Path.Combine(BasePath, oldRelative);
                string directory = Path.GetDirectoryName(oldRelative)?.Replace("\\", "/") ?? "";
                string extension = Path.GetExtension(oldRelative);

                if (!newName.EndsWith(extension))
                    newName += extension;

                string newRelative = string.IsNullOrEmpty(directory) ? newName : $"{directory}/{newName}";
                string newFullPath = Path.Combine(BasePath, newRelative);

                if (File.Exists(newFullPath))
                {
                    Console.WriteLine($"File already exists: {newRelative}");
                    return false;
                }

                File.Move(oldFullPath, newFullPath);
                MoveSidecar(oldRelative, newRelative);

                asset.Path = newRelative;
                WriteSidecar(asset);

                Console.WriteLine($"Renamed asset: {oldRelative} -> {newRelative}");
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

                string parentPath = folderPath.Contains('/')
                    ? string.Join("/", folderPath.Split('/').SkipLast(1))
                    : "";

                string newFolderPath = string.IsNullOrEmpty(parentPath) ? newName : $"{parentPath}/{newName}";

                var assetsToRename = _assets.Values.Where(a => a.Path.StartsWith(folderPath + "/")).ToList();
                var foldersToRename = _folders.Values
                    .Where(f => f.Path.StartsWith(folderPath + "/") || f.Path == folderPath)
                    .ToList();

                foreach (var asset in assetsToRename)
                {
                    string oldRelative = asset.Path;
                    string relativePart = asset.Path.Substring(folderPath.Length + 1);
                    string newRelative = $"{newFolderPath}/{relativePart}";
                    string oldFullPath = Path.Combine(BasePath, oldRelative);
                    string newFullPath = Path.Combine(BasePath, newRelative);

                    EnsureDirectory(newFullPath);

                    if (File.Exists(oldFullPath))
                    {
                        File.Move(oldFullPath, newFullPath);
                        MoveSidecar(oldRelative, newRelative);
                        asset.Path = newRelative;
                        WriteSidecar(asset);
                    }
                }

                foreach (var folder in foldersToRename)
                {
                    folder.Path = folder.Path == folderPath
                        ? newFolderPath
                        : $"{newFolderPath}/{folder.Path.Substring(folderPath.Length + 1)}";
                }

                string oldFolderFull = Path.Combine(BasePath, folderPath);
                if (Directory.Exists(oldFolderFull))
                    Directory.Delete(oldFolderFull, true);

                Console.WriteLine($"Renamed folder: {folderPath} -> {newFolderPath}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error renaming folder: {ex.Message}");
                return false;
            }
        }


        // ─────────────────────────────────────────────
        //  DELETE
        // ─────────────────────────────────────────────

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

                DeleteSidecar(asset.Path);
                _assets.Remove(guid);

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

                var guidsToDelete = _assets.Values
                    .Where(a => a.Path.StartsWith(folderPath + "/"))
                    .Select(a => a.Guid)
                    .ToList();

                foreach (var guid in guidsToDelete)
                    DeleteAsset(guid);

                var folderGuids = _folders.Values
                    .Where(f => f.Path.StartsWith(folderPath + "/") || f.Path == folderPath)
                    .Select(f => f.Guid)
                    .ToList();

                foreach (var guid in folderGuids)
                    _folders.Remove(guid);

                string fullPath = Path.Combine(BasePath, folderPath);
                if (Directory.Exists(fullPath))
                    Directory.Delete(fullPath, true);

                Console.WriteLine($"Deleted folder: {folderPath}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting folder: {ex.Message}");
                return false;
            }
        }


        // ─────────────────────────────────────────────
        //  CREATE FOLDER
        // ─────────────────────────────────────────────

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

                Console.WriteLine($"Created folder: {newFolderPath}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating folder: {ex.Message}");
                return false;
            }
        }


        // ─────────────────────────────────────────────
        //  SIDECAR  (.data)
        // ─────────────────────────────────────────────

        private static string SidecarFullPath(string assetRelativePath)
            => Path.Combine(BasePath, assetRelativePath + DataExt);

        private static void WriteSidecar(AssetRecord record)
        {
            try
            {
                string sidecarPath = SidecarFullPath(record.Path);
                string json = JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true });
                // Always write without BOM so the file is portable and JsonSerializer-safe
                File.WriteAllText(sidecarPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing sidecar for {record.Path}: {ex.Message}");
            }
        }

        private static AssetRecord ReadSidecar(string sidecarFullPath)
        {
            try
            {
                string json = File.ReadAllText(sidecarFullPath);
                return JsonSerializer.Deserialize<AssetRecord>(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading sidecar {sidecarFullPath}: {ex.Message}");
                return null;
            }
        }

        private static void DeleteSidecar(string assetRelativePath)
        {
            try
            {
                string sidecarPath = SidecarFullPath(assetRelativePath);
                if (File.Exists(sidecarPath))
                    File.Delete(sidecarPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting sidecar for {assetRelativePath}: {ex.Message}");
            }
        }

        private static void MoveSidecar(string oldRelativePath, string newRelativePath)
        {
            try
            {
                string oldSidecar = SidecarFullPath(oldRelativePath);
                string newSidecar = SidecarFullPath(newRelativePath);

                if (File.Exists(oldSidecar))
                {
                    EnsureDirectory(newSidecar);
                    File.Move(oldSidecar, newSidecar);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error moving sidecar {oldRelativePath} -> {newRelativePath}: {ex.Message}");
            }
        }


        // ─────────────────────────────────────────────
        //  SCAN FILE SYSTEM
        // ─────────────────────────────────────────────

        private static void ScanFileSystem()
        {
            if (!Directory.Exists(BasePath))
                Directory.CreateDirectory(BasePath);

            _assets.Clear();
            _folders.Clear();

            // ── Read existing .data sidecars ──────────────────────────────────
            var sidecars = Directory.GetFiles(BasePath, "*" + DataExt, SearchOption.AllDirectories);

            foreach (var sidecarPath in sidecars)
            {
                var record = ReadSidecar(sidecarPath);
                if (record == null)
                    continue;

                // Derive path from physical location, not from what's stored inside.
                // Handles files moved manually on disk.
                string derivedRelativePath = Path
                    .GetRelativePath(BasePath, sidecarPath[..^DataExt.Length])
                    .Replace("\\", "/");

                string assetFullPath = Path.Combine(BasePath, derivedRelativePath);
                if (!File.Exists(assetFullPath))
                {
                    Console.WriteLine($"Asset missing, orphan sidecar: {sidecarPath}");
                    continue;
                }

                if (record.Path != derivedRelativePath)
                {
                    Console.WriteLine($"Sidecar path mismatch, healing: '{record.Path}' -> '{derivedRelativePath}'");
                    record.Path = derivedRelativePath;
                    WriteSidecar(record);
                }

                if (!_assets.ContainsKey(record.Guid))
                    _assets.Add(record.Guid, record);
            }

            // ── Detect new files with no sidecar yet ─────────────────────────
            var allFiles = Directory.GetFiles(BasePath, "*.*", SearchOption.AllDirectories);

            foreach (var file in allFiles)
            {
                if (file.EndsWith(DataExt, StringComparison.OrdinalIgnoreCase))
                    continue;

                string relativePath = Path.GetRelativePath(BasePath, file).Replace("\\", "/");

                if (_assets.Values.Any(a => a.Path == relativePath))
                    continue;

                relativePath = ResolveNameConflict(relativePath, file);

                var record = new AssetRecord
                {
                    Guid = Guid.NewGuid(),
                    Path = relativePath,
                    Type = DetectType(file),
                    ImportedAt = DateTime.Now
                };

                _assets.Add(record.Guid, record);
                WriteSidecar(record);
                Console.WriteLine($"New asset detected, sidecar created: {relativePath}");
            }

            // ── Remove stale in-memory entries ────────────────────────────────
            var staleAssets = _assets.Values
                .Where(a => !File.Exists(Path.Combine(BasePath, a.Path)))
                .ToList();

            foreach (var asset in staleAssets)
            {
                _assets.Remove(asset.Guid);
                Console.WriteLine($"Removed missing asset: {asset.Path}");
            }

            // ── Folders ───────────────────────────────────────────────────────
            var directories = Directory.GetDirectories(BasePath, "*", SearchOption.AllDirectories);

            foreach (var dir in directories)
            {
                string relativePath = Path.GetRelativePath(BasePath, dir).Replace("\\", "/");

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

            var staleFolders = _folders.Values
                .Where(f => !Directory.Exists(Path.Combine(BasePath, f.Path)))
                .ToList();

            foreach (var folder in staleFolders)
            {
                _folders.Remove(folder.Guid);
                Console.WriteLine($"Removed missing folder: {folder.Path}");
            }
        }


        // ─────────────────────────────────────────────
        //  COMPILED GAME: load from .pak
        // ─────────────────────────────────────────────

        private static void LoadFromPak()
        {
            try
            {
                byte[] bytes = GetBytes("Engine.AssetsData");
                if (bytes == null)
                    return;

                // Strip UTF-8 BOM (EF BB BF) defensively — should never be present
                // since BuildPipeline writes without BOM, but just in case.
                if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                    bytes = bytes[3..];

                string json = Encoding.UTF8.GetString(bytes);
                var list = JsonSerializer.Deserialize<List<AssetRecord>>(json);
                if (list == null)
                    return;

                foreach (var record in list)
                {
                    if (!_assets.ContainsKey(record.Guid))
                        _assets.Add(record.Guid, record);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading assets from pak: {ex.Message}");
            }
        }


        // ─────────────────────────────────────────────
        //  HELPERS
        // ─────────────────────────────────────────────

        private static string ResolveNameConflict(string relativePath, string physicalFile = null)
        {
            string directory = Path.GetDirectoryName(relativePath)?.Replace("\\", "/") ?? "";
            string nameWithoutExt = Path.GetFileNameWithoutExtension(relativePath);
            string extension = Path.GetExtension(relativePath);

            bool pathExists = _assets.Values.Any(a =>
                a.Path.Equals(relativePath, StringComparison.OrdinalIgnoreCase));

            if (!pathExists)
                return relativePath;

            string candidate = nameWithoutExt + "_clone";
            int counter = 1;

            string CandidatePath() => string.IsNullOrEmpty(directory)
                ? candidate + extension
                : $"{directory}/{candidate}{extension}";

            while (_assets.Values.Any(a =>
                a.Path.Equals(CandidatePath(), StringComparison.OrdinalIgnoreCase)))
            {
                candidate = nameWithoutExt + "_clone" + counter;
                counter++;
            }

            string newRelativePath = CandidatePath();

            if (physicalFile != null)
            {
                string newFullPath = Path.Combine(BasePath, newRelativePath);
                EnsureDirectory(newFullPath);
                File.Move(physicalFile, newFullPath);
                Console.WriteLine($"Duplicate path detected, renamed: {relativePath} -> {newRelativePath}");
            }

            return newRelativePath;
        }

        private static string DetectType(string file) =>
        Path.GetExtension(file).ToLower() switch
        {
            ".png" or ".jpg" or ".jpeg" => "Texture",
            ".fbx" or ".obj" => "Model",
            ".wav" or ".mp3" => "Audio",
            ".frag" or ".vert" => "Shader",
            ".js" => "GameScript",
            ".mat" => "Material",
            ".cs" => "Script",
            ".animator" => "AnimatorController",  
            _ => "Unknown"
        };

        private static void EnsureDirectory(string fullFilePath)
        {
            string dir = Path.GetDirectoryName(fullFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }

        public static IEnumerable<AssetRecord> GetAllScenes()
            => _assets.Values.Where(a => a.Type == "Scene" || a.Path.EndsWith(".scene", StringComparison.OrdinalIgnoreCase));

        private static void DeleteEmptyDirectories(string directory)
        {
            try
            {
                foreach (var sub in Directory.GetDirectories(directory))
                    DeleteEmptyDirectories(sub);

                if (!Directory.EnumerateFileSystemEntries(directory).Any())
                    Directory.Delete(directory);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting empty directory {directory}: {ex.Message}");
            }
        }
    }
}