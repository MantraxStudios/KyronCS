using ImGuiNET;
using KrayonCore.Core.Attributes;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;

namespace KrayonEditor.UI
{
    public class AssetsUI : UIBehaviour
    {
        private Guid? _selectedAsset = null;
        private string _selectedFolder = "";
        private HashSet<string> _openFolders = new HashSet<string>();

        private FolderNode _rootNode = null;
        private bool _treeDirty = true;

        private bool _showNewScriptPopup = false;
        private string _newScriptName = "NewScript";
        private string _newScriptFolder = "";

        private bool _showNewFolderPopup = false;
        private string _newFolderName = "NewFolder";
        private string _newFolderParent = "";

        private bool _showDeleteAssetPopup = false;
        private Guid? _assetToDelete = null;
        private string _assetToDeleteName = "";

        private bool _showDeleteFolderPopup = false;
        private string _folderToDelete = "";
        private string _folderToDeleteName = "";

        private bool _showRenameAssetPopup = false;
        private Guid? _assetToRename = null;
        private string _renameAssetNewName = "";
        private string _renameAssetExtension = "";

        private bool _showRenameFolderPopup = false;
        private string _folderToRename = "";
        private string _renameFolderNewName = "";

        private bool _showNewAnimatorPopup = false;
        private string _newAnimatorName = "NewAnimator";
        private string _newAnimatorFolder = "";

        private bool _showNewUIPopupatorPopup = false;
        private string _newUIName = "NewAnimator";
        private string _newUIFolder = "";


        private class FolderNode
        {
            public string Path;
            public string DisplayName;
            public List<FolderNode> SubFolders = new();
            public List<AssetRecord> Assets = new();
        }

        public void MarkDirty() => _treeDirty = true;

        public void HandleExternalDrop(string[] externalPaths, string targetFolder = "")
        {
            if (externalPaths == null || externalPaths.Length == 0)
                return;

            try
            {
                foreach (string externalPath in externalPaths)
                {
                    if (Directory.Exists(externalPath))
                    {
                        ImportExternalFolder(externalPath, targetFolder);
                    }
                    else if (File.Exists(externalPath))
                    {
                        ImportExternalFile(externalPath, targetFolder);
                    }
                }

                MarkDirty();
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"Error importing external drop: {ex.Message}");
            }
        }

        private void ImportExternalFile(string sourceFilePath, string targetFolder)
        {
            string fileName = Path.GetFileName(sourceFilePath);
            string relativePath = string.IsNullOrEmpty(targetFolder)
                ? fileName
                : $"{targetFolder}/{fileName}";
            string destPath = Path.Combine(AssetManager.BasePath, relativePath);

            if (File.Exists(destPath))
            {
                string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                string extension = Path.GetExtension(fileName);
                int counter = 1;

                while (File.Exists(destPath))
                {
                    fileName = $"{nameWithoutExt}_{counter}{extension}";
                    relativePath = string.IsNullOrEmpty(targetFolder)
                        ? fileName
                        : $"{targetFolder}/{fileName}";
                    destPath = Path.Combine(AssetManager.BasePath, relativePath);
                    counter++;
                }
            }

            string directory = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            File.Copy(sourceFilePath, destPath, false);

            AssetManager.Import(relativePath);

            System.Console.WriteLine($"Imported file: {relativePath}");
        }

        private void ImportExternalFolder(string sourceFolderPath, string targetFolder)
        {
            string folderName = new DirectoryInfo(sourceFolderPath).Name;
            string relativePath = string.IsNullOrEmpty(targetFolder)
                ? folderName
                : $"{targetFolder}/{folderName}";
            string destPath = Path.Combine(AssetManager.BasePath, relativePath);

            if (Directory.Exists(destPath))
            {
                int counter = 1;
                while (Directory.Exists(destPath))
                {
                    folderName = $"{new DirectoryInfo(sourceFolderPath).Name}_{counter}";
                    relativePath = string.IsNullOrEmpty(targetFolder)
                        ? folderName
                        : $"{targetFolder}/{folderName}";
                    destPath = Path.Combine(AssetManager.BasePath, relativePath);
                    counter++;
                }
            }

            CopyDirectory(sourceFolderPath, destPath);

            AssetManager.CreateFolder(targetFolder, folderName);

            ImportFolderContents(relativePath);

            System.Console.WriteLine($"Imported folder: {relativePath}");
        }

