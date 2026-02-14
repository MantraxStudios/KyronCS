using ImGuiNET;
using KrayonCore;
using KrayonCore.Core.Attributes;
using KrayonCore.GraphicsData;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Vector2 = System.Numerics.Vector2;

namespace KrayonEditor.UI
{
    internal static class EngineEditor
    {
        // ── Estado de cámara ─────────────────────────────────────────────────
        private static bool _firstMouse = true;
        private static float _lastX = 640f;
        private static float _lastY = 400f;

        private static readonly OpenTK.Mathematics.Vector3 _initialCameraPosition = new(0, 2, 5);
        private const float _initialCameraYaw = -90.0f;
        private const float _initialCameraPitch = 0.0f;

        // ── FPS ──────────────────────────────────────────────────────────────
        private static double _fpsTimer = 0.0;
        private static int _fpsCounter = 0;
        private static double _currentFps = 0.0;
        private static double _currentFrameTime = 0.0;

        // ── Referencias ──────────────────────────────────────────────────────
        private static ImGuiController? _imguiController;
        private static Camera? _editorCamera;
        private static SceneRenderer? _renderer;
        private static GraphicsEngine? _engine;
        private static GameWindow? _window;

        // ── Estado del editor ────────────────────────────────────────────────
        private static GameObject? _selectedObject = null;
        private static UIRender? _uiRender = null;
        private static bool _isPlaying = false;
        private static float _editorCameraSpeed = 2.5f;
        private static Vector2 _lastSceneViewportSize = new(1280, 720);

        // ── Consola ──────────────────────────────────────────────────────────
        private static readonly List<string> _consoleMessages = new();
        private const int _maxConsoleMessages = 100;

        // ── Punto de entrada ─────────────────────────────────────────────────
        public static void Run()
        {
            _engine = new GraphicsEngine();

            _engine.OnLoad += HandleLoad;
            _engine.OnUpdate += HandleUpdate;
            _engine.OnRender += HandleRender;
            _engine.OnResize += HandleResize;
            _engine.OnClose += HandleClose;
            _engine.OnFileDrop += OnFilesDropped;

            _engine.CreateWindow(WindowConfig.Width, WindowConfig.Height, "Kryon Engine - Editor");
            _engine.Run();
        }

        // ── Handlers del ciclo de vida ───────────────────────────────────────
        private static void HandleLoad()
        {
            LogMessage("Kryon Engine Initialized");
            LogMessage("Controls:");
            LogMessage("  WASD         - Move camera");
            LogMessage("  Space        - Move up");
            LogMessage("  Shift        - Move down");
            LogMessage("  Right Mouse  - Look around");
            LogMessage("  Scroll       - Zoom");
            LogMessage("  Q            - Move Gizmo");
            LogMessage("  E            - Rotate Gizmo");
            LogMessage("  R            - Scale Gizmo");
            LogMessage("  Ctrl+R       - Reset Camera");
            LogMessage("  1, 2, 3      - Switch scenes");
            LogMessage("================================");

            _renderer = _engine!.GetSceneRenderer();
            _window = _engine.Window;
            _imguiController = new ImGuiController(WindowConfig.Width, WindowConfig.Height);
            _uiRender = new UIRender();

            _engine.OnTextInput += e => _imguiController?.PressChar((char)e.Unicode);

            EditorUI.Initialize();
            SetupCamera();
        }

        private static void HandleUpdate(float dt)
        {
            if (_editorCamera is null || _window is null) return;

            HandleInput(dt);
            UpdateFPS(dt);

            _imguiController?.Update(_window, dt);

            EditorUI.Draw(
                _engine!, _editorCamera, _isPlaying, _editorCameraSpeed,
                _lastSceneViewportSize, _currentFps, _currentFrameTime, _consoleMessages,
                out _isPlaying, out _editorCameraSpeed, out _lastSceneViewportSize
            );

            _uiRender?.RenderUI();
        }

        private static void HandleRender(float dt)
            => _imguiController?.Render();

        private static void HandleResize(int w, int h)
            => _imguiController?.WindowResized(w, h);

        private static void HandleClose()
        {
            _imguiController?.Dispose();
            LogMessage("Engine closed");
        }

        // ── Input ────────────────────────────────────────────────────────────
        private static void HandleInput(float dt)
        {
            if (_engine is null || _editorCamera is null) return;

            var keyboard = _engine.GetKeyboardState();
            var mouse = _engine.GetMouseState();

            HandleGizmoInput(keyboard, mouse);
            HandleCameraReset(keyboard);
            HandleSceneSwitch(keyboard);
            HandleCameraMovement(keyboard, mouse, dt);
            HandleCameraRotation(mouse);
            HandleCameraZoom(mouse);
        }

        private static void HandleGizmoInput(
            OpenTK.Windowing.GraphicsLibraryFramework.KeyboardState keyboard,
            OpenTK.Windowing.GraphicsLibraryFramework.MouseState mouse)
        {
            if (mouse.IsButtonDown(MouseButton.Right) || !EditorActions.IsHoveringScene) return;

            if (keyboard.IsKeyPressed(Keys.Q))
            {
                TransformGizmo.SetMode(GizmoMode.Translate);
                LogMessage("Switched to Move mode");
            }
            if (keyboard.IsKeyPressed(Keys.E))
            {
                TransformGizmo.SetMode(GizmoMode.Rotate);
                LogMessage("Switched to Rotate mode");
            }
            if (keyboard.IsKeyPressed(Keys.R) && !keyboard.IsKeyDown(Keys.LeftControl))
            {
                TransformGizmo.SetMode(GizmoMode.Scale);
                LogMessage("Switched to Scale mode");
            }
        }

