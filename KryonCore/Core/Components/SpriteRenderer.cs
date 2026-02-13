using KrayonCore.GraphicsData;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;

namespace KrayonCore
{
    [Serializable]
    public class AnimationFrame
    {
        public int TileIndexX = 0;
        public int TileIndexY = 0;

        public AnimationFrame() { }

        public AnimationFrame(int x, int y)
        {
            TileIndexX = x;
            TileIndexY = y;
        }
    }

    [Serializable]
    public class SpriteClip
    {
        public string Name = "New Animation";
        public List<AnimationFrame> Frames = new List<AnimationFrame>();
        public float FrameRate = 12.0f;
        public bool Loop = true;

        public SpriteClip()
        {
        }

        public SpriteClip(string name)
        {
            Name = name;
        }

        public void AddFrame(int x, int y)
        {
            Frames.Add(new AnimationFrame(x, y));
        }
    }

    public class SpriteRenderer : Component
    {
        private string _materialPath = "";
        private Material _material;
        private Model _quadModel;
        private bool _isInitialized = false;

        private int _lastTileIndexX = 0;
        private int _lastTileIndexY = 0;
        private int _lastTileWidth = 32;
        private int _lastTileHeight = 32;
        private float _lastPixelsPerUnit = 32.0f;
        private bool _lastFlipX = false;
        private bool _lastFlipY = false;
        private bool _needsUVUpdate = false;
        private bool _needsMeshRebuild = false;

        private int _textureWidth = 0;
        private int _textureHeight = 0;

        private SpriteClip _currentClip;
        private int _currentFrameIndex = 0;
        private float _frameTimer = 0.0f;

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

        [ToStorage] public int TileWidth { get; set; } = 32;
        [ToStorage] public int TileHeight { get; set; } = 32;
        [ToStorage] public int TileIndexX { get; set; } = 0;
        [ToStorage] public int TileIndexY { get; set; } = 0;

        public int TextureWidth => _textureWidth;
        public int TextureHeight => _textureHeight;

        public int TilesPerRow => TextureWidth > 0 && TileWidth > 0 ? TextureWidth / TileWidth : 0;
        public int TilesPerColumn => TextureHeight > 0 && TileHeight > 0 ? TextureHeight / TileHeight : 0;
        public int TotalTiles => TilesPerRow * TilesPerColumn;

        [ToStorage] public Vector4 Color { get; set; } = new Vector4(1, 1, 1, 1);
        [ToStorage] public bool FlipX { get; set; } = false;
        [ToStorage] public bool FlipY { get; set; } = false;
        [ToStorage] public float PixelsPerUnit { get; set; } = 32.0f;

        [ToStorage] public List<SpriteClip> Animations { get; set; } = new List<SpriteClip>();
        [ToStorage] public bool IsPlaying { get; set; } = false;
        [ToStorage] public string CurrentAnimationName { get; set; } = "";
        [ToStorage] public float AnimationSpeed { get; set; } = 1.0f;

        public Material Material => _material;
        public Model QuadModel => _quadModel;
        public int CurrentFrameIndex => _currentFrameIndex;
        public SpriteClip CurrentClip => _currentClip;
        public int TotalFrames => _currentClip?.Frames.Count ?? 0;

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
            if (_material == null && !string.IsNullOrEmpty(MaterialPath))
            {
                Console.WriteLine($"[SpriteRenderer] Reintentando cargar material en Start: {MaterialPath}");
                LoadMaterialFromPath(MaterialPath);
            }

            if (_material == null)
            {
                Console.WriteLine($"[SpriteRenderer] Warning: No hay material asignado. Usando material básico.");
                _material = GraphicsEngine.Instance.Materials.Get("basic");
                ReadTextureDimensionsFromMaterial();
            }

            if (_material != null && (_textureWidth == 0 || _textureHeight == 0))
            {
                Console.WriteLine($"[SpriteRenderer] Forzando lectura de dimensiones en Start");
                ReadTextureDimensionsFromMaterial();
            }

            _lastTileIndexX = TileIndexX;
            _lastTileIndexY = TileIndexY;
            _lastTileWidth = TileWidth;
            _lastTileHeight = TileHeight;
            _lastPixelsPerUnit = PixelsPerUnit;
            _lastFlipX = FlipX;
            _lastFlipY = FlipY;

            UpdateQuadSize();
            UpdateUVs();

            if (!string.IsNullOrEmpty(CurrentAnimationName))
            {
                Play(CurrentAnimationName);
            }

