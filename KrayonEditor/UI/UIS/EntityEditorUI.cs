using ImGuiNET;
using KrayonCore;
using KrayonCore.Components;
using KrayonCore.Components.RenderComponents;
using KrayonCore.Core;
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
using System.Runtime.InteropServices;
using Vector2 = System.Numerics.Vector2;

namespace KrayonEditor.UI.UIS
{
    public class EntityEditorUI : UIBehaviour
    {
        public GameScene Scene;
        private Camera _editorCamera;
        private GameObject _selectedObject;

        private bool _firstMouse = true;
        private float _cameraSpeed = 2.5f;

        private GameObject _renamingObject = null;
        private string _renameBuffer = "";

        private string _entitySavePath = null;

        private const string DRAG_DROP_PAYLOAD = "ENTITY_GAMEOBJECT";
        bool isHovered;

        private bool _showSaveNameModal = false;
        private string _saveNameBuffer = "";
        private bool _showOverwriteModal = false;
        private bool _showOpenModal = false;
        private string[] _entityFiles = Array.Empty<string>();
        private int _selectedEntityFile = -1;
        private string _openSearchBuffer = "";
        public static bool ShowAllGizmos = true;
        private static GameObject? _hoveredObject = null;


        public void OpenEntity(Guid assetGuid)
        {
            var asset = AssetManager.Get(assetGuid);
            if (asset == null) return;

            string fullPath = System.IO.Path.Combine(AssetManager.BasePath, asset.Path);
            if (_entitySavePath == fullPath && Scene != null) return;

            LoadEntityFromPath(fullPath);
        }

