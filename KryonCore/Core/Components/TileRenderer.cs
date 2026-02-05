using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL4;
using System;
using System.Collections.Generic;

namespace KrayonCore.Graphics
{
    public class TileTransform
    {
        public Vector3 _Pos;
        public Quaternion _Rot;
        public Vector3 _Scale = new Vector3(1.0f, 1.0f, 1.0f);

        public TileTransform()
        {
            _Pos = Vector3.Zero;
            _Rot = Quaternion.Identity;
        }

        public Matrix4 GetMatrix()
        {
            Matrix4 translation = Matrix4.CreateTranslation(_Pos);
            Matrix4 rotation = Matrix4.CreateFromQuaternion(_Rot);
            Matrix4 scale = Matrix4.CreateScale(_Scale);

            return scale * rotation * translation;
        }
    }

    public class Tile
    {
        public TileTransform Transform { get; set; }
        public int MaterialIndex { get; set; } = 0;
        public int ModelIndex { get; set; } = 0;

        public Tile()
        {
            Transform = new TileTransform();
        }
    }

    public class TileRenderer : Component
    {
        [ToStorage] public string[] ModelPaths { get; set; } = new string[] { "models/Cube.fbx" };
        [ToStorage] public string[] MaterialPaths { get; set; } = new string[0];

        [NoSerializeToInspector]
        private Model[] _models = new Model[0];

        [NoSerializeToInspector]
        private Material[] _materials = new Material[0];

        [NoSerializeToInspector, ToStorage]
        private List<Tile> _tiles = new List<Tile>();

        // Propiedades para el Inspector
        public int TileCount => _tiles.Count;
        public int GridSizeX { get; set; } = 10;
        public int GridSizeZ { get; set; } = 10;
        public float TileSpacing { get; set; } = 1.0f;

        public Model[] Models
        {
            get => _models;
            set => _models = value ?? new Model[0];
        }

        public Material[] Materials
        {
            get => _materials;
            set => _materials = value ?? new Material[0];
        }

        public TileRenderer()
        {
            _models = new Model[0];
            _materials = new Material[0];
            _tiles = new List<Tile>();
        }

        public override void Awake()
        {
            Console.WriteLine($"[TileRenderer] Awake llamado en {GameObject?.Name ?? "Unknown"}");

            // Cargar modelos
            if (ModelPaths != null && ModelPaths.Length > 0)
            {
                _models = new Model[ModelPaths.Length];
                for (int i = 0; i < ModelPaths.Length; i++)
                {
                    if (!string.IsNullOrEmpty(ModelPaths[i]))
                    {
                        Console.WriteLine($"[TileRenderer] Intentando cargar modelo {i}: {ModelPaths[i]}");
                        LoadModelAtIndex(i, ModelPaths[i]);
                    }
                }
            }

            // Cargar materiales desde MaterialPaths (igual que MeshRenderer)
            for (int i = 0; i < MaterialPaths.Length; i++)
            {
                SetMaterial(i, GraphicsEngine.Instance.Materials.Get(MaterialPaths[i]));
            }
        }

        public override void Start()
        {
            if (_models.Length == 0 && ModelPaths != null && ModelPaths.Length > 0)
            {
                Console.WriteLine($"[TileRenderer] Warning: Modelos no cargados en Start. Reintentando carga.");
                for (int i = 0; i < ModelPaths.Length; i++)
                {
                    if (!string.IsNullOrEmpty(ModelPaths[i]))
                    {
                        LoadModelAtIndex(i, ModelPaths[i]);
                    }
                }
            }

            if (_materials.Length == 0 && MaterialPaths != null && MaterialPaths.Length > 0)
            {
                Console.WriteLine($"[TileRenderer] Info: Hay {MaterialPaths.Length} rutas de materiales guardadas");
                Console.WriteLine($"[TileRenderer] Los materiales deben ser asignados manualmente después de cargar la escena");
            }

            Console.WriteLine($"[TileRenderer] Start completado - Modelos: {_models.Length}, Materiales: {_materials.Length}, Tiles: {_tiles.Count}");
        }

        protected virtual void LoadModelAtIndex(int index, string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                Console.WriteLine($"[TileRenderer] LoadModelAtIndex: ruta vacía o nula para índice {index}");
                return;
            }

            try
            {
                Console.WriteLine($"[TileRenderer] Llamando a Model.Load para: {path}");

                var model = Model.Load(path);

                if (model != null)
                {
                    if (index >= _models.Length)
                    {
                        Array.Resize(ref _models, index + 1);
                    }
                    _models[index] = model;

                    Console.WriteLine($"[TileRenderer] ✓ Modelo cargado exitosamente en índice {index}: {path}");
                }
                else
                {
                    Console.WriteLine($"[TileRenderer] ✗ Model.Load retornó null para: {path}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TileRenderer] ✗ Error al cargar modelo '{path}':");
                Console.WriteLine($"[TileRenderer]   Mensaje: {ex.Message}");
                Console.WriteLine($"[TileRenderer]   Tipo: {ex.GetType().Name}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"[TileRenderer]   Inner: {ex.InnerException.Message}");
                }
            }
        }

