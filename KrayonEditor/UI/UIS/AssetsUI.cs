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
        private HashSet<string> _openFolders = new HashSet<string>();

        // --- Cached tree ---
        private FolderNode _rootNode = null;
        private bool _treeDirty = true;

        private class FolderNode
        {
            public string Path;
            public string DisplayName;
            public List<FolderNode> SubFolders = new();
            public List<AssetRecord> Assets = new();
        }

        /// <summary>
        /// Call this whenever assets are added, removed, or moved.
        /// </summary>
        public void MarkDirty() => _treeDirty = true;

        public override void OnDrawUI()
        {
            if (!_isVisible)
                return;

            // Rebuild only when needed, never every frame
            if (_treeDirty)
            {
                RebuildTree();
                _treeDirty = false;
            }

            ImGui.Begin("Assets", ref _isVisible);

            if (ImGui.BeginChild("AssetTree", new Vector2(0, 0)))
            {
                if (_rootNode != null)
                    DrawFolderNode(_rootNode);
            }
            ImGui.EndChild();

            ImGui.End();
        }

        // Single-pass O(n) tree builder — runs once, not every frame
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

            // Register explicit folders
            foreach (var folder in AssetManager.AllFolders())
                GetOrCreate(folder.Path);

            // Place each asset in its parent folder (creates implicit folders too)
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
                                                       Path.GetFileName(b.Path),
                                                       System.StringComparison.OrdinalIgnoreCase));
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

            if (ImGui.BeginDragDropSource())
            {
                string guid = asset.Guid.ToString();
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(guid);

                unsafe
                {
                    fixed (byte* ptr = bytes)
                        ImGui.SetDragDropPayload("ASSET_PATH", (IntPtr)ptr, (uint)bytes.Length);
                }

                ImGui.Text(guid);
                ImGui.EndDragDropSource();
            }

            ImGui.PopID();
        }
    }
}