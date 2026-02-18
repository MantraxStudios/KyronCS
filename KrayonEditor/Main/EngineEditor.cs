using ImGuiNET;
using KrayonCore;
using KrayonCore.Components.Components;
using KrayonCore.Components.RenderComponents;
using KrayonCore.Core.Attributes;
using KrayonCore.Core.Components;
using KrayonCore.Core.Input;
using KrayonCore.EventSystem;
using KrayonCore.Graphics.Camera;
using KrayonCore.Graphics.GameUI;
using KrayonCore.GraphicsData;
using KrayonCore.UI;
using KrayonEditor.UI;
using OpenTK.Mathematics;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Vector2 = System.Numerics.Vector2;

namespace KrayonEditor.Main
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
        private static bool _isPlaying = false;
        private static float _editorCameraSpeed = 2.5f;
        private static Vector2 _lastSceneViewportSize = new(1280, 720);
        private static GameObject? _hoveredObject = null;

        // ── GIZMOS GLOBALES ──────────────────────────────────────────────────
        /// <summary>
        /// true  → muestra gizmos de TODOS los objetos de la escena
        /// false → solo muestra el gizmo del objeto seleccionado
        /// </summary>
        public static bool ShowAllGizmos { get; set; } = false;

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
            LogMessage("================================");

            _renderer = _engine!.GetSceneRenderer();
            _window = _engine.Window;
            _imguiController = new ImGuiController(WindowConfig.Width, WindowConfig.Height);

            _engine.OnTextInput += e => _imguiController?.PressChar((char)e.Unicode);
            _engine!.Window.MouseMove += (e) => UIInputManager.SetMousePos(e.X, e.Y);
            _engine!.Window.MouseDown += (e) => { if (e.Button == MouseButton.Left) UIInputManager.SetLeftButton(true); };
            _engine!.Window.MouseUp += (e) => { if (e.Button == MouseButton.Left) UIInputManager.SetLeftButton(false); };

            EditorUI.Initialize();
            SetupCamera();

            var sceneRenderer = GraphicsEngine.Instance.GetSceneRenderer();

            // ── Gizmo Rigidbody ───────────────────────────────────────────────
            sceneRenderer.AttachRender("gizmo_rigidbody", (view, projection, cameraPos) =>
            {
                if (ShowAllGizmos)
                {
                    var allObjects = SceneManager.ActiveScene?.GetAllGameObjects();
                    if (allObjects == null) return;

                    foreach (var go in allObjects)
                    {
                        if (!go.HasComponent<Rigidbody>()) continue;
                        var rb = go.GetComponent<Rigidbody>();
                        bool isSelected = go == EditorActions.SelectedObject;

                        Vector4 color = isSelected
                            ? new Vector4(0.0f, 1.0f, 1.0f, 1.0f)   // cyan brillante si está seleccionado
                            : new Vector4(0.0f, 0.8f, 0.8f, 0.5f);  // cyan tenue para el resto
                        float lw = isSelected ? 2.5f : 1.2f;

                        DrawRigidbodyGizmo(rb, go, view, projection, color, lw);
                    }
                }
                else if (EditorActions.SelectedObject?.HasComponent<Rigidbody>() == true)
                {
                    var rb = EditorActions.SelectedObject.GetComponent<Rigidbody>();
                    var go = EditorActions.SelectedObject;
                    DrawRigidbodyGizmo(rb, go, view, projection,
                        new Vector4(0.0f, 1.0f, 1.0f, 1.0f), lineWidth: 2.5f);
                }
            });


            // ── Gizmo Audio ───────────────────────────────────────────────────
            sceneRenderer.AttachRender("gizmo_audio", (view, projection, cameraPos) =>
            {
                if (ShowAllGizmos)
                {
                    var allObjects = SceneManager.ActiveScene?.GetAllGameObjects();
                    if (allObjects == null) return;

                    foreach (var go in allObjects)
                    {
                        if (!go.HasComponent<AudioSource>()) continue;
                        var audioSource = go.GetComponent<AudioSource>();
                        var position = go.Transform.Position;
                        bool isSelected = go == EditorActions.SelectedObject;
                        float alpha = isSelected ? 1.0f : 0.45f;
                        float lw = isSelected ? 2.0f : 1.0f;

                        Matrix4 modelMin = Matrix4.CreateScale(audioSource.MinDistance * 2.0f) *
                                           Matrix4.CreateTranslation(position);
                        GizmoSphere.Draw(modelMin, view, projection,
                            new Vector4(0.0f, 1.0f, 0.0f, alpha), lw);

                        Matrix4 modelMax = Matrix4.CreateScale(audioSource.MaxDistance * 2.0f) *
                                           Matrix4.CreateTranslation(position);
                        GizmoSphere.Draw(modelMax, view, projection,
                            new Vector4(1.0f, 0.0f, 0.0f, alpha), lw);
                    }
                }
                else if (EditorActions.SelectedObject?.HasComponent<AudioSource>() == true)
                {
                    var audioSource = EditorActions.SelectedObject.GetComponent<AudioSource>();
                    var position = EditorActions.SelectedObject.Transform.Position;

                    Matrix4 modelMin = Matrix4.CreateScale(audioSource.MinDistance * 2.0f) *
                                       Matrix4.CreateTranslation(position);
                    GizmoSphere.Draw(modelMin, view, projection,
                        new Vector4(0.0f, 1.0f, 0.0f, 1.0f), 2.0f);

                    Matrix4 modelMax = Matrix4.CreateScale(audioSource.MaxDistance * 2.0f) *
                                       Matrix4.CreateTranslation(position);
                    GizmoSphere.Draw(modelMax, view, projection,
                        new Vector4(1.0f, 0.0f, 0.0f, 1.0f), 1.5f);
                }
            });

            // ── Gizmo Lights ──────────────────────────────────────────────────
            sceneRenderer.AttachRender("gizmo_lights", (view, projection, cameraPos) =>
            {
                if (ShowAllGizmos)
                {
                    var allObjects = SceneManager.ActiveScene?.GetAllGameObjects();
                    if (allObjects == null) return;

                    foreach (var go in allObjects)
                    {
                        if (!go.HasComponent<Light>()) continue;
                        bool isSelected = go == EditorActions.SelectedObject;
                        DrawLightGizmo(go.GetComponent<Light>(), view, projection, isSelected);
                    }
                }
                else if (EditorActions.SelectedObject?.HasComponent<Light>() == true)
                {
                    DrawLightGizmo(EditorActions.SelectedObject.GetComponent<Light>(),
                        view, projection, isSelected: true);
                }
            });

            // ── Gizmo Camera ──────────────────────────────────────────────────
            sceneRenderer.AttachRender("gizmo_camera", (view, projection, cameraPos) =>
            {
                if (ShowAllGizmos)
                {
                    var allObjects = SceneManager.ActiveScene?.GetAllGameObjects();
                    if (allObjects == null) return;

                    foreach (var go in allObjects)
                    {
                        if (!go.HasComponent<CameraComponent>()) continue;
                        bool isSelected = go == EditorActions.SelectedObject;
                        DrawCameraGizmo(go, view, projection, isSelected);
                    }
                }
                else if (EditorActions.SelectedObject?.HasComponent<CameraComponent>() == true)
                {
                    DrawCameraGizmo(EditorActions.SelectedObject, view, projection, isSelected: true);
                }
            });

            // ── Gizmo Hover ───────────────────────────────────────────────────
            sceneRenderer.AttachRender("gizmo_hover", (view, projection, cameraPos) =>
            {
                if (_hoveredObject == null) return;
                if (_hoveredObject == EditorActions.SelectedObject) return;

                Matrix4 model = _hoveredObject.Transform.GetWorldMatrix();
                GizmoCube.Draw(model, view, projection,
                    new Vector4(1.0f, 1.0f, 1.0f, 0.4f), lineWidth: 1.5f);
            });
        }

        // ── Helpers de gizmos ─────────────────────────────────────────────────

        private static void DrawRigidbodyGizmo(
    Rigidbody rb, GameObject go,
    Matrix4 view, Matrix4 projection,
    Vector4 color, float lineWidth)
        {
            switch (rb.ShapeType)
            {
                case ShapeType.Box:
                    {
                        // ShapeSize es la semidimensión → escalar x2 para obtener el tamaño completo
                        Matrix4 model = Matrix4.CreateScale(rb.ShapeSize * 2.0f)
                                      * go.Transform.GetWorldMatrix();
                        GizmoCube.Draw(model, view, projection, color, lineWidth);
                        break;
                    }

                case ShapeType.Sphere:
                    {
                        // ShapeSize.X es el radio
                        float diameter = rb.ShapeSize.X * 2.0f;
                        Matrix4 model = Matrix4.CreateScale(diameter)
                                      * go.Transform.GetWorldMatrix();
                        GizmoSphere.Draw(model, view, projection, color, lineWidth);
                        break;
                    }

                case ShapeType.Capsule:
                    {
                        // Convención de Bepu en CreateCapsule: (halfLength, radius)
                        //   ShapeSize.Y = halfLength del cilindro central
                        //   ShapeSize.X = radio
                        float radius = rb.ShapeSize.X;
                        float halfLength = rb.ShapeSize.Y;

                        // GizmoCapsule está construido en espacio local con:
                        //   - radio unitario = 0.5  → escalar XZ por diámetro
                        //   - halfHeight del cilindro = 0.5  → escalar Y por (cilindro + hemisferios)
                        //   totalHeight = cilindro * 2 + radio * 2  (dos semiesferas = 1 esfera completa)
                        float diameter = radius * 2.0f;
                        float totalHeight = halfLength * 2.0f + diameter;

                        Matrix4 model = Matrix4.CreateScale(diameter, totalHeight, diameter)
                                      * go.Transform.GetWorldMatrix();
                        GizmoCapsule.Draw(model, view, projection, color, lineWidth);
                        break;
                    }
            }
        }


        private static void DrawLightGizmo(Light light, Matrix4 view, Matrix4 projection, bool isSelected)
        {
            var position  = light.GetPosition();
            var direction = light.GetDirection();
            float alpha   = isSelected ? 1.0f : 0.50f;
            float lw      = isSelected ? 2.5f : 1.2f;

            switch (light.Type)
            {
                case LightType.Point:
                {
                    float radius = CalculateLightRadius(light.Intensity,
                        light.Constant, light.Linear, light.Quadratic);
                    Vector4 color = new(1.0f, 1.0f, 0.0f, alpha);

                    // Tres círculos ortogonales
                    GizmoCircle.Draw(
                        Matrix4.CreateScale(radius * 2.0f) * Matrix4.CreateTranslation(position),
                        view, projection, color, lw);

                    GizmoCircle.Draw(
                        Matrix4.CreateRotationX(MathHelper.DegreesToRadians(90)) *
                        Matrix4.CreateScale(radius * 2.0f) * Matrix4.CreateTranslation(position),
                        view, projection, color, lw);

                    GizmoCircle.Draw(
                        Matrix4.CreateRotationY(MathHelper.DegreesToRadians(90)) *
                        Matrix4.CreateScale(radius * 2.0f) * Matrix4.CreateTranslation(position),
                        view, projection, color, lw);
                    break;
                }

                case LightType.Spot:
                {
                    float coneLength = CalculateLightRadius(light.Intensity,
                        light.Constant, light.Linear, light.Quadratic);
                    float coneRadius = MathF.Tan(
                        MathHelper.DegreesToRadians(light.OuterCutOffDegrees)) * coneLength;
                    Vector4 color = new(1.0f, 0.5f, 0.0f, alpha);

                    Matrix4 rotation = CreateLookAtRotation(Vector3.UnitZ, direction);
                    Matrix4 modelSpot = Matrix4.CreateScale(coneRadius * 2.0f, coneRadius * 2.0f, coneLength) *
                                        rotation * Matrix4.CreateTranslation(position);
                    GizmoCone.Draw(modelSpot, view, projection, color, lw);

                    // Flecha de dirección
                    Matrix4 arrowModel = Matrix4.CreateScale(0.5f) * rotation *
                                         Matrix4.CreateTranslation(position);
                    GizmoArrow.Draw(arrowModel, view, projection,
                        new Vector4(1, 1, 1, alpha), lw);
                    break;
                }

                case LightType.Directional:
                {
                    Matrix4 rotDir = CreateLookAtRotation(Vector3.UnitZ, direction);
                    Matrix4 modelDir = Matrix4.CreateScale(2.0f) * rotDir *
                                       Matrix4.CreateTranslation(position);
                    Vector4 color = new(1.0f, 1.0f, 1.0f, alpha);

                    // Esfera pequeña de posición + flecha de dirección
                    GizmoSphere.Draw(
                        Matrix4.CreateScale(0.15f) * Matrix4.CreateTranslation(position),
                        view, projection, color, lw);
                    GizmoArrow.Draw(modelDir, view, projection, color, lw);
                    break;
                }
            }
        }

        private static void DrawCameraGizmo(GameObject go, Matrix4 view, Matrix4 projection, bool isSelected)
        {
            var cameraComp = go.GetComponent<CameraComponent>();
            var transform  = go.GetComponent<Transform>();
            if (cameraComp?.RenderCamera == null || transform == null) return;

            Vector3 position = transform.GetWorldPosition();
            Vector3 forward  = transform.Forward;
            Vector3 up       = transform.Up;
            float alpha      = isSelected ? 1.0f : 0.45f;
            float lw         = isSelected ? 2.5f : 1.2f;
            Vector4 color    = new(0.0f, 1.0f, 0.5f, alpha);

            if (cameraComp.ProjectionMode == ProjectionMode.Perspective)
            {
                GizmoFrustum.DrawPerspective(
                    position, forward, up,
                    cameraComp.Fov, cameraComp.AspectRatio,
                    cameraComp.NearPlane, cameraComp.FarPlane,
                    view, projection, color, lw);
            }
            else
            {
                GizmoFrustum.DrawOrthographic(
                    position, forward, up,
                    cameraComp.OrthoSize, cameraComp.AspectRatio,
                    cameraComp.NearPlane, cameraComp.FarPlane,
                    view, projection, color, lw);
            }
        }

        // ── Update ────────────────────────────────────────────────────────────
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

            System.Numerics.Vector2 globalMouse = ImGui.GetMousePos();
            System.Numerics.Vector2 vpOrigin = EditorActions.ViewPortPosition;
            System.Numerics.Vector2 vpSize = new System.Numerics.Vector2(GraphicsEngine.Instance.GetSceneFrameBuffer().Width, GraphicsEngine.Instance.GetSceneFrameBuffer().Height);

            UIInputManager.SetKeyboardState(GraphicsEngine.Instance.GetKeyboardState().GetSnapshot());
            UIInputManager.SetMousePosFromViewport(
                new OpenTK.Mathematics.Vector2(globalMouse.X, globalMouse.Y),
                new OpenTK.Mathematics.Vector2(vpOrigin.X, vpOrigin.Y),
                new OpenTK.Mathematics.Vector2(vpSize.X, vpSize.Y)
            );
            UIInputManager.SetLeftButton(
                GraphicsEngine.Instance.GetMouseState().IsButtonDown(MouseButton.Left));
            UIInputManager.UpdateAll();
            UICanvasManager.Update(dt);
        }

        private static void UpdateHoveredObject()
        {
            if (!EditorActions.IsHoveringScene)
            {
                _hoveredObject = null;
                return;
            }

            var mouse = _engine!.GetMouseState();
            if (mouse.IsButtonDown(MouseButton.Right))
            {
                _hoveredObject = null;
                return;
            }

            try
            {
                var camera = GraphicsEngine.Instance.GetSceneRenderer().GetCamera();
                int screenWidth  = GraphicsEngine.Instance.GetSceneFrameBuffer().Width;
                int screenHeight = GraphicsEngine.Instance.GetSceneFrameBuffer().Height;

                System.Numerics.Vector2 globalMousePos = ImGui.GetMousePos();
                System.Numerics.Vector2 sceneWindowPos = EditorActions.ViewPortPosition;

                OpenTK.Mathematics.Vector2 relativeMousePos = new(
                    globalMousePos.X - sceneWindowPos.X,
                    globalMousePos.Y - sceneWindowPos.Y
                );

                EventSystem.ScreenToWorldRay(relativeMousePos, camera, screenWidth, screenHeight,
                    out OpenTK.Mathematics.Vector3 rayOrigin,
                    out OpenTK.Mathematics.Vector3 rayDir);

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
            var mouse    = _engine.GetMouseState();

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
            float step  = dt * speed / 2.5f;

            if (keyboard.IsKeyDown(Keys.W))         _editorCamera!.Move(CameraMovement.Forward, step);
            if (keyboard.IsKeyDown(Keys.S))         _editorCamera!.Move(CameraMovement.Backward, step);
            if (keyboard.IsKeyDown(Keys.A))         _editorCamera!.Move(CameraMovement.Left, step);
            if (keyboard.IsKeyDown(Keys.D))         _editorCamera!.Move(CameraMovement.Right, step);
            if (keyboard.IsKeyDown(Keys.Space))     _editorCamera!.Move(CameraMovement.Up, step);
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
            if (mouse.ScrollDelta.Y != 0 &&
                EditorActions.IsHoveringScene &&
                !InputSystem.GetKeyDown(Keys.LeftControl))
                _editorCamera!.Zoom(mouse.ScrollDelta.Y);
        }

        // ── FPS ──────────────────────────────────────────────────────────────
        private static void UpdateFPS(float dt)
        {
            _fpsCounter++;
            _fpsTimer += dt;
            if (_fpsTimer < 1.0) return;

            _currentFps       = _fpsCounter / _fpsTimer;
            _currentFrameTime = 1000.0 / _currentFps;
            _fpsCounter = 0;
            _fpsTimer   = 0.0;
        }

        // ── Cámara ───────────────────────────────────────────────────────────
        private static void SetupCamera()
        {
            if (_renderer is null) return;
            _editorCamera = _renderer.GetCamera();
            _editorCamera.Position    = _initialCameraPosition;
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

        // ── Helpers ──────────────────────────────────────────────────────────
        static float CalculateLightRadius(float intensity, float constant, float linear, float quadratic)
        {
            float threshold  = 5.0f / 256.0f;
            float a = quadratic;
            float b = linear;
            float c = constant - (intensity / threshold);

            float discriminant = b * b - 4 * a * c;
            if (discriminant < 0) return 10.0f;

            float radius = (-b + MathF.Sqrt(discriminant)) / (2 * a);
            return MathF.Max(radius, 1.0f);
        }

        static Matrix4 CreateLookAtRotation(Vector3 from, Vector3 to)
        {
            Vector3 forward = Vector3.Normalize(to);
            Vector3 right   = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, forward));
            if (right.LengthSquared < 0.001f)
                right = Vector3.Normalize(Vector3.Cross(Vector3.UnitX, forward));
            Vector3 up = Vector3.Cross(forward, right);

            return new Matrix4(
                new Vector4(right,   0),
                new Vector4(up,      0),
                new Vector4(forward, 0),
                new Vector4(0, 0, 0, 1)
            );
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