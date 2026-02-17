using Assimp;
using KrayonCore.Animation;
using KrayonCore.Core.Attributes;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.IO;

namespace KrayonCore.Animation
{
    public class AnimatedSubMeshInfo
    {
        public AnimatedMesh Mesh { get; set; }
        public int MaterialIndex { get; set; }
        public MaterialTextureInfo TextureInfo { get; set; }
        public int BaseVertex { get; set; }
        public int BaseIndex { get; set; }
        public int IndexCount { get; set; }

        public AnimatedSubMeshInfo(AnimatedMesh mesh, int materialIndex, MaterialTextureInfo textureInfo)
        {
            Mesh = mesh;
            MaterialIndex = materialIndex;
            TextureInfo = textureInfo ?? new MaterialTextureInfo();
            BaseVertex = 0;
            BaseIndex = 0;
            IndexCount = mesh?.IndexCount ?? 0;
        }
    }

    public class AnimatedModel : IDisposable
    {
        public const int MAX_BONES = 256;

        public List<AnimatedSubMeshInfo> SubMeshes { get; private set; } = new();
        public Dictionary<string, BoneInfo> BoneInfoMap { get; private set; } = new();
        public List<AnimationClip> Animations { get; private set; } = new();
        public int BoneCount { get; private set; } = 0;

        private List<MaterialTextureInfo> _materials = new();
        private bool _disposed = false;
        private Box3 _aabb;
        private bool _aabbCalculated = false;
        private AnimatedMesh _combinedMesh;
        private NodeData _rootNode;
        private Matrix4 _globalInverseTransform = Matrix4.Identity;

        public int SubMeshCount => SubMeshes.Count;
        public NodeData RootNode => _rootNode;
        public Matrix4 GlobalInverseTransform => _globalInverseTransform;

        private static readonly HashSet<string> _formatsNeedingFlipUVs = new(StringComparer.OrdinalIgnoreCase)
        {
            ".obj", ".fbx", ".3ds", ".dae", ".blend", ".ply", ".stl", ".x", ".lwo", ".lws"
        };

        private static PostProcessSteps GetPostProcessFlags(string extension)
        {
            var flags = PostProcessSteps.Triangulate
                      | PostProcessSteps.CalculateTangentSpace
                      | PostProcessSteps.GenerateSmoothNormals
                      | PostProcessSteps.LimitBoneWeights;

            if (_formatsNeedingFlipUVs.Contains(extension))
                flags |= PostProcessSteps.FlipUVs;

            return flags;
        }

        public Box3 AABB
        {
            get
            {
                if (!_aabbCalculated) CalculateAABB();
                return _aabb;
            }
        }

        // ─── Carga desde archivo ──────────────────────────────────────
        public static AnimatedModel Load(string path)
        {
            var model = new AnimatedModel();
            path = AssetManager.BasePath + path;
            string extension = System.IO.Path.GetExtension(path);

            var importer = new AssimpContext();
            importer.SetConfig(new Assimp.Configs.RemoveDegeneratePrimitivesConfig(true));
            importer.SetConfig(new Assimp.Configs.MaxBoneCountConfig(100));

            Scene scene = importer.ImportFile(path, GetPostProcessFlags(extension));

            if (scene == null || scene.SceneFlags.HasFlag(SceneFlags.Incomplete) || scene.RootNode == null)
                throw new Exception($"Error loading animated model: {path}");

            model.ProcessMaterials(scene, path);
            model.ProcessNode(scene.RootNode, scene);
            model.CombineSubMeshes();
            model.CalculateAABB();
            model._rootNode = ConvertNodeHierarchy(scene.RootNode);

            // La inversa del transform del nodo raíz normaliza la escala/rotación
            // que Assimp inyecta en los FBX (p.ej. eje Y-up, escala cm→m, etc.)
            Matrix4 rootTransform = ConvertMatrix(scene.RootNode.Transform);
            model._globalInverseTransform = Matrix4.Invert(rootTransform);

            model.ExtractAnimations(scene);

            return model;
        }

