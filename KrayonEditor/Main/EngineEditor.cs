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
using KrayonCore.Utilities;
using KrayonEditor.UI;
using OpenTK.Mathematics;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Vector2 = System.Numerics.Vector2;

namespace KrayonEditor.Main
{
    internal static class EngineEditor
    {
        private static bool _firstMouse = true;
        private static float _lastX = 640f;
        private static float _lastY = 400f;

        private static readonly OpenTK.Mathematics.Vector3 _initialCameraPosition = new(0, 2, 5);
        private const float _initialCameraYaw = -90.0f;
        private const float _initialCameraPitch = 0.0f;

        private static double _fpsTimer = 0.0;
        private static int _fpsCounter = 0;
        private static double _currentFps = 0.0;
        private static double _currentFrameTime = 0.0;

        private static ImGuiController? _imguiController;
        private static Camera? _editorCamera;
        private static SceneRenderer? _renderer;
        private static GraphicsEngine? _engine;
        private static GameWindow? _window;

        private static GameObject? _selectedObject = null;
        private static bool _isPlaying = false;
        private static float _editorCameraSpeed = 2.5f;
        private static Vector2 _lastSceneViewportSize = new(1280, 720);
        private static GameObject? _hoveredObject = null;

        public static bool ShowAllGizmos { get; set; } = false;

        private static readonly List<string> _consoleMessages = new();
        private const int _maxConsoleMessages = 100;

        public static void Run()
        {
            _engine = new GraphicsEngine();

            _engine.OnLoad += HandleLoad;
            _engine.OnUpdate += HandleUpdate;
            _engine.OnRender += HandleRender;
            _engine.OnResize += HandleResize;
            _engine.OnClose += HandleClose;
            _engine.OnFileDrop += OnFilesDropped;
            _engine.OnTryClose += HandleWindowTryClose;

            SceneManager.OnSceneLoaded += _ => RefreshEditorState();

            _engine.CreateWindow(WindowConfig.Width, WindowConfig.Height, "Kryon Engine - Editor");
            _engine.Run();
        }

        private static void RefreshEditorState()
        {
            _editorCamera = GraphicsEngine.Instance.GetSceneRenderer().GetCamera();
            _editorCamera.Position = _initialCameraPosition;
            var type = _editorCamera.GetType();
            type.GetProperty("Yaw")?.SetValue(_editorCamera, _initialCameraYaw);
            type.GetProperty("Pitch")?.SetValue(_editorCamera, _initialCameraPitch);

            AttachGizmos(GraphicsEngine.Instance.GetSceneRenderer());
        }

