using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;

namespace KrayonCore.Animation
{
    /// <summary>
    /// Mesh con soporte para skinning. Vertex layout:
    /// [0-2]   Position    (3 floats)  location 0
    /// [3-5]   Normal      (3 floats)  location 1
    /// [6-7]   TexCoord    (2 floats)  location 2
    /// [8-10]  Tangent     (3 floats)  location 3
    /// [11-13] Bitangent   (3 floats)  location 4
    /// Bone IDs   (4 ints)    location 9   — buffer separado
    /// Bone Weights (4 floats) location 10  — buffer separado
    /// </summary>
    public class AnimatedMesh : IDisposable
    {
        private int _vao, _vbo, _ebo;
        private int _boneVBO;
        private int _indexCount;
        private int _vertexCount;
        private bool _disposed = false;
        private float[] _vertices;
        private uint[] _indices;

        public int IndexCount => _indexCount;
        public int VertexCount => _vertexCount;

        public AnimatedMesh(float[] vertices, uint[] indices, int[] boneIds, float[] boneWeights)
        {
            _vertices = vertices;
            _indices = indices;
            _indexCount = indices.Length;
            _vertexCount = vertices.Length / 14;

            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();
            _ebo = GL.GenBuffer();
            _boneVBO = GL.GenBuffer();

            GL.BindVertexArray(_vao);

            // Buffer de vértices (mismo layout que Mesh original: 14 floats)
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);

            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(uint), indices, BufferUsageHint.StaticDraw);

            int stride = 14 * sizeof(float);

            // location 0: Position
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);
            GL.EnableVertexAttribArray(0);

            // location 1: Normal
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float));
            GL.EnableVertexAttribArray(1);

            // location 2: TexCoord
            GL.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, stride, 6 * sizeof(float));
            GL.EnableVertexAttribArray(2);

            // location 3: Tangent
            GL.VertexAttribPointer(3, 3, VertexAttribPointerType.Float, false, stride, 8 * sizeof(float));
            GL.EnableVertexAttribArray(3);

            // location 4: Bitangent
            GL.VertexAttribPointer(4, 3, VertexAttribPointerType.Float, false, stride, 11 * sizeof(float));
            GL.EnableVertexAttribArray(4);

            // Buffer de huesos (bone IDs + bone weights intercalados)
            // Layout: [id0, id1, id2, id3, w0, w1, w2, w3] por vértice → 8 ints/floats
            int boneStride = (4 * sizeof(int)) + (4 * sizeof(float));
            byte[] boneData = new byte[_vertexCount * boneStride];

            for (int i = 0; i < _vertexCount; i++)
            {
                int offset = i * boneStride;

                // 4 ints para bone IDs
                System.Buffer.BlockCopy(boneIds, i * 4 * sizeof(int), boneData, offset, 4 * sizeof(int));

                // 4 floats para bone weights
                System.Buffer.BlockCopy(boneWeights, i * 4 * sizeof(float), boneData, offset + 4 * sizeof(int), 4 * sizeof(float));
            }

            GL.BindBuffer(BufferTarget.ArrayBuffer, _boneVBO);
            GL.BufferData(BufferTarget.ArrayBuffer, boneData.Length, boneData, BufferUsageHint.StaticDraw);

            // location 9: BoneIDs (ivec4)
            GL.VertexAttribIPointer(9, 4, VertexAttribIntegerType.Int, boneStride, IntPtr.Zero);
            GL.EnableVertexAttribArray(9);

            // location 10: BoneWeights (vec4)
            GL.VertexAttribPointer(10, 4, VertexAttribPointerType.Float, false, boneStride, 4 * sizeof(int));
            GL.EnableVertexAttribArray(10);

            GL.BindVertexArray(0);
        }

        public int GetVAO() => _vao;
        public float[] GetVertices() => _vertices;
        public uint[] GetIndices() => _indices;

        public void Draw()
        {
            GL.BindVertexArray(_vao);
            GL.DrawElements(PrimitiveType.Triangles, _indexCount, DrawElementsType.UnsignedInt, 0);
            GL.BindVertexArray(0);
        }

        public Box3 GetAABB()
        {
            if (_vertices == null || _vertices.Length == 0)
                return new Box3(Vector3.Zero, Vector3.Zero);

            Vector3 min = new Vector3(float.MaxValue);
            Vector3 max = new Vector3(float.MinValue);
            int stride = 14;

            for (int i = 0; i < _vertices.Length; i += stride)
            {
                Vector3 position = new Vector3(_vertices[i], _vertices[i + 1], _vertices[i + 2]);
                min = Vector3.ComponentMin(min, position);
                max = Vector3.ComponentMax(max, position);
            }

            return new Box3(min, max);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                GL.DeleteVertexArray(_vao);
                GL.DeleteBuffer(_vbo);
                GL.DeleteBuffer(_ebo);
                GL.DeleteBuffer(_boneVBO);
                _vertices = null;
                _indices = null;
                _disposed = true;
            }
        }
    }
}