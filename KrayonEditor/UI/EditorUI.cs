using ImGuiNET;
using KrayonCore;
using System.Collections.Generic;
using System.Numerics;
using Vector2 = System.Numerics.Vector2;

namespace KrayonEditor.UI
{
    internal static class EditorUI
    {
        private static MainMenuBarUI _mainMenuBar = new MainMenuBarUI();
        private static DockSpaceUI _dockSpace = new DockSpaceUI();
        private static HierarchyUI _hierarchy = new HierarchyUI();
        private static InspectorUI _inspector = new InspectorUI();
        private static SceneViewUI _sceneView = new SceneViewUI();
        private static ConsoleUI _console = new ConsoleUI();
        private static StatsUI _stats = new StatsUI();
        private static AssetsUI _assets = new AssetsUI();
        private static MaterialUI _materials = new MaterialUI();
        private static TileEditor _TileEditor = new TileEditor();
        private static SpriteAnimationUI _SpriteAnimator= new SpriteAnimationUI();

        public static void Initialize()
        {
            SetupImGuiStyle();
        }

        public static void Draw(
            GraphicsEngine? engine,
            Camera? mainCamera,
            bool isPlaying,
            float editorCameraSpeed,
            Vector2 lastSceneViewportSize,
            double currentFps,
            double currentFrameTime,
            List<string> consoleMessages,
            out bool newIsPlaying,
            out float newEditorCameraSpeed,
            out Vector2 newLastSceneViewportSize)
        {
            _sceneView.Engine = engine;
            _sceneView.MainCamera = mainCamera;
            _sceneView.IsPlaying = isPlaying;
            _sceneView.EditorCameraSpeed = editorCameraSpeed;
            _sceneView.LastViewportSize = lastSceneViewportSize;
            _console.Messages = consoleMessages;
            _stats.MainCamera = mainCamera;
            _stats.CurrentFps = currentFps;
            _stats.CurrentFrameTime = currentFrameTime;

            // Sincronizar visibilidad con el menú
            _hierarchy.IsVisible = _mainMenuBar.ShowHierarchy;
            _inspector.IsVisible = _mainMenuBar.ShowInspector;
            _console.IsVisible = _mainMenuBar.ShowConsole;
            _stats.IsVisible = _mainMenuBar.ShowStats;
            _assets.IsVisible = _mainMenuBar.ShowAssets;

            // Dibujar UI
            _mainMenuBar.OnDrawUI();
            _dockSpace.OnDrawUI();
            _hierarchy.OnDrawUI();
            _inspector.OnDrawUI();
            _sceneView.OnDrawUI();
            _console.OnDrawUI();
            _stats.OnDrawUI();
            _assets.OnDrawUI();
            _SpriteAnimator.OnDrawUI();
            _materials.OnDrawUI();
            _TileEditor.OnDrawUI();

            _mainMenuBar.ShowHierarchy = _hierarchy.IsVisible;
            _mainMenuBar.ShowInspector = _inspector.IsVisible;
            _mainMenuBar.ShowConsole = _console.IsVisible;
            _mainMenuBar.ShowStats = _stats.IsVisible;
            _mainMenuBar.ShowAssets = _assets.IsVisible;

            newIsPlaying = _sceneView.IsPlaying;
            newEditorCameraSpeed = _sceneView.EditorCameraSpeed;
            newLastSceneViewportSize = _sceneView.LastViewportSize;

            if (GraphicsEngine.Instance.GetKeyboardState().IsKeyDown(OpenTK.Windowing.GraphicsLibraryFramework.Keys.LeftControl) && GraphicsEngine.Instance.GetKeyboardState().IsKeyPressed(OpenTK.Windowing.GraphicsLibraryFramework.Keys.D))
            {
                if (EditorActions.SelectedObject != null)
                {
                    GameObject clone = SceneManager.ActiveScene.Instantiate(EditorActions.SelectedObject);
                    EditorActions.SelectedObject = clone;
                }
            }
        }

