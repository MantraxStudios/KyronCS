using ImGuiNET;
using KrayonCore.Core.Attributes;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace KrayonEditor.UI
{
    public class AssetsUI : UIBehaviour
    {
        private Guid? _selectedAsset = null;
        private string _selectedFolder = "";

        // Context menu
        private bool _showContextMenu = false;
        private Vector2 _contextMenuPos = Vector2.Zero;
        private Guid? _contextMenuAsset = null;
        private string _contextMenuFolder = "";
        private bool _contextMenuOnEmpty = false;

        // Rename
        private bool _isRenaming = false;
        private string _renameBuffer = "";
        private Guid? _renamingAssetGuid = null;
        private string _renamingFolderPath = "";

        // New File Dialog
        private bool _showNewFileDialog = false;
        private string _newFileName = "";
        private string _selectedFileType = "Script";
        private string _newFileTargetFolder = "";
        private readonly string[] _fileTypes = { "Script", "Texture", "Model", "Audio", "Material", "Prefab" };

        // New Folder Dialog
        private bool _showNewFolderDialog = false;
        private string _newFolderName = "";
        private string _newFolderTargetPath = "";

        // Tree state
        private HashSet<string> _openFolders = new HashSet<string>();

        public override void OnDrawUI()
        {
            if (!_isVisible)
                return;

            ImGui.Begin("Assets", ref _isVisible);

            DrawToolbar();
            ImGui.Separator();

            // Tree view
            if (ImGui.BeginChild("AssetTree", new Vector2(0, 0)))
            {
                DrawTree();
            }
            ImGui.EndChild();

            DrawContextMenu();
            DrawNewFileDialog();
            DrawNewFolderDialog();

            ImGui.End();
        }

        // =========================
        // TOOLBAR
        // =========================

        private void DrawToolbar()
        {
            if (ImGui.Button("+ New File"))
            {
                _showNewFileDialog = true;
                _newFileName = "NewFile";
                _selectedFileType = "Script";
                _newFileTargetFolder = _selectedFolder;
            }

            ImGui.SameLine();

            if (ImGui.Button("+ New Folder"))
            {
                _showNewFolderDialog = true;
                _newFolderName = "NewFolder";
                _newFolderTargetPath = _selectedFolder;
            }

            ImGui.SameLine();
            ImGui.Separator();
            ImGui.SameLine();

            if (ImGui.Button("Expand All"))
            {
                ExpandAll();
            }

            ImGui.SameLine();

            if (ImGui.Button("Collapse All"))
            {
                _openFolders.Clear();
            }
        }

        private void ExpandAll()
        {
            _openFolders.Clear();
            var allFolders = GetAllFolders("");
            foreach (var folder in allFolders)
            {
                _openFolders.Add(folder);
            }
        }

        // =========================
        // TREE VIEW
        // =========================

        private void DrawTree()
        {
            // Detectar click derecho en área vacía
            if (ImGui.IsWindowHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            {
                _showContextMenu = true;
                _contextMenuPos = ImGui.GetMousePos();
                _contextMenuAsset = null;
                _contextMenuFolder = "";
                _contextMenuOnEmpty = true;
            }

            // Root folder
            DrawFolderNode("", "Assets");
        }

        private void DrawFolderNode(string folderPath, string displayName)
        {
            var subfolders = GetSubFolders(folderPath);
            var assets = GetAssetsInFolder(folderPath);

            bool hasChildren = subfolders.Any() || assets.Any();
            bool isOpen = _openFolders.Contains(folderPath);

            ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.OpenOnArrow |
                                       ImGuiTreeNodeFlags.OpenOnDoubleClick |
                                       ImGuiTreeNodeFlags.SpanAvailWidth;

            if (!hasChildren)
                flags |= ImGuiTreeNodeFlags.Leaf;

            if (_selectedFolder == folderPath)
                flags |= ImGuiTreeNodeFlags.Selected;

            if (isOpen)
                flags |= ImGuiTreeNodeFlags.DefaultOpen;

            ImGui.PushID($"folder_{folderPath}");
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.85f, 0.4f, 1.0f));

            // Renombrando carpeta
            if (_isRenaming && _renamingFolderPath == folderPath)
            {
                bool nodeOpen = ImGui.TreeNodeEx("##renamingFolder", flags);

                ImGui.PopStyleColor(); // Pop del color de carpeta

                ImGui.SameLine();
                ImGui.SetKeyboardFocusHere();

                if (ImGui.InputText("##renameFolder", ref _renameBuffer, 256, ImGuiInputTextFlags.EnterReturnsTrue))
                {
                    if (!string.IsNullOrWhiteSpace(_renameBuffer))
                    {
                        AssetManager.RenameFolder(folderPath, _renameBuffer);
                        AssetManager.SaveDatabasePublic();
                    }
                    _isRenaming = false;
                    _renamingFolderPath = "";
                }

                if (ImGui.IsKeyPressed(ImGuiKey.Escape))
                {
                    _isRenaming = false;
                    _renamingFolderPath = "";
                }

                if (nodeOpen)
                {
                    // Dibujar subcarpetas y assets
                    foreach (var subfolder in subfolders)
                    {
                        string subfolderPath = string.IsNullOrEmpty(folderPath)
                            ? subfolder
                            : $"{folderPath}/{subfolder}";
                        DrawFolderNode(subfolderPath, subfolder);
                    }

                    foreach (var asset in assets)
                    {
                        DrawAssetNode(asset);
                    }

                    ImGui.TreePop();
                }
            }
            else
            {
                bool nodeOpen = ImGui.TreeNodeEx(displayName, flags);

                ImGui.PopStyleColor(); // Pop del color de carpeta

                // Manejar estado de apertura
                if (ImGui.IsItemClicked() && !ImGui.IsItemToggledOpen())
                {
                    _selectedFolder = folderPath;
                }

                if (nodeOpen && !isOpen)
                {
                    _openFolders.Add(folderPath);
                }
                else if (!nodeOpen && isOpen)
                {
                    _openFolders.Remove(folderPath);
                }

                // Context menu en carpeta
                if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                {
                    _showContextMenu = true;
                    _contextMenuPos = ImGui.GetMousePos();
                    _contextMenuFolder = folderPath;
                    _contextMenuAsset = null;
                    _contextMenuOnEmpty = false;
                }

                // Mostrar hijos si está abierto
                if (nodeOpen)
                {
                    // Dibujar subcarpetas
                    foreach (var subfolder in subfolders)
                    {
                        string subfolderPath = string.IsNullOrEmpty(folderPath)
                            ? subfolder
                            : $"{folderPath}/{subfolder}";
                        DrawFolderNode(subfolderPath, subfolder);
                    }

                    // Dibujar assets
                    foreach (var asset in assets)
                    {
                        DrawAssetNode(asset);
                    }

                    ImGui.TreePop();
                }
            }

            ImGui.PopID();
        }

        private void DrawAssetNode(AssetRecord asset)
        {
            ImGui.PushID($"asset_{asset.Guid}");

            ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.Leaf |
                                       ImGuiTreeNodeFlags.NoTreePushOnOpen |
                                       ImGuiTreeNodeFlags.SpanAvailWidth;

            if (_selectedAsset == asset.Guid)
                flags |= ImGuiTreeNodeFlags.Selected;

            // Icono según tipo
            string icon = GetAssetIcon(asset.Type);
            Vector4 iconColor = GetAssetIconColor(asset.Type);

            ImGui.PushStyleColor(ImGuiCol.Text, iconColor);

            string fileName = Path.GetFileName(asset.Path);

            // Si está renombrando
            if (_isRenaming && _renamingAssetGuid == asset.Guid)
            {
                ImGui.TreeNodeEx("", flags);
                ImGui.SameLine();
                ImGui.SetNextItemWidth(-1);
                ImGui.SetKeyboardFocusHere();

                if (ImGui.InputText("##rename", ref _renameBuffer, 256, ImGuiInputTextFlags.EnterReturnsTrue))
                {
                    if (!string.IsNullOrWhiteSpace(_renameBuffer))
                    {
                        AssetManager.RenameAsset(asset.Guid, _renameBuffer);
                        AssetManager.SaveDatabasePublic();
                    }
                    _isRenaming = false;
                    _renamingAssetGuid = null;
                }

                if (ImGui.IsKeyPressed(ImGuiKey.Escape))
                {
                    _isRenaming = false;
                    _renamingAssetGuid = null;
                }
            }
            else
            {
                ImGui.TreeNodeEx($"{icon} {fileName}", flags);
            }

            ImGui.PopStyleColor();

            // Click para seleccionar
            if (ImGui.IsItemClicked())
            {
                _selectedAsset = asset.Guid;
            }

            // Context menu
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            {
                _showContextMenu = true;
                _contextMenuPos = ImGui.GetMousePos();
                _contextMenuAsset = asset.Guid;
                _contextMenuFolder = "";
                _contextMenuOnEmpty = false;
            }

            ImGui.PopID();
        }

        // =========================
        // ASSET ICONS
        // =========================

        private string GetAssetIcon(string assetType)
        {
            return assetType switch
            {
                "Texture" => "[IMG]",
                "Model" => "[3D]",
                "Audio" => "[SND]",
                "Script" => "[JS]",
                "Material" => "[MAT]",
                "Shader" => "[SHD]",
                _ => "[FILE]"
            };
        }

        private Vector4 GetAssetIconColor(string assetType)
        {
            return assetType switch
            {
                "Texture" => new Vector4(0.5f, 0.8f, 1.0f, 1.0f),
                "Model" => new Vector4(0.8f, 0.8f, 0.8f, 1.0f),
                "Audio" => new Vector4(1.0f, 0.7f, 0.3f, 1.0f),
                "Script" => new Vector4(0.7f, 1.0f, 0.7f, 1.0f),
                "Material" => new Vector4(1.0f, 0.5f, 0.8f, 1.0f),
                "Shader" => new Vector4(1.0f, 1.0f, 0.5f, 1.0f),
                _ => new Vector4(0.7f, 0.7f, 0.7f, 1.0f)
            };
        }

        // =========================
        // DATA QUERIES
        // =========================

        private IEnumerable<string> GetAllFolders(string basePath)
        {
            var prefix = string.IsNullOrEmpty(basePath) ? "" : basePath + "/";

            // Obtener todas las carpetas registradas
            var registeredFolders = AssetManager.AllFolders()
                .Select(f => f.Path)
                .Where(p => string.IsNullOrEmpty(basePath) || p.StartsWith(prefix))
                .ToList();

            // También incluir carpetas que se deducen de los paths de assets
            var implicitFolders = AssetManager.All()
                .Select(a => a.Path)
                .Where(p => p.StartsWith(prefix))
                .Select(p => p.Substring(prefix.Length))
                .Where(p => p.Contains('/'))
                .Select(p =>
                {
                    var parts = p.Split('/');
                    return prefix + parts[0];
                })
                .Distinct()
                .ToList();

            // Combinar ambas listas
            var allFolders = registeredFolders.Union(implicitFolders).Distinct().ToList();

            var result = new List<string>(allFolders);
            foreach (var folder in allFolders)
            {
                result.AddRange(GetAllFolders(folder));
            }

            return result.Distinct();
        }

        private IEnumerable<string> GetSubFolders(string folderPath)
        {
            var prefix = string.IsNullOrEmpty(folderPath) ? "" : folderPath + "/";

            // Carpetas registradas directas
            var registeredFolders = AssetManager.AllFolders()
                .Select(f => f.Path)
                .Where(p => p.StartsWith(prefix))
                .Select(p => p.Substring(prefix.Length))
                .Where(p => !p.Contains('/')) // Solo subcarpetas directas
                .ToList();

            // Carpetas implícitas de assets
            var implicitFolders = AssetManager.All()
                .Select(a => a.Path)
                .Where(p => p.StartsWith(prefix))
                .Select(p => p.Substring(prefix.Length))
                .Where(p => p.Contains('/'))
                .Select(p => p.Split('/')[0])
                .ToList();

            return registeredFolders.Union(implicitFolders)
                .Distinct()
                .OrderBy(f => f);
        }

        private IEnumerable<AssetRecord> GetAssetsInFolder(string folderPath)
        {
            var prefix = string.IsNullOrEmpty(folderPath) ? "" : folderPath + "/";

            return AssetManager.All()
                .Where(a =>
                {
                    if (!a.Path.StartsWith(prefix))
                        return false;

                    var rest = a.Path.Substring(prefix.Length);
                    return !rest.Contains('/');
                })
                .OrderBy(a => Path.GetFileName(a.Path));
        }

        // =========================
        // CONTEXT MENU
        // =========================

        private void DrawContextMenu()
        {
            if (!_showContextMenu)
                return;

            ImGui.SetNextWindowPos(_contextMenuPos);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(8, 8));

            bool windowOpen = true;
            if (ImGui.Begin("##AssetContextMenu", ref windowOpen,
                ImGuiWindowFlags.NoTitleBar |
                ImGuiWindowFlags.NoResize |
                ImGuiWindowFlags.NoMove |
                ImGuiWindowFlags.AlwaysAutoResize |
                ImGuiWindowFlags.NoSavedSettings))
            {
                bool closeMenu = false;

                if (_contextMenuAsset.HasValue)
                {
                    // Menú para asset
                    if (ImGui.MenuItem("Rename"))
                    {
                        _isRenaming = true;
                        _renamingAssetGuid = _contextMenuAsset;
                        var asset = AssetManager.Get(_contextMenuAsset.Value);
                        if (asset != null)
                        {
                            _renameBuffer = Path.GetFileNameWithoutExtension(asset.Path);
                        }
                        closeMenu = true;
                    }

                    if (ImGui.MenuItem("Delete"))
                    {
                        AssetManager.DeleteAsset(_contextMenuAsset.Value);
                        AssetManager.SaveDatabasePublic();
                        _selectedAsset = null;
                        closeMenu = true;
                    }
                }
                else if (_contextMenuFolder != null && !_contextMenuOnEmpty)
                {
                    // Menú para carpeta
                    if (ImGui.MenuItem("New File..."))
                    {
                        _showNewFileDialog = true;
                        _newFileName = "NewFile";
                        _selectedFileType = "Script";
                        _newFileTargetFolder = _contextMenuFolder;
                        closeMenu = true;
                    }

                    if (ImGui.MenuItem("New Folder..."))
                    {
                        _showNewFolderDialog = true;
                        _newFolderName = "NewFolder";
                        _newFolderTargetPath = _contextMenuFolder;
                        closeMenu = true;
                    }

                    ImGui.Separator();

                    if (ImGui.MenuItem("Rename"))
                    {
                        _isRenaming = true;
                        _renamingFolderPath = _contextMenuFolder;
                        _renameBuffer = string.IsNullOrEmpty(_contextMenuFolder)
                            ? "Assets"
                            : _contextMenuFolder.Split('/').Last();
                        closeMenu = true;
                    }

                    if (ImGui.MenuItem("Delete"))
                    {
                        AssetManager.DeleteFolder(_contextMenuFolder);
                        AssetManager.SaveDatabasePublic();
                        closeMenu = true;
                    }
                }
                else if (_contextMenuOnEmpty)
                {
                    // Menú en área vacía
                    if (ImGui.MenuItem("New File..."))
                    {
                        _showNewFileDialog = true;
                        _newFileName = "NewFile";
                        _selectedFileType = "Script";
                        _newFileTargetFolder = _selectedFolder;
                        closeMenu = true;
                    }

                    if (ImGui.MenuItem("New Folder..."))
                    {
                        _showNewFolderDialog = true;
                        _newFolderName = "NewFolder";
                        _newFolderTargetPath = _selectedFolder;
                        closeMenu = true;
                    }
                }

                if (closeMenu)
                {
                    _showContextMenu = false;
                }

                ImGui.End();
            }

            if (!windowOpen)
            {
                _showContextMenu = false;
            }

            ImGui.PopStyleVar();

            // Cerrar si se hace click fuera del menú
            if (_showContextMenu && (ImGui.IsMouseClicked(ImGuiMouseButton.Left) || ImGui.IsMouseClicked(ImGuiMouseButton.Right)))
            {
                Vector2 mousePos = ImGui.GetMousePos();
                Vector2 menuMin = _contextMenuPos;
                Vector2 menuMax = new Vector2(_contextMenuPos.X + 200, _contextMenuPos.Y + 200); // Aproximado

                if (mousePos.X < menuMin.X || mousePos.X > menuMax.X ||
                    mousePos.Y < menuMin.Y || mousePos.Y > menuMax.Y)
                {
                    _showContextMenu = false;
                }
            }
        }

        // =========================
        // NEW FILE DIALOG
        // =========================

        private void DrawNewFileDialog()
        {
            if (!_showNewFileDialog)
                return;

            ImGui.OpenPopup("New File");

            Vector2 center = ImGui.GetMainViewport().GetCenter();
            ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
            ImGui.SetNextWindowSize(new Vector2(400, 200), ImGuiCond.Appearing);

            if (ImGui.BeginPopupModal("New File", ref _showNewFileDialog, ImGuiWindowFlags.NoResize))
            {
                ImGui.Text($"Create in: {(_newFileTargetFolder == "" ? "Assets" : _newFileTargetFolder)}");
                ImGui.Separator();
                ImGui.Spacing();

                ImGui.Text("File Name:");
                ImGui.SetNextItemWidth(-1);
                ImGui.InputText("##fileName", ref _newFileName, 256);

                ImGui.Spacing();
                ImGui.Text("File Type:");
                ImGui.SetNextItemWidth(-1);
                if (ImGui.BeginCombo("##fileType", _selectedFileType))
                {
                    foreach (var fileType in _fileTypes)
                    {
                        bool isSelected = _selectedFileType == fileType;
                        if (ImGui.Selectable(fileType, isSelected))
                        {
                            _selectedFileType = fileType;
                        }
                        if (isSelected)
                            ImGui.SetItemDefaultFocus();
                    }
                    ImGui.EndCombo();
                }

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                float buttonWidth = 120;
                float spacing = ImGui.GetStyle().ItemSpacing.X;
                float totalWidth = buttonWidth * 2 + spacing;
                float offsetX = (ImGui.GetContentRegionAvail().X - totalWidth) * 0.5f;

                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offsetX);

                if (ImGui.Button("Create", new Vector2(buttonWidth, 0)))
                {
                    CreateNewFile();
                    _showNewFileDialog = false;
                }

                ImGui.SameLine();

                if (ImGui.Button("Cancel", new Vector2(buttonWidth, 0)))
                {
                    _showNewFileDialog = false;
                }

                if (ImGui.IsKeyPressed(ImGuiKey.Enter) && !string.IsNullOrWhiteSpace(_newFileName))
                {
                    CreateNewFile();
                    _showNewFileDialog = false;
                }

                if (ImGui.IsKeyPressed(ImGuiKey.Escape))
                {
                    _showNewFileDialog = false;
                }

                ImGui.EndPopup();
            }
        }

        private void CreateNewFile()
        {
            if (string.IsNullOrWhiteSpace(_newFileName))
                return;

            string extension = GetExtensionForType(_selectedFileType);
            string fileName = _newFileName;

            if (!fileName.EndsWith(extension))
            {
                fileName += extension;
            }

            string assetPath = string.IsNullOrEmpty(_newFileTargetFolder)
                ? fileName
                : $"{_newFileTargetFolder}/{fileName}";

            string fullPath = Path.Combine(AssetManager.BasePath, assetPath);

            if (File.Exists(fullPath))
            {
                int counter = 1;
                string baseFileName = Path.GetFileNameWithoutExtension(fileName);
                do
                {
                    fileName = $"{baseFileName}_{counter}{extension}";
                    assetPath = string.IsNullOrEmpty(_newFileTargetFolder)
                        ? fileName
                        : $"{_newFileTargetFolder}/{fileName}";
                    fullPath = Path.Combine(AssetManager.BasePath, assetPath);
                    counter++;
                } while (File.Exists(fullPath));
            }

            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string initialContent = GetInitialContentForType(_selectedFileType, Path.GetFileNameWithoutExtension(fileName));
            File.WriteAllText(fullPath, initialContent);

            AssetManager.ImportAsset(fullPath, _selectedFileType);
            AssetManager.SaveDatabasePublic();

            // Expandir la carpeta donde se creó el archivo
            if (!string.IsNullOrEmpty(_newFileTargetFolder))
            {
                _openFolders.Add(_newFileTargetFolder);
            }
        }

        // =========================
        // NEW FOLDER DIALOG
        // =========================

        private void DrawNewFolderDialog()
        {
            if (!_showNewFolderDialog)
                return;

            ImGui.OpenPopup("New Folder");

            Vector2 center = ImGui.GetMainViewport().GetCenter();
            ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
            ImGui.SetNextWindowSize(new Vector2(400, 150), ImGuiCond.Appearing);

            if (ImGui.BeginPopupModal("New Folder", ref _showNewFolderDialog, ImGuiWindowFlags.NoResize))
            {
                ImGui.Text($"Create in: {(_newFolderTargetPath == "" ? "Assets" : _newFolderTargetPath)}");
                ImGui.Separator();
                ImGui.Spacing();

                ImGui.Text("Folder Name:");
                ImGui.SetNextItemWidth(-1);
                ImGui.InputText("##folderName", ref _newFolderName, 256);

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                float buttonWidth = 120;
                float spacing = ImGui.GetStyle().ItemSpacing.X;
                float totalWidth = buttonWidth * 2 + spacing;
                float offsetX = (ImGui.GetContentRegionAvail().X - totalWidth) * 0.5f;

                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offsetX);

                if (ImGui.Button("Create", new Vector2(buttonWidth, 0)))
                {
                    CreateNewFolder();
                    _showNewFolderDialog = false;
                }

                ImGui.SameLine();

                if (ImGui.Button("Cancel", new Vector2(buttonWidth, 0)))
                {
                    _showNewFolderDialog = false;
                }

                if (ImGui.IsKeyPressed(ImGuiKey.Enter) && !string.IsNullOrWhiteSpace(_newFolderName))
                {
                    CreateNewFolder();
                    _showNewFolderDialog = false;
                }

                if (ImGui.IsKeyPressed(ImGuiKey.Escape))
                {
                    _showNewFolderDialog = false;
                }

                ImGui.EndPopup();
            }
        }

        private void CreateNewFolder()
        {
            if (string.IsNullOrWhiteSpace(_newFolderName))
                return;

            AssetManager.CreateFolder(_newFolderTargetPath, _newFolderName);

            // Expandir la carpeta padre
            if (!string.IsNullOrEmpty(_newFolderTargetPath))
            {
                _openFolders.Add(_newFolderTargetPath);
            }
        }

        // =========================
        // HELPERS
        // =========================

        private string GetExtensionForType(string type)
        {
            return type switch
            {
                "Script" => ".js",
                "Texture" => ".png",
                "Model" => ".obj",
                "Audio" => ".wav",
                "Material" => ".mat",
                "Prefab" => ".prefab",
                _ => ".txt"
            };
        }

        private string GetInitialContentForType(string type, string name)
        {
            return type switch
            {
                "Script" => $@"// {name}.js
let speed = 1.0;

function OnStart() {{
    console.log(""{name} started"");
}}

function OnTick(deltaTime) {{
    // Update logic here
}}
",
                "Material" => "{\n  \"shader\": \"Standard\",\n  \"properties\": {}\n}",
                "Prefab" => "{\n  \"name\": \"" + name + "\",\n  \"components\": []\n}",
                _ => ""
            };
        }
    }
}