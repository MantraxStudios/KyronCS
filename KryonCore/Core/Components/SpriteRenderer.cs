using KrayonCore.Core;
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
        public string MaterialPath = "";

        public SpriteClip() { }

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
        private Material _baseMaterial;
        private Model _quadModel;
        private Mesh _quadMesh;
        private bool _isInitialized = false;
        private bool _isReadyToRender = false;
        private bool _pendingReadyToRender = false;

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

        private string _currentAnimationName = "";

        [ToStorage]
        public string CurrentAnimationName
        {
            get => _currentAnimationName;
            set
            {
                if (_currentAnimationName != value)
                {
                    _currentAnimationName = value;
                    OnCurrentAnimationNameChanged();
                }
            }
        }

        private void OnCurrentAnimationNameChanged()
        {
            if (_isInitialized && !string.IsNullOrEmpty(_currentAnimationName))
                Play(_currentAnimationName);
        }

        [ToStorage] public float AnimationSpeed { get; set; } = 1.0f;

        public Material Material => _material;
        public Material BaseMaterial => _baseMaterial;
        public Model QuadModel => _quadModel;
        public int CurrentFrameIndex => _currentFrameIndex;
        public SpriteClip CurrentClip => _currentClip;
        public int TotalFrames => _currentClip?.Frames.Count ?? 0;

        public SpriteRenderer() { }

        public override void Awake()
        {
            _isReadyToRender = false;

            CreateQuadMesh();

            if (!string.IsNullOrEmpty(MaterialPath))
            {
                LoadMaterialFromPath(MaterialPath);
                _baseMaterial = _material;
            }

            _isInitialized = true;
        }

        public override void Start()
        {
            if (_material == null && !string.IsNullOrEmpty(MaterialPath))
            {
                LoadMaterialFromPath(MaterialPath);
                _baseMaterial = _material;
            }

            if (_material == null)
            {
                _material = GraphicsEngine.Instance.Materials.Get("basic");
                _baseMaterial = _material;
                ReadTextureDimensionsFromMaterial();
            }

            if (_material != null && (_textureWidth == 0 || _textureHeight == 0))
                ReadTextureDimensionsFromMaterial();

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
                Play(CurrentAnimationName);

            _pendingReadyToRender = true;
        }

        public override void OnWillRenderObject()
        {
            if (_pendingReadyToRender)
            {
                _isReadyToRender = true;
                _pendingReadyToRender = false;
            }

            if (!_isReadyToRender)
                return;

            if (_material != null && (_textureWidth == 0 || _textureHeight == 0))
            {
                ReadTextureDimensionsFromMaterial();
                if (_textureWidth > 0 && _textureHeight > 0)
                {
                    _needsMeshRebuild = true;
                    _needsUVUpdate = true;
                }
            }

            UpdateAnimation(TimerData.DeltaTime);

            bool changed = false;

            if (_lastTileIndexX != TileIndexX) { _lastTileIndexX = TileIndexX; changed = true; }
            if (_lastTileIndexY != TileIndexY) { _lastTileIndexY = TileIndexY; changed = true; }

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
            }

            if (_lastFlipX != FlipX) { _lastFlipX = FlipX; changed = true; }
            if (_lastFlipY != FlipY) { _lastFlipY = FlipY; changed = true; }

            if (_needsMeshRebuild) { UpdateQuadSize(); _needsMeshRebuild = false; }
            if (changed || _needsUVUpdate) { UpdateUVs(); _needsUVUpdate = false; }
        }

        private void UpdateAnimation(float timeDelta)
        {
            if (!IsPlaying || _currentClip == null || _currentClip.Frames.Count == 0)
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
                        _currentFrameIndex = 0;
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
            if (_currentClip == null || _currentFrameIndex < 0 || _currentFrameIndex >= _currentClip.Frames.Count)
                return;

            var frame = _currentClip.Frames[_currentFrameIndex];
            SetTile(frame.TileIndexX, frame.TileIndexY);
        }

        public void Play(string animationName, bool restart = false)
        {
            var clip = Animations.Find(a => a.Name == animationName);

            if (clip == null)
                return;

            bool clipChanged = _currentClip != clip;

            if (clipChanged || restart)
            {
                _currentClip = clip;
                _currentFrameIndex = 0;
                _frameTimer = 0.0f;
                CurrentAnimationName = animationName;

                if (!string.IsNullOrEmpty(clip.MaterialPath))
                {
                    LoadAnimationMaterial(clip.MaterialPath);
                }
                else if (clipChanged)
                {
                    ApplyMaterial(_baseMaterial);
                }

                UpdateAnimationFrame();
            }

            IsPlaying = true;
        }

        private void ApplyMaterial(Material newMaterial)
        {
            if (newMaterial == null)
                return;

            _isReadyToRender = false;
            _pendingReadyToRender = false;
            _material = newMaterial;
            ReadTextureDimensionsFromMaterial();

            // Primero actualizar el frame correcto ANTES de reconstruir el mesh
            UpdateAnimationFrame();

            // Sincronizar los "last" values para que OnWillRenderObject no detecte cambios falsos
            _lastTileIndexX = TileIndexX;
            _lastTileIndexY = TileIndexY;
            _lastTileWidth = TileWidth;
            _lastTileHeight = TileHeight;
            _lastPixelsPerUnit = PixelsPerUnit;
            _lastFlipX = FlipX;
            _lastFlipY = FlipY;

            RebuildMesh();

            _needsUVUpdate = false;
            _needsMeshRebuild = false;
            _isReadyToRender = true;
        }

        private void RebuildMesh()
        {
            if (PixelsPerUnit <= 0 || TileWidth == 0 || TileHeight == 0)
                return;

            float halfWidth = (TileWidth / PixelsPerUnit) * 0.5f;
            float halfHeight = (TileHeight / PixelsPerUnit) * 0.5f;

            float uLeft = _textureWidth > 0 ? (float)(TileIndexX * TileWidth) / _textureWidth : 0f;
            float uRight = _textureWidth > 0 ? (float)(TileIndexX * TileWidth + TileWidth) / _textureWidth : 1f;
            float vTop = _textureHeight > 0 ? (float)(TileIndexY * TileHeight) / _textureHeight : 0f;
            float vBot = _textureHeight > 0 ? (float)(TileIndexY * TileHeight + TileHeight) / _textureHeight : 1f;

            float u0 = FlipX ? uRight : uLeft;
            float u1 = FlipX ? uLeft : uRight;
            float u2 = FlipX ? uLeft : uRight;
            float u3 = FlipX ? uRight : uLeft;

            float v0 = FlipY ? vTop : vBot;
            float v1 = FlipY ? vTop : vBot;
            float v2 = FlipY ? vBot : vTop;
            float v3 = FlipY ? vBot : vTop;

            float[] vertices = new float[]
            {
                -halfWidth, -halfHeight, 0.0f,  0.0f, 0.0f, 1.0f,  u0, v0,  1.0f, 0.0f, 0.0f,  0.0f, 1.0f, 0.0f,
                 halfWidth, -halfHeight, 0.0f,  0.0f, 0.0f, 1.0f,  u1, v1,  1.0f, 0.0f, 0.0f,  0.0f, 1.0f, 0.0f,
                 halfWidth,  halfHeight, 0.0f,  0.0f, 0.0f, 1.0f,  u2, v2,  1.0f, 0.0f, 0.0f,  0.0f, 1.0f, 0.0f,
                -halfWidth,  halfHeight, 0.0f,  0.0f, 0.0f, 1.0f,  u3, v3,  1.0f, 0.0f, 0.0f,  0.0f, 1.0f, 0.0f,
            };

            uint[] indices = new uint[] { 0, 1, 2, 2, 3, 0 };

            _quadMesh?.Dispose();
            _quadModel?.Dispose();

            _quadMesh = new Mesh(vertices, indices);
            _quadModel = new Model();

            var subMeshInfo = new SubMeshInfo(_quadMesh, 0, new MaterialTextureInfo());
            _quadModel._subMeshes.Add(subMeshInfo);

            var combinedMeshField = typeof(Model).GetField("_combinedMesh",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            combinedMeshField?.SetValue(_quadModel, _quadMesh);
        }

        private void LoadAnimationMaterial(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                var newMaterial = GraphicsEngine.Instance.Materials.Get(path);
                ApplyMaterial(newMaterial);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SpriteRenderer] Error al cargar material de animación '{path}': {ex.Message}");
                _isReadyToRender = true;
            }
        }

        public void Stop()
        {
            IsPlaying = false;
            _frameTimer = 0.0f;

            if (_material != _baseMaterial)
                ApplyMaterial(_baseMaterial);
        }

        public void Pause() => IsPlaying = false;

        public void Resume()
        {
            if (_currentClip != null)
                IsPlaying = true;
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

                if (_material != _baseMaterial)
                {
                    _material = _baseMaterial;
                    ReadTextureDimensionsFromMaterial();
                    _needsUVUpdate = true;
                }
            }
        }

        public SpriteClip GetAnimation(string name) => Animations.Find(a => a.Name == name);

        public void DebugStatus()
        {
            Console.WriteLine("=== SPRITE RENDERER DEBUG ===");
            Console.WriteLine($"GameObject: {GameObject?.Name ?? "NULL"}");
            Console.WriteLine($"Enabled: {Enabled}");
            Console.WriteLine($"Material: {(_material != null ? "OK" : "NULL")}");
            Console.WriteLine($"Base Material: {(_baseMaterial != null ? "OK" : "NULL")}");
            Console.WriteLine($"QuadModel: {(_quadModel != null ? "OK" : "NULL")}");
            Console.WriteLine($"QuadMesh: {(_quadMesh != null ? "OK" : "NULL")}");
            Console.WriteLine($"Texture Size: {_textureWidth}x{_textureHeight}");
            Console.WriteLine($"Tile Size: {TileWidth}x{TileHeight}");
            Console.WriteLine($"Tiles Grid: {TilesPerRow}x{TilesPerColumn}");
            Console.WriteLine($"Current Tile: [{TileIndexX},{TileIndexY}]");
            Console.WriteLine($"PixelsPerUnit: {PixelsPerUnit}");
            Console.WriteLine($"ReadyToRender: {_isReadyToRender}");

            if (_quadModel != null)
            {
                Console.WriteLine($"Model.SubMeshCount: {_quadModel.SubMeshCount}");
                Console.WriteLine($"Model._subMeshes.Count: {_quadModel._subMeshes?.Count ?? 0}");
            }

            if (_quadMesh != null)
            {
                Console.WriteLine($"Mesh.VAO: {_quadMesh.GetVAO()}");
                Console.WriteLine($"Mesh.IndexCount: {_quadMesh.IndexCount}");
                Console.WriteLine($"Mesh.VertexCount: {_quadMesh.VertexCount}");
            }

            Console.WriteLine("============================");
        }

        private void CreateQuadMesh()
        {
            float[] vertices = new float[]
            {
                -0.5f, -0.5f, 0.0f,  0.0f, 0.0f, 1.0f,  0.0f, 0.0f,  1.0f, 0.0f, 0.0f,  0.0f, 1.0f, 0.0f,
                 0.5f, -0.5f, 0.0f,  0.0f, 0.0f, 1.0f,  1.0f, 0.0f,  1.0f, 0.0f, 0.0f,  0.0f, 1.0f, 0.0f,
                 0.5f,  0.5f, 0.0f,  0.0f, 0.0f, 1.0f,  1.0f, 1.0f,  1.0f, 0.0f, 0.0f,  0.0f, 1.0f, 0.0f,
                -0.5f,  0.5f, 0.0f,  0.0f, 0.0f, 1.0f,  0.0f, 1.0f,  1.0f, 0.0f, 0.0f,  0.0f, 1.0f, 0.0f,
            };

            uint[] indices = new uint[] { 0, 1, 2, 2, 3, 0 };

            _quadMesh = new Mesh(vertices, indices);
            _quadModel = new Model();

            var subMeshInfo = new SubMeshInfo(_quadMesh, 0, new MaterialTextureInfo());
            _quadModel._subMeshes.Add(subMeshInfo);

            var combinedMeshField = typeof(Model).GetField("_combinedMesh",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            combinedMeshField?.SetValue(_quadModel, _quadMesh);
        }

        private void UpdateQuadSize()
        {
            if (_quadMesh == null || PixelsPerUnit <= 0)
                return;

            float halfWidth = (TileWidth / PixelsPerUnit) * 0.5f;
            float halfHeight = (TileHeight / PixelsPerUnit) * 0.5f;

            UpdateMeshSize(halfWidth, halfHeight);
            _needsUVUpdate = true;
        }

        private void UpdateMeshSize(float halfWidth, float halfHeight)
        {
            float[] vertices = new float[]
            {
                -halfWidth, -halfHeight, 0.0f,  0.0f, 0.0f, 1.0f,  0.0f, 0.0f,  1.0f, 0.0f, 0.0f,  0.0f, 1.0f, 0.0f,
                 halfWidth, -halfHeight, 0.0f,  0.0f, 0.0f, 1.0f,  1.0f, 0.0f,  1.0f, 0.0f, 0.0f,  0.0f, 1.0f, 0.0f,
                 halfWidth,  halfHeight, 0.0f,  0.0f, 0.0f, 1.0f,  1.0f, 1.0f,  1.0f, 0.0f, 0.0f,  0.0f, 1.0f, 0.0f,
                -halfWidth,  halfHeight, 0.0f,  0.0f, 0.0f, 1.0f,  0.0f, 1.0f,  1.0f, 0.0f, 0.0f,  0.0f, 1.0f, 0.0f,
            };

            var vboField = typeof(Mesh).GetField("_vbo",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (vboField != null)
            {
                int vbo = (int)vboField.GetValue(_quadMesh);
                GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
                GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, vertices.Length * sizeof(float), vertices);
                GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            }
        }

        private void OnMaterialPathChanged()
        {
            if (_isInitialized)
            {
                if (!string.IsNullOrEmpty(MaterialPath))
                {
                    LoadMaterialFromPath(MaterialPath);
                    _baseMaterial = _material;
                }
                else
                {
                    _material = null;
                    _baseMaterial = null;
                    _textureWidth = 0;
                    _textureHeight = 0;
                }
            }
        }

        protected virtual void LoadMaterialFromPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                _material = GraphicsEngine.Instance.Materials.Get(path);

                if (_material != null)
                {
                    ReadTextureDimensionsFromMaterial();
                    _needsUVUpdate = true;
                    _needsMeshRebuild = true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SpriteRenderer] Error al cargar material '{path}': {ex.Message}");
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
                return;
            }

            try
            {
                var texture = _material.AlbedoTexture;

                if (texture != null)
                {
                    _textureWidth = texture.Width;
                    _textureHeight = texture.Height;
                }
                else
                {
                    _textureWidth = 0;
                    _textureHeight = 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SpriteRenderer] Error leyendo dimensiones: {ex.Message}");
                _textureWidth = 0;
                _textureHeight = 0;
            }
        }

        public void SetMaterial(Material material)
        {
            _baseMaterial = material;
            _materialPath = material?.Name ?? "";
            ApplyMaterial(material);
        }

        public void SetTile(int indexX, int indexY)
        {
            if (indexX < 0 || indexY < 0)
                return;

            if (TilesPerRow > 0 && TilesPerColumn > 0)
            {
                if (indexX >= TilesPerRow || indexY >= TilesPerColumn)
                    return;
            }

            TileIndexX = indexX;
            TileIndexY = indexY;
        }

        public void SetTileByIndex(int index)
        {
            if (index < 0)
                return;

            if (TotalTiles > 0 && index >= TotalTiles)
                return;

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

            ReadTextureDimensionsFromMaterial();
            _needsMeshRebuild = true;
            _needsUVUpdate = true;
        }

        private void UpdateUVs()
        {
            if (_quadMesh == null || _textureWidth == 0 || _textureHeight == 0 || TileWidth == 0 || TileHeight == 0)
                return;

            float uLeft = (float)(TileIndexX * TileWidth) / _textureWidth;
            float uRight = (float)(TileIndexX * TileWidth + TileWidth) / _textureWidth;
            float vTop = (float)(TileIndexY * TileHeight) / _textureHeight;
            float vBot = (float)(TileIndexY * TileHeight + TileHeight) / _textureHeight;

            float u0 = FlipX ? uRight : uLeft;
            float u1 = FlipX ? uLeft : uRight;
            float u2 = FlipX ? uLeft : uRight;
            float u3 = FlipX ? uRight : uLeft;

            float v0 = FlipY ? vTop : vBot;
            float v1 = FlipY ? vTop : vBot;
            float v2 = FlipY ? vBot : vTop;
            float v3 = FlipY ? vBot : vTop;

            UpdateMeshUVs(u0, v0, u1, v1, u2, v2, u3, v3);
        }

        private void UpdateMeshUVs(
            float u0, float v0,
            float u1, float v1,
            float u2, float v2,
            float u3, float v3)
        {
            if (TileWidth == 0 || TileHeight == 0 || PixelsPerUnit <= 0)
                return;

            float halfWidth = (TileWidth / PixelsPerUnit) * 0.5f;
            float halfHeight = (TileHeight / PixelsPerUnit) * 0.5f;

            float[] vertices = new float[]
            {
                -halfWidth, -halfHeight, 0.0f,  0.0f, 0.0f, 1.0f,  u0, v0,  1.0f, 0.0f, 0.0f,  0.0f, 1.0f, 0.0f,
                 halfWidth, -halfHeight, 0.0f,  0.0f, 0.0f, 1.0f,  u1, v1,  1.0f, 0.0f, 0.0f,  0.0f, 1.0f, 0.0f,
                 halfWidth,  halfHeight, 0.0f,  0.0f, 0.0f, 1.0f,  u2, v2,  1.0f, 0.0f, 0.0f,  0.0f, 1.0f, 0.0f,
                -halfWidth,  halfHeight, 0.0f,  0.0f, 0.0f, 1.0f,  u3, v3,  1.0f, 0.0f, 0.0f,  0.0f, 1.0f, 0.0f,
            };

            var vboField = typeof(Mesh).GetField("_vbo",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (vboField != null)
            {
                int vbo = (int)vboField.GetValue(_quadMesh);
                GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
                GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, vertices.Length * sizeof(float), vertices);
                GL.Flush();
                GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            }
        }

        public void Draw()
        {
            if (_quadModel != null)
                _quadModel.Draw();
        }

        public override void OnDestroy()
        {
            _material = null;
            _baseMaterial = null;
            _quadMesh?.Dispose();
            _quadMesh = null;
            _quadModel?.Dispose();
            _quadModel = null;
            _textureWidth = 0;
            _textureHeight = 0;
        }
    }
}