        // Método para sincronizar MaterialPaths (igual que MeshRenderer.SaveMaterialPaths)
        public void SaveMaterialPaths(string[] paths)
        {
            MaterialPaths = paths ?? new string[0];
        }

        // Gestión de Materiales (igual que MeshRenderer)
        public void SetMaterial(int index, Material material)
        {
            if (index < 0) return;

            if (index >= _materials.Length)
            {
                Array.Resize(ref _materials, index + 1);
            }

            _materials[index] = material;

            // NO sincronizar MaterialPaths aquí - se hace solo desde el Inspector
            // (igual que MeshRenderer)
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

        // Gestión de Modelos
        public void SetModel(int index, string path)
        {
            if (index < 0) return;

            var paths = ModelPaths;
            if (index >= paths.Length)
            {
                Array.Resize(ref paths, index + 1);
                ModelPaths = paths;
            }

            paths[index] = path;
            ModelPaths = paths;
            LoadModelAtIndex(index, path);
        }

        public void SetModelDirect(int index, Model model)
        {
            if (index < 0) return;

            if (index >= _models.Length)
            {
                Array.Resize(ref _models, index + 1);
            }

            _models[index] = model;
        }

        public Model GetModel(int index)
        {
            if (index < 0 || index >= _models.Length)
                return null;
            return _models[index];
        }

        public int ModelCount => _models.Length;

        // Gestión de Tiles (métodos base)
        public void AddTile(Vector3 position, int materialIndex = 0, int modelIndex = 0)
        {
            var tile = new Tile
            {
                Transform = new TileTransform
                {
                    _Pos = position,
                    _Rot = Quaternion.Identity,
                    _Scale = Vector3.One
                },
                MaterialIndex = materialIndex,
                ModelIndex = modelIndex
            };

            _tiles.Add(tile);
            Console.WriteLine($"[TileRenderer] Tile añadido en posición {position}. Total tiles: {_tiles.Count}");
        }

        public void AddTile(Vector3 position, Quaternion rotation, Vector3 scale, int materialIndex = 0, int modelIndex = 0)
        {
            var tile = new Tile
            {
                Transform = new TileTransform
                {
                    _Pos = position,
                    _Rot = rotation,
                    _Scale = scale
                },
                MaterialIndex = materialIndex,
                ModelIndex = modelIndex
            };

            _tiles.Add(tile);
            Console.WriteLine($"[TileRenderer] Tile personalizado añadido. Total tiles: {_tiles.Count}");
        }

        public void AddCustomTile(Tile tile)
        {
            _tiles.Add(tile);
        }

        public void RemoveTile(int index)
        {
            if (index >= 0 && index < _tiles.Count)
            {
                _tiles.RemoveAt(index);
                Console.WriteLine($"[TileRenderer] Tile removido en índice {index}. Total tiles: {_tiles.Count}");
            }
        }

        // Métodos de Inspector con CallEvent
        [CallEvent("Add Single Tile at Origin")]
        public void AddSingleTile()
        {
            AddTile(Vector3.Zero, 0, 0);
        }

        [CallEvent("Generate Grid")]
        public void GenerateGrid()
        {
            ClearTiles();

            for (int x = 0; x < GridSizeX; x++)
            {
                for (int z = 0; z < GridSizeZ; z++)
                {
                    Vector3 position = new Vector3(x * TileSpacing, 0, z * TileSpacing);
                    AddTile(position, 0, 0);
                }
            }

            Console.WriteLine($"[TileRenderer] Grid generado: {GridSizeX}x{GridSizeZ} = {_tiles.Count} tiles");
        }

        [CallEvent("Clear All Tiles")]
        public void ClearTiles()
        {
            int count = _tiles.Count;
            _tiles.Clear();
            Console.WriteLine($"[TileRenderer] {count} tiles eliminados");
        }

        [CallEvent("Add Tile at X=1")]
        public void AddTileAtX1()
        {
            AddTile(new Vector3(1, 0, 0), 0, 0);
        }

        [CallEvent("Add Tile at Z=1")]
        public void AddTileAtZ1()
        {
            AddTile(new Vector3(0, 0, 1), 0, 0);
        }

        [CallEvent("Generate Line X (10 tiles)")]
        public void GenerateLineX()
        {
            ClearTiles();
            for (int i = 0; i < 10; i++)
            {
                AddTile(new Vector3(i * TileSpacing, 0, 0), 0, 0);
            }
            Console.WriteLine($"[TileRenderer] Línea X generada: 10 tiles");
        }

        [CallEvent("Generate Line Z (10 tiles)")]
        public void GenerateLineZ()
        {
            ClearTiles();
            for (int i = 0; i < 10; i++)
            {
                AddTile(new Vector3(0, 0, i * TileSpacing), 0, 0);
            }
            Console.WriteLine($"[TileRenderer] Línea Z generada: 10 tiles");
        }

        public Tile GetTile(int index)
        {
            if (index >= 0 && index < _tiles.Count)
                return _tiles[index];
            return null;
        }

        // Renderizado
        public void Render(Matrix4 view, Matrix4 projection)
        {
            if (!Enabled || _tiles.Count == 0)
                return;

            if (_models.Length == 0 || _materials.Length == 0)
            {
                Console.WriteLine($"[TileRenderer] No se puede renderizar: Modelos={_models.Length}, Materiales={_materials.Length}");
                return;
            }

            var transform = GetComponent<Transform>();
            if (transform == null)
                return;

            Matrix4 parentMatrix = transform.GetWorldMatrix();
            Vector3 cameraPos = GraphicsEngine.Instance.GetSceneRenderer().GetCamera().Position;

            foreach (var tile in _tiles)
            {
                // Validar índices
                if (tile.ModelIndex < 0 || tile.ModelIndex >= _models.Length)
                    continue;
                if (tile.MaterialIndex < 0 || tile.MaterialIndex >= _materials.Length)
                    continue;

                var model = _models[tile.ModelIndex];
                var material = _materials[tile.MaterialIndex];

                if (model == null || material == null)
                    continue;

                // Combinar transformación del padre con la del tile
                Matrix4 tileLocalMatrix = tile.Transform.GetMatrix();
                Matrix4 finalModelMatrix = tileLocalMatrix * parentMatrix;

                material.SetPBRProperties();
                material.Use();

                material.SetMatrix4("model", finalModelMatrix);
                material.SetMatrix4("view", view);
                material.SetMatrix4("projection", projection);
                material.SetVector3("u_CameraPos", cameraPos);

                model.Draw();
            }
        }

        public override void OnDestroy()
        {
            _tiles.Clear();
            _models = new Model[0];
            _materials = new Material[0];
        }
    }

