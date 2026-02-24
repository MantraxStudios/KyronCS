using ImGuiNET;
using KrayonCore;
using KrayonCore.Components;
using KrayonCore.Components.RenderComponents;
using KrayonCore.Core.Attributes;
using KrayonCore.EventSystem;
using System.Numerics;
using System.Runtime.InteropServices;

namespace KrayonEditor.UI.UIS
{
    public class EntityEditorUI : UIBehaviour
    {
        public GameScene Scene;
        private Camera _editorCamera;
        private GameObject _selectedObject;

        private bool _firstMouse = true;
        private float _cameraSpeed = 2.5f;

        private GameObject _renamingObject = null;
        private string _renameBuffer = "";

        private string _entitySavePath = null;

        private const string DRAG_DROP_PAYLOAD = "ENTITY_GAMEOBJECT";

        public void OpenEntity(Guid assetGuid)
        {
            var asset = AssetManager.Get(assetGuid);
            if (asset == null) return;

            string fullPath = System.IO.Path.Combine(AssetManager.BasePath, asset.Path);
            if (_entitySavePath == fullPath && Scene != null) return;

            _entitySavePath = fullPath;

            if (Scene != null)
                SceneManager.UnregisterSceneOnly(Scene);

            Scene = SceneSaveSystem.LoadScene(fullPath)
                 ?? SceneManager.CreateScene(System.IO.Path.GetFileNameWithoutExtension(asset.Path));

            SceneManager.RegisterSceneOnly(Scene);

            _selectedObject = null;
            _editorCamera = Scene.SelfRenderScene.GetCamera();
            _editorCamera.Position = new OpenTK.Mathematics.Vector3(0, 0, 5);
        }

        public override void OnDrawUI()
        {
            ImGui.Begin("Entity Editor", ImGuiWindowFlags.NoScrollbar);

            uint dockId = ImGui.GetID("EntityEditorDock");
            ImGui.DockSpace(dockId);

            DrawHierarchy();
            DrawDetails();
            DrawViewport();

            ImGui.End();
        }

        private void DrawHierarchy()
        {
            ImGui.SetNextWindowDockID(ImGui.GetID("EntityEditorDock"), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSize(new Vector2(250, 400), ImGuiCond.FirstUseEver);
            ImGui.Begin("Entity Hierarchy");

            if (ImGui.Button("Save Entity"))
            {
                string savePath = _entitySavePath
                    ?? $"{AssetManager.BasePath}/Entity/{Scene?.Name}.entity";
                SceneSaveSystem.SaveScene(Scene, savePath);
            }

            ImGui.SameLine();

            if (ImGui.Button("New Scene"))
            {
                if (Scene != null)
                    SceneManager.UnregisterSceneOnly(Scene);

                Scene = SceneManager.CreateScene("Entity Scene");
                _entitySavePath = null;
                SceneManager.RegisterSceneOnly(Scene);

                _selectedObject = null;
                _editorCamera = Scene.SelfRenderScene.GetCamera();
                _editorCamera.Position = new OpenTK.Mathematics.Vector3(0, 0, 5);
            }

            ImGui.Separator();

            if (Scene != null)
            {
                if (_selectedObject != null && ImGui.IsWindowFocused() && ImGui.IsKeyPressed(ImGuiKey.Delete))
                {
                    Scene.DestroyGameObject(_selectedObject);
                    _selectedObject = null;
                }

                var sceneFlags = ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.SpanAvailWidth;
                bool sceneOpen = ImGui.TreeNodeEx($"{Scene.Name}##scene", sceneFlags);

                if (ImGui.BeginPopupContextWindow("hierarchy_ctx",
                    ImGuiPopupFlags.MouseButtonRight | ImGuiPopupFlags.NoOpenOverItems))
                {
                    if (ImGui.MenuItem("New Empty Object"))
                    {
                        var newObj = Scene.CreateGameObject();
                        newObj.Name = "New Object";
                        _selectedObject = newObj;
                    }

                    if (ImGui.MenuItem("New Object with MeshRenderer"))
                    {
                        var newObj = Scene.CreateGameObject();
                        newObj.Name = "New Mesh Object";
                        newObj.AddComponent<MeshRenderer>().Start();
                        _selectedObject = newObj;
                    }

                    ImGui.EndPopup();
                }

                if (ImGui.BeginDragDropTarget())
                {
                    var payload = ImGui.AcceptDragDropPayload(DRAG_DROP_PAYLOAD);
                    unsafe
                    {
                        if (payload.NativePtr != null)
                        {
                            Guid draggedId = Marshal.PtrToStructure<Guid>((IntPtr)payload.Data);
                            var draggedObject = FindObjectInScene(draggedId);
                            if (draggedObject != null)
                                draggedObject.Transform.SetParent(null);
                        }
                    }
                    ImGui.EndDragDropTarget();
                }

                if (sceneOpen)
                {
                    foreach (var go in Scene.GetAllGameObjects())
                    {
                        if (go.Transform.Parent == null)
                            DrawGameObjectNode(go);
                    }
                    ImGui.TreePop();
                }
            }

            ImGui.End();
        }

        private void DrawGameObjectNode(GameObject go)
        {
            bool isSelected = _selectedObject == go;
            bool hasChildren = go.Transform.Children.Count > 0;

            var flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.SpanAvailWidth;
            if (isSelected) flags |= ImGuiTreeNodeFlags.Selected;
            if (!hasChildren) flags |= ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen;

            if (_renamingObject == go)
            {
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputText($"##rename_{go.Id}", ref _renameBuffer, 128,
                    ImGuiInputTextFlags.EnterReturnsTrue))
                {
                    go.Name = _renameBuffer;
                    _renamingObject = null;
                }
                if (!ImGui.IsItemActive() && !ImGui.IsItemFocused())
                    _renamingObject = null;
                return;
            }

            bool nodeOpen = ImGui.TreeNodeEx($"{go.Name}##{go.Id.GetHashCode()}", flags);

            if (ImGui.IsItemClicked())
                _selectedObject = go;

            if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
            {
                _renamingObject = go;
                _renameBuffer = go.Name;
            }

            if (ImGui.BeginPopupContextItem($"ctx_{go.Id}"))
            {
                if (ImGui.MenuItem("New Empty Child"))
                {
                    var child = Scene.CreateGameObject();
                    child.Name = "New Object";
                    child.Transform.SetParent(go.Transform);
                    _selectedObject = child;
                }

                if (ImGui.MenuItem("New MeshRenderer Child"))
                {
                    var child = Scene.CreateGameObject();
                    child.Name = "New Mesh Object";
                    child.AddComponent<MeshRenderer>().Start();
                    child.Transform.SetParent(go.Transform);
                    _selectedObject = child;
                }

                ImGui.Separator();

                if (ImGui.MenuItem("Delete"))
                {
                    Scene.DestroyGameObject(go);
                    if (_selectedObject == go) _selectedObject = null;
                }

                ImGui.EndPopup();
            }

            if (ImGui.BeginDragDropSource(ImGuiDragDropFlags.None))
            {
                unsafe
                {
                    Guid id = go.Id;
                    IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf<Guid>());
                    Marshal.StructureToPtr(id, ptr, false);
                    ImGui.SetDragDropPayload(DRAG_DROP_PAYLOAD, ptr, (uint)Marshal.SizeOf<Guid>());
                    Marshal.FreeHGlobal(ptr);
                }
                ImGui.Text($"Moving: {go.Name}");
                ImGui.EndDragDropSource();
            }

