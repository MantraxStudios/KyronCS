using KrayonCore;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.IO;

namespace KrayonCore
{
    public class MeshRenderer : Component
    {
        [ToStorage] public string ModelPath { get; set; } = "models/Cube.fbx";
        [ToStorage, NoSerializeToInspector] public string[] MaterialPaths { get; set; } = new string[0];

        private Material[] _materials = new Material[0];
        private Model? _model;

        // Instanced rendering support
        private Matrix4[] _instanceMatrices = null;
        private bool _instanceDataDirty = false;

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

        public int InstanceCount => _instanceMatrices?.Length ?? 0;

        public MeshRenderer()
        {
            _materials = new Material[0];
        }

        public override void Awake()
        {
            Console.WriteLine($"[MeshRenderer] Awake llamado en {GameObject?.Name ?? "Unknown"}");

            if (!string.IsNullOrEmpty(ModelPath))
            {
                Console.WriteLine($"[MeshRenderer] Intentando cargar modelo desde: {ModelPath}");
                LoadModelFromPath(ModelPath);
            }
            else
            {
                Console.WriteLine($"[MeshRenderer] No hay ModelPath especificado");
            }

            for (int i = 0; i < MaterialPaths.Count(); i++)
            {
                SetMaterial(i, GraphicsEngine.Instance.Materials.Get(MaterialPaths[i]));
            }
        }

        public override void Start()
        {
            if (_model == null && !string.IsNullOrEmpty(ModelPath))
            {
                Console.WriteLine($"[MeshRenderer] Warning: Modelo no cargado en Start. Reintentando carga de: {ModelPath}");
                LoadModelFromPath(ModelPath);
            }

            if (_model == null)
            {
                Console.WriteLine($"[MeshRenderer] Warning: No hay modelo asignado a {GameObject?.Name ?? "Unknown"}");
            }

            if (_materials.Length == 0 && MaterialPaths != null && MaterialPaths.Length > 0)
            {
                Console.WriteLine($"[MeshRenderer] Info: Hay {MaterialPaths.Length} rutas de materiales guardadas");
                Console.WriteLine($"[MeshRenderer] Los materiales deben ser asignados manualmente después de cargar la escena");
            }
        }

        public void SetModel(string path)
        {
            ModelPath = path;
            LoadModelFromPath(path);
        }

        public void SetModelDirect(Model model)
        {
            _model = model;
            ModelPath = "";
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
                    Console.WriteLine($"[MeshRenderer] Verifica que el archivo exista: {path}");
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
            if (index < 0) return;

            if (index >= _materials.Length)
            {
                Array.Resize(ref _materials, index + 1);
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

        public int MaterialCount => _materials.Length;

        // Instanced rendering methods
        public void SetInstanceMatrices(Matrix4[] matrices)
        {
            _instanceMatrices = matrices;
            _instanceDataDirty = true;
        }

        public void SetInstanceMatrices(List<Matrix4> matrices)
        {
            SetInstanceMatrices(matrices?.ToArray());
        }

        public void AddInstance(Matrix4 matrix)
        {
            if (_instanceMatrices == null)
            {
                _instanceMatrices = new Matrix4[] { matrix };
            }
            else
            {
                Array.Resize(ref _instanceMatrices, _instanceMatrices.Length + 1);
                _instanceMatrices[_instanceMatrices.Length - 1] = matrix;
            }

            _instanceDataDirty = true;
        }

        public void ClearInstances()
        {
            _instanceMatrices = null;
            _instanceDataDirty = false;
        }

        private void UpdateInstanceData()
        {
            if (!_instanceDataDirty || _model == null || _instanceMatrices == null)
                return;

            _model.SetupInstancing(_instanceMatrices);
            _instanceDataDirty = false;
        }

        public void Render(Matrix4 view, Matrix4 projection)
        {
            if (_model == null || !Enabled)
                return;

            if (_materials.Length == 0)
                return;

            Vector3 cameraPos = GraphicsEngine.Instance.GetSceneRenderer().GetCamera().Position;

            // Instanced rendering (cuando hay matrices de instancia)
            if (_instanceMatrices != null && _instanceMatrices.Length > 0)
            {
                UpdateInstanceData();

                var material = _materials[0];
                if (material != null)
                {
                    material.SetPBRProperties();
                    material.Use();

                    material.SetMatrix4("view", view);
                    material.SetMatrix4("projection", projection);
                    material.SetVector3("u_CameraPos", cameraPos);

                    _model.DrawInstanced(_instanceMatrices.Length);
                }
            }
            // Normal rendering (sin instancing)
            else
            {
                var transform = GetComponent<Transform>();
                if (transform == null)
                    return;

                Matrix4 model = transform.GetWorldMatrix();

                if (_materials.Length == 1)
                {
                    var material = _materials[0];
                    if (material != null)
                    {
                        material.SetPBRProperties();
                        material.Use();

                        material.SetMatrix4("model", model);
                        material.SetMatrix4("view", view);
                        material.SetMatrix4("projection", projection);
                        material.SetVector3("u_CameraPos", cameraPos);

                        _model.Draw();
                    }
                }
                else
                {
                    for (int i = 0; i < _materials.Length && i < _model.SubMeshCount; i++)
                    {
                        var material = _materials[i];
                        if (material == null)
                            continue;

                        material.SetPBRProperties();
                        material.Use();

                        material.SetMatrix4("model", model);
                        material.SetMatrix4("view", view);
                        material.SetMatrix4("projection", projection);
                        material.SetVector3("u_CameraPos", cameraPos);

                        _model.DrawSubMesh(i);
                    }
                }
            }
        }

        public override void OnDestroy()
        {
            _model = null;
            _materials = new Material[0];
            _instanceMatrices = null;
        }
    }
}