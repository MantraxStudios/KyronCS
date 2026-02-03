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
        [ToStorage] public string ModelPath { get; set; } = "";
        [ToStorage] public string[] MaterialPaths { get; set; } = new string[0];

        private Material[] _materials = new Material[0];

        private Model? _model;

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

        public void Render(Matrix4 view, Matrix4 projection)
        {
            if (_model == null || !Enabled)
                return;

            var transform = GetComponent<Transform>();
            if (transform == null)
                return;

            Matrix4 model = CalculateModelMatrix(transform);

            if (_materials.Length == 0)
            {
                return;
            }

            // Obtener posición de cámara una sola vez
            Vector3 cameraPos = GraphicsEngine.Instance.GetSceneRenderer().GetCamera().Position;

            if (_materials.Length == 1)
            {
                var material = _materials[0];
                if (material != null)
                {
                    // IMPORTANTE: SetPBRProperties antes de Use()
                    material.SetPBRProperties();
                    material.Use();

                    // Pasar matrices y posición de cámara
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

                    // IMPORTANTE: SetPBRProperties antes de Use()
                    material.SetPBRProperties();
                    material.Use();

                    // Pasar matrices y posición de cámara
                    material.SetMatrix4("model", model);
                    material.SetMatrix4("view", view);
                    material.SetMatrix4("projection", projection);
                    material.SetVector3("u_CameraPos", cameraPos);

                    _model.DrawSubMesh(i);
                }
            }
        }

        private Matrix4 CalculateModelMatrix(Transform transform)
        {
            Matrix4 translation = Matrix4.CreateTranslation(transform.X, transform.Y, transform.Z);
            Matrix4 rotationX = Matrix4.CreateRotationX(MathHelper.DegreesToRadians(transform.RotationX));
            Matrix4 rotationY = Matrix4.CreateRotationY(MathHelper.DegreesToRadians(transform.RotationY));
            Matrix4 rotationZ = Matrix4.CreateRotationZ(MathHelper.DegreesToRadians(transform.RotationZ));
            Matrix4 rotation = rotationZ * rotationY * rotationX;
            Matrix4 scale = Matrix4.CreateScale(transform.ScaleX, transform.ScaleY, transform.ScaleZ);
            return scale * rotation * translation;
        }

        public override void OnDestroy()
        {
            _model = null;
            _materials = new Material[0];
        }
    }
}