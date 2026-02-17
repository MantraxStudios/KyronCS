using KrayonCore.Core.Attributes;
using KrayonCore.GraphicsData;
using KrayonCore.Utilities;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.IO;

namespace KrayonCore.Components.RenderComponents
{
    public class MeshRenderer : Component, IRenderable
    {
        [NoSerializeToInspector] public RenderableType RenderType => RenderableType.Mesh;

        private string _modelPath = "1b467368-606c-462c-af3f-a23a3d08d12c";

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

        [ToStorage]
        public string[] MaterialPaths { get; set; } = new string[0];

        [NoSerializeToInspector] private Model? _model;
        [NoSerializeToInspector] private bool _isInitialized = false;

        [NoSerializeToInspector]
        public Model? Model
        {
            get => _model;
            set => _model = value;
        }

        [NoSerializeToInspector]
        public Material[] Materials
        {
            get
            {
                var materials = new Material[MaterialPaths.Length];
                for (int i = 0; i < MaterialPaths.Length; i++)
                {
                    if (!string.IsNullOrEmpty(MaterialPaths[i]))
                    {
                        materials[i] = GraphicsEngine.Instance.Materials.Get(MaterialPaths[i]);
                    }
                }
                return materials;
            }
            set
            {
                if (value == null)
                {
                    MaterialPaths = new string[0];
                    return;
                }

                MaterialPaths = new string[value.Length];
                for (int i = 0; i < value.Length; i++)
                {
                    MaterialPaths[i] = value[i]?.Name ?? "";
                }
            }
        }

        [NoSerializeToInspector]
        public int MaterialCount => MaterialPaths.Length;

        public MeshRenderer()
        {
            MaterialPaths = new string[0];
        }

        public override void Awake()
        {
            Console.WriteLine($"*********MATERIAL EN ESTE OBJETO {MaterialPaths.Length}");
            GraphicsEngine.Instance?.GetSceneRenderer()?.RegisterRenderer(this);

            foreach (var item in MaterialPaths)
            {
                Console.WriteLine($"Material: {item} | Object: {GameObject.Name}");
            }

            if (!string.IsNullOrEmpty(ModelPath))
            {
                LoadModelFromPath(ModelPath);

                if (_model != null && _model.SubMeshCount > 0)
                {
                    if (MaterialPaths.Length != _model.SubMeshCount)
                    {
                        var oldPaths = MaterialPaths;
                        MaterialPaths = new string[_model.SubMeshCount];

                        for (int i = 0; i < Math.Min(oldPaths.Length, MaterialPaths.Length); i++)
                        {
                            MaterialPaths[i] = oldPaths[i];
                        }
                    }
                }
            }

            _isInitialized = true;
        }

        [CallEvent("Start Event")]
        public override void Start()
        {
            if (_model == null && !string.IsNullOrEmpty(ModelPath))
            {
                LoadModelFromPath(ModelPath);

                if (_model != null && MaterialPaths.Length != _model.SubMeshCount)
                {
                    var newPaths = new string[_model.SubMeshCount];
                    Array.Copy(MaterialPaths, newPaths, Math.Min(MaterialPaths.Length, newPaths.Length));
                    MaterialPaths = newPaths;
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
                    MaterialPaths = new string[0];

                    LoadModelFromPath(ModelPath);

                    if (_model != null && _model.SubMeshCount > 0)
                    {
                        MaterialPaths = new string[_model.SubMeshCount];
                    }
                }
                else
                {
                    _model = null;
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
                MaterialPaths = new string[model.SubMeshCount];
            }
            else
            {
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
                var assetInfo = AssetManager.GetBytes(Guid.Parse(path));

                Console.WriteLine("Bytes Procesados");
                _model = Model.LoadFromBytes(assetInfo, "fbx");
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

            if (index >= MaterialPaths.Length)
            {
                // CAMBIO: No usar Array.Resize con propiedad
                var newPaths = new string[index + 1];
                Array.Copy(MaterialPaths, newPaths, MaterialPaths.Length);
                MaterialPaths = newPaths;
            }

            MaterialPaths[index] = material?.Name ?? "";
        }

        public Material GetMaterial(int index)
        {
            if (index < 0 || index >= MaterialPaths.Length)
                return null;

            if (string.IsNullOrEmpty(MaterialPaths[index]))
                return null;

            return GraphicsEngine.Instance.Materials.Get(MaterialPaths[index]);
        }

        public void AddMaterial(Material material)
        {
            if (material == null)
                return;

            // CAMBIO: No usar Array.Resize con propiedad
            var newPaths = new string[MaterialPaths.Length + 1];
            Array.Copy(MaterialPaths, newPaths, MaterialPaths.Length);
            newPaths[newPaths.Length - 1] = material.Name;
            MaterialPaths = newPaths;
        }

        public void RemoveMaterial(int index)
        {
            if (index < 0 || index >= MaterialPaths.Length)
                return;

            var newPaths = new string[MaterialPaths.Length - 1];
            for (int i = 0, j = 0; i < MaterialPaths.Length; i++)
            {
                if (i != index)
                {
                    newPaths[j++] = MaterialPaths[i];
                }
            }
            MaterialPaths = newPaths;
        }

        public void ClearMaterials()
        {
            MaterialPaths = new string[0];
        }

        public override void OnDestroy()
        {
            GraphicsEngine.Instance?.GetSceneRenderer()?.UnregisterRenderer(this);
            _model = null;
            MaterialPaths = new string[0];
        }

        [CallEvent("Setup Materials")]
        public void SetupAutomaticMaterials()
        {
            if (_model == null)
            {
                return;
            }

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
                    string relativePath = PathUtils.GetPathAfterContent(texturePath);
                    var assetRecord = AssetManager.FindByPath(relativePath);

                    Console.WriteLine($"Relative Path Found: {assetRecord.Guid}");

                    if (assetRecord == null)
                    {
                        Console.WriteLine($"Texture not registered in AssetManager: {relativePath}");
                        continue;
                    }

                    Material G = GraphicsEngine.Instance.Materials.Create(
                        PathUtils.GetFileNameWithoutExtension(texturePath),
                        Guid.Parse("f3df852d-4e51-4e3c-ae64-81184e1b1182"),
                        Guid.Parse("94804744-32d4-4fa3-8aa0-d7f8f19fc3fb"));
                    G.LoadAlbedoTexture(assetRecord.Guid);
                    G.Roughness = 0;
                    MaterialPaths[i] = G.Name;
                }
            }
            GraphicsEngine.Instance.Materials.SaveMaterialsData();
        }
    }
}