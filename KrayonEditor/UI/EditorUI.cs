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
                GameObject clone = SceneManager.ActiveScene.Instantiate(EditorActions.SelectedObject);
            }
        }

        private static void SetupImGuiStyle()
        {
            var style = ImGui.GetStyle();

            style.WindowPadding = new Vector2(8, 8);
            style.FramePadding = new Vector2(5, 4);
            style.CellPadding = new Vector2(4, 2);
            style.ItemSpacing = new Vector2(8, 4);
            style.TouchExtraPadding = new Vector2(0, 0);
            style.IndentSpacing = 21;
            style.ScrollbarSize = 14;
            style.GrabMinSize = 8;

            // Bordes
            style.WindowBorderSize = 1;
            style.ChildBorderSize = 1;
            style.PopupBorderSize = 1;
            style.FrameBorderSize = 0;
            style.TabBorderSize = 0;

            // Bordes ligeramente redondeados
            style.WindowRounding = 4;
            style.ChildRounding = 4;
            style.FrameRounding = 3;
            style.PopupRounding = 4;
            style.ScrollbarRounding = 4;
            style.GrabRounding = 3;
            style.TabRounding = 3;
            style.LogSliderDeadzone = 4;

            var colors = style.Colors;

            // Colores gris claro estilo UE5
            // Fondos principales - gris medio claro
            colors[(int)ImGuiCol.WindowBg] = new Vector4(0.24f, 0.24f, 0.24f, 1.0f);              // #3D3D3D
            colors[(int)ImGuiCol.ChildBg] = new Vector4(0.20f, 0.20f, 0.20f, 1.0f);               // #333333
            colors[(int)ImGuiCol.PopupBg] = new Vector4(0.26f, 0.26f, 0.26f, 0.98f);              // #424242
            colors[(int)ImGuiCol.Border] = new Vector4(0.14f, 0.14f, 0.14f, 1.0f);                // #242424
            colors[(int)ImGuiCol.BorderShadow] = new Vector4(0.0f, 0.0f, 0.0f, 0.0f);

            // Barras de título y menú
            colors[(int)ImGuiCol.TitleBg] = new Vector4(0.16f, 0.16f, 0.16f, 1.0f);               // #292929
            colors[(int)ImGuiCol.TitleBgActive] = new Vector4(0.20f, 0.20f, 0.20f, 1.0f);         // #333333
            colors[(int)ImGuiCol.TitleBgCollapsed] = new Vector4(0.16f, 0.16f, 0.16f, 1.0f);      // #292929
            colors[(int)ImGuiCol.MenuBarBg] = new Vector4(0.18f, 0.18f, 0.18f, 1.0f);             // #2E2E2E

            // Frames (inputs, boxes)
            colors[(int)ImGuiCol.FrameBg] = new Vector4(0.16f, 0.16f, 0.16f, 1.0f);               // #292929
            colors[(int)ImGuiCol.FrameBgHovered] = new Vector4(0.28f, 0.28f, 0.28f, 1.0f);        // #474747
            colors[(int)ImGuiCol.FrameBgActive] = new Vector4(0.32f, 0.32f, 0.32f, 1.0f);         // #525252

            // Tabs
            colors[(int)ImGuiCol.Tab] = new Vector4(0.18f, 0.18f, 0.18f, 1.0f);                   // #2E2E2E
            colors[(int)ImGuiCol.TabHovered] = new Vector4(0.34f, 0.34f, 0.34f, 1.0f);            // #575757
            colors[(int)ImGuiCol.TabSelected] = new Vector4(0.24f, 0.24f, 0.24f, 1.0f);           // #3D3D3D
            colors[(int)ImGuiCol.TabSelectedOverline] = new Vector4(0.0f, 0.47f, 0.84f, 1.0f);    // Azul UE5
            colors[(int)ImGuiCol.TabDimmed] = new Vector4(0.16f, 0.16f, 0.16f, 1.0f);             // #292929
            colors[(int)ImGuiCol.TabDimmedSelected] = new Vector4(0.20f, 0.20f, 0.20f, 1.0f);     // #333333
            colors[(int)ImGuiCol.TabDimmedSelectedOverline] = new Vector4(0.0f, 0.34f, 0.61f, 1.0f);

            // Scrollbar
            colors[(int)ImGuiCol.ScrollbarBg] = new Vector4(0.16f, 0.16f, 0.16f, 1.0f);           // #292929
            colors[(int)ImGuiCol.ScrollbarGrab] = new Vector4(0.38f, 0.38f, 0.38f, 1.0f);         // #616161
            colors[(int)ImGuiCol.ScrollbarGrabHovered] = new Vector4(0.46f, 0.46f, 0.46f, 1.0f);  // #757575
            colors[(int)ImGuiCol.ScrollbarGrabActive] = new Vector4(0.54f, 0.54f, 0.54f, 1.0f);   // #8A8A8A

            // Sliders
            colors[(int)ImGuiCol.SliderGrab] = new Vector4(0.0f, 0.47f, 0.84f, 1.0f);             // Azul UE5
            colors[(int)ImGuiCol.SliderGrabActive] = new Vector4(0.0f, 0.59f, 1.0f, 1.0f);        // Azul claro UE5

            // Buttons
            colors[(int)ImGuiCol.Button] = new Vector4(0.30f, 0.30f, 0.30f, 1.0f);                // #4D4D4D
            colors[(int)ImGuiCol.ButtonHovered] = new Vector4(0.0f, 0.47f, 0.84f, 0.6f);          // Azul UE5 con transparencia
            colors[(int)ImGuiCol.ButtonActive] = new Vector4(0.0f, 0.47f, 0.84f, 1.0f);           // Azul UE5

            // Headers (CollapsingHeader, TreeNode)
            colors[(int)ImGuiCol.Header] = new Vector4(0.30f, 0.30f, 0.30f, 1.0f);                // #4D4D4D
            colors[(int)ImGuiCol.HeaderHovered] = new Vector4(0.0f, 0.47f, 0.84f, 0.4f);          // Azul UE5 suave
            colors[(int)ImGuiCol.HeaderActive] = new Vector4(0.0f, 0.47f, 0.84f, 0.6f);           // Azul UE5 medio

            // Separators
            colors[(int)ImGuiCol.Separator] = new Vector4(0.14f, 0.14f, 0.14f, 1.0f);             // #242424
            colors[(int)ImGuiCol.SeparatorHovered] = new Vector4(0.0f, 0.47f, 0.84f, 0.78f);      // Azul UE5
            colors[(int)ImGuiCol.SeparatorActive] = new Vector4(0.0f, 0.47f, 0.84f, 1.0f);        // Azul UE5

            // Resize grip
            colors[(int)ImGuiCol.ResizeGrip] = new Vector4(0.0f, 0.47f, 0.84f, 0.25f);            // Azul UE5 transparente
            colors[(int)ImGuiCol.ResizeGripHovered] = new Vector4(0.0f, 0.47f, 0.84f, 0.67f);     // Azul UE5
            colors[(int)ImGuiCol.ResizeGripActive] = new Vector4(0.0f, 0.47f, 0.84f, 0.95f);      // Azul UE5

            // CheckMark y Text
            colors[(int)ImGuiCol.CheckMark] = new Vector4(0.0f, 0.59f, 1.0f, 1.0f);               // Azul claro UE5
            colors[(int)ImGuiCol.Text] = new Vector4(0.95f, 0.95f, 0.95f, 1.0f);                  // Texto blanco
            colors[(int)ImGuiCol.TextDisabled] = new Vector4(0.50f, 0.50f, 0.50f, 1.0f);          // Texto deshabilitado
            colors[(int)ImGuiCol.TextSelectedBg] = new Vector4(0.0f, 0.47f, 0.84f, 0.35f);        // Selección azul UE5

            // Docking
            colors[(int)ImGuiCol.DockingPreview] = new Vector4(0.0f, 0.47f, 0.84f, 0.70f);        // Azul UE5
            colors[(int)ImGuiCol.DockingEmptyBg] = new Vector4(0.20f, 0.20f, 0.20f, 1.0f);        // #333333

            // Plots
            colors[(int)ImGuiCol.PlotLines] = new Vector4(0.0f, 0.59f, 1.0f, 1.0f);               // Azul claro
            colors[(int)ImGuiCol.PlotLinesHovered] = new Vector4(0.0f, 0.78f, 1.0f, 1.0f);        // Azul muy claro
            colors[(int)ImGuiCol.PlotHistogram] = new Vector4(0.0f, 0.47f, 0.84f, 1.0f);          // Azul UE5
            colors[(int)ImGuiCol.PlotHistogramHovered] = new Vector4(0.0f, 0.59f, 1.0f, 1.0f);    // Azul claro

            // Tables
            colors[(int)ImGuiCol.TableHeaderBg] = new Vector4(0.22f, 0.22f, 0.22f, 1.0f);         // #383838
            colors[(int)ImGuiCol.TableBorderStrong] = new Vector4(0.14f, 0.14f, 0.14f, 1.0f);     // #242424
            colors[(int)ImGuiCol.TableBorderLight] = new Vector4(0.18f, 0.18f, 0.18f, 1.0f);      // #2E2E2E
            colors[(int)ImGuiCol.TableRowBg] = new Vector4(0.0f, 0.0f, 0.0f, 0.0f);               // Transparente
            colors[(int)ImGuiCol.TableRowBgAlt] = new Vector4(1.0f, 1.0f, 1.0f, 0.06f);           // Alternado sutil

            // Drag & Drop
            colors[(int)ImGuiCol.DragDropTarget] = new Vector4(0.0f, 0.78f, 1.0f, 0.90f);         // Azul muy claro

            // Navigation
            colors[(int)ImGuiCol.NavWindowingHighlight] = new Vector4(0.0f, 0.59f, 1.0f, 0.70f);  // Azul claro
            colors[(int)ImGuiCol.NavWindowingDimBg] = new Vector4(0.0f, 0.0f, 0.0f, 0.20f);       // Dim oscuro

            // Modal
            colors[(int)ImGuiCol.ModalWindowDimBg] = new Vector4(0.0f, 0.0f, 0.0f, 0.50f);        // Fondo modal
        }
    }
}