        public static AnimatedModel LoadFromBytes(byte[] data, string extension)
        {
            var model = new AnimatedModel();
            var importer = new AssimpContext();

            // Forzar que Assimp bake las constraints de IK igual que Unity
            importer.SetConfig(new Assimp.Configs.RemoveDegeneratePrimitivesConfig(true));
            importer.SetConfig(new Assimp.Configs.MaxBoneCountConfig(100));

            using var ms = new MemoryStream(data);
            Scene scene = importer.ImportFileFromStream(ms, GetPostProcessFlags(extension), extension);

            if (scene == null || scene.SceneFlags.HasFlag(SceneFlags.Incomplete) || scene.RootNode == null)
                throw new Exception("Error loading animated model from memory");

            model.ProcessMaterials(scene, "InMemory:" + extension);
            model.ProcessNode(scene.RootNode, scene);
            model.CombineSubMeshes();
            model.CalculateAABB();
            model._rootNode = ConvertNodeHierarchy(scene.RootNode);

            // Igual que en Load(): invertir el transform raíz de Assimp
            Matrix4 rootTransform = ConvertMatrix(scene.RootNode.Transform);
            model._globalInverseTransform = Matrix4.Invert(rootTransform);

            model.ExtractAnimations(scene);

            return model;
        }

        // ─── Materiales (mismo código que Model) ──────────────────────
        private void ProcessMaterials(Scene scene, string modelPath)
        {
            string modelDirectory = System.IO.Path.GetDirectoryName(modelPath) ?? "";
            for (int i = 0; i < scene.MaterialCount; i++)
            {
                var material = scene.Materials[i];
                var textureInfo = new MaterialTextureInfo();

                if (material.HasTextureDiffuse)
                    textureInfo.DiffuseTexture = GetTexturePath(material.TextureDiffuse, modelDirectory);
                if (material.HasTextureNormal)
                    textureInfo.NormalTexture = GetTexturePath(material.TextureNormal, modelDirectory);
                else if (material.HasTextureHeight)
                    textureInfo.NormalTexture = GetTexturePath(material.TextureHeight, modelDirectory);
                if (material.HasTextureSpecular)
                    textureInfo.SpecularTexture = GetTexturePath(material.TextureSpecular, modelDirectory);
                if (material.HasTextureEmissive)
                    textureInfo.EmissiveTexture = GetTexturePath(material.TextureEmissive, modelDirectory);

                _materials.Add(textureInfo);
            }
        }

        private string GetTexturePath(TextureSlot slot, string modelDirectory)
        {
            if (string.IsNullOrEmpty(slot.FilePath)) return null;
            string texturePath = slot.FilePath.Replace("\\", "/");
            if (System.IO.Path.IsPathRooted(texturePath))
                texturePath = System.IO.Path.GetFileName(texturePath);
            return System.IO.Path.Combine(modelDirectory, texturePath).Replace("\\", "/");
        }

        // ─── Procesar nodos ──────────────────────────────────────────
        private void ProcessNode(Node node, Scene scene)
        {
            for (int i = 0; i < node.MeshCount; i++)
            {
                Assimp.Mesh mesh = scene.Meshes[node.MeshIndices[i]];
                var processedMesh = ProcessMesh(mesh, scene);
                int materialIndex = mesh.MaterialIndex;

                MaterialTextureInfo textureInfo = null;
                if (materialIndex >= 0 && materialIndex < _materials.Count)
                    textureInfo = _materials[materialIndex];

                SubMeshes.Add(new AnimatedSubMeshInfo(processedMesh, materialIndex, textureInfo));
            }

            for (int i = 0; i < node.ChildCount; i++)
                ProcessNode(node.Children[i], scene);
        }

        // ─── Procesar mesh con huesos ────────────────────────────────
        private AnimatedMesh ProcessMesh(Assimp.Mesh mesh, Scene scene)
        {
            var vertices = new List<float>();
            var indices = new List<uint>();

            var vertexBoneData = new VertexBoneData[mesh.VertexCount];
            for (int i = 0; i < mesh.VertexCount; i++)
                vertexBoneData[i] = VertexBoneData.Create();

            ExtractBones(mesh, vertexBoneData);

            for (int i = 0; i < vertexBoneData.Length; i++)
                vertexBoneData[i].Normalize();

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
                else { vertices.Add(0); vertices.Add(0); vertices.Add(0); }

                if (mesh.HasTextureCoords(0))
                {
                    vertices.Add(mesh.TextureCoordinateChannels[0][i].X);
                    vertices.Add(mesh.TextureCoordinateChannels[0][i].Y);
                }
                else { vertices.Add(0); vertices.Add(0); }

                if (mesh.HasTangentBasis)
                {
                    vertices.Add(mesh.Tangents[i].X);
                    vertices.Add(mesh.Tangents[i].Y);
                    vertices.Add(mesh.Tangents[i].Z);
                }
                else { vertices.Add(1); vertices.Add(0); vertices.Add(0); }

                if (mesh.HasTangentBasis)
                {
                    vertices.Add(mesh.BiTangents[i].X);
                    vertices.Add(mesh.BiTangents[i].Y);
                    vertices.Add(mesh.BiTangents[i].Z);
                }
                else { vertices.Add(0); vertices.Add(1); vertices.Add(0); }
            }

