using ImGuiNET;
using KrayonCore;
using KrayonCore.Core.Attributes;
using KrayonCore.GraphicsData;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text.Json;
using Vector2 = System.Numerics.Vector2;

namespace KrayonEditor.UI
{
    public class WindowsState
    {
        public bool ShowHierarchy { get; set; } = true;
        public bool ShowInspector { get; set; } = true;
        public bool ShowSceneView { get; set; } = true;
        public bool ShowConsole { get; set; } = true;
        public bool ShowStats { get; set; } = true;
        public bool ShowAssets { get; set; } = true;
        public bool ShowMaterials { get; set; } = false;
        public bool ShowTileEditor { get; set; } = false;
        public bool ShowSpriteAnimator { get; set; } = false;
        public bool ShowCompiler { get; set; } = false;
    }

    internal static class EditorUI
    {
        private static MainMenuBarUI _mainMenuBar = new MainMenuBarUI();
        public static DockSpaceUI _dockSpace = new DockSpaceUI();
        public static HierarchyUI _hierarchy = new HierarchyUI();
        public static InspectorUI _inspector = new InspectorUI();
        public static SceneViewUI _sceneView = new SceneViewUI();
        public static ConsoleUI _console = new ConsoleUI();
        public static StatsUI _stats = new StatsUI();
        public static AssetsUI _assets = new AssetsUI();
        public static MaterialUI _materials = new MaterialUI();
        public static TileEditor _TileEditor = new TileEditor();
        public static SpriteAnimationUI _SpriteAnimator = new SpriteAnimationUI();
        public static CompilerUI _CompilerUI = new CompilerUI();
        public static RoslynCodeEditor editorCode;
        public static AnimatorEditorUI _animatorEditor = new AnimatorEditorUI();


        private static string WindowsStatePath => AssetManager.TotalBase + "Windows.json";
        private static WindowsState _lastState = new WindowsState();

        public static void Initialize()
        {
            SetupImGuiStyle();
            editorCode = new RoslynCodeEditor();
            editorCode.LoadDll(AssetManager.TotalBase + "/Library/KrayonCore.dll");
            LoadWindowsState();
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

            _hierarchy.IsVisible = _mainMenuBar.ShowHierarchy;
            _inspector.IsVisible = _mainMenuBar.ShowInspector;
            _console.IsVisible = _mainMenuBar.ShowConsole;
            _stats.IsVisible = _mainMenuBar.ShowStats;
            _assets.IsVisible = _mainMenuBar.ShowAssets;

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
            _CompilerUI.OnDrawUI();
            editorCode.Draw();
            _animatorEditor.OnDrawUI();

            _mainMenuBar.ShowHierarchy = _hierarchy.IsVisible;
            _mainMenuBar.ShowInspector = _inspector.IsVisible;
            _mainMenuBar.ShowConsole = _console.IsVisible;
            _mainMenuBar.ShowStats = _stats.IsVisible;
            _mainMenuBar.ShowAssets = _assets.IsVisible;

            newIsPlaying = _sceneView.IsPlaying;
            newEditorCameraSpeed = _sceneView.EditorCameraSpeed;
            newLastSceneViewportSize = _sceneView.LastViewportSize;

            CheckAndSaveWindowsState();

            if (!GraphicsEngine.Instance.GetMouseState().IsButtonDown(OpenTK.Windowing.GraphicsLibraryFramework.MouseButton.Right) && EditorActions.IsHoveringScene && GraphicsEngine.Instance.GetKeyboardState().IsKeyDown(OpenTK.Windowing.GraphicsLibraryFramework.Keys.LeftControl) && GraphicsEngine.Instance.GetKeyboardState().IsKeyPressed(OpenTK.Windowing.GraphicsLibraryFramework.Keys.D))
            {
                if (EditorActions.SelectedObject != null)
                {
                    GameObject clone = SceneManager.ActiveScene.Instantiate(EditorActions.SelectedObject);
                    EditorActions.SelectedObject = clone;
                }
            }
        }

