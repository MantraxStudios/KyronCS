using Assimp;
using KrayonCore.Core.Attributes;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace KrayonCore
{
    public class Model : IDisposable
    {
        private List<Mesh> _meshes = new();
        private bool _disposed = false;

        public int SubMeshCount => _meshes.Count;

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

            model.ProcessNode(scene.RootNode, scene);
            return model;
        }

        private void ProcessNode(Node node, Scene scene)
        {
            for (int i = 0; i < node.MeshCount; i++)
            {
                Assimp.Mesh mesh = scene.Meshes[node.MeshIndices[i]];
                _meshes.Add(ProcessMesh(mesh, scene));
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

        public void SetupInstancing(Matrix4[] instanceMatrices)
        {
            foreach (var mesh in _meshes)
            {
                mesh.SetupInstancing(instanceMatrices);
            }
        }

        public void Draw()
        {
            foreach (var mesh in _meshes)
            {
                mesh.Draw();
            }
        }

        public void DrawInstanced(int instanceCount)
        {
            foreach (var mesh in _meshes)
            {
                mesh.DrawInstanced(instanceCount);
            }
        }

        public void DrawSubMesh(int index)
        {
            if (index >= 0 && index < _meshes.Count)
            {
                _meshes[index].Draw();
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                foreach (var mesh in _meshes)
                {
                    mesh?.Dispose();
                }
                _meshes.Clear();
                _disposed = true;
            }
        }
    }
}