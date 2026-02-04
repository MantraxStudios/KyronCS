using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using Assimp;
using GLPrimitiveType = OpenTK.Graphics.OpenGL4.PrimitiveType;
using AssimpPrimitiveType = Assimp.PrimitiveType;
using KrayonCore.Core.Attributes;

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

        public void Draw()
        {
            foreach (var mesh in _meshes)
            {
                mesh.Draw();
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

    public class Mesh : IDisposable
    {
        private int _vao, _vbo, _ebo;
        private int _indexCount;
        private bool _disposed = false;

        public Mesh(float[] vertices, uint[] indices)
        {
            _indexCount = indices.Length;

            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();
            _ebo = GL.GenBuffer();

            GL.BindVertexArray(_vao);

            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);

            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(uint), indices, BufferUsageHint.StaticDraw);

            int stride = 14 * sizeof(float);

            // Position
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);
            GL.EnableVertexAttribArray(0);

            // Normal
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float));
            GL.EnableVertexAttribArray(1);

            // TexCoords
            GL.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, stride, 6 * sizeof(float));
            GL.EnableVertexAttribArray(2);

            // Tangent
            GL.VertexAttribPointer(3, 3, VertexAttribPointerType.Float, false, stride, 8 * sizeof(float));
            GL.EnableVertexAttribArray(3);

            // BiTangent
            GL.VertexAttribPointer(4, 3, VertexAttribPointerType.Float, false, stride, 11 * sizeof(float));
            GL.EnableVertexAttribArray(4);

            GL.BindVertexArray(0);
        }

        public void Draw()
        {
            GL.BindVertexArray(_vao);
            GL.DrawElements(GLPrimitiveType.Triangles, _indexCount, DrawElementsType.UnsignedInt, 0);
            GL.BindVertexArray(0);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                GL.DeleteVertexArray(_vao);
                GL.DeleteBuffer(_vbo);
                GL.DeleteBuffer(_ebo);
                _disposed = true;
            }
        }
    }
}