        private static void LoadWindowsState()
        {
            try
            {
                if (File.Exists(WindowsStatePath))
                {
                    string json = File.ReadAllText(WindowsStatePath);
                    var state = JsonSerializer.Deserialize<WindowsState>(json);

                    if (state != null)
                    {
                        _hierarchy.IsVisible = state.ShowHierarchy;
                        _inspector.IsVisible = state.ShowInspector;
                        _sceneView.IsVisible = state.ShowSceneView;
                        _console.IsVisible = state.ShowConsole;
                        _stats.IsVisible = state.ShowStats;
                        _assets.IsVisible = state.ShowAssets;
                        _materials.IsVisible = state.ShowMaterials;
                        _TileEditor.IsVisible = state.ShowTileEditor;
                        _SpriteAnimator.IsVisible = state.ShowSpriteAnimator;
                        _CompilerUI.IsVisible = state.ShowCompiler;

                        _lastState = state;
                        Console.WriteLine("[EditorUI] Windows state loaded");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Console.WriteLine($"[EditorUI] Error loading windows state: {ex.Message}");
            }
        }

        private static void CheckAndSaveWindowsState()
        {
            var currentState = new WindowsState
            {
                ShowHierarchy = _hierarchy.IsVisible,
                ShowInspector = _inspector.IsVisible,
                ShowSceneView = _sceneView.IsVisible,
                ShowConsole = _console.IsVisible,
                ShowStats = _stats.IsVisible,
                ShowAssets = _assets.IsVisible,
                ShowMaterials = _materials.IsVisible,
                ShowTileEditor = _TileEditor.IsVisible,
                ShowSpriteAnimator = _SpriteAnimator.IsVisible,
                ShowCompiler = _CompilerUI.IsVisible
            };

            if (HasStateChanged(currentState))
            {
                SaveWindowsState(currentState);
                _lastState = currentState;
            }
        }

        private static bool HasStateChanged(WindowsState current)
        {
            return current.ShowHierarchy != _lastState.ShowHierarchy ||
                   current.ShowInspector != _lastState.ShowInspector ||
                   current.ShowSceneView != _lastState.ShowSceneView ||
                   current.ShowConsole != _lastState.ShowConsole ||
                   current.ShowStats != _lastState.ShowStats ||
                   current.ShowAssets != _lastState.ShowAssets ||
                   current.ShowMaterials != _lastState.ShowMaterials ||
                   current.ShowTileEditor != _lastState.ShowTileEditor ||
                   current.ShowSpriteAnimator != _lastState.ShowSpriteAnimator ||
                   current.ShowCompiler != _lastState.ShowCompiler;
        }

        private static void SaveWindowsState(WindowsState state)
        {
            try
            {
                string json = JsonSerializer.Serialize(state, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(WindowsStatePath, json, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                Console.WriteLine("[EditorUI] Windows state saved");
            }
            catch (System.Exception ex)
            {
                Console.WriteLine($"[EditorUI] Error saving windows state: {ex.Message}");
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

            style.WindowBorderSize = 1;
            style.ChildBorderSize = 1;
            style.PopupBorderSize = 1;
            style.FrameBorderSize = 0;
            style.TabBorderSize = 0;

            style.WindowRounding = 0;
            style.ChildRounding = 0;
            style.FrameRounding = 0;
            style.PopupRounding = 0;
            style.ScrollbarRounding = 0;
            style.GrabRounding = 0;
            style.TabRounding = 0;
            style.LogSliderDeadzone = 4;

            var colors = style.Colors;

            colors[(int)ImGuiCol.WindowBg] = new Vector4(0.15f, 0.15f, 0.15f, 1.0f);
            colors[(int)ImGuiCol.ChildBg] = new Vector4(0.12f, 0.12f, 0.12f, 1.0f);
            colors[(int)ImGuiCol.PopupBg] = new Vector4(0.18f, 0.18f, 0.18f, 0.98f);
            colors[(int)ImGuiCol.Border] = new Vector4(0.08f, 0.08f, 0.08f, 1.0f);
            colors[(int)ImGuiCol.BorderShadow] = new Vector4(0.0f, 0.0f, 0.0f, 0.0f);

            colors[(int)ImGuiCol.TitleBg] = new Vector4(0.12f, 0.12f, 0.12f, 1.0f);
            colors[(int)ImGuiCol.TitleBgActive] = new Vector4(0.18f, 0.18f, 0.18f, 1.0f);
            colors[(int)ImGuiCol.TitleBgCollapsed] = new Vector4(0.12f, 0.12f, 0.12f, 1.0f);
            colors[(int)ImGuiCol.MenuBarBg] = new Vector4(0.18f, 0.18f, 0.18f, 1.0f);

            colors[(int)ImGuiCol.FrameBg] = new Vector4(0.12f, 0.12f, 0.12f, 1.0f);
            colors[(int)ImGuiCol.FrameBgHovered] = new Vector4(0.20f, 0.20f, 0.20f, 1.0f);
            colors[(int)ImGuiCol.FrameBgActive] = new Vector4(0.25f, 0.25f, 0.25f, 1.0f);

            colors[(int)ImGuiCol.Tab] = new Vector4(0.18f, 0.18f, 0.18f, 1.0f);
            colors[(int)ImGuiCol.TabHovered] = new Vector4(0.25f, 0.25f, 0.25f, 1.0f);
            colors[(int)ImGuiCol.TabSelected] = new Vector4(0.15f, 0.15f, 0.15f, 1.0f);
            colors[(int)ImGuiCol.TabSelectedOverline] = new Vector4(0.55f, 0.25f, 0.30f, 1.0f);
            colors[(int)ImGuiCol.TabDimmed] = new Vector4(0.12f, 0.12f, 0.12f, 1.0f);
            colors[(int)ImGuiCol.TabDimmedSelected] = new Vector4(0.18f, 0.18f, 0.18f, 1.0f);
            colors[(int)ImGuiCol.TabDimmedSelectedOverline] = new Vector4(0.45f, 0.20f, 0.25f, 1.0f);

            colors[(int)ImGuiCol.ScrollbarBg] = new Vector4(0.12f, 0.12f, 0.12f, 1.0f);
            colors[(int)ImGuiCol.ScrollbarGrab] = new Vector4(0.20f, 0.20f, 0.20f, 1.0f);
            colors[(int)ImGuiCol.ScrollbarGrabHovered] = new Vector4(0.25f, 0.25f, 0.25f, 1.0f);
            colors[(int)ImGuiCol.ScrollbarGrabActive] = new Vector4(0.55f, 0.25f, 0.30f, 1.0f);

            colors[(int)ImGuiCol.SliderGrab] = new Vector4(0.55f, 0.25f, 0.30f, 1.0f);
            colors[(int)ImGuiCol.SliderGrabActive] = new Vector4(0.65f, 0.30f, 0.35f, 1.0f);

            colors[(int)ImGuiCol.Button] = new Vector4(0.20f, 0.20f, 0.20f, 1.0f);
            colors[(int)ImGuiCol.ButtonHovered] = new Vector4(0.55f, 0.25f, 0.30f, 0.5f);
            colors[(int)ImGuiCol.ButtonActive] = new Vector4(0.55f, 0.25f, 0.30f, 1.0f);

            colors[(int)ImGuiCol.Header] = new Vector4(0.20f, 0.20f, 0.20f, 1.0f);
            colors[(int)ImGuiCol.HeaderHovered] = new Vector4(0.55f, 0.25f, 0.30f, 0.3f);
            colors[(int)ImGuiCol.HeaderActive] = new Vector4(0.55f, 0.25f, 0.30f, 0.5f);

            colors[(int)ImGuiCol.Separator] = new Vector4(0.08f, 0.08f, 0.08f, 1.0f);
            colors[(int)ImGuiCol.SeparatorHovered] = new Vector4(0.55f, 0.25f, 0.30f, 1.0f);
            colors[(int)ImGuiCol.SeparatorActive] = new Vector4(0.65f, 0.30f, 0.35f, 1.0f);

            colors[(int)ImGuiCol.ResizeGrip] = new Vector4(0.55f, 0.25f, 0.30f, 0.2f);
            colors[(int)ImGuiCol.ResizeGripHovered] = new Vector4(0.55f, 0.25f, 0.30f, 0.6f);
            colors[(int)ImGuiCol.ResizeGripActive] = new Vector4(0.55f, 0.25f, 0.30f, 1.0f);

            colors[(int)ImGuiCol.CheckMark] = new Vector4(0.65f, 0.30f, 0.35f, 1.0f);
            colors[(int)ImGuiCol.Text] = new Vector4(0.95f, 0.95f, 0.95f, 1.0f);
            colors[(int)ImGuiCol.TextDisabled] = new Vector4(0.50f, 0.50f, 0.50f, 1.0f);
            colors[(int)ImGuiCol.TextSelectedBg] = new Vector4(0.55f, 0.25f, 0.30f, 0.35f);

            colors[(int)ImGuiCol.DockingPreview] = new Vector4(0.55f, 0.25f, 0.30f, 0.7f);
            colors[(int)ImGuiCol.DockingEmptyBg] = new Vector4(0.12f, 0.12f, 0.12f, 1.0f);

            colors[(int)ImGuiCol.PlotLines] = new Vector4(0.55f, 0.25f, 0.30f, 1.0f);
            colors[(int)ImGuiCol.PlotLinesHovered] = new Vector4(0.65f, 0.30f, 0.35f, 1.0f);
            colors[(int)ImGuiCol.PlotHistogram] = new Vector4(0.55f, 0.25f, 0.30f, 1.0f);
            colors[(int)ImGuiCol.PlotHistogramHovered] = new Vector4(0.65f, 0.30f, 0.35f, 1.0f);

            colors[(int)ImGuiCol.TableHeaderBg] = new Vector4(0.18f, 0.18f, 0.18f, 1.0f);
            colors[(int)ImGuiCol.TableBorderStrong] = new Vector4(0.08f, 0.08f, 0.08f, 1.0f);
            colors[(int)ImGuiCol.TableBorderLight] = new Vector4(0.12f, 0.12f, 0.12f, 1.0f);
            colors[(int)ImGuiCol.TableRowBg] = new Vector4(0.0f, 0.0f, 0.0f, 0.0f);
            colors[(int)ImGuiCol.TableRowBgAlt] = new Vector4(0.55f, 0.25f, 0.30f, 0.06f);

            colors[(int)ImGuiCol.DragDropTarget] = new Vector4(0.65f, 0.30f, 0.35f, 1.0f);

            colors[(int)ImGuiCol.NavWindowingHighlight] = new Vector4(0.55f, 0.25f, 0.30f, 0.7f);
            colors[(int)ImGuiCol.NavWindowingDimBg] = new Vector4(0.0f, 0.0f, 0.0f, 0.3f);

            colors[(int)ImGuiCol.ModalWindowDimBg] = new Vector4(0.0f, 0.0f, 0.0f, 0.5f);
        }
    }
}