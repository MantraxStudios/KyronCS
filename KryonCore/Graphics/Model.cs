using Assimp;
using KrayonCore.Core.Attributes;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace KrayonCore
{
    public class MaterialTextureInfo
    {
        public string DiffuseTexture { get; set; }
        public string NormalTexture { get; set; }
        public string SpecularTexture { get; set; }
        public string MetallicTexture { get; set; }
        public string RoughnessTexture { get; set; }
        public string AmbientOcclusionTexture { get; set; }
        public string EmissiveTexture { get; set; }

        public bool HasAnyTexture()
        {
            return !string.IsNullOrEmpty(DiffuseTexture) ||
                   !string.IsNullOrEmpty(NormalTexture) ||
                   !string.IsNullOrEmpty(SpecularTexture) ||
                   !string.IsNullOrEmpty(MetallicTexture) ||
                   !string.IsNullOrEmpty(RoughnessTexture) ||
                   !string.IsNullOrEmpty(AmbientOcclusionTexture) ||
                   !string.IsNullOrEmpty(EmissiveTexture);
        }

        public override string ToString()
        {
            var textures = new List<string>();
            if (!string.IsNullOrEmpty(DiffuseTexture)) textures.Add($"Diffuse: {DiffuseTexture}");
            if (!string.IsNullOrEmpty(NormalTexture)) textures.Add($"Normal: {NormalTexture}");
            if (!string.IsNullOrEmpty(SpecularTexture)) textures.Add($"Specular: {SpecularTexture}");
            if (!string.IsNullOrEmpty(MetallicTexture)) textures.Add($"Metallic: {MetallicTexture}");
            if (!string.IsNullOrEmpty(RoughnessTexture)) textures.Add($"Roughness: {RoughnessTexture}");
            if (!string.IsNullOrEmpty(AmbientOcclusionTexture)) textures.Add($"AO: {AmbientOcclusionTexture}");
            if (!string.IsNullOrEmpty(EmissiveTexture)) textures.Add($"Emissive: {EmissiveTexture}");
            return textures.Count > 0 ? string.Join(", ", textures) : "Sin texturas";
        }
    }

    public class SubMeshInfo
    {
        public Mesh Mesh { get; set; }
        public int MaterialIndex { get; set; }
        public MaterialTextureInfo TextureInfo { get; set; }

        public SubMeshInfo(Mesh mesh, int materialIndex, MaterialTextureInfo textureInfo)
        {
            Mesh = mesh;
            MaterialIndex = materialIndex;
            TextureInfo = textureInfo ?? new MaterialTextureInfo();
        }
    }

    public class Model : IDisposable
    {
        public List<SubMeshInfo> _subMeshes = new();
        private List<MaterialTextureInfo> _materials = new();
        private bool _disposed = false;
        private Box3 _aabb;
        private bool _aabbCalculated = false;

        public int SubMeshCount => _subMeshes.Count;
        public int MaterialCount => _materials.Count;

        public Box3 AABB
        {
            get
            {
                if (!_aabbCalculated)
                {
                    CalculateAABB();
                }
                return _aabb;
            }
        }

        // Obtener el material index de un submesh
        public int GetMaterialIndex(int subMeshIndex)
        {
            if (subMeshIndex >= 0 && subMeshIndex < _subMeshes.Count)
            {
                return _subMeshes[subMeshIndex].MaterialIndex;
            }
            return 0;
        }

        // Obtener la información de texturas de un submesh específico
        public MaterialTextureInfo GetSubMeshTextureInfo(int subMeshIndex)
        {
            if (subMeshIndex >= 0 && subMeshIndex < _subMeshes.Count)
            {
                return _subMeshes[subMeshIndex].TextureInfo;
            }
            return new MaterialTextureInfo();
        }

        // Obtener la información de texturas de un material por índice
        public MaterialTextureInfo GetMaterialTextureInfo(int materialIndex)
        {
            if (materialIndex >= 0 && materialIndex < _materials.Count)
            {
                return _materials[materialIndex];
            }
            return new MaterialTextureInfo();
        }

        public static Model Load(string path)
        {
            var model = new Model();
            path = AssetManager.BasePath + path;

            AssimpContext importer = new AssimpContext();
            Scene scene = importer.ImportFile(path,
                PostProcessSteps.Triangulate |
                PostProcessSteps.FlipUVs |
                PostProcessSteps.CalculateTangentSpace);

            if (scene == null || scene.SceneFlags.HasFlag(SceneFlags.Incomplete) || scene.RootNode == null)
            {
                throw new Exception($"Error loading model: {path}");
            }

            // Primero procesar todos los materiales
            model.ProcessMaterials(scene, path);

            // Luego procesar los nodos y meshes
            model.ProcessNode(scene.RootNode, scene);
            model.CalculateAABB();
            
            // Log para debug
            Console.WriteLine($"[Model] ═══════════════════════════════════════");
            Console.WriteLine($"[Model] Modelo cargado: {path}");
            Console.WriteLine($"[Model]   Materiales en archivo: {model.MaterialCount}");
            
            for (int i = 0; i < model.MaterialCount; i++)
            {
                var matInfo = model.GetMaterialTextureInfo(i);
                Console.WriteLine($"[Model]     Material {i}: {matInfo}");
            }
            
            Console.WriteLine($"[Model]   SubMeshes: {model.SubMeshCount}");
            for (int i = 0; i < model.SubMeshCount; i++)
            {
                var texInfo = model.GetSubMeshTextureInfo(i);
                Console.WriteLine($"[Model]     SubMesh {i} -> Material: {model.GetMaterialIndex(i)}");
                Console.WriteLine($"[Model]       Texturas: {texInfo}");
            }
            Console.WriteLine($"[Model] ═══════════════════════════════════════");
            
            return model;
        }

        private void ProcessMaterials(Scene scene, string modelPath)
        {
            string modelDirectory = System.IO.Path.GetDirectoryName(modelPath) ?? "";
            
            Console.WriteLine($"[Model] Procesando {scene.MaterialCount} materiales...");

            for (int i = 0; i < scene.MaterialCount; i++)
            {
                var material = scene.Materials[i];
                var textureInfo = new MaterialTextureInfo();

                // Diffuse/Albedo texture
                if (material.HasTextureDiffuse)
                {
                    textureInfo.DiffuseTexture = GetTexturePath(material.TextureDiffuse, modelDirectory);
                }

                // Normal map
                if (material.HasTextureNormal)
                {
                    textureInfo.NormalTexture = GetTexturePath(material.TextureNormal, modelDirectory);
                }
                // A veces las normales están en TextureHeight
                else if (material.HasTextureHeight)
                {
                    textureInfo.NormalTexture = GetTexturePath(material.TextureHeight, modelDirectory);
                }

                // Specular texture
                if (material.HasTextureSpecular)
                {
                    textureInfo.SpecularTexture = GetTexturePath(material.TextureSpecular, modelDirectory);
                }

                // Metallic texture (puede estar en diferentes slots según el formato)
                if (material.HasTextureReflection)
                {
                    textureInfo.MetallicTexture = GetTexturePath(material.TextureReflection, modelDirectory);
                }

                // Roughness texture
                // if (material.HasTextureShininess)
                // {
                //     textureInfo.RoughnessTexture = GetTexturePath(material.TextureShininess, modelDirectory);
                // }

                // Ambient Occlusion
                if (material.HasTextureAmbient)
                {
                    textureInfo.AmbientOcclusionTexture = GetTexturePath(material.TextureAmbient, modelDirectory);
                }
                else if (material.HasTextureLightMap)
                {
                    textureInfo.AmbientOcclusionTexture = GetTexturePath(material.TextureLightMap, modelDirectory);
                }

                // Emissive texture
                if (material.HasTextureEmissive)
                {
                    textureInfo.EmissiveTexture = GetTexturePath(material.TextureEmissive, modelDirectory);
                }

                _materials.Add(textureInfo);
            }
        }

        private string GetTexturePath(TextureSlot textureSlot, string modelDirectory)
        {
            if (string.IsNullOrEmpty(textureSlot.FilePath))
                return null;

            string texturePath = textureSlot.FilePath;

            // Limpiar la ruta (a veces viene con caracteres raros)
            texturePath = texturePath.Replace("\\", "/");

            // Si la ruta es absoluta, extraer solo el nombre del archivo
            if (System.IO.Path.IsPathRooted(texturePath))
            {
                texturePath = System.IO.Path.GetFileName(texturePath);
            }

            // Combinar con el directorio del modelo
            string fullPath = System.IO.Path.Combine(modelDirectory, texturePath);
            fullPath = fullPath.Replace("\\", "/");

            // Convertir a ruta relativa desde BasePath si es posible
            if (fullPath.StartsWith(AssetManager.BasePath))
            {
                fullPath = fullPath.Substring(AssetManager.BasePath.Length);
                if (fullPath.StartsWith("/"))
                    fullPath = fullPath.Substring(1);
            }

            return fullPath;
        }

        private void ProcessNode(Node node, Scene scene)
        {
            for (int i = 0; i < node.MeshCount; i++)
            {
                Assimp.Mesh mesh = scene.Meshes[node.MeshIndices[i]];
                
                // Procesar el mesh Y obtener su info de material
                var processedMesh = ProcessMesh(mesh, scene);
                int materialIndex = mesh.MaterialIndex;
                
                // Obtener la información de texturas del material
                MaterialTextureInfo textureInfo = null;
                if (materialIndex >= 0 && materialIndex < _materials.Count)
                {
                    textureInfo = _materials[materialIndex];
                }
                
                var subMeshInfo = new SubMeshInfo(processedMesh, materialIndex, textureInfo);
                _subMeshes.Add(subMeshInfo);
            }

            for (int i = 0; i < node.ChildCount; i++)
            {
                ProcessNode(node.Children[i], scene);
            }
        }

        private Mesh ProcessMesh(Assimp.Mesh mesh, Scene scene)
        {
            List<float> vertices = new List<float>();
            List<uint> indices = new List<uint>();

            for (int i = 0; i < mesh.VertexCount; i++)
            {
                vertices.Add(mesh.Vertices[i].X);
                vertices.Add(mesh.Vertices[i].Y);
                vertices.Add(mesh.Vertices[i].Z);

                if (mesh.HasNormals)
                {
                    vertices.Add(mesh.Normals[i].X);
                    vertices.Add(mesh.Normals[i].Y);
                    vertices.Add(mesh.Normals[i].Z);
                }
                else
                {
                    vertices.Add(0f);
                    vertices.Add(0f);
                    vertices.Add(0f);
                }

                if (mesh.HasTextureCoords(0))
                {
                    vertices.Add(mesh.TextureCoordinateChannels[0][i].X);
                    vertices.Add(mesh.TextureCoordinateChannels[0][i].Y);
                }
                else
                {
                    vertices.Add(0f);
                    vertices.Add(0f);
                }

                if (mesh.HasTangentBasis)
                {
                    vertices.Add(mesh.Tangents[i].X);
                    vertices.Add(mesh.Tangents[i].Y);
                    vertices.Add(mesh.Tangents[i].Z);
                }
                else
                {
                    vertices.Add(1f);
                    vertices.Add(0f);
                    vertices.Add(0f);
                }

                if (mesh.HasTangentBasis)
                {
                    vertices.Add(mesh.BiTangents[i].X);
                    vertices.Add(mesh.BiTangents[i].Y);
                    vertices.Add(mesh.BiTangents[i].Z);
                }
                else
                {
                    vertices.Add(0f);
                    vertices.Add(1f);
                    vertices.Add(0f);
                }
            }

            for (int i = 0; i < mesh.FaceCount; i++)
            {
                Face face = mesh.Faces[i];
                for (int j = 0; j < face.IndexCount; j++)
                {
                    indices.Add((uint)face.Indices[j]);
                }
            }

            return new Mesh(vertices.ToArray(), indices.ToArray());
        }

        private void CalculateAABB()
        {
            if (_subMeshes.Count == 0)
            {
                _aabb = new Box3(Vector3.Zero, Vector3.Zero);
                _aabbCalculated = true;
                return;
            }

            Vector3 min = new Vector3(float.MaxValue);
            Vector3 max = new Vector3(float.MinValue);

            foreach (var subMeshInfo in _subMeshes)
            {
                Box3 meshAABB = subMeshInfo.Mesh.GetAABB();
                min = Vector3.ComponentMin(min, meshAABB.Min);
                max = Vector3.ComponentMax(max, meshAABB.Max);
            }

            _aabb = new Box3(min, max);
            _aabbCalculated = true;
        }

        public Box3 GetTransformedAABB(Matrix4 transform)
        {
            if (!_aabbCalculated)
            {
                CalculateAABB();
            }

            Vector3[] corners = new Vector3[8]
            {
                new Vector3(_aabb.Min.X, _aabb.Min.Y, _aabb.Min.Z),
                new Vector3(_aabb.Max.X, _aabb.Min.Y, _aabb.Min.Z),
                new Vector3(_aabb.Min.X, _aabb.Max.Y, _aabb.Min.Z),
                new Vector3(_aabb.Max.X, _aabb.Max.Y, _aabb.Min.Z),
                new Vector3(_aabb.Min.X, _aabb.Min.Y, _aabb.Max.Z),
                new Vector3(_aabb.Max.X, _aabb.Min.Y, _aabb.Max.Z),
                new Vector3(_aabb.Min.X, _aabb.Max.Y, _aabb.Max.Z),
                new Vector3(_aabb.Max.X, _aabb.Max.Y, _aabb.Max.Z)
            };

            Vector3 min = new Vector3(float.MaxValue);
            Vector3 max = new Vector3(float.MinValue);

            for (int i = 0; i < 8; i++)
            {
                Vector3 transformed = (new Vector4(corners[i], 1.0f) * transform).Xyz;
                min = Vector3.ComponentMin(min, transformed);
                max = Vector3.ComponentMax(max, transformed);
            }

            return new Box3(min, max);
        }

        public void SetupInstancing(Matrix4[] instanceMatrices)
        {
            foreach (var subMeshInfo in _subMeshes)
            {
                subMeshInfo.Mesh.SetupInstancing(instanceMatrices);
            }
        }

        public void Draw()
        {
            foreach (var subMeshInfo in _subMeshes)
            {
                subMeshInfo.Mesh.Draw();
            }
        }

        public void DrawInstanced(int instanceCount)
        {
            foreach (var subMeshInfo in _subMeshes)
            {
                subMeshInfo.Mesh.DrawInstanced(instanceCount);
            }
        }

        public void DrawSubMesh(int index)
        {
            if (index >= 0 && index < _subMeshes.Count)
            {
                _subMeshes[index].Mesh.Draw();
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                foreach (var subMeshInfo in _subMeshes)
                {
                    subMeshInfo.Mesh?.Dispose();
                }
                _subMeshes.Clear();
                _materials.Clear();
                _disposed = true;
            }
        }
    }
}