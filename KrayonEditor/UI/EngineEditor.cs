using ImGuiNET;
using KrayonCore;
using KrayonCore.Components.Components;
using KrayonCore.Components.RenderComponents;
using KrayonCore.Core.Attributes;
using KrayonCore.Core.Components;
using KrayonCore.Core.Input;
using KrayonCore.EventSystem;
using KrayonCore.Graphics.Camera;
using KrayonCore.GraphicsData;
using OpenTK.Mathematics;
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
        private static GameObject? _hoveredObject = null;

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

            var sceneRenderer = GraphicsEngine.Instance.GetSceneRenderer();

            sceneRenderer.AttachRender("gizmo_rigidbody", (view, projection, cameraPos) =>
            {
                if (EditorActions.SelectedObject != null && EditorActions.SelectedObject.HasComponent<Rigidbody>())
                {
                    var rb = EditorActions.SelectedObject.GetComponent<Rigidbody>();
                    Vector3 shapeSize = rb.ShapeSize;

                    // Multiplicar el ShapeSize por 2 para que coincida con el tamaño del cubo gizmo
                    Vector4 color = new Vector4(0.0f, 1.0f, 1.0f, 1.0f); // Cyan
                    Matrix4 model = Matrix4.CreateScale(shapeSize * 2.0f) *
                                    EditorActions.SelectedObject.Transform.GetWorldMatrix();

                    GizmoCube.Draw(model, view, projection, color, lineWidth: 2.5f);
                }
            });

            sceneRenderer.AttachRender("gizmo_audio", (view, projection, cameraPos) =>
            {
                if (EditorActions.SelectedObject != null && EditorActions.SelectedObject.HasComponent<AudioSource>())
                {
                    var audioSource = EditorActions.SelectedObject.GetComponent<AudioSource>();
                    var position = EditorActions.SelectedObject.Transform.Position;

                    float minDistance = audioSource.MinDistance;
                    float maxDistance = audioSource.MaxDistance;

                    Matrix4 modelMin = Matrix4.CreateScale(minDistance * 2.0f) * Matrix4.CreateTranslation(position);
                    Vector4 colorMin = new Vector4(0.0f, 1.0f, 0.0f, 1.0f); 
                    GizmoSphere.Draw(modelMin, view, projection, colorMin, lineWidth: 2.0f);

                    Matrix4 modelMax = Matrix4.CreateScale(maxDistance * 2.0f) * Matrix4.CreateTranslation(position);
                    Vector4 colorMax = new Vector4(1.0f, 0.0f, 0.0f, 1.0f); 
                    GizmoSphere.Draw(modelMax, view, projection, colorMax, lineWidth: 1.5f);
                }
            });

            sceneRenderer.AttachRender("gizmo_lights", (view, projection, cameraPos) =>
            {
                if (EditorActions.SelectedObject != null && EditorActions.SelectedObject.HasComponent<Light>())
                {
                    var light = EditorActions.SelectedObject.GetComponent<Light>();
                    var position = light.GetPosition();
                    var direction = light.GetDirection();

                    switch (light.Type)
                    {
                        case LightType.Point:
                            float radius = CalculateLightRadius(light.Intensity, light.Constant, light.Linear, light.Quadratic);

                            Vector4 colorPoint = new Vector4(1.0f, 1.0f, 0.0f, 1.0f);

                            Matrix4 modelXY = Matrix4.CreateScale(radius * 2.0f) * Matrix4.CreateTranslation(position);
                            GizmoCircle.Draw(modelXY, view, projection, colorPoint, 2.0f);

                            Matrix4 modelXZ = Matrix4.CreateRotationX(MathHelper.DegreesToRadians(90)) *
                                              Matrix4.CreateScale(radius * 2.0f) *
                                              Matrix4.CreateTranslation(position);
                            GizmoCircle.Draw(modelXZ, view, projection, colorPoint, 2.0f);

                            Matrix4 modelYZ = Matrix4.CreateRotationY(MathHelper.DegreesToRadians(90)) *
                                              Matrix4.CreateScale(radius * 2.0f) *
                                              Matrix4.CreateTranslation(position);
                            GizmoCircle.Draw(modelYZ, view, projection, colorPoint, 2.0f);
                            break;

                        case LightType.Spot:
                            float coneLength = CalculateLightRadius(light.Intensity, light.Constant, light.Linear, light.Quadratic);
                            float coneRadius = MathF.Tan(MathHelper.DegreesToRadians(light.OuterCutOffDegrees)) * coneLength;

                            Matrix4 rotation = CreateLookAtRotation(Vector3.UnitZ, direction);
                            Matrix4 modelSpot = Matrix4.CreateScale(coneRadius * 2.0f, coneRadius * 2.0f, coneLength) *
                                                rotation *
                                                Matrix4.CreateTranslation(position);

                            Vector4 colorSpot = new Vector4(1.0f, 0.5f, 0.0f, 1.0f); 
                            GizmoCone.Draw(modelSpot, view, projection, colorSpot, 2.0f);

                            Matrix4 arrowModel = Matrix4.CreateScale(0.5f) * rotation * Matrix4.CreateTranslation(position);
                            GizmoArrow.Draw(arrowModel, view, projection, new Vector4(1, 1, 1, 1), 2.5f);
                            break;

                        case LightType.Directional:
                            Matrix4 rotDir = CreateLookAtRotation(Vector3.UnitZ, direction);
                            Matrix4 modelDir = Matrix4.CreateScale(2.0f) * rotDir * Matrix4.CreateTranslation(position);

                            Vector4 colorDir = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
                            GizmoArrow.Draw(modelDir, view, projection, colorDir, 3.0f);
                            break;
                    }
                }
            });

            sceneRenderer.AttachRender("gizmo_camera", (view, projection, cameraPos) =>
            {
                if (EditorActions.SelectedObject != null && EditorActions.SelectedObject.HasComponent<CameraComponent>())
                {
                    var cameraComp = EditorActions.SelectedObject.GetComponent<CameraComponent>();
                    var transform = EditorActions.SelectedObject.GetComponent<Transform>();

                    if (cameraComp.RenderCamera == null || transform == null) return;

                    var cam = cameraComp.RenderCamera.Camera;
                    Vector3 position = transform.GetWorldPosition();
                    Vector3 forward = transform.Forward;
                    Vector3 up = transform.Up;

                    Vector4 color = new Vector4(0.0f, 1.0f, 0.5f, 1.0f);

                    if (cameraComp.ProjectionMode == ProjectionMode.Perspective)
                    {
                        GizmoFrustum.DrawPerspective(
                            position, forward, up,
                            cameraComp.Fov,
                            cameraComp.AspectRatio,
                            cameraComp.NearPlane,
                            cameraComp.FarPlane,
                            view, projection, color, 2.5f
                        );
                    }
                    else // Orthographic
                    {
                        GizmoFrustum.DrawOrthographic(
                            position, forward, up,
                            cameraComp.OrthoSize,
                            cameraComp.AspectRatio,
                            cameraComp.NearPlane,
                            cameraComp.FarPlane,
                            view, projection, color, 2.5f
                        );
                    }
                }
            });

            sceneRenderer.AttachRender("gizmo_hover", (view, projection, cameraPos) =>
            {
                if (_hoveredObject == null) return;
                if (_hoveredObject == EditorActions.SelectedObject) return;

                Matrix4 model = _hoveredObject.Transform.GetWorldMatrix();
                Vector4 color = new Vector4(1.0f, 1.0f, 1.0f, 0.4f);
                GizmoCube.Draw(model, view, projection, color, lineWidth: 1.5f);
            });
        }

        static float CalculateLightRadius(float intensity, float constant, float linear, float quadratic)
        {
            float threshold = 5.0f / 256.0f;
            float maxIntensity = intensity;

            float a = quadratic;
            float b = linear;
            float c = constant - (maxIntensity / threshold);

            float discriminant = b * b - 4 * a * c;
            if (discriminant < 0) return 10.0f; 

            float radius = (-b + MathF.Sqrt(discriminant)) / (2 * a);
            return MathF.Max(radius, 1.0f);
        }

        static Matrix4 CreateLookAtRotation(Vector3 from, Vector3 to)
        {
            Vector3 forward = Vector3.Normalize(to);
            Vector3 right = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, forward));
            if (right.LengthSquared < 0.001f) 
                right = Vector3.Normalize(Vector3.Cross(Vector3.UnitX, forward));
            Vector3 up = Vector3.Cross(forward, right);

            return new Matrix4(
                new Vector4(right, 0),
                new Vector4(up, 0),
                new Vector4(forward, 0),
                new Vector4(0, 0, 0, 1)
            );
        }

        private static void HandleUpdate(float dt)
        {
            if (_editorCamera is null || _window is null) return;

            HandleInput(dt);
            UpdateFPS(dt);
            UpdateHoveredObject();

            _imguiController?.Update(_window, dt);
            EditorNotifications.Draw(dt);
            EditorUI.Draw(
                _engine!, _editorCamera, _isPlaying, _editorCameraSpeed,
                _lastSceneViewportSize, _currentFps, _currentFrameTime, _consoleMessages,
                out _isPlaying, out _editorCameraSpeed, out _lastSceneViewportSize
            );

            _uiRender?.RenderUI();
        }

        private static void UpdateHoveredObject()
        {
            // Solo detectar si el mouse está sobre la escena y no hay click derecho activo
            if (!EditorActions.IsHoveringScene)
            {
                _hoveredObject = null;
                return;
            }

            var mouse = _engine!.GetMouseState();

            // No hacer hover si estamos rotando la cámara
            if (mouse.IsButtonDown(MouseButton.Right))
            {
                _hoveredObject = null;
                return;
            }

            try
            {
                var camera = GraphicsEngine.Instance.GetSceneRenderer().GetCamera();
                int screenWidth = GraphicsEngine.Instance.GetSceneFrameBuffer().Width;
                int screenHeight = GraphicsEngine.Instance.GetSceneFrameBuffer().Height;

                // Posición del mouse relativa al viewport de la escena
                System.Numerics.Vector2 globalMousePos = ImGui.GetMousePos();
                System.Numerics.Vector2 sceneWindowPos = EditorActions.ViewPortPosition;
                OpenTK.Mathematics.Vector2 relativeMousePos = new(
                    globalMousePos.X - sceneWindowPos.X,
                    globalMousePos.Y - sceneWindowPos.Y
                );

                EventSystem.ScreenToWorldRay(relativeMousePos, camera, screenWidth, screenHeight,
                    out OpenTK.Mathematics.Vector3 rayOrigin, out OpenTK.Mathematics.Vector3 rayDir);

                _hoveredObject = EventSystem.GetObjectByRay(rayOrigin, rayDir);
            }
            catch
            {
                _hoveredObject = null;
            }
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
            if (mouse.ScrollDelta.Y != 0 && EditorActions.IsHoveringScene && !InputSystem.GetKeyDown(Keys.LeftControl))
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