        private static void SetupImGuiStyle()
        {
            var style = ImGui.GetStyle();

            // Configuración de estilo
            style.WindowPadding = new Vector2(8, 8);
            style.FramePadding = new Vector2(5, 4);
            style.CellPadding = new Vector2(4, 2);
            style.ItemSpacing = new Vector2(8, 4);
            style.TouchExtraPadding = new Vector2(0, 0);
            style.IndentSpacing = 21;
            style.ScrollbarSize = 14;
            style.GrabMinSize = 8;

            style.WindowBorderSize = 1;
            style.ChildBorderSize = 1;
            style.PopupBorderSize = 1;
            style.FrameBorderSize = 0;
            style.TabBorderSize = 0;

            style.WindowRounding = 4;
            style.ChildRounding = 4;
            style.FrameRounding = 3;
            style.PopupRounding = 4;
            style.ScrollbarRounding = 4;
            style.GrabRounding = 3;
            style.TabRounding = 3;
            style.LogSliderDeadzone = 4;

            var colors = style.Colors;

            colors[(int)ImGuiCol.WindowBg] = new Vector4(0.12f, 0.15f, 0.22f, 1.0f);           
            colors[(int)ImGuiCol.ChildBg] = new Vector4(0.10f, 0.12f, 0.18f, 1.0f);            
            colors[(int)ImGuiCol.PopupBg] = new Vector4(0.14f, 0.17f, 0.24f, 0.98f);          
            colors[(int)ImGuiCol.Border] = new Vector4(0.08f, 0.10f, 0.14f, 1.0f);              
            colors[(int)ImGuiCol.BorderShadow] = new Vector4(0.0f, 0.0f, 0.0f, 0.0f);

            // Barras de título y menú
            colors[(int)ImGuiCol.TitleBg] = new Vector4(0.10f, 0.12f, 0.18f, 1.0f);             // #1A1F2E
            colors[(int)ImGuiCol.TitleBgActive] = new Vector4(0.15f, 0.18f, 0.27f, 1.0f);       // #262E45 - Deep Blue
            colors[(int)ImGuiCol.TitleBgCollapsed] = new Vector4(0.10f, 0.12f, 0.18f, 1.0f);
            colors[(int)ImGuiCol.MenuBarBg] = new Vector4(0.15f, 0.18f, 0.27f, 1.0f);           // #262E45

            // Frames (inputs, boxes)
            colors[(int)ImGuiCol.FrameBg] = new Vector4(0.10f, 0.12f, 0.18f, 1.0f);             // #1A1F2E
            colors[(int)ImGuiCol.FrameBgHovered] = new Vector4(0.18f, 0.22f, 0.32f, 1.0f);      // #2E3852 - Ocean Blue
            colors[(int)ImGuiCol.FrameBgActive] = new Vector4(0.20f, 0.25f, 0.35f, 1.0f);

            // Tabs
            colors[(int)ImGuiCol.Tab] = new Vector4(0.15f, 0.18f, 0.27f, 1.0f);                 // #262E45
            colors[(int)ImGuiCol.TabHovered] = new Vector4(0.18f, 0.22f, 0.32f, 1.0f);          // #2E3852
            colors[(int)ImGuiCol.TabSelected] = new Vector4(0.12f, 0.15f, 0.22f, 1.0f);         // #1F2638
            colors[(int)ImGuiCol.TabSelectedOverline] = new Vector4(0.26f, 0.77f, 0.85f, 1.0f); // #42C5D9 - CYAN ✨
            colors[(int)ImGuiCol.TabDimmed] = new Vector4(0.10f, 0.12f, 0.18f, 1.0f);
            colors[(int)ImGuiCol.TabDimmedSelected] = new Vector4(0.15f, 0.18f, 0.27f, 1.0f);
            colors[(int)ImGuiCol.TabDimmedSelectedOverline] = new Vector4(0.20f, 0.60f, 0.68f, 1.0f);

            // Scrollbar
            colors[(int)ImGuiCol.ScrollbarBg] = new Vector4(0.10f, 0.12f, 0.18f, 1.0f);
            colors[(int)ImGuiCol.ScrollbarGrab] = new Vector4(0.18f, 0.22f, 0.32f, 1.0f);
            colors[(int)ImGuiCol.ScrollbarGrabHovered] = new Vector4(0.24f, 0.28f, 0.38f, 1.0f);
            colors[(int)ImGuiCol.ScrollbarGrabActive] = new Vector4(0.26f, 0.77f, 0.85f, 1.0f); // CYAN ✨

            // Sliders
            colors[(int)ImGuiCol.SliderGrab] = new Vector4(0.26f, 0.77f, 0.85f, 1.0f);          // #42C5D9 - CYAN ✨
            colors[(int)ImGuiCol.SliderGrabActive] = new Vector4(0.40f, 0.88f, 0.95f, 1.0f);    // #66E0F2 - CYAN BRILLANTE ✨

            // Buttons
            colors[(int)ImGuiCol.Button] = new Vector4(0.18f, 0.22f, 0.32f, 1.0f);              // #2E3852
            colors[(int)ImGuiCol.ButtonHovered] = new Vector4(0.26f, 0.77f, 0.85f, 0.5f);       // CYAN semi-transparente ✨
            colors[(int)ImGuiCol.ButtonActive] = new Vector4(0.26f, 0.77f, 0.85f, 1.0f);        // CYAN ✨

            // Headers (CollapsingHeader, TreeNode)
            colors[(int)ImGuiCol.Header] = new Vector4(0.18f, 0.22f, 0.32f, 1.0f);
            colors[(int)ImGuiCol.HeaderHovered] = new Vector4(0.26f, 0.77f, 0.85f, 0.3f);       // CYAN suave ✨
            colors[(int)ImGuiCol.HeaderActive] = new Vector4(0.26f, 0.77f, 0.85f, 0.5f);        // CYAN medio ✨

            // Separators
            colors[(int)ImGuiCol.Separator] = new Vector4(0.08f, 0.10f, 0.14f, 1.0f);
            colors[(int)ImGuiCol.SeparatorHovered] = new Vector4(0.26f, 0.77f, 0.85f, 1.0f);    // CYAN ✨
            colors[(int)ImGuiCol.SeparatorActive] = new Vector4(0.40f, 0.88f, 0.95f, 1.0f);     // CYAN BRILLANTE ✨

            // Resize grip
            colors[(int)ImGuiCol.ResizeGrip] = new Vector4(0.26f, 0.77f, 0.85f, 0.2f);
            colors[(int)ImGuiCol.ResizeGripHovered] = new Vector4(0.26f, 0.77f, 0.85f, 0.6f);
            colors[(int)ImGuiCol.ResizeGripActive] = new Vector4(0.26f, 0.77f, 0.85f, 1.0f);

            // CheckMark y Text
            colors[(int)ImGuiCol.CheckMark] = new Vector4(0.40f, 0.88f, 0.95f, 1.0f);           // CYAN BRILLANTE ✨
            colors[(int)ImGuiCol.Text] = new Vector4(0.92f, 0.94f, 0.96f, 1.0f);                // #EBF0F5 - Blanco azulado
            colors[(int)ImGuiCol.TextDisabled] = new Vector4(0.45f, 0.50f, 0.57f, 1.0f);        // Gris azulado
            colors[(int)ImGuiCol.TextSelectedBg] = new Vector4(0.26f, 0.77f, 0.85f, 0.35f);     // CYAN transparente ✨

            // Docking
            colors[(int)ImGuiCol.DockingPreview] = new Vector4(0.26f, 0.77f, 0.85f, 0.7f);      // CYAN ✨
            colors[(int)ImGuiCol.DockingEmptyBg] = new Vector4(0.10f, 0.12f, 0.18f, 1.0f);

            // Plots
            colors[(int)ImGuiCol.PlotLines] = new Vector4(0.26f, 0.77f, 0.85f, 1.0f);           // CYAN ✨
            colors[(int)ImGuiCol.PlotLinesHovered] = new Vector4(0.40f, 0.88f, 0.95f, 1.0f);    // CYAN BRILLANTE ✨
            colors[(int)ImGuiCol.PlotHistogram] = new Vector4(0.51f, 0.82f, 0.78f, 1.0f);       // #82D1C7 - Aqua ✨
            colors[(int)ImGuiCol.PlotHistogramHovered] = new Vector4(0.40f, 0.88f, 0.95f, 1.0f);

            // Tables
            colors[(int)ImGuiCol.TableHeaderBg] = new Vector4(0.15f, 0.18f, 0.27f, 1.0f);
            colors[(int)ImGuiCol.TableBorderStrong] = new Vector4(0.08f, 0.10f, 0.14f, 1.0f);
            colors[(int)ImGuiCol.TableBorderLight] = new Vector4(0.12f, 0.14f, 0.18f, 1.0f);
            colors[(int)ImGuiCol.TableRowBg] = new Vector4(0.0f, 0.0f, 0.0f, 0.0f);
            colors[(int)ImGuiCol.TableRowBgAlt] = new Vector4(0.26f, 0.77f, 0.85f, 0.06f);      // CYAN muy suave ✨

            // Drag & Drop
            colors[(int)ImGuiCol.DragDropTarget] = new Vector4(0.40f, 0.88f, 0.95f, 1.0f);      // CYAN BRILLANTE ✨

            // Navigation
            colors[(int)ImGuiCol.NavWindowingHighlight] = new Vector4(0.26f, 0.77f, 0.85f, 0.7f);
            colors[(int)ImGuiCol.NavWindowingDimBg] = new Vector4(0.0f, 0.0f, 0.0f, 0.3f);

            // Modal
            colors[(int)ImGuiCol.ModalWindowDimBg] = new Vector4(0.0f, 0.0f, 0.0f, 0.5f);
        }
    }
}