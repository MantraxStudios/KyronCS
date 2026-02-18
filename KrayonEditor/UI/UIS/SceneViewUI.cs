using ImGuiNET;
using KrayonCore.EventSystem;
using KrayonCore.GraphicsData;
using KrayonCore;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;
using KrayonCore.Graphics.Camera;
using KrayonEditor.Main;

namespace KrayonEditor.UI
{
    public class SceneViewUI : UIBehaviour
    {
        public GraphicsEngine? Engine { get; set; }
        public Camera? MainCamera { get; set; }
        public bool IsPlaying { get; set; }
        public float EditorCameraSpeed { get; set; } = 5.0f;
        public Vector2 LastViewportSize { get; set; }

        private static readonly Vector4 ActiveButtonColor = new(0.3f, 0.5f, 0.8f, 1.0f);
        private static readonly Vector4 ActiveButtonHoveredColor = new(0.4f, 0.6f, 0.9f, 1.0f);
        private static readonly Vector4 ActiveButtonPressedColor = new(0.2f, 0.4f, 0.7f, 1.0f);
        private static readonly Vector2 ToolbarIconSize = new(20, 20);

        public SceneViewUI()
        {
            IconManager.Initialize();
        }

        public override void OnDrawUI()
        {
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
            ImGui.Begin("Scene", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
            ImGui.PopStyleVar();

            DrawToolbar();
            DrawViewport();

            ImGui.End();

            ImGui.Begin("Game", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
            DrawGameViewPort();
            ImGui.End();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  TOOLBAR
        // ─────────────────────────────────────────────────────────────────────

        private void DrawToolbar()
        {
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(4, 4));
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8, 4));
            ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.125f, 0.125f, 0.125f, 1.0f));
            ImGui.BeginChild("SceneToolbar", new Vector2(0, 46), ImGuiChildFlags.Borders);

            ImGui.SetCursorPosY(7);

            DrawTransformButtons(); Separator();
            DrawSpaceButton(); Separator();
            DrawViewButtons(); Separator();
            DrawSnapControls(); Separator();
            DrawVFXButton(); Separator();
            DrawCameraSpeed();