        private static void HandleCameraReset(
            OpenTK.Windowing.GraphicsLibraryFramework.KeyboardState keyboard)
        {
            if (keyboard.IsKeyDown(Keys.LeftControl) && keyboard.IsKeyPressed(Keys.R))
            {
                ResetCamera();
                LogMessage("Camera reset to initial position");
            }
        }

        private static void HandleSceneSwitch(
            OpenTK.Windowing.GraphicsLibraryFramework.KeyboardState keyboard)
        {
            if (keyboard.IsKeyPressed(Keys.D1)) { SceneManager.LoadScene("Scene1"); LogMessage("Loaded Scene1"); }
            if (keyboard.IsKeyPressed(Keys.D2)) { SceneManager.LoadScene("Scene2"); LogMessage("Loaded Scene2"); }
            if (keyboard.IsKeyPressed(Keys.D3)) { SceneManager.LoadScene("Scene3"); LogMessage("Loaded Scene3"); }
        }

        private static void HandleCameraMovement(
            OpenTK.Windowing.GraphicsLibraryFramework.KeyboardState keyboard,
            OpenTK.Windowing.GraphicsLibraryFramework.MouseState mouse,
            float dt)
        {
            if (!mouse.IsButtonDown(MouseButton.Right) || !EditorActions.IsHoveringScene) return;

            float speed = _editorCameraSpeed * (keyboard.IsKeyDown(Keys.LeftControl) ? 2.0f : 1.0f);
            float step = dt * speed / 2.5f;

            if (keyboard.IsKeyDown(Keys.W)) _editorCamera!.Move(CameraMovement.Forward, step);
            if (keyboard.IsKeyDown(Keys.S)) _editorCamera!.Move(CameraMovement.Backward, step);
            if (keyboard.IsKeyDown(Keys.A)) _editorCamera!.Move(CameraMovement.Left, step);
            if (keyboard.IsKeyDown(Keys.D)) _editorCamera!.Move(CameraMovement.Right, step);
            if (keyboard.IsKeyDown(Keys.Space)) _editorCamera!.Move(CameraMovement.Up, step);
            if (keyboard.IsKeyDown(Keys.LeftShift)) _editorCamera!.Move(CameraMovement.Down, step);
        }

        private static void HandleCameraRotation(
            OpenTK.Windowing.GraphicsLibraryFramework.MouseState mouse)
        {
            if (_firstMouse)
            {
                _lastX = mouse.X;
                _lastY = mouse.Y;
                _firstMouse = false;
            }

            float xOffset = mouse.X - _lastX;
            float yOffset = _lastY - mouse.Y;
            _lastX = mouse.X;
            _lastY = mouse.Y;

            var camera = GraphicsEngine.Instance?.GetSceneRenderer().GetCamera();
            if (mouse.IsButtonDown(MouseButton.Right) &&
                camera?.IsPerspective == true &&
                EditorActions.IsHoveringScene)
            {
                _editorCamera!.Rotate(xOffset, yOffset);
            }
        }

        private static void HandleCameraZoom(
            OpenTK.Windowing.GraphicsLibraryFramework.MouseState mouse)
        {
            if (mouse.ScrollDelta.Y != 0 && EditorActions.IsHoveringScene)
                _editorCamera!.Zoom(mouse.ScrollDelta.Y);
        }

        // ── FPS ──────────────────────────────────────────────────────────────
        private static void UpdateFPS(float dt)
        {
            _fpsCounter++;
            _fpsTimer += dt;

            if (_fpsTimer < 1.0) return;

            _currentFps = _fpsCounter / _fpsTimer;
            _currentFrameTime = 1000.0 / _currentFps;
            _fpsCounter = 0;
            _fpsTimer = 0.0;
        }

        // ── Cámara ───────────────────────────────────────────────────────────
        private static void SetupCamera()
        {
            if (_renderer is null) return;

            _editorCamera = _renderer.GetCamera();
            _editorCamera.Position = _initialCameraPosition;
            _editorCamera.AspectRatio = 1280f / 800f;
        }

        private static void ResetCamera()
        {
            if (_editorCamera is null) return;

            _editorCamera.Position = _initialCameraPosition;

            var type = _editorCamera.GetType();
            type.GetProperty("Yaw")?.SetValue(_editorCamera, _initialCameraYaw);
            type.GetProperty("Pitch")?.SetValue(_editorCamera, _initialCameraPitch);
        }

        // ── Archivos ─────────────────────────────────────────────────────────
        private static void OnFilesDropped(string[] filePaths)
            => EditorUI._assets.HandleExternalDrop(filePaths, "");

        // ── Consola ──────────────────────────────────────────────────────────
        public static void LogMessage(string message)
        {
            _consoleMessages.Add($"[{DateTime.Now:HH:mm:ss}] {message}");

            if (_consoleMessages.Count > _maxConsoleMessages)
                _consoleMessages.RemoveAt(0);
        }

        // ── Selección ────────────────────────────────────────────────────────
        public static GameObject? GetSelectedObject() => _selectedObject;
        public static void SetSelectedObject(GameObject? obj) => _selectedObject = obj;
    }
}