        private static void AttachGizmos(SceneRenderer sceneRenderer)
        {
            sceneRenderer.AttachRender("gizmo_rigidbody", (view, projection, cameraPos) =>
            {
                if (ShowAllGizmos)
                {
                    var allObjects = SceneManager.PrimaryScene?.GetAllGameObjects();
                    if (allObjects == null) return;
                    foreach (var go in allObjects)
                    {
                        if (!go.HasComponent<Rigidbody>()) continue;
                        bool isSelected = go == EditorActions.SelectedObject;
                        DrawRigidbodyGizmo(go.GetComponent<Rigidbody>(), go, view, projection,
                            isSelected ? new Vector4(0f, 1f, 1f, 1f) : new Vector4(0f, 0.8f, 0.8f, 0.5f),
                            isSelected ? 2.5f : 1.2f);
                    }
                }
                else if (EditorActions.SelectedObject?.HasComponent<Rigidbody>() == true)
                {
                    DrawRigidbodyGizmo(EditorActions.SelectedObject.GetComponent<Rigidbody>(),
                        EditorActions.SelectedObject, view, projection,
                        new Vector4(0f, 1f, 1f, 1f), lineWidth: 2.5f);
                }
            });

            sceneRenderer.AttachRender("gizmo_audio", (view, projection, cameraPos) =>
            {
                if (ShowAllGizmos)
                {
                    var allObjects = SceneManager.PrimaryScene?.GetAllGameObjects();
                    if (allObjects == null) return;
                    foreach (var go in allObjects)
                    {
                        if (!go.HasComponent<AudioSource>()) continue;
                        var audioSource = go.GetComponent<AudioSource>();
                        var position = go.Transform.Position;
                        bool isSelected = go == EditorActions.SelectedObject;
                        float alpha = isSelected ? 1.0f : 0.45f;
                        float lw = isSelected ? 2.0f : 1.0f;

                        GizmoSphere.Draw(Matrix4.CreateScale(audioSource.MinDistance * 2f) * Matrix4.CreateTranslation(position),
                            view, projection, new Vector4(0f, 1f, 0f, alpha), lw);
                        GizmoSphere.Draw(Matrix4.CreateScale(audioSource.MaxDistance * 2f) * Matrix4.CreateTranslation(position),
                            view, projection, new Vector4(1f, 0f, 0f, alpha), lw);
                    }
                }
                else if (EditorActions.SelectedObject?.HasComponent<AudioSource>() == true)
                {
                    var audioSource = EditorActions.SelectedObject.GetComponent<AudioSource>();
                    var position = EditorActions.SelectedObject.Transform.Position;

                    GizmoSphere.Draw(Matrix4.CreateScale(audioSource.MinDistance * 2f) * Matrix4.CreateTranslation(position),
                        view, projection, new Vector4(0f, 1f, 0f, 1f), 2.0f);
                    GizmoSphere.Draw(Matrix4.CreateScale(audioSource.MaxDistance * 2f) * Matrix4.CreateTranslation(position),
                        view, projection, new Vector4(1f, 0f, 0f, 1f), 1.5f);
                }
            });

            sceneRenderer.AttachRender("gizmo_lights", (view, projection, cameraPos) =>
            {
                if (ShowAllGizmos)
                {
                    var allObjects = SceneManager.PrimaryScene?.GetAllGameObjects();
                    if (allObjects == null) return;
                    foreach (var go in allObjects)
                    {
                        if (!go.HasComponent<Light>()) continue;
                        DrawLightGizmo(go.GetComponent<Light>(), view, projection, go == EditorActions.SelectedObject);
                    }
                }
                else if (EditorActions.SelectedObject?.HasComponent<Light>() == true)
                    DrawLightGizmo(EditorActions.SelectedObject.GetComponent<Light>(), view, projection, isSelected: true);
            });

            sceneRenderer.AttachRender("gizmo_camera", (view, projection, cameraPos) =>
            {
                if (ShowAllGizmos)
                {
                    var allObjects = SceneManager.PrimaryScene?.GetAllGameObjects();
                    if (allObjects == null) return;
                    foreach (var go in allObjects)
                    {
                        if (!go.HasComponent<CameraComponent>()) continue;
                        DrawCameraGizmo(go, view, projection, go == EditorActions.SelectedObject);
                    }
                }
                else if (EditorActions.SelectedObject?.HasComponent<CameraComponent>() == true)
                    DrawCameraGizmo(EditorActions.SelectedObject, view, projection, isSelected: true);
            });

            sceneRenderer.AttachRender("gizmo_hover", (view, projection, cameraPos) =>
            {
                if (_hoveredObject == null || _hoveredObject == EditorActions.SelectedObject) return;
                GizmoCube.Draw(_hoveredObject.Transform.GetWorldMatrix(), view, projection,
                    new Vector4(1f, 1f, 1f, 0.4f), lineWidth: 1.5f);
            });
        }

        private static void HandleWindowTryClose()
        {
            if (EditorActions.IsDirty)
                EditorNotifications.Error("You cannot close the engine until you have saved your scene.");
        }

        private static void HandleLoad()
        {
            LogMessage("Kryon Engine Initialized");
            LogMessage("  WASD / Space / Shift - Move camera");
            LogMessage("  Right Mouse          - Look around");
            LogMessage("  Scroll               - Zoom");
            LogMessage("  Q / E / R            - Gizmo mode");
            LogMessage("  Ctrl+R               - Reset Camera");
            LogMessage("================================");

            _renderer = _engine!.GetSceneRenderer();
            _window = _engine.Window;
            _imguiController = new ImGuiController(WindowConfig.Width, WindowConfig.Height);

            _engine.OnTextInput += e => _imguiController?.PressChar((char)e.Unicode);
            _engine.Window.MouseMove += e => UIInputManager.SetMousePos(e.X, e.Y);
            _engine.Window.MouseDown += e => { if (e.Button == MouseButton.Left) UIInputManager.SetLeftButton(true); };
            _engine.Window.MouseUp += e => { if (e.Button == MouseButton.Left) UIInputManager.SetLeftButton(false); };

            EditorUI.Initialize();
            SetupCamera();
            AttachGizmos(_engine.GetSceneRenderer());
        }

