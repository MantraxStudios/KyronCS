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
        private static Camera? _editorCamera;
        private static SceneRenderer? _renderer;
        private static GraphicsEngine? _engine;

        private static GameObject? _selectedObject = null;
        private static UIRender? _UIRender = null;
        private static bool _isPlaying = false;
        private static float _editorCameraSpeed = 2.5f;

        private static List<string> _consoleMessages = new List<string>();
        private static int _maxConsoleMessages = 100;

        private static Vector2 _lastSceneViewportSize = new Vector2(1280, 720);

        // Posición y rotación inicial de la cámara
        private static readonly OpenTK.Mathematics.Vector3 _initialCameraPosition = new OpenTK.Mathematics.Vector3(0, 2, 5);
        private static readonly float _initialCameraYaw = -90.0f;
        private static readonly float _initialCameraPitch = 0.0f;

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
                LogMessage("  Ctrl+R - Reset Camera");
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

            _engine.FileDropEvent += OnFilesDropped;

            _engine.UpdateEvent += dt =>
            {
                if (_editorCamera == null || window == null) return;

                HandleInput(dt);
                UpdateFPS(dt);

                _imguiController?.Update(window, (float)dt);

                EditorUI.Draw(_engine, _editorCamera, _isPlaying, _editorCameraSpeed,
                    _lastSceneViewportSize, _currentFps, _currentFrameTime, _consoleMessages, out _isPlaying, out _editorCameraSpeed, out _lastSceneViewportSize);

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


        private static void OnFilesDropped(string[] filePaths)
        {
            EditorUI._assets.HandleExternalDrop(filePaths, "");
        }
        private static void HandleInput(float dt)
        {
            if (_engine == null || _editorCamera == null) return;

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
                if (keyboard.IsKeyPressed(Keys.R) && !keyboard.IsKeyDown(Keys.LeftControl))
                {
                    TransformGizmo.SetMode(GizmoMode.Scale);
                    LogMessage("Switched to Scale mode");
                }
            }

            // Ctrl+R para reiniciar cámara
            if (keyboard.IsKeyDown(Keys.LeftControl) && keyboard.IsKeyPressed(Keys.R))
            {
                ResetCamera();
                LogMessage("Camera reset to initial position");
            }

            if (keyboard.IsKeyPressed(Keys.D1))
            {
                SceneManager.LoadScene("Scene1");
                LogMessage("Loaded Scene1");
            }
            if (keyboard.IsKeyPressed(Keys.D2))
            {
                SceneManager.LoadScene("Scene2");
                LogMessage("Loaded Scene2");
            }
            if (keyboard.IsKeyPressed(Keys.D3))
            {
                SceneManager.LoadScene("Scene3");
                LogMessage("Loaded Scene3");
            }

            if (mouse.IsButtonDown(MouseButton.Right) && EditorActions.IsHoveringScene)
            {
                float speed = _editorCameraSpeed;
                if (keyboard.IsKeyDown(Keys.LeftControl))
                    speed *= 2.0f;

                if (keyboard.IsKeyDown(Keys.W))
                    _editorCamera.Move(CameraMovement.Forward, dt * speed / 2.5f);
                if (keyboard.IsKeyDown(Keys.S))
                    _editorCamera.Move(CameraMovement.Backward, dt * speed / 2.5f);
                if (keyboard.IsKeyDown(Keys.A))
                    _editorCamera.Move(CameraMovement.Left, dt * speed / 2.5f);
                if (keyboard.IsKeyDown(Keys.D))
                    _editorCamera.Move(CameraMovement.Right, dt * speed / 2.5f);
                if (keyboard.IsKeyDown(Keys.Space))
                    _editorCamera.Move(CameraMovement.Up, dt * speed / 2.5f);
                if (keyboard.IsKeyDown(Keys.LeftShift))
                    _editorCamera.Move(CameraMovement.Down, dt * speed / 2.5f);
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

            if (mouse.IsButtonDown(MouseButton.Right) && GraphicsEngine.Instance.GetSceneRenderer().GetCamera().IsPerspective && EditorActions.IsHoveringScene)
            {
                _editorCamera.Rotate(xOffset, yOffset);
            }

            if (mouse.ScrollDelta.Y != 0 && EditorActions.IsHoveringScene)
            {
                _editorCamera.Zoom(mouse.ScrollDelta.Y);
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
            if (_renderer == null)
                return;

            // Crear cámara de editor independiente
            _editorCamera = _renderer.GetCamera();
            _editorCamera.Position = _initialCameraPosition;
            _editorCamera.AspectRatio = 1280f / 800f;
        }

        private static void ResetCamera()
        {
            if (_editorCamera == null)
                return;

            _editorCamera.Position = _initialCameraPosition;

            // Resetear yaw y pitch usando reflexión o propiedades públicas si están disponibles
            var cameraType = _editorCamera.GetType();

            var yawProperty = cameraType.GetProperty("Yaw");
            if (yawProperty != null && yawProperty.CanWrite)
            {
                yawProperty.SetValue(_editorCamera, _initialCameraYaw);
            }

            var pitchProperty = cameraType.GetProperty("Pitch");
            if (pitchProperty != null && pitchProperty.CanWrite)
            {
                pitchProperty.SetValue(_editorCamera, _initialCameraPitch);
            }
        }

        public static void LogMessage(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            _consoleMessages.Add($"[{timestamp}] {message}");

            if (_consoleMessages.Count > _maxConsoleMessages)
            {
                _consoleMessages.RemoveAt(0);
            }
        }

        public static GameObject? GetSelectedObject() => _selectedObject;
        public static void SetSelectedObject(GameObject? obj) => _selectedObject = obj;
    }
}