            Console.WriteLine($"[SpriteRenderer] Start completado - Material: {(_material != null ? "OK" : "NULL")}");
            Console.WriteLine($"[SpriteRenderer] Textura final: {TextureWidth}x{TextureHeight}, Tiles: {TilesPerRow}x{TilesPerColumn}");
        }

        public override void Update(float timeDelta)
        {
            if (_material != null && (_textureWidth == 0 || _textureHeight == 0))
            {
                Console.WriteLine($"[SpriteRenderer] Update: Detectadas dimensiones en 0 (W:{_textureWidth}, H:{_textureHeight}), intentando leer de nuevo");
                ReadTextureDimensionsFromMaterial();
                if (_textureWidth > 0 && _textureHeight > 0)
                {
                    Console.WriteLine($"[SpriteRenderer] ✓ Dimensiones cargadas en Update: {_textureWidth}x{_textureHeight}");
                    _needsMeshRebuild = true;
                    _needsUVUpdate = true;
                }
            }

            UpdateAnimation(timeDelta);

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
                ReadTextureDimensionsFromMaterial();
                _needsMeshRebuild = true;
                changed = true;
            }

            if (_lastTileHeight != TileHeight)
            {
                _lastTileHeight = TileHeight;
                ReadTextureDimensionsFromMaterial();
                _needsMeshRebuild = true;
                changed = true;
            }

            if (Math.Abs(_lastPixelsPerUnit - PixelsPerUnit) > 0.001f)
            {
                _lastPixelsPerUnit = PixelsPerUnit;
                _needsMeshRebuild = true;
                changed = true;
                Console.WriteLine($"[SpriteRenderer] PixelsPerUnit cambió a: {PixelsPerUnit}");
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

            if (_needsMeshRebuild)
            {
                UpdateQuadSize();
                _needsMeshRebuild = false;
            }

            if (changed || _needsUVUpdate)
            {
                UpdateUVs();
                _needsUVUpdate = false;
            }
        }

        private void UpdateAnimation(float timeDelta)
        {
            if (!IsPlaying || _currentClip == null)
                return;

            if (_currentClip.Frames.Count == 0)
                return;

            _frameTimer += timeDelta * AnimationSpeed;

            float frameDuration = 1.0f / _currentClip.FrameRate;

            if (_frameTimer >= frameDuration)
            {
                _frameTimer -= frameDuration;
                _currentFrameIndex++;

                if (_currentFrameIndex >= _currentClip.Frames.Count)
                {
                    if (_currentClip.Loop)
                    {
                        _currentFrameIndex = 0;
                    }
                    else
                    {
                        _currentFrameIndex = _currentClip.Frames.Count - 1;
                        IsPlaying = false;
                        return;
                    }
                }

                UpdateAnimationFrame();
            }
        }

        private void UpdateAnimationFrame()
        {
            if (_currentClip == null)
                return;

            if (_currentFrameIndex < 0 || _currentFrameIndex >= _currentClip.Frames.Count)
                return;

            var frame = _currentClip.Frames[_currentFrameIndex];
            SetTile(frame.TileIndexX, frame.TileIndexY);
        }

        public void Play(string animationName, bool restart = false)
        {
            var clip = Animations.Find(a => a.Name == animationName);

            if (clip == null)
            {
                Console.WriteLine($"[SpriteRenderer] Animation '{animationName}' not found");
                return;
            }

            if (_currentClip != clip || restart)
            {
                _currentClip = clip;
                _currentFrameIndex = 0;
                _frameTimer = 0.0f;
                CurrentAnimationName = animationName;
                UpdateAnimationFrame();
            }

            IsPlaying = true;
        }

        public void Stop()
        {
            IsPlaying = false;
            _frameTimer = 0.0f;
        }

        public void Pause()
        {
            IsPlaying = false;
        }

        public void Resume()
        {
            if (_currentClip != null)
            {
                IsPlaying = true;
            }
        }

        public void SetAnimationFrame(int frameIndex)
        {
            if (_currentClip == null || _currentClip.Frames.Count == 0)
                return;

            _currentFrameIndex = Math.Clamp(frameIndex, 0, _currentClip.Frames.Count - 1);
            _frameTimer = 0.0f;
            UpdateAnimationFrame();
        }

        public SpriteClip AddAnimation(string name)
        {
            var clip = new SpriteClip(name);
            Animations.Add(clip);
            return clip;
        }

        public void RemoveAnimation(string name)
        {
            Animations.RemoveAll(a => a.Name == name);

            if (_currentClip?.Name == name)
            {
                _currentClip = null;
                IsPlaying = false;
            }
        }

        public SpriteClip GetAnimation(string name)
        {
            return Animations.Find(a => a.Name == name);
        }

