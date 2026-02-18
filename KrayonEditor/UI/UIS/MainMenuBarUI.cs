using ImGuiNET;
using KrayonCore;
using KrayonCore.Core.Attributes;
using KrayonCore.GraphicsData;
using KrayonEditor.Main;
using System;
using System.IO;
using System.Linq;

namespace KrayonEditor.UI
{
    public class MainMenuBarUI : UIBehaviour
    {
        public bool ShowHierarchy { get; set; } = true;
        public bool ShowInspector { get; set; } = true;
        public bool ShowConsole { get; set; } = true;
        public bool ShowStats { get; set; } = true;
        public bool ShowAssets { get; set; } = true;

        private bool _showSaveDialog = false;
        private bool _showOpenDialog = false;
        private bool _showNewSceneDialog = false;
        private bool _showOverwriteConfirmDialog = false;
        private bool _showQuickSaveConfirmDialog = false;  
        private string _sceneNameInput = "";
        private string _sceneNameToOverwrite = "";
        private string[] _availableScenes = new string[0];
        private int _selectedSceneIndex = 0;

        private string SCENES_DIRECTORY
        {
            get
            {
                return AssetManager.BasePath + "scenes";
            }
        }

        private const string PREF_LAST_SCENE = "LastOpenedScene";
        private const int MAX_RECENT_SCENES = 10;

        public override void OnDrawUI()
        {
            if (ImGui.BeginMainMenuBar())
            {
                if (ImGui.BeginMenu("File"))
                {
                    if (ImGui.MenuItem("New Scene", "Ctrl+N"))
                    {
                        ShowNewSceneDialog();
                    }

                    if (ImGui.MenuItem("Open Scene", "Ctrl+O"))
                    {
                        OpenSceneDialog();
                    }

                    if (ImGui.MenuItem("Save Scene", "Ctrl+S"))
                    {
                        SaveCurrentScene();
                    }

                    if (ImGui.MenuItem("Save Scene As...", "Ctrl+Shift+S"))
                    {
                        SaveSceneAsDialog();
                    }

                    ImGui.Separator();

                    if (ImGui.BeginMenu("Recent Scenes"))
                    {
                        ShowRecentScenes();
                        ImGui.EndMenu();
                    }

                    ImGui.Separator();

                    if (ImGui.MenuItem("Exit", "Alt+F4"))
                    {
                        OnExit();
                    }

                    ImGui.EndMenu();
                }

                if (ImGui.BeginMenu("Edit"))
                {
                    if (ImGui.MenuItem("Undo", "Ctrl+Z")) { EngineEditor.LogMessage("Undo"); }
                    if (ImGui.MenuItem("Redo", "Ctrl+Y")) { EngineEditor.LogMessage("Redo"); }
                    ImGui.Separator();
                    if (ImGui.MenuItem("Preferences")) { EngineEditor.LogMessage("Open Preferences"); }
                    ImGui.EndMenu();
                }

                if (ImGui.BeginMenu("GameObject"))
                {
                    if (ImGui.MenuItem("Create Empty")) { EditorActions.CreateEmptyGameObject(); }
                    if (ImGui.MenuItem("Create Cube")) { EditorActions.CreateCubeGameObject(); }
                    if (ImGui.MenuItem("Setup All Materials From Scene")) { EditorActions.SetupAllMaterials(); }
                    ImGui.Separator();
                    if (ImGui.MenuItem("Delete Selected", "Del")) { EditorActions.DeleteSelectedObject(); }
                    ImGui.EndMenu();
                }

                if (ImGui.BeginMenu("Window"))
                {
                    if (ImGui.MenuItem("Hierarchy", null, EditorUI._hierarchy.IsVisible))
                    {
                        EditorUI._hierarchy.IsVisible = !EditorUI._hierarchy.IsVisible;
                    }

                    if (ImGui.MenuItem("Inspector", null, EditorUI._inspector.IsVisible))
                    {
                        EditorUI._inspector.IsVisible = !EditorUI._inspector.IsVisible;
                    }

                    if (ImGui.MenuItem("Scene View", null, EditorUI._sceneView.IsVisible))
                    {
                        EditorUI._sceneView.IsVisible = !EditorUI._sceneView.IsVisible;
                    }

                    if (ImGui.MenuItem("Console", null, EditorUI._console.IsVisible))
                    {
                        EditorUI._console.IsVisible = !EditorUI._console.IsVisible;
                    }

                    if (ImGui.MenuItem("Stats", null, EditorUI._stats.IsVisible))
                    {
                        EditorUI._stats.IsVisible = !EditorUI._stats.IsVisible;
                    }

                    if (ImGui.MenuItem("Assets", null, EditorUI._assets.IsVisible))
                    {
                        EditorUI._assets.IsVisible = !EditorUI._assets.IsVisible;
                    }

                    if (ImGui.MenuItem("Materials", null, EditorUI._materials.IsVisible))
                    {
                        EditorUI._materials.IsVisible = !EditorUI._materials.IsVisible;
                    }

                    if (ImGui.MenuItem("Tile Editor", null, EditorUI._TileEditor.IsVisible))
                    {
                        EditorUI._TileEditor.IsVisible = !EditorUI._TileEditor.IsVisible;
                    }

                    if (ImGui.MenuItem("Sprite Animator", null, EditorUI._SpriteAnimator.IsVisible))
                    {
                        EditorUI._SpriteAnimator.IsVisible = !EditorUI._SpriteAnimator.IsVisible;
                    }

                    if (ImGui.MenuItem("Compiler", null, EditorUI._CompilerUI.IsVisible))
                    {
                        EditorUI._CompilerUI.IsVisible = !EditorUI._CompilerUI.IsVisible;
                    }

                    ImGui.EndMenu();
                }

                if (SceneManager.ActiveScene != null)
                {
                    float offset = ImGui.GetWindowWidth() - 250;
                    if (offset > 0)
                    {
                        ImGui.SetCursorPosX(offset);
                        ImGui.Text($"Scene: {SceneManager.ActiveScene.Name}");
                    }
                }

                ImGui.EndMainMenuBar();
            }

            DrawNewSceneDialog();
            DrawSaveDialog();
            DrawOpenDialog();
            DrawOverwriteConfirmDialog();
            DrawQuickSaveConfirmDialog();  // NUEVO: Diálogo para Ctrl+S
            HandleKeyboardShortcuts();

            if (ImGui.IsKeyDown(ImGuiKey.LeftCtrl) && ImGui.IsKeyPressed(ImGuiKey.S) && !ImGui.IsKeyDown(ImGuiKey.MouseRight))
            {
                SaveCurrentScene();
            }
        }

