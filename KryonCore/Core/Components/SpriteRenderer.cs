using KrayonCore;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;

namespace KrayonCore
{
    public class SpriteRenderer : Component
    {
        private string _materialPath = "";
        private Material _material;
        private Model _quadModel;
        private bool _isInitialized = false;

        // Variables para detectar cambios
        private int _lastTileIndexX = 0;
        private int _lastTileIndexY = 0;
        private int _lastTileWidth = 32;
        private int _lastTileHeight = 32;
        private bool _lastFlipX = false;
        private bool _lastFlipY = false;
        private bool _needsUVUpdate = false;

        // Propiedades de material
        [ToStorage]
        public string MaterialPath
        {
            get => _materialPath;
            set
            {
                if (_materialPath != value)
                {
                    _materialPath = value;
                    OnMaterialPathChanged();
                }
            }
        }

        // Propiedades del sprite/tile
        [ToStorage] public int TileWidth { get; set; } = 32;
        [ToStorage] public int TileHeight { get; set; } = 32;
        [ToStorage] public int TileIndexX { get; set; } = 0;
        [ToStorage] public int TileIndexY { get; set; } = 0;

        // Dimensiones de la textura (se leen automáticamente del material)
        public int TextureWidth { get; private set; } = 0;
        public int TextureHeight { get; private set; } = 0;

        // Propiedades calculadas
        public int TilesPerRow => TextureWidth > 0 && TileWidth > 0 ? TextureWidth / TileWidth : 0;
        public int TilesPerColumn => TextureHeight > 0 && TileHeight > 0 ? TextureHeight / TileHeight : 0;
        public int TotalTiles => TilesPerRow * TilesPerColumn;

        // Propiedades de renderizado
        [ToStorage] public Vector4 Color { get; set; } = new Vector4(1, 1, 1, 1);
        [ToStorage] public bool FlipX { get; set; } = false;
        [ToStorage] public bool FlipY { get; set; } = false;

        public Material Material => _material;
        public Model QuadModel => _quadModel;

        public SpriteRenderer()
        {
        }

        public override void Awake()
        {
            Console.WriteLine($"[SpriteRenderer] Awake llamado en {GameObject?.Name ?? "Unknown"}");

            CreateQuadMesh();

            if (!string.IsNullOrEmpty(MaterialPath))
            {
                Console.WriteLine($"[SpriteRenderer] Cargando material desde: {MaterialPath}");
                LoadMaterialFromPath(MaterialPath);
            }
            else
            {
                Console.WriteLine($"[SpriteRenderer] No hay MaterialPath especificado");
            }

            _isInitialized = true;
        }

        public override void Start()
        {
            if (_material == null)
            {
                Console.WriteLine($"[SpriteRenderer] Warning: No hay material asignado. Usando material básico.");
                _material = GraphicsEngine.Instance.Materials.Get("basic");
            }

            // Leer dimensiones de la textura del material
            ReadTextureDimensionsFromMaterial();

            // Inicializar valores de última actualización
            _lastTileIndexX = TileIndexX;
            _lastTileIndexY = TileIndexY;
            _lastTileWidth = TileWidth;
            _lastTileHeight = TileHeight;
            _lastFlipX = FlipX;
            _lastFlipY = FlipY;

            UpdateUVs();

            Console.WriteLine($"[SpriteRenderer] Start completado - Material: {(_material != null ? "OK" : "NULL")}");
            Console.WriteLine($"[SpriteRenderer] Textura: {TextureWidth}x{TextureHeight}, Tiles: {TilesPerRow}x{TilesPerColumn}");
        }