        private void CreateQuadMesh()
        {
            float[] vertices = new float[]
            {
                -0.5f, -0.5f, 0.0f,
                 0.0f,  0.0f, 1.0f,
                 0.0f,  0.0f,
                 1.0f,  0.0f, 0.0f,
                 0.0f,  1.0f, 0.0f,

                 0.5f, -0.5f, 0.0f,
                 0.0f,  0.0f, 1.0f,
                 1.0f,  0.0f,
                 1.0f,  0.0f, 0.0f,
                 0.0f,  1.0f, 0.0f,

                 0.5f,  0.5f, 0.0f,
                 0.0f,  0.0f, 1.0f,
                 1.0f,  1.0f,
                 1.0f,  0.0f, 0.0f,
                 0.0f,  1.0f, 0.0f,

                -0.5f,  0.5f, 0.0f,
                 0.0f,  0.0f, 1.0f,
                 0.0f,  1.0f,
                 1.0f,  0.0f, 0.0f,
                 0.0f,  1.0f, 0.0f
            };

            uint[] indices = new uint[]
            {
                0, 1, 2,
                2, 3, 0
            };

            _quadModel = new Model();
            var mesh = new Mesh(vertices, indices);

            var meshesField = typeof(Model).GetField("_meshes",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (meshesField != null)
            {
                var meshList = (List<Mesh>)meshesField.GetValue(_quadModel);
                meshList.Add(mesh);
            }

            Console.WriteLine($"[SpriteRenderer] Quad mesh creado");
        }

        private void UpdateQuadSize()
        {
            if (_quadModel == null || PixelsPerUnit <= 0)
                return;

            float quadWidth = TileWidth / PixelsPerUnit;
            float quadHeight = TileHeight / PixelsPerUnit;

            float halfWidth = quadWidth * 0.5f;
            float halfHeight = quadHeight * 0.5f;

            Console.WriteLine($"[SpriteRenderer] Quad ajustado: {TileWidth}px / {PixelsPerUnit}ppu = {quadWidth:F2} units ({quadWidth:F2}x{quadHeight:F2})");

            var meshesField = typeof(Model).GetField("_meshes",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (meshesField != null)
            {
                var meshList = (List<Mesh>)meshesField.GetValue(_quadModel);
                if (meshList != null && meshList.Count > 0)
                {
                    var mesh = meshList[0];
                    UpdateMeshSize(mesh, halfWidth, halfHeight);
                }
            }

            _needsUVUpdate = true;
        }

        private void UpdateMeshSize(Mesh mesh, float halfWidth, float halfHeight)
        {
            float[] vertices = new float[]
            {
                -halfWidth, -halfHeight, 0.0f,
                 0.0f,  0.0f, 1.0f,
                 0.0f,  0.0f,
                 1.0f,  0.0f, 0.0f,
                 0.0f,  1.0f, 0.0f,

                 halfWidth, -halfHeight, 0.0f,
                 0.0f,  0.0f, 1.0f,
                 1.0f,  0.0f,
                 1.0f,  0.0f, 0.0f,
                 0.0f,  1.0f, 0.0f,

                 halfWidth,  halfHeight, 0.0f,
                 0.0f,  0.0f, 1.0f,
                 1.0f,  1.0f,
                 1.0f,  0.0f, 0.0f,
                 0.0f,  1.0f, 0.0f,

                -halfWidth,  halfHeight, 0.0f,
                 0.0f,  0.0f, 1.0f,
                 0.0f,  1.0f,
                 1.0f,  0.0f, 0.0f,
                 0.0f,  1.0f, 0.0f
            };

            var vboField = typeof(Mesh).GetField("_vbo",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (vboField != null)
            {
                int vbo = (int)vboField.GetValue(mesh);

                GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
                GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero,
                    vertices.Length * sizeof(float), vertices);
                GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            }
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
                    _textureWidth = 0;
                    _textureHeight = 0;
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

                    ReadTextureDimensionsFromMaterial();

                    _needsUVUpdate = true;
                    _needsMeshRebuild = true;
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
                _textureWidth = 0;
                _textureHeight = 0;
            }
        }

        private void ReadTextureDimensionsFromMaterial()
        {
            if (_material == null)
            {
                _textureWidth = 0;
                _textureHeight = 0;
                Console.WriteLine($"[SpriteRenderer] ReadTextureDimensions: Material es null");
                return;
            }

            try
            {
                var texture = _material.AlbedoTexture;

                if (texture != null)
                {
                    _textureWidth = texture.Width;
                    _textureHeight = texture.Height;

                    Console.WriteLine($"[SpriteRenderer] ✓ Dimensiones leídas - Textura: {_textureWidth}x{_textureHeight}px");
                    Console.WriteLine($"[SpriteRenderer]   Tile: {TileWidth}x{TileHeight}px");
                    Console.WriteLine($"[SpriteRenderer]   Grid: {TilesPerRow}x{TilesPerColumn} tiles");
                }
                else
                {
                    Console.WriteLine($"[SpriteRenderer] ✗ Material.AlbedoTexture es null");
                    _textureWidth = 0;
                    _textureHeight = 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SpriteRenderer] ✗ Error leyendo dimensiones: {ex.Message}");
                _textureWidth = 0;
                _textureHeight = 0;
            }
        }

        public void SetMaterial(Material material)
        {
            _material = material;
            _materialPath = material?.Name ?? "";

            Console.WriteLine($"[SpriteRenderer] Material asignado directamente");

            ReadTextureDimensionsFromMaterial();

            _needsUVUpdate = true;
            _needsMeshRebuild = true;
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
            }
        }

