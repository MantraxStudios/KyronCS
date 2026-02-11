using KrayonCore;
using KrayonCore.Core.Attributes;
using KrayonCore.Utilities;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.IO;

namespace KrayonCore
{
    public class MeshRenderer : Component
    {
        private string _modelPath = "3d361d14-7340-48cd-b35c-0968515455a0";

        [ToStorage]
        public string ModelPath
        {
            get => _modelPath;
            set
            {
                if (_modelPath != value)
                {
                    _modelPath = value;
                    OnModelPathChanged();
                }
            }
        }

        [ToStorage, NoSerializeToInspector] public string[] MaterialPaths { get; set; } = new string[0];

        [NoSerializeToInspector] private Material[] _materials = new Material[0];
        [NoSerializeToInspector] private Model? _model;
        [NoSerializeToInspector] private bool _isInitialized = false;

        [NoSerializeToInspector] public Model? Model
        {
            get => _model;
            set => _model = value;
        }

        public Material[] Materials
        {
            get => _materials;
            set => _materials = value ?? new Material[0];
        }

        [NoSerializeToInspector] public int MaterialCount => _materials.Length;

        public MeshRenderer()
        {
            _materials = new Material[0];
        }

        public override void Awake()
        {
            foreach (var item in MaterialPaths)
            {
                Console.WriteLine($"Material: {item} | Object: {GameObject.Name}");
            }
            
            if (!string.IsNullOrEmpty(ModelPath))
            {
                LoadModelFromPath(ModelPath);
                
                if (_model != null && _model.SubMeshCount > 0)
                {
                    if (_materials == null || _materials.Length != _model.SubMeshCount)
                    {
                        var oldMaterials = _materials ?? new Material[0];
                        _materials = new Material[_model.SubMeshCount];
                        
                        for (int i = 0; i < Math.Min(oldMaterials.Length, _materials.Length); i++)
                        {
                            _materials[i] = oldMaterials[i];
                        }
                    }
                }
            }
            else
            {
                if (_materials == null)
                {
                    _materials = new Material[0];
                }
            }

            for (int i = 0; i < MaterialPaths.Length && i < _materials.Length; i++)
            {
                if (_materials[i] == null && !string.IsNullOrEmpty(MaterialPaths[i]))
                {
                    var material = GraphicsEngine.Instance.Materials.Get(MaterialPaths[i]);
                    if (material != null)
                    {
                        _materials[i] = material;
                    }
                    else
                    {
                        Console.WriteLine($"No se pudo cargar el material '{MaterialPaths[i]}' para el índice {i}");
                    }
                }
            }

            _isInitialized = true;
        }

        public override void Start()
        {
            if (_model == null && !string.IsNullOrEmpty(ModelPath))
            {
                LoadModelFromPath(ModelPath);
                
                if (_model != null && _materials.Length != _model.SubMeshCount)
                {
                    Array.Resize(ref _materials, _model.SubMeshCount);
                }
            }
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);
        }

        private void OnModelPathChanged()
        {
            if (_isInitialized)
            {
                if (!string.IsNullOrEmpty(ModelPath))
                {
                    _materials = new Material[0];
                    MaterialPaths = new string[0];

                    LoadModelFromPath(ModelPath);

                    if (_model != null && _model.SubMeshCount > 0)
                    {
                        _materials = new Material[_model.SubMeshCount];
                        MaterialPaths = new string[_model.SubMeshCount];
                    }
                }
                else
                {
                    _model = null;
                    _materials = new Material[0];
                    MaterialPaths = new string[0];
                }
            }
        }

        public void SetModel(string path)
        {
            ModelPath = path;
        }

        public void SetModelDirect(Model model)
        {
            _model = model;
            _modelPath = "";

            if (model != null && model.SubMeshCount > 0)
            {
                _materials = new Material[model.SubMeshCount];
                MaterialPaths = new string[model.SubMeshCount];
            }
            else
            {
                _materials = new Material[0];
                MaterialPaths = new string[0];
            }
        }