        public override void Update(float timeDelta)
        {
            // Detectar si algún valor cambió y necesitamos actualizar las UVs
            bool changed = false;

            if (_lastTileIndexX != TileIndexX)
            {
                _lastTileIndexX = TileIndexX;
                changed = true;
            }

            if (_lastTileIndexY != TileIndexY)
            {
                _lastTileIndexY = TileIndexY;
                changed = true;
            }

            if (_lastTileWidth != TileWidth)
            {
                _lastTileWidth = TileWidth;
                changed = true;
            }

            if (_lastTileHeight != TileHeight)
            {
                _lastTileHeight = TileHeight;
                changed = true;
            }

            if (_lastFlipX != FlipX)
            {
                _lastFlipX = FlipX;
                changed = true;
            }

            if (_lastFlipY != FlipY)
            {
                _lastFlipY = FlipY;
                changed = true;
            }

            // Si hubo cambios o se marcó para actualización, actualizar UVs
            if (changed || _needsUVUpdate)
            {
                UpdateUVs();
                _needsUVUpdate = false;
            }
        }

        private void CreateQuadMesh()
        {
            // Crear un quad simple (dos triángulos)
            // Formato de vértices: Posición(3) + Normal(3) + UV(2) + Tangent(3) + Bitangent(3) = 14 floats por vértice
            float[] vertices = new float[]
            {
                // Vértice 0: Bottom-left
                -0.5f, -0.5f, 0.0f,  // Posición
                 0.0f,  0.0f, 1.0f,  // Normal (apuntando hacia Z+)
                 0.0f,  0.0f,        // UV
                 1.0f,  0.0f, 0.0f,  // Tangent
                 0.0f,  1.0f, 0.0f,  // Bitangent

                // Vértice 1: Bottom-right
                 0.5f, -0.5f, 0.0f,  // Posición
                 0.0f,  0.0f, 1.0f,  // Normal
                 1.0f,  0.0f,        // UV
                 1.0f,  0.0f, 0.0f,  // Tangent
                 0.0f,  1.0f, 0.0f,  // Bitangent

                // Vértice 2: Top-right
                 0.5f,  0.5f, 0.0f,  // Posición
                 0.0f,  0.0f, 1.0f,  // Normal
                 1.0f,  1.0f,        // UV
                 1.0f,  0.0f, 0.0f,  // Tangent
                 0.0f,  1.0f, 0.0f,  // Bitangent

                // Vértice 3: Top-left
                -0.5f,  0.5f, 0.0f,  // Posición
                 0.0f,  0.0f, 1.0f,  // Normal
                 0.0f,  1.0f,        // UV
                 1.0f,  0.0f, 0.0f,  // Tangent
                 0.0f,  1.0f, 0.0f   // Bitangent
            };

            uint[] indices = new uint[]
            {
                0, 1, 2,  // Primer triángulo
                2, 3, 0   // Segundo triángulo
            };

            // Crear el modelo usando el mesh interno
            _quadModel = new Model();
            var mesh = new Mesh(vertices, indices);

            // Usar reflexión para agregar el mesh al modelo
            var meshesField = typeof(Model).GetField("_meshes",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (meshesField != null)
            {
                var meshList = (List<Mesh>)meshesField.GetValue(_quadModel);
                meshList.Add(mesh);
            }

            Console.WriteLine($"[SpriteRenderer] Quad mesh creado con {vertices.Length / 14} vértices");
        }

        private void OnMaterialPathChanged()
        {
            if (_isInitialized)
            {
                Console.WriteLine($"[SpriteRenderer] MaterialPath cambió a: {MaterialPath}");

                if (!string.IsNullOrEmpty(MaterialPath))
                {
                    LoadMaterialFromPath(MaterialPath);
                }
                else
                {
                    _material = null;
                    TextureWidth = 0;
                    TextureHeight = 0;
                    Console.WriteLine($"[SpriteRenderer] MaterialPath vacío, material eliminado");
                }
            }
        }

        protected virtual void LoadMaterialFromPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                Console.WriteLine($"[SpriteRenderer] LoadMaterialFromPath: ruta vacía o nula");
                return;
            }

