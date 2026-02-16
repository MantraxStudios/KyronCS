using ImGuiNET;
using KrayonCore;
using KrayonCore.Core.Attributes;
using KrayonCore.EventSystem;
using KrayonCore.GraphicsData;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text.Json;

namespace KrayonEditor.UI
{
    [Serializable]
    public class TileConfiguration
    {
        public string Name { get; set; } = "Default";
        public string MaterialPath { get; set; } = "";
        public int TileWidth { get; set; } = 32;
        public int TileHeight { get; set; } = 32;
        public float PixelsPerUnit { get; set; } = 32.0f;
        public int SelectedTileX { get; set; } = -1;
        public int SelectedTileY { get; set; } = -1;
        public string ObjectNamePrefix { get; set; } = "NewSprite";
        public float GridSnapSize { get; set; } = 1.0f;
    }

    public class TileEditor : UIBehaviour
    {
        private string _materialPath = "";
        private Material _loadedMaterial = null;
        private IntPtr _textureId = IntPtr.Zero;
        private int _textureWidth = 0;
        private int _textureHeight = 0;

        private int _tileWidth = 32;
        private int _tileHeight = 32;
        private float _pixelsPerUnit = 32.0f;

        private int _selectedTileX = -1;
        private int _selectedTileY = -1;
        private int _hoveredTileX = -1;
        private int _hoveredTileY = -1;

        private int _tilesPerRow = 0;
        private int _tilesPerColumn = 0;

        private string _objectName = "NewSprite";
        private GameObject _tempLoaderObject = null;
        private int _loadAttempts = 0;
        private const int MAX_LOAD_ATTEMPTS = 60;

        private bool _paintMode = false;
        private GameObject _previewObject = null;
        private float _paintZPosition = 0.0f;
        private float _gridSnapSize = 1.0f;

        private List<string> _savedConfigurations = new List<string>();
        private string _currentConfigName = "Default";
        private string _newConfigName = "";
        private bool _showSaveDialog = false;
        private bool _showLoadDialog = false;

        private static readonly Vector4 ColAccent = new Vector4(0.30f, 0.65f, 1.00f, 1.00f);
        private static readonly Vector4 ColAccentDim = new Vector4(0.30f, 0.65f, 1.00f, 0.25f);
        private static readonly Vector4 ColSuccess = new Vector4(0.25f, 0.85f, 0.45f, 1.00f);
        private static readonly Vector4 ColDanger = new Vector4(0.90f, 0.30f, 0.30f, 1.00f);
        private static readonly Vector4 ColHeader = new Vector4(0.75f, 0.88f, 1.00f, 1.00f);
        private static readonly Vector4 ColMuted = new Vector4(0.50f, 0.55f, 0.65f, 1.00f);
        private static readonly Vector4 ColBg = new Vector4(0.10f, 0.11f, 0.14f, 1.00f);
        private static readonly Vector4 ColBgPanel = new Vector4(0.13f, 0.14f, 0.18f, 1.00f);
        private static readonly Vector4 ColBorder = new Vector4(0.22f, 0.24f, 0.30f, 1.00f);
        private static readonly Vector4 ColWarning = new Vector4(1.00f, 0.75f, 0.20f, 1.00f);

        private uint C(Vector4 v) => ImGui.GetColorU32(v);

        private string ConfigPath => AssetManager.TotalBase + "/TilesData";

        public TileEditor()
        {
            LoadStartupConfiguration();
            RefreshConfigurationList();
        }

