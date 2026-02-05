using ImGuiNET;
using KrayonCore;
using Vector2 = System.Numerics.Vector2;
using Vector4 = System.Numerics.Vector4;

namespace KrayonEditor.UI
{
    public class SceneViewUI : UIBehaviour
    {
        public GraphicsEngine? Engine { get; set; }
        public Camera? MainCamera { get; set; }
        public GameObject? SelectedObject { get; set; }
        public bool IsPlaying { get; set; }
        public float EditorCameraSpeed { get; set; } = 5.0f;
        public Vector2 LastViewportSize { get; set; }

        public override void OnDrawUI()
        {
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0, 0));
            ImGui.Begin("Scene", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
            ImGui.PopStyleVar();

            DrawToolbar();
            DrawViewport();

            ImGui.End();
        }

        private void DrawToolbar()
        {
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(4, 4));
            ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.125f, 0.125f, 0.125f, 1.0f));
            ImGui.BeginChild("SceneToolbar", new Vector2(0, 40), ImGuiChildFlags.Borders);

            DrawTransformButtons();
            ImGui.SameLine();
            ImGui.Dummy(new Vector2(10, 0));
            ImGui.SameLine();

            DrawSpaceButton();
            ImGui.SameLine();
            ImGui.Dummy(new Vector2(10, 0));
            ImGui.SameLine();

            DrawSnapControls();
            ImGui.SameLine();
            ImGui.Dummy(new Vector2(10, 0));
            ImGui.SameLine();

            DrawCameraSpeed();

            ImGui.EndChild();
            ImGui.PopStyleColor();
            ImGui.PopStyleVar();
        }

        private void DrawTransformButtons()
        {
            bool isTranslate = TransformGizmo.CurrentMode == GizmoMode.Translate;
            bool isRotate = TransformGizmo.CurrentMode == GizmoMode.Rotate;
            bool isScale = TransformGizmo.CurrentMode == GizmoMode.Scale;

            if (isTranslate) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.3f, 0.5f, 0.8f, 1.0f));
            if (ImGui.Button("Move (Q)", new Vector2(80, 0)))
            {
                TransformGizmo.SetMode(GizmoMode.Translate);
            }
            if (isTranslate) ImGui.PopStyleColor();

            ImGui.SameLine();
            if (isRotate) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.3f, 0.5f, 0.8f, 1.0f));
            if (ImGui.Button("Rotate (E)", new Vector2(80, 0)))
            {
                TransformGizmo.SetMode(GizmoMode.Rotate);
            }
            if (isRotate) ImGui.PopStyleColor();

            ImGui.SameLine();
            if (isScale) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.3f, 0.5f, 0.8f, 1.0f));
            if (ImGui.Button("Scale (R)", new Vector2(80, 0)))
            {
                TransformGizmo.SetMode(GizmoMode.Scale);
            }
            if (isScale) ImGui.PopStyleColor();
        }

        private void DrawSpaceButton()
        {
            bool isWorld = TransformGizmo.CurrentSpace == GizmoSpace.World;
            string spaceText = isWorld ? "World (X)" : "Local (X)";

            if (!isWorld) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.5f, 0.3f, 0.8f, 1.0f));
            if (ImGui.Button(spaceText, new Vector2(90, 0)))
            {
                TransformGizmo.ToggleSpace();
            }
            if (!isWorld) ImGui.PopStyleColor();

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(isWorld ? "Switch to Local Space" : "Switch to World Space");
            }
        }

        private void DrawSnapControls()
        {
            bool snapEnabled = TransformGizmo.SnapEnabled;
            if (ImGui.Checkbox("Snap (Ctrl)", ref snapEnabled))
            {
                TransformGizmo.SnapEnabled = snapEnabled;
            }

            ImGui.SameLine();

            if (ImGui.Button("Camera Mode"))
            {
                GraphicsEngine.Instance.GetSceneRenderer().GetCamera().ToggleProjectionMode();
            }

            if (ImGui.IsItemHovered())
            {
                string snapInfo = $"Position: {TransformGizmo.TranslateSnapValue}\n" +
                                 $"Rotation: {TransformGizmo.RotateSnapValue}°\n" +
                                 $"Scale: {TransformGizmo.ScaleSnapValue}";
                ImGui.SetTooltip($"Enable/Disable Snapping\nHold Ctrl to toggle temporarily\n\n{snapInfo}");
            }

            ImGui.SameLine();
            if (ImGui.Button("⚙", new Vector2(25, 0)))
            {
                ImGui.OpenPopup("SnapSettings");
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Snap Settings");
            }

            DrawSnapSettingsPopup();
        }

        private void DrawSnapSettingsPopup()
        {
            if (ImGui.BeginPopup("SnapSettings"))
            {
                ImGui.Text("Snap Settings");
                ImGui.Separator();

                float translateSnap = TransformGizmo.TranslateSnapValue;
                if (ImGui.DragFloat("Position Snap", ref translateSnap, 0.05f, 0.01f, 10.0f, "%.2f"))
                {
                    TransformGizmo.TranslateSnapValue = translateSnap;
                }

                float rotateSnap = TransformGizmo.RotateSnapValue;
                if (ImGui.DragFloat("Rotation Snap", ref rotateSnap, 1.0f, 1.0f, 90.0f, "%.1f°"))
                {
                    TransformGizmo.RotateSnapValue = rotateSnap;
                }

                float scaleSnap = TransformGizmo.ScaleSnapValue;
                if (ImGui.DragFloat("Scale Snap", ref scaleSnap, 0.01f, 0.01f, 1.0f, "%.2f"))
                {
                    TransformGizmo.ScaleSnapValue = scaleSnap;
                }

                ImGui.Separator();
                if (ImGui.Button("Reset to Defaults", new Vector2(-1, 0)))
                {
                    TransformGizmo.TranslateSnapValue = 0.5f;
                    TransformGizmo.RotateSnapValue = 15.0f;
                    TransformGizmo.ScaleSnapValue = 0.1f;
                }

                ImGui.EndPopup();
            }
        }

        private void DrawCameraSpeed()
        {
            if (MainCamera != null)
            {
                ImGui.Text("Speed:");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(80);

                // Usar variable local para pasar por referencia
                float speed = EditorCameraSpeed;
                if (ImGui.DragFloat("##speed", ref speed, 0.1f, 0.1f, 10.0f, "%.1f"))
                {
                    EditorCameraSpeed = speed;
                }
            }
        }

        private void DrawViewport()
        {
            Vector2 viewportSize = ImGui.GetContentRegionAvail();

            if (viewportSize.X > 0 && viewportSize.Y > 0)
            {
                if (LastViewportSize.X != viewportSize.X || LastViewportSize.Y != viewportSize.Y)
                {
                    LastViewportSize = viewportSize;
                    Engine?.ResizeSceneFrameBuffer((int)viewportSize.X, (int)viewportSize.Y);

                    if (MainCamera != null)
                    {
                        MainCamera.AspectRatio = viewportSize.X / viewportSize.Y;
                    }
                }

                var frameBuffer = Engine?.GetSceneFrameBuffer();
                if (frameBuffer != null)
                {
                    Vector2 cursorPos = ImGui.GetCursorScreenPos();

                    ImGui.Image(
                        frameBuffer.TextureId,
                        viewportSize,
                        new Vector2(0, 1),
                        new Vector2(1, 0)
                    );

                    bool isHovered = ImGui.IsItemHovered();

                    EditorGizmos.DrawOrientationGizmo(cursorPos, viewportSize, MainCamera);

                    if (SelectedObject != null && MainCamera != null)
                    {
                        TransformGizmo.Draw(SelectedObject, MainCamera, cursorPos, viewportSize, isHovered);
                    }
                }
            }
        }
    }
}