using KrayonCore;
using KrayonCore.GraphicsData;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Vector2 = System.Numerics.Vector2;

namespace KrayonEditor.UI
{
    internal static class EngineLoader
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
        private static Camera? _editorCamera;
        private static SceneRenderer? _renderer;
        private static GraphicsEngine? _engine;

        // ── Estado del editor ────────────────────────────────────────────────
        private static GameObject? _selectedObject = null;
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

            _engine.CreateWindow(WindowConfig.Width, WindowConfig.Height, "Kryon Engine - Editor");
            _engine.Run();
        }

        // ── Handlers del ciclo de vida ───────────────────────────────────────
        private static void HandleLoad()
        {
            LogMessage("Kryon Engine Initialized");
            LogMessage("Controls:");
            LogMessage("  WASD        - Move camera");
            LogMessage("  Space       - Move up");
            LogMessage("  Shift       - Move down");
            LogMessage("  Right Mouse - Look around");
            LogMessage("  Scroll      - Zoom");
            LogMessage("  P           - Print camera info");
            LogMessage("  Ctrl+R      - Reset Camera");
            LogMessage("  1, 2, 3     - Switch scenes");
            LogMessage("================================");

            _renderer = _engine!.GetSceneRenderer();
            SetupCamera();
        }

        private static void HandleUpdate(float dt)
        {
            if (_editorCamera is null) return;

            HandleInput(dt);
            UpdateFPS(dt);
        }

        private static void HandleRender(float dt) { }

        private static void HandleResize(int w, int h) { }

        private static void HandleClose()
            => LogMessage("Engine closed");

        // ── Input ────────────────────────────────────────────────────────────
        private static void HandleInput(float dt)
        {
            if (_engine is null || _editorCamera is null) return;

            var keyboard = _engine.GetKeyboardState();
            var mouse = _engine.GetMouseState();

            HandleCameraInfo(keyboard);
            HandleCameraReset(keyboard);
            HandleSceneSwitch(keyboard);
            HandleCameraMovement(keyboard, mouse, dt);
            HandleCameraRotation(mouse);
            HandleCameraZoom(mouse);
        }

        private static void HandleCameraInfo(
            KeyboardState keyboard)
        {
            if (keyboard.IsKeyPressed(Keys.P))
                PrintCameraInfo();
        }

        private static void HandleCameraReset(
            KeyboardState keyboard)
        {
            if (keyboard.IsKeyDown(Keys.LeftControl) && keyboard.IsKeyPressed(Keys.R))
            {
                ResetCamera();
                LogMessage("Camera reset to initial position");
            }
        }

        private static void HandleSceneSwitch(
            KeyboardState keyboard)
        {
            if (keyboard.IsKeyPressed(Keys.D1)) { SceneManager.LoadScene("/DefaultScene.scene"); LogMessage("Loaded DefaultScene"); }
            if (keyboard.IsKeyPressed(Keys.D2)) { SceneManager.LoadScene("Scene2"); LogMessage("Loaded Scene2"); }
            if (keyboard.IsKeyPressed(Keys.D3)) { SceneManager.LoadScene("Scene3"); LogMessage("Loaded Scene3"); }
        }

        private static void HandleCameraMovement(
            KeyboardState keyboard,
            MouseState mouse,
            float dt)
        {
            if (!mouse.IsButtonDown(MouseButton.Right)) return;

            float speed = _editorCameraSpeed * (keyboard.IsKeyDown(Keys.LeftControl) ? 2.0f : 1.0f);
            float step = dt * speed / 2.5f;

            if (keyboard.IsKeyDown(Keys.W)) _editorCamera!.Move(CameraMovement.Forward, step);
            if (keyboard.IsKeyDown(Keys.S)) _editorCamera!.Move(CameraMovement.Backward, step);
            if (keyboard.IsKeyDown(Keys.A)) _editorCamera!.Move(CameraMovement.Left, step);
            if (keyboard.IsKeyDown(Keys.D)) _editorCamera!.Move(CameraMovement.Right, step);
            if (keyboard.IsKeyDown(Keys.Space)) _editorCamera!.Move(CameraMovement.Up, step);
            if (keyboard.IsKeyDown(Keys.LeftShift)) _editorCamera!.Move(CameraMovement.Down, step);
        }

        private static void HandleCameraRotation(MouseState mouse)
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
            if (mouse.IsButtonDown(MouseButton.Right) && camera?.IsPerspective == true)
                _editorCamera!.Rotate(xOffset, yOffset);
        }

        private static void HandleCameraZoom(MouseState mouse)
        {
            if (mouse.ScrollDelta.Y != 0)
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
            _editorCamera.SetProjectionMode(ProjectionMode.Perspective);
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

        private static void PrintCameraInfo()
        {
            if (_editorCamera is null) return;

            var pos = _editorCamera.Position;
            var type = _editorCamera.GetType();
            float yaw = (float?)type.GetProperty("Yaw")?.GetValue(_editorCamera) ?? 0f;
            float pitch = (float?)type.GetProperty("Pitch")?.GetValue(_editorCamera) ?? 0f;

            LogMessage("================================");
            LogMessage("CAMERA INFO:");
            LogMessage($"  Position : ({pos.X:F2}, {pos.Y:F2}, {pos.Z:F2})");
            LogMessage($"  Rotation : Yaw={yaw:F2}°  Pitch={pitch:F2}°");
            LogMessage($"  Mode     : {_editorCamera.ProjectionMode}");
            LogMessage("================================");
        }

        // ── Consola ──────────────────────────────────────────────────────────
        public static void LogMessage(string message)
        {
            var formatted = $"[{DateTime.Now:HH:mm:ss}] {message}";
            Console.WriteLine(formatted);

            _consoleMessages.Add(formatted);
            if (_consoleMessages.Count > _maxConsoleMessages)
                _consoleMessages.RemoveAt(0);
        }

        // ── Selección ────────────────────────────────────────────────────────
        public static GameObject? GetSelectedObject() => _selectedObject;
        public static void SetSelectedObject(GameObject? obj) => _selectedObject = obj;
    }
}