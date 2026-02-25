using ImGuiNET;
using KrayonCore;
using KrayonCore.Components;
using KrayonEditor.Main;
using KrayonEditor.Utilities;
using System;
using System.Runtime.InteropServices;

namespace KrayonEditor.UI
{
    public class HierarchyUI : UIBehaviour
    {
        private const string DRAG_DROP_PAYLOAD_TYPE = "GAMEOBJECT_HIERARCHY";

        private struct DragPayload
        {
            public Guid ObjectId;
        }

        private GameObject _renamingObject = null;
        private string _renameBuffer = string.Empty;
        private bool _focusRenameField = false;

        public override void OnDrawUI()
        {
            if (!_isVisible) return;

            ImGui.Begin("Hierarchy", ref _isVisible);

            if (ImGui.Button("+ Create Empty"))
                EditorActions.CreateEmptyGameObject();

            ImGui.SameLine();

            if (ImGui.Button("+ Create Cube"))
                EditorActions.CreateCubeGameObject();

            ImGui.Separator();

            var primaryScene = SceneManager.PrimaryScene;

            if (primaryScene == null)
            {
                ImGui.TextDisabled("No active scene.");
            }
            else
            {
                DrawSceneNode(primaryScene);
            }

            if (ImGui.BeginPopupContextWindow("hierarchy_context", ImGuiPopupFlags.MouseButtonRight | ImGuiPopupFlags.NoOpenOverItems))
            {
                if (ImGui.BeginMenu("Light"))
                {
                    if (ImGui.MenuItem("Directional Light")) EditorActions.CreateDirectionalLight();
                    if (ImGui.MenuItem("Point Light")) EditorActions.CreatePointLight();
                    if (ImGui.MenuItem("Spot Light")) EditorActions.CreateSpotLight();
                    ImGui.EndMenu();
                }

                if (ImGui.MenuItem("Empty GameObject")) EditorActions.CreateEmptyGameObject();
                if (ImGui.MenuItem("Model")) EditorActions.CreateModelGameObject();
                if (ImGui.MenuItem("TileRenderer")) EditorActions.CreateTileRendererGameObject();
                if (ImGui.MenuItem("New Camera")) EditorActions.CreateCamera();

                ImGui.EndPopup();
            }

            ImGui.End();
        }

        private void DrawSceneNode(GameScene scene)
        {
            ImGuiTreeNodeFlags sceneFlags =
                ImGuiTreeNodeFlags.DefaultOpen |
                ImGuiTreeNodeFlags.OpenOnArrow |
                ImGuiTreeNodeFlags.OpenOnDoubleClick |
                ImGuiTreeNodeFlags.SpanAvailWidth;

            bool sceneOpen = ImGui.TreeNodeEx(
                $"{scene.Name} {(EditorActions.IsDirty ? "*" : string.Empty)} ##{scene.GetHashCode()}",
                sceneFlags
            );

            if (ImGui.BeginDragDropTarget())
            {
                var payload = ImGui.AcceptDragDropPayload(DRAG_DROP_PAYLOAD_TYPE);
                unsafe
                {
                    if (payload.NativePtr != null)
                    {
                        Guid draggedId = Marshal.PtrToStructure<Guid>((IntPtr)payload.Data);
                        var draggedObject = FindObjectInScene(scene, draggedId);

                        if (draggedObject != null)
                        {
                            draggedObject.Transform.SetParent(null);
                            EngineEditor.LogMessage($"{draggedObject.Name} moved to root");
                        }
                    }
                }
                ImGui.EndDragDropTarget();
            }

            if (sceneOpen)
            {
                var allObjects = scene.GetAllGameObjects();

                foreach (var go in allObjects)
                {
                    if (go.Transform.Parent == null)
                        DrawGameObjectNode(go, scene);
                }

                ImGui.TreePop();
            }
        }