    public class TileEditor
    {
        private TileRenderer _renderer;
        private int _selectedTileIndex = -1;

        public TileEditor(TileRenderer renderer)
        {
            _renderer = renderer;
        }

        public void SelectTile(int index)
        {
            if (index >= 0 && index < _renderer.TileCount)
            {
                _selectedTileIndex = index;
            }
        }

        public void DeselectTile()
        {
            _selectedTileIndex = -1;
        }

        public bool HasSelection => _selectedTileIndex >= 0;

        public void MoveSelectedTile(Vector3 offset)
        {
            if (_selectedTileIndex >= 0)
            {
                var tile = _renderer.GetTile(_selectedTileIndex);
                if (tile != null)
                {
                    tile.Transform._Pos += offset;
                }
            }
        }

        public void RotateSelectedTile(Quaternion rotation)
        {
            if (_selectedTileIndex >= 0)
            {
                var tile = _renderer.GetTile(_selectedTileIndex);
                if (tile != null)
                {
                    tile.Transform._Rot *= rotation;
                }
            }
        }

        public void ScaleSelectedTile(Vector3 scale)
        {
            if (_selectedTileIndex >= 0)
            {
                var tile = _renderer.GetTile(_selectedTileIndex);
                if (tile != null)
                {
                    tile.Transform._Scale = Vector3.Multiply(tile.Transform._Scale, scale);
                }
            }
        }

        public void SetSelectedTileMaterial(int materialIndex)
        {
            if (_selectedTileIndex >= 0)
            {
                var tile = _renderer.GetTile(_selectedTileIndex);
                if (tile != null)
                {
                    tile.MaterialIndex = materialIndex;
                }
            }
        }

        public void SetSelectedTileModel(int modelIndex)
        {
            if (_selectedTileIndex >= 0)
            {
                var tile = _renderer.GetTile(_selectedTileIndex);
                if (tile != null)
                {
                    tile.ModelIndex = modelIndex;
                }
            }
        }

        public void DeleteSelectedTile()
        {
            if (_selectedTileIndex >= 0)
            {
                _renderer.RemoveTile(_selectedTileIndex);
                _selectedTileIndex = -1;
            }
        }

        public Tile GetSelectedTile()
        {
            if (_selectedTileIndex >= 0)
            {
                return _renderer.GetTile(_selectedTileIndex);
            }
            return null;
        }
    }
}