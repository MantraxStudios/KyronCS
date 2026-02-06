using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL4;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

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

        private bool _isGenerating = false;
        private CancellationTokenSource _generationCancellation;
        private int _tilesGeneratedCount = 0;
        private int _totalTilesToGenerate = 0;
        private object _tilesLock = new object();

        private bool _instanceDataDirty = true;
        private Dictionary<(int modelIndex, int materialIndex), List<Matrix4>> _instanceGroups = new Dictionary<(int, int), List<Matrix4>>();

        public int TileCount => _tiles.Count;
        public int GridSizeX { get; set; } = 10;
        public int GridSizeZ { get; set; } = 10;
        public float TileSpacing { get; set; } = 1.0f;

        public int BatchSize { get; set; } = 100;
        public int DelayBetweenBatchesMs { get; set; } = 1;

        public bool IsGenerating => _isGenerating;
        public float GenerationProgress => _totalTilesToGenerate > 0
            ? (float)_tilesGeneratedCount / _totalTilesToGenerate
            : 0f;

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

            Console.WriteLine("Material updated");
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

            lock (_tilesLock)
            {
                _tiles.Add(tile);
                _instanceDataDirty = true;
            }
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

            lock (_tilesLock)
            {
                _tiles.Add(tile);
                _instanceDataDirty = true;
            }
        }

        public void AddCustomTile(Tile tile)
        {
            lock (_tilesLock)
            {
                _tiles.Add(tile);
                _instanceDataDirty = true;
            }
        }

        public void RemoveTile(int index)
        {
            lock (_tilesLock)
            {
                if (index >= 0 && index < _tiles.Count)
                {
                    _tiles.RemoveAt(index);
                    _instanceDataDirty = true;
                }
            }
        }

        private async Task GenerateTilesAsync(List<Tile> tilesToGenerate, CancellationToken cancellationToken)
        {
            _isGenerating = true;
            _tilesGeneratedCount = 0;
            _totalTilesToGenerate = tilesToGenerate.Count;

            Console.WriteLine($"[TileRenderer] Iniciando generación asíncrona de {_totalTilesToGenerate} tiles");

            try
            {
                for (int i = 0; i < tilesToGenerate.Count; i += BatchSize)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        Console.WriteLine($"[TileRenderer] Generación cancelada en tile {i}");
                        break;
                    }

                    int batchEnd = Math.Min(i + BatchSize, tilesToGenerate.Count);

                    lock (_tilesLock)
                    {
                        for (int j = i; j < batchEnd; j++)
                        {
                            _tiles.Add(tilesToGenerate[j]);
                        }
                        _instanceDataDirty = true;
                    }

                    _tilesGeneratedCount = batchEnd;

                    if (DelayBetweenBatchesMs > 0 && batchEnd < tilesToGenerate.Count)
                    {
                        await Task.Delay(DelayBetweenBatchesMs, cancellationToken);
                    }
                }

                Console.WriteLine($"[TileRenderer] ✓ Generación completada: {_tiles.Count} tiles totales");
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"[TileRenderer] Generación cancelada por el usuario");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TileRenderer] Error durante generación: {ex.Message}");
            }
            finally
            {
                _isGenerating = false;
                _tilesGeneratedCount = 0;
                _totalTilesToGenerate = 0;
            }
        }

        public void CancelGeneration()
        {
            if (_isGenerating && _generationCancellation != null)
            {
                _generationCancellation.Cancel();
            }
        }

        [CallEvent("Add Single Tile at Origin")]
        public void AddSingleTile()
        {
            AddTile(Vector3.Zero, 0, 0);
        }

        [CallEvent("Generate Grid")]
        public async void GenerateGrid()
        {
            if (_isGenerating)
            {
                Console.WriteLine($"[TileRenderer] Ya hay una generación en progreso");
                return;
            }

            ClearTiles();

            int totalTiles = GridSizeX * GridSizeZ;
            var tilesToGenerate = new List<Tile>(totalTiles);

            for (int x = 0; x < GridSizeX; x++)
            {
                for (int z = 0; z < GridSizeZ; z++)
                {
                    Vector3 position = new Vector3(x * TileSpacing, 0, z * TileSpacing);

                    var tile = new Tile
                    {
                        Transform = new TileTransform
                        {
                            _Pos = position,
                            _Rot = Quaternion.Identity,
                            _Scale = Vector3.One
                        },
                        MaterialIndex = 0,
                        ModelIndex = 0
                    };

                    tilesToGenerate.Add(tile);
                }
            }

            _generationCancellation = new CancellationTokenSource();
            await GenerateTilesAsync(tilesToGenerate, _generationCancellation.Token);
        }

        [CallEvent("Clear All Tiles")]
        public void ClearTiles()
        {
            CancelGeneration();

            lock (_tilesLock)
            {
                _tiles.Clear();
                _instanceDataDirty = true;
            }
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
        }

        [CallEvent("Generate Line Z (10 tiles)")]
        public void GenerateLineZ()
        {
            ClearTiles();
            for (int i = 0; i < 10; i++)
            {
                AddTile(new Vector3(0, 0, i * TileSpacing), 0, 0);
            }
        }

        [CallEvent("Generate Large Grid (100x100)")]
        public async void GenerateLargeGrid()
        {
            GridSizeX = 100;
            GridSizeZ = 100;
            await Task.Run(GenerateGrid);
        }

        [CallEvent("Generate Huge Grid (500x500)")]
        public async void GenerateHugeGrid()
        {
            GridSizeX = 500;
            GridSizeZ = 500;
            await Task.Run(GenerateGrid);
        }

        [CallEvent("Cancel Generation")]
        public void CancelGenerationButton()
        {
            CancelGeneration();
        }

        public Tile GetTile(int index)
        {
            lock (_tilesLock)
            {
                if (index >= 0 && index < _tiles.Count)
                    return _tiles[index];
                return null;
            }
        }

        // Método interno para actualizar datos de instancias
        internal void UpdateInstanceData()
        {
            if (!_instanceDataDirty)
                return;

            _instanceGroups.Clear();

            Tile[] tilesSnapshot;
            lock (_tilesLock)
            {
                tilesSnapshot = _tiles.ToArray();
            }

            var transform = GetComponent<Transform>();
            if (transform == null)
                return;

            Matrix4 parentMatrix = transform.GetWorldMatrix();

            foreach (var tile in tilesSnapshot)
            {
                if (tile.ModelIndex < 0 || tile.ModelIndex >= _models.Length)
                    continue;
                if (tile.MaterialIndex < 0 || tile.MaterialIndex >= _materials.Length)
                    continue;

                var key = (tile.ModelIndex, tile.MaterialIndex);

                if (!_instanceGroups.ContainsKey(key))
                {
                    _instanceGroups[key] = new List<Matrix4>();
                }

                Matrix4 tileLocalMatrix = tile.Transform.GetMatrix();
                Matrix4 finalMatrix = tileLocalMatrix * parentMatrix;
                _instanceGroups[key].Add(finalMatrix);
            }

            foreach (var kvp in _instanceGroups)
            {
                int modelIndex = kvp.Key.modelIndex;
                if (modelIndex >= 0 && modelIndex < _models.Length && _models[modelIndex] != null)
                {
                    _models[modelIndex].SetupInstancing(kvp.Value.ToArray());
                }
            }

            _instanceDataDirty = false;
        }

        // Propiedades públicas para que SceneRenderer acceda
        public Dictionary<(int modelIndex, int materialIndex), List<Matrix4>> InstanceGroups => _instanceGroups;

        public override void OnDestroy()
        {
            CancelGeneration();

            lock (_tilesLock)
            {
                _tiles.Clear();
            }

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