        #region Scene Management

        private void ShowNewSceneDialog()
        {
            _sceneNameInput = $"EmptyScene";
            _showNewSceneDialog = true;
        }

        private void CreateNewScene(string sceneName)
        {
            try
            {
                var scene = SceneManager.CreateScene(sceneName);
                SceneManager.LoadScene(sceneName);

                var cameraObj = scene.CreateGameObject("Main Camera");
                cameraObj.Tag = "MainCamera";

                EngineEditor.LogMessage($"Nueva escena creada: {sceneName}");

                EditorPrefs.SetString(PREF_LAST_SCENE, sceneName);
                EditorPrefs.Save();
            }
            catch (Exception ex)
            {
                EngineEditor.LogMessage($"Error al crear nueva escena: {ex.Message}");
            }
        }

        // MODIFICADO: Ahora verifica si el archivo existe antes de guardar
        private void SaveCurrentScene()
        {
            try
            {
                if (SceneManager.ActiveScene == null)
                {
                    EngineEditor.LogMessage("No hay escena activa para guardar");
                    return;
                }

                string sceneName = SceneManager.ActiveScene.Name;
                string sceneFilePath = GetSceneFilePath(sceneName);

                // Verificar si el archivo ya existe
                if (File.Exists(sceneFilePath))
                {
                    // Mostrar diálogo de confirmación
                    _sceneNameToOverwrite = sceneName;
                    _showQuickSaveConfirmDialog = true;
                }
                else
                {
                    // Es un archivo nuevo, guardar directamente
                    PerformSaveScene(sceneName);
                }
            }
            catch (Exception ex)
            {
                EngineEditor.LogMessage($"Error al guardar escena: {ex.Message}");
            }
        }

