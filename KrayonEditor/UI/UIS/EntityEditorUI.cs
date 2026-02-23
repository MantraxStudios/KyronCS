using ImGuiNET;
using KrayonCore;
using KrayonCore.Components.RenderComponents;
using KrayonCore.Core.Attributes;
using KrayonCore.Graphics.Camera;
using KrayonCore.Graphics.GameUI;
using KrayonEditor.UI;
using System.Numerics;

namespace KrayonEditor.UI.UIS
{
    public class EntityEditorUI : UIBehaviour
    {
        public GameScene Scene;
        public CameraComponent _Cam;
        private GameObject _selectedObject;

        private bool _firstMouse = true;
        private float _cameraSpeed = 2.5f;

        private GameObject _renamingObject = null;
        private string _renameBuffer = "";

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
                SceneSaveSystem.SaveScene(Scene, $"{AssetManager.BasePath}/Entity/{Scene.Name}.entity");

            ImGui.SameLine();

            if (ImGui.Button("New Scene"))
            {
                Scene = SceneManager.CreateScene("Entity Scene");

                GameObject obj = Scene.CreateGameObject();
                obj.Name = "Editor Camera";
                obj.AddComponent<MeshRenderer>().Start();
                obj.Transform.Position = new OpenTK.Mathematics.Vector3(0, 0, -5);

                GameObject camObj = Scene.CreateGameObject();
                _Cam = camObj.AddComponent<CameraComponent>();
                _Cam.Start();
                camObj.Transform.Position = new OpenTK.Mathematics.Vector3(0, 0, 5);
            }

            if (Scene != null)
            {
                ImGui.SameLine();
                if (ImGui.Button("+ Add Object"))
                {
                    GameObject newObj = Scene.CreateGameObject();
                    newObj.Name = "New Object";
                    _selectedObject = newObj;
                }

                if (_selectedObject != null)
                {
                    ImGui.SameLine();
                    ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.6f, 0.1f, 0.1f, 1f));
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.78f, 0.22f, 0.22f, 1f));
                    if (ImGui.Button("Delete"))
                    {
                        Scene.DestroyGameObject(_selectedObject);
                        _selectedObject = null;
                    }
                    ImGui.PopStyleColor(2);
                }

                ImGui.Separator();

                var sceneFlags = ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.SpanAvailWidth;
                if (ImGui.TreeNodeEx($"{Scene.Name}##scene", sceneFlags))
                {
                    foreach (var go in Scene.GetAllGameObjects())
                    {
                        if (_Cam != null && _Cam.GameObject != go && go.Transform.Parent == null)
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
                if (ImGui.InputText($"##rename_{go.Id}", ref _renameBuffer, 128, ImGuiInputTextFlags.EnterReturnsTrue))
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

            if (hasChildren && nodeOpen)
            {
                foreach (var child in go.Transform.Children)
                    DrawGameObjectNode(child.GameObject);
                ImGui.TreePop();
            }
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

            if (Scene != null && _Cam != null)
            {
                var viewportSize = ImGui.GetContentRegionAvail();
                var fb = _Cam.RenderCamera.GetFinalTextureId(false);

                if (_Cam.RenderCamera.ViewportWidth != (int)viewportSize.X ||
                    _Cam.RenderCamera.ViewportHeight != (int)viewportSize.Y)
                {
                    _Cam.ResizeBuffer((int)viewportSize.X, (int)viewportSize.Y);
                    _Cam.RenderCamera.Camera.UpdateAspectRatio((int)viewportSize.X, (int)viewportSize.Y);
                }

                Vector2 cursorPos = ImGui.GetCursorScreenPos();
                ImGui.Image(fb, viewportSize, new Vector2(0, 1), new Vector2(1, 0));
                bool isHovered = ImGui.IsItemHovered();

                if (isHovered)
                    HandleCameraInput();

                DrawGizmo(cursorPos, viewportSize, isHovered);
            }

            ImGui.End();
        }

        private void HandleCameraInput()
        {
            if (_Cam?.RenderCamera?.Camera is null) return;

            var io = ImGui.GetIO();
            var camera = _Cam.RenderCamera.Camera;
            float dt = io.DeltaTime;

            if (ImGui.IsMouseDown(ImGuiMouseButton.Right))
            {
                Vector2 delta = io.MouseDelta;
                if (!_firstMouse)
                    camera.Rotate(delta.X, -delta.Y);
                _firstMouse = false;

                float speed = _cameraSpeed * dt * (io.KeyCtrl ? 2.0f : 1.0f);

                if (ImGui.IsKeyDown(ImGuiKey.W)) camera.Move(CameraMovement.Forward, speed);
                if (ImGui.IsKeyDown(ImGuiKey.S)) camera.Move(CameraMovement.Backward, speed);
                if (ImGui.IsKeyDown(ImGuiKey.A)) camera.Move(CameraMovement.Left, speed);
                if (ImGui.IsKeyDown(ImGuiKey.D)) camera.Move(CameraMovement.Right, speed);
                if (ImGui.IsKeyDown(ImGuiKey.Space)) camera.Move(CameraMovement.Up, speed);
                if (ImGui.IsKeyDown(ImGuiKey.LeftShift)) camera.Move(CameraMovement.Down, speed);
            }
            else
            {
                _firstMouse = true;
            }

            if (io.MouseWheel != 0)
                camera.Zoom(io.MouseWheel);
        }

        private void DrawGizmo(Vector2 cursorPos, Vector2 viewportSize, bool isHovered)
        {
            if (_selectedObject == null || _Cam == null || !isHovered) return;

            var transform = new GizmoTransform(
                _selectedObject.Transform.GetWorldPosition(),
                _selectedObject.Transform.GetWorldRotation(),
                _selectedObject.Transform.GetWorldScale());

            bool modified = TransformGizmo.Draw(
                ref transform,
                _Cam.RenderCamera.Camera.GetViewMatrix(),
                _Cam.RenderCamera.Camera.GetProjectionMatrix(),
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