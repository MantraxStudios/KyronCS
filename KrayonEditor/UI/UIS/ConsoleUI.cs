using ImGuiNET;
using System.Collections.Generic;
using Vector2 = System.Numerics.Vector2;

namespace KrayonEditor.UI
{
    public class ConsoleUI : UIBehaviour
    {
        public List<string> Messages { get; set; } = new List<string>();

        public override void OnDrawUI()
        {
            if (!_isVisible) return;

            ImGui.Begin("Console", ref _isVisible);

            if (ImGui.Button("Clear"))
            {
                Messages.Clear();
            }
            ImGui.SameLine();
            ImGui.TextDisabled($"({Messages.Count} messages)");

            ImGui.Separator();

            ImGui.BeginChild("ConsoleScrollRegion", new Vector2(0, 0), ImGuiChildFlags.None);
            foreach (var message in Messages)
            {
                ImGui.TextWrapped(message);
            }

            if (ImGui.GetScrollY() >= ImGui.GetScrollMaxY())
                ImGui.SetScrollHereY(1.0f);

            ImGui.EndChild();

            ImGui.End();
        }
    }
}