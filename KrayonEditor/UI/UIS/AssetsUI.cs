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

        public override void OnDrawUI()
        {
            if (!_isVisible)
                return;

            ImGui.Begin("Assets", ref _isVisible);

            if (ImGui.BeginChild("AssetTree", new Vector2(0, 0)))
            {
                DrawTree();
            }
            ImGui.EndChild();

            ImGui.End();
        }

        private void DrawTree()
        {
            DrawFolderNode("", "Content");
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

            bool nodeOpen = ImGui.TreeNodeEx(displayName, flags);

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

            if (nodeOpen)
            {
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

            string fileName = Path.GetFileName(asset.Path);
            ImGui.TreeNodeEx(fileName, flags);

            if (ImGui.IsItemClicked())
            {
                _selectedAsset = asset.Guid;
            }

            if (ImGui.BeginDragDropSource())
            {
                string relativePath = "/" + asset.Path;
                byte[] pathBytes = System.Text.Encoding.UTF8.GetBytes(relativePath);
                
                unsafe
                {
                    fixed (byte* ptr = pathBytes)
                    {
                        ImGui.SetDragDropPayload("ASSET_PATH", (IntPtr)ptr, (uint)pathBytes.Length);
                    }
                }
                
                ImGui.Text(relativePath);
                ImGui.EndDragDropSource();
            }

            ImGui.PopID();
        }

        private IEnumerable<string> GetSubFolders(string folderPath)
        {
            var prefix = string.IsNullOrEmpty(folderPath) ? "" : folderPath + "/";

            var registeredFolders = AssetManager.AllFolders()
                .Select(f => f.Path)
                .Where(p => p.StartsWith(prefix))
                .Select(p => p.Substring(prefix.Length))
                .Where(p => !p.Contains('/'))
                .ToList();

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
    }
}