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
        public int BaseVertex { get; set; }
        public int BaseIndex { get; set; }
        public int IndexCount { get; set; }

        public SubMeshInfo(Mesh mesh, int materialIndex, MaterialTextureInfo textureInfo)
        {
            Mesh = mesh;
            MaterialIndex = materialIndex;
            TextureInfo = textureInfo ?? new MaterialTextureInfo();
            BaseVertex = 0;
            BaseIndex = 0;
            IndexCount = mesh?.IndexCount ?? 0;
        }
    }

    public class Model : IDisposable
    {
        public List<SubMeshInfo> _subMeshes = new();
        private List<MaterialTextureInfo> _materials = new();
        private bool _disposed = false;
        private Box3 _aabb;
        private bool _aabbCalculated = false;
        private Mesh _combinedMesh;

        public int SubMeshCount => _subMeshes.Count;
        public int MaterialCount => _materials.Count;

        private static readonly HashSet<string> _formatsNeedingFlipUVs = new(StringComparer.OrdinalIgnoreCase)
        {
            ".obj", ".fbx", ".3ds", ".dae", ".blend", ".ply", ".stl", ".x", ".lwo", ".lws"
        };

        private static readonly HashSet<string> _formatsNeedingFlipWinding = new(StringComparer.OrdinalIgnoreCase)
        {
            ".fbx"
        };

        private static PostProcessSteps GetPostProcessFlags(string extension)
        {
            var flags = PostProcessSteps.Triangulate
                      | PostProcessSteps.CalculateTangentSpace
                      | PostProcessSteps.JoinIdenticalVertices
                      | PostProcessSteps.GenerateSmoothNormals
                      | PostProcessSteps.OptimizeMeshes
                      | PostProcessSteps.PreTransformVertices;

            if (_formatsNeedingFlipUVs.Contains(extension))
                flags |= PostProcessSteps.FlipUVs;

            if (_formatsNeedingFlipWinding.Contains(extension))
                flags |= PostProcessSteps.FlipWindingOrder;

            return flags;
        }

        public Box3 AABB
        {
            get
            {
                if (!_aabbCalculated)
                    CalculateAABB();
                return _aabb;
            }
        }

        public int GetMaterialIndex(int subMeshIndex)
        {
            if (subMeshIndex >= 0 && subMeshIndex < _subMeshes.Count)
                return _subMeshes[subMeshIndex].MaterialIndex;
            return 0;
        }

        public MaterialTextureInfo GetSubMeshTextureInfo(int subMeshIndex)
        {
            if (subMeshIndex >= 0 && subMeshIndex < _subMeshes.Count)
                return _subMeshes[subMeshIndex].TextureInfo;
            return new MaterialTextureInfo();
        }

        public MaterialTextureInfo GetMaterialTextureInfo(int materialIndex)
        {
            if (materialIndex >= 0 && materialIndex < _materials.Count)
                return _materials[materialIndex];
            return new MaterialTextureInfo();
        }

        public int GetVAO()
        {
            return _combinedMesh?.GetVAO() ?? 0;
        }

        public int GetSubmeshBaseVertex(int subMeshIndex)
        {
            if (subMeshIndex >= 0 && subMeshIndex < _subMeshes.Count)
                return _subMeshes[subMeshIndex].BaseVertex;
            return 0;
        }

        public int GetSubmeshBaseIndex(int subMeshIndex)
        {
            if (subMeshIndex >= 0 && subMeshIndex < _subMeshes.Count)
                return _subMeshes[subMeshIndex].BaseIndex;
            return 0;
        }

        public int GetSubmeshIndexCount(int subMeshIndex)
        {
            if (subMeshIndex >= 0 && subMeshIndex < _subMeshes.Count)
                return _subMeshes[subMeshIndex].IndexCount;
            return 0;
        }

        public static Model Load(string path)
        {
            var model = new Model();
            path = AssetManager.BasePath + path;

            string extension = System.IO.Path.GetExtension(path);

            AssimpContext importer = new AssimpContext();
            Scene scene = importer.ImportFile(path, GetPostProcessFlags(extension));

            if (scene == null || scene.SceneFlags.HasFlag(SceneFlags.Incomplete) || scene.RootNode == null)
                throw new Exception($"Error loading model: {path}");

            model.ProcessMaterials(scene, path);
            model.ProcessNode(scene.RootNode, scene);
            model.CombineSubMeshes();
            model.CalculateAABB();

            return model;
        }

        public static Model LoadFromBytes(byte[] data, string extension)
        {
            var model = new Model();

            AssimpContext importer = new AssimpContext();

            using MemoryStream ms = new MemoryStream(data);

            Scene scene = importer.ImportFileFromStream(ms, GetPostProcessFlags(extension), extension);

            if (scene == null || scene.SceneFlags.HasFlag(SceneFlags.Incomplete) || scene.RootNode == null)
                throw new Exception("Error loading model from memory");

            model.ProcessMaterials(scene, "InMemory:" + extension);
            model.ProcessNode(scene.RootNode, scene);
            model.CombineSubMeshes();
            model.CalculateAABB();

            return model;
        }

        private void ProcessMaterials(Scene scene, string modelPath)
        {
            string modelDirectory = System.IO.Path.GetDirectoryName(modelPath) ?? "";

            for (int i = 0; i < scene.MaterialCount; i++)
            {
                var material = scene.Materials[i];
                var textureInfo = new MaterialTextureInfo();

                foreach (var slot in material.GetMaterialTextures(TextureType.Diffuse))
                    if (string.IsNullOrEmpty(textureInfo.DiffuseTexture))
                        textureInfo.DiffuseTexture = ResolveTexturePath(slot.FilePath, modelDirectory);

                foreach (var slot in material.GetMaterialTextures(TextureType.Normals))
                    if (string.IsNullOrEmpty(textureInfo.NormalTexture))
                        textureInfo.NormalTexture = ResolveTexturePath(slot.FilePath, modelDirectory);

                if (string.IsNullOrEmpty(textureInfo.NormalTexture))
                    foreach (var slot in material.GetMaterialTextures(TextureType.Height))
                        if (string.IsNullOrEmpty(textureInfo.NormalTexture))
                            textureInfo.NormalTexture = ResolveTexturePath(slot.FilePath, modelDirectory);

                foreach (var slot in material.GetMaterialTextures(TextureType.Specular))
                    if (string.IsNullOrEmpty(textureInfo.SpecularTexture))
                        textureInfo.SpecularTexture = ResolveTexturePath(slot.FilePath, modelDirectory);

                foreach (var slot in material.GetMaterialTextures(TextureType.Reflection))
                    if (string.IsNullOrEmpty(textureInfo.MetallicTexture))
                        textureInfo.MetallicTexture = ResolveTexturePath(slot.FilePath, modelDirectory);

                foreach (var slot in material.GetMaterialTextures(TextureType.Shininess))
                    if (string.IsNullOrEmpty(textureInfo.RoughnessTexture))
                        textureInfo.RoughnessTexture = ResolveTexturePath(slot.FilePath, modelDirectory);

                foreach (var slot in material.GetMaterialTextures(TextureType.Ambient))
                    if (string.IsNullOrEmpty(textureInfo.AmbientOcclusionTexture))
                        textureInfo.AmbientOcclusionTexture = ResolveTexturePath(slot.FilePath, modelDirectory);

                foreach (var slot in material.GetMaterialTextures(TextureType.Lightmap))
                    if (string.IsNullOrEmpty(textureInfo.AmbientOcclusionTexture))
                        textureInfo.AmbientOcclusionTexture = ResolveTexturePath(slot.FilePath, modelDirectory);

                foreach (var slot in material.GetMaterialTextures(TextureType.Emissive))
                    if (string.IsNullOrEmpty(textureInfo.EmissiveTexture))
                        textureInfo.EmissiveTexture = ResolveTexturePath(slot.FilePath, modelDirectory);

                foreach (var slot in material.GetMaterialTextures(TextureType.Unknown))
                {
                    if (string.IsNullOrEmpty(slot.FilePath)) continue;
                    string resolved = ResolveTexturePath(slot.FilePath, modelDirectory);
                    AssignUnknownTexture(textureInfo, resolved);
                }

                _materials.Add(textureInfo);
            }
        }

        private static void AssignUnknownTexture(MaterialTextureInfo info, string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            string name = System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant();

            if (string.IsNullOrEmpty(info.MetallicTexture) &&
                (name.EndsWith("_metallic") || name.EndsWith("_metal") || name.EndsWith("_m") || name.Contains("metallic")))
            {
                info.MetallicTexture = path;
            }
            else if (string.IsNullOrEmpty(info.RoughnessTexture) &&
                (name.EndsWith("_roughness") || name.EndsWith("_rough") || name.EndsWith("_r") || name.Contains("roughness")))
            {
                info.RoughnessTexture = path;
            }
            else if (string.IsNullOrEmpty(info.AmbientOcclusionTexture) &&
                (name.EndsWith("_ao") || name.EndsWith("_ambientocclusion") || name.EndsWith("_occlusion") || name.Contains("_ao")))
            {
                info.AmbientOcclusionTexture = path;
            }
            else if (string.IsNullOrEmpty(info.NormalTexture) &&
                (name.EndsWith("_normal") || name.EndsWith("_nrm") || name.EndsWith("_n") || name.Contains("normal")))
            {
                info.NormalTexture = path;
            }
            else if (string.IsNullOrEmpty(info.EmissiveTexture) &&
                (name.EndsWith("_emissive") || name.EndsWith("_emit") || name.EndsWith("_e") || name.Contains("emissive")))
            {
                info.EmissiveTexture = path;
            }
            else if (string.IsNullOrEmpty(info.DiffuseTexture) &&
                (name.EndsWith("_diffuse") || name.EndsWith("_albedo") || name.EndsWith("_d") || name.Contains("albedo") || name.Contains("diffuse")))
            {
                Console.WriteLine(path);
                info.DiffuseTexture = path;
            }
        }

        private string ResolveTexturePath(string filePath, string modelDirectory)
        {
            if (string.IsNullOrEmpty(filePath))
                return null;

            string texturePath = filePath.Replace("\\", "/");

            if (System.IO.Path.IsPathRooted(texturePath))
                texturePath = System.IO.Path.GetFileName(texturePath);

            string fullPath = System.IO.Path.Combine(modelDirectory, texturePath).Replace("\\", "/");

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
                Assimp.Mesh assimpMesh = scene.Meshes[node.MeshIndices[i]];
                var processedMesh = ProcessMesh(assimpMesh);
                int materialIndex = assimpMesh.MaterialIndex;

                MaterialTextureInfo textureInfo = (materialIndex >= 0 && materialIndex < _materials.Count)
                    ? _materials[materialIndex]
                    : null;

                _subMeshes.Add(new SubMeshInfo(processedMesh, materialIndex, textureInfo));
            }

            for (int i = 0; i < node.ChildCount; i++)
                ProcessNode(node.Children[i], scene);
        }

        private Mesh ProcessMesh(Assimp.Mesh mesh)
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
                    vertices.Add(0f); vertices.Add(1f); vertices.Add(0f);
                }

                if (mesh.HasTextureCoords(0))
                {
                    vertices.Add(mesh.TextureCoordinateChannels[0][i].X);
                    vertices.Add(mesh.TextureCoordinateChannels[0][i].Y);
                }
                else
                {
                    vertices.Add(0f); vertices.Add(0f);
                }

                if (mesh.HasTangentBasis)
                {
                    vertices.Add(mesh.Tangents[i].X);
                    vertices.Add(mesh.Tangents[i].Y);
                    vertices.Add(mesh.Tangents[i].Z);
                }
                else
                {
                    vertices.Add(1f); vertices.Add(0f); vertices.Add(0f);
                }

                if (mesh.HasTangentBasis)
                {
                    vertices.Add(mesh.BiTangents[i].X);
                    vertices.Add(mesh.BiTangents[i].Y);
                    vertices.Add(mesh.BiTangents[i].Z);
                }
                else
                {
                    vertices.Add(0f); vertices.Add(1f); vertices.Add(0f);
                }
            }

            for (int i = 0; i < mesh.FaceCount; i++)
            {
                Face face = mesh.Faces[i];
                for (int j = 0; j < face.IndexCount; j++)
                    indices.Add((uint)face.Indices[j]);
            }

            return new Mesh(vertices.ToArray(), indices.ToArray());
        }

        private void CombineSubMeshes()
        {
            if (_subMeshes.Count == 0)
                return;

            List<float> combinedVertices = new List<float>();
            List<uint> combinedIndices = new List<uint>();

            int currentBaseVertex = 0;
            int currentBaseIndex = 0;

            for (int i = 0; i < _subMeshes.Count; i++)
            {
                var subMesh = _subMeshes[i];
                var mesh = subMesh.Mesh;

                var verts = mesh.GetVertices();
                var idxs = mesh.GetIndices();

                subMesh.BaseVertex = currentBaseVertex;
                subMesh.BaseIndex = currentBaseIndex;
                subMesh.IndexCount = idxs.Length;

                combinedVertices.AddRange(verts);

                foreach (var idx in idxs)
                    combinedIndices.Add(idx);

                currentBaseVertex += mesh.VertexCount;
                currentBaseIndex += idxs.Length;
            }

            _combinedMesh = new Mesh(combinedVertices.ToArray(), combinedIndices.ToArray());
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
                CalculateAABB();

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
            _combinedMesh?.SetupInstancing(instanceMatrices);
        }

        public void ClearInstancing()
        {
            _combinedMesh?.ClearInstancing();
        }

        public void Draw()
        {
            _combinedMesh?.Draw();
        }

        public void DrawInstanced(int instanceCount)
        {
            _combinedMesh?.DrawInstanced(instanceCount);
        }

        public void DrawSubMesh(int index)
        {
            if (index >= 0 && index < _subMeshes.Count)
                _subMeshes[index].Mesh.Draw();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                foreach (var subMeshInfo in _subMeshes)
                    subMeshInfo.Mesh?.Dispose();
                _subMeshes.Clear();
                _materials.Clear();
                _combinedMesh?.Dispose();
                _disposed = true;
            }
        }
    }
}