using ImGuiNET;
using System.Numerics;
using System.Runtime.InteropServices;

public static class EditorLayout
{
    private static bool _layoutInitialized = false;

    // DockBuilder no está en ImGuiNET, hay que importarlo manualmente
    [DllImport("cimgui", CallingConvention = CallingConvention.Cdecl)]
    private static extern void igDockBuilderRemoveNode(uint node_id);

    [DllImport("cimgui", CallingConvention = CallingConvention.Cdecl)]
    private static extern void igDockBuilderAddNode(uint node_id, uint flags);

    [DllImport("cimgui", CallingConvention = CallingConvention.Cdecl)]
    private static extern void igDockBuilderSetNodeSize(uint node_id, Vector2 size);

    [DllImport("cimgui", CallingConvention = CallingConvention.Cdecl)]
    private static extern uint igDockBuilderSplitNode(uint node_id, ImGuiDir split_dir, float size_ratio_for_node_at_dir, out uint out_id_at_dir, out uint out_id_at_opposite_dir);

    [DllImport("cimgui", CallingConvention = CallingConvention.Cdecl)]
    private static extern void igDockBuilderDockWindow(byte[] window_name, uint node_id);

    [DllImport("cimgui", CallingConvention = CallingConvention.Cdecl)]
    private static extern void igDockBuilderFinish(uint node_id);

    public static void Draw()
    {
        uint dockspaceId = ImGui.GetID("MainDockSpace");

        // Primero construir el layout si es necesario
        if (!_layoutInitialized)
        {
            _layoutInitialized = true;
            BuildLayout(dockspaceId);
        }

        ImGui.DockSpace(dockspaceId);

        ImGui.Begin("Left");
        ImGui.Text("Panel izquierdo");
        ImGui.End();

        ImGui.Begin("Center");
        ImGui.Text("Panel central");
        ImGui.End();

        ImGui.Begin("Right");
        ImGui.Text("Panel derecho");
        ImGui.End();
    }

    private static void BuildLayout(uint dockspaceId)
    {
        var viewport = ImGui.GetMainViewport();

        igDockBuilderRemoveNode(dockspaceId);

        // 1024 = ImGuiDockNodeFlags_DockSpace, necesario para que funcione como dockspace
        igDockBuilderAddNode(dockspaceId, 1024);
        igDockBuilderSetNodeSize(dockspaceId, viewport.Size);

        uint dockLeft, dockCenter, dockRight;
        uint dockMain = dockspaceId;

        igDockBuilderSplitNode(dockMain, ImGuiDir.Left, 0.2f, out dockLeft, out dockMain);
        igDockBuilderSplitNode(dockMain, ImGuiDir.Right, 0.25f, out dockRight, out dockCenter);

        igDockBuilderDockWindow(GetBytes("Left"), dockLeft);
        igDockBuilderDockWindow(GetBytes("Center"), dockCenter);
        igDockBuilderDockWindow(GetBytes("Right"), dockRight);

        igDockBuilderFinish(dockspaceId);
    }

    // ImGui usa strings UTF-8 terminadas en null
    private static byte[] GetBytes(string str)
    {
        byte[] bytes = new byte[str.Length + 1];
        System.Text.Encoding.UTF8.GetBytes(str, 0, str.Length, bytes, 0);
        return bytes;
    }
}