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

        private Material[] _materials = new Material[0];
        private Model? _model;
        private bool _isInitialized = false;

        public Model? Model
        {
            get => _model;
            set => _model = value;
        }

        public Material[] Materials
        {
            get => _materials;
            set => _materials = value ?? new Material[0];
        }

        public int MaterialCount => _materials.Length;

        public MeshRenderer()
        {
            _materials = new Material[0];
        }

        public override void Awake()
        {
            Console.WriteLine($"[MeshRenderer] Awake llamado en {GameObject?.Name ?? "Unknown"}");

            // CRÍTICO: Limpiar materiales anteriores para evitar glitches al recargar
            _materials = new Material[0];

            if (!string.IsNullOrEmpty(ModelPath))
            {
                Console.WriteLine($"[MeshRenderer] Intentando cargar modelo desde: {ModelPath}");
                LoadModelFromPath(ModelPath);
                
                // Inicializar array de materiales al tamaño correcto basado en el modelo
                if (_model != null && _model.SubMeshCount > 0)
                {
                    _materials = new Material[_model.SubMeshCount];
                    Console.WriteLine($"[MeshRenderer] Array de materiales inicializado con {_materials.Length} slots");
                }
            }
            else
            {
                Console.WriteLine($"[MeshRenderer] No hay ModelPath especificado");
            }

            // Cargar materiales guardados (solo hasta el límite del array)
            for (int i = 0; i < MaterialPaths.Length && i < _materials.Length; i++)
            {
                var material = GraphicsEngine.Instance.Materials.Get(MaterialPaths[i]);
                if (material != null)
                {
                    _materials[i] = material;
                    Console.WriteLine($"[MeshRenderer] Material {i} cargado: {MaterialPaths[i]}");
                }
            }

            _isInitialized = true;
        }

        public override void Start()
        {
            if (_model == null && !string.IsNullOrEmpty(ModelPath))
            {
                Console.WriteLine($"[MeshRenderer] Warning: Modelo no cargado en Start. Reintentando carga de: {ModelPath}");
                LoadModelFromPath(ModelPath);
                
                // Asegurar que el array de materiales existe
                if (_model != null && _materials.Length != _model.SubMeshCount)
                {
                    Console.WriteLine($"[MeshRenderer] Corrigiendo tamaño de array de materiales: {_materials.Length} -> {_model.SubMeshCount}");
                    Array.Resize(ref _materials, _model.SubMeshCount);
                }
            }

            if (_model == null)
            {
                Console.WriteLine($"[MeshRenderer] Warning: No hay modelo asignado a {GameObject?.Name ?? "Unknown"}");
            }

            Console.WriteLine($"[MeshRenderer] Start completado - Modelo: {(_model != null ? "OK" : "NULL")}, Materiales: {_materials.Length}");
        }

        private void OnModelPathChanged()
        {
            // Solo recargar si ya está inicializado (después de Awake)
            if (_isInitialized)
            {
                Console.WriteLine($"[MeshRenderer] ModelPath cambió a: {ModelPath}");

                if (!string.IsNullOrEmpty(ModelPath))
                {
                    // Limpiar materiales antes de cargar nuevo modelo
                    _materials = new Material[0];
                    
                    LoadModelFromPath(ModelPath);
                    
                    // Redimensionar array de materiales
                    if (_model != null && _model.SubMeshCount > 0)
                    {
                        _materials = new Material[_model.SubMeshCount];
                        Console.WriteLine($"[MeshRenderer] Array de materiales redimensionado a {_materials.Length}");
                    }
                }
                else
                {
                    _model = null;
                    _materials = new Material[0];
                    Console.WriteLine($"[MeshRenderer] ModelPath vacío, modelo y materiales eliminados");
                }
            }
        }

        public void SetModel(string path)
        {
            ModelPath = path; // Esto ahora disparará automáticamente OnModelPathChanged
        }

        public void SetModelDirect(Model model)
        {
            _model = model;
            _modelPath = ""; // Usa el backing field para evitar disparar el evento
            
            // Ajustar array de materiales al nuevo modelo
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
                Console.WriteLine($"[MeshRenderer] LoadModelFromPath: ruta vacía o nula");
                return;
            }

            try
            {
                Console.WriteLine($"[MeshRenderer] Llamando a Model.Load para: {path}");

                _model = Model.Load(path);

                if (_model != null)
                {
                    Console.WriteLine($"[MeshRenderer] ✓ Modelo cargado exitosamente: {path}");
                    Console.WriteLine($"[MeshRenderer]   SubMeshes: {_model.SubMeshCount}");
                }
                else
                {
                    Console.WriteLine($"[MeshRenderer] ✗ Model.Load retornó null para: {path}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MeshRenderer] ✗ Error al cargar modelo '{path}':");
                Console.WriteLine($"[MeshRenderer]   Mensaje: {ex.Message}");
                Console.WriteLine($"[MeshRenderer]   Tipo: {ex.GetType().Name}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"[MeshRenderer]   Inner: {ex.InnerException.Message}");
                }
                _model = null;
            }
        }

        public void SaveMaterialPaths(string[] paths)
        {
            MaterialPaths = paths ?? new string[0];
        }

        public void SetMaterial(int index, Material material)
        {
            Console.WriteLine($"[MeshRenderer] SetMaterial llamado - Index: {index}, Material: {material?.Name ?? "null"}");

            if (index < 0) return;

            // Asegurar que el array tenga el tamaño necesario
            if (index >= _materials.Length)
            {
                int newSize = index + 1;
                Console.WriteLine($"[MeshRenderer] Redimensionando array de materiales: {_materials.Length} -> {newSize}");
                Array.Resize(ref _materials, newSize);
            }

            _materials[index] = material;
            Console.WriteLine($"[MeshRenderer] Material asignado en índice {index}");
        }

        public Material GetMaterial(int index)
        {
            if (index < 0 || index >= _materials.Length)
                return null;
            return _materials[index];
        }

        public void AddMaterial(Material material)
        {
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
            Console.WriteLine($"[MeshRenderer] Materiales limpiados");
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
                Console.WriteLine($"[MeshRenderer] No se puede configurar materiales automáticos: modelo es null");
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