using KrayonCore;
using ImGuiNET;
using Vector2 = System.Numerics.Vector2;
using Vector4 = System.Numerics.Vector4;

namespace KrayonEditor.UI
{
    internal static class EditorGizmos
    {
        public static void DrawOrientationGizmo(Vector2 viewportPos, Vector2 viewportSize, CameraComponent? mainCamera)
        {
            if (mainCamera == null) return;

            float gizmoSize = 80;
            float padding = 15;
            Vector2 gizmoCenter = new Vector2(
                viewportPos.X + viewportSize.X - gizmoSize / 2 - padding,
                viewportPos.Y + gizmoSize / 2 + padding
            );

            ImDrawListPtr drawList = ImGui.GetWindowDrawList();

            var forward = mainCamera.Front;
            var right = mainCamera.Right;
            var up = mainCamera.Up;

            float axisLength = 35;

            Vector2 xEnd = new Vector2(
                gizmoCenter.X + right.X * axisLength,
                gizmoCenter.Y - right.Y * axisLength
            );
            Vector2 yEnd = new Vector2(
                gizmoCenter.X + up.X * axisLength,
                gizmoCenter.Y - up.Y * axisLength
            );
            Vector2 zEnd = new Vector2(
                gizmoCenter.X - forward.X * axisLength,
                gizmoCenter.Y + forward.Y * axisLength
            );

            drawList.AddCircleFilled(gizmoCenter, gizmoSize / 2,
                ImGui.GetColorU32(new Vector4(0.1f, 0.1f, 0.1f, 0.7f)), 32);

            drawList.AddCircle(gizmoCenter, gizmoSize / 2,
                ImGui.GetColorU32(new Vector4(0.3f, 0.3f, 0.3f, 0.8f)), 32, 2.0f);

            uint xColor = ImGui.GetColorU32(new Vector4(0.8f, 0.2f, 0.2f, 1.0f));
            uint yColor = ImGui.GetColorU32(new Vector4(0.3f, 0.8f, 0.3f, 1.0f));
            uint zColor = ImGui.GetColorU32(new Vector4(0.2f, 0.4f, 0.9f, 1.0f));

            float axisThickness = 3.0f;

            if (right.Z > 0)
            {
                drawList.AddLine(gizmoCenter, xEnd, xColor, axisThickness);
                DrawAxisLabel(drawList, xEnd, "X", xColor);
            }
            if (up.Z > 0)
            {
                drawList.AddLine(gizmoCenter, yEnd, yColor, axisThickness);
                DrawAxisLabel(drawList, yEnd, "Y", yColor);
            }
            if (-forward.Z > 0)
            {
                drawList.AddLine(gizmoCenter, zEnd, zColor, axisThickness);
                DrawAxisLabel(drawList, zEnd, "Z", zColor);
            }

            if (right.Z <= 0)
            {
                uint xDimColor = ImGui.GetColorU32(new Vector4(0.4f, 0.1f, 0.1f, 0.5f));
                drawList.AddLine(gizmoCenter, xEnd, xDimColor, axisThickness * 0.7f);
                DrawAxisLabel(drawList, xEnd, "X", xDimColor);
            }
            if (up.Z <= 0)
            {
                uint yDimColor = ImGui.GetColorU32(new Vector4(0.15f, 0.4f, 0.15f, 0.5f));
                drawList.AddLine(gizmoCenter, yEnd, yDimColor, axisThickness * 0.7f);
                DrawAxisLabel(drawList, yEnd, "Y", yDimColor);
            }
            if (-forward.Z <= 0)
            {
                uint zDimColor = ImGui.GetColorU32(new Vector4(0.1f, 0.2f, 0.45f, 0.5f));
                drawList.AddLine(gizmoCenter, zEnd, zDimColor, axisThickness * 0.7f);
                DrawAxisLabel(drawList, zEnd, "Z", zDimColor);
            }
        }

        private static void DrawAxisLabel(ImDrawListPtr drawList, Vector2 position, string label, uint color)
        {
            Vector2 textSize = ImGui.CalcTextSize(label);
            Vector2 textPos = new Vector2(position.X - textSize.X / 2, position.Y - textSize.Y / 2);

            float bgSize = 16;
            drawList.AddCircleFilled(position, bgSize / 2,
                ImGui.GetColorU32(new Vector4(0.15f, 0.15f, 0.15f, 0.9f)), 16);

            drawList.AddText(textPos, color, label);
        }
    }
}