        private void SaveSceneAsDialog()
        {
            if (SceneManager.ActiveScene == null)
            {
                EngineEditor.LogMessage("No hay escena activa para guardar");
                return;
            }

            _sceneNameInput = SceneManager.ActiveScene.Name;
            _showSaveDialog = true;
        }

        private void OpenSceneDialog()
        {
            RefreshAvailableScenes();
            _showOpenDialog = true;
        }

        private void ShowRecentScenes()
        {
            RefreshAvailableScenes();

            if (_availableScenes.Length == 0)
            {
                ImGui.MenuItem("No hay escenas guardadas", null, false, false);
                return;
            }

            int count = Math.Min(_availableScenes.Length, MAX_RECENT_SCENES);
            for (int i = 0; i < count; i++)
            {
                string sceneName = _availableScenes[i];
                if (ImGui.MenuItem(sceneName))
                {
                    LoadScene(sceneName);
                    EditorActions.SelectedObject = null;
                }
            }
        }

        private void LoadScene(string sceneName)
        {
            try
            {
                string sceneFilePath = GetSceneFilePath(sceneName);

                if (!File.Exists(sceneFilePath))
                {
                    EngineEditor.LogMessage($"No se encontró el archivo de escena: {sceneFilePath}");
                    return;
                }

                Console.WriteLine($"Cargando la escena: {sceneFilePath}");

                SceneManager.LoadScene(sceneFilePath);
                EngineEditor.LogMessage($"Escena cargada: {sceneName}");

                EditorPrefs.SetString(PREF_LAST_SCENE, sceneName);
                EditorPrefs.Save();
            }
            catch (Exception ex)
            {
                EngineEditor.LogMessage($"Error al cargar escena '{sceneName}': {ex.Message}");
            }
        }

        public void LoadLastScene()
        {
            string lastScene = EditorPrefs.GetString(PREF_LAST_SCENE, "");

            if (!string.IsNullOrEmpty(lastScene))
            {
                string filePath = GetSceneFilePath(lastScene);
                if (File.Exists(filePath))
                {
                    LoadScene(lastScene);
                    return;
                }
            }

            CreateNewScene("DefaultScene");
        }

        private void OnExit()
        {
            SaveAllMaterials();
            EditorPrefs.AutoSave();
            EngineEditor.LogMessage("Exit requested");
        }

        // Ejecuta el guardado después de la confirmación
        private void PerformSaveScene(string sceneName)
        {
            try
            {
                string filePath = GetSceneFilePath(sceneName);

                if (SceneManager.ActiveScene != null)
                {
                    SceneManager.ActiveScene.Name = sceneName;
                }

                SceneManager.SaveActiveScene(filePath);
                SaveAllMaterials();
                GraphicsEngine.Instance._fullscreenQuad.GetSettings().Save(AssetManager.VFXPath);

                EngineEditor.LogMessage($"Escena guardada como: {sceneName}");

                EditorPrefs.SetString(PREF_LAST_SCENE, sceneName);
                EditorPrefs.Save();
            }
            catch (Exception ex)
            {
                EngineEditor.LogMessage($"Error al guardar: {ex.Message}");
            }
        }

        #endregion

        #region Materials Management

        private void SaveAllMaterials()
        {
            try
            {
                var renderer = GraphicsEngine.Instance?.GetSceneRenderer();
                if (renderer == null)
                {
                    EngineEditor.LogMessage("Warning: No se pudo obtener el SceneRenderer para guardar materiales");
                    return;
                }

                GraphicsEngine.Instance.Materials.SaveMaterialsData();
            }
            catch (Exception ex)
            {
                EngineEditor.LogMessage($"Error al guardar materiales: {ex.Message}");
            }
        }

        #endregion

        #region Dialogs