            ImGui.EndChild();
            ImGui.PopStyleColor();
            ImGui.PopStyleVar(2);
        }

        private static void Separator()
        {
            ImGui.SameLine();
            float savedY = ImGui.GetCursorPosY();
            ImGui.SetCursorPosY(4);
            ImGui.PushStyleColor(ImGuiCol.Separator, new Vector4(0.4f, 0.4f, 0.4f, 1.0f));
            ImGui.Dummy(new Vector2(1, 38));
            ImGui.PopStyleColor();
            ImGui.SameLine();
            ImGui.SetCursorPosY(savedY);
            ImGui.SameLine();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  TRANSFORM BUTTONS
        // ─────────────────────────────────────────────────────────────────────

        private void DrawTransformButtons()
        {
            DrawGizmoButton("##Move", "move", GizmoMode.Translate, "Move (Q)");
            ImGui.SameLine();
            DrawGizmoButton("##Rotate", "rotate", GizmoMode.Rotate, "Rotate (E)");
            ImGui.SameLine();
            DrawGizmoButton("##Scale", "scale", GizmoMode.Scale, "Scale (R)");
        }

        private void DrawGizmoButton(string id, string iconName, GizmoMode mode, string tooltip)
        {
            bool active = TransformGizmo.CurrentMode == mode;

            if (active) PushActiveButtonColors();

            if (ImGui.ImageButton(id, IconManager.GetIcon(iconName), ToolbarIconSize))
                TransformGizmo.SetMode(mode);

            if (active) ImGui.PopStyleColor(3);

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(tooltip);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  SPACE BUTTON
        // ─────────────────────────────────────────────────────────────────────

        private void DrawSpaceButton()
        {
            bool isWorld = TransformGizmo.CurrentSpace == GizmoSpace.World;

            if (!isWorld)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.5f, 0.3f, 0.8f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.6f, 0.4f, 0.9f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.4f, 0.2f, 0.7f, 1.0f));
            }

            if (ImGui.Button(isWorld ? "World (X)" : "Local (X)", new Vector2(80, 32)))
                TransformGizmo.ToggleSpace();

            if (!isWorld) ImGui.PopStyleColor(3);

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(isWorld ? "Switch to Local Space" : "Switch to World Space");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  VIEW BUTTONS
        // ─────────────────────────────────────────────────────────────────────

        private void DrawViewButtons()
        {
            var renderer = GraphicsEngine.Instance.GetSceneRenderer();
            bool wireframe = renderer.WireframeMode;
            Vector2 buttonSize = new(75, 32);

            if (ImGui.Button("Camera", buttonSize))
                renderer.GetCamera().ToggleProjectionMode();

            if (ImGui.IsItemHovered())
            {
                string mode = renderer.GetCamera().IsPerspective ? "Perspective" : "Orthographic";
                ImGui.SetTooltip($"{mode}\nClick to toggle");
            }

            ImGui.SameLine();

            if (wireframe) PushActiveButtonColors();

            if (ImGui.Button("Wireframe", buttonSize))
                renderer.ToggleWireframe();

            if (wireframe) ImGui.PopStyleColor(3);

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(wireframe ? "Wireframe ON\nClick to disable" : "Wireframe OFF\nClick to enable");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  SNAP CONTROLS
        // ─────────────────────────────────────────────────────────────────────

        private void DrawSnapControls()
        {
            bool snapEnabled = TransformGizmo.SnapEnabled;

            ImGui.PushStyleColor(ImGuiCol.Text, snapEnabled
                ? new Vector4(0.3f, 0.8f, 0.3f, 1.0f)
                : new Vector4(0.6f, 0.6f, 0.6f, 1.0f));

            if (ImGui.Checkbox("Snap", ref snapEnabled))
                TransformGizmo.SnapEnabled = snapEnabled;

            ImGui.SameLine();

            bool showAll = EngineEditor.ShowAllGizmos;
            if (ImGui.Checkbox("Show All Gizmos", ref showAll))
                EngineEditor.ShowAllGizmos = showAll;

            ImGui.PopStyleColor();

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(
                    $"Position: {TransformGizmo.TranslateSnapValue}\n" +
                    $"Rotation: {TransformGizmo.RotateSnapValue}°\n" +
                    $"Scale: {TransformGizmo.ScaleSnapValue}\n\n" +
                    "Hold Ctrl to toggle temporarily");
            }

            ImGui.SameLine();

            if (ImGui.Button("Settings", new Vector2(70, 0)))
                ImGui.OpenPopup("SnapSettings");

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Snap Settings");

            DrawSnapSettingsPopup();
        }

        private void DrawSnapSettingsPopup()
        {
            if (!ImGui.BeginPopup("SnapSettings")) return;

            ImGui.Text("Snap Settings");
            ImGui.Separator();

            // FIX: variables locales en lugar de pasar propiedades como ref
            float translateSnap = TransformGizmo.TranslateSnapValue;
            if (ImGui.DragFloat("Position Snap", ref translateSnap, 0.05f, 0.01f, 10.0f, "%.2f"))
                TransformGizmo.TranslateSnapValue = translateSnap;

            float rotateSnap = TransformGizmo.RotateSnapValue;
            if (ImGui.DragFloat("Rotation Snap", ref rotateSnap, 1.0f, 1.0f, 90.0f, "%.1f°"))
                TransformGizmo.RotateSnapValue = rotateSnap;

            float scaleSnap = TransformGizmo.ScaleSnapValue;
            if (ImGui.DragFloat("Scale Snap", ref scaleSnap, 0.01f, 0.01f, 1.0f, "%.2f"))
                TransformGizmo.ScaleSnapValue = scaleSnap;

            ImGui.Separator();

            if (ImGui.Button("Reset to Defaults", new Vector2(-1, 0)))
            {
                TransformGizmo.TranslateSnapValue = 0.5f;
                TransformGizmo.RotateSnapValue = 15.0f;
                TransformGizmo.ScaleSnapValue = 0.1f;
            }

            ImGui.EndPopup();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  VFX BUTTON
        // ─────────────────────────────────────────────────────────────────────

        private void DrawVFXButton()
        {
            var pp = GraphicsEngine.Instance?.PostProcessing;
            if (pp == null) return;

            ImGui.PushStyleColor(ImGuiCol.Text, pp.Enabled
                ? new Vector4(0.8f, 0.3f, 0.8f, 1.0f)
                : new Vector4(0.6f, 0.6f, 0.6f, 1.0f));

            bool enabled = pp.Enabled;
            if (ImGui.Checkbox("VFX", ref enabled))
                pp.Enabled = enabled;

            ImGui.PopStyleColor();

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Post Processing Effects");

            ImGui.SameLine();

            if (ImGui.Button("VFX Settings", new Vector2(90, 0)))
                ImGui.OpenPopup("VFXSettings");

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("VFX Settings");

            DrawVFXSettingsPopup();
        }

        private void DrawVFXSettingsPopup()
        {
            var pp = GraphicsEngine.Instance?.PostProcessing;
            if (pp == null) return;
            if (!ImGui.BeginPopup("VFXSettings")) return;

            ImGui.Text("Post Processing Settings");
            ImGui.Separator();

            if (ImGui.CollapsingHeader("Color Correction", ImGuiTreeNodeFlags.DefaultOpen))
            {
                bool cc = pp.ColorCorrectionEnabled;
                if (ImGui.Checkbox("Enable##CC", ref cc)) pp.ColorCorrectionEnabled = cc;
                ImGui.Spacing();

                // FIX: variables locales para cada propiedad de pp
                float brightness = pp.Brightness;
                if (ImGui.SliderFloat("Brightness", ref brightness, -1.0f, 1.0f, "%.2f"))
                    pp.Brightness = brightness;

                float contrast = pp.Contrast;
                if (ImGui.SliderFloat("Contrast", ref contrast, 0.0f, 2.0f, "%.2f"))
                    pp.Contrast = contrast;

                float saturation = pp.Saturation;
                if (ImGui.SliderFloat("Saturation", ref saturation, 0.0f, 2.0f, "%.2f"))
                    pp.Saturation = saturation;

                var cf = new Vector3(pp.ColorFilter.X, pp.ColorFilter.Y, pp.ColorFilter.Z);
                if (ImGui.ColorEdit3("Color Filter", ref cf))
                    pp.ColorFilter = new OpenTK.Mathematics.Vector3(cf.X, cf.Y, cf.Z);
            }

            if (ImGui.CollapsingHeader("Bloom"))
            {
                bool bloom = pp.BloomEnabled;
                if (ImGui.Checkbox("Enable##Bloom", ref bloom)) pp.BloomEnabled = bloom;
                ImGui.Spacing();

                float bloomThreshold = pp.BloomThreshold;
                if (ImGui.SliderFloat("Threshold", ref bloomThreshold, 0.0f, 2.0f, "%.2f"))
                    pp.BloomThreshold = bloomThreshold;

                float bloomSoftThreshold = pp.BloomSoftThreshold;
                if (ImGui.SliderFloat("Soft Threshold", ref bloomSoftThreshold, 0.0f, 1.0f, "%.2f"))
                    pp.BloomSoftThreshold = bloomSoftThreshold;

                float bloomIntensity = pp.BloomIntensity;
                if (ImGui.SliderFloat("Intensity", ref bloomIntensity, 0.0f, 10.0f, "%.2f"))
                    pp.BloomIntensity = bloomIntensity;

                float bloomRadius = pp.BloomRadius;
                if (ImGui.SliderFloat("Radius", ref bloomRadius, 1.0f, 10.0f, "%.1f"))
                    pp.BloomRadius = bloomRadius;
            }

            if (ImGui.CollapsingHeader("Film Grain"))
            {
                bool grain = pp.GrainEnabled;
                if (ImGui.Checkbox("Enable##Grain", ref grain)) pp.GrainEnabled = grain;
                ImGui.Spacing();

                float grainIntensity = pp.GrainIntensity;
                if (ImGui.SliderFloat("Intensity##Grain", ref grainIntensity, 0.0f, 0.5f, "%.3f"))
                    pp.GrainIntensity = grainIntensity;

                float grainSize = pp.GrainSize;
                if (ImGui.SliderFloat("Size", ref grainSize, 0.1f, 5.0f, "%.2f"))
                    pp.GrainSize = grainSize;
            }

            if (ImGui.CollapsingHeader("SSAO (Screen Space Ambient Occlusion)"))
            {
                bool ssao = pp.SSAOEnabled;
                if (ImGui.Checkbox("Enable##SSAO", ref ssao)) pp.SSAOEnabled = ssao;
                ImGui.Spacing();

                int kernel = pp.SSAOKernelSize;
                if (ImGui.SliderInt("Kernel Size", ref kernel, 8, 64)) pp.SSAOKernelSize = kernel;

                float ssaoRadius = pp.SSAORadius;
                if (ImGui.SliderFloat("Radius##SSAO", ref ssaoRadius, 0.1f, 2.0f, "%.2f"))
                    pp.SSAORadius = ssaoRadius;

                float ssaoBias = pp.SSAOBias;
                if (ImGui.SliderFloat("Bias", ref ssaoBias, 0.001f, 0.1f, "%.4f"))
                    pp.SSAOBias = ssaoBias;

                float ssaoPower = pp.SSAOPower;
                if (ImGui.SliderFloat("Power", ref ssaoPower, 0.5f, 4.0f, "%.2f"))
                    pp.SSAOPower = ssaoPower;
            }

            ImGui.Separator();

            if (ImGui.Button("Reset to Defaults", new Vector2(-1, 0)))
                pp.Reset();

            ImGui.EndPopup();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  CAMERA SPEED
        // ─────────────────────────────────────────────────────────────────────

        private void DrawCameraSpeed()
        {
            if (MainCamera == null) return;

            ImGui.Text("Speed:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(80);

            float speed = EditorCameraSpeed;
            if (ImGui.DragFloat("##speed", ref speed, 0.1f, 0.1f, 10.0f, "%.1f"))
                EditorCameraSpeed = speed;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  GAME VIEWPORT
        // ─────────────────────────────────────────────────────────────────────

        private void DrawGameViewPort()
        {
            Vector2 viewportSize = ImGui.GetContentRegionAvail();
            if (viewportSize.X <= 0 || viewportSize.Y <= 0) return;

            var gameCamera = GetGameCamera();

            if (LastViewportSize != viewportSize)
            {
                LastViewportSize = viewportSize;

                if (gameCamera is not null && gameCamera.Name != "main")
                {
                    gameCamera.ResizeBuffer((int)viewportSize.X, (int)viewportSize.Y);
                    gameCamera.Camera.UpdateAspectRatio((int)viewportSize.X, (int)viewportSize.Y);
                }
            }

            int textureId = GetGameCameraTextureId();
            if (textureId == 0) { DrawNoCameraMessage(viewportSize); return; }

            ImGui.Image(textureId, viewportSize, new Vector2(0, 1), new Vector2(1, 0));
        }

        private void DrawNoCameraMessage(Vector2 viewportSize)
        {
            var drawList = ImGui.GetWindowDrawList();
            var cursorPos = ImGui.GetCursorScreenPos();

            drawList.AddRectFilled(
                cursorPos,
                new Vector2(cursorPos.X + viewportSize.X, cursorPos.Y + viewportSize.Y),
                ImGui.ColorConvertFloat4ToU32(new Vector4(0.08f, 0.08f, 0.08f, 1f)));

            const string line1 = "No Camera Available";
            const string line2 = "Add a GameObject with a CameraComponent to render the scene.";

            var textSize1 = ImGui.CalcTextSize(line1);
            var textSize2 = ImGui.CalcTextSize(line2);
            float totalH = textSize1.Y + 8f + textSize2.Y;
            var center = new Vector2(
                cursorPos.X + viewportSize.X * 0.5f,
                cursorPos.Y + viewportSize.Y * 0.5f);

            drawList.AddText(
                new Vector2(center.X - textSize1.X * 0.5f, center.Y - totalH * 0.5f),
                ImGui.ColorConvertFloat4ToU32(new Vector4(0.8f, 0.8f, 0.8f, 1f)),
                line1);

            drawList.AddText(
                new Vector2(center.X - textSize2.X * 0.5f, center.Y - totalH * 0.5f + textSize1.Y + 8f),
                ImGui.ColorConvertFloat4ToU32(new Vector4(0.5f, 0.5f, 0.5f, 1f)),
                line2);

            ImGui.Dummy(viewportSize);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  SCENE VIEWPORT
        // ─────────────────────────────────────────────────────────────────────

        private void DrawViewport()
        {
            Vector2 viewportSize = ImGui.GetContentRegionAvail();
            if (viewportSize.X <= 0 || viewportSize.Y <= 0) return;

            if (LastViewportSize != viewportSize)
            {
                LastViewportSize = viewportSize;
                Engine?.ResizeFrameBuffer("scene", (int)viewportSize.X, (int)viewportSize.Y);

                if (MainCamera != null)
                    MainCamera.AspectRatio = viewportSize.X / viewportSize.Y;
            }

            var frameBuffer = Engine?.GetSceneFrameBuffer();
            if (frameBuffer == null) return;

            Vector2 cursorPos = ImGui.GetCursorScreenPos();
            EditorActions.ViewPortPosition = cursorPos;
            EditorActions.ViewPortPositionOrigin = cursorPos;

            ImGui.Image(frameBuffer.ColorTexture, viewportSize, new Vector2(0, 1), new Vector2(1, 0));
            EditorActions.IsHoveringScene = ImGui.IsItemHovered();
            bool isHovered = EditorActions.IsHoveringScene;

            var mouse = GraphicsEngine.Instance.GetMouseState();

            if (mouse.IsButtonPressed(MouseButton.Left) && !TransformGizmo.IsHovering)
            {
                Vector2 relMouse = ImGui.GetMousePos() - cursorPos;
                bool inBounds = relMouse.X >= 0 && relMouse.X <= viewportSize.X &&
                                relMouse.Y >= 0 && relMouse.Y <= viewportSize.Y;

                if (inBounds)
                {
                    var tkMouse = new OpenTK.Mathematics.Vector2(relMouse.X, relMouse.Y);

                    if (!ImGui.IsKeyDown(ImGuiKey.LeftAlt))
                    {
                        var clicked = EventSystem.OnClickObject(tkMouse);
                        EditorActions.SelectedObject = clicked == EditorActions.SelectedObject ? null : clicked;
                    }
                    else
                    {
                        Camera camera = GraphicsEngine.Instance.GetSceneRenderer().GetCamera();
                        int screenW = GraphicsEngine.Instance.GetSceneFrameBuffer().Width;
                        int screenH = GraphicsEngine.Instance.GetSceneFrameBuffer().Height;
                        float gridSize = 1.0f;

                        EventSystem.ScreenToWorldRay(tkMouse, camera, screenW, screenH,
                            out OpenTK.Mathematics.Vector3 rayOrigin,
                            out OpenTK.Mathematics.Vector3 rayDir);

                        float snappedX = MathF.Round(rayOrigin.X / gridSize) * gridSize;
                        float snappedY = MathF.Round(rayOrigin.Y / gridSize) * gridSize;

                        EditorActions.CreateCubeGameObject()
                            .Transform.SetWorldPosition(new OpenTK.Mathematics.Vector3(snappedX, snappedY, 0f));
                    }
                }
            }

            EditorGizmos.DrawOrientationGizmo(cursorPos, viewportSize, MainCamera);

            if (EditorActions.SelectedObject != null && MainCamera != null)
                TransformGizmo.Draw(EditorActions.SelectedObject, MainCamera, cursorPos, viewportSize, isHovered);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  CAMERA HELPERS
        // ─────────────────────────────────────────────────────────────────────

        private RenderCamera? GetGameCamera()
        {
            var objects = SceneManager.ActiveScene?.FindGameObjectsWithComponent<CameraComponent>();
            if (objects == null) return null;

            CameraComponent? best = null;
            int bestPrio = int.MaxValue;

            foreach (var go in objects)
            {
                var comp = go.GetComponent<CameraComponent>();
                if (comp?.RenderCamera == null) continue;

                if (comp.Priority < bestPrio)
                {
                    bestPrio = comp.Priority;
                    best = comp;
                }
            }

            return best?.RenderCamera;
        }

        private int GetGameCameraTextureId()
        {
            var cam = GetGameCamera();
            if (cam == null) return 0;

            bool ppEnabled = GraphicsEngine.Instance?.PostProcessing?.Enabled == true;
            return cam.GetFinalTextureId(ppEnabled);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  HELPERS
        // ─────────────────────────────────────────────────────────────────────

        private static void PushActiveButtonColors()
        {
            ImGui.PushStyleColor(ImGuiCol.Button, ActiveButtonColor);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, ActiveButtonHoveredColor);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, ActiveButtonPressedColor);
        }
    }
}