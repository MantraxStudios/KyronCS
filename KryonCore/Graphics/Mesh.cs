using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace KrayonCore
{
    public class Mesh : IDisposable
    {
        private int _vao, _vbo, _ebo, _instanceVBO;
        private int _indexCount;
        private int _vertexCount;
        private bool _disposed = false;
        private float[] _vertices;
        private uint[] _indices;
        private bool _instancingSetup = false;

        public int IndexCount => _indexCount;
        public int VertexCount => _vertexCount;

        public Mesh(float[] vertices, uint[] indices)
        {
            _vertices = vertices;
            _indices = indices;
            _indexCount = indices.Length;
            _vertexCount = vertices.Length / 14;

            SetupGPU();
        }

        private void SetupGPU()
        {
            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();
            _ebo = GL.GenBuffer();

            GL.BindVertexArray(_vao);

            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, _vertices.Length * sizeof(float), _vertices, BufferUsageHint.StaticDraw);

            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, _indices.Length * sizeof(uint), _indices, BufferUsageHint.StaticDraw);

            int stride = 14 * sizeof(float);

            // posición
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);
            GL.EnableVertexAttribArray(0);
            // normal
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float));
            GL.EnableVertexAttribArray(1);
            // UV
            GL.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, stride, 6 * sizeof(float));
            GL.EnableVertexAttribArray(2);
            // tangente
            GL.VertexAttribPointer(3, 3, VertexAttribPointerType.Float, false, stride, 8 * sizeof(float));
            GL.EnableVertexAttribArray(3);
            // bitangente
            GL.VertexAttribPointer(4, 3, VertexAttribPointerType.Float, false, stride, 11 * sizeof(float));
            GL.EnableVertexAttribArray(4);

            GL.BindVertexArray(0);
        }

        public int GetVAO() => _vao;

        public float[] GetVertices() => _vertices;

        public uint[] GetIndices() => _indices;

        // ═══════════════════════════════════════════════════════════════════
        //  Normalización de escala
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Aplica una traslación (−center) y un factor de escala uniforme a los
        /// vértices en CPU y sube los datos actualizados a la GPU.
        /// Solo afecta posición (offset 0), tangentes y bitangentes NO se escalan
        /// porque son vectores de dirección.
        /// Las normales tampoco se escalan (solo escala uniforme, así que son válidas).
        /// </summary>
        public void TransformVertices(Vector3 center, float scale)
        {
            const int stride = 14; // floats por vértice

            for (int i = 0; i < _vertices.Length; i += stride)
            {
                // Posición: centrar y escalar
                _vertices[i + 0] = (_vertices[i + 0] - center.X) * scale;
                _vertices[i + 1] = (_vertices[i + 1] - center.Y) * scale;
                _vertices[i + 2] = (_vertices[i + 2] - center.Z) * scale;

                // Normales (índices 3,4,5): escala uniforme no las afecta, no tocar.
                // UVs (índices 6,7): no tocar.
                // Tangentes (índices 8,9,10) y bitangentes (11,12,13): vectores de dirección, no tocar.
            }

            // Re-subir el buffer a GPU
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, _vertices.Length * sizeof(float), _vertices, BufferUsageHint.StaticDraw);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Resto sin cambios
        // ═══════════════════════════════════════════════════════════════════

        public void SetupInstancing(Matrix4[] instanceMatrices)
        {
            if (_instanceVBO == 0)
                _instanceVBO = GL.GenBuffer();

            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _instanceVBO);
            GL.BufferData(BufferTarget.ArrayBuffer, instanceMatrices.Length * sizeof(float) * 16, instanceMatrices, BufferUsageHint.DynamicDraw);

            if (!_instancingSetup)
            {
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

                _instancingSetup = true;
            }

            GL.BindVertexArray(0);
        }

        public void ClearInstancing()
        {
            if (_instanceVBO != 0)
            {
                GL.BindVertexArray(_vao);

                GL.DisableVertexAttribArray(5);
                GL.DisableVertexAttribArray(6);
                GL.DisableVertexAttribArray(7);
                GL.DisableVertexAttribArray(8);

                GL.BindVertexArray(0);

                GL.DeleteBuffer(_instanceVBO);
                _instanceVBO = 0;
                _instancingSetup = false;
            }
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
                ClearInstancing();
                GL.DeleteVertexArray(_vao);
                GL.DeleteBuffer(_vbo);
                GL.DeleteBuffer(_ebo);
                _vertices = null;
                _indices = null;
                _disposed = true;
            }
        }
    }
}