        private void LoadEntityFromPath(string fullPath)
        {
            _entitySavePath = fullPath;

            if (Scene != null)
                SceneManager.UnregisterSceneOnly(Scene);

            Scene = SceneSaveSystem.LoadScene(fullPath)
                 ?? SceneManager.CreateScene(System.IO.Path.GetFileNameWithoutExtension(fullPath));

            SceneManager.RegisterSceneOnly(Scene);

            _selectedObject = null;
            _editorCamera = Scene.SelfRenderScene.GetCamera();
            _editorCamera.Position = new OpenTK.Mathematics.Vector3(0, 0, 5);

            AttachGizmos(Scene.SelfRenderScene);
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

        private static void AttachGizmos(SceneRenderer sceneRenderer)
        {
            sceneRenderer.AttachRender("gizmo_grid", (view, projection, cameraPos) =>
            {
                GizmoGrid.Draw(view, projection);
            });

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

        public override void OnDrawUI()
        {
            ImGui.Begin("Entity Editor", ImGuiWindowFlags.NoScrollbar);

            uint dockId = ImGui.GetID("EntityEditorDock");
            ImGui.DockSpace(dockId);

            DrawHierarchy();
            DrawDetails();
            DrawViewport();

            ImGui.End();
        }

        private void TrySave()
        {
            if (AppInfo.IsPlayingGame)
            {
                EditorNotifications.Success("You cannot save the scene during playback.");
                return;
            }

            if (_entitySavePath == null)
            {
                _saveNameBuffer = Scene?.Name ?? "New Entity";
                _showSaveNameModal = true;
                return;
            }

            if (System.IO.File.Exists(_entitySavePath))
            {
                _showOverwriteModal = true;
                return;
            }

            SceneSaveSystem.SaveScene(Scene, _entitySavePath);
        }

        private void OpenOpenEntityModal()
        {
            if (System.IO.Directory.Exists(AssetManager.BasePath))
            {
                _entityFiles = System.IO.Directory.GetFiles(AssetManager.BasePath, "*", System.IO.SearchOption.AllDirectories)
                    .Where(f => f.EndsWith(".entity", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(f => f)
                    .ToArray();
            }
            else
            {
                _entityFiles = Array.Empty<string>();
            }

            _selectedEntityFile = -1;
            _openSearchBuffer = "";
            _showOpenModal = true;
        }

        private void DrawModals()
        {
            Vector2 center = ImGui.GetMainViewport().GetCenter();

            if (_showSaveNameModal)
                ImGui.OpenPopup("###save_name_popup");

            if (_showOverwriteModal)
                ImGui.OpenPopup("###overwrite_popup");

            if (_showOpenModal)
                ImGui.OpenPopup("###open_entity_popup");

            ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
            ImGui.SetNextWindowSize(new Vector2(360, 0), ImGuiCond.Appearing);
            if (ImGui.BeginPopupModal("Guardar Entity###save_name_popup", ref _showSaveNameModal, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove))
            {
                ImGui.Spacing();
                ImGui.TextDisabled("Nombre del archivo");
                ImGui.SetNextItemWidth(-1);
                ImGui.InputText("##save_name", ref _saveNameBuffer, 256);
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                float btnW = (ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X) * 0.5f;

                if (ImGui.Button("Guardar", new Vector2(btnW, 28)))
                {
                    if (!string.IsNullOrWhiteSpace(_saveNameBuffer))
                    {
                        string dir = System.IO.Path.Combine(AssetManager.BasePath, "Entity");
                        System.IO.Directory.CreateDirectory(dir);
                        string path = System.IO.Path.Combine(dir, _saveNameBuffer.Trim() + ".entity");
                        _entitySavePath = path;
                        SceneSaveSystem.SaveScene(Scene, path);
                        _showSaveNameModal = false;
                        ImGui.CloseCurrentPopup();
                    }
                }
                ImGui.SameLine();
                if (ImGui.Button("Cancelar", new Vector2(btnW, 28)))
                {
                    _showSaveNameModal = false;
                    ImGui.CloseCurrentPopup();
                }
                ImGui.Spacing();
                ImGui.EndPopup();
            }

            ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
            ImGui.SetNextWindowSize(new Vector2(380, 0), ImGuiCond.Appearing);
            if (ImGui.BeginPopupModal("Sobreescribir archivo###overwrite_popup", ref _showOverwriteModal, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove))
            {
                ImGui.Spacing();
                ImGui.Text("El archivo ya existe:");
                ImGui.Spacing();
                ImGui.TextDisabled(_entitySavePath ?? "");
                ImGui.Spacing();
                ImGui.Text("Deseas sobreescribirlo?");
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                float btnW = (ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X) * 0.5f;

                if (ImGui.Button("Sobreescribir", new Vector2(btnW, 28)))
                {
                    SceneSaveSystem.SaveScene(Scene, _entitySavePath);
                    _showOverwriteModal = false;
                    ImGui.CloseCurrentPopup();
                }
                ImGui.SameLine();
                if (ImGui.Button("Cancelar", new Vector2(btnW, 28)))
                {
                    _showOverwriteModal = false;
                    ImGui.CloseCurrentPopup();
                }
                ImGui.Spacing();
                ImGui.EndPopup();
            }

            ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
            ImGui.SetNextWindowSize(new Vector2(480, 380), ImGuiCond.Appearing);
            if (ImGui.BeginPopupModal("Abrir Entity###open_entity_popup", ref _showOpenModal, ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize))
            {
                ImGui.Spacing();
                ImGui.TextDisabled($"Buscando en: {AssetManager.BasePath}  ({_entityFiles.Length} archivo(s))");
                ImGui.Spacing();
                ImGui.SetNextItemWidth(-1);
                ImGui.InputTextWithHint("##open_search", "Buscar...", ref _openSearchBuffer, 256);
                ImGui.Spacing();
                ImGui.Separator();

                ImGui.BeginChild("##entity_file_list", new Vector2(-1, 230));

                for (int i = 0; i < _entityFiles.Length; i++)
                {
                    string file = _entityFiles[i];
                    string display = System.IO.Path.GetFileNameWithoutExtension(file);
                    string rel = file.Replace(AssetManager.BasePath, "").TrimStart('/', '\\');

                    if (!string.IsNullOrEmpty(_openSearchBuffer) &&
                        !display.Contains(_openSearchBuffer, StringComparison.OrdinalIgnoreCase))
                        continue;

                    bool sel = _selectedEntityFile == i;
                    ImGui.PushID(i);
                    if (ImGui.Selectable($"  {display}", sel, ImGuiSelectableFlags.SpanAllColumns))
                        _selectedEntityFile = i;
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip(rel);
                    if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                    {
                        LoadEntityFromPath(file);
                        _showOpenModal = false;
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.PopID();
                }

                if (_entityFiles.Length == 0)
                {
                    ImGui.Spacing();
                    float cw = ImGui.GetContentRegionAvail().X;
                    string none = "No se encontraron archivos .entity";
                    ImGui.SetCursorPosX((cw - ImGui.CalcTextSize(none).X) * 0.5f);
                    ImGui.TextDisabled(none);
                }

                ImGui.EndChild();

                ImGui.Separator();
                ImGui.Spacing();

                float btnW2 = (ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X) * 0.5f;
                bool canOpen = _selectedEntityFile >= 0 && _selectedEntityFile < _entityFiles.Length;

                if (!canOpen) ImGui.BeginDisabled();
                if (ImGui.Button("Abrir", new Vector2(btnW2, 28)) && canOpen)
                {
                    LoadEntityFromPath(_entityFiles[_selectedEntityFile]);
                    _showOpenModal = false;
                    ImGui.CloseCurrentPopup();
                }
                if (!canOpen) ImGui.EndDisabled();

                ImGui.SameLine();
                if (ImGui.Button("Cancelar", new Vector2(btnW2, 28)))
                {
                    _showOpenModal = false;
                    ImGui.CloseCurrentPopup();
                }
                ImGui.Spacing();
                ImGui.EndPopup();
            }
        }

        private void DrawHierarchy()
        {
            ImGui.SetNextWindowDockID(ImGui.GetID("EntityEditorDock"), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSize(new Vector2(260, 500), ImGuiCond.FirstUseEver);
            ImGui.Begin("Hierarchy##entity_hierarchy");

            DrawToolbar();

            DrawModals();

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            if (Scene != null)
            {
                if (_selectedObject != null && ImGui.IsWindowFocused() && ImGui.IsKeyPressed(ImGuiKey.Delete))
                {
                    Scene.DestroyGameObject(_selectedObject);
                    _selectedObject = null;
                }

                ImGui.PushStyleColor(ImGuiCol.Text, (System.Numerics.Vector4)new Vector4(0.85f, 0.85f, 0.85f, 1f));
                var sceneFlags = ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.SpanAvailWidth | ImGuiTreeNodeFlags.FramePadding;
                bool sceneOpen = ImGui.TreeNodeEx($"  {Scene.Name}##scene_root", sceneFlags);
                ImGui.PopStyleColor();

                if (ImGui.BeginPopupContextWindow("hierarchy_ctx",
                    ImGuiPopupFlags.MouseButtonRight | ImGuiPopupFlags.NoOpenOverItems))
                {
                    if (ImGui.MenuItem("  Nuevo objeto vacio"))
                    {
                        var newObj = Scene.CreateGameObject();
                        newObj.Name = "New Object";
                        _selectedObject = newObj;
                    }
                    if (ImGui.MenuItem("  Nuevo objeto con MeshRenderer"))
                    {
                        var newObj = Scene.CreateGameObject();
                        newObj.Name = "New Mesh Object";
                        newObj.AddComponent<MeshRenderer>().Start();
                        _selectedObject = newObj;
                    }
                    ImGui.EndPopup();
                }

                if (ImGui.BeginDragDropTarget())
                {
                    var payload = ImGui.AcceptDragDropPayload(DRAG_DROP_PAYLOAD);
                    unsafe
                    {
                        if (payload.NativePtr != null)
                        {
                            Guid draggedId = Marshal.PtrToStructure<Guid>((IntPtr)payload.Data);
                            var draggedObject = FindObjectInScene(draggedId);
                            if (draggedObject != null)
                                draggedObject.Transform.SetParent(null);
                        }
                    }
                    ImGui.EndDragDropTarget();
                }

                if (sceneOpen)
                {
                    foreach (var go in Scene.GetAllGameObjects())
                    {
                        if (go.Transform.Parent == null)
                            DrawGameObjectNode(go);
                    }
                    ImGui.TreePop();
                }
            }
            else
            {
                ImGui.Spacing();
                float w = ImGui.GetContentRegionAvail().X;
                string msg = "Sin escena abierta";
                float tw = ImGui.CalcTextSize(msg).X;
                ImGui.SetCursorPosX((w - tw) * 0.5f);
                ImGui.TextDisabled(msg);
            }

            ImGui.End();
        }

        private void DrawToolbar()
        {
            float availW = ImGui.GetContentRegionAvail().X;
            float spacing = ImGui.GetStyle().ItemSpacing.X;
            float btnW = (availW - spacing * 3) / 4f;
            float btnH = 24f;

            if (ImGui.Button("Nuevo", new Vector2(btnW, btnH)))
            {
                if (!AppInfo.IsPlayingGame)
                {
                    if (Scene != null)
                        SceneManager.UnregisterSceneOnly(Scene);

                    Scene = SceneManager.CreateScene("Entity Scene");
                    _entitySavePath = null;
                    SceneManager.RegisterSceneOnly(Scene);

                    AttachGizmos(Scene.SelfRenderScene);

                    _selectedObject = null;
                    _editorCamera = Scene.SelfRenderScene.GetCamera();
                    _editorCamera.Position = new OpenTK.Mathematics.Vector3(0, 0, 5);
                }
                else
                {
                    EditorNotifications.Success("You cannot create a scene during playback.");
                }
            }

            ImGui.SameLine();
            if (ImGui.Button("Abrir", new Vector2(btnW, btnH)))
                OpenOpenEntityModal();

            ImGui.SameLine();
            if (ImGui.Button("Guardar", new Vector2(btnW, btnH)))
                TrySave();

            ImGui.SameLine();
            if (ImGui.Button("Guardar Como", new Vector2(btnW, btnH)))
            {
                if (!AppInfo.IsPlayingGame)
                {
                    _saveNameBuffer = Scene?.Name ?? "New Entity";
                    _showSaveNameModal = true;
                }
                else
                {
                    EditorNotifications.Success("You cannot save the scene during playback.");
                }
            }

            if (ImGui.IsKeyDown(ImGuiKey.LeftCtrl) && ImGui.IsKeyReleased(ImGuiKey.S)
                && !ImGui.IsKeyDown(ImGuiKey.MouseRight) && isHovered)
            {
                TrySave();
            }

            ImGui.Spacing();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 2);
            ImGui.TextDisabled(_entitySavePath != null
                ? System.IO.Path.GetFileName(_entitySavePath)
                : "Sin guardar");
        }

        private void DrawGameObjectNode(GameObject go)
        {
            bool isSelected = _selectedObject == go;
            bool hasChildren = go.Transform.Children.Count > 0;

            var flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.SpanAvailWidth | ImGuiTreeNodeFlags.FramePadding;
            if (isSelected) flags |= ImGuiTreeNodeFlags.Selected;
            if (!hasChildren) flags |= ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen;

            if (_renamingObject == go)
            {
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputText($"##rename_{go.Id}", ref _renameBuffer, 128, ImGuiInputTextFlags.EnterReturnsTrue))
                {
                    go.Name = _renameBuffer;
                    _renamingObject = null;
                }
                if (!ImGui.IsItemActive() && !ImGui.IsItemFocused())
                    _renamingObject = null;
                return;
            }

            bool nodeOpen = ImGui.TreeNodeEx($"  {go.Name}##{go.Id.GetHashCode()}", flags);

            if (ImGui.IsItemClicked())
                _selectedObject = go;

            if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
            {
                _renamingObject = go;
                _renameBuffer = go.Name;
            }

            if (ImGui.BeginPopupContextItem($"ctx_{go.Id}"))
            {
                if (ImGui.MenuItem("  Nuevo hijo vacio"))
                {
                    var child = Scene.CreateGameObject();
                    child.Name = "New Object";
                    child.Transform.SetParent(go.Transform);
                    _selectedObject = child;
                }
                if (ImGui.MenuItem("  Nuevo hijo con MeshRenderer"))
                {
                    var child = Scene.CreateGameObject();
                    child.Name = "New Mesh Object";
                    child.AddComponent<MeshRenderer>().Start();
                    child.Transform.SetParent(go.Transform);
                    _selectedObject = child;
                }
                ImGui.Separator();
                ImGui.PushStyleColor(ImGuiCol.Text, (System.Numerics.Vector4)new Vector4(0.9f, 0.35f, 0.35f, 1f));
                if (ImGui.MenuItem("  Eliminar"))
                {
                    Scene.DestroyGameObject(go);
                    if (_selectedObject == go) _selectedObject = null;
                }
                ImGui.PopStyleColor();
                ImGui.EndPopup();
            }

            if (ImGui.BeginDragDropSource(ImGuiDragDropFlags.None))
            {
                unsafe
                {
                    Guid id = go.Id;
                    IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf<Guid>());
                    Marshal.StructureToPtr(id, ptr, false);
                    ImGui.SetDragDropPayload(DRAG_DROP_PAYLOAD, ptr, (uint)Marshal.SizeOf<Guid>());
                    Marshal.FreeHGlobal(ptr);
                }
                ImGui.Text($"Moviendo: {go.Name}");
                ImGui.EndDragDropSource();
            }

            if (ImGui.BeginDragDropTarget())
            {
                var payload = ImGui.AcceptDragDropPayload(DRAG_DROP_PAYLOAD);
                unsafe
                {
                    if (payload.NativePtr != null)
                    {
                        Guid draggedId = Marshal.PtrToStructure<Guid>((IntPtr)payload.Data);
                        var draggedObject = FindObjectInScene(draggedId);
                        if (draggedObject != null && draggedObject != go)
                        {
                            if (!IsDescendantOf(go, draggedObject))
                                draggedObject.Transform.SetParent(go.Transform);
                        }
                    }
                }
                ImGui.EndDragDropTarget();
            }

            if (hasChildren && nodeOpen)
            {
                foreach (var child in go.Transform.Children)
                    DrawGameObjectNode(child.GameObject);
                ImGui.TreePop();
            }
        }

        private GameObject FindObjectInScene(Guid id)
        {
            if (Scene == null) return null;
            foreach (var obj in Scene.GetAllGameObjects())
                if (obj.Id == id) return obj;
            return null;
        }

        private bool IsDescendantOf(GameObject go, GameObject potentialAncestor)
        {
            Transform current = go.Transform.Parent;
            while (current != null)
            {
                if (current.GameObject == potentialAncestor) return true;
                current = current.Parent;
            }
            return false;
        }

        private void DrawDetails()
        {
            ImGui.SetNextWindowDockID(ImGui.GetID("EntityEditorDock"), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSize(new Vector2(300, 500), ImGuiCond.FirstUseEver);
            ImGui.Begin("Inspector##entity_inspector");

            if (_selectedObject == null)
            {
                ImGui.Spacing();
                float w = ImGui.GetContentRegionAvail().X;
                string msg = "Selecciona un objeto";
                float tw = ImGui.CalcTextSize(msg).X;
                ImGui.SetCursorPosX((w - tw) * 0.5f);
                ImGui.TextDisabled(msg);
                ComponentInspector.DrawAssetPickerModal();
                ImGui.End();
                return;
            }

            ImGui.Spacing();

            float avail = ImGui.GetContentRegionAvail().X;
            float tagW = 100f;
            float nameW = avail - tagW - ImGui.GetStyle().ItemSpacing.X;

            string name = _selectedObject.Name;
            ImGui.SetNextItemWidth(nameW);
            if (ImGui.InputText("##obj_name", ref name, 128))
                _selectedObject.Name = name;

            ImGui.SameLine();
            string tag = _selectedObject.Tag;
            ImGui.SetNextItemWidth(tagW);
            if (ImGui.InputText("##obj_tag", ref tag, 64))
                _selectedObject.Tag = tag;

            ImGui.Spacing();

            bool active = _selectedObject.Active;
            if (ImGui.Checkbox("Activo", ref active))
                _selectedObject.Active = active;

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ComponentInspector.DrawTransform(_selectedObject.Transform);

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            var components = _selectedObject.GetAllComponents().ToList();
            int idx = 0;
            foreach (var component in components)
            {
                if (component is KrayonCore.Components.Transform) { idx++; continue; }
                ImGui.PushID($"comp_{idx}");
                ComponentInspector.DrawComponentWithReflection(component, _selectedObject);
                ImGui.PopID();
                idx++;
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ComponentInspector.DrawAddComponentButton(_selectedObject);
            ComponentInspector.DrawAssetPickerModal();

            ImGui.End();
        }

        private void DrawViewport()
        {
            ImGui.SetNextWindowDockID(ImGui.GetID("EntityEditorDock"), ImGuiCond.FirstUseEver);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
            ImGui.Begin("Viewport##entity_viewport");
            ImGui.PopStyleVar();

            if (Scene != null && _editorCamera != null)
            {
                var viewportSize = ImGui.GetContentRegionAvail();
                var fb = Scene.SelfRenderScene.Buffers.Get("scene");

                if (fb.Width != (int)viewportSize.X || fb.Height != (int)viewportSize.Y)
                {
                    Scene.SelfRenderScene.Resize((int)viewportSize.X, (int)viewportSize.Y);
                    _editorCamera.AspectRatio = viewportSize.X / viewportSize.Y;
                }

                Vector2 cursorPos = ImGui.GetCursorScreenPos();
                ImGui.Image(fb.ColorTexture, viewportSize, new Vector2(0, 1), new Vector2(1, 0));
                isHovered = ImGui.IsItemHovered();

                if (isHovered)
                {
                    HandleCameraInput();

                    if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !TransformGizmo.IsHovering)
                    {
                        Vector2 relMouse = ImGui.GetMousePos() - cursorPos;
                        bool inBounds = relMouse.X >= 0 && relMouse.X <= viewportSize.X &&
                                        relMouse.Y >= 0 && relMouse.Y <= viewportSize.Y;

                        if (inBounds)
                        {
                            var tkMouse = new OpenTK.Mathematics.Vector2(relMouse.X, relMouse.Y);
                            EventSystem.ScreenToWorldRay(tkMouse, _editorCamera,
                                (int)viewportSize.X, (int)viewportSize.Y,
                                out OpenTK.Mathematics.Vector3 rayOrigin,
                                out OpenTK.Mathematics.Vector3 rayDir);

                            var hit = EventSystem.GetObjectByRay(rayOrigin, rayDir, Scene);
                            _selectedObject = hit == _selectedObject ? null : hit;
                        }
                    }
                }

                DrawGizmo(cursorPos, viewportSize, isHovered);
                DrawViewportOverlay(cursorPos);
            }
            else
            {
                Vector2 avail = ImGui.GetContentRegionAvail();
                string msg = "Abre o crea una entity para comenzar";
                Vector2 ts = ImGui.CalcTextSize(msg);
                ImGui.SetCursorPos((avail - ts) * 0.5f);
                ImGui.TextDisabled(msg);
            }

            ImGui.End();
        }

        private void DrawViewportOverlay(Vector2 cursorPos)
        {
            if (Scene == null) return;

            var drawList = ImGui.GetWindowDrawList();
            string label = _entitySavePath != null
                ? System.IO.Path.GetFileNameWithoutExtension(_entitySavePath)
                : (Scene.Name + "  *");

            Vector2 textPos = cursorPos + new Vector2(10, 8);
            uint shadow = ImGui.ColorConvertFloat4ToU32((System.Numerics.Vector4)new Vector4(0, 0, 0, 0.55f));
            uint fg = ImGui.ColorConvertFloat4ToU32((System.Numerics.Vector4)new Vector4(0.9f, 0.9f, 0.9f, 0.8f));
            drawList.AddText(textPos + new Vector2(1, 1), shadow, label);
            drawList.AddText(textPos, fg, label);
        }

        private void HandleCameraInput()
        {
            if (_editorCamera is null) return;

            var io = ImGui.GetIO();
            float dt = io.DeltaTime;

            if (ImGui.IsMouseDown(ImGuiMouseButton.Right))
            {
                Vector2 delta = io.MouseDelta;
                if (!_firstMouse && (delta.X != 0 || delta.Y != 0))
                    _editorCamera.Rotate(delta.X, -delta.Y);
                _firstMouse = false;

                float speed = _cameraSpeed * dt * (io.KeyCtrl ? 2.0f : 1.0f);

                if (ImGui.IsKeyDown(ImGuiKey.W)) _editorCamera.Move(CameraMovement.Forward, speed);
                if (ImGui.IsKeyDown(ImGuiKey.S)) _editorCamera.Move(CameraMovement.Backward, speed);
                if (ImGui.IsKeyDown(ImGuiKey.A)) _editorCamera.Move(CameraMovement.Left, speed);
                if (ImGui.IsKeyDown(ImGuiKey.D)) _editorCamera.Move(CameraMovement.Right, speed);
                if (ImGui.IsKeyDown(ImGuiKey.Space)) _editorCamera.Move(CameraMovement.Up, speed);
                if (ImGui.IsKeyDown(ImGuiKey.LeftShift)) _editorCamera.Move(CameraMovement.Down, speed);
            }
            else
            {
                _firstMouse = true;
            }

            if (io.MouseWheel != 0)
                _editorCamera.Zoom(io.MouseWheel);
        }

        private void DrawGizmo(Vector2 cursorPos, Vector2 viewportSize, bool isHovered)
        {
            if (_selectedObject == null || _editorCamera == null || !isHovered) return;

            var transform = new GizmoTransform(
                _selectedObject.Transform.GetWorldPosition(),
                _selectedObject.Transform.GetWorldRotation(),
                _selectedObject.Transform.GetWorldScale());

            bool modified = TransformGizmo.Draw(
                ref transform,
                _editorCamera.GetViewMatrix(),
                _editorCamera.GetProjectionMatrix(),
                cursorPos,
                viewportSize,
                isHovered);

            if (modified)
            {
                _selectedObject.Transform.SetWorldPosition(transform.Position);
                _selectedObject.Transform.SetWorldRotation(transform.Rotation);
                _selectedObject.Transform.SetWorldScale(transform.Scale);
            }
        }
    }
}