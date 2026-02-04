using ImGuiNET;
using KrayonCore;
using System;
using Vector2 = System.Numerics.Vector2;

namespace KrayonEditor.UI
{
    public class StatsUI : UIBehaviour
    {
        public Camera? MainCamera { get; set; }
        public double CurrentFps { get; set; }
        public double CurrentFrameTime { get; set; }

        public override void OnDrawUI()
        {
            if (!_isVisible) return;

            ImGui.Begin("Stats", ref _isVisible);

            DrawPerformanceStats();
            DrawCameraStats();
            DrawSceneStats();

            ImGui.End();
        }

        private void DrawPerformanceStats()
        {
            if (ImGui.CollapsingHeader("Performance", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.Text($"FPS: {CurrentFps:F1}");
                ImGui.Text($"Frame Time: {CurrentFrameTime:F2}ms");

                if (CurrentFps > 0)
                {
                    float fpsNormalized = Math.Clamp((float)CurrentFps / 144.0f, 0.0f, 1.0f);
                    ImGui.ProgressBar(fpsNormalized, new Vector2(-1, 0), "");
                }
            }
        }

        private void DrawCameraStats()
        {
            if (ImGui.CollapsingHeader("Camera", ImGuiTreeNodeFlags.DefaultOpen))
            {
                if (MainCamera != null)
                {
                    var pos = MainCamera.Position;
                    ImGui.Text($"Position:");
                    ImGui.SameLine();
                    ImGui.TextDisabled($"({pos.X:F2}, {pos.Y:F2}, {pos.Z:F2})");

                    ImGui.Text($"Yaw: {MainCamera.Yaw:F1}");
                    ImGui.Text($"Pitch: {MainCamera.Pitch:F1}");
                    ImGui.Text($"FOV: {MainCamera.Fov:F1}");
                }
            }
        }

        private void DrawSceneStats()
        {
            if (ImGui.CollapsingHeader("Scene", ImGuiTreeNodeFlags.DefaultOpen))
            {
                if (SceneManager.ActiveScene != null)
                {
                    ImGui.Text($"Active: {SceneManager.ActiveScene.Name}");
                    ImGui.Text($"Objects: {SceneManager.ActiveScene.GetAllGameObjects().Count}");
                }
                ImGui.Text($"Total Scenes: {SceneManager.SceneCount}");
            }
        }
    }
}