        public void SetTileSize(int width, int height)
        {
            TileWidth = Math.Max(1, width);
            TileHeight = Math.Max(1, height);

            Console.WriteLine($"[SpriteRenderer] Tamaño de tile: {TileWidth}x{TileHeight}px");

            ReadTextureDimensionsFromMaterial();

            _needsMeshRebuild = true;
            _needsUVUpdate = true;
        }

        private void UpdateUVs()
        {
            if (_quadModel == null || _textureWidth == 0 || _textureHeight == 0 || TileWidth == 0 || TileHeight == 0)
            {
                Console.WriteLine($"[SpriteRenderer] UpdateUVs cancelado - Quad:{_quadModel != null}, TexW:{_textureWidth}, TexH:{_textureHeight}, TileW:{TileWidth}, TileH:{TileHeight}");
                return;
            }

            int pixelStartX = TileIndexX * TileWidth;
            int pixelStartY = TileIndexY * TileHeight;
            int pixelEndX = pixelStartX + TileWidth;
            int pixelEndY = pixelStartY + TileHeight;

            float uMin = (float)pixelStartX / (float)_textureWidth;
            float vMin = (float)pixelStartY / (float)_textureHeight;
            float uMax = (float)pixelEndX / (float)_textureWidth;
            float vMax = (float)pixelEndY / (float)_textureHeight;

            Console.WriteLine($"[SpriteRenderer] Tile[{TileIndexX},{TileIndexY}] → Pixels[{pixelStartX},{pixelStartY}]->[{pixelEndX},{pixelEndY}] → UV[{uMin:F3},{vMin:F3}]->[{uMax:F3},{vMax:F3}]");

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

            var meshesField = typeof(Model).GetField("_meshes",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (meshesField != null)
            {
                var meshList = (List<Mesh>)meshesField.GetValue(_quadModel);
                if (meshList != null && meshList.Count > 0)
                {
                    var mesh = meshList[0];
                    UpdateMeshUVs(mesh, uMin, vMin, uMax, vMax);
                }
            }
        }

        private void UpdateMeshUVs(Mesh mesh, float uMin, float vMin, float uMax, float vMax)
        {
            if (TileWidth == 0 || TileHeight == 0 || PixelsPerUnit <= 0)
                return;

            float quadWidth = TileWidth / PixelsPerUnit;
            float quadHeight = TileHeight / PixelsPerUnit;
            float halfWidth = quadWidth * 0.5f;
            float halfHeight = quadHeight * 0.5f;

            float[] vertices = new float[]
            {
                -halfWidth, -halfHeight, 0.0f,
                 0.0f,  0.0f, 1.0f,
                 uMin,  vMin,
                 1.0f,  0.0f, 0.0f,
                 0.0f,  1.0f, 0.0f,

                 halfWidth, -halfHeight, 0.0f,
                 0.0f,  0.0f, 1.0f,
                 uMax,  vMin,
                 1.0f,  0.0f, 0.0f,
                 0.0f,  1.0f, 0.0f,

                 halfWidth,  halfHeight, 0.0f,
                 0.0f,  0.0f, 1.0f,
                 uMax,  vMax,
                 1.0f,  0.0f, 0.0f,
                 0.0f,  1.0f, 0.0f,

                -halfWidth,  halfHeight, 0.0f,
                 0.0f,  0.0f, 1.0f,
                 uMin,  vMax,
                 1.0f,  0.0f, 0.0f,
                 0.0f,  1.0f, 0.0f
            };

            var vboField = typeof(Mesh).GetField("_vbo",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (vboField != null)
            {
                int vbo = (int)vboField.GetValue(mesh);

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
            _textureWidth = 0;
            _textureHeight = 0;
        }
    }
}