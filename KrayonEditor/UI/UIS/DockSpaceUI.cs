using ImGuiNET;
using Vector2 = System.Numerics.Vector2;

namespace KrayonEditor.UI
{
    public class DockSpaceUI : UIBehaviour
    {
        public override void OnDrawUI()
        {
            ImGuiWindowFlags windowFlags = ImGuiWindowFlags.NoDocking |
                                          ImGuiWindowFlags.NoTitleBar |
                                          ImGuiWindowFlags.NoCollapse |
                                          ImGuiWindowFlags.NoResize |
                                          ImGuiWindowFlags.NoMove |
                                          ImGuiWindowFlags.NoBringToFrontOnFocus |
                                          ImGuiWindowFlags.NoNavFocus;

            ImGuiViewportPtr viewport = ImGui.GetMainViewport();
            ImGui.SetNextWindowPos(viewport.WorkPos);
            ImGui.SetNextWindowSize(viewport.WorkSize);
            ImGui.SetNextWindowViewport(viewport.ID);

            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0.0f);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0.0f);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0.0f, 0.0f));

            ImGui.Begin("DockSpaceWindow", windowFlags);
            ImGui.PopStyleVar(3);

            uint dockspaceId = ImGui.GetID("MainDockSpace");
            ImGui.DockSpace(dockspaceId, new Vector2(0, 0), ImGuiDockNodeFlags.None);

            ImGui.End();
        }
    }
}