        protected virtual void LoadModelFromPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            try
            {
                _model = Model.Load(AssetManager.Get(Guid.Parse(path)).Path);
            }
            catch (Exception ex)
            {
                _model = null;
            }
        }

        public void SaveMaterialPaths(string[] paths)
        {
            MaterialPaths = paths ?? new string[0];
        }

        public void SetMaterial(int index, Material material)
        {
            if (index < 0) return;

            if (index >= _materials.Length)
            {
                int newSize = index + 1;
                Array.Resize(ref _materials, newSize);
            }

            _materials[index] = material;

            // Actualizar MaterialPaths para que se guarde correctamente
            if (index >= MaterialPaths.Length)
            {
                var newPaths = new string[index + 1];
                Array.Copy(MaterialPaths, newPaths, MaterialPaths.Length);
                MaterialPaths = newPaths;
            }

            MaterialPaths[index] = material?.Name ?? "";
        }

        public Material GetMaterial(int index)
        {
            if (index < 0 || index >= _materials.Length)
                return null;
            return _materials[index];
        }

        public void AddMaterial(Material material)
        {
            if (material == null)
                return;

            Array.Resize(ref _materials, _materials.Length + 1);
            _materials[_materials.Length - 1] = material;

            // Sincronizar MaterialPaths
            var newPaths = new string[_materials.Length];
            Array.Copy(MaterialPaths, newPaths, Math.Min(MaterialPaths.Length, newPaths.Length));
            newPaths[newPaths.Length - 1] = material?.Name ?? "";
            MaterialPaths = newPaths;
        }

        public void RemoveMaterial(int index)
        {
            if (index < 0 || index >= _materials.Length)
                return;

            var newMaterials = new Material[_materials.Length - 1];
            for (int i = 0, j = 0; i < _materials.Length; i++)
            {
                if (i != index)
                {
                    newMaterials[j++] = _materials[i];
                }
            }
            _materials = newMaterials;

            // Sincronizar MaterialPaths
            if (index < MaterialPaths.Length)
            {
                var newPaths = new string[Math.Max(0, _materials.Length)];
                for (int i = 0, j = 0; i < MaterialPaths.Length && i < _materials.Length + 1; i++)
                {
                    if (i != index)
                    {
                        if (j < newPaths.Length)
                        {
                            newPaths[j++] = MaterialPaths[i];
                        }
                    }
                }
                MaterialPaths = newPaths;
            }
        }

        public void ClearMaterials()
        {
            _materials = new Material[0];
            MaterialPaths = new string[0];
        }

        public override void OnDestroy()
        {
            _model = null;
            _materials = new Material[0];
        }

        [CallEvent("Setup Materials")]
        public void SetupAutomaticMaterials()
        {
            if (_model == null)
            {
                return;
            }

            _materials = new Material[Model.SubMeshCount];
            MaterialPaths = new string[Model.SubMeshCount];

            for (int i = 0; i < Model.SubMeshCount; i++)
            {
                SubMeshInfo meshInfo = Model._subMeshes[i];

                string textureFileName = PathUtils.GetFileNameWithExtension(
                    meshInfo.TextureInfo.DiffuseTexture
                );

                string texturePath = PathUtils.FindFileByName(
                    AssetManager.BasePath,
                    textureFileName
                );

                if (!string.IsNullOrEmpty(texturePath))
                {
                    Material G = GraphicsEngine.Instance.Materials.Create(PathUtils.GetFileNameWithoutExtension(texturePath), "shaders/basic");
                    G.LoadAlbedoTexture(PathUtils.GetPathAfterContent(texturePath));
                    G.Roughness = 0;
                    _materials[i] = G;
                    MaterialPaths[i] = G.Name;
                }
            }

            GraphicsEngine.Instance.Materials.SaveMaterialsData();
        }
    }
}