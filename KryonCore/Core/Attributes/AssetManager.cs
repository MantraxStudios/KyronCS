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
        private static Dictionary<Guid, AssetRecord> _assets = new();
        private static Dictionary<Guid, FolderRecord> _folders = new();

        // =========================
        // INIT
        // =========================

        public static void Initialize()
        {
            LoadDatabase();
            ScanFileSystem();
            SaveDatabase();
            Console.WriteLine($"AssetManager initialized. Assets: {_assets.Count}, Folders: {_folders.Count}");
        }

        // =========================
        // QUERY - ASSETS
        // =========================

        public static bool Exists(Guid guid)
            => _assets.ContainsKey(guid);

        public static AssetRecord Get(Guid guid)
            => _assets.TryGetValue(guid, out var asset) ? asset : null;

        public static IEnumerable<AssetRecord> All()
            => _assets.Values;

        public static AssetRecord FindByPath(string relativePath)
            => _assets.Values.FirstOrDefault(a => a.Path == relativePath);

        // =========================
        // QUERY - FOLDERS
        // =========================

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

        // =========================
        // MOVE & REORGANIZE
        // =========================

        /// <summary>
        /// Mueve un asset a una nueva carpeta
        /// </summary>
        public static bool MoveAsset(Guid guid, string newFolderPath)
        {
            if (!_assets.TryGetValue(guid, out var asset))
            {
                Console.WriteLine($"Asset {guid} not found");
                return false;
            }

            try
            {
                // Construir rutas
                string oldFullPath = Path.Combine(BasePath, asset.Path);
                string fileName = Path.GetFileName(asset.Path);
                string newRelativePath = string.IsNullOrEmpty(newFolderPath)
                    ? fileName
                    : $"{newFolderPath}/{fileName}";
                string newFullPath = Path.Combine(BasePath, newRelativePath);

                // Verificar si el destino ya existe
                if (File.Exists(newFullPath))
                {
                    Console.WriteLine($"File already exists at destination: {newRelativePath}");
                    return false;
                }

                // Crear directorio de destino si no existe
                string newDirectory = Path.GetDirectoryName(newFullPath);
                if (!string.IsNullOrEmpty(newDirectory) && !Directory.Exists(newDirectory))
                {
                    Directory.CreateDirectory(newDirectory);
                }

                // Mover archivo físico
                File.Move(oldFullPath, newFullPath);

                // Actualizar registro
                asset.Path = newRelativePath;

                // Guardar cambios
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

        /// <summary>
        /// Mueve una carpeta completa con todo su contenido
        /// </summary>
        public static bool MoveFolder(string sourceFolderPath, string destinationFolderPath)
        {
            try
            {
                // Normalizar paths
                sourceFolderPath = sourceFolderPath.Trim('/');
                destinationFolderPath = destinationFolderPath?.Trim('/') ?? "";

                // Obtener nombre de la carpeta
                string folderName = sourceFolderPath.Split('/').Last();
                string newFolderPath = string.IsNullOrEmpty(destinationFolderPath)
                    ? folderName
                    : $"{destinationFolderPath}/{folderName}";

                // Verificar que no se esté moviendo a sí misma o a una subcarpeta
                if (newFolderPath.StartsWith(sourceFolderPath + "/"))
                {
                    Console.WriteLine("Cannot move folder into itself or its subfolder");
                    return false;
                }

                // Obtener todos los assets en la carpeta y subcarpetas
                var assetsToMove = _assets.Values
                    .Where(a => a.Path.StartsWith(sourceFolderPath + "/"))
                    .ToList();

                // Obtener todas las subcarpetas
                var foldersToMove = _folders.Values
                    .Where(f => f.Path.StartsWith(sourceFolderPath + "/") || f.Path == sourceFolderPath)
                    .ToList();

                // Mover cada asset
                foreach (var asset in assetsToMove)
                {
                    string oldFullPath = Path.Combine(BasePath, asset.Path);
                    string relativePart = asset.Path.Substring(sourceFolderPath.Length + 1);
                    string newRelativePath = string.IsNullOrEmpty(newFolderPath)
                        ? relativePart
                        : $"{newFolderPath}/{relativePart}";
                    string newFullPath = Path.Combine(BasePath, newRelativePath);

                    // Crear directorios necesarios
                    string newDirectory = Path.GetDirectoryName(newFullPath);
                    if (!string.IsNullOrEmpty(newDirectory) && !Directory.Exists(newDirectory))
                    {
                        Directory.CreateDirectory(newDirectory);
                    }

                    // Mover archivo
                    if (File.Exists(oldFullPath))
                    {
                        File.Move(oldFullPath, newFullPath);
                        asset.Path = newRelativePath;
                    }
                }

                // Actualizar registros de carpetas
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

                // Eliminar carpeta antigua si está vacía
                string oldFolderFullPath = Path.Combine(BasePath, sourceFolderPath);
                if (Directory.Exists(oldFolderFullPath))
                {
                    DeleteEmptyDirectories(oldFolderFullPath);
                }

                // Guardar cambios
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

        /// <summary>
        /// Renombra un asset
        /// </summary>
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

                // Asegurar que el nuevo nombre tenga la extensión correcta
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

        /// <summary>
        /// Renombra una carpeta
        /// </summary>
        public static bool RenameFolder(string folderPath, string newName)
        {
            try
            {
                folderPath = folderPath.Trim('/');

                // Obtener el path padre
                string parentPath = "";
                if (folderPath.Contains('/'))
                {
                    parentPath = string.Join("/", folderPath.Split('/').SkipLast(1));
                }

                string newFolderPath = string.IsNullOrEmpty(parentPath)
                    ? newName
                    : $"{parentPath}/{newName}";

                // Obtener todos los assets en la carpeta
                var assetsToRename = _assets.Values
                    .Where(a => a.Path.StartsWith(folderPath + "/"))
                    .ToList();

                // Obtener todas las subcarpetas
                var foldersToRename = _folders.Values
                    .Where(f => f.Path.StartsWith(folderPath + "/") || f.Path == folderPath)
                    .ToList();

                foreach (var asset in assetsToRename)
                {
                    string oldFullPath = Path.Combine(BasePath, asset.Path);
                    string relativePart = asset.Path.Substring(folderPath.Length + 1);
                    string newRelativePath = $"{newFolderPath}/{relativePart}";
                    string newFullPath = Path.Combine(BasePath, newRelativePath);

                    // Crear directorios necesarios
                    string newDirectory = Path.GetDirectoryName(newFullPath);
                    if (!string.IsNullOrEmpty(newDirectory) && !Directory.Exists(newDirectory))
                    {
                        Directory.CreateDirectory(newDirectory);
                    }

                    if (File.Exists(oldFullPath))
                    {
                        File.Move(oldFullPath, newFullPath);
                        asset.Path = newRelativePath;
                    }
                }

                // Actualizar registros de carpetas
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

                // Eliminar carpeta antigua
                string oldFolderFullPath = Path.Combine(BasePath, folderPath);
                if (Directory.Exists(oldFolderFullPath))
                {
                    Directory.Delete(oldFolderFullPath, true);
                }

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

        /// <summary>
        /// Elimina un asset
        /// </summary>
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
                {
                    File.Delete(fullPath);
                }

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

        /// <summary>
        /// Elimina una carpeta y todo su contenido
        /// </summary>
        public static bool DeleteFolder(string folderPath)
        {
            try
            {
                folderPath = folderPath.Trim('/');

                // Eliminar todos los assets en la carpeta
                var assetsToDelete = _assets.Values
                    .Where(a => a.Path.StartsWith(folderPath + "/"))
                    .Select(a => a.Guid)
                    .ToList();

                foreach (var guid in assetsToDelete)
                {
                    DeleteAsset(guid);
                }

                // Eliminar todas las subcarpetas registradas
                var foldersToDelete = _folders.Values
                    .Where(f => f.Path.StartsWith(folderPath + "/") || f.Path == folderPath)
                    .Select(f => f.Guid)
                    .ToList();

                foreach (var guid in foldersToDelete)
                {
                    _folders.Remove(guid);
                }

                // Eliminar carpeta física
                string fullPath = Path.Combine(BasePath, folderPath);
                if (Directory.Exists(fullPath))
                {
                    Directory.Delete(fullPath, true);
                }

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

        /// <summary>
        /// Crea una nueva carpeta
        /// </summary>
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

                // Crear carpeta física
                Directory.CreateDirectory(fullPath);

                // Registrar carpeta en la base de datos
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

        /// <summary>
        /// Importa un asset existente al AssetManager
        /// </summary>
        public static Guid? ImportAsset(string fullPath, string assetType)
        {
            try
            {
                if (!File.Exists(fullPath))
                {
                    Console.WriteLine($"File not found: {fullPath}");
                    return null;
                }

                // Obtener path relativo
                string relativePath = Path.GetRelativePath(BasePath, fullPath)
                    .Replace("\\", "/");

                // Verificar si ya existe
                var existing = FindByPath(relativePath);
                if (existing != null)
                {
                    Console.WriteLine($"Asset already registered: {relativePath}");
                    return existing.Guid;
                }

                // Crear nuevo registro
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

        // =========================
        // FILE SYSTEM SYNC
        // =========================

        private static void ScanFileSystem()
        {
            if (!Directory.Exists(BasePath))
                Directory.CreateDirectory(BasePath);

            // Escanear archivos
            var files = Directory.GetFiles(BasePath, "*.*", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                var relativePath = Path.GetRelativePath(BasePath, file)
                    .Replace("\\", "/");

                if (_assets.Values.Any(a => a.Path == relativePath))
                    continue;

                var record = new AssetRecord
                {
                    Guid = Guid.NewGuid(),
                    Path = relativePath,
                    Type = DetectType(file)
                };

                _assets.Add(record.Guid, record);
                Console.WriteLine($"New asset detected: {relativePath}");
            }

            // Escanear carpetas
            var directories = Directory.GetDirectories(BasePath, "*", SearchOption.AllDirectories);

            foreach (var directory in directories)
            {
                var relativePath = Path.GetRelativePath(BasePath, directory)
                    .Replace("\\", "/");

                // Si la carpeta ya está registrada, continuar
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

            // Limpiar assets que ya no existen en el sistema de archivos
            var assetsToRemove = _assets.Values
                .Where(a => !File.Exists(Path.Combine(BasePath, a.Path)))
                .ToList();

            foreach (var asset in assetsToRemove)
            {
                _assets.Remove(asset.Guid);
                Console.WriteLine($"Removed missing asset: {asset.Path}");
            }

            // Limpiar carpetas que ya no existen
            var foldersToRemove = _folders.Values
                .Where(f => !Directory.Exists(Path.Combine(BasePath, f.Path)))
                .ToList();

            foreach (var folder in foldersToRemove)
            {
                _folders.Remove(folder.Guid);
                Console.WriteLine($"Removed missing folder: {folder.Path}");
            }
        }

        // =========================
        // DATABASE
        // =========================

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

                // Cargar assets
                if (data.Assets != null)
                {
                    foreach (var asset in data.Assets)
                    {
                        if (!_assets.ContainsKey(asset.Guid))
                            _assets.Add(asset.Guid, asset);
                    }
                }

                // Cargar folders
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

        // Método público para forzar guardado
        public static void SaveDatabasePublic()
        {
            SaveDatabase();
        }

        // =========================
        // HELPERS
        // =========================

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
                {
                    DeleteEmptyDirectories(subDir);
                }

                if (!Directory.EnumerateFileSystemEntries(directory).Any())
                {
                    Directory.Delete(directory);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting empty directory {directory}: {ex.Message}");
            }
        }

        // Clase contenedora para serialización
        private class DatabaseContainer
        {
            public List<AssetRecord> Assets { get; set; } = new();
            public List<FolderRecord> Folders { get; set; } = new();
        }
    }
}