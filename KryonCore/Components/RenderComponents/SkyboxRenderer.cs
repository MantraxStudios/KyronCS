using KrayonCore.Core;
using KrayonCore.GraphicsData;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;

namespace KrayonCore.Components.RenderComponents
{
    public class SkyboxRenderer : Component, IRenderable
    {
        [NoSerializeToInspector] public RenderableType RenderType => RenderableType.Skybox;

        private string _materialPath = "";
        private Material _material;
        private Model _sphereModel;
        private Mesh _sphereMesh;
        private bool _isInitialized = false;
        private bool _isReadyToRender = false;
        private bool _pendingReadyToRender = false;

        private int _textureWidth = 0;
        private int _textureHeight = 0;

        // Sphere generation parameters
        private int _lastSegments = 32;
        private int _lastRings = 16;
        private float _lastRadius = 500.0f;
        private bool _needsMeshRebuild = false;

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

        [ToStorage] public Vector4 Color { get; set; } = new Vector4(1, 1, 1, 1);
        [ToStorage] public float Radius { get; set; } = 500.0f;
        [ToStorage] public int Segments { get; set; } = 32;
        [ToStorage] public int Rings { get; set; } = 16;
        [ToStorage] public bool InvertNormals { get; set; } = true; // Para ver el skybox desde dentro
        [ToStorage] public Vector2 TextureOffset { get; set; } = Vector2.Zero;
        [ToStorage] public Vector2 TextureScale { get; set; } = Vector2.One;
        [ToStorage] public float RotationSpeed { get; set; } = 0.0f; // Grados por segundo

        private float _currentRotation = 0.0f;

        public int TextureWidth => _textureWidth;
        public int TextureHeight => _textureHeight;
        public Material Material => _material;
        public Model SphereModel => _sphereModel;

        public SkyboxRenderer() { }

        public override void Awake()
        {
            GraphicsEngine.Instance?.GetSceneRenderer()?.RegisterRenderer(this);

            _isReadyToRender = false;

            CreateSphereMesh();

            if (!string.IsNullOrEmpty(MaterialPath))
            {
                LoadMaterialFromPath(MaterialPath);
            }

            _isInitialized = true;
        }

        public override void Start()
        {
            if (_material == null && !string.IsNullOrEmpty(MaterialPath))
            {
                LoadMaterialFromPath(MaterialPath);
            }

            if (_material == null)
            {
                _material = GraphicsEngine.Instance.Materials.Get("basic");
                ReadTextureDimensionsFromMaterial();
            }

            if (_material != null && (_textureWidth == 0 || _textureHeight == 0))
                ReadTextureDimensionsFromMaterial();

            _lastSegments = Segments;
            _lastRings = Rings;
            _lastRadius = Radius;

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
            }

            // Update rotation
            if (Math.Abs(RotationSpeed) > 0.001f)
            {
                _currentRotation += RotationSpeed * TimerData.DeltaTime;
                _currentRotation %= 360.0f;
            }

            // Check if mesh needs rebuilding
            bool needsRebuild = false;

            if (_lastSegments != Segments)
            {
                _lastSegments = Math.Max(3, Segments);
                Segments = _lastSegments;
                needsRebuild = true;
            }

            if (_lastRings != Rings)
            {
                _lastRings = Math.Max(2, Rings);
                Rings = _lastRings;
                needsRebuild = true;
            }

            if (Math.Abs(_lastRadius - Radius) > 0.001f)
            {
                _lastRadius = Math.Max(1.0f, Radius);
                Radius = _lastRadius;
                needsRebuild = true;
            }

            if (needsRebuild || _needsMeshRebuild)
            {
                CreateSphereMesh();
                _needsMeshRebuild = false;
            }
        }