        private void DrawNewSceneDialog()
        {
            if (!_showNewSceneDialog)
                return;

            ImGui.OpenPopup("Nueva Escena");

            if (ImGui.BeginPopupModal("Nueva Escena", ref _showNewSceneDialog, ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.Text("Nombre de la nueva escena:");
                ImGui.SetNextItemWidth(300);
                ImGui.InputText("##newscenename", ref _sceneNameInput, 100);

                if (SceneManager.ActiveScene != null)
                {
                    ImGui.Spacing();
                    ImGui.TextColored(new System.Numerics.Vector4(1, 0.8f, 0, 1),
                        "¡Advertencia! La escena actual se cerrará.");
                }

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                if (ImGui.Button("Crear", new System.Numerics.Vector2(145, 0)))
                {
                    if (!string.IsNullOrWhiteSpace(_sceneNameInput))
                    {
                        CreateNewScene(_sceneNameInput);
                        _showNewSceneDialog = false;
                        _sceneNameInput = "";
                    }
                    else
                    {
                        EngineEditor.LogMessage("Por favor ingresa un nombre válido");
                    }
                }

                ImGui.SameLine();

                if (ImGui.Button("Cancelar", new System.Numerics.Vector2(145, 0)))
                {
                    _showNewSceneDialog = false;
                    _sceneNameInput = "";
                }

                ImGui.EndPopup();
            }
        }

        private void DrawSaveDialog()
        {
            if (!_showSaveDialog)
                return;

            ImGui.OpenPopup("Guardar Escena Como");

            if (ImGui.BeginPopupModal("Guardar Escena Como", ref _showSaveDialog, ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.Text("Nombre de la escena:");
                ImGui.SetNextItemWidth(300);
                ImGui.InputText("##scenename", ref _sceneNameInput, 100);

                string filePath = GetSceneFilePath(_sceneNameInput);
                bool fileExists = File.Exists(filePath) && !string.IsNullOrWhiteSpace(_sceneNameInput);

                if (fileExists)
                {
                    ImGui.Spacing();
                    ImGui.TextColored(new System.Numerics.Vector4(1, 0.8f, 0, 1),
                        "⚠ Ya existe una escena con este nombre");
                }

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                if (ImGui.Button("Guardar", new System.Numerics.Vector2(145, 0)))
                {
                    if (!string.IsNullOrWhiteSpace(_sceneNameInput))
                    {
                        // Si el archivo existe, mostrar confirmación
                        if (fileExists)
                        {
                            _sceneNameToOverwrite = _sceneNameInput;
                            _showOverwriteConfirmDialog = true;
                        }
                        else
                        {
                            // Archivo nuevo, guardar directamente
                            PerformSaveScene(_sceneNameInput);
                            _showSaveDialog = false;
                            _sceneNameInput = "";
                        }
                    }
                    else
                    {
                        EngineEditor.LogMessage("Por favor ingresa un nombre válido");
                    }
                }

                ImGui.SameLine();

                if (ImGui.Button("Cancelar", new System.Numerics.Vector2(145, 0)))
                {
                    _showSaveDialog = false;
                    _sceneNameInput = "";
                }

                ImGui.EndPopup();
            }
        }

        // Confirmación de sobrescritura para "Save Scene As..."
        private void DrawOverwriteConfirmDialog()
        {
            if (!_showOverwriteConfirmDialog)
                return;

            ImGui.OpenPopup("Confirmar Sobrescritura");

            bool isOpen = _showOverwriteConfirmDialog;
            if (ImGui.BeginPopupModal("Confirmar Sobrescritura", ref isOpen, ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(1, 0.8f, 0, 1));
                ImGui.Text("⚠ ADVERTENCIA");
                ImGui.PopStyleColor();

                ImGui.Spacing();
                ImGui.Text($"La escena '{_sceneNameToOverwrite}' ya existe.");
                ImGui.Text("¿Deseas sobrescribir el archivo existente?");
                ImGui.Spacing();

                ImGui.TextColored(new System.Numerics.Vector4(0.8f, 0.3f, 0.3f, 1),
                    "Esta acción no se puede deshacer.");

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                // Botón de Sobrescribir (rojo)
                ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.8f, 0.2f, 0.2f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new System.Numerics.Vector4(1.0f, 0.3f, 0.3f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new System.Numerics.Vector4(0.7f, 0.1f, 0.1f, 1.0f));

                if (ImGui.Button("Sobrescribir", new System.Numerics.Vector2(145, 0)))
                {
                    PerformSaveScene(_sceneNameToOverwrite);
                    _showOverwriteConfirmDialog = false;
                    _showSaveDialog = false;
                    _sceneNameInput = "";
                    _sceneNameToOverwrite = "";
                }

                ImGui.PopStyleColor(3);
                ImGui.SameLine();

                // Botón de Cancelar
                if (ImGui.Button("Cancelar", new System.Numerics.Vector2(145, 0)))
                {
                    _showOverwriteConfirmDialog = false;
                    _sceneNameToOverwrite = "";
                }

                ImGui.EndPopup();
            }

            if (!isOpen)
            {
                _showOverwriteConfirmDialog = false;
            }
        }

        // NUEVO: Confirmación de sobrescritura para Ctrl+S / "Save Scene"
        private void DrawQuickSaveConfirmDialog()
        {
            if (!_showQuickSaveConfirmDialog)
                return;

            ImGui.OpenPopup("Confirmar Guardado");

            bool isOpen = _showQuickSaveConfirmDialog;
            if (ImGui.BeginPopupModal("Confirmar Guardado", ref isOpen, ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(1, 0.8f, 0, 1));
                ImGui.Text("⚠ ADVERTENCIA");
                ImGui.PopStyleColor();

                ImGui.Spacing();
                ImGui.Text($"La escena '{_sceneNameToOverwrite}' ya existe.");
                ImGui.Text("¿Deseas sobrescribir el archivo existente?");
                ImGui.Spacing();

                ImGui.TextColored(new System.Numerics.Vector4(0.8f, 0.3f, 0.3f, 1),
                    "Esta acción no se puede deshacer.");

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                // Botón de Sobrescribir (rojo)
                ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.8f, 0.2f, 0.2f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new System.Numerics.Vector4(1.0f, 0.3f, 0.3f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new System.Numerics.Vector4(0.7f, 0.1f, 0.1f, 1.0f));

                if (ImGui.Button("Sobrescribir", new System.Numerics.Vector2(145, 0)))
                {
                    PerformSaveScene(_sceneNameToOverwrite);
                    _showQuickSaveConfirmDialog = false;
                    _sceneNameToOverwrite = "";
                }

                ImGui.PopStyleColor(3);
                ImGui.SameLine();

                // Botón de Cancelar
                if (ImGui.Button("Cancelar", new System.Numerics.Vector2(145, 0)))
                {
                    _showQuickSaveConfirmDialog = false;
                    _sceneNameToOverwrite = "";
                }

                ImGui.EndPopup();
            }

            if (!isOpen)
            {
                _showQuickSaveConfirmDialog = false;
            }
        }

        private void DrawOpenDialog()
        {
            if (!_showOpenDialog)
                return;

            ImGui.OpenPopup("Abrir Escena");

            if (ImGui.BeginPopupModal("Abrir Escena", ref _showOpenDialog, ImGuiWindowFlags.AlwaysAutoResize))
            {
                if (_availableScenes.Length == 0)
                {
                    ImGui.Text("No hay escenas guardadas disponibles.");
                    ImGui.Spacing();

                    if (ImGui.Button("Cerrar", new System.Numerics.Vector2(-1, 0)))
                    {
                        _showOpenDialog = false;
                    }
                }
                else
                {
                    ImGui.Text("Selecciona una escena para cargar:");
                    ImGui.Spacing();

                    if (_selectedSceneIndex >= 0 && _selectedSceneIndex < _availableScenes.Length)
                    {
                        string selectedScene = _availableScenes[_selectedSceneIndex];
                        string filePath = GetSceneFilePath(selectedScene);

                        if (File.Exists(filePath))
                        {
                            var fileInfo = new FileInfo(filePath);
                            ImGui.TextColored(new System.Numerics.Vector4(0.5f, 0.8f, 1f, 1f),
                                $"Última modificación: {fileInfo.LastWriteTime:dd/MM/yyyy HH:mm}");
                            ImGui.TextColored(new System.Numerics.Vector4(0.5f, 0.8f, 1f, 1f),
                                $"Tamaño: {fileInfo.Length / 1024.0:F2} KB");
                        }
                    }

                    ImGui.Spacing();
                    ImGui.SetNextItemWidth(400);

                    if (ImGui.BeginListBox("##scenelist", new System.Numerics.Vector2(400, 250)))
                    {
                        for (int i = 0; i < _availableScenes.Length; i++)
                        {
                            bool isSelected = (_selectedSceneIndex == i);
                            if (ImGui.Selectable(_availableScenes[i], isSelected))
                            {
                                _selectedSceneIndex = i;
                            }

                            if (isSelected && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                            {
                                LoadScene(_availableScenes[_selectedSceneIndex]);
                                _showOpenDialog = false;
                            }
                        }
                        ImGui.EndListBox();
                    }

                    ImGui.Spacing();
                    ImGui.Separator();
                    ImGui.Spacing();

                    if (ImGui.Button("Abrir", new System.Numerics.Vector2(130, 0)))
                    {
                        if (_selectedSceneIndex >= 0 && _selectedSceneIndex < _availableScenes.Length)
                        {
                            LoadScene(_availableScenes[_selectedSceneIndex]);
                            _showOpenDialog = false;
                        }
                    }

                    ImGui.SameLine();

                    if (ImGui.Button("Cancelar", new System.Numerics.Vector2(130, 0)))
                    {
                        _showOpenDialog = false;
                    }

                    ImGui.SameLine();

                    if (ImGui.Button("Refrescar", new System.Numerics.Vector2(130, 0)))
                    {
                        RefreshAvailableScenes();
                    }

                    ImGui.Spacing();

                    if (_selectedSceneIndex >= 0 && _selectedSceneIndex < _availableScenes.Length)
                    {
                        ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.8f, 0.2f, 0.2f, 1.0f));
                        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new System.Numerics.Vector4(1.0f, 0.3f, 0.3f, 1.0f));

                        if (ImGui.Button("🗑 Eliminar Escena Seleccionada", new System.Numerics.Vector2(400, 0)))
                        {
                            DeleteScene(_availableScenes[_selectedSceneIndex]);
                        }

                        ImGui.PopStyleColor(2);
                    }
                }

                ImGui.EndPopup();
            }
        }

