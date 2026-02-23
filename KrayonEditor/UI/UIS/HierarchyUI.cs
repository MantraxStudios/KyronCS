using ImGuiNET;
using KrayonCore;
using KrayonCore.Components;
using KrayonEditor.Main;
using System;
using System.Runtime.InteropServices;

namespace KrayonEditor.UI
{
    public class HierarchyUI : UIBehaviour
    {
        private const string DRAG_DROP_PAYLOAD_TYPE = "GAMEOBJECT_HIERARCHY";

        // Payload extendido: ID del GameObject + nombre de la escena origen
        private struct DragPayload
        {
            public Guid ObjectId;
            // El nombre de la escena se pasa como contexto estático para evitar
            // serializar strings en el payload nativo de ImGui
        }

        // Guardamos la escena origen durante el drag (sólo válido en el mismo frame/sesión)
        private static string _dragSourceSceneName = null;

        public override void OnDrawUI()
        {
            if (!_isVisible) return;

            ImGui.Begin("Hierarchy", ref _isVisible);

            // ── Botones de creación rápida ──────────────────────────────────
            if (ImGui.Button("+ Create Empty"))
                EditorActions.CreateEmptyGameObject();

            ImGui.SameLine();

            if (ImGui.Button("+ Create Cube"))
                EditorActions.CreateCubeGameObject();

            ImGui.Separator();

            // ── Iterar todas las escenas primarias ──────────────────────────
            var scenes = SceneManager.PrimaryScenes;

            if (scenes.Count == 0)
            {
                ImGui.TextDisabled("No active scenes.");
            }
            else
            {
                foreach (var scene in scenes)
                {
                    DrawSceneNode(scene);
                }
            }

            // ── Menú contextual global (clic derecho en área vacía) ─────────
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

        // ═══════════════════════════════════════════════════════════════════
        //  Dibuja el nodo raíz de una escena
        // ═══════════════════════════════════════════════════════════════════
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

            // Drop target en la raíz → quitar padre al objeto arrastrado
            if (ImGui.BeginDragDropTarget())
            {
                var payload = ImGui.AcceptDragDropPayload(DRAG_DROP_PAYLOAD_TYPE);
                unsafe
                {
                    if (payload.NativePtr != null)
                    {
                        Guid draggedId = Marshal.PtrToStructure<Guid>((IntPtr)payload.Data);
                        var draggedObject = FindObjectInScene(scene, draggedId)
                                         ?? FindObjectInAnyScene(draggedId);

                        if (draggedObject != null)
                        {
                            // Si viene de otra escena, moverlo a esta
                            MoveObjectToScene(draggedObject, scene);
                            draggedObject.Transform.SetParent(null);
                            EngineEditor.LogMessage($"{draggedObject.Name} moved to root of '{scene.Name}'");
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

        // ═══════════════════════════════════════════════════════════════════
        //  Dibuja el nodo de un GameObject (recursivo)
        // ═══════════════════════════════════════════════════════════════════
        private void DrawGameObjectNode(GameObject go, GameScene ownerScene)
        {
            bool isSelected = EditorActions.SelectedObject == go;
            bool hasChildren = go.Transform.Children.Count > 0;

            ImGuiTreeNodeFlags flags =
                ImGuiTreeNodeFlags.OpenOnArrow |
                ImGuiTreeNodeFlags.OpenOnDoubleClick |
                ImGuiTreeNodeFlags.SpanAvailWidth;

            if (isSelected) flags |= ImGuiTreeNodeFlags.Selected;
            if (!hasChildren) flags |= ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen;

            bool nodeOpen = ImGui.TreeNodeEx($"{go.Name}##{go.Id.GetHashCode()}", flags);

            if (ImGui.IsItemClicked())
                EditorActions.SelectedObject = go;

            // ── Drag source ────────────────────────────────────────────────
            if (ImGui.BeginDragDropSource(ImGuiDragDropFlags.None))
            {
                _dragSourceSceneName = ownerScene.Name;   // guardamos la escena origen
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

            // ── Drop target ────────────────────────────────────────────────
            if (ImGui.BeginDragDropTarget())
            {
                var payload = ImGui.AcceptDragDropPayload(DRAG_DROP_PAYLOAD_TYPE);
                unsafe
                {
                    if (payload.NativePtr != null)
                    {
                        Guid draggedId = Marshal.PtrToStructure<Guid>((IntPtr)payload.Data);
                        var draggedObject = FindObjectInAnyScene(draggedId);

                        if (draggedObject != null && draggedObject != go)
                        {
                            if (!IsDescendantOf(go, draggedObject))
                            {
                                // Mover a la escena destino si es diferente
                                MoveObjectToScene(draggedObject, ownerScene);
                                draggedObject.Transform.SetParent(go.Transform);
                                EngineEditor.LogMessage(
                                    $"{draggedObject.Name} is now child of {go.Name} in '{ownerScene.Name}'");
                            }
                            else
                            {
                                EngineEditor.LogMessage(
                                    $"Cannot make {go.Name} child of its own descendant!");
                            }
                        }
                    }
                }
                ImGui.EndDragDropTarget();
            }

            // ── Menú contextual ────────────────────────────────────────────
            if (ImGui.BeginPopupContextItem($"context_{go.Id}"))
            {
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

                // Sub-menú para mover a otra escena
                var scenes = SceneManager.PrimaryScenes;
                if (scenes.Count > 1 && ImGui.BeginMenu("Move to Scene"))
                {
                    foreach (var targetScene in scenes)
                    {
                        if (targetScene == ownerScene) continue;
                        if (ImGui.MenuItem(targetScene.Name))
                        {
                            MoveObjectToScene(go, targetScene);
                            EngineEditor.LogMessage($"{go.Name} moved to scene '{targetScene.Name}'");
                        }
                    }
                    ImGui.EndMenu();
                }

                if (ImGui.MenuItem("Delete") && go.Tag != "MainCamera")
                {
                    EditorActions.DeleteGameObject(go);
                    if (EditorActions.SelectedObject == go)
                        EditorActions.SelectedObject = null;
                }

                ImGui.EndPopup();
            }

            // ── Hijos recursivos ───────────────────────────────────────────
            if (hasChildren && nodeOpen)
            {
                foreach (var child in go.Transform.Children)
                    DrawGameObjectNode(child.GameObject, ownerScene);

                ImGui.TreePop();
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Helpers
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Mueve un GameObject de su escena actual a la escena destino.
        /// Si ya pertenece a la escena destino, no hace nada.
        /// </summary>
        private void MoveObjectToScene(GameObject go, GameScene targetScene)
        {
            foreach (var scene in SceneManager.PrimaryScenes)
            {
                if (scene == targetScene) continue;
                if (scene.ContainsGameObject(go))
                {
                    scene.RemoveGameObject(go);
                    targetScene.AddGameObject(go);
                    return;
                }
            }
        }

        /// <summary>Busca un GameObject por ID en una escena concreta.</summary>
        private GameObject FindObjectInScene(GameScene scene, Guid id)
        {
            foreach (var obj in scene.GetAllGameObjects())
                if (obj.Id == id) return obj;
            return null;
        }

        /// <summary>Busca un GameObject por ID en todas las escenas activas.</summary>
        private GameObject FindObjectInAnyScene(Guid id)
        {
            foreach (var scene in SceneManager.PrimaryScenes)
            {
                var result = FindObjectInScene(scene, id);
                if (result != null) return result;
            }
            return null;
        }

        /// <summary>
        /// Devuelve true si <paramref name="go"/> es descendiente de
        /// <paramref name="potentialAncestor"/>.
        /// </summary>
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