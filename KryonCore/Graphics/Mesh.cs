using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace KrayonCore
{
    public class Mesh : IDisposable
    {
        private int _vao, _vbo, _ebo, _instanceVBO;
        private int _indexCount;
        private bool _disposed = false;
        private float[] _vertices; // Guardamos los vértices para calcular AABB

        public Mesh(float[] vertices, uint[] indices)
        {
            _vertices = vertices; // Guardar los vértices
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

            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);
            GL.EnableVertexAttribArray(0);

            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float));
            GL.EnableVertexAttribArray(1);

            GL.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, stride, 6 * sizeof(float));
            GL.EnableVertexAttribArray(2);

            GL.VertexAttribPointer(3, 3, VertexAttribPointerType.Float, false, stride, 8 * sizeof(float));
            GL.EnableVertexAttribArray(3);

            GL.VertexAttribPointer(4, 3, VertexAttribPointerType.Float, false, stride, 11 * sizeof(float));
            GL.EnableVertexAttribArray(4);

            GL.BindVertexArray(0);
        }

        public void SetupInstancing(Matrix4[] instanceMatrices)
        {
            if (_instanceVBO == 0)
            {
                _instanceVBO = GL.GenBuffer();
            }

            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _instanceVBO);
            GL.BufferData(BufferTarget.ArrayBuffer, instanceMatrices.Length * sizeof(float) * 16, instanceMatrices, BufferUsageHint.DynamicDraw);

            int vec4Size = sizeof(float) * 4;

            GL.EnableVertexAttribArray(5);
            GL.VertexAttribPointer(5, 4, VertexAttribPointerType.Float, false, sizeof(float) * 16, 0);
            GL.VertexAttribDivisor(5, 1);

            GL.EnableVertexAttribArray(6);
            GL.VertexAttribPointer(6, 4, VertexAttribPointerType.Float, false, sizeof(float) * 16, vec4Size);
            GL.VertexAttribDivisor(6, 1);

            GL.EnableVertexAttribArray(7);
            GL.VertexAttribPointer(7, 4, VertexAttribPointerType.Float, false, sizeof(float) * 16, vec4Size * 2);
            GL.VertexAttribDivisor(7, 1);

            GL.EnableVertexAttribArray(8);
            GL.VertexAttribPointer(8, 4, VertexAttribPointerType.Float, false, sizeof(float) * 16, vec4Size * 3);
            GL.VertexAttribDivisor(8, 1);

            GL.BindVertexArray(0);
        }

        public void Draw()
        {
            GL.BindVertexArray(_vao);
            GL.DrawElements(PrimitiveType.Triangles, _indexCount, DrawElementsType.UnsignedInt, 0);
            GL.BindVertexArray(0);
        }

        public void DrawInstanced(int instanceCount)
        {
            GL.BindVertexArray(_vao);
            GL.DrawElementsInstanced(PrimitiveType.Triangles, _indexCount, DrawElementsType.UnsignedInt, IntPtr.Zero, instanceCount);
            GL.BindVertexArray(0);
        }

        public Box3 GetAABB()
        {
            if (_vertices == null || _vertices.Length == 0)
            {
                return new Box3(Vector3.Zero, Vector3.Zero);
            }

            Vector3 min = new Vector3(float.MaxValue);
            Vector3 max = new Vector3(float.MinValue);

            // Los vértices están en formato: pos(3) + normal(3) + uv(2) + tangent(3) + bitangent(3) = 14 floats
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
                if (_instanceVBO != 0)
                {
                    GL.DeleteBuffer(_instanceVBO);
                }
                _vertices = null; // Liberar la referencia
                _disposed = true;
            }
        }
    }
}