        private void CreateSphereMesh()
        {
            if (Segments < 3) Segments = 3;
            if (Rings < 2) Rings = 2;

            List<float> vertices = new List<float>();
            List<uint> indices = new List<uint>();

            // Generate vertices
            for (int ring = 0; ring <= Rings; ring++)
            {
                float phi = MathHelper.Pi * ring / Rings;
                float y = (float)Math.Cos(phi);
                float ringRadius = (float)Math.Sin(phi);

                for (int segment = 0; segment <= Segments; segment++)
                {
                    float theta = 2.0f * MathHelper.Pi * segment / Segments;
                    float x = ringRadius * (float)Math.Cos(theta);
                    float z = ringRadius * (float)Math.Sin(theta);

                    // Position
                    Vector3 position = new Vector3(x, y, z) * Radius;

                    // Normal (invertida si InvertNormals está activo)
                    Vector3 normal = InvertNormals ? -new Vector3(x, y, z) : new Vector3(x, y, z);
                    normal.Normalize();

                    // UV coordinates (equirectangular mapping)
                    float u = (float)segment / Segments;
                    float v = 1.0f - ((float)ring / Rings); // Flipear en Y

                    // Apply texture offset and scale
                    u = u * TextureScale.X + TextureOffset.X;
                    v = v * TextureScale.Y + TextureOffset.Y;

                    // Tangent and Bitangent (for normal mapping if needed)
                    Vector3 tangent = new Vector3(-(float)Math.Sin(theta), 0, (float)Math.Cos(theta));
                    tangent.Normalize();

                    Vector3 bitangent = Vector3.Cross(normal, tangent);
                    bitangent.Normalize();

                    // Add vertex data: Position(3) + Normal(3) + UV(2) + Tangent(3) + Bitangent(3)
                    vertices.Add(position.X);
                    vertices.Add(position.Y);
                    vertices.Add(position.Z);

                    vertices.Add(normal.X);
                    vertices.Add(normal.Y);
                    vertices.Add(normal.Z);

                    vertices.Add(u);
                    vertices.Add(v);

                    vertices.Add(tangent.X);
                    vertices.Add(tangent.Y);
                    vertices.Add(tangent.Z);

                    vertices.Add(bitangent.X);
                    vertices.Add(bitangent.Y);
                    vertices.Add(bitangent.Z);
                }
            }

            // Generate indices
            for (int ring = 0; ring < Rings; ring++)
            {
                for (int segment = 0; segment < Segments; segment++)
                {
                    uint current = (uint)(ring * (Segments + 1) + segment);
                    uint next = current + (uint)Segments + 1;

                    if (InvertNormals)
                    {
                        // Invertir el orden de los triángulos
                        indices.Add(current);
                        indices.Add(current + 1);
                        indices.Add(next);

                        indices.Add(current + 1);
                        indices.Add(next + 1);
                        indices.Add(next);
                    }
                    else
                    {
                        indices.Add(current);
                        indices.Add(next);
                        indices.Add(current + 1);

                        indices.Add(current + 1);
                        indices.Add(next);
                        indices.Add(next + 1);
                    }
                }
            }

            // Dispose old mesh and model
            _sphereMesh?.Dispose();
            _sphereModel?.Dispose();

            // Create new mesh and model
            _sphereMesh = new Mesh(vertices.ToArray(), indices.ToArray());
            _sphereModel = new Model();

            var subMeshInfo = new SubMeshInfo(_sphereMesh, 0, new MaterialTextureInfo());
            _sphereModel._subMeshes.Add(subMeshInfo);

            var combinedMeshField = typeof(Model).GetField("_combinedMesh",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            combinedMeshField?.SetValue(_sphereModel, _sphereMesh);

            // Configure texture wrap mode
            if (_material != null && _material.AlbedoTexture != null)
            {
                GL.BindTexture(TextureTarget.Texture2D, (int)_material.AlbedoTexture.TextureId);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
                GL.BindTexture(TextureTarget.Texture2D, 0);
            }
        }

        private void OnMaterialPathChanged()
        {
            if (_isInitialized)
            {
                if (!string.IsNullOrEmpty(MaterialPath))
                {
                    LoadMaterialFromPath(MaterialPath);
                }
                else
                {
                    _material = null;
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
                    _needsMeshRebuild = true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SkyboxRenderer] Error al cargar material '{path}': {ex.Message}");
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
                Console.WriteLine($"[SkyboxRenderer] Error leyendo dimensiones: {ex.Message}");
                _textureWidth = 0;
                _textureHeight = 0;
            }
        }

        public void SetMaterial(Material material)
        {
            _material = material;
            _materialPath = material?.Name ?? "";

            if (_material != null)
            {
                ReadTextureDimensionsFromMaterial();
                _needsMeshRebuild = true;
            }
        }

        public void SetTextureOffset(float u, float v)
        {
            TextureOffset = new Vector2(u, v);
            _needsMeshRebuild = true;
        }

        public void SetTextureScale(float scaleU, float scaleV)
        {
            TextureScale = new Vector2(
                Math.Max(0.001f, scaleU),
                Math.Max(0.001f, scaleV)
            );
            _needsMeshRebuild = true;
        }

        public void SetSphereDetail(int segments, int rings)
        {
            Segments = Math.Max(3, segments);
            Rings = Math.Max(2, rings);
            _needsMeshRebuild = true;
        }

        public Matrix4 GetRotationMatrix()
        {
            return Matrix4.CreateRotationY(MathHelper.DegreesToRadians(_currentRotation));
        }

        public void DebugStatus()
        {
            Console.WriteLine("=== SKYBOX RENDERER DEBUG ===");
            Console.WriteLine($"GameObject: {GameObject?.Name ?? "NULL"}");
            Console.WriteLine($"Enabled: {Enabled}");
            Console.WriteLine($"Material: {(_material != null ? "OK" : "NULL")}");
            Console.WriteLine($"SphereModel: {(_sphereModel != null ? "OK" : "NULL")}");
            Console.WriteLine($"SphereMesh: {(_sphereMesh != null ? "OK" : "NULL")}");
            Console.WriteLine($"Texture Size: {_textureWidth}x{_textureHeight}");
            Console.WriteLine($"Radius: {Radius}");
            Console.WriteLine($"Segments: {Segments}, Rings: {Rings}");
            Console.WriteLine($"Invert Normals: {InvertNormals}");
            Console.WriteLine($"Texture Offset: {TextureOffset}");
            Console.WriteLine($"Texture Scale: {TextureScale}");
            Console.WriteLine($"Rotation Speed: {RotationSpeed}°/s");
            Console.WriteLine($"Current Rotation: {_currentRotation}°");
            Console.WriteLine($"ReadyToRender: {_isReadyToRender}");

            if (_sphereModel != null)
            {
                Console.WriteLine($"Model.SubMeshCount: {_sphereModel.SubMeshCount}");
                Console.WriteLine($"Model._subMeshes.Count: {_sphereModel._subMeshes?.Count ?? 0}");
            }

            if (_sphereMesh != null)
            {
                Console.WriteLine($"Mesh.VAO: {_sphereMesh.GetVAO()}");
                Console.WriteLine($"Mesh.IndexCount: {_sphereMesh.IndexCount}");
                Console.WriteLine($"Mesh.VertexCount: {_sphereMesh.VertexCount}");
            }

            Console.WriteLine("============================");
        }

        public void Draw()
        {
            if (_sphereModel != null)
                _sphereModel.Draw();
        }

        public override void OnDestroy()
        {
            GraphicsEngine.Instance?.GetSceneRenderer()?.UnregisterRenderer(this);

            _material = null;
            _sphereMesh?.Dispose();
            _sphereMesh = null;
            _sphereModel?.Dispose();
            _sphereModel = null;
            _textureWidth = 0;
            _textureHeight = 0;
        }
    }
}