            for (int i = 0; i < mesh.FaceCount; i++)
            {
                Face face = mesh.Faces[i];
                for (int j = 0; j < face.IndexCount; j++)
                    indices.Add((uint)face.Indices[j]);
            }

            int[] boneIds = new int[mesh.VertexCount * 4];
            float[] boneWeights = new float[mesh.VertexCount * 4];

            for (int i = 0; i < mesh.VertexCount; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    boneIds[i * 4 + j] = vertexBoneData[i].BoneIDs[j];
                    boneWeights[i * 4 + j] = vertexBoneData[i].Weights[j];
                }
            }

            return new AnimatedMesh(vertices.ToArray(), indices.ToArray(), boneIds, boneWeights);
        }

        // ─── Extraer huesos de un mesh ───────────────────────────────
        private void ExtractBones(Assimp.Mesh mesh, VertexBoneData[] vertexBoneData)
        {
            if (!mesh.HasBones) return;

            for (int boneIndex = 0; boneIndex < mesh.BoneCount; boneIndex++)
            {
                var bone = mesh.Bones[boneIndex];
                string boneName = bone.Name;
                int boneId;

                if (BoneInfoMap.ContainsKey(boneName))
                {
                    boneId = BoneInfoMap[boneName].Id;
                }
                else
                {
                    boneId = BoneCount;
                    BoneInfoMap[boneName] = new BoneInfo
                    {
                        Id = boneId,
                        OffsetMatrix = ConvertMatrix(bone.OffsetMatrix)
                    };
                    BoneCount++;
                }

                for (int weightIndex = 0; weightIndex < bone.VertexWeightCount; weightIndex++)
                {
                    var vertexWeight = bone.VertexWeights[weightIndex];
                    int vertexId = vertexWeight.VertexID;

                    if (vertexId < vertexBoneData.Length)
                        vertexBoneData[vertexId].AddBone(boneId, vertexWeight.Weight);
                }
            }
        }

        private void ExtractAnimations(Scene scene)
        {
            if (!scene.HasAnimations) return;

            for (int animIndex = 0; animIndex < scene.AnimationCount; animIndex++)
            {
                var anim = scene.Animations[animIndex];

                var clip = new AnimationClip
                {
                    Name = string.IsNullOrEmpty(anim.Name) ? $"Animation_{animIndex}" : anim.Name,
                    Duration = (float)anim.DurationInTicks,
                    TicksPerSecond = anim.TicksPerSecond > 0 ? (float)anim.TicksPerSecond : 25f,
                    RootNode = _rootNode
                };

                // LOG: ver qué bones tienen canal de animación
                Console.WriteLine($"[AnimatedModel] Canales de animación ({anim.NodeAnimationChannelCount}):");
                foreach (var ch in anim.NodeAnimationChannels)
                {
                    bool enBoneMap = BoneInfoMap.ContainsKey(ch.NodeName);
                    Console.WriteLine($"  canal: '{ch.NodeName}' | en BoneInfoMap: {enBoneMap} | pos:{ch.PositionKeyCount} rot:{ch.RotationKeyCount} scale:{ch.ScalingKeyCount}");
                }

                // LOG: ver qué bones del mesh NO tienen canal
                Console.WriteLine($"[AnimatedModel] Bones sin canal de animación:");
                foreach (var boneName in BoneInfoMap.Keys)
                {
                    bool tieneCanal = anim.NodeAnimationChannels.Any(c => c.NodeName == boneName);
                    if (!tieneCanal)
                        Console.WriteLine($"  SIN CANAL: '{boneName}'");
                }

                for (int channelIndex = 0; channelIndex < anim.NodeAnimationChannelCount; channelIndex++)
                {
                    var channel = anim.NodeAnimationChannels[channelIndex];
                    var boneAnim = new BoneAnimation { BoneName = channel.NodeName };

                    if (BoneInfoMap.TryGetValue(channel.NodeName, out var boneInfo))
                        boneAnim.BoneId = boneInfo.Id;

                    for (int i = 0; i < channel.PositionKeyCount; i++)
                    {
                        var key = channel.PositionKeys[i];
                        boneAnim.Positions.Add(new KeyPosition
                        {
                            Position = new Vector3(key.Value.X, key.Value.Y, key.Value.Z),
                            TimeStamp = (float)key.Time
                        });
                    }

                    for (int i = 0; i < channel.RotationKeyCount; i++)
                    {
                        var key = channel.RotationKeys[i];
                        boneAnim.Rotations.Add(new KeyRotation
                        {
                            Rotation = new OpenTK.Mathematics.Quaternion(key.Value.X, key.Value.Y, key.Value.Z, key.Value.W),
                            TimeStamp = (float)key.Time
                        });
                    }

                    for (int i = 0; i < channel.ScalingKeyCount; i++)
                    {
                        var key = channel.ScalingKeys[i];
                        boneAnim.Scales.Add(new KeyScale
                        {
                            Scale = new Vector3(key.Value.X, key.Value.Y, key.Value.Z),
                            TimeStamp = (float)key.Time
                        });
                    }

                    clip.BoneAnimations.Add(boneAnim);
                }

                Animations.Add(clip);
                Console.WriteLine($"[AnimatedModel] Animación cargada: '{clip.Name}' | Duración: {clip.Duration / clip.TicksPerSecond:F2}s | Huesos animados: {clip.BoneAnimations.Count}");
            }
        }

        // ─── Jerarquía de nodos ──────────────────────────────────────
        private static NodeData ConvertNodeHierarchy(Node node)
        {
            // LOG temporal
            Console.WriteLine($"[NodeHierarchy] '{node.Name}' hijos: {node.ChildCount}");

            var nodeData = new NodeData
            {
                Name = node.Name,
                Transform = ConvertMatrix(node.Transform)
            };

            for (int i = 0; i < node.ChildCount; i++)
                nodeData.Children.Add(ConvertNodeHierarchy(node.Children[i]));

            return nodeData;
        }

        public static Matrix4 ConvertMatrix(Assimp.Matrix4x4 m)
        {
            return new Matrix4(
                m.A1, m.B1, m.C1, m.D1,
                m.A2, m.B2, m.C2, m.D2,
                m.A3, m.B3, m.C3, m.D3,
                m.A4, m.B4, m.C4, m.D4
            );
        }

        // ─── Combinar submeshes ──────────────────────────────────────
        private void CombineSubMeshes()
        {
            if (SubMeshes.Count == 0) return;

            var combinedVertices = new List<float>();
            var combinedIndices = new List<uint>();
            var combinedBoneIds = new List<int>();
            var combinedBoneWeights = new List<float>();

            int currentBaseVertex = 0;
            int currentBaseIndex = 0;

            foreach (var subMesh in SubMeshes)
            {
                var mesh = subMesh.Mesh;
                var verts = mesh.GetVertices();
                var inds = mesh.GetIndices();

                subMesh.BaseVertex = currentBaseVertex;
                subMesh.BaseIndex = currentBaseIndex;
                subMesh.IndexCount = inds.Length;

                combinedVertices.AddRange(verts);
                combinedIndices.AddRange(inds);

                currentBaseVertex += mesh.VertexCount;
                currentBaseIndex += inds.Length;
            }
        }

        // ─── AABB ────────────────────────────────────────────────────
        private void CalculateAABB()
        {
            if (SubMeshes.Count == 0) { _aabb = new Box3(Vector3.Zero, Vector3.Zero); _aabbCalculated = true; return; }

            Vector3 min = new Vector3(float.MaxValue);
            Vector3 max = new Vector3(float.MinValue);

            foreach (var sub in SubMeshes)
            {
                Box3 meshAABB = sub.Mesh.GetAABB();
                min = Vector3.ComponentMin(min, meshAABB.Min);
                max = Vector3.ComponentMax(max, meshAABB.Max);
            }

            _aabb = new Box3(min, max);
            _aabbCalculated = true;
        }

        public MaterialTextureInfo GetSubMeshTextureInfo(int index)
        {
            if (index >= 0 && index < SubMeshes.Count)
                return SubMeshes[index].TextureInfo;
            return new MaterialTextureInfo();
        }

        // ─── Dispose ─────────────────────────────────────────────────
        public void Dispose()
        {
            if (!_disposed)
            {
                foreach (var sub in SubMeshes)
                    sub.Mesh?.Dispose();
                SubMeshes.Clear();
                _materials.Clear();
                Animations.Clear();
                _disposed = true;
            }
        }
    }
}