            try
            {
                Console.WriteLine($"[SpriteRenderer] Cargando material: {path}");

                _material = GraphicsEngine.Instance.Materials.Get(path);

                if (_material != null)
                {
                    Console.WriteLine($"[SpriteRenderer] ✓ Material cargado: {path}");

                    // Leer dimensiones de la textura
                    ReadTextureDimensionsFromMaterial();

                    // Marcar para actualizar UVs en el próximo Update
                    _needsUVUpdate = true;
                }
                else
                {
                    Console.WriteLine($"[SpriteRenderer] ✗ GraphicsEngine.Materials.Get retornó null para: {path}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SpriteRenderer] ✗ Error al cargar material '{path}':");
                Console.WriteLine($"[SpriteRenderer]   Mensaje: {ex.Message}");
                Console.WriteLine($"[SpriteRenderer]   Tipo: {ex.GetType().Name}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"[SpriteRenderer]   Inner: {ex.InnerException.Message}");
                }
                _material = null;
                TextureWidth = 0;
                TextureHeight = 0;
            }
        }

        private void ReadTextureDimensionsFromMaterial()
        {
            if (_material == null)
            {
                TextureWidth = 0;
                TextureHeight = 0;
                return;
            }

            try
            {
                // Intentar obtener la textura principal del material
                var texture = _material.AlbedoTexture;

                if (texture != null)
                {
                    TextureWidth = texture.Width;
                    TextureHeight = texture.Height;

                    Console.WriteLine($"[SpriteRenderer] Dimensiones de textura leídas del material:");
                    Console.WriteLine($"[SpriteRenderer]   Ancho: {TextureWidth}, Alto: {TextureHeight}");
                    Console.WriteLine($"[SpriteRenderer]   Grid de tiles: {TilesPerRow}x{TilesPerColumn} ({TotalTiles} total)");
                }
                else
                {
                    Console.WriteLine($"[SpriteRenderer] Warning: Material no tiene textura AlbedoTexture");
                    TextureWidth = 0;
                    TextureHeight = 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SpriteRenderer] Warning: No se pudo leer textura del material: {ex.Message}");
                TextureWidth = 0;
                TextureHeight = 0;
            }
        }

        public void SetMaterial(Material material)
        {
            _material = material;
            _materialPath = ""; // Limpiar el path ya que es asignación directa

            Console.WriteLine($"[SpriteRenderer] Material asignado directamente");

            // Leer dimensiones de la textura del nuevo material
            ReadTextureDimensionsFromMaterial();

            // Marcar para actualizar UVs en el próximo Update
            _needsUVUpdate = true;
        }

        public void SetTile(int indexX, int indexY)
        {
            if (indexX < 0 || indexY < 0)
            {
                Console.WriteLine($"[SpriteRenderer] Warning: Índices negativos ({indexX}, {indexY})");
                return;
            }

            if (TilesPerRow > 0 && TilesPerColumn > 0)
            {
                if (indexX >= TilesPerRow || indexY >= TilesPerColumn)
                {
                    Console.WriteLine($"[SpriteRenderer] Warning: Índice de tile fuera de rango ({indexX}, {indexY}). Max: ({TilesPerRow - 1}, {TilesPerColumn - 1})");
                    return;
                }
            }

            TileIndexX = indexX;
            TileIndexY = indexY;
            // No llamamos a UpdateUVs() aquí, se actualizará automáticamente en Update()
        }

        public void SetTileByIndex(int index)
        {
            if (index < 0)
            {
                Console.WriteLine($"[SpriteRenderer] Warning: Índice negativo {index}");
                return;
            }

            if (TotalTiles > 0 && index >= TotalTiles)
            {
                Console.WriteLine($"[SpriteRenderer] Warning: Índice de tile fuera de rango {index}. Max: {TotalTiles - 1}");
                return;
            }

            if (TilesPerRow > 0)
            {
                TileIndexX = index % TilesPerRow;
                TileIndexY = index / TilesPerRow;
                // No llamamos a UpdateUVs() aquí, se actualizará automáticamente en Update()
            }
        }

