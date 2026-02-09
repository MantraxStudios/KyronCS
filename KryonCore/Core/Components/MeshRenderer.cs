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
        private string _modelPath = "/models/Cube.fbx";

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
            // NO borrar _materials aquí, solo inicializar si es necesario
            
            if (!string.IsNullOrEmpty(ModelPath))
            {
                LoadModelFromPath(ModelPath);
                
                if (_model != null && _model.SubMeshCount > 0)
                {
                    // Solo crear el array si no existe o tiene tamaño incorrecto
                    if (_materials == null || _materials.Length != _model.SubMeshCount)
                    {
                        var oldMaterials = _materials ?? new Material[0];
                        _materials = new Material[_model.SubMeshCount];
                        
                        // Copiar materiales existentes si los hay
                        for (int i = 0; i < Math.Min(oldMaterials.Length, _materials.Length); i++)
                        {
                            _materials[i] = oldMaterials[i];
                        }
                    }
                }
            }
            else
            {
                // Si no hay modelo, asegurar que existe el array
                if (_materials == null)
                {
                    _materials = new Material[0];
                }
            }

            // Cargar materiales desde MaterialPaths solo si no están ya asignados
            for (int i = 0; i < MaterialPaths.Length && i < _materials.Length; i++)
            {
                // Solo cargar si el slot está vacío
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
                    
                    LoadModelFromPath(ModelPath);
                    
                    if (_model != null && _model.SubMeshCount > 0)
                    {
                        _materials = new Material[_model.SubMeshCount];
                    }
                }
                else
                {
                    _model = null;
                    _materials = new Material[0];
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
            }
            else
            {
                _materials = new Material[0];
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
                _model = Model.Load(path);
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
        }

        public void ClearMaterials()
        {
            _materials = new Material[0];
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
                }
            }

            GraphicsEngine.Instance.Materials.SaveMaterialsData();
        }
    }
}