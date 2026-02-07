using ImGuiNET;
using KrayonCore;
using KrayonCore.EventSystem;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Vector2 = System.Numerics.Vector2;
using Vector4 = System.Numerics.Vector4;

namespace KrayonEditor.UI
{
    public class SceneViewUI : UIBehaviour
    {
        public GraphicsEngine? Engine { get; set; }
        public Camera? MainCamera { get; set; }
        public bool IsPlaying { get; set; }
        public float EditorCameraSpeed { get; set; } = 5.0f;
        public Vector2 LastViewportSize { get; set; }

        public SceneViewUI()
        {
            IconManager.Initialize();
        }

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
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8, 4));
            ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.125f, 0.125f, 0.125f, 1.0f));
            ImGui.BeginChild("SceneToolbar", new Vector2(0, 46), ImGuiChildFlags.Borders);

            ImGui.SetCursorPosY(7);

            DrawTransformButtons();
            DrawVerticalSeparator();

            DrawSpaceButton();
            DrawVerticalSeparator();

            DrawViewButtons();
            DrawVerticalSeparator();

            DrawSnapControls();
            DrawVerticalSeparator();

            DrawCameraSpeed();

            ImGui.EndChild();
            ImGui.PopStyleColor();
            ImGui.PopStyleVar(2);
        }

        private void DrawVerticalSeparator()
        {
            ImGui.SameLine();
            float cursorY = ImGui.GetCursorPosY();
            ImGui.SetCursorPosY(4);

            ImGui.PushStyleColor(ImGuiCol.Separator, new Vector4(0.4f, 0.4f, 0.4f, 1.0f));
            ImGui.Dummy(new Vector2(1, 38));
            ImGui.SameLine();
            ImGui.PopStyleColor();

            ImGui.SetCursorPosY(cursorY);
            ImGui.SameLine();
        }

        private void DrawTransformButtons()
        {
            bool isTranslate = TransformGizmo.CurrentMode == GizmoMode.Translate;
            bool isRotate = TransformGizmo.CurrentMode == GizmoMode.Rotate;
            bool isScale = TransformGizmo.CurrentMode == GizmoMode.Scale;

            Vector2 buttonSize = new Vector2(32, 32);
            Vector2 iconSize = new Vector2(20, 20);

            // Botón Move
            if (isTranslate)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.3f, 0.5f, 0.8f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.4f, 0.6f, 0.9f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.2f, 0.4f, 0.7f, 1.0f));
            }

            IntPtr moveIcon = IconManager.GetIcon("move");
            if (ImGui.ImageButton("##Move", moveIcon, iconSize))
            {
                TransformGizmo.SetMode(GizmoMode.Translate);
            }

            if (isTranslate) ImGui.PopStyleColor(3);

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Move (Q)");
            }

            ImGui.SameLine();

            // Botón Rotate
            if (isRotate)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.3f, 0.5f, 0.8f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.4f, 0.6f, 0.9f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.2f, 0.4f, 0.7f, 1.0f));
            }

            IntPtr rotateIcon = IconManager.GetIcon("rotate");
            if (ImGui.ImageButton("##Rotate", rotateIcon, iconSize))
            {
                TransformGizmo.SetMode(GizmoMode.Rotate);
            }

            if (isRotate) ImGui.PopStyleColor(3);

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Rotate (E)");
            }

            ImGui.SameLine();

            // Botón Scale
            if (isScale)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.3f, 0.5f, 0.8f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.4f, 0.6f, 0.9f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.2f, 0.4f, 0.7f, 1.0f));
            }

            IntPtr scaleIcon = IconManager.GetIcon("scale");
            if (ImGui.ImageButton("##Scale", scaleIcon, iconSize))
            {
                TransformGizmo.SetMode(GizmoMode.Scale);
            }

            if (isScale) ImGui.PopStyleColor(3);

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Scale (R)");
            }
        }

        private void DrawSpaceButton()
        {
            bool isWorld = TransformGizmo.CurrentSpace == GizmoSpace.World;
            Vector2 buttonSize = new Vector2(80, 32);

            if (!isWorld)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.5f, 0.3f, 0.8f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.6f, 0.4f, 0.9f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.4f, 0.2f, 0.7f, 1.0f));
            }

            string text = isWorld ? "World (X)" : "Local (X)";
            if (ImGui.Button(text, buttonSize))
            {
                TransformGizmo.ToggleSpace();
            }

            if (!isWorld) ImGui.PopStyleColor(3);

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(isWorld ? "Switch to Local Space" : "Switch to World Space");
            }
        }

        private void DrawViewButtons()
        {
            Vector2 buttonSize = new Vector2(75, 32);
            bool wireframeEnabled = GraphicsEngine.Instance.GetSceneRenderer().WireframeMode;

            // Botón Camera Mode
            if (ImGui.Button("Camera", buttonSize))
            {
                GraphicsEngine.Instance.GetSceneRenderer().GetCamera().ToggleProjectionMode();
            }

            if (ImGui.IsItemHovered())
            {
                var camera = GraphicsEngine.Instance.GetSceneRenderer().GetCamera();
                string mode = camera.IsPerspective ? "Perspective" : "Orthographic";
                ImGui.SetTooltip($"{mode}\nClick to toggle");
            }

            ImGui.SameLine();

            // Botón Wireframe
            if (wireframeEnabled)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.3f, 0.5f, 0.8f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.4f, 0.6f, 0.9f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.2f, 0.4f, 0.7f, 1.0f));
            }

            if (ImGui.Button("Wireframe", buttonSize))
            {
                GraphicsEngine.Instance.GetSceneRenderer().ToggleWireframe();
            }

            if (wireframeEnabled) ImGui.PopStyleColor(3);

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(wireframeEnabled ? "Wireframe ON\nClick to disable" : "Wireframe OFF\nClick to enable");
            }
        }

        private void DrawSnapControls()
        {
            bool snapEnabled = TransformGizmo.SnapEnabled;

            Vector4 snapColor = snapEnabled ? new Vector4(0.3f, 0.8f, 0.3f, 1.0f) : new Vector4(0.6f, 0.6f, 0.6f, 1.0f);
            ImGui.PushStyleColor(ImGuiCol.Text, snapColor);

            if (ImGui.Checkbox("Snap", ref snapEnabled))
            {
                TransformGizmo.SnapEnabled = snapEnabled;
            }

            ImGui.PopStyleColor();

            if (ImGui.IsItemHovered())
            {
                string snapInfo = $"Position: {TransformGizmo.TranslateSnapValue}\n" +
                                 $"Rotation: {TransformGizmo.RotateSnapValue}°\n" +
                                 $"Scale: {TransformGizmo.ScaleSnapValue}\n\n" +
                                 "Hold Ctrl to toggle temporarily";
                ImGui.SetTooltip(snapInfo);
            }

            ImGui.SameLine();

            if (ImGui.Button("Settings", new Vector2(70, 0)))
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

                    if (GraphicsEngine.Instance.GetMouseState().IsButtonPressed(MouseButton.Left) && !TransformGizmo.IsHovering)
                    {
                        // Obtener posición del mouse global (System.Numerics.Vector2)
                        System.Numerics.Vector2 globalMousePos = ImGui.GetMousePos();

                        // Convertir a posición relativa al viewport
                        System.Numerics.Vector2 relativeMousePos = new System.Numerics.Vector2(
                            globalMousePos.X - cursorPos.X,
                            globalMousePos.Y - cursorPos.Y
                        );

                        // Verificar que el click esté dentro del viewport
                        if (relativeMousePos.X >= 0 && relativeMousePos.X <= viewportSize.X &&
                            relativeMousePos.Y >= 0 && relativeMousePos.Y <= viewportSize.Y)
                        {
                            // Convertir a OpenTK.Mathematics.Vector2
                            OpenTK.Mathematics.Vector2 openTKMousePos = new OpenTK.Mathematics.Vector2(
                                relativeMousePos.X,
                                relativeMousePos.Y
                            );

                            EditorActions.SelectedObject = EventSystem.OnClickObject(openTKMousePos) == EditorActions.SelectedObject ? null : EventSystem.OnClickObject(openTKMousePos);
                        }
                    }

                    bool isHovered = ImGui.IsItemHovered();
                    EditorGizmos.DrawOrientationGizmo(cursorPos, viewportSize, MainCamera);

                    if (EditorActions.SelectedObject != null && MainCamera != null)
                    {
                        TransformGizmo.Draw(EditorActions.SelectedObject, MainCamera, cursorPos, viewportSize, isHovered);
                    }
                }
            }
        }
    }
}