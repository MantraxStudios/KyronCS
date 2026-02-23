using ImGuiNET;
using KrayonCore;
using KrayonCore.GraphicsData;
using KrayonEditor.Main;
using System.Numerics;

namespace KrayonEditor.UI
{
    public class InspectorUI : UIBehaviour
    {
        public override void OnDrawUI()
        {
            if (!_isVisible) return;

            ImGui.Begin("Inspector", ref _isVisible);

            if (EditorActions.SelectedObject != null)
            {
                DrawObjectHeader();
                ImGui.Spacing();
                ComponentInspector.DrawTransform(EditorActions.SelectedObject.Transform);

                var components = EditorActions.SelectedObject.GetAllComponents().ToList();
                int idx = 0;
                foreach (var comp in components)
                {
                    if (comp.GetType().Name == "Transform") { idx++; continue; }
                    ImGui.PushID($"Comp_{idx}");
                    ComponentInspector.DrawComponentWithReflection(comp, EditorActions.SelectedObject);
                    ImGui.PopID();
                    idx++;
                }

                ImGui.Spacing();
                ComponentInspector.DrawAddComponentButton(EditorActions.SelectedObject);
            }
            else
            {
                ImGui.TextUnformatted("No object selected");
            }

            ImGui.End();

            if (EditorActions.SelectedObject != null &&
                GraphicsEngine.Instance.GetKeyboardState()
                    .IsKeyPressed(OpenTK.Windowing.GraphicsLibraryFramework.Keys.Delete) &&
                EditorActions.IsHoveringScene)
            {
                SceneManager.PrimaryScene.DestroyGameObject(EditorActions.SelectedObject);
                EditorActions.SelectedObject = null;
            }
        }

        private void DrawObjectHeader()
        {
            float avail = ImGui.GetContentRegionAvail().X;
            float tagW = 90f;
            float spacing = ImGui.GetStyle().ItemSpacing.X;
            float nameW = avail - tagW - spacing;

            ImGui.SetNextItemWidth(nameW);
            string name = EditorActions.SelectedObject!.Name;
            if (ImGui.InputText("##obj_name", ref name, 256))
                EditorActions.SelectedObject.Name = name;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Name");

            ImGui.SameLine();
            ImGui.SetNextItemWidth(tagW);
            string tag = EditorActions.SelectedObject.Tag;
            if (ImGui.InputText("##obj_tag", ref tag, 128))
                EditorActions.SelectedObject.Tag = tag;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Tag");

            ImGui.Spacing();
            ImGui.Separator();
        }
    }
}