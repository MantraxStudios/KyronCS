using ImGuiNET;
using KrayonCore;
using KrayonCore.Components.RenderComponents;
using KrayonCore.Graphics.Camera;
using System;
using System.Collections.Generic;
using System.Text;
using System.Numerics;

namespace KrayonEditor.UI.UIS
{
    public class EntityEditorUI : UIBehaviour
    {
        public GameScene Scene;
        public CameraComponent _Cam;
        private GameObject _selectedObject;

        public override void OnDrawUI()
        {
            ImGui.Begin("Entity Editor", ImGuiWindowFlags.NoScrollbar);

            uint dockId = ImGui.GetID("EntityEditorDock");
            ImGui.DockSpace(dockId);

            DrawHierarchy();
            DrawViewport();

            ImGui.End();
        }

        private void DrawHierarchy()
        {
            ImGui.SetNextWindowDockID(ImGui.GetID("EntityEditorDock"), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSize(new Vector2(250, 400), ImGuiCond.FirstUseEver);

            ImGui.Begin("Entity Hierarchy");

            if (ImGui.Button("New Scene"))
            {
                Scene = SceneManager.CreateScene("Entity Scene");
                GameObject _OBJ = Scene.CreateGameObject();
                _OBJ.Name = "Editor Camera";
                _OBJ.AddComponent<MeshRenderer>().Start();
                _OBJ.Transform.Position = new OpenTK.Mathematics.Vector3(0, 0, -5);
                GameObject _CAM = Scene.CreateGameObject();
                _Cam = _CAM.AddComponent<CameraComponent>();
                _Cam.Start();
                _CAM.Transform.Position = new OpenTK.Mathematics.Vector3(0, 0, 5);
            }

            if (Scene != null)
            {
                ImGuiTreeNodeFlags sceneFlags = ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.SpanAvailWidth;
                if (ImGui.TreeNodeEx($"{Scene.Name}##scene", sceneFlags))
                {
                    var allObjects = Scene.GetAllGameObjects();
                    foreach (var go in allObjects)
                    {
                        if (_Cam.GameObject != go)
                        {
                            if (go.Transform.Parent == null)
                                DrawGameObjectNode(go);
                        }
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
            ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.SpanAvailWidth;

            if (isSelected)
                flags |= ImGuiTreeNodeFlags.Selected;

            if (!hasChildren)
                flags |= ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen;

            bool nodeOpen = ImGui.TreeNodeEx($"{go.Name}##{go.Id.GetHashCode()}", flags);

            if (ImGui.IsItemClicked())
                _selectedObject = go;

            if (hasChildren && nodeOpen)
            {
                foreach (var child in go.Transform.Children)
                    DrawGameObjectNode(child.GameObject);
                ImGui.TreePop();
            }
        }

        private void DrawViewport()
        {
            ImGui.SetNextWindowDockID(ImGui.GetID("EntityEditorDock"), ImGuiCond.FirstUseEver);

            ImGui.Begin("Entity Viewport");

            if (Scene != null && _Cam != null)
            {
                var viewportSize = ImGui.GetContentRegionAvail();
                var fb = _Cam.RenderCamera.GetFinalTextureId(false);

                if (_Cam.RenderCamera.ViewportWidth != (int)viewportSize.X || _Cam.RenderCamera.ViewportHeight != (int)viewportSize.Y)
                {
                    _Cam.ResizeBuffer((int)viewportSize.X, (int)viewportSize.Y);
                    _Cam.RenderCamera.Camera.UpdateAspectRatio((int)viewportSize.X, (int)viewportSize.Y);
                }

                Vector2 cursorPos = ImGui.GetCursorScreenPos();
                ImGui.Image(fb, viewportSize, new Vector2(0, 1), new Vector2(1, 0));
                bool isHovered = ImGui.IsItemHovered();

                DrawGizmo(cursorPos, viewportSize, isHovered);
            }

            ImGui.End();
        }

        private void DrawGizmo(Vector2 cursorPos, Vector2 viewportSize, bool isHovered)
        {
            if (_selectedObject == null || _Cam == null || !isHovered)
                return;

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