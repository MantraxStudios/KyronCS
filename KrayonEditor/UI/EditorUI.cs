using ImGuiNET;
using KrayonCore;
using KrayonCore.Core;
using KrayonCore.Core.Attributes;
using KrayonCore.Editor.Panels;
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
        private static string WindowsStatePath => AssetManager.TotalBase + "Windows.json";
        private static WindowsState _lastState = new WindowsState();

        public static void Initialize()
        {
            SetupImGuiStyle();
            LoadWindowsState();
        }

        public static void Draw()
        {
            var sceneView = UIRender.GetUI<SceneViewUI>();
            var hierarchy = UIRender.GetUI<HierarchyUI>();
            var inspector = UIRender.GetUI<InspectorUI>();
            var console = UIRender.GetUI<ConsoleUI>();
            var assets = UIRender.GetUI<AssetsUI>();
            var mainMenuBar = UIRender.GetUI<MainMenuBarUI>();

            ImGui.DockSpaceOverViewport();

            ImGui.Begin("Main View Port");
            uint innerDock = ImGui.GetID("InnerDockSpace");
            ImGui.DockSpace(innerDock, new Vector2(0, 0), ImGuiDockNodeFlags.None);
            ImGui.End();

            UIRender.Render();

            CheckAndSaveWindowsState();

            if (!GraphicsEngine.Instance.GetMouseState().IsButtonDown(OpenTK.Windowing.GraphicsLibraryFramework.MouseButton.Right) && EditorActions.IsHoveringScene && GraphicsEngine.Instance.GetKeyboardState().IsKeyDown(OpenTK.Windowing.GraphicsLibraryFramework.Keys.LeftControl) && GraphicsEngine.Instance.GetKeyboardState().IsKeyPressed(OpenTK.Windowing.GraphicsLibraryFramework.Keys.D))
            {
                if (EditorActions.SelectedObject != null)
                {
                    GameObject clone = SceneManager.PrimaryScene.Instantiate(EditorActions.SelectedObject);
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
                        UIRender.GetUI<HierarchyUI>().IsVisible = state.ShowHierarchy;
                        UIRender.GetUI<InspectorUI>().IsVisible = state.ShowInspector;
                        UIRender.GetUI<SceneViewUI>().IsVisible = state.ShowSceneView;
                        UIRender.GetUI<ConsoleUI>().IsVisible = state.ShowConsole;
                        UIRender.GetUI<AssetsUI>().IsVisible = state.ShowAssets;
                        UIRender.GetUI<MaterialUI>().IsVisible = state.ShowMaterials;
                        UIRender.GetUI<TileEditor>().IsVisible = state.ShowTileEditor;
                        UIRender.GetUI<SpriteAnimationUI>().IsVisible = state.ShowSpriteAnimator;
                        UIRender.GetUI<CompilerUI>().IsVisible = state.ShowCompiler;

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
                ShowHierarchy = UIRender.GetUI<HierarchyUI>().IsVisible,
                ShowInspector = UIRender.GetUI<InspectorUI>().IsVisible,
                ShowSceneView = UIRender.GetUI<SceneViewUI>().IsVisible,
                ShowConsole = UIRender.GetUI<ConsoleUI>().IsVisible,
                ShowAssets = UIRender.GetUI<AssetsUI>().IsVisible,
                ShowMaterials = UIRender.GetUI<MaterialUI>().IsVisible,
                ShowTileEditor = UIRender.GetUI<TileEditor>().IsVisible,
                ShowSpriteAnimator = UIRender.GetUI<SpriteAnimationUI>().IsVisible,
                ShowCompiler = UIRender.GetUI<CompilerUI>().IsVisible
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
                string json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
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
            style.IndentSpacing = 21;
            style.ScrollbarSize = 14;
            style.GrabMinSize = 8;
            style.WindowBorderSize = 1;
            style.ChildBorderSize = 1;
            style.PopupBorderSize = 1;

            var colors = style.Colors;
            colors[(int)ImGuiCol.WindowBg] = new Vector4(0.15f, 0.15f, 0.15f, 1.0f);
            colors[(int)ImGuiCol.ChildBg] = new Vector4(0.12f, 0.12f, 0.12f, 1.0f);
            colors[(int)ImGuiCol.PopupBg] = new Vector4(0.18f, 0.18f, 0.18f, 0.98f);
            colors[(int)ImGuiCol.Border] = new Vector4(0.08f, 0.08f, 0.08f, 1.0f);
            colors[(int)ImGuiCol.TitleBg] = new Vector4(0.12f, 0.12f, 0.12f, 1.0f);
            colors[(int)ImGuiCol.TitleBgActive] = new Vector4(0.18f, 0.18f, 0.18f, 1.0f);
            colors[(int)ImGuiCol.MenuBarBg] = new Vector4(0.18f, 0.18f, 0.18f, 1.0f);
            colors[(int)ImGuiCol.FrameBg] = new Vector4(0.12f, 0.12f, 0.12f, 1.0f);
            colors[(int)ImGuiCol.FrameBgHovered] = new Vector4(0.20f, 0.20f, 0.20f, 1.0f);
            colors[(int)ImGuiCol.FrameBgActive] = new Vector4(0.25f, 0.25f, 0.25f, 1.0f);
            colors[(int)ImGuiCol.Button] = new Vector4(0.20f, 0.20f, 0.20f, 1.0f);
            colors[(int)ImGuiCol.ButtonHovered] = new Vector4(0.55f, 0.25f, 0.30f, 0.5f);
            colors[(int)ImGuiCol.ButtonActive] = new Vector4(0.55f, 0.25f, 0.30f, 1.0f);
            colors[(int)ImGuiCol.Header] = new Vector4(0.20f, 0.20f, 0.20f, 1.0f);
            colors[(int)ImGuiCol.HeaderHovered] = new Vector4(0.55f, 0.25f, 0.30f, 0.3f);
            colors[(int)ImGuiCol.HeaderActive] = new Vector4(0.55f, 0.25f, 0.30f, 0.5f);
            colors[(int)ImGuiCol.Separator] = new Vector4(0.08f, 0.08f, 0.08f, 1.0f);
            colors[(int)ImGuiCol.SliderGrab] = new Vector4(0.55f, 0.25f, 0.30f, 1.0f);
            colors[(int)ImGuiCol.Text] = new Vector4(0.95f, 0.95f, 0.95f, 1.0f);
            colors[(int)ImGuiCol.TextDisabled] = new Vector4(0.50f, 0.50f, 0.50f, 1.0f);
            colors[(int)ImGuiCol.DockingPreview] = new Vector4(0.55f, 0.25f, 0.30f, 0.7f);
            colors[(int)ImGuiCol.Tab] = new Vector4(0.18f, 0.18f, 0.18f, 1.0f);
            colors[(int)ImGuiCol.TabHovered] = new Vector4(0.25f, 0.25f, 0.25f, 1.0f);
            colors[(int)ImGuiCol.TabSelected] = new Vector4(0.15f, 0.15f, 0.15f, 1.0f);
        }
    }
}