        private void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);

            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string fileName = Path.GetFileName(file);
                string destFile = Path.Combine(destDir, fileName);
                File.Copy(file, destFile, false);
            }

            foreach (string dir in Directory.GetDirectories(sourceDir))
            {
                string dirName = new DirectoryInfo(dir).Name;
                string destSubDir = Path.Combine(destDir, dirName);
                CopyDirectory(dir, destSubDir);
            }
        }

        private void ImportFolderContents(string folderPath)
        {
            string fullPath = Path.Combine(AssetManager.BasePath, folderPath);

            foreach (string file in Directory.GetFiles(fullPath))
            {
                string fileName = Path.GetFileName(file);
                string relativePath = $"{folderPath}/{fileName}";
                AssetManager.Import(relativePath);
            }

            foreach (string dir in Directory.GetDirectories(fullPath))
            {
                string dirName = new DirectoryInfo(dir).Name;
                string subFolderPath = $"{folderPath}/{dirName}";

                AssetManager.CreateFolder(folderPath, dirName);

                ImportFolderContents(subFolderPath);
            }
        }

        public override void OnDrawUI()
        {
            if (!_isVisible)
                return;

            if (_treeDirty)
            {
                RebuildTree();
                _treeDirty = false;
            }

            ImGui.Begin("Assets", ref _isVisible);

            if (ImGui.BeginChild("AssetTree", new Vector2(0, 0)))
            {
                if (ImGui.BeginDragDropTarget())
                {
                    var payload = ImGui.AcceptDragDropPayload("EXTERNAL_FILE");
                    unsafe
                    {
                        if (payload.NativePtr != null)
                        {
                            byte[] data = new byte[payload.DataSize];
                            Marshal.Copy(payload.Data, data, 0, payload.DataSize);
                            string pathsData = System.Text.Encoding.UTF8.GetString(data);

                            string[] paths = pathsData.Split(new[] { '\n', '\r' },
                                System.StringSplitOptions.RemoveEmptyEntries);

                            HandleExternalDrop(paths, _selectedFolder);
                        }
                    }

                    ImGui.EndDragDropTarget();
                }

                if (_rootNode != null)
                    DrawFolderNode(_rootNode);
            }
            ImGui.EndChild();

            DrawNewScriptPopup();
            DrawNewAnimatorPopup();
            DrawNewFolderPopup();
            DrawDeleteAssetPopup();
            DrawDeleteFolderPopup();
            DrawRenameAssetPopup();
            DrawRenameFolderPopup();

            ImGui.End();
        }

        private void RebuildTree()
        {
            var nodes = new Dictionary<string, FolderNode>();
            _rootNode = new FolderNode { Path = "", DisplayName = "Content" };
            nodes[""] = _rootNode;

            FolderNode GetOrCreate(string path)
            {
                if (nodes.TryGetValue(path, out var existing))
                    return existing;

                int lastSlash = path.LastIndexOf('/');
                string parentPath = lastSlash < 0 ? "" : path.Substring(0, lastSlash);
                string name = lastSlash < 0 ? path : path.Substring(lastSlash + 1);

                var parent = GetOrCreate(parentPath);
                var node = new FolderNode { Path = path, DisplayName = name };
                nodes[path] = node;
                parent.SubFolders.Add(node);
                return node;
            }

            foreach (var folder in AssetManager.AllFolders())
                GetOrCreate(folder.Path);

            foreach (var asset in AssetManager.All())
            {
                int lastSlash = asset.Path.LastIndexOf('/');
                string folderPath = lastSlash < 0 ? "" : asset.Path.Substring(0, lastSlash);
                GetOrCreate(folderPath).Assets.Add(asset);
            }

            SortNode(_rootNode);
        }

        private void SortNode(FolderNode node)
        {
            node.SubFolders.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName,
                System.StringComparison.OrdinalIgnoreCase));
            node.Assets.Sort((a, b) => string.Compare(Path.GetFileName(a.Path),
                Path.GetFileName(b.Path), System.StringComparison.OrdinalIgnoreCase));
            foreach (var child in node.SubFolders)
                SortNode(child);
        }

        private void DrawFolderNode(FolderNode node)
        {
            bool hasChildren = node.SubFolders.Count > 0 || node.Assets.Count > 0;
            bool isOpen = _openFolders.Contains(node.Path);

            ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.OpenOnArrow |
                                       ImGuiTreeNodeFlags.OpenOnDoubleClick |
                                       ImGuiTreeNodeFlags.SpanAvailWidth;

            if (!hasChildren)
                flags |= ImGuiTreeNodeFlags.Leaf;

            if (_selectedFolder == node.Path)
                flags |= ImGuiTreeNodeFlags.Selected;

            if (isOpen)
                flags |= ImGuiTreeNodeFlags.DefaultOpen;

            ImGui.PushID(node.Path);

            bool nodeOpen = ImGui.TreeNodeEx(node.DisplayName, flags);

            if (ImGui.IsItemClicked() && !ImGui.IsItemToggledOpen())
                _selectedFolder = node.Path;

            if (ImGui.BeginPopupContextItem($"FolderCtx_{node.Path}"))
            {
                if (ImGui.MenuItem("New Script"))
                {
                    _newScriptFolder = node.Path;
                    _newScriptName = "NewScript";
                    _showNewScriptPopup = true;
                }

                if (ImGui.MenuItem("New Folder"))
                {
                    _newFolderParent = node.Path;
                    _newFolderName = "NewFolder";
                    _showNewFolderPopup = true;
                }

                if (ImGui.MenuItem("New Animator Controller"))
                {
                    _newAnimatorFolder = node.Path;
                    _newAnimatorName = "NewAnimator";
                    _showNewAnimatorPopup = true;
                }

                if (ImGui.MenuItem("New UI Canvas"))
                {
                    _newUIFolder = node.Path;
                    _newUIName = "NewCanvas";
                    _showNewUIPopupatorPopup = true;
                }

                if (!string.IsNullOrEmpty(node.Path))
                {
                    ImGui.Separator();

                    if (ImGui.MenuItem("Rename"))
                    {
                        _folderToRename = node.Path;
                        _renameFolderNewName = node.DisplayName;
                        _showRenameFolderPopup = true;
                    }

                    if (ImGui.MenuItem("Delete"))
                    {
                        _folderToDelete = node.Path;
                        _folderToDeleteName = node.DisplayName;
                        _showDeleteFolderPopup = true;
                    }
                }

                ImGui.EndPopup();
            }

            if (ImGui.BeginDragDropTarget())
            {
                var payload = ImGui.AcceptDragDropPayload("ASSET_PATH");
                unsafe
                {
                    if (payload.NativePtr != null)
                    {
                        byte[] data = new byte[payload.DataSize];
                        Marshal.Copy(payload.Data, data, 0, payload.DataSize);
                        string guidStr = System.Text.Encoding.UTF8.GetString(data);

                        if (Guid.TryParse(guidStr, out Guid assetGuid))
                        {
                            AssetManager.MoveAsset(assetGuid, node.Path);
                            MarkDirty();
                        }
                    }
                }

                var folderPayload = ImGui.AcceptDragDropPayload("FOLDER_PATH");
                unsafe
                {
                    if (folderPayload.NativePtr != null)
                    {
                        byte[] data = new byte[folderPayload.DataSize];
                        Marshal.Copy(folderPayload.Data, data, 0, folderPayload.DataSize);
                        string sourceFolderPath = System.Text.Encoding.UTF8.GetString(data);

                        if (sourceFolderPath != node.Path && !node.Path.StartsWith(sourceFolderPath + "/"))
                        {
                            AssetManager.MoveFolder(sourceFolderPath, node.Path);
                            MarkDirty();
                        }
                    }
                }

                var externalPayload = ImGui.AcceptDragDropPayload("EXTERNAL_FILE");
                unsafe
                {
                    if (externalPayload.NativePtr != null)
                    {
                        byte[] data = new byte[externalPayload.DataSize];
                        Marshal.Copy(externalPayload.Data, data, 0, externalPayload.DataSize);
                        string pathsData = System.Text.Encoding.UTF8.GetString(data);

                        string[] paths = pathsData.Split(new[] { '\n', '\r' },
                            System.StringSplitOptions.RemoveEmptyEntries);

                        HandleExternalDrop(paths, node.Path);
                    }
                }

                ImGui.EndDragDropTarget();
            }

            if (!string.IsNullOrEmpty(node.Path) && ImGui.BeginDragDropSource())
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(node.Path);
                unsafe
                {
                    fixed (byte* ptr = bytes)
                        ImGui.SetDragDropPayload("FOLDER_PATH", (IntPtr)ptr, (uint)bytes.Length);
                }
                ImGui.Text(node.DisplayName);
                ImGui.EndDragDropSource();
            }

            if (nodeOpen && !isOpen) _openFolders.Add(node.Path);
            else if (!nodeOpen && isOpen) _openFolders.Remove(node.Path);

            if (nodeOpen)
            {
                foreach (var subfolder in node.SubFolders)
                    DrawFolderNode(subfolder);

                foreach (var asset in node.Assets)
                    DrawAssetNode(asset);

                ImGui.TreePop();
            }

            ImGui.PopID();
        }

        private void DrawNewAnimatorPopup()
        {
            if (_showNewAnimatorPopup)
            {
                ImGui.OpenPopup("New Animator Controller##Popup");
                _showNewAnimatorPopup = false;
            }

            var viewport = ImGui.GetMainViewport();
            ImGui.SetNextWindowPos(viewport.GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
            ImGui.SetNextWindowSize(new Vector2(350, 0), ImGuiCond.Appearing);

            bool popupOpen = true;
            if (ImGui.BeginPopupModal("New Animator Controller##Popup", ref popupOpen,
                ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove))
            {
                ImGui.Text("Animator Controller name:");
                ImGui.SetNextItemWidth(-1);

                if (ImGui.IsWindowAppearing())
                    ImGui.SetKeyboardFocusHere();

                bool enterPressed = ImGui.InputText("##AnimatorName", ref _newAnimatorName, 128,
                    ImGuiInputTextFlags.EnterReturnsTrue);

                bool validName = !string.IsNullOrWhiteSpace(_newAnimatorName) &&
                                 _newAnimatorName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

                string fileName = _newAnimatorName + ".animator";
                string previewPath = string.IsNullOrEmpty(_newAnimatorFolder)
                    ? fileName
                    : $"{_newAnimatorFolder}/{fileName}";
                string fullPath = Path.Combine(AssetManager.BasePath, previewPath);
                bool alreadyExists = File.Exists(fullPath);

                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.6f, 0.6f, 0.6f, 1f));
                ImGui.Text($"Path: {previewPath}");
                ImGui.PopStyleColor();

                if (alreadyExists)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.4f, 0.4f, 1f));
                    ImGui.Text("A file with this name already exists.");
                    ImGui.PopStyleColor();
                }

                if (!validName)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.6f, 0.2f, 1f));
                    ImGui.Text("Invalid file name.");
                    ImGui.PopStyleColor();
                }

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                bool canCreate = validName && !alreadyExists;

                if (!canCreate) ImGui.BeginDisabled();
                if (ImGui.Button("Create", new Vector2(120, 0)) || (enterPressed && canCreate))
                {
                    CreateNewAnimator(previewPath, fullPath);
                    ImGui.CloseCurrentPopup();
                }
                if (!canCreate) ImGui.EndDisabled();

                ImGui.SameLine();

                if (ImGui.Button("Cancel", new Vector2(120, 0)))
                    ImGui.CloseCurrentPopup();

                ImGui.EndPopup();
            }
        }

        private void CreateNewAnimator(string relativePath, string fullPath)
        {
            try
            {
                string directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                // JSON vacío pero válido según AnimatorControllerData
                string content = System.Text.Json.JsonSerializer.Serialize(
                    new KrayonCore.Animation.AnimatorControllerData
                    {
                        Name = _newAnimatorName,
                        DefaultState = "",
                        Parameters = new(),
                        States = new()
                    },
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

                File.WriteAllText(fullPath, content);

                AssetManager.Import(relativePath);
                MarkDirty();

                System.Console.WriteLine($"Animator Controller created: {relativePath}");
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"Error creating animator: {ex.Message}");
            }
        }
        private void DrawAssetNode(AssetRecord asset)
        {
            ImGui.PushID(asset.Guid.ToString());

            ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.Leaf |
                                       ImGuiTreeNodeFlags.NoTreePushOnOpen |
                                       ImGuiTreeNodeFlags.SpanAvailWidth;

            if (_selectedAsset == asset.Guid)
                flags |= ImGuiTreeNodeFlags.Selected;

            ImGui.TreeNodeEx(Path.GetFileName(asset.Path), flags);

            if (ImGui.IsItemClicked())
                _selectedAsset = asset.Guid;

            if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
            {
                string ext = Path.GetExtension(asset.Path)?.ToLowerInvariant();

                if (ext == ".cs")
                {
                    string fullFilePath = Path.GetFullPath(Path.Combine(AssetManager.BasePath, asset.Path));
                    if (File.Exists(fullFilePath))
                        OpenVSCodeProject(fullFilePath);
                    else
                        OpenVSCodeProject();
                }
                else if (ext == ".animator") 
                {
                    EditorUI._animatorEditor.OpenAsset(asset.Guid);
                }
                else if (ext == ".ui")
                {
                    EditorUI._uiCanvasEditor.OpenAsset(asset.Guid);
                }
            }

            if (ImGui.BeginPopupContextItem($"AssetCtx_{asset.Guid}"))
            {
                string ext = Path.GetExtension(asset.Path)?.ToLowerInvariant();

                if (ext == ".cs" && ImGui.MenuItem("Open Project"))
                {
                    OpenVSCodeProject();
                }

                if (ext == ".animator" && ImGui.MenuItem("Open Animator Editor"))  
                {
                    EditorUI._animatorEditor.OpenAsset(asset.Guid);
                }

                if (ext == ".ui" && ImGui.MenuItem("Open UI Editor"))
                {
                    EditorUI._uiCanvasEditor.OpenAsset(asset.Guid);
                }

                if (ext == ".cs" || ext == ".animator")
                    ImGui.Separator();

                if (ImGui.MenuItem("Rename"))
                {
                    _assetToRename = asset.Guid;
                    _renameAssetExtension = Path.GetExtension(asset.Path);
                    _renameAssetNewName = Path.GetFileNameWithoutExtension(asset.Path);
                    _showRenameAssetPopup = true;
                }

                if (ImGui.MenuItem("Delete"))
                {
                    _assetToDelete = asset.Guid;
                    _assetToDeleteName = Path.GetFileName(asset.Path);
                    _showDeleteAssetPopup = true;
                }

                ImGui.EndPopup();
            }

            if (ImGui.BeginDragDropSource())
            {
                string guid = asset.Guid.ToString();
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(guid);

                unsafe
                {
                    fixed (byte* ptr = bytes)
                        ImGui.SetDragDropPayload("ASSET_PATH", (IntPtr)ptr, (uint)bytes.Length);
                }

                ImGui.Text(Path.GetFileName(asset.Path));
                ImGui.EndDragDropSource();
            }

            ImGui.PopID();
        }


        private static readonly string DefaultScriptTemplate =
