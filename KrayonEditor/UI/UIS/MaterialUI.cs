using ImGuiNET;
using KrayonCore;
using KrayonCore.Core.Attributes;
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

        public override void OnDrawUI()
        {
            ImGui.Begin("Material Editor");

            DrawMaterialList();
            ImGui.Separator();

            if (_selectedMaterial != null)
            {
                DrawMaterialEditor();
            }
            else
            {
                ImGui.TextDisabled("No material selected");
            }

            ImGui.End();

            if (_showFileDialog)
            {
                DrawFileDialog();
            }

            if (_showCreateDialog)
            {
                DrawCreateMaterialDialog();
            }
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

            ImGui.BeginChild("MaterialList", new Vector2(0, 200));

            var materials = GraphicsEngine.Instance!.Materials.GetAll().ToList();
            var filtered = string.IsNullOrWhiteSpace(_searchFilter)
                ? materials
                : materials.Where(m => m.Name.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase)).ToList();

            foreach (var mat in filtered)
            {
                bool isSelected = _selectedMaterial == mat;
                if (ImGui.Selectable(mat.Name, isSelected))
                {
                    _selectedMaterial = mat;
                }
            }

            ImGui.EndChild();
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
                _currentPath = AssetManager.BasePath;
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
                var newMaterial = GraphicsEngine.Instance!.Materials.Create(_newMaterialName, _selectedShaderPath);
                _selectedMaterial = newMaterial;
                Console.WriteLine($"Material created: {_newMaterialName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating material: {ex.Message}");
            }
        }

        private void DrawMaterialEditor()
        {
            ImGui.BeginChild("MaterialEditor");

            ImGui.Text($"Editing: {_selectedMaterial.Name}");
            ImGui.Separator();

            if (ImGui.CollapsingHeader("PBR Properties", ref _showPBRSection))
            {
                DrawPBRProperties();
            }

            if (ImGui.CollapsingHeader("Textures", ref _showTexturesSection))
            {
                DrawTexturesSection();
            }

            if (ImGui.CollapsingHeader("Advanced", ref _showAdvancedSection))
            {
                DrawAdvancedSection();
            }

            ImGui.EndChild();
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
            ImGui.Text("Albedo Map");
            DrawTextureSlot(_selectedMaterial.AlbedoTexture, "AlbedoTexture",
                () => _selectedMaterial.RemoveAlbedoTexture());
            if (ImGui.Button("Load Albedo Texture##LoadAlbedo"))
            {
                OpenFileDialog("Albedo");
            }

            ImGui.Spacing();

            ImGui.Text("Normal Map");
            DrawTextureSlot(_selectedMaterial.NormalTexture, "NormalTexture",
                () => _selectedMaterial.RemoveNormalTexture());
            if (ImGui.Button("Load Normal Texture##LoadNormal"))
            {
                OpenFileDialog("Normal");
            }

            if (_selectedMaterial.UseNormalMap)
            {
                float intensity = _selectedMaterial.NormalMapIntensity;
                if (ImGui.SliderFloat("Normal Intensity", ref intensity, 0.0f, 2.0f))
                {
                    _selectedMaterial.SetNormalMapIntensity(intensity);
                }
            }

            ImGui.Spacing();

            ImGui.Text("Metallic Map");
            DrawTextureSlot(_selectedMaterial.MetallicTexture, "MetallicTexture",
                () => _selectedMaterial.RemoveMetallicTexture());
            if (ImGui.Button("Load Metallic Texture##LoadMetallic"))
            {
                OpenFileDialog("Metallic");
            }

            ImGui.Spacing();

            ImGui.Text("Roughness Map");
            DrawTextureSlot(_selectedMaterial.RoughnessTexture, "RoughnessTexture",
                () => _selectedMaterial.RemoveRoughnessTexture());
            if (ImGui.Button("Load Roughness Texture##LoadRoughness"))
            {
                OpenFileDialog("Roughness");
            }

            ImGui.Spacing();

            ImGui.Text("Ambient Occlusion Map");
            DrawTextureSlot(_selectedMaterial.AOTexture, "AOTexture",
                () => _selectedMaterial.RemoveAOTexture());
            if (ImGui.Button("Load AO Texture##LoadAO"))
            {
                OpenFileDialog("AO");
            }

            ImGui.Spacing();

            ImGui.Text("Emissive Map");
            DrawTextureSlot(_selectedMaterial.EmissiveTexture, "EmissiveTexture",
                () => _selectedMaterial.RemoveEmissiveTexture());
            if (ImGui.Button("Load Emissive Texture##LoadEmissive"))
            {
                OpenFileDialog("Emissive");
            }

            ImGui.Spacing();

            if (ImGui.Button("Remove All Textures"))
            {
                _selectedMaterial.RemoveAllPBRTextures();
            }
        }

        private void DrawTextureSlot(TextureLoader texture, string label, Action onRemove)
        {
            if (texture != null)
            {
                ImGui.Text($"  Path: {texture.GetTexturePath}");
                ImGui.SameLine();
                if (ImGui.SmallButton($"Remove##{label}"))
                {
                    onRemove?.Invoke();
                }
            }
            else
            {
                ImGui.TextDisabled("  No texture loaded");
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

            if (ImGui.Button("Apply PBR Properties"))
            {
                _selectedMaterial.SetPBRProperties();
            }
        }

        private void OpenFileDialog(string target)
        {
            _fileDialogTarget = target;
            _showFileDialog = true;
            _currentPath = AssetManager.BasePath;
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

            ImGui.Text($"Current Path: {_currentPath}");
            ImGui.Separator();

            if (ImGui.Button("Parent Directory"))
            {
                var parentPath = Directory.GetParent(_currentPath)?.FullName;

                if (parentPath != null && parentPath.StartsWith(AssetManager.BasePath))
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
                if (ImGui.Selectable(file, false))
                {
                    string fullPath = Path.Combine(_currentPath, file);
                    string relativePath = GetRelativePathFromBasePath(fullPath);

                    if (_fileDialogTarget == "Shader")
                    {
                        _selectedShaderPath = relativePath;
                        _showFileDialog = false;
                    }
                    else
                    {
                        LoadTextureForTarget(relativePath);
                        _showFileDialog = false;
                    }
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

        private string GetRelativePathFromBasePath(string fullPath)
        {
            if (fullPath.StartsWith(AssetManager.BasePath))
            {
                string relativePath = fullPath.Substring(AssetManager.BasePath.Length);
                relativePath = relativePath.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return relativePath.Replace('\\', '/');
            }

            return fullPath;
        }

        private void LoadTextureForTarget(string path)
        {
            if (_selectedMaterial == null) return;

            switch (_fileDialogTarget)
            {
                case "Albedo":
                    _selectedMaterial.LoadAlbedoTexture(path);
                    break;
                case "Normal":
                    _selectedMaterial.LoadNormalTexture(path);
                    break;
                case "Metallic":
                    _selectedMaterial.LoadMetallicTexture(path);
                    break;
                case "Roughness":
                    _selectedMaterial.LoadRoughnessTexture(path);
                    break;
                case "AO":
                    _selectedMaterial.LoadAOTexture(path);
                    break;
                case "Emissive":
                    _selectedMaterial.LoadEmissiveTexture(path);
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