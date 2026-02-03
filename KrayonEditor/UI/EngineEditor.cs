using ImGuiNET;
using KrayonCore;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Vector2 = System.Numerics.Vector2;

namespace KrayonEditor.UI
{
    internal static class EngineEditor
    {
        private static bool _firstMouse = true;
        private static float _lastX = 640f;
        private static float _lastY = 400f;

        private static double _fpsTimer = 0.0;
        private static int _fpsCounter = 0;
        private static double _currentFps = 0.0;
        private static double _currentFrameTime = 0.0;

        private static ImGuiController? _imguiController;
        private static CameraComponent? _mainCamera;
        private static SceneRenderer? _renderer;
        private static GraphicsEngine? _engine;

        private static GameObject? _selectedObject = null;
        private static UIRender? _UIRender = null;
        private static bool _isPlaying = false;
        private static float _editorCameraSpeed = 2.5f;

        private static List<string> _consoleMessages = new List<string>();
        private static int _maxConsoleMessages = 100;

        private static Vector2 _lastSceneViewportSize = new Vector2(1280, 720);

        public static void Run()
        {
            _engine = new GraphicsEngine();
            GameWindow? window = null;

            _engine.LoadEvent += () =>
            {
                LogMessage("Kryon Engine Initialized");
                LogMessage("Controls:");
                LogMessage("  WASD - Move camera");
                LogMessage("  Space - Move up");
                LogMessage("  Shift - Move down");
                LogMessage("  Right Mouse - Look around");
                LogMessage("  Scroll - Zoom");
                LogMessage("  Q - Move Gizmo");
                LogMessage("  E - Rotate Gizmo");
                LogMessage("  R - Scale Gizmo");
                LogMessage("  1, 2, 3 - Switch scenes");
                LogMessage("================================");

                _renderer = _engine.GetSceneRenderer();
                window = _engine.GetWindow();
                _imguiController = new ImGuiController(WindowConfig.Width, WindowConfig.Height);
                _UIRender = new UIRender();

                _engine.TextInputEvent += (e) =>
                {
                    _imguiController?.PressChar((char)e.Unicode);
                };

                EditorUI.Initialize();
                SetupCamera();
            };

            _engine.UpdateEvent += dt =>
            {
                if (_mainCamera == null || window == null) return;


                HandleInput(dt);
                UpdateFPS(dt);

                _imguiController?.Update(window, (float)dt);

                EditorUI.Draw(_engine, _mainCamera, _selectedObject, _isPlaying, _editorCameraSpeed,
                    _lastSceneViewportSize, _currentFps, _currentFrameTime, _consoleMessages,
                    out _selectedObject, out _isPlaying, out _editorCameraSpeed, out _lastSceneViewportSize);

                _UIRender.RenderUI();
            };

            _engine.RenderEvent += dt =>
            {
                _imguiController?.Render();
            };

            _engine.ResizeEvent += (w, h) =>
            {
                _imguiController?.WindowResized(w, h);
            };

            _engine.CloseEvent += () =>
            {
                _imguiController?.Dispose();
                LogMessage("Engine closed");
            };

            _engine.CreateWindow(WindowConfig.Width, WindowConfig.Height, "Kryon Engine - Editor");
            _engine.Run();
        }

        private static void HandleInput(float dt)
        {
            if (_engine == null || _mainCamera == null) return;

            var keyboard = _engine.GetKeyboardState();
            var mouse = _engine.GetMouseState();

            if (!mouse.IsButtonDown(MouseButton.Right))
            {
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
                if (keyboard.IsKeyPressed(Keys.R))
                {
                    TransformGizmo.SetMode(GizmoMode.Scale);
                    LogMessage("Switched to Scale mode");
                }
            }

            if (keyboard.IsKeyPressed(Keys.D1))
            {
                SceneManager.LoadScene("Scene1");
                SetupCamera();
                LogMessage("Loaded Scene1");
            }
            if (keyboard.IsKeyPressed(Keys.D2))
            {
                SceneManager.LoadScene("Scene2");
                SetupCamera();
                LogMessage("Loaded Scene2");
            }
            if (keyboard.IsKeyPressed(Keys.D3))
            {
                SceneManager.LoadScene("Scene3");
                SetupCamera();
                LogMessage("Loaded Scene3");
            }

            if (mouse.IsButtonDown(MouseButton.Right))
            if (!_isPlaying || true)
            {
                float speed = _editorCameraSpeed;
                if (keyboard.IsKeyDown(Keys.LeftControl))
                    speed *= 2.0f;

                if (keyboard.IsKeyDown(Keys.W))
                    _mainCamera.Move(CameraMovement.Forward, dt * speed / 2.5f);
                if (keyboard.IsKeyDown(Keys.S))
                    _mainCamera.Move(CameraMovement.Backward, dt * speed / 2.5f);
                if (keyboard.IsKeyDown(Keys.A))
                    _mainCamera.Move(CameraMovement.Left, dt * speed / 2.5f);
                if (keyboard.IsKeyDown(Keys.D))
                    _mainCamera.Move(CameraMovement.Right, dt * speed / 2.5f);
                if (keyboard.IsKeyDown(Keys.Space))
                    _mainCamera.Move(CameraMovement.Up, dt * speed / 2.5f);
                if (keyboard.IsKeyDown(Keys.LeftShift))
                    _mainCamera.Move(CameraMovement.Down, dt * speed / 2.5f);
            }

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

            if (mouse.IsButtonDown(MouseButton.Right))
            {
                _mainCamera.Rotate(xOffset, yOffset);
            }

            if (mouse.ScrollDelta.Y != 0)
            {
                _mainCamera.Zoom(mouse.ScrollDelta.Y);
            }
        }

        private static void UpdateFPS(float dt)
        {
            _fpsCounter++;
            _fpsTimer += dt;

            if (_fpsTimer >= 1.0)
            {
                _currentFps = _fpsCounter / _fpsTimer;
                _currentFrameTime = 1000.0 / _currentFps;
                _fpsCounter = 0;
                _fpsTimer = 0.0;
            }
        }

        private static void SetupCamera()
        {
            if (SceneManager.ActiveScene == null || _renderer == null)
                return;

            var cameraObject = SceneManager.ActiveScene.FindGameObjectWithTag("MainCamera");

            if (cameraObject == null)
            {
                cameraObject = SceneManager.ActiveScene.CreateGameObject("MainCamera");
                cameraObject.Tag = "MainCamera";
                cameraObject.Transform.SetPosition(0, 2, 5);

                _mainCamera = cameraObject.AddComponent<CameraComponent>();
                _mainCamera.AspectRatio = 1280f / 800f;
            }
            else
            {
                _mainCamera = cameraObject.GetComponent<CameraComponent>();
            }

            _renderer.SetCamera(_mainCamera);
        }

        public static void LogMessage(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            _consoleMessages.Add($"[{timestamp}] {message}");

            if (_consoleMessages.Count > _maxConsoleMessages)
            {
                _consoleMessages.RemoveAt(0);
            }

            Console.WriteLine(message);
        }

        public static GameObject? GetSelectedObject() => _selectedObject;
        public static void SetSelectedObject(GameObject? obj) => _selectedObject = obj;
    }
}