        private static void HandleUpdate(float dt)
        {
            byte[] currentBytes = SceneManager.CloneSceneToBytes(SceneManager.PrimaryScene);
            EditorActions.IsDirty = !currentBytes.SequenceEqual(SceneManager.CurrentSceneData);

            if (EditorActions.IsDirty) GraphicsEngine.Instance.CantNoClose();
            else GraphicsEngine.Instance.CanClose();

            _editorCamera ??= GraphicsEngine.Instance.GetSceneRenderer().GetCamera();

            if (_window is null) return;

            HandleInput(dt);
            UpdateFPS(dt);
            UpdateHoveredObject();

            _imguiController?.Update(_window, dt);
            EditorNotifications.Draw(dt);
            EditorUI.Draw();

            var globalMouse = ImGui.GetMousePos();
            var vpOrigin = EditorActions.ViewPortPosition;
            var vpSize = new Vector2(
                GraphicsEngine.Instance.GetSceneFrameBuffer().Width,
                GraphicsEngine.Instance.GetSceneFrameBuffer().Height);

            UIInputManager.SetKeyboardState(GraphicsEngine.Instance.GetKeyboardState().GetSnapshot());
            UIInputManager.SetMousePosFromViewport(
                new OpenTK.Mathematics.Vector2(globalMouse.X, globalMouse.Y),
                new OpenTK.Mathematics.Vector2(vpOrigin.X, vpOrigin.Y),
                new OpenTK.Mathematics.Vector2(vpSize.X, vpSize.Y));
            UIInputManager.SetLeftButton(GraphicsEngine.Instance.GetMouseState().IsButtonDown(MouseButton.Left));
            UIInputManager.UpdateAll();
            UICanvasManager.Update(dt);
        }

        private static void UpdateHoveredObject()
        {
            if (!EditorActions.IsHoveringScene || _engine!.GetMouseState().IsButtonDown(MouseButton.Right))
            {
                _hoveredObject = null;
                return;
            }

            try
            {
                var camera = GraphicsEngine.Instance.GetSceneRenderer().GetCamera();
                int screenWidth = GraphicsEngine.Instance.GetSceneFrameBuffer().Width;
                int screenHeight = GraphicsEngine.Instance.GetSceneFrameBuffer().Height;
                var globalMouse = ImGui.GetMousePos();
                var sceneOrigin = EditorActions.ViewPortPosition;

                var relMouse = new OpenTK.Mathematics.Vector2(
                    globalMouse.X - sceneOrigin.X,
                    globalMouse.Y - sceneOrigin.Y);

                EventSystem.ScreenToWorldRay(relMouse, camera, screenWidth, screenHeight,
                    out var rayOrigin, out var rayDir);

                _hoveredObject = EventSystem.GetObjectByRay(rayOrigin, rayDir);
            }
            catch { _hoveredObject = null; }
        }

        private static void HandleRender(float dt) => _imguiController?.Render();
        private static void HandleResize(int w, int h) => _imguiController?.WindowResized(w, h);
        private static void HandleClose()
        {
            _imguiController?.Dispose();
            LogMessage("Engine closed");
        }

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

            if (keyboard.IsKeyPressed(Keys.Q)) { TransformGizmo.SetMode(GizmoMode.Translate); LogMessage("Switched to Move mode"); }
            if (keyboard.IsKeyPressed(Keys.E)) { TransformGizmo.SetMode(GizmoMode.Rotate); LogMessage("Switched to Rotate mode"); }
            if (keyboard.IsKeyPressed(Keys.R) && !keyboard.IsKeyDown(Keys.LeftControl))
            { TransformGizmo.SetMode(GizmoMode.Scale); LogMessage("Switched to Scale mode"); }
        }

        private static void HandleCameraReset(OpenTK.Windowing.GraphicsLibraryFramework.KeyboardState keyboard)
        {
            if (keyboard.IsKeyDown(Keys.LeftControl) && keyboard.IsKeyPressed(Keys.R))
            { ResetCamera(); LogMessage("Camera reset to initial position"); }
        }

