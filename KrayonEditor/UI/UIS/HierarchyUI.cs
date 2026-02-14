using ImGuiNET;
using KrayonCore;
using System;
using System.Runtime.InteropServices;

namespace KrayonEditor.UI
{
    public class HierarchyUI : UIBehaviour
    {
        private const string DRAG_DROP_PAYLOAD_TYPE = "GAMEOBJECT_HIERARCHY";

        public override void OnDrawUI()
        {
            if (!_isVisible) return;

            ImGui.Begin("Hierarchy", ref _isVisible);

            if (ImGui.Button("+ Create Empty"))
            {
                EditorActions.CreateEmptyGameObject();
            }
            ImGui.SameLine();
            if (ImGui.Button("+ Create Cube"))
            {
                EditorActions.CreateCubeGameObject();
            }

            ImGui.Separator();

            if (SceneManager.ActiveScene != null)
            {
                bool sceneOpen = ImGui.TreeNodeEx(
                    $"{SceneManager.ActiveScene.Name}##{SceneManager.ActiveScene.GetHashCode()}",
                    ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.OpenOnDoubleClick
                );

                // Drop target en la raíz de la escena (para quitar padres)
                if (ImGui.BeginDragDropTarget())
                {
                    var payload = ImGui.AcceptDragDropPayload(DRAG_DROP_PAYLOAD_TYPE);
                    unsafe
                    {
                        if (payload.NativePtr != null)
                        {
                            // Obtener el GameObject desde el payload
                            IntPtr dataPtr = (IntPtr)payload.Data;
                            Guid draggedId = Marshal.PtrToStructure<Guid>(dataPtr);
                            
                            var allObjects = SceneManager.ActiveScene.GetAllGameObjects();
                            GameObject draggedObject = null;
                            foreach (var obj in allObjects)
                            {
                                if (obj.Id == draggedId)
                                {
                                    draggedObject = obj;
                                    break;
                                }
                            }

                            if (draggedObject != null)
                            {
                                // Quitar padre (mover a la raíz)
                                draggedObject.Transform.SetParent(null);
                                EngineEditor.LogMessage($"{draggedObject.Name} moved to root");
                            }
                        }
                    }
                    ImGui.EndDragDropTarget();
                }

                if (sceneOpen)
                {
                    var allObjects = SceneManager.ActiveScene.GetAllGameObjects();
                    
                    // Solo mostrar objetos de nivel raíz (sin padre)
                    foreach (var go in allObjects)
                    {
                        if (go.Transform.Parent == null)
                        {
                            DrawGameObjectNode(go);
                        }
                    }
                    
                    ImGui.TreePop();
                }
            }

            if (ImGui.BeginPopupContextWindow("hierarchy_context"))
            {
                if (ImGui.MenuItem("Empty GameObject"))
                {
                    EditorActions.CreateEmptyGameObject();
                }

                if (ImGui.MenuItem("Model"))
                {
                    EditorActions.CreateModelGameObject();
                }

                if (ImGui.MenuItem("TileRenderer"))
                {
                    EditorActions.CreateTileRendererGameObject();
                }

                if (ImGui.BeginMenu("Light"))
                {
                    if (ImGui.MenuItem("Directional Light"))
                    {
                        EditorActions.CreateDirectionalLight();
                    }
                    if (ImGui.MenuItem("Point Light"))
                    {
                        EditorActions.CreatePointLight();
                    }
                    if (ImGui.MenuItem("Spot Light"))
                    {
                        EditorActions.CreateSpotLight();
                    }
                    ImGui.EndMenu();
                }

                if (ImGui.MenuItem("New Camera"))
                {
                    EditorActions.CreateCamera();
                }

                ImGui.EndPopup();
            }

            ImGui.End();
        }

        private void DrawGameObjectNode(GameObject go)
        {
            bool isSelected = EditorActions.SelectedObject == go;
            bool hasChildren = go.Transform.Children.Count > 0;

            ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.OpenOnArrow | 
                                       ImGuiTreeNodeFlags.OpenOnDoubleClick |
                                       ImGuiTreeNodeFlags.SpanAvailWidth;

            if (isSelected)
                flags |= ImGuiTreeNodeFlags.Selected;

            if (!hasChildren)
                flags |= ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen;

            string label = $"{go.Name}##{go.Id}";
            bool nodeOpen = ImGui.TreeNodeEx(label, flags);

            // Seleccionar al hacer click
            if (ImGui.IsItemClicked())
            {
                EditorActions.SelectedObject = go;
            }

            // === DRAG SOURCE ===
            if (ImGui.BeginDragDropSource(ImGuiDragDropFlags.None))
            {
                // Guardar el ID del GameObject que se está arrastrando
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

            // === DROP TARGET ===
            if (ImGui.BeginDragDropTarget())
            {
                var payload = ImGui.AcceptDragDropPayload(DRAG_DROP_PAYLOAD_TYPE);
                unsafe
                {
                    if (payload.NativePtr != null)
                    {
                        // Obtener el GameObject desde el payload
                        IntPtr dataPtr = (IntPtr)payload.Data;
                        Guid draggedId = Marshal.PtrToStructure<Guid>(dataPtr);
                        
                        var allObjects = SceneManager.ActiveScene.GetAllGameObjects();
                        GameObject draggedObject = null;
                        foreach (var obj in allObjects)
                        {
                            if (obj.Id == draggedId)
                            {
                                draggedObject = obj;
                                break;
                            }
                        }

                        if (draggedObject != null && draggedObject != go)
                        {
                            // Verificar que no estamos intentando hacer un objeto hijo de sí mismo
                            // o crear un ciclo (hacer padre hijo de su propio descendiente)
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

            // Menú contextual
            if (ImGui.BeginPopupContextItem($"context_{go.Id}"))
            {
                if (ImGui.MenuItem("Duplicate"))
                {
                    var clone = go.Clone(true);
                    EngineEditor.LogMessage($"Duplicated {go.Name}");
                }
                

                if (hasChildren && ImGui.MenuItem("Unparent Children"))
                {
                    var children = go.Transform.Children.ToArray();
                    foreach (var child in children)
                    {
                        child.SetParent(null);
                    }
                }

                if (ImGui.MenuItem("Delete") && go.Tag != "MainCamera")
                {
                    EditorActions.DeleteGameObject(go);
                    if (EditorActions.SelectedObject == go)
                        EditorActions.SelectedObject = null;
                }
                
                ImGui.EndPopup();
            }

            // Dibujar hijos recursivamente
            if (hasChildren && nodeOpen)
            {
                foreach (var child in go.Transform.Children)
                {
                    DrawGameObjectNode(child.GameObject);
                }
                ImGui.TreePop();
            }
        }

        // Verificar si 'potentialAncestor' es ancestro de 'go'
        private bool IsDescendantOf(GameObject go, GameObject potentialAncestor)
        {
            Transform current = go.Transform.Parent;
            while (current != null)
            {
                if (current.GameObject == potentialAncestor)
                    return true;
                current = current.Parent;
            }
            return false;
        }
    }
}