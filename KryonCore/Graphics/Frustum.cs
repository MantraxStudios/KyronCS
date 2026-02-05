using OpenTK.Mathematics;

namespace KrayonCore
{
    public class Frustum
    {
        private Vector4[] _planes = new Vector4[6]; // Left, Right, Bottom, Top, Near, Far

        public void Update(Matrix4 view, Matrix4 projection)
        {
            Matrix4 viewProjection = view * projection;

            // Left plane
            _planes[0] = new Vector4(
                viewProjection.M14 + viewProjection.M11,
                viewProjection.M24 + viewProjection.M21,
                viewProjection.M34 + viewProjection.M31,
                viewProjection.M44 + viewProjection.M41
            );

            // Right plane
            _planes[1] = new Vector4(
                viewProjection.M14 - viewProjection.M11,
                viewProjection.M24 - viewProjection.M21,
                viewProjection.M34 - viewProjection.M31,
                viewProjection.M44 - viewProjection.M41
            );

            // Bottom plane
            _planes[2] = new Vector4(
                viewProjection.M14 + viewProjection.M12,
                viewProjection.M24 + viewProjection.M22,
                viewProjection.M34 + viewProjection.M32,
                viewProjection.M44 + viewProjection.M42
            );

            // Top plane
            _planes[3] = new Vector4(
                viewProjection.M14 - viewProjection.M12,
                viewProjection.M24 - viewProjection.M22,
                viewProjection.M34 - viewProjection.M32,
                viewProjection.M44 - viewProjection.M42
            );

            // Near plane
            _planes[4] = new Vector4(
                viewProjection.M14 + viewProjection.M13,
                viewProjection.M24 + viewProjection.M23,
                viewProjection.M34 + viewProjection.M33,
                viewProjection.M44 + viewProjection.M43
            );

            // Far plane
            _planes[5] = new Vector4(
                viewProjection.M14 - viewProjection.M13,
                viewProjection.M24 - viewProjection.M23,
                viewProjection.M34 - viewProjection.M33,
                viewProjection.M44 - viewProjection.M43
            );

            // Normalizar los planos
            for (int i = 0; i < 6; i++)
            {
                float length = _planes[i].Xyz.Length;
                _planes[i] /= length;
            }
        }

        public bool Intersects(Box3 aabb)
        {
            Vector3 center = aabb.Center;
            Vector3 extents = aabb.HalfSize;

            for (int i = 0; i < 6; i++)
            {
                Vector3 planeNormal = _planes[i].Xyz;
                float planeDistance = _planes[i].W;

                // Calcular la proyección del AABB sobre la normal del plano
                float r = extents.X * Math.Abs(planeNormal.X) +
                         extents.Y * Math.Abs(planeNormal.Y) +
                         extents.Z * Math.Abs(planeNormal.Z);

                float distance = Vector3.Dot(planeNormal, center) + planeDistance;

                // Si el AABB está completamente detrás del plano, no es visible
                if (distance < -r)
                    return false;
            }

            return true;
        }

        public bool Contains(Vector3 point)
        {
            for (int i = 0; i < 6; i++)
            {
                if (Vector3.Dot(_planes[i].Xyz, point) + _planes[i].W < 0)
                    return false;
            }
            return true;
        }
    }
}