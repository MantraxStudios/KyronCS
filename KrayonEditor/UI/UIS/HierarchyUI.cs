using ImGuiNET;
using KrayonCore;

namespace KrayonEditor.UI
{
    public class HierarchyUI : UIBehaviour
    {
        public GameObject? SelectedObject { get; set; }

        public override void OnDrawUI()
        {
            if (!_isVisible) return;

            ImGui.Begin("Hierarchy", ref _isVisible);

            if (ImGui.Button("+ Create Empty"))
            {
                EditorActions.CreateEmptyGameObject();
            }
            ImGui.SameLine();
            if (ImGui.Button("+ Create Cube"))
            {
                EditorActions.CreateCubeGameObject();
            }

            ImGui.Separator();

            if (SceneManager.ActiveScene != null)
            {
                bool sceneOpen = ImGui.TreeNodeEx(
                    $"{SceneManager.ActiveScene.Name}##{SceneManager.ActiveScene.GetHashCode()}",
                    ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.OpenOnDoubleClick
                );

                if (sceneOpen)
                {
                    var allObjects = SceneManager.ActiveScene.GetAllGameObjects();
                    foreach (var go in allObjects)
                    {
                        bool isSelected = SelectedObject == go;

                        ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.Leaf |
                                                   ImGuiTreeNodeFlags.NoTreePushOnOpen |
                                                   ImGuiTreeNodeFlags.SpanAvailWidth;
                        if (isSelected)
                            flags |= ImGuiTreeNodeFlags.Selected;

                        ImGui.TreeNodeEx($"{go.Name}##{go.GetHashCode()}", flags);

                        if (ImGui.IsItemClicked())
                        {
                            SelectedObject = go;
                        }

                        if (ImGui.BeginPopupContextItem($"context_{go.GetHashCode()}"))
                        {
                            if (ImGui.MenuItem("Duplicate"))
                            {
                                EngineEditor.LogMessage($"Duplicate {go.Name}");
                            }
                            if (ImGui.MenuItem("Delete") && go.Tag != "MainCamera")
                            {
                                EditorActions.DeleteGameObject(go);
                                if (SelectedObject == go)
                                    SelectedObject = null;
                            }
                            ImGui.EndPopup();
                        }
                    }
                    ImGui.TreePop();
                }
            }

            if (ImGui.BeginPopupContextWindow("hierarchy_context"))
            {
                if (ImGui.MenuItem("Empty GameObject"))
                {
                    EditorActions.CreateEmptyGameObject();
                }

                if (ImGui.BeginMenu("Light"))
                {
                    if (ImGui.MenuItem("Directional Light"))
                    {
                        EditorActions.CreateDirectionalLight();
                    }
                    if (ImGui.MenuItem("Point Light"))
                    {
                        EditorActions.CreatePointLight();
                    }
                    if (ImGui.MenuItem("Spot Light"))
                    {
                        EditorActions.CreateSpotLight();
                    }
                    ImGui.EndMenu();
                }

                if (ImGui.MenuItem("Model"))
                {
                    EditorActions.CreateModelGameObject();
                }

                if (ImGui.MenuItem("TileRenderer"))
                {
                    EditorActions.CreateTileRendererGameObject();
                }

                ImGui.EndPopup();
            }

            ImGui.End();
        }
    }
}