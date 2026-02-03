using ImGuiNET;
using Vector4 = System.Numerics.Vector4;

namespace KrayonEditor.UI
{
    public class AssetsUI : UIBehaviour
    {
        public override void OnDrawUI()
        {
            if (!_isVisible) return;

            ImGui.Begin("Assets", ref _isVisible);

            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.7f, 0.7f, 0.7f, 1.0f));
            ImGui.Text("Project Assets");
            ImGui.PopStyleColor();
            ImGui.Separator();

            if (ImGui.TreeNode("Models"))
            {
                ImGui.Selectable("Cube.fbx");
                ImGui.TreePop();
            }

            if (ImGui.TreeNode("Textures"))
            {
                ImGui.TextDisabled("(empty)");
                ImGui.TreePop();
            }

            if (ImGui.TreeNode("Materials"))
            {
                ImGui.TextDisabled("(empty)");
                ImGui.TreePop();
            }

            if (ImGui.TreeNode("Scripts"))
            {
                ImGui.TextDisabled("(empty)");
                ImGui.TreePop();
            }

            ImGui.End();
        }
    }
}