        private static void HandleCameraMovement(
            OpenTK.Windowing.GraphicsLibraryFramework.KeyboardState keyboard,
            OpenTK.Windowing.GraphicsLibraryFramework.MouseState mouse,
            float dt)
        {
            if (!mouse.IsButtonDown(MouseButton.Right) || !EditorActions.IsHoveringScene) return;

            float step = dt * _editorCameraSpeed * (keyboard.IsKeyDown(Keys.LeftControl) ? 2f : 1f) / 2.5f;

            if (keyboard.IsKeyDown(Keys.W)) _editorCamera!.Move(CameraMovement.Forward, step);
            if (keyboard.IsKeyDown(Keys.S)) _editorCamera!.Move(CameraMovement.Backward, step);
            if (keyboard.IsKeyDown(Keys.A)) _editorCamera!.Move(CameraMovement.Left, step);
            if (keyboard.IsKeyDown(Keys.D)) _editorCamera!.Move(CameraMovement.Right, step);
            if (keyboard.IsKeyDown(Keys.Space)) _editorCamera!.Move(CameraMovement.Up, step);
            if (keyboard.IsKeyDown(Keys.LeftShift)) _editorCamera!.Move(CameraMovement.Down, step);
        }

        private static void HandleCameraRotation(OpenTK.Windowing.GraphicsLibraryFramework.MouseState mouse)
        {
            if (_firstMouse) { _lastX = mouse.X; _lastY = mouse.Y; _firstMouse = false; }

            float xOffset = mouse.X - _lastX;
            float yOffset = _lastY - mouse.Y;
            _lastX = mouse.X;
            _lastY = mouse.Y;

            var camera = GraphicsEngine.Instance?.GetSceneRenderer().GetCamera();
            if (mouse.IsButtonDown(MouseButton.Right) && camera?.IsPerspective == true && EditorActions.IsHoveringScene)
                _editorCamera!.Rotate(xOffset, yOffset);
        }

        private static void HandleCameraZoom(OpenTK.Windowing.GraphicsLibraryFramework.MouseState mouse)
        {
            if (mouse.ScrollDelta.Y != 0 && EditorActions.IsHoveringScene && !InputSystem.GetKeyDown(Keys.LeftControl))
                _editorCamera!.Zoom(mouse.ScrollDelta.Y);
        }

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

        private static void DrawRigidbodyGizmo(Rigidbody rb, GameObject go,
            Matrix4 view, Matrix4 projection, Vector4 color, float lineWidth)
        {
            var worldPos = go.Transform.GetWorldPosition();
            var worldRot = go.Transform.GetWorldRotation();
            var worldOffset = Vector3.Transform(rb.ColliderOffset, worldRot);
            var center = worldPos + worldOffset;
            var rotMat = Matrix4.CreateFromQuaternion(worldRot);
            var transMat = Matrix4.CreateTranslation(center);

            switch (rb.ShapeType)
            {
                case ShapeType.Box:
                    GizmoCube.Draw(Matrix4.CreateScale(rb.ShapeSize * 2f) * rotMat * transMat,
                        view, projection, color, lineWidth);
                    break;
                case ShapeType.Sphere:
                    GizmoSphere.Draw(Matrix4.CreateScale(rb.ShapeSize.X * 2f) * rotMat * transMat,
                        view, projection, color, lineWidth);
                    break;
                case ShapeType.Capsule:
                    float diameter = rb.ShapeSize.X * 2f;
                    float totalHeight = rb.ShapeSize.Y * 2f + diameter;
                    GizmoCapsule.Draw(Matrix4.CreateScale(diameter, totalHeight, diameter) * rotMat * transMat,
                        view, projection, color, lineWidth);
                    break;
            }
        }