@"using KrayonCore;
using OpenTK.Mathematics;

public class {SCRIPT_NAME} : KrayonBehaviour
{
    public override void Start()
    {
        
    }

    public override void Update(float deltaTime)
    {
        
    }

    public override void OnDestroy()
    {
        
    }
}
";

        private void DrawNewScriptPopup()
        {
            if (_showNewScriptPopup)
            {
                ImGui.OpenPopup("New Script##Popup");
                _showNewScriptPopup = false;
            }

            var viewport = ImGui.GetMainViewport();
            ImGui.SetNextWindowPos(viewport.GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
            ImGui.SetNextWindowSize(new Vector2(350, 0), ImGuiCond.Appearing);

            bool popupOpen = true;
            if (ImGui.BeginPopupModal("New Script##Popup", ref popupOpen,
                ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove))
            {
                ImGui.Text("Script name:");
                ImGui.SetNextItemWidth(-1);

                if (ImGui.IsWindowAppearing())
                    ImGui.SetKeyboardFocusHere();

                bool enterPressed = ImGui.InputText("##ScriptName", ref _newScriptName, 128,
                    ImGuiInputTextFlags.EnterReturnsTrue);

                bool validName = !string.IsNullOrWhiteSpace(_newScriptName) &&
                                 _newScriptName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

                string previewPath = string.IsNullOrEmpty(_newScriptFolder)
                    ? $"{_newScriptName}.cs"
                    : $"{_newScriptFolder}/{_newScriptName}.cs";
                string fullPath = Path.Combine(AssetManager.BasePath, previewPath);
                bool alreadyExists = File.Exists(fullPath);

                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.6f, 0.6f, 0.6f, 1f));
                ImGui.Text($"Path: {previewPath}");
                ImGui.PopStyleColor();

                if (alreadyExists)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.4f, 0.4f, 1f));
                    ImGui.Text("A file with this name already exists.");
                    ImGui.PopStyleColor();
                }

                if (!validName)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.6f, 0.2f, 1f));
                    ImGui.Text("Invalid file name.");
                    ImGui.PopStyleColor();
                }

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                bool canCreate = validName && !alreadyExists;

                if (!canCreate) ImGui.BeginDisabled();
                if (ImGui.Button("Create", new Vector2(120, 0)) || (enterPressed && canCreate))
                {
                    CreateNewScript(previewPath, fullPath);
                    ImGui.CloseCurrentPopup();
                }
                if (!canCreate) ImGui.EndDisabled();

                ImGui.SameLine();

                if (ImGui.Button("Cancel", new Vector2(120, 0)))
                    ImGui.CloseCurrentPopup();

                ImGui.EndPopup();
            }
        }

        private void CreateNewScript(string relativePath, string fullPath)
        {
            try
            {
                string directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                string content = DefaultScriptTemplate.Replace("{SCRIPT_NAME}", _newScriptName);
                File.WriteAllText(fullPath, content);

                AssetManager.Import(relativePath);
                MarkDirty();

                OpenVSCodeProject(fullPath);
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"Error creating script: {ex.Message}");
            }
        }

        private void DrawNewFolderPopup()
        {
            if (_showNewFolderPopup)
            {
                ImGui.OpenPopup("New Folder##Popup");
                _showNewFolderPopup = false;
            }

            var viewport = ImGui.GetMainViewport();
            ImGui.SetNextWindowPos(viewport.GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
            ImGui.SetNextWindowSize(new Vector2(350, 0), ImGuiCond.Appearing);

            bool popupOpen = true;
            if (ImGui.BeginPopupModal("New Folder##Popup", ref popupOpen,
                ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove))
            {
                ImGui.Text("Folder name:");
                ImGui.SetNextItemWidth(-1);

                if (ImGui.IsWindowAppearing())
                    ImGui.SetKeyboardFocusHere();

                bool enterPressed = ImGui.InputText("##FolderName", ref _newFolderName, 128,
                    ImGuiInputTextFlags.EnterReturnsTrue);

                bool validName = !string.IsNullOrWhiteSpace(_newFolderName) &&
                                 _newFolderName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

                string previewPath = string.IsNullOrEmpty(_newFolderParent)
                    ? _newFolderName
                    : $"{_newFolderParent}/{_newFolderName}";
                string fullPath = Path.Combine(AssetManager.BasePath, previewPath);
                bool alreadyExists = Directory.Exists(fullPath);

                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.6f, 0.6f, 0.6f, 1f));
                ImGui.Text($"Path: {previewPath}");
                ImGui.PopStyleColor();

                if (alreadyExists)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.4f, 0.4f, 1f));
                    ImGui.Text("A folder with this name already exists.");
                    ImGui.PopStyleColor();
                }

                if (!validName)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.6f, 0.2f, 1f));
                    ImGui.Text("Invalid folder name.");
                    ImGui.PopStyleColor();
                }

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                bool canCreate = validName && !alreadyExists;

                if (!canCreate) ImGui.BeginDisabled();
                if (ImGui.Button("Create", new Vector2(120, 0)) || (enterPressed && canCreate))
                {
                    AssetManager.CreateFolder(_newFolderParent, _newFolderName);
                    MarkDirty();
                    ImGui.CloseCurrentPopup();
                }
                if (!canCreate) ImGui.EndDisabled();

                ImGui.SameLine();

                if (ImGui.Button("Cancel", new Vector2(120, 0)))
                    ImGui.CloseCurrentPopup();

                ImGui.EndPopup();
            }
        }

        private void DrawRenameAssetPopup()
        {
            if (_showRenameAssetPopup)
            {
                ImGui.OpenPopup("Rename Asset##Popup");
                _showRenameAssetPopup = false;
            }

            var viewport = ImGui.GetMainViewport();
            ImGui.SetNextWindowPos(viewport.GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
            ImGui.SetNextWindowSize(new Vector2(350, 0), ImGuiCond.Appearing);

            bool popupOpen = true;
            if (ImGui.BeginPopupModal("Rename Asset##Popup", ref popupOpen,
                ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove))
            {
                ImGui.Text("New name:");
                ImGui.SetNextItemWidth(-1);

                if (ImGui.IsWindowAppearing())
                    ImGui.SetKeyboardFocusHere();

                bool enterPressed = ImGui.InputText("##RenameAsset", ref _renameAssetNewName, 128,
                    ImGuiInputTextFlags.EnterReturnsTrue);

                bool validName = !string.IsNullOrWhiteSpace(_renameAssetNewName) &&
                                 _renameAssetNewName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

                string finalName = _renameAssetNewName + _renameAssetExtension;

                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.6f, 0.6f, 0.6f, 1f));
                ImGui.Text($"Final: {finalName}");
                ImGui.PopStyleColor();

                bool nameConflict = false;
                if (_assetToRename.HasValue && validName)
                {
                    var asset = AssetManager.Get(_assetToRename.Value);
                    if (asset != null)
                    {
                        string dir = Path.GetDirectoryName(asset.Path)?.Replace("\\", "/") ?? "";
                        string newRelPath = string.IsNullOrEmpty(dir)
                            ? finalName
                            : $"{dir}/{finalName}";
                        string newFullPath = Path.Combine(AssetManager.BasePath, newRelPath);
                        nameConflict = File.Exists(newFullPath) && newRelPath != asset.Path;
                    }
                }

                if (nameConflict)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.4f, 0.4f, 1f));
                    ImGui.Text("A file with this name already exists.");
                    ImGui.PopStyleColor();
                }

                if (!validName)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.6f, 0.2f, 1f));
                    ImGui.Text("Invalid name.");
                    ImGui.PopStyleColor();
                }

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                bool canRename = validName && !nameConflict;

                if (!canRename) ImGui.BeginDisabled();
                if (ImGui.Button("Rename", new Vector2(120, 0)) || (enterPressed && canRename))
                {
                    if (_assetToRename.HasValue)
                    {
                        AssetManager.RenameAsset(_assetToRename.Value, finalName);
                        MarkDirty();
                    }
                    ImGui.CloseCurrentPopup();
                }
                if (!canRename) ImGui.EndDisabled();

                ImGui.SameLine();

                if (ImGui.Button("Cancel", new Vector2(120, 0)))
                    ImGui.CloseCurrentPopup();

                ImGui.EndPopup();
            }
        }

        private void DrawRenameFolderPopup()
        {
            if (_showRenameFolderPopup)
            {
                ImGui.OpenPopup("Rename Folder##Popup");
                _showRenameFolderPopup = false;
            }

            var viewport = ImGui.GetMainViewport();
            ImGui.SetNextWindowPos(viewport.GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
            ImGui.SetNextWindowSize(new Vector2(350, 0), ImGuiCond.Appearing);

            bool popupOpen = true;
            if (ImGui.BeginPopupModal("Rename Folder##Popup", ref popupOpen,
                ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove))
            {
                ImGui.Text("New name:");
                ImGui.SetNextItemWidth(-1);

                if (ImGui.IsWindowAppearing())
                    ImGui.SetKeyboardFocusHere();

                bool enterPressed = ImGui.InputText("##RenameFolder", ref _renameFolderNewName, 128,
                    ImGuiInputTextFlags.EnterReturnsTrue);

                bool validName = !string.IsNullOrWhiteSpace(_renameFolderNewName) &&
                                 _renameFolderNewName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

                string parentPath = "";
                if (_folderToRename.Contains('/'))
                    parentPath = string.Join("/", _folderToRename.Split('/').SkipLast(1));

                string previewPath = string.IsNullOrEmpty(parentPath)
                    ? _renameFolderNewName
                    : $"{parentPath}/{_renameFolderNewName}";

                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.6f, 0.6f, 0.6f, 1f));
                ImGui.Text($"Path: {previewPath}");
                ImGui.PopStyleColor();

                string newFullPath = Path.Combine(AssetManager.BasePath, previewPath);
                bool alreadyExists = Directory.Exists(newFullPath) && previewPath != _folderToRename;

                if (alreadyExists)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.4f, 0.4f, 1f));
                    ImGui.Text("A folder with this name already exists.");
                    ImGui.PopStyleColor();
                }

                if (!validName)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.6f, 0.2f, 1f));
                    ImGui.Text("Invalid name.");
                    ImGui.PopStyleColor();
                }

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                bool canRename = validName && !alreadyExists;

                if (!canRename) ImGui.BeginDisabled();
                if (ImGui.Button("Rename", new Vector2(120, 0)) || (enterPressed && canRename))
                {
                    if (!string.IsNullOrEmpty(_folderToRename))
                    {
                        AssetManager.RenameFolder(_folderToRename, _renameFolderNewName);
                        if (_selectedFolder == _folderToRename)
                            _selectedFolder = previewPath;
                        MarkDirty();
                    }
                    ImGui.CloseCurrentPopup();
                }
                if (!canRename) ImGui.EndDisabled();

                ImGui.SameLine();

                if (ImGui.Button("Cancel", new Vector2(120, 0)))
                    ImGui.CloseCurrentPopup();

                ImGui.EndPopup();
            }
        }

        private void DrawDeleteAssetPopup()
        {
            if (_showDeleteAssetPopup)
            {
                ImGui.OpenPopup("Delete Asset##Popup");
                _showDeleteAssetPopup = false;
            }

            var viewport = ImGui.GetMainViewport();
            ImGui.SetNextWindowPos(viewport.GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

            bool popupOpen = true;
            if (ImGui.BeginPopupModal("Delete Asset##Popup", ref popupOpen,
                ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove))
            {
                ImGui.Text("Are you sure you want to delete:");

                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.8f, 0.3f, 1f));
                ImGui.Text($"  {_assetToDeleteName}");
                ImGui.PopStyleColor();

                ImGui.Text("This action cannot be undone.");

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.8f, 0.2f, 0.2f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.9f, 0.3f, 0.3f, 1f));
                if (ImGui.Button("Delete", new Vector2(120, 0)))
                {
                    if (_assetToDelete.HasValue)
                    {
                        AssetManager.DeleteAsset(_assetToDelete.Value);
                        if (_selectedAsset == _assetToDelete)
                            _selectedAsset = null;
                        MarkDirty();
                    }
                    ImGui.CloseCurrentPopup();
                }
                ImGui.PopStyleColor(2);

                ImGui.SameLine();

                if (ImGui.Button("Cancel", new Vector2(120, 0)))
                    ImGui.CloseCurrentPopup();

                ImGui.EndPopup();
            }
        }

        private void DrawDeleteFolderPopup()
        {
            if (_showDeleteFolderPopup)
            {
                ImGui.OpenPopup("Delete Folder##Popup");
                _showDeleteFolderPopup = false;
            }

            var viewport = ImGui.GetMainViewport();
            ImGui.SetNextWindowPos(viewport.GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

            bool popupOpen = true;
            if (ImGui.BeginPopupModal("Delete Folder##Popup", ref popupOpen,
                ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove))
            {
                ImGui.Text("Are you sure you want to delete folder:");

                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.8f, 0.3f, 1f));
                ImGui.Text($"  {_folderToDeleteName}");
                ImGui.PopStyleColor();

                ImGui.Text("All contents will be permanently deleted.");

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.8f, 0.2f, 0.2f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.9f, 0.3f, 0.3f, 1f));
                if (ImGui.Button("Delete", new Vector2(120, 0)))
                {
                    if (!string.IsNullOrEmpty(_folderToDelete))
                    {
                        AssetManager.DeleteFolder(_folderToDelete);
                        if (_selectedFolder == _folderToDelete)
                            _selectedFolder = "";
                        MarkDirty();
                    }
                    ImGui.CloseCurrentPopup();
                }
                ImGui.PopStyleColor(2);

                ImGui.SameLine();

                if (ImGui.Button("Cancel", new Vector2(120, 0)))
                    ImGui.CloseCurrentPopup();

                ImGui.EndPopup();
            }
        }

        private static void OpenVSCodeProject(string filePath = null)
        {
            string codePath = FindVSCodePath();

            if (codePath != null)
            {
                try
                {
                    string args = $"\"{AssetManager.VSProyect}\"";
                    if (!string.IsNullOrEmpty(filePath))
                        args += $" \"{filePath}\"";

                    Process.Start(new ProcessStartInfo
                    {
                        FileName = codePath,
                        Arguments = args,
                        UseShellExecute = false
                    });
                }
                catch (System.Exception ex)
                {
                    System.Console.WriteLine($"Error opening VSCode: {ex.Message}");
                    OpenVSCodeDownloadPage();
                }
            }
            else
            {
                OpenVSCodeDownloadPage();
            }
        }

        private static string FindVSCodePath()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string[] windowsPaths = new[]
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "Programs", "Microsoft VS Code", "Code.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                        "Microsoft VS Code", "Code.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                        "Microsoft VS Code", "Code.exe"),
                };

                foreach (var p in windowsPaths)
                    if (File.Exists(p)) return p;

                return FindInPath("code.cmd") ?? FindInPath("code.exe");
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                string macPath = "/Applications/Visual Studio Code.app/Contents/Resources/app/bin/code";
                if (File.Exists(macPath)) return macPath;
                return FindInPath("code");
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return FindInPath("code");

            return null;
        }

        private static string FindInPath(string executable)
        {
            string pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(pathEnv)) return null;

            char separator = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ';' : ':';

            foreach (string dir in pathEnv.Split(separator))
            {
                string fullPath = Path.Combine(dir.Trim(), executable);
                if (File.Exists(fullPath)) return fullPath;
            }

            return null;
        }

        private static void OpenVSCodeDownloadPage()
        {
            string url = "https://code.visualstudio.com/Download";

            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    Process.Start("open", url);
                else
                    Process.Start("xdg-open", url);
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"Could not open browser: {ex.Message}");
            }
        }
    }
}