        private void DrawGameObjectNode(GameObject go, GameScene ownerScene)
        {
            bool isSelected = EditorActions.SelectedObject == go;
            bool hasChildren = go.Transform.Children.Count > 0;
            bool isRenaming = _renamingObject == go;

            ImGuiTreeNodeFlags flags =
                ImGuiTreeNodeFlags.OpenOnArrow |
                ImGuiTreeNodeFlags.OpenOnDoubleClick |
                ImGuiTreeNodeFlags.SpanAvailWidth;

            if (isSelected) flags |= ImGuiTreeNodeFlags.Selected;
            if (!hasChildren) flags |= ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen;

            if (isRenaming)
            {
                if (!hasChildren)
                    ImGui.Indent();

                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);

                if (_focusRenameField)
                {
                    ImGui.SetKeyboardFocusHere();
                    _focusRenameField = false;
                }

                bool confirmed = ImGui.InputText(
                    $"##rename_{go.Id}",
                    ref _renameBuffer,
                    128,
                    ImGuiInputTextFlags.EnterReturnsTrue | ImGuiInputTextFlags.AutoSelectAll
                );

                bool clickedElsewhere = !ImGui.IsItemActive() && !ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
                bool pressedEscape = ImGui.IsKeyPressed(ImGuiKey.Escape);

                if (confirmed || clickedElsewhere)
                {
                    if (!string.IsNullOrWhiteSpace(_renameBuffer))
                    {
                        go.Name = _renameBuffer.Trim();
                        EngineEditor.LogMessage($"Renamed to {go.Name}");
                    }
                    _renamingObject = null;
                    _renameBuffer = string.Empty;
                }
                else if (pressedEscape)
                {
                    _renamingObject = null;
                    _renameBuffer = string.Empty;
                }

                if (!hasChildren)
                    ImGui.Unindent();

                return;
            }

            bool nodeOpen = ImGui.TreeNodeEx($"{go.Name}##{go.Id.GetHashCode()}", flags);

            if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
                EditorActions.SelectedObject = go;

            if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
            {
                _renamingObject = go;
                _renameBuffer = go.Name;
                _focusRenameField = true;
            }

            if (isSelected && ImGui.IsKeyPressed(ImGuiKey.F2))
            {
                _renamingObject = go;
                _renameBuffer = go.Name;
                _focusRenameField = true;
            }

            if (ImGui.BeginDragDropSource(ImGuiDragDropFlags.None))
            {
                unsafe
                {
                    Guid id = go.Id;
                    IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf<Guid>());
                    Marshal.StructureToPtr(id, ptr, false);
                    ImGui.SetDragDropPayload(DRAG_DROP_PAYLOAD_TYPE, ptr, (uint)Marshal.SizeOf<Guid>());
                    Marshal.FreeHGlobal(ptr);
                }
                ImGui.Text($"Moving: {go.Name}");
                ImGui.EndDragDropSource();
            }

            if (ImGui.BeginDragDropTarget())
            {
                var payload = ImGui.AcceptDragDropPayload(DRAG_DROP_PAYLOAD_TYPE);
                unsafe
                {
                    if (payload.NativePtr != null)
                    {
                        Guid draggedId = Marshal.PtrToStructure<Guid>((IntPtr)payload.Data);
                        var draggedObject = FindObjectInScene(ownerScene, draggedId);

                        if (draggedObject != null && draggedObject != go)
                        {
                            if (!IsDescendantOf(go, draggedObject))
                            {
                                draggedObject.Transform.SetParent(go.Transform);
                                EngineEditor.LogMessage($"{draggedObject.Name} is now child of {go.Name}");
                            }
                            else
                            {
                                EngineEditor.LogMessage($"Cannot make {go.Name} child of its own descendant!");
                            }
                        }
                    }
                }
                ImGui.EndDragDropTarget();
            }

            if (ImGui.BeginPopupContextItem($"context_{go.Id}"))
            {
                if (ImGui.MenuItem("Rename"))
                {
                    _renamingObject = go;
                    _renameBuffer = go.Name;
                    _focusRenameField = true;
                }

                if (ImGui.MenuItem("Duplicate"))
                {
                    go.Clone(true);
                    EngineEditor.LogMessage($"Duplicated {go.Name}");
                }

                if (hasChildren && ImGui.MenuItem("Unparent Children"))
                {
                    foreach (var child in go.Transform.Children.ToArray())
                        child.SetParent(null);
                }

                if (ImGui.MenuItem("Delete") && go.Tag != "MainCamera")
                {
                    EditorActions.DeleteGameObject(go);
                    if (EditorActions.SelectedObject == go)
                        EditorActions.SelectedObject = null;
                }

                ImGui.EndPopup();
            }

            if (hasChildren && nodeOpen)
            {
                foreach (var child in go.Transform.Children)
                    DrawGameObjectNode(child.GameObject, ownerScene);

                ImGui.TreePop();
            }
        }

        private GameObject FindObjectInScene(GameScene scene, Guid id)
        {
            foreach (var obj in scene.GetAllGameObjects())
                if (obj.Id == id) return obj;
            return null;
        }

        private bool IsDescendantOf(GameObject go, GameObject potentialAncestor)
        {
            Transform current = go.Transform.Parent;
            while (current != null)
            {
                if (current.GameObject == potentialAncestor) return true;
                current = current.Parent;
            }
            return false;
        }
    }
}