        public override void OnDrawUI()
        {
            if (!_isVisible)
                return;

            if (_tempLoaderObject != null && _loadAttempts < MAX_LOAD_ATTEMPTS)
            {
                _loadAttempts++;
                TryLoadTextureFromTempObject();
            }

            UpdatePaintMode();

            ImGui.SetNextWindowSize(new Vector2(800, 700), ImGuiCond.FirstUseEver);
            ImGui.PushStyleColor(ImGuiCol.WindowBg, ColBg);
            ImGui.Begin("Tile Editor", ref _isVisible, ImGuiWindowFlags.MenuBar);
            ImGui.PopStyleColor();

            DrawMenuBar();

            DrawMaterialSelector();
            ImGui.Spacing();
            ImGui.PushStyleColor(ImGuiCol.Separator, ColBorder);
            ImGui.Separator();
            ImGui.PopStyleColor();
            ImGui.Spacing();

            if (_loadedMaterial != null && _textureId != IntPtr.Zero)
            {
                DrawTileConfiguration();
                ImGui.Spacing();
                ImGui.PushStyleColor(ImGuiCol.Separator, ColBorder);
                ImGui.Separator();
                ImGui.PopStyleColor();
                ImGui.Spacing();
                DrawTileGrid();
                ImGui.Spacing();
                DrawCreateObjectSection();
            }
            else if (_tempLoaderObject != null)
            {
                var avail = ImGui.GetContentRegionAvail();
                string msg = "Loading material...";
                var sz = ImGui.CalcTextSize(msg);
                ImGui.SetCursorPos(new Vector2((avail.X - sz.X) * 0.5f, avail.Y * 0.5f));
                ImGui.TextColored(ColAccent, msg);
            }
            else
            {
                DrawEmptyState();
            }

            ImGui.End();

            if (_showSaveDialog) DrawSaveDialog();
            if (_showLoadDialog) DrawLoadDialog();
        }

        private void DrawMenuBar()
        {
            if (ImGui.BeginMenuBar())
            {
                if (ImGui.BeginMenu("File"))
                {
                    if (ImGui.MenuItem("Save Configuration", "Ctrl+S"))
                    {
                        _showSaveDialog = true;
                    }
                    if (ImGui.MenuItem("Load Configuration", "Ctrl+O"))
                    {
                        RefreshConfigurationList();
                        _showLoadDialog = true;
                    }
                    ImGui.Separator();
                    if (ImGui.MenuItem("Save as Startup"))
                    {
                        SaveStartupConfiguration();
                    }
                    ImGui.EndMenu();
                }
                ImGui.EndMenuBar();
            }
        }

        private void DrawSaveDialog()
        {
            ImGui.SetNextWindowSize(new Vector2(400, 150), ImGuiCond.Always);
            ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

            ImGui.PushStyleColor(ImGuiCol.WindowBg, ColBg);
            if (ImGui.Begin("Save Configuration", ref _showSaveDialog, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse))
            {
                ImGui.TextColored(ColHeader, "Save Current Configuration");
                ImGui.Spacing();

                ImGui.TextColored(ColMuted, "Configuration Name");
                ImGui.SetNextItemWidth(-1);
                ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.18f, 0.20f, 0.26f, 1f));
                ImGui.InputTextWithHint("##ConfigName", "Enter configuration name", ref _newConfigName, 256);
                ImGui.PopStyleColor();

                ImGui.Spacing();
                ImGui.Spacing();

                bool canSave = !string.IsNullOrWhiteSpace(_newConfigName);
                if (!canSave) ImGui.BeginDisabled();

                if (ImGui.Button("Save", new Vector2(-1, 30)))
                {
                    SaveConfiguration(_newConfigName);
                    _showSaveDialog = false;
                    _newConfigName = "";
                }

