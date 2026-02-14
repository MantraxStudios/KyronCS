using ImGuiNET;
using KrayonCore;
using KrayonCore.Core.Attributes;
using KrayonCore.GraphicsData;
using KrayonCore.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace KrayonEditor.UI
{
    public class MaterialUI : UIBehaviour
    {
        private Material _selectedMaterial;
        private string _searchFilter = "";
        private bool _showPBRSection = true;
        private bool _showTexturesSection = true;
        private bool _showAdvancedSection = false;

        private bool _showFileDialog = false;
        private string _fileDialogTarget = "";
        private string _currentPath = "";
        private string _pendingPathChange = "";
        private List<string> _currentFiles = new();
        private List<string> _currentDirectories = new();

        private bool _showCreateDialog = false;
        private string _newMaterialName = "";
        private string _selectedShaderPath = "";

        private bool _showChangeShaderDialog = false;
        private string _newShaderPath = "";

        private const string PROTECTED_MATERIAL_NAME = "basic";

        private Dictionary<string, TextureLoader> _previewTextureCache = new();
        private Dictionary<string, int> _previewLoadAttempts = new();
        private string _hoveredFile = "";
        private float _hoverTime = 0f;
        private const float HOVER_DELAY = 0.3f;
        private const int MAX_LOAD_ATTEMPTS = 60;

        private string AbsoluteBasePath => Path.GetFullPath(AssetManager.BasePath);

        public override void OnDrawUI()
        {
            ImGui.Begin("Material Editor", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);

            var windowSize = ImGui.GetContentRegionAvail();
            float leftPanelWidth = 300;

            ImGui.BeginChild("LeftPanel", new Vector2(leftPanelWidth, windowSize.Y));
            DrawMaterialList();
            ImGui.EndChild();

            ImGui.SameLine();

            ImGui.BeginChild("RightPanel", new Vector2(windowSize.X - leftPanelWidth - 10, windowSize.Y));
            if (_selectedMaterial != null)
            {
                DrawMaterialEditor();
            }
            else
            {
                ImGui.TextDisabled("No material selected");
            }
            ImGui.EndChild();

            ImGui.End();

            if (_showFileDialog)
            {
                DrawFileDialog();
            }
            else
            {
                ClearPreviewCache();
            }

            if (_showCreateDialog)
            {
                DrawCreateMaterialDialog();
            }

            if (_showChangeShaderDialog)
            {
                DrawChangeShaderDialog();
            }
        }

        private void ClearPreviewCache()
        {
            foreach (var kvp in _previewTextureCache)
            {
                kvp.Value?.Dispose();
            }
            _previewTextureCache.Clear();
            _previewLoadAttempts.Clear();
            _hoveredFile = "";
            _hoverTime = 0f;
        }

        private void DrawMaterialList()
        {
            ImGui.Text("Materials");
            ImGui.InputText("Search", ref _searchFilter, 256);

            if (ImGui.Button("Create New Material", new Vector2(-1, 0)))
            {
                _showCreateDialog = true;
                _newMaterialName = "NewMaterial";
                _selectedShaderPath = "";
            }

            ImGui.Separator();

            ImGui.BeginChild("MaterialList", new Vector2(0, -70));

            var materials = GraphicsEngine.Instance!.Materials.GetAll().ToList();

            var filtered = materials
                .Where(m => !string.Equals(m.Name, PROTECTED_MATERIAL_NAME, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!string.IsNullOrWhiteSpace(_searchFilter))
            {
                filtered = filtered
                    .Where(m => m.Name.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            foreach (var mat in filtered)
            {
                bool isSelected = _selectedMaterial == mat;
                if (ImGui.Selectable(mat.Name, isSelected))
                {
                    _selectedMaterial = mat;
                }
            }

            ImGui.EndChild();

            if (_selectedMaterial != null && !IsProtectedMaterial(_selectedMaterial))
            {
                ImGui.Separator();

                if (ImGui.Button("Change Shader", new Vector2(-1, 0)))
                {
                    _showChangeShaderDialog = true;
                    _newShaderPath = "";
                }

                if (ImGui.Button("Delete Selected Material", new Vector2(-1, 0)))
                {
                    DeleteSelectedMaterial();
                }
            }
        }

        private bool IsProtectedMaterial(Material material)
        {
            return string.Equals(material.Name, PROTECTED_MATERIAL_NAME, StringComparison.OrdinalIgnoreCase);
        }

        private void DeleteSelectedMaterial()
        {
            if (_selectedMaterial == null)
                return;

            if (IsProtectedMaterial(_selectedMaterial))
            {
                Console.WriteLine($"Cannot delete protected material '{PROTECTED_MATERIAL_NAME}'");
                return;
            }

            string materialName = _selectedMaterial.Name;
            GraphicsEngine.Instance!.Materials.Remove(materialName);
            _selectedMaterial = null;

            Console.WriteLine($"Material '{materialName}' deleted successfully");
        }

        private void DrawChangeShaderDialog()
        {
            if (_selectedMaterial != null && IsProtectedMaterial(_selectedMaterial))
            {
                _showChangeShaderDialog = false;
                return;
            }

            ImGui.SetNextWindowSize(new Vector2(400, 150), ImGuiCond.FirstUseEver);
            ImGui.Begin("Change Shader", ref _showChangeShaderDialog);

            ImGui.Text($"Change shader for: {_selectedMaterial?.Name}");
            ImGui.Spacing();

            ImGui.Text("New Shader Path:");
            ImGui.InputText("##NewShaderPath", ref _newShaderPath, 512);
            ImGui.SameLine();
            if (ImGui.Button("Browse..."))
            {
                _fileDialogTarget = "Shader";
                _showFileDialog = true;
                _currentPath = AbsoluteBasePath;
                _pendingPathChange = "";
                RefreshFileList();
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            bool canChange = !string.IsNullOrWhiteSpace(_newShaderPath);

            if (!canChange)
            {
                ImGui.BeginDisabled();
            }

            if (ImGui.Button("Change", new Vector2(190, 0)))
            {
                ChangeShader();
                _showChangeShaderDialog = false;
            }

            if (!canChange)
            {
                ImGui.EndDisabled();
            }

            ImGui.SameLine();

            if (ImGui.Button("Cancel", new Vector2(190, 0)))
            {
                _showChangeShaderDialog = false;
            }

            ImGui.End();
        }

        private void ChangeShader()
        {
            if (_selectedMaterial == null || string.IsNullOrWhiteSpace(_newShaderPath))
                return;

            if (IsProtectedMaterial(_selectedMaterial))
            {
                Console.WriteLine($"Cannot change shader for protected material '{PROTECTED_MATERIAL_NAME}'");
                return;
            }

            try
            {
                string materialName = _selectedMaterial.Name;

                var albedoColor = _selectedMaterial.AlbedoColor;
                var metallic = _selectedMaterial.Metallic;
                var roughness = _selectedMaterial.Roughness;
                var ao = _selectedMaterial.AO;
                var emissiveColor = _selectedMaterial.EmissiveColor;

                var albedoGuid = _selectedMaterial.AlbedoTextureGUID;
                var normalGuid = _selectedMaterial.NormalTextureGUID;
                var metallicGuid = _selectedMaterial.MetallicTextureGUID;
                var roughnessGuid = _selectedMaterial.RoughnessTextureGUID;
                var aoGuid = _selectedMaterial.AOTextureGUID;
                var emissiveGuid = _selectedMaterial.EmissiveTextureGUID;

                GraphicsEngine.Instance!.Materials.Remove(materialName);

                var newMaterial = GraphicsEngine.Instance!.Materials.Create(materialName, 
                                                                            AssetManager.FindFolderByPath(_newShaderPath + ".vert").Guid, 
                                                                            AssetManager.FindFolderByPath(_newShaderPath + ".frag").Guid);

                if (newMaterial != null)
                {
                    newMaterial.AlbedoColor = albedoColor;
                    newMaterial.Metallic = metallic;
                    newMaterial.Roughness = roughness;
                    newMaterial.AO = ao;
                    newMaterial.EmissiveColor = emissiveColor;

                    if (albedoGuid != Guid.Empty) newMaterial.LoadAlbedoTexture(albedoGuid);
                    if (normalGuid != Guid.Empty) newMaterial.LoadNormalTexture(normalGuid);
                    if (metallicGuid != Guid.Empty) newMaterial.LoadMetallicTexture(metallicGuid);
                    if (roughnessGuid != Guid.Empty) newMaterial.LoadRoughnessTexture(roughnessGuid);
                    if (aoGuid != Guid.Empty) newMaterial.LoadAOTexture(aoGuid);
                    if (emissiveGuid != Guid.Empty) newMaterial.LoadEmissiveTexture(emissiveGuid);

                    newMaterial.SetPBRProperties();

                    _selectedMaterial = newMaterial;
                    Console.WriteLine($"Shader changed successfully for material '{materialName}'");
                }
                else
                {
                    Console.WriteLine($"Failed to change shader for material '{materialName}'");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error changing shader: {ex.Message}");
            }
        }

        private void DrawCreateMaterialDialog()
        {
            ImGui.SetNextWindowSize(new Vector2(400, 200), ImGuiCond.FirstUseEver);
            ImGui.Begin("Create New Material", ref _showCreateDialog);

            ImGui.Text("Material Name:");
            ImGui.InputText("##MaterialName", ref _newMaterialName, 256);

            ImGui.Spacing();

            ImGui.Text("Shader Path:");
            ImGui.InputText("##ShaderPath", ref _selectedShaderPath, 512);
            ImGui.SameLine();
            if (ImGui.Button("Browse..."))
            {
                _fileDialogTarget = "Shader";
                _showFileDialog = true;
                _currentPath = AbsoluteBasePath;
                _pendingPathChange = "";
                RefreshFileList();
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            bool canCreate = !string.IsNullOrWhiteSpace(_newMaterialName) &&
                           !string.IsNullOrWhiteSpace(_selectedShaderPath);

            if (!canCreate)
            {
                ImGui.BeginDisabled();
            }

            if (ImGui.Button("Create", new Vector2(190, 0)))
            {
                CreateNewMaterial();
                _showCreateDialog = false;
            }

            if (!canCreate)
            {
                ImGui.EndDisabled();
            }

            ImGui.SameLine();

            if (ImGui.Button("Cancel", new Vector2(190, 0)))
            {
                _showCreateDialog = false;
            }

            ImGui.End();
        }

        private void CreateNewMaterial()
        {
            try
            {
                var existingMaterial = GraphicsEngine.Instance!.Materials.GetAll()
                    .FirstOrDefault(m => string.Equals(m.Name, _newMaterialName, StringComparison.OrdinalIgnoreCase));

                if (existingMaterial != null)
                {
                    Console.WriteLine($"Material with name '{_newMaterialName}' already exists. Please choose a different name.");
                    return;
                }

                var vertShaderAsset = AssetManager.FindByPath(_selectedShaderPath + ".vert");
                var fragShaderAsset = AssetManager.FindByPath(_selectedShaderPath + ".frag");

                if (vertShaderAsset == null)
                {
                    Console.WriteLine($"Vertex shader not found: {_selectedShaderPath}.vert");
                    return;
                }

                if (fragShaderAsset == null)
                {
                    Console.WriteLine($"Fragment shader not found: {_selectedShaderPath}.frag");
                    return;
                }

                Console.WriteLine($"Creating material with shaders: {vertShaderAsset.Path} ({vertShaderAsset.Guid}) and {fragShaderAsset.Path} ({fragShaderAsset.Guid})");

                var newMaterial = GraphicsEngine.Instance!.Materials.Create(
                    _newMaterialName,
                    vertShaderAsset.Guid,
                    fragShaderAsset.Guid
                );

                if (newMaterial != null)
                {
                    _selectedMaterial = newMaterial;
                    Console.WriteLine($"Material created successfully: {_newMaterialName}");
                }
                else
                {
                    Console.WriteLine($"Failed to create material: {_newMaterialName}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating material: {ex.Message}");
            }
        }

        private void DrawMaterialEditor()
        {
            ImGui.Text($"Editing: {_selectedMaterial.Name}");
            ImGui.Separator();

            if (_selectedMaterial.VertexShaderGUID != Guid.Empty)
            {
                var vertAsset = AssetManager.Get(_selectedMaterial.VertexShaderGUID);
                var fragAsset = AssetManager.Get(_selectedMaterial.FragmentShaderGUID);
                ImGui.TextDisabled($"Vertex: {vertAsset?.Path ?? _selectedMaterial.VertexShaderGUID.ToString()}");
                ImGui.TextDisabled($"Fragment: {fragAsset?.Path ?? _selectedMaterial.FragmentShaderGUID.ToString()}");
            }
            ImGui.Separator();

            if (ImGui.CollapsingHeader("PBR Properties", ImGuiTreeNodeFlags.DefaultOpen))
            {
                DrawPBRProperties();
            }

            if (ImGui.CollapsingHeader("Textures", ImGuiTreeNodeFlags.DefaultOpen))
            {
                DrawTexturesSection();
            }

            if (ImGui.CollapsingHeader("Advanced"))
            {
                DrawAdvancedSection();
            }
        }

        private void DrawPBRProperties()
        {
            var albedo = ToNumericsVector3(_selectedMaterial.AlbedoColor);
            if (ImGui.ColorEdit3("Albedo Color", ref albedo))
            {
                _selectedMaterial.SetAlbedo(ToOpenTKVector3(albedo));
            }

            float metallic = _selectedMaterial.Metallic;
            if (ImGui.SliderFloat("Metallic", ref metallic, 0.0f, 1.0f))
            {
                _selectedMaterial.SetMetallic(metallic);
            }

            float roughness = _selectedMaterial.Roughness;
            if (ImGui.SliderFloat("Roughness", ref roughness, 0.0f, 1.0f))
            {
                _selectedMaterial.SetRoughness(roughness);
            }

            float ao = _selectedMaterial.AO;
            if (ImGui.SliderFloat("Ambient Occlusion", ref ao, 0.0f, 1.0f))
            {
                _selectedMaterial.SetAO(ao);
            }

            var emissive = ToNumericsVector3(_selectedMaterial.EmissiveColor);
            if (ImGui.ColorEdit3("Emissive Color", ref emissive))
            {
                _selectedMaterial.SetEmissive(ToOpenTKVector3(emissive));
            }
        }

        private void DrawTexturesSection()
        {
            DrawTextureProperty("Albedo Map", _selectedMaterial.AlbedoTexture, _selectedMaterial.AlbedoTextureGUID, "AlbedoTexture",
                () => _selectedMaterial.RemoveAlbedoTexture(), () => OpenFileDialog("Albedo"));

            ImGui.Spacing();

            DrawTextureProperty("Normal Map", _selectedMaterial.NormalTexture, _selectedMaterial.NormalTextureGUID, "NormalTexture",
                () => _selectedMaterial.RemoveNormalTexture(), () => OpenFileDialog("Normal"));

            if (_selectedMaterial.UseNormalMap)
            {
                float intensity = _selectedMaterial.NormalMapIntensity;
                if (ImGui.SliderFloat("Normal Intensity", ref intensity, 0.0f, 2.0f))
                {
                    _selectedMaterial.SetNormalMapIntensity(intensity);
                }
            }

            ImGui.Spacing();

            DrawTextureProperty("Metallic Map", _selectedMaterial.MetallicTexture, _selectedMaterial.MetallicTextureGUID, "MetallicTexture",
                () => _selectedMaterial.RemoveMetallicTexture(), () => OpenFileDialog("Metallic"));

            ImGui.Spacing();

            DrawTextureProperty("Roughness Map", _selectedMaterial.RoughnessTexture, _selectedMaterial.RoughnessTextureGUID, "RoughnessTexture",
                () => _selectedMaterial.RemoveRoughnessTexture(), () => OpenFileDialog("Roughness"));

            ImGui.Spacing();

            DrawTextureProperty("Ambient Occlusion Map", _selectedMaterial.AOTexture, _selectedMaterial.AOTextureGUID, "AOTexture",
                () => _selectedMaterial.RemoveAOTexture(), () => OpenFileDialog("AO"));

            ImGui.Spacing();

            DrawTextureProperty("Emissive Map", _selectedMaterial.EmissiveTexture, _selectedMaterial.EmissiveTextureGUID, "EmissiveTexture",
                () => _selectedMaterial.RemoveEmissiveTexture(), () => OpenFileDialog("Emissive"));

            ImGui.Spacing();

            if (ImGui.Button("Remove All Textures", new Vector2(-1, 0)))
            {
                _selectedMaterial.RemoveAllPBRTextures();
            }
        }

        private void DrawTextureProperty(string label, TextureLoader texture, Guid textureGuid, string id, Action onRemove, Action onLoad)
        {
            ImGui.Text(label);

            if (texture != null)
            {
                ImGui.Indent();

                if (!texture.IsLoaded)
                {
                    texture.Load();
                }

                if (texture.IsLoaded)
                {
                    uint textureId = (uint)texture.TextureId;
                    Vector2 previewSize = new Vector2(128, 128);

                    var cursorPos = ImGui.GetCursorScreenPos();
                    var drawList = ImGui.GetWindowDrawList();

                    drawList.AddRectFilled(
                        cursorPos,
                        new Vector2(cursorPos.X + previewSize.X, cursorPos.Y + previewSize.Y),
                        ImGui.GetColorU32(new Vector4(0.2f, 0.2f, 0.2f, 1.0f))
                    );

                    ImGui.Image((IntPtr)textureId, previewSize, new Vector2(0, 1), new Vector2(1, 0));

                    ImGui.Text($"Size: {texture.Width}x{texture.Height}");

                    if (textureGuid != Guid.Empty)
                    {
                        var assetRecord = AssetManager.Get(textureGuid);
                        if (assetRecord != null)
                            ImGui.Text($"Asset: {assetRecord.Path}");
                        else
                            ImGui.TextDisabled($"GUID: {textureGuid}");
                    }
                }
                else
                {
                    ImGui.TextDisabled("Loading texture...");
                }

                if (ImGui.Button($"Remove##{id}", new Vector2(-1, 0)))
                {
                    onRemove?.Invoke();
                }

                ImGui.Unindent();
            }
            else
            {
                ImGui.Indent();
                ImGui.TextDisabled("No texture loaded");

                if (ImGui.Button($"Load Texture##{id}", new Vector2(-1, 0)))
                {
                    onLoad?.Invoke();
                }

                ImGui.Unindent();
            }
        }

        private void DrawAdvancedSection()
        {
            var ambient = ToNumericsVector3(_selectedMaterial.AmbientLight);
            if (ImGui.ColorEdit3("Ambient Light", ref ambient))
            {
                _selectedMaterial.SetAmbientLight(ToOpenTKVector3(ambient));
            }

            float ambientStrength = _selectedMaterial.AmbientStrength;
            if (ImGui.SliderFloat("Ambient Strength", ref ambientStrength, 0.0f, 2.0f))
            {
                _selectedMaterial.SetAmbientStrength(ambientStrength);
            }

            ImGui.Spacing();

            ImGui.Text("Texture Usage Flags:");
            bool useAlbedo = _selectedMaterial.UseAlbedoMap;
            if (ImGui.Checkbox("Use Albedo Map", ref useAlbedo))
                _selectedMaterial.UseAlbedoMap = useAlbedo;

            bool useNormal = _selectedMaterial.UseNormalMap;
            if (ImGui.Checkbox("Use Normal Map", ref useNormal))
                _selectedMaterial.UseNormalMap = useNormal;

            bool useMetallic = _selectedMaterial.UseMetallicMap;
            if (ImGui.Checkbox("Use Metallic Map", ref useMetallic))
                _selectedMaterial.UseMetallicMap = useMetallic;

            bool useRoughness = _selectedMaterial.UseRoughnessMap;
            if (ImGui.Checkbox("Use Roughness Map", ref useRoughness))
                _selectedMaterial.UseRoughnessMap = useRoughness;

            bool useAO = _selectedMaterial.UseAOMap;
            if (ImGui.Checkbox("Use AO Map", ref useAO))
                _selectedMaterial.UseAOMap = useAO;

            bool useEmissive = _selectedMaterial.UseEmissiveMap;
            if (ImGui.Checkbox("Use Emissive Map", ref useEmissive))
                _selectedMaterial.UseEmissiveMap = useEmissive;

            ImGui.Spacing();

            if (ImGui.Button("Apply PBR Properties", new Vector2(-1, 0)))
            {
                _selectedMaterial.SetPBRProperties();
            }
        }

        private void OpenFileDialog(string target)
        {
            _fileDialogTarget = target;
            _showFileDialog = true;
            _currentPath = AbsoluteBasePath;
            _pendingPathChange = "";
            RefreshFileList();
        }

        private void RefreshFileList()
        {
            _currentFiles.Clear();
            _currentDirectories.Clear();

            if (Directory.Exists(_currentPath))
            {
                try
                {
                    var directories = Directory.GetDirectories(_currentPath);
                    foreach (var dir in directories)
                    {
                        _currentDirectories.Add(Path.GetFileName(dir));
                    }

                    _currentDirectories.Sort(StringComparer.OrdinalIgnoreCase);

                    var allowedExtensions = _fileDialogTarget == "Shader"
                        ? new[] { ".vert", ".frag", ".glsl", ".shader" }
                        : new[] { ".png", ".jpg", ".jpeg", ".bmp", ".tga", ".dds" };

                    var files = Directory.GetFiles(_currentPath);

                    foreach (var file in files)
                    {
                        if (allowedExtensions.Contains(Path.GetExtension(file).ToLower()))
                        {
                            _currentFiles.Add(Path.GetFileName(file));
                        }
                    }

                    _currentFiles.Sort(StringComparer.OrdinalIgnoreCase);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading directory: {ex.Message}");
                }
            }
        }

        private void DrawFileDialog()
        {
            if (!string.IsNullOrEmpty(_pendingPathChange))
            {
                _currentPath = _pendingPathChange;
                _pendingPathChange = "";
                RefreshFileList();
            }

            ImGui.SetNextWindowSize(new Vector2(600, 400), ImGuiCond.FirstUseEver);
            ImGui.Begin($"Select {_fileDialogTarget} File", ref _showFileDialog);

            string displayPath = GetDisplayPath(_currentPath);
            ImGui.Text($"Current Path: {displayPath}");
            ImGui.Separator();

            if (ImGui.Button("Parent Directory"))
            {
                var parentPath = Directory.GetParent(_currentPath)?.FullName;

                if (parentPath != null && parentPath.Length >= AbsoluteBasePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Length)
                {
                    _pendingPathChange = parentPath;
                }
            }

            ImGui.Separator();

            ImGui.BeginChild("FileList", new Vector2(0, 300));

            var directoriesCopy = new List<string>(_currentDirectories);
            foreach (var dir in directoriesCopy)
            {
                if (ImGui.Selectable($"[DIR] {dir}", false))
                {
                    _pendingPathChange = Path.Combine(_currentPath, dir);
                }
            }

            var filesCopy = new List<string>(_currentFiles);
            foreach (var file in filesCopy)
            {
                bool isHovered = false;

                if (ImGui.Selectable(file, false))
                {
                    string fullPath = Path.Combine(_currentPath, file);
                    string relativePath = GetRelativePathFromBasePath(fullPath);

                    if (_fileDialogTarget == "Shader")
                    {
                        relativePath = RemoveShaderExtension(relativePath);

                        if (_showChangeShaderDialog)
                        {
                            _newShaderPath = relativePath;
                        }
                        else
                        {
                            _selectedShaderPath = relativePath;
                        }
                        _showFileDialog = false;
                    }
                    else
                    {
                        LoadTextureForTarget(relativePath);
                        _showFileDialog = false;
                    }
                }

                if (ImGui.IsItemHovered() && _fileDialogTarget != "Shader")
                {
                    isHovered = true;

                    if (_hoveredFile != file)
                    {
                        _hoveredFile = file;
                        _hoverTime = 0f;
                    }
                    else
                    {
                        _hoverTime += ImGui.GetIO().DeltaTime;
                    }

                    if (_hoverTime >= HOVER_DELAY)
                    {
                        DrawTexturePreview(file);
                    }
                }

                if (!isHovered && _hoveredFile == file)
                {
                    _hoveredFile = "";
                    _hoverTime = 0f;
                }
            }

            ImGui.EndChild();

            ImGui.Separator();

            if (ImGui.Button("Cancel"))
            {
                _showFileDialog = false;
            }

            ImGui.End();
        }

        private void DrawTexturePreview(string fileName)
        {
            string fullPath = Path.Combine(_currentPath, fileName);

            if (!_previewTextureCache.ContainsKey(fullPath))
            {
                try
                {
                    var texture = TextureLoader.FromAbsolutePath($"Preview_{fileName}", fullPath, false, true, TextureFilterMode.Bilinear);
                    _previewTextureCache[fullPath] = texture;
                    _previewLoadAttempts[fullPath] = 0;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[PREVIEW ERROR] Error creating TextureLoader: {ex.Message}");
                    return;
                }
            }

            var previewTexture = _previewTextureCache[fullPath];

            if (previewTexture == null)
                return;

            if (!_previewLoadAttempts.ContainsKey(fullPath))
            {
                _previewLoadAttempts[fullPath] = 0;
            }

            int attempts = _previewLoadAttempts[fullPath];

            if (!previewTexture.IsLoaded && attempts < MAX_LOAD_ATTEMPTS)
            {
                previewTexture.Load();
                _previewLoadAttempts[fullPath]++;

                if (!previewTexture.IsLoaded)
                {
                    ImGui.BeginTooltip();
                    ImGui.Text($"Loading preview: {fileName}");
                    ImGui.TextDisabled($"Please wait... ({attempts}/{MAX_LOAD_ATTEMPTS})");
                    ImGui.EndTooltip();
                    return;
                }
            }

            if (previewTexture.IsLoaded)
            {
                ImGui.BeginTooltip();

                ImGui.Text($"Preview: {fileName}");
                ImGui.Separator();

                float maxPreviewSize = 256f;
                float aspectRatio = (float)previewTexture.Width / (float)previewTexture.Height;

                Vector2 previewSize;
                if (aspectRatio > 1f)
                {
                    previewSize = new Vector2(maxPreviewSize, maxPreviewSize / aspectRatio);
                }
                else
                {
                    previewSize = new Vector2(maxPreviewSize * aspectRatio, maxPreviewSize);
                }

                var cursorPos = ImGui.GetCursorScreenPos();
                var drawList = ImGui.GetWindowDrawList();

                drawList.AddRectFilled(
                    cursorPos,
                    new Vector2(cursorPos.X + previewSize.X, cursorPos.Y + previewSize.Y),
                    ImGui.GetColorU32(new Vector4(0.15f, 0.15f, 0.15f, 1.0f))
                );

                uint textureId = (uint)previewTexture.TextureId;
                ImGui.Image((IntPtr)textureId, previewSize, new Vector2(0, 1), new Vector2(1, 0));

                ImGui.Text($"Size: {previewTexture.Width} x {previewTexture.Height}");

                ImGui.EndTooltip();
            }
            else if (attempts >= MAX_LOAD_ATTEMPTS)
            {
                ImGui.BeginTooltip();
                ImGui.Text($"Failed to load: {fileName}");
                ImGui.TextColored(new Vector4(1, 0, 0, 1), "Texture could not be loaded");
                ImGui.EndTooltip();
            }
        }

        private string RemoveShaderExtension(string path)
        {
            var extensions = new[] { ".vert", ".frag", ".glsl", ".shader" };

            foreach (var ext in extensions)
            {
                if (path.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                {
                    return path.Substring(0, path.Length - ext.Length);
                }
            }

            return path;
        }

        private string GetDisplayPath(string fullPath)
        {
            string basePath = AbsoluteBasePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string normalizedFull = Path.GetFullPath(fullPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (normalizedFull.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
            {
                string relative = normalizedFull.Substring(basePath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return string.IsNullOrEmpty(relative) ? "Content/" : $"Content/{relative.Replace('\\', '/')}";
            }

            return fullPath;
        }

        private string GetRelativePathFromBasePath(string fullPath)
        {
            string basePath = AbsoluteBasePath;
            string normalizedFull = Path.GetFullPath(fullPath);

            if (normalizedFull.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
            {
                string relativePath = normalizedFull.Substring(basePath.Length);
                relativePath = relativePath.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return relativePath.Replace('\\', '/');
            }

            return fullPath;
        }

        private void LoadTextureForTarget(string relativePath)
        {
            if (_selectedMaterial == null) return;

            var assetRecord = AssetManager.FindByPath(relativePath);
            if (assetRecord == null)
            {
                Console.WriteLine($"Texture not registered in AssetManager: {relativePath}");
                return;
            }

            Guid guid = assetRecord.Guid;

            switch (_fileDialogTarget)
            {
                case "Albedo":
                    _selectedMaterial.LoadAlbedoTexture(guid);
                    break;
                case "Normal":
                    _selectedMaterial.LoadNormalTexture(guid);
                    break;
                case "Metallic":
                    _selectedMaterial.LoadMetallicTexture(guid);
                    break;
                case "Roughness":
                    _selectedMaterial.LoadRoughnessTexture(guid);
                    break;
                case "AO":
                    _selectedMaterial.LoadAOTexture(guid);
                    break;
                case "Emissive":
                    _selectedMaterial.LoadEmissiveTexture(guid);
                    break;
            }

            _selectedMaterial.SetPBRProperties();
        }

        private Vector3 ToNumericsVector3(OpenTK.Mathematics.Vector3 vec)
        {
            return new Vector3(vec.X, vec.Y, vec.Z);
        }

        private OpenTK.Mathematics.Vector3 ToOpenTKVector3(Vector3 vec)
        {
            return new OpenTK.Mathematics.Vector3(vec.X, vec.Y, vec.Z);
        }
    }
}