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

    // =========================================================================
    //  Datos crudos de un sub-mesh ANTES de crear el Mesh / subir a GPU
    // =========================================================================
    internal class RawSubMesh
    {
        public float[] Vertices;   // stride 14 floats
        public uint[] Indices;
        public int MaterialIndex;
        public MaterialTextureInfo TextureInfo;
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

        private static readonly HashSet<string> _formatsNeedingNormalize = new(StringComparer.OrdinalIgnoreCase)
        {
            ".fbx"
        };

        private static string NormalizeExtension(string extension)
            => extension.StartsWith('.') ? extension : "." + extension;

        private static PostProcessSteps GetPostProcessFlags(string extension)
        {
            extension = NormalizeExtension(extension);

            var flags = PostProcessSteps.Triangulate
                      | PostProcessSteps.CalculateTangentSpace
                      | PostProcessSteps.JoinIdenticalVertices
                      | PostProcessSteps.GenerateSmoothNormals
                      | PostProcessSteps.FixInFacingNormals
                      | PostProcessSteps.OptimizeMeshes
                      | PostProcessSteps.PreTransformVertices;

            if (_formatsNeedingFlipUVs.Contains(extension))
                flags |= PostProcessSteps.FlipUVs;

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

        public int GetVAO() => _combinedMesh?.GetVAO() ?? 0;

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

        // =====================================================================
        //  Load / LoadFromBytes
        // =====================================================================

        public static Model Load(string path)
        {
            var model = new Model();
            path = AssetManager.BasePath + path;
            string ext = System.IO.Path.GetExtension(path);

            var context = new AssimpContext();
            Scene scene = context.ImportFile(path, GetPostProcessFlags(ext));

            if (scene == null || scene.SceneFlags.HasFlag(SceneFlags.Incomplete) || scene.RootNode == null)
                throw new Exception($"Error loading model: {path}");

            model.ProcessMaterials(scene, path);
            model.BuildFromScene(scene, ext);
            return model;
        }

        public static Model LoadFromBytes(byte[] data, string extension)
        {
            var model = new Model();
            var context = new AssimpContext();

            using var ms = new MemoryStream(data);
            Scene scene = context.ImportFileFromStream(ms, GetPostProcessFlags(extension), extension);

            if (scene == null || scene.SceneFlags.HasFlag(SceneFlags.Incomplete) || scene.RootNode == null)
                throw new Exception("Error loading model from memory");

            model.ProcessMaterials(scene, "InMemory:" + extension);
            model.BuildFromScene(scene, extension);
            return model;
        }

        // =====================================================================
        //  Pipeline principal
        //  ORDEN CRÍTICO:
        //    1. Recopilar datos crudos de Assimp  → solo float[], sin GPU
        //    2. Normalizar vértices si hace falta → solo float[], sin GPU
        //    3. Crear Mesh (sube a GPU)
        //    4. CombineSubMeshes
        //    5. CalculateAABB
        // =====================================================================

        private void BuildFromScene(Scene scene, string extension)
        {
            extension = NormalizeExtension(extension);

            // 1. Datos crudos
            var rawList = new List<RawSubMesh>();
            CollectRaw(scene.RootNode, scene, rawList);

            // 2. Normalizar ANTES de crear ningún objeto GPU
            if (_formatsNeedingNormalize.Contains(extension))
                NormalizeRaw(rawList);

            // 3. Crear Mesh (sube a GPU con los datos ya normalizados)
            foreach (var raw in rawList)
            {
                var mesh = new Mesh(raw.Vertices, raw.Indices);
                _subMeshes.Add(new SubMeshInfo(mesh, raw.MaterialIndex, raw.TextureInfo));
            }

            // 4 & 5
            CombineSubMeshes();
            CalculateAABB();
        }

        // =====================================================================
        //  Recopilación de datos crudos (no crea ningún objeto GPU)
        // =====================================================================

        private void CollectRaw(Node node, Scene scene, List<RawSubMesh> rawList)
        {
            for (int i = 0; i < node.MeshCount; i++)
            {
                Assimp.Mesh assimpMesh = scene.Meshes[node.MeshIndices[i]];
                int matIdx = assimpMesh.MaterialIndex;

                MaterialTextureInfo texInfo = (matIdx >= 0 && matIdx < _materials.Count)
                    ? _materials[matIdx]
                    : new MaterialTextureInfo();

                rawList.Add(new RawSubMesh
                {
                    Vertices = ExtractVertices(assimpMesh),
                    Indices = ExtractIndices(assimpMesh),
                    MaterialIndex = matIdx,
                    TextureInfo = texInfo
                });
            }

            for (int i = 0; i < node.ChildCount; i++)
                CollectRaw(node.Children[i], scene, rawList);
        }

        private static float[] ExtractVertices(Assimp.Mesh mesh)
        {
            var verts = new List<float>(mesh.VertexCount * 14);

            for (int i = 0; i < mesh.VertexCount; i++)
            {
                verts.Add(mesh.Vertices[i].X);
                verts.Add(mesh.Vertices[i].Y);
                verts.Add(mesh.Vertices[i].Z);

                if (mesh.HasNormals)
                { verts.Add(mesh.Normals[i].X); verts.Add(mesh.Normals[i].Y); verts.Add(mesh.Normals[i].Z); }
                else
                { verts.Add(0f); verts.Add(1f); verts.Add(0f); }

                if (mesh.HasTextureCoords(0))
                { verts.Add(mesh.TextureCoordinateChannels[0][i].X); verts.Add(mesh.TextureCoordinateChannels[0][i].Y); }
                else
                { verts.Add(0f); verts.Add(0f); }

                if (mesh.HasTangentBasis)
                { verts.Add(mesh.Tangents[i].X); verts.Add(mesh.Tangents[i].Y); verts.Add(mesh.Tangents[i].Z); }
                else
                { verts.Add(1f); verts.Add(0f); verts.Add(0f); }

                if (mesh.HasTangentBasis)
                { verts.Add(mesh.BiTangents[i].X); verts.Add(mesh.BiTangents[i].Y); verts.Add(mesh.BiTangents[i].Z); }
                else
                { verts.Add(0f); verts.Add(1f); verts.Add(0f); }
            }

            return verts.ToArray();
        }

        private static uint[] ExtractIndices(Assimp.Mesh mesh)
        {
            var idxs = new List<uint>(mesh.FaceCount * 3);
            for (int i = 0; i < mesh.FaceCount; i++)
            {
                Face face = mesh.Faces[i];
                for (int j = 0; j < face.IndexCount; j++)
                    idxs.Add((uint)face.Indices[j]);
            }
            return idxs.ToArray();
        }

        // =====================================================================
        //  Normalización sobre datos crudos (CPU puro, sin GPU)
        // =====================================================================

        /// <summary>
        /// Calcula el AABB global de todos los raw sub-meshes, luego centra y escala
        /// todos los vértices (solo componente XYZ de posición) para que el modelo
        /// quepa en un cubo de 1 unidad centrado en el origen.
        /// Se ejecuta ANTES de crear cualquier Mesh / objeto OpenGL.
        /// </summary>
        private static void NormalizeRaw(List<RawSubMesh> rawList)
        {
            if (rawList.Count == 0) return;

            const int stride = 14;

            // Paso 1: AABB global sobre todos los raw meshes
            var min = new Vector3(float.MaxValue);
            var max = new Vector3(float.MinValue);

            foreach (var raw in rawList)
            {
                for (int i = 0; i < raw.Vertices.Length; i += stride)
                {
                    var p = new Vector3(raw.Vertices[i], raw.Vertices[i + 1], raw.Vertices[i + 2]);
                    min = Vector3.ComponentMin(min, p);
                    max = Vector3.ComponentMax(max, p);
                }
            }

            Vector3 size = max - min;
            Vector3 center = (min + max) * 0.5f;
            float longest = MathF.Max(size.X, MathF.Max(size.Y, size.Z));

            if (longest < float.Epsilon) return;

            float scale = 1.0f / longest;

            Console.WriteLine($"[Model] NormalizeRaw → size={size} | longest={longest:F4} | scale={scale:F6}");

            // Paso 2: aplicar a TODOS los raw meshes (solo offset 0-1-2 = posición)
            foreach (var raw in rawList)
            {
                for (int i = 0; i < raw.Vertices.Length; i += stride)
                {
                    raw.Vertices[i + 0] = (raw.Vertices[i + 0] - center.X) * scale;
                    raw.Vertices[i + 1] = (raw.Vertices[i + 1] - center.Y) * scale;
                    raw.Vertices[i + 2] = (raw.Vertices[i + 2] - center.Z) * scale;
                    // offsets 3-5 = normales  → no se escalan (escala uniforme)
                    // offsets 6-7 = UVs       → no se tocan
                    // offsets 8-13= tan/bitan  → vectores de dirección, no se tocan
                }
            }
        }

        // =====================================================================
        //  CombineSubMeshes
        // =====================================================================

        private void CombineSubMeshes()
        {
            if (_subMeshes.Count == 0) return;

            var combinedVerts = new List<float>();
            var combinedIndices = new List<uint>();
            int baseVert = 0;
            int baseIndex = 0;

            for (int i = 0; i < _subMeshes.Count; i++)
            {
                var sub = _subMeshes[i];
                var mesh = sub.Mesh;

                float[] verts = mesh.GetVertices();
                uint[] idxs = mesh.GetIndices();

                sub.BaseVertex = baseVert;
                sub.BaseIndex = baseIndex;
                sub.IndexCount = idxs.Length;

                combinedVerts.AddRange(verts);
                foreach (var idx in idxs)
                    combinedIndices.Add(idx);

                baseVert += mesh.VertexCount;
                baseIndex += idxs.Length;
            }

            _combinedMesh = new Mesh(combinedVerts.ToArray(), combinedIndices.ToArray());
        }

        // =====================================================================
        //  AABB
        // =====================================================================

        private void CalculateAABB()
        {
            if (_subMeshes.Count == 0)
            {
                _aabb = new Box3(Vector3.Zero, Vector3.Zero);
                _aabbCalculated = true;
                return;
            }

            var min = new Vector3(float.MaxValue);
            var max = new Vector3(float.MinValue);

            foreach (var sub in _subMeshes)
            {
                Box3 aabb = sub.Mesh.GetAABB();
                min = Vector3.ComponentMin(min, aabb.Min);
                max = Vector3.ComponentMax(max, aabb.Max);
            }

            _aabb = new Box3(min, max);
            _aabbCalculated = true;
        }

        public Box3 GetTransformedAABB(Matrix4 transform)
        {
            if (!_aabbCalculated)
                CalculateAABB();

            Vector3[] corners =
            {
                new(_aabb.Min.X, _aabb.Min.Y, _aabb.Min.Z),
                new(_aabb.Max.X, _aabb.Min.Y, _aabb.Min.Z),
                new(_aabb.Min.X, _aabb.Max.Y, _aabb.Min.Z),
                new(_aabb.Max.X, _aabb.Max.Y, _aabb.Min.Z),
                new(_aabb.Min.X, _aabb.Min.Y, _aabb.Max.Z),
                new(_aabb.Max.X, _aabb.Min.Y, _aabb.Max.Z),
                new(_aabb.Min.X, _aabb.Max.Y, _aabb.Max.Z),
                new(_aabb.Max.X, _aabb.Max.Y, _aabb.Max.Z)
            };

            var min = new Vector3(float.MaxValue);
            var max = new Vector3(float.MinValue);

            for (int i = 0; i < 8; i++)
            {
                Vector3 t = (new Vector4(corners[i], 1.0f) * transform).Xyz;
                min = Vector3.ComponentMin(min, t);
                max = Vector3.ComponentMax(max, t);
            }

            return new Box3(min, max);
        }

        // =====================================================================
        //  ProcessMaterials (sin cambios)
        // =====================================================================

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
                    AssignUnknownTexture(textureInfo, ResolveTexturePath(slot.FilePath, modelDirectory));
                }

                _materials.Add(textureInfo);
            }
        }

        private static void AssignUnknownTexture(MaterialTextureInfo info, string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            string name = System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant();

            if (string.IsNullOrEmpty(info.MetallicTexture) && (name.Contains("metallic") || name.EndsWith("_metal") || name.EndsWith("_m")))
                info.MetallicTexture = path;
            else if (string.IsNullOrEmpty(info.RoughnessTexture) && (name.Contains("roughness") || name.EndsWith("_rough") || name.EndsWith("_r")))
                info.RoughnessTexture = path;
            else if (string.IsNullOrEmpty(info.AmbientOcclusionTexture) && (name.Contains("_ao") || name.Contains("occlusion")))
                info.AmbientOcclusionTexture = path;
            else if (string.IsNullOrEmpty(info.NormalTexture) && (name.Contains("normal") || name.EndsWith("_nrm") || name.EndsWith("_n")))
                info.NormalTexture = path;
            else if (string.IsNullOrEmpty(info.EmissiveTexture) && (name.Contains("emissive") || name.EndsWith("_emit") || name.EndsWith("_e")))
                info.EmissiveTexture = path;
            else if (string.IsNullOrEmpty(info.DiffuseTexture) && (name.Contains("albedo") || name.Contains("diffuse") || name.EndsWith("_d")))
            {
                Console.WriteLine(path);
                info.DiffuseTexture = path;
            }
        }

        private string ResolveTexturePath(string filePath, string modelDirectory)
        {
            if (string.IsNullOrEmpty(filePath)) return null;

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

        // =====================================================================
        //  Draw / Instancing / Dispose
        // =====================================================================

        public void SetupInstancing(Matrix4[] instanceMatrices) => _combinedMesh?.SetupInstancing(instanceMatrices);
        public void ClearInstancing() => _combinedMesh?.ClearInstancing();
        public void Draw() => _combinedMesh?.Draw();
        public void DrawInstanced(int instanceCount) => _combinedMesh?.DrawInstanced(instanceCount);

        public void DrawSubMesh(int index)
        {
            if (index >= 0 && index < _subMeshes.Count)
                _subMeshes[index].Mesh.Draw();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                foreach (var sub in _subMeshes)
                    sub.Mesh?.Dispose();
                _subMeshes.Clear();
                _materials.Clear();
                _combinedMesh?.Dispose();
                _disposed = true;
            }
        }
    }
}