            if (ImGui.BeginDragDropTarget())
            {
                var payload = ImGui.AcceptDragDropPayload(DRAG_DROP_PAYLOAD);
                unsafe
                {
                    if (payload.NativePtr != null)
                    {
                        Guid draggedId = Marshal.PtrToStructure<Guid>((IntPtr)payload.Data);
                        var draggedObject = FindObjectInScene(draggedId);
                        if (draggedObject != null && draggedObject != go)
                        {
                            if (!IsDescendantOf(go, draggedObject))
                                draggedObject.Transform.SetParent(go.Transform);
                        }
                    }
                }
                ImGui.EndDragDropTarget();
            }

            if (hasChildren && nodeOpen)
            {
                foreach (var child in go.Transform.Children)
                    DrawGameObjectNode(child.GameObject);
                ImGui.TreePop();
            }
        }

        private GameObject FindObjectInScene(Guid id)
        {
            if (Scene == null) return null;
            foreach (var obj in Scene.GetAllGameObjects())
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

        private void DrawDetails()
        {
            ImGui.SetNextWindowDockID(ImGui.GetID("EntityEditorDock"), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSize(new Vector2(280, 400), ImGuiCond.FirstUseEver);
            ImGui.Begin("Details");

            if (_selectedObject == null)
            {
                ImGui.TextDisabled("No object selected.");
                ComponentInspector.DrawAssetPickerModal();
                ImGui.End();
                return;
            }

            float avail = ImGui.GetContentRegionAvail().X;
            float tagW = 90f;
            float nameW = avail - tagW - ImGui.GetStyle().ItemSpacing.X;

            string name = _selectedObject.Name;
            ImGui.SetNextItemWidth(nameW);
            if (ImGui.InputText("##obj_name", ref name, 128))
                _selectedObject.Name = name;

            ImGui.SameLine();
            string tag = _selectedObject.Tag;
            ImGui.SetNextItemWidth(tagW);
            if (ImGui.InputText("##obj_tag", ref tag, 64))
                _selectedObject.Tag = tag;

            bool active = _selectedObject.Active;
            if (ImGui.Checkbox("Active", ref active))
                _selectedObject.Active = active;

            ImGui.Separator();

            ComponentInspector.DrawTransform(_selectedObject.Transform);

            ImGui.Separator();
            ImGui.Spacing();

            var components = _selectedObject.GetAllComponents().ToList();
            int idx = 0;
            foreach (var component in components)
            {
                if (component is KrayonCore.Components.Transform) { idx++; continue; }
                ImGui.PushID($"comp_{idx}");
                ComponentInspector.DrawComponentWithReflection(component, _selectedObject);
                ImGui.PopID();
                idx++;
            }

            ImGui.Spacing();
            ImGui.Separator();

            ComponentInspector.DrawAddComponentButton(_selectedObject);
            ComponentInspector.DrawAssetPickerModal();

            ImGui.End();
        }

        private void DrawViewport()
        {
            ImGui.SetNextWindowDockID(ImGui.GetID("EntityEditorDock"), ImGuiCond.FirstUseEver);
            ImGui.Begin("Entity Viewport");

            if (Scene != null && _editorCamera != null)
            {
                var viewportSize = ImGui.GetContentRegionAvail();
                var fb = Scene.SelfRenderScene.Buffers.Get("scene");

                if (fb.Width != (int)viewportSize.X || fb.Height != (int)viewportSize.Y)
                {
                    Scene.SelfRenderScene.Resize((int)viewportSize.X, (int)viewportSize.Y);
                    _editorCamera.AspectRatio = viewportSize.X / viewportSize.Y;
                }

                Vector2 cursorPos = ImGui.GetCursorScreenPos();
                ImGui.Image(fb.ColorTexture, viewportSize, new Vector2(0, 1), new Vector2(1, 0));
                bool isHovered = ImGui.IsItemHovered();

                if (isHovered)
                {
                    HandleCameraInput();

                    if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !TransformGizmo.IsHovering)
                    {
                        Vector2 relMouse = ImGui.GetMousePos() - cursorPos;
                        bool inBounds = relMouse.X >= 0 && relMouse.X <= viewportSize.X &&
                                        relMouse.Y >= 0 && relMouse.Y <= viewportSize.Y;

                        if (inBounds)
                        {
                            var tkMouse = new OpenTK.Mathematics.Vector2(relMouse.X, relMouse.Y);
                            EventSystem.ScreenToWorldRay(tkMouse, _editorCamera,
                                (int)viewportSize.X, (int)viewportSize.Y,
                                out OpenTK.Mathematics.Vector3 rayOrigin,
                                out OpenTK.Mathematics.Vector3 rayDir);

                            var hit = EventSystem.GetObjectByRay(rayOrigin, rayDir, Scene);
                            _selectedObject = hit == _selectedObject ? null : hit;
                        }
                    }
                }

                DrawGizmo(cursorPos, viewportSize, isHovered);
            }

            ImGui.End();
        }

        private void HandleCameraInput()
        {
            if (_editorCamera is null) return;

            var io = ImGui.GetIO();
            float dt = io.DeltaTime;

            if (ImGui.IsMouseDown(ImGuiMouseButton.Right))
            {
                Vector2 delta = io.MouseDelta;
                if (!_firstMouse && (delta.X != 0 || delta.Y != 0))
                    _editorCamera.Rotate(delta.X, -delta.Y);
                _firstMouse = false;

                float speed = _cameraSpeed * dt * (io.KeyCtrl ? 2.0f : 1.0f);

                if (ImGui.IsKeyDown(ImGuiKey.W)) _editorCamera.Move(CameraMovement.Forward, speed);
                if (ImGui.IsKeyDown(ImGuiKey.S)) _editorCamera.Move(CameraMovement.Backward, speed);
                if (ImGui.IsKeyDown(ImGuiKey.A)) _editorCamera.Move(CameraMovement.Left, speed);
                if (ImGui.IsKeyDown(ImGuiKey.D)) _editorCamera.Move(CameraMovement.Right, speed);
                if (ImGui.IsKeyDown(ImGuiKey.Space)) _editorCamera.Move(CameraMovement.Up, speed);
                if (ImGui.IsKeyDown(ImGuiKey.LeftShift)) _editorCamera.Move(CameraMovement.Down, speed);
            }
            else
            {
                _firstMouse = true;
            }

            if (io.MouseWheel != 0)
                _editorCamera.Zoom(io.MouseWheel);
        }

        private void DrawGizmo(Vector2 cursorPos, Vector2 viewportSize, bool isHovered)
        {
            if (_selectedObject == null || _editorCamera == null || !isHovered) return;

            var transform = new GizmoTransform(
                _selectedObject.Transform.GetWorldPosition(),
                _selectedObject.Transform.GetWorldRotation(),
                _selectedObject.Transform.GetWorldScale());

            bool modified = TransformGizmo.Draw(
                ref transform,
                _editorCamera.GetViewMatrix(),
                _editorCamera.GetProjectionMatrix(),
                cursorPos,
                viewportSize,
                isHovered);

            if (modified)
            {
                _selectedObject.Transform.SetWorldPosition(transform.Position);
                _selectedObject.Transform.SetWorldRotation(transform.Rotation);
                _selectedObject.Transform.SetWorldScale(transform.Scale);
            }
        }
    }
}