                if (!canSave) ImGui.EndDisabled();
            }
            ImGui.End();
            ImGui.PopStyleColor();
        }

        private void DrawLoadDialog()
        {
            ImGui.SetNextWindowSize(new Vector2(400, 400), ImGuiCond.Always);
            ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

            ImGui.PushStyleColor(ImGuiCol.WindowBg, ColBg);
            if (ImGui.Begin("Load Configuration", ref _showLoadDialog, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse))
            {
                ImGui.TextColored(ColHeader, "Saved Configurations");
                ImGui.Spacing();

                ImGui.BeginChild("ConfigList", new Vector2(-1, -40));

                if (_savedConfigurations.Count == 0)
                {
                    ImGui.TextColored(ColMuted, "No saved configurations found");
                }
                else
                {
                    foreach (var configName in _savedConfigurations)
                    {
                        bool isSelected = _currentConfigName == configName;

                        ImGui.PushID(configName);

                        if (ImGui.Selectable(configName, isSelected, ImGuiSelectableFlags.None, new Vector2(0, 30)))
                        {
                            LoadConfiguration(configName);
                            _showLoadDialog = false;
                        }

                        if (ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                        {
                            ImGui.OpenPopup("ConfigContextMenu");
                        }

                        if (ImGui.BeginPopup("ConfigContextMenu"))
                        {
                            if (ImGui.MenuItem("Delete"))
                            {
                                DeleteConfiguration(configName);
                                RefreshConfigurationList();
                            }
                            ImGui.EndPopup();
                        }

                        ImGui.PopID();
                    }
                }

                ImGui.EndChild();

                if (ImGui.Button("Cancel", new Vector2(-1, 30)))
                {
                    _showLoadDialog = false;
                }
            }
            ImGui.End();
            ImGui.PopStyleColor();
        }

        private void SaveConfiguration(string name)
        {
            try
            {
                if (!Directory.Exists(ConfigPath))
                    Directory.CreateDirectory(ConfigPath);

                var config = new TileConfiguration
                {
                    Name = name,
                    MaterialPath = _materialPath,
                    TileWidth = _tileWidth,
                    TileHeight = _tileHeight,
                    PixelsPerUnit = _pixelsPerUnit,
                    SelectedTileX = _selectedTileX,
                    SelectedTileY = _selectedTileY,
                    ObjectNamePrefix = _objectName,
                    GridSnapSize = _gridSnapSize
                };

                string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                string filePath = Path.Combine(ConfigPath, $"{name}.json");
                File.WriteAllText(filePath, json);

                _currentConfigName = name;
                RefreshConfigurationList();

                Console.WriteLine($"[TileEditor] ✓ Configuration saved: {name}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TileEditor] ✗ Error saving configuration: {ex.Message}");
            }
        }

        private void LoadConfiguration(string name)
        {
            try
            {
                string filePath = Path.Combine(ConfigPath, $"{name}.json");
                if (!File.Exists(filePath))
                {
                    Console.WriteLine($"[TileEditor] ✗ Configuration not found: {name}");
                    return;
                }

                string json = File.ReadAllText(filePath);
                var config = JsonSerializer.Deserialize<TileConfiguration>(json);

                if (config != null)
                {
                    _materialPath = config.MaterialPath;
                    _tileWidth = config.TileWidth;
                    _tileHeight = config.TileHeight;
                    _pixelsPerUnit = config.PixelsPerUnit;
                    _selectedTileX = config.SelectedTileX;
                    _selectedTileY = config.SelectedTileY;
                    _objectName = config.ObjectNamePrefix;
                    _gridSnapSize = config.GridSnapSize;
                    _currentConfigName = name;

                    if (!string.IsNullOrEmpty(_materialPath))
                        LoadMaterial();

                    Console.WriteLine($"[TileEditor] ✓ Configuration loaded: {name}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TileEditor] ✗ Error loading configuration: {ex.Message}");
            }
        }

        private void DeleteConfiguration(string name)
        {
            try
            {
                string filePath = Path.Combine(ConfigPath, $"{name}.json");
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    Console.WriteLine($"[TileEditor] ✓ Configuration deleted: {name}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TileEditor] ✗ Error deleting configuration: {ex.Message}");
            }
        }

        private void SaveStartupConfiguration()
        {
            try
            {
                if (!Directory.Exists(ConfigPath))
                    Directory.CreateDirectory(ConfigPath);

                var config = new TileConfiguration
                {
                    Name = "Startup",
                    MaterialPath = _materialPath,
                    TileWidth = _tileWidth,
                    TileHeight = _tileHeight,
                    PixelsPerUnit = _pixelsPerUnit,
                    SelectedTileX = _selectedTileX,
                    SelectedTileY = _selectedTileY,
                    ObjectNamePrefix = _objectName,
                    GridSnapSize = _gridSnapSize
                };

                string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                string filePath = Path.Combine(ConfigPath, "Startup.json");
                File.WriteAllText(filePath, json);

                Console.WriteLine($"[TileEditor] ✓ Startup configuration saved");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TileEditor] ✗ Error saving startup configuration: {ex.Message}");
            }
        }

        private void LoadStartupConfiguration()
        {
            try
            {
                string filePath = Path.Combine(ConfigPath, "Startup.json");
                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath);
                    var config = JsonSerializer.Deserialize<TileConfiguration>(json);

                    if (config != null)
                    {
                        _materialPath = config.MaterialPath;
                        _tileWidth = config.TileWidth;
                        _tileHeight = config.TileHeight;
                        _pixelsPerUnit = config.PixelsPerUnit;
                        _selectedTileX = config.SelectedTileX;
                        _selectedTileY = config.SelectedTileY;
                        _objectName = config.ObjectNamePrefix;
                        _gridSnapSize = config.GridSnapSize;

                        Console.WriteLine($"[TileEditor] ✓ Startup configuration loaded");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TileEditor] ✗ Error loading startup configuration: {ex.Message}");
            }
        }

        private void RefreshConfigurationList()
        {
            _savedConfigurations.Clear();

            try
            {
                if (Directory.Exists(ConfigPath))
                {
                    var files = Directory.GetFiles(ConfigPath, "*.json");
                    foreach (var file in files)
                    {
                        string fileName = Path.GetFileNameWithoutExtension(file);
                        if (fileName != "Startup")
                            _savedConfigurations.Add(fileName);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TileEditor] ✗ Error refreshing configuration list: {ex.Message}");
            }
        }

        private void UpdatePaintMode()
        {
            if (!_paintMode || _selectedTileX < 0 || _selectedTileY < 0)
            {
                CleanupPreviewObject();
                return;
            }

            var sceneWindow = EditorUI._sceneView;
            if (sceneWindow == null || !EditorActions.IsHoveringScene)
            {
                CleanupPreviewObject();
                return;
            }

            // Control + Mouse Wheel para ajustar Z position
            if (ImGui.IsKeyDown(ImGuiKey.LeftCtrl) || ImGui.IsKeyDown(ImGuiKey.RightCtrl))
            {
                float mouseWheel = ImGui.GetIO().MouseWheel;
                if (mouseWheel != 0)
                {
                    _paintZPosition += mouseWheel * _gridSnapSize;
                    Console.WriteLine($"[TileEditor] Z Position: {_paintZPosition}");
                }
            }

            var mouseState = GraphicsEngine.Instance.GetMouseState();
            var camera = GraphicsEngine.Instance.GetSceneRenderer().GetCamera();
            int screenWidth = GraphicsEngine.Instance.GetSceneFrameBuffer().Width;
            int screenHeight = GraphicsEngine.Instance.GetSceneFrameBuffer().Height;

            System.Numerics.Vector2 globalMousePos = ImGui.GetMousePos();
            System.Numerics.Vector2 sceneWindowPos = EditorActions.ViewPortPosition;
            System.Numerics.Vector2 relativeMousePos = new System.Numerics.Vector2(
                globalMousePos.X - sceneWindowPos.X,
                globalMousePos.Y - sceneWindowPos.Y
            );

            OpenTK.Mathematics.Vector2 openTKMousePos = new OpenTK.Mathematics.Vector2(
                relativeMousePos.X,
                relativeMousePos.Y
            );

            EventSystem.ScreenToWorldRay(openTKMousePos, camera, screenWidth, screenHeight,
                out OpenTK.Mathematics.Vector3 rayOrigin, out OpenTK.Mathematics.Vector3 rayDir);

            float snappedX = MathF.Round(rayOrigin.X / _gridSnapSize) * _gridSnapSize;
            float snappedY = MathF.Round(rayOrigin.Y / _gridSnapSize) * _gridSnapSize;

            if (_previewObject == null)
            {
                var scene = SceneManager.ActiveScene;
                if (scene != null)
                {
                    _previewObject = scene.CreateGameObject();
                    _previewObject.Name = "__TilePaintPreview__";
                    var sprite = _previewObject.AddComponent<SpriteRenderer>();
                    sprite.MaterialPath = _materialPath;
                    sprite.TileWidth = _tileWidth;
                    sprite.TileHeight = _tileHeight;
                    sprite.TileIndexX = _selectedTileX;
                    sprite.TileIndexY = _selectedTileY;
                    sprite.PixelsPerUnit = _pixelsPerUnit;
                    sprite.Color = new OpenTK.Mathematics.Vector4(1, 1, 1, 0.5f);
                    sprite.Start();
                }
            }

            if (_previewObject != null)
            {
                _previewObject.Transform.SetWorldPosition(new OpenTK.Mathematics.Vector3(snappedX, snappedY, _paintZPosition));

                if (mouseState.IsButtonPressed(OpenTK.Windowing.GraphicsLibraryFramework.MouseButton.Left))
                {
                    var scene = SceneManager.ActiveScene;
                    if (scene != null)
                    {
                        var newObject = scene.CreateGameObject();
                        newObject.Name = _objectName;
                        var sprite = newObject.AddComponent<SpriteRenderer>();
                        sprite.MaterialPath = _materialPath;
                        sprite.TileWidth = _tileWidth;
                        sprite.TileHeight = _tileHeight;
                        sprite.TileIndexX = _selectedTileX;
                        sprite.TileIndexY = _selectedTileY;
                        sprite.PixelsPerUnit = _pixelsPerUnit;
                        newObject.Transform.SetWorldPosition(new OpenTK.Mathematics.Vector3(snappedX, snappedY, _paintZPosition));
                        sprite.Start();

                        Console.WriteLine($"[TileEditor] Painted tile [{_selectedTileX}, {_selectedTileY}] at ({snappedX}, {snappedY}, {_paintZPosition})");
                        IncrementObjectName();
                    }
                }
            }

            if (ImGui.IsKeyPressed(ImGuiKey.Escape))
            {
                _paintMode = false;
                _paintZPosition = 0.0f;
                CleanupPreviewObject();
            }
        }

        private void CleanupPreviewObject()
        {
            if (_previewObject != null)
            {
                var scene = SceneManager.ActiveScene;
                scene?.DestroyGameObject(_previewObject);
                _previewObject = null;
            }
        }

        private void DrawMaterialSelector()
        {
            ImGui.PushStyleColor(ImGuiCol.Text, ColHeader);
            ImGui.TextUnformatted("MATERIAL");
            ImGui.PopStyleColor();

            if (!string.IsNullOrEmpty(_currentConfigName))
            {
                ImGui.SameLine();
                ImGui.TextColored(ColAccent, $"[{_currentConfigName}]");
            }

            ImGui.TextColored(ColMuted, "Enter material name to load");
            ImGui.SetNextItemWidth(-80);
            ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.18f, 0.20f, 0.26f, 1f));
            if (ImGui.InputTextWithHint("##MatPath", "e.g. spritesheet_player", ref _materialPath, 512, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                LoadMaterial();
            }
            ImGui.PopStyleColor();

            ImGui.SameLine();
            bool canLoad = !string.IsNullOrWhiteSpace(_materialPath);
            if (!canLoad) ImGui.BeginDisabled();

            ImGui.PushStyleColor(ImGuiCol.Button, ColAccent with { W = 0.18f });
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, ColAccent with { W = 0.35f });
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, ColAccent with { W = 0.55f });
            ImGui.PushStyleColor(ImGuiCol.Text, ColAccent);
            if (ImGui.Button("Load", new Vector2(70, 0)))
            {
                LoadMaterial();
            }
            ImGui.PopStyleColor(4);

            if (!canLoad) ImGui.EndDisabled();

            if (_loadedMaterial != null)
            {
                ImGui.Spacing();
                ImGui.TextColored(ColSuccess, $"✓ Loaded: {_materialPath}");
                ImGui.TextColored(ColMuted, $"Texture size: {_textureWidth} x {_textureHeight} px");
            }
        }

        private void DrawTileConfiguration()
        {
            ImGui.PushStyleColor(ImGuiCol.Text, ColHeader);
            ImGui.TextUnformatted("TILE SIZE");
            ImGui.PopStyleColor();

            ImGui.TextColored(ColMuted, "Width");
            ImGui.SetNextItemWidth(85);
            if (ImGui.InputInt("##TileW", ref _tileWidth))
            {
                _tileWidth = Math.Max(1, _tileWidth);
                CalculateGrid();
            }

            ImGui.SameLine();
            ImGui.TextColored(ColMuted, "Height");
            ImGui.SetNextItemWidth(85);
            if (ImGui.InputInt("##TileH", ref _tileHeight))
            {
                _tileHeight = Math.Max(1, _tileHeight);
                CalculateGrid();
            }

            ImGui.SameLine();
            ImGui.TextColored(ColMuted, "PPU");
            ImGui.SetNextItemWidth(85);
            if (ImGui.InputFloat("##PPU", ref _pixelsPerUnit))
            {
                _pixelsPerUnit = Math.Max(0.1f, _pixelsPerUnit);
            }

            ImGui.SameLine();
            ImGui.TextColored(ColMuted, "Snap");
            ImGui.SetNextItemWidth(85);
            if (ImGui.InputFloat("##Snap", ref _gridSnapSize))
            {
                _gridSnapSize = Math.Max(0.1f, _gridSnapSize);
            }

            if (_tilesPerRow > 0 && _tilesPerColumn > 0)
            {
                ImGui.TextColored(ColMuted, $"Grid: {_tilesPerRow} x {_tilesPerColumn} tiles | PPU: {_pixelsPerUnit:F1} | Snap: {_gridSnapSize:F1}");
            }
        }

        private void DrawTileGrid()
        {
            if (_tilesPerRow == 0 || _tilesPerColumn == 0)
            {
                ImGui.TextColored(ColMuted, "Configure tile size to see grid");
                return;
            }

            ImGui.PushStyleColor(ImGuiCol.Text, ColHeader);
            ImGui.TextUnformatted("TILE GRID");
            ImGui.PopStyleColor();
            ImGui.TextColored(ColMuted, "Click on a tile to select it");

            ImGui.BeginChild("TileGridArea", new Vector2(0, -120));

            const float CELL = 64f;
            const float PAD = 5f;

            float availWidth = ImGui.GetContentRegionAvail().X;
            int tilesPerRowInView = Math.Max(1, (int)((availWidth + PAD) / (CELL + PAD)));

            _hoveredTileX = -1;
            _hoveredTileY = -1;

            int tileIndex = 0;
            int currentRowTiles = 0;

            for (int y = 0; y < _tilesPerColumn; y++)
            {
                for (int x = 0; x < _tilesPerRow; x++)
                {
                    if (currentRowTiles > 0)
                    {
                        ImGui.SameLine(0, PAD);
                    }

                    ImGui.PushID(tileIndex);

                    var cellMin = ImGui.GetCursorScreenPos();
                    var cellMax = new Vector2(cellMin.X + CELL, cellMin.Y + CELL);
                    var dl = ImGui.GetWindowDrawList();

                    bool isSelected = (_selectedTileX == x && _selectedTileY == y);
                    uint bgColor = isSelected ? C(ColAccentDim) : C(ColBgPanel);
                    dl.AddRectFilled(cellMin, cellMax, bgColor, 5f);

                    var (u0, v0, u1, v1) = GetTileUV(x, y);
                    dl.AddImage(_textureId, cellMin, cellMax, new Vector2(u0, v0), new Vector2(u1, v1));

                    ImGui.InvisibleButton($"##tile{x}_{y}", new Vector2(CELL, CELL));

                    if (ImGui.IsItemHovered())
                    {
                        _hoveredTileX = x;
                        _hoveredTileY = y;
                        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

                        dl.AddRectFilled(cellMin, cellMax, C(ColAccentDim), 5f);
                        dl.AddRect(cellMin, cellMax, C(ColAccent), 5f, ImDrawFlags.None, 2f);

                        ImGui.BeginTooltip();
                        ImGui.TextColored(ColAccent, $"Tile [{x}, {y}]");
                        ImGui.TextColored(ColMuted, "Click to select");
                        ImGui.EndTooltip();
                    }
                    else
                    {
                        uint borderColor = isSelected ? C(ColAccent) : C(ColBorder);
                        float borderWidth = isSelected ? 2f : 1f;
                        dl.AddRect(cellMin, cellMax, borderColor, 5f, ImDrawFlags.None, borderWidth);
                    }

                    if (ImGui.IsItemClicked())
                    {
                        _selectedTileX = x;
                        _selectedTileY = y;
                        Console.WriteLine($"[TileEditor] Selected tile: [{x}, {y}]");
                    }

                    ImGui.PopID();

                    tileIndex++;
                    currentRowTiles++;

                    if (currentRowTiles >= tilesPerRowInView)
                    {
                        currentRowTiles = 0;
                    }
                }
            }

            ImGui.EndChild();
        }

        private void DrawCreateObjectSection()
        {
            ImGui.PushStyleColor(ImGuiCol.Separator, ColBorder);
            ImGui.Separator();
            ImGui.PopStyleColor();
            ImGui.Spacing();

            ImGui.PushStyleColor(ImGuiCol.Text, ColHeader);
            ImGui.TextUnformatted("CREATE OBJECT");
            ImGui.PopStyleColor();

            if (_selectedTileX >= 0 && _selectedTileY >= 0)
            {
                ImGui.TextColored(ColSuccess, $"✓ Selected Tile: [{_selectedTileX}, {_selectedTileY}]");
                ImGui.TextColored(ColMuted, $"Material: {_materialPath} | Size: {_tileWidth}x{_tileHeight}px | PPU: {_pixelsPerUnit:F1}");

                ImGui.Spacing();

                ImGui.TextColored(ColMuted, "Object Name");
                ImGui.SetNextItemWidth(-240);
                ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.18f, 0.20f, 0.26f, 1f));
                ImGui.InputText("##ObjName", ref _objectName, 256);
                ImGui.PopStyleColor();

                ImGui.SameLine();
                bool canCreate = !string.IsNullOrWhiteSpace(_objectName);
                if (!canCreate) ImGui.BeginDisabled();

                ImGui.PushStyleColor(ImGuiCol.Button, ColSuccess with { W = 0.25f });
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, ColSuccess with { W = 0.45f });
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, ColSuccess with { W = 0.65f });
                ImGui.PushStyleColor(ImGuiCol.Text, ColSuccess);
                if (ImGui.Button("Create", new Vector2(80, 26)))
                {
                    CreateSpriteObject();
                }
                ImGui.PopStyleColor(4);

                if (!canCreate) ImGui.EndDisabled();

                ImGui.SameLine();

                Vector4 paintColor = _paintMode ? ColDanger : ColAccent;
                ImGui.PushStyleColor(ImGuiCol.Button, paintColor with { W = 0.25f });
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, paintColor with { W = 0.45f });
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, paintColor with { W = 0.65f });
                ImGui.PushStyleColor(ImGuiCol.Text, paintColor);
                string paintLabel = _paintMode ? "Stop Paint" : "Paint Mode";
                if (ImGui.Button(paintLabel, new Vector2(120, 26)))
                {
                    _paintMode = !_paintMode;
                    if (!_paintMode)
                    {
                        _paintZPosition = 0.0f;
                        CleanupPreviewObject();
                    }
                }
                ImGui.PopStyleColor(4);

                if (_paintMode)
                {
                    ImGui.Spacing();
                    ImGui.TextColored(ColWarning, $"🎨 Paint Mode Active | Z: {_paintZPosition:F2} | Ctrl+Wheel to adjust Z | ESC to exit");
                }
            }
            else
            {
                ImGui.TextColored(ColMuted, "Select a tile from the grid above to create an object");
            }
        }

        private void CreateSpriteObject()
        {
            var scene = SceneManager.ActiveScene;
            if (scene == null)
            {
                Console.WriteLine("[TileEditor] ✗ No active scene");
                return;
            }

            try
            {
                var gameObject = scene.CreateGameObject();
                gameObject.Name = _objectName;

                var sprite = gameObject.AddComponent<SpriteRenderer>();

                sprite.MaterialPath = _materialPath;
                sprite.TileWidth = _tileWidth;
                sprite.TileHeight = _tileHeight;
                sprite.TileIndexX = _selectedTileX;
                sprite.TileIndexY = _selectedTileY;
                sprite.PixelsPerUnit = _pixelsPerUnit;
                sprite.Start();

                Console.WriteLine($"[TileEditor] ✓ Created object '{_objectName}' with tile [{_selectedTileX}, {_selectedTileY}]");
                Console.WriteLine($"[TileEditor]   Material: {_materialPath}");
                Console.WriteLine($"[TileEditor]   Tile Size: {_tileWidth}x{_tileHeight}");
                Console.WriteLine($"[TileEditor]   Pixels Per Unit: {_pixelsPerUnit}");

                IncrementObjectName();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TileEditor] ✗ Error creating object: {ex.Message}");
            }
        }

        private void IncrementObjectName()
        {
            int lastUnderscore = _objectName.LastIndexOf('_');
            if (lastUnderscore >= 0 && lastUnderscore < _objectName.Length - 1)
            {
                string prefix = _objectName.Substring(0, lastUnderscore + 1);
                string suffix = _objectName.Substring(lastUnderscore + 1);

                if (int.TryParse(suffix, out int number))
                {
                    _objectName = prefix + (number + 1);
                    return;
                }
            }

            _objectName += "_1";
        }

        private void DrawEmptyState()
        {
            var avail = ImGui.GetContentRegionAvail();
            string line1 = "No material loaded";
            string line2 = "Enter a material name above and click Load";

            var s1 = ImGui.CalcTextSize(line1);
            var s2 = ImGui.CalcTextSize(line2);

            ImGui.SetCursorPos(new Vector2((avail.X - s1.X) * 0.5f, avail.Y * 0.5f - 20));
            ImGui.TextColored(new Vector4(0.6f, 0.65f, 0.75f, 1f), line1);

            ImGui.SetCursorPos(new Vector2((avail.X - s2.X) * 0.5f, avail.Y * 0.5f));
            ImGui.TextColored(ColMuted, line2);
        }

        private void LoadMaterial()
        {
            if (string.IsNullOrWhiteSpace(_materialPath))
                return;

            CleanupTempObject();
            ClearMaterial();

            var scene = SceneManager.ActiveScene;
            if (scene == null)
            {
                Console.WriteLine("[TileEditor] ✗ No active scene");
                return;
            }

            try
            {
                _tempLoaderObject = scene.CreateGameObject();
                _tempLoaderObject.Name = "__TileEditorLoader__";
                var tempSprite = _tempLoaderObject.AddComponent<SpriteRenderer>();
                tempSprite.MaterialPath = _materialPath;
                _loadAttempts = 0;
                tempSprite.Start();

                Console.WriteLine($"[TileEditor] Loading material: {_materialPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TileEditor] ✗ Error loading material '{_materialPath}': {ex.Message}");
                CleanupTempObject();
            }
        }

        private void TryLoadTextureFromTempObject()
        {
            if (_tempLoaderObject == null) return;

            var sprite = _tempLoaderObject.GetComponent<SpriteRenderer>();
            if (sprite == null)
            {
                CleanupTempObject();
                return;
            }

            var material = sprite.Material;
            if (material != null && material.AlbedoTexture != null)
            {
                _textureId = material.AlbedoTexture.TextureId;
                _textureWidth = material.AlbedoTexture.Width;
                _textureHeight = material.AlbedoTexture.Height;

                if (_textureId != IntPtr.Zero && _textureWidth > 0 && _textureHeight > 0)
                {
                    _loadedMaterial = material;
                    CalculateGrid();

                    Console.WriteLine($"[TileEditor] ✓ Material loaded successfully: {_materialPath}");
                    Console.WriteLine($"[TileEditor] Texture size: {_textureWidth}x{_textureHeight}");
                    Console.WriteLine($"[TileEditor] TextureId: {_textureId}");

                    CleanupTempObject();
                }
            }
            else if (_loadAttempts >= MAX_LOAD_ATTEMPTS)
            {
                Console.WriteLine($"[TileEditor] ✗ Failed to load material after {MAX_LOAD_ATTEMPTS} attempts");
                CleanupTempObject();
            }
        }

        private void CleanupTempObject()
        {
            if (_tempLoaderObject != null)
            {
                var scene = SceneManager.ActiveScene;
                scene?.DestroyGameObject(_tempLoaderObject);
                _tempLoaderObject = null;
            }
            _loadAttempts = 0;
        }

        private void CalculateGrid()
        {
            if (_textureWidth > 0 && _textureHeight > 0 && _tileWidth > 0 && _tileHeight > 0)
            {
                _tilesPerRow = _textureWidth / _tileWidth;
                _tilesPerColumn = _textureHeight / _tileHeight;
            }
            else
            {
                _tilesPerRow = 0;
                _tilesPerColumn = 0;
            }
        }

        private (float u0, float v0, float u1, float v1) GetTileUV(int tx, int ty)
        {
            float u0 = (tx * _tileWidth) / (float)_textureWidth;
            float v0 = ((ty + 1) * _tileHeight) / (float)_textureHeight;
            float u1 = ((tx + 1) * _tileWidth) / (float)_textureWidth;
            float v1 = (ty * _tileHeight) / (float)_textureHeight;
            return (u0, v0, u1, v1);
        }

        private void ClearMaterial()
        {
            _loadedMaterial = null;
            _textureId = IntPtr.Zero;
            _textureWidth = 0;
            _textureHeight = 0;
            _tilesPerRow = 0;
            _tilesPerColumn = 0;
            _selectedTileX = -1;
            _selectedTileY = -1;
        }
    }
}