        #endregion

        #region Helper Methods

        private void RefreshAvailableScenes()
        {
            try
            {
                if (!Directory.Exists(SCENES_DIRECTORY))
                {
                    Directory.CreateDirectory(SCENES_DIRECTORY);
                    _availableScenes = new string[0];
                    return;
                }

                var files = Directory.GetFiles(SCENES_DIRECTORY, "*.scene")
                    .Select(f => Path.GetFileNameWithoutExtension(f))
                    .OrderByDescending(f => File.GetLastWriteTime(Path.Combine(SCENES_DIRECTORY, f + ".scene")))
                    .ToArray();

                _availableScenes = files;

                if (_selectedSceneIndex >= _availableScenes.Length)
                {
                    _selectedSceneIndex = Math.Max(0, _availableScenes.Length - 1);
                }
            }
            catch (Exception ex)
            {
                EngineEditor.LogMessage($"Error al listar escenas: {ex.Message}");
                _availableScenes = new string[0];
            }
        }

        private string GetSceneFilePath(string sceneName)
        {
            if (!Directory.Exists(SCENES_DIRECTORY))
            {
                Directory.CreateDirectory(SCENES_DIRECTORY);
            }

            return Path.Combine(SCENES_DIRECTORY, $"{sceneName}.scene");
        }

        private void DeleteScene(string sceneName)
        {
            try
            {
                string sceneFilePath = GetSceneFilePath(sceneName);

                if (File.Exists(sceneFilePath))
                {
                    File.Delete(sceneFilePath);
                    EngineEditor.LogMessage($"Escena eliminada: {sceneName}");
                }

                RefreshAvailableScenes();
            }
            catch (Exception ex)
            {
                EngineEditor.LogMessage($"Error al eliminar escena: {ex.Message}");
            }
        }

        private void HandleKeyboardShortcuts()
        {
            // Placeholder para atajos de teclado
        }

        #endregion
    }
}