        private static void DrawLightGizmo(Light light, Matrix4 view, Matrix4 projection, bool isSelected)
        {
            var position = light.GetPosition();
            var direction = light.GetDirection();
            float alpha = isSelected ? 1.0f : 0.50f;
            float lw = isSelected ? 2.5f : 1.2f;

            switch (light.Type)
            {
                case LightType.Point:
                    {
                        float radius = CalculateLightRadius(light.Intensity, light.Constant, light.Linear, light.Quadratic);
                        var color = new Vector4(1f, 1f, 0f, alpha);
                        GizmoCircle.Draw(Matrix4.CreateScale(radius * 2f) * Matrix4.CreateTranslation(position), view, projection, color, lw);
                        GizmoCircle.Draw(Matrix4.CreateRotationX(MathHelper.DegreesToRadians(90)) * Matrix4.CreateScale(radius * 2f) * Matrix4.CreateTranslation(position), view, projection, color, lw);
                        GizmoCircle.Draw(Matrix4.CreateRotationY(MathHelper.DegreesToRadians(90)) * Matrix4.CreateScale(radius * 2f) * Matrix4.CreateTranslation(position), view, projection, color, lw);
                        break;
                    }
                case LightType.Spot:
                    {
                        float coneLength = CalculateLightRadius(light.Intensity, light.Constant, light.Linear, light.Quadratic);
                        float coneRadius = MathF.Tan(MathHelper.DegreesToRadians(light.OuterCutOffDegrees)) * coneLength;
                        var color = new Vector4(1f, 0.5f, 0f, alpha);
                        var rotation = CreateLookAtRotation(Vector3.UnitZ, direction);

                        GizmoCone.Draw(Matrix4.CreateScale(coneRadius * 2f, coneRadius * 2f, coneLength) * rotation * Matrix4.CreateTranslation(position), view, projection, color, lw);
                        GizmoArrow.Draw(Matrix4.CreateScale(0.5f) * rotation * Matrix4.CreateTranslation(position), view, projection, new Vector4(1, 1, 1, alpha), lw);
                        break;
                    }
                case LightType.Directional:
                    {
                        var color = new Vector4(1f, 1f, 1f, alpha);
                        var rotDir = CreateLookAtRotation(Vector3.UnitZ, direction);
                        var modelDir = Matrix4.CreateScale(2f) * rotDir * Matrix4.CreateTranslation(position);

                        GizmoSphere.Draw(Matrix4.CreateScale(0.15f) * Matrix4.CreateTranslation(position), view, projection, color, lw);
                        GizmoArrow.Draw(modelDir, view, projection, color, lw);
                        break;
                    }
            }
        }

        private static void DrawCameraGizmo(GameObject go, Matrix4 view, Matrix4 projection, bool isSelected)
        {
            var cameraComp = go.GetComponent<CameraComponent>();
            var transform = go.GetComponent<Transform>();
            if (cameraComp?.RenderCamera == null || transform == null) return;

            float alpha = isSelected ? 1.0f : 0.45f;
            float lw = isSelected ? 2.5f : 1.2f;
            var color = new Vector4(0f, 1f, 0.5f, alpha);
            var position = transform.GetWorldPosition();
            var forward = transform.Forward;
            var up = transform.Up;

            if (cameraComp.ProjectionMode == ProjectionMode.Perspective)
                GizmoFrustum.DrawPerspective(position, forward, up, cameraComp.Fov, cameraComp.AspectRatio, cameraComp.NearPlane, cameraComp.FarPlane, view, projection, color, lw);
            else
                GizmoFrustum.DrawOrthographic(position, forward, up, cameraComp.OrthoSize, cameraComp.AspectRatio, cameraComp.NearPlane, cameraComp.FarPlane, view, projection, color, lw);
        }

        private static float CalculateLightRadius(float intensity, float constant, float linear, float quadratic)
        {
            float c = constant - (intensity / (5f / 256f));
            float discriminant = linear * linear - 4 * quadratic * c;
            if (discriminant < 0) return 10f;
            return MathF.Max((-linear + MathF.Sqrt(discriminant)) / (2 * quadratic), 1f);
        }

        private static Matrix4 CreateLookAtRotation(Vector3 from, Vector3 to)
        {
            var forward = Vector3.Normalize(to);
            var right = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, forward));
            if (right.LengthSquared < 0.001f)
                right = Vector3.Normalize(Vector3.Cross(Vector3.UnitX, forward));
            var up = Vector3.Cross(forward, right);

            return new Matrix4(
                new Vector4(right, 0),
                new Vector4(up, 0),
                new Vector4(forward, 0),
                new Vector4(0, 0, 0, 1));
        }

        private static void OnFilesDropped(string[] filePaths)
            => UIRender.GetUI<AssetsUI>().HandleExternalDrop(filePaths, "");

        public static void LogMessage(string message)
        {
            _consoleMessages.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
            if (_consoleMessages.Count > _maxConsoleMessages)
                _consoleMessages.RemoveAt(0);
        }

        public static GameObject? GetSelectedObject() => _selectedObject;
        public static void SetSelectedObject(GameObject? obj) => _selectedObject = obj;
    }
}