        public void SetTileSize(int width, int height)
        {
            TileWidth = Math.Max(1, width);
            TileHeight = Math.Max(1, height);
            // No llamamos a UpdateUVs() aquí, se actualizará automáticamente en Update()

            Console.WriteLine($"[SpriteRenderer] Tamaño de tile actualizado: {TileWidth}x{TileHeight}");
            Console.WriteLine($"[SpriteRenderer] Nuevo grid: {TilesPerRow}x{TilesPerColumn}");
        }

        private void UpdateUVs()
        {
            if (_quadModel == null || TextureWidth == 0 || TextureHeight == 0 || TileWidth == 0 || TileHeight == 0)
                return;

            // Calcular UVs basados en el tile actual
            float uMin = (float)(TileIndexX * TileWidth) / TextureWidth;
            float vMin = (float)(TileIndexY * TileHeight) / TextureHeight;
            float uMax = (float)((TileIndexX + 1) * TileWidth) / TextureWidth;
            float vMax = (float)((TileIndexY + 1) * TileHeight) / TextureHeight;

            // Aplicar flip si es necesario
            if (FlipX)
            {
                float temp = uMin;
                uMin = uMax;
                uMax = temp;
            }

            if (FlipY)
            {
                float temp = vMin;
                vMin = vMax;
                vMax = temp;
            }

            // Obtener el mesh del modelo usando reflexión
            var meshesField = typeof(Model).GetField("_meshes",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (meshesField != null)
            {
                var meshList = (List<Mesh>)meshesField.GetValue(_quadModel);
                if (meshList != null && meshList.Count > 0)
                {
                    var mesh = meshList[0];

                    // Actualizar UVs del mesh
                    UpdateMeshUVs(mesh, uMin, vMin, uMax, vMax);
                }
            }
        }

        private void UpdateMeshUVs(Mesh mesh, float uMin, float vMin, float uMax, float vMax)
        {
            // Crear array con las nuevas UVs
            // Formato: 14 floats por vértice (Pos(3) + Normal(3) + UV(2) + Tangent(3) + Bitangent(3))
            float[] vertices = new float[]
            {
                // Vértice 0: Bottom-left
                -0.5f, -0.5f, 0.0f,  // Posición
                 0.0f,  0.0f, 1.0f,  // Normal
                 uMin,  vMin,        // UV - ACTUALIZADO
                 1.0f,  0.0f, 0.0f,  // Tangent
                 0.0f,  1.0f, 0.0f,  // Bitangent

                // Vértice 1: Bottom-right
                 0.5f, -0.5f, 0.0f,  // Posición
                 0.0f,  0.0f, 1.0f,  // Normal
                 uMax,  vMin,        // UV - ACTUALIZADO
                 1.0f,  0.0f, 0.0f,  // Tangent
                 0.0f,  1.0f, 0.0f,  // Bitangent

                // Vértice 2: Top-right
                 0.5f,  0.5f, 0.0f,  // Posición
                 0.0f,  0.0f, 1.0f,  // Normal
                 uMax,  vMax,        // UV - ACTUALIZADO
                 1.0f,  0.0f, 0.0f,  // Tangent
                 0.0f,  1.0f, 0.0f,  // Bitangent

                // Vértice 3: Top-left
                -0.5f,  0.5f, 0.0f,  // Posición
                 0.0f,  0.0f, 1.0f,  // Normal
                 uMin,  vMax,        // UV - ACTUALIZADO
                 1.0f,  0.0f, 0.0f,  // Tangent
                 0.0f,  1.0f, 0.0f   // Bitangent
            };

            // Actualizar el VBO del mesh
            var vboField = typeof(Mesh).GetField("_vbo",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (vboField != null)
            {
                int vbo = (int)vboField.GetValue(mesh);

                // Bind y actualizar el VBO
                GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
                GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero,
                    vertices.Length * sizeof(float), vertices);
                GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            }
        }

        public void Draw()
        {
            if (_quadModel != null)
            {
                _quadModel.Draw();
            }
        }

        public override void OnDestroy()
        {
            _material = null;
            _quadModel?.Dispose();
            _quadModel = null;
            TextureWidth = 0;
            TextureHeight = 0;
        }
    }
}