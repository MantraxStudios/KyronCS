using KrayonCore.Components.RenderComponents;
using KrayonCore.Graphics.GameUI;
using KrayonCore.GraphicsData;
using OpenTK.Mathematics;

namespace KrayonCore.EventSystem
{
    public static class EventSystem
    {
        private const int STRIDE = 14;

        // ── UI Hit Test ──────────────────────────────────────────────────

        /// <summary>
        /// Devuelve el UIElement visible más al frente (mayor ZOrder) que
        /// contiene <paramref name="screenPos"/>, o null si ninguno lo hace.
        /// Recorre todos los canvas del SceneRenderer en orden de SortOrder.
        /// </summary>
        public static UIElement? GetUIElementAt(Vector2 screenPos)
        {
            var ui = GraphicsEngine.Instance.GetSceneRenderer().UI;

            // Recorrer canvas de mayor a menor SortOrder (el de arriba primero)
            foreach (var canvas in ui.All().OrderByDescending(c => c.SortOrder))
            {
                if (!canvas.Visible) continue;

                Vector2 refPos = canvas.ScreenToReference(screenPos);

                // Elementos de mayor ZOrder primero
                var hit = canvas.Elements
                    .Where(e => e.Visible && e.Enabled)
                    .OrderByDescending(e => e.ZOrder)
                    .FirstOrDefault(e => e.HitTest(refPos));

                if (hit is not null)
                    return hit;
            }

            return null;
        }

        /// <summary>
        /// Devuelve true si <paramref name="screenPos"/> cae sobre cualquier
        /// elemento de UI visible/habilitado. Útil para bloquear el raycast 3D.
        /// </summary>
        public static bool IsPointerOverUI(Vector2 screenPos)
            => GetUIElementAt(screenPos) is not null;

        // ── Screen → World Ray ───────────────────────────────────────────

        public static void ScreenToWorldRay(Vector2 screenPos, Camera camera,
            int screenWidth, int screenHeight,
            out Vector3 rayOrigin, out Vector3 rayDirection)
        {
            float ndcX = (screenPos.X / screenWidth) * 2.0f - 1.0f;
            float ndcY = 1.0f - (screenPos.Y / screenHeight) * 2.0f;

            Matrix4 invVP = Matrix4.Invert(
                camera.GetViewMatrix() * camera.GetProjectionMatrix());

            Vector4 nearPoint = new Vector4(ndcX, ndcY, -1.0f, 1.0f) * invVP;
            Vector4 farPoint = new Vector4(ndcX, ndcY, 1.0f, 1.0f) * invVP;

            Vector3 near3 = nearPoint.Xyz / nearPoint.W;
            Vector3 far3 = farPoint.Xyz / farPoint.W;

            rayOrigin = near3;
            rayDirection = Vector3.Normalize(far3 - near3);
        }

        // ── Ray vs AABB ──────────────────────────────────────────────────

        private static float RayIntersectsAABB(Vector3 origin, Vector3 dir,
            Vector3 aabbMin, Vector3 aabbMax)
        {
            float tMin = float.MinValue;
            float tMax = float.MaxValue;

            if (MathF.Abs(dir.X) > 1e-8f)
            {
                float t1 = (aabbMin.X - origin.X) / dir.X;
                float t2 = (aabbMax.X - origin.X) / dir.X;
                if (t1 > t2) (t1, t2) = (t2, t1);
                tMin = MathF.Max(tMin, t1);
                tMax = MathF.Min(tMax, t2);
                if (tMin > tMax) return float.MaxValue;
            }
            else if (origin.X < aabbMin.X || origin.X > aabbMax.X)
                return float.MaxValue;

            if (MathF.Abs(dir.Y) > 1e-8f)
            {
                float t1 = (aabbMin.Y - origin.Y) / dir.Y;
                float t2 = (aabbMax.Y - origin.Y) / dir.Y;
                if (t1 > t2) (t1, t2) = (t2, t1);
                tMin = MathF.Max(tMin, t1);
                tMax = MathF.Min(tMax, t2);
                if (tMin > tMax) return float.MaxValue;
            }
            else if (origin.Y < aabbMin.Y || origin.Y > aabbMax.Y)
                return float.MaxValue;

            if (MathF.Abs(dir.Z) > 1e-8f)
            {
                float t1 = (aabbMin.Z - origin.Z) / dir.Z;
                float t2 = (aabbMax.Z - origin.Z) / dir.Z;
                if (t1 > t2) (t1, t2) = (t2, t1);
                tMin = MathF.Max(tMin, t1);
                tMax = MathF.Min(tMax, t2);
                if (tMin > tMax) return float.MaxValue;
            }
            else if (origin.Z < aabbMin.Z || origin.Z > aabbMax.Z)
                return float.MaxValue;

            if (tMax < 0) return float.MaxValue;
            return tMin >= 0 ? tMin : tMax;
        }

        // ── Ray vs Triangle (Möller–Trumbore) ───────────────────────────

        private static float RayIntersectsTriangle(Vector3 origin, Vector3 dir,
            Vector3 v0, Vector3 v1, Vector3 v2)
        {
            const float EPSILON = 1e-8f;

            Vector3 edge1 = v1 - v0;
            Vector3 edge2 = v2 - v0;
            Vector3 h = Vector3.Cross(dir, edge2);
            float a = Vector3.Dot(edge1, h);

            if (a > -EPSILON && a < EPSILON) return float.MaxValue;

            float f = 1.0f / a;
            Vector3 s = origin - v0;
            float u = f * Vector3.Dot(s, h);
            if (u < 0.0f || u > 1.0f) return float.MaxValue;

            Vector3 q = Vector3.Cross(s, edge1);
            float v = f * Vector3.Dot(dir, q);
            if (v < 0.0f || u + v > 1.0f) return float.MaxValue;

            float t = f * Vector3.Dot(edge2, q);
            return t > EPSILON ? t : float.MaxValue;
        }

        // ── Ray vs Mesh ──────────────────────────────────────────────────

        private static float RayIntersectsMesh(Vector3 localOrigin, Vector3 localDir, Model model)
        {
            float closest = float.MaxValue;

            for (int s = 0; s < model._subMeshes.Count; s++)
            {
                float[] verts = model._subMeshes[s].Mesh.GetVertices();
                uint[] indices = model._subMeshes[s].Mesh.GetIndices();

                for (int i = 0; i + 2 < indices.Length; i += 3)
                {
                    int i0 = (int)indices[i] * STRIDE;
                    int i1 = (int)indices[i + 1] * STRIDE;
                    int i2 = (int)indices[i + 2] * STRIDE;

                    Vector3 v0 = new(verts[i0], verts[i0 + 1], verts[i0 + 2]);
                    Vector3 v1 = new(verts[i1], verts[i1 + 1], verts[i1 + 2]);
                    Vector3 v2 = new(verts[i2], verts[i2 + 1], verts[i2 + 2]);

                    float t = RayIntersectsTriangle(localOrigin, localDir, v0, v1, v2);
                    if (t < closest) closest = t;
                }
            }

            return closest;
        }

        // ── Object picking ───────────────────────────────────────────────

        public static GameObject GetObjectByRay(Vector3 rayOrigin, Vector3 rayDir)
        {
            GameObject closest = null;
            float closestDist = float.MaxValue;

            var objects = SceneManager.ActiveScene.GetAllGameObjects();

            for (int i = 0; i < objects.Count; i++)
            {
                GameObject obj = objects[i];

                Matrix4 world = obj.Transform.GetWorldMatrix();
                Matrix4 invWorld = Matrix4.Invert(world);

                Vector4 lo4 = new Vector4(rayOrigin, 1.0f) * invWorld;
                Vector4 le4 = new Vector4(rayOrigin + rayDir, 1.0f) * invWorld;

                Vector3 localOrigin = lo4.Xyz / lo4.W;
                Vector3 localEnd = le4.Xyz / le4.W;
                Vector3 localDir = Vector3.Normalize(localEnd - localOrigin);

                MeshRenderer meshRenderer = obj.GetComponent<MeshRenderer>();
                Model model = meshRenderer?.Model;

                float tLocal;

                if (model != null && model.SubMeshCount > 0)
                {
                    Box3 aabb = model.AABB;
                    float tAABB = RayIntersectsAABB(localOrigin, localDir, aabb.Min, aabb.Max);
                    if (tAABB >= float.MaxValue) continue;

                    tLocal = RayIntersectsMesh(localOrigin, localDir, model);
                }
                else
                {
                    Vector3 halfSize = Vector3.One * 0.5f;
                    tLocal = RayIntersectsAABB(localOrigin, localDir, -halfSize, halfSize);
                }

                if (tLocal >= float.MaxValue) continue;

                Vector3 localHit = localOrigin + localDir * tLocal;
                Vector4 worldHit4 = new Vector4(localHit, 1.0f) * world;
                Vector3 worldHit = worldHit4.Xyz / worldHit4.W;

                float worldDist = (worldHit - rayOrigin).Length;
                if (worldDist < closestDist)
                {
                    closestDist = worldDist;
                    closest = obj;
                }
            }

            return closest;
        }

        // ── Unified entry point ──────────────────────────────────────────

        /// <summary>
        /// Intenta clickear un objeto 3D. Si el cursor está sobre un elemento
        /// de UI devuelve null automáticamente (la UI consume el evento).
        /// </summary>
        public static GameObject OnClickObject(Vector2 viewportMousePos)
        {
            // ── UI primero: si hay un elemento de UI bajo el cursor, bloquear ──
            if (IsPointerOverUI(viewportMousePos))
                return null;

            Camera camera = GraphicsEngine.Instance.GetSceneRenderer().GetCamera();
            int screenWidth = GraphicsEngine.Instance.GetSceneFrameBuffer().Width;
            int screenHeight = GraphicsEngine.Instance.GetSceneFrameBuffer().Height;

            ScreenToWorldRay(viewportMousePos, camera, screenWidth, screenHeight,
                out Vector3 rayOrigin, out Vector3 rayDir);

            return GetObjectByRay(rayOrigin, rayDir);
        }

        // ── Legacy helpers ───────────────────────────────────────────────

        public static Vector2 ScreenToWorldPosition(Vector2 screenPos)
        {
            Vector3 cameraPos = GraphicsEngine.Instance.GetSceneRenderer().GetCamera().Position;
            float orthoSize = GraphicsEngine.Instance.GetSceneRenderer().GetCamera().OrthoSize;
            int screenWidth = GraphicsEngine.Instance.GetSceneFrameBuffer().Width;
            int screenHeight = GraphicsEngine.Instance.GetSceneFrameBuffer().Height;

            float aspect = (float)screenWidth / screenHeight;
            float width = orthoSize * aspect;

            float ndcX = (screenPos.X / screenWidth) * 2.0f - 1.0f;
            float ndcY = 1.0f - (screenPos.Y / screenHeight) * 2.0f;

            return new Vector2(
                cameraPos.X + ndcX * width / 2.0f,
                cameraPos.Y + ndcY * orthoSize / 2.0f
            );
        }

        public static GameObject GetObjectAtWorldPosition(Vector2 worldPos)
        {
            var objects = SceneManager.ActiveScene.GetAllGameObjects();
            for (int i = 0; i < objects.Count; i++)
            {
                GameObject obj = objects[i];

                float minX = obj.Transform.Position.X - obj.Transform.Scale.X;
                float maxX = obj.Transform.Position.X + obj.Transform.Scale.X;
                float minY = obj.Transform.Position.Y - obj.Transform.Scale.Y;
                float maxY = obj.Transform.Position.Y + obj.Transform.Scale.Y;

                if (worldPos.X >= minX && worldPos.X <= maxX &&
                    worldPos.Y >= minY && worldPos.Y <= maxY)
                    return obj;
            }
            return null;
        }
    }
}