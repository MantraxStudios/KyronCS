using KrayonCore;
using KrayonCore.Components;
using KrayonCore.Components.RenderComponents;
using KrayonCore.Graphics.Camera;
using KrayonCore.Graphics.GameUI;
using OpenTK.Mathematics;

namespace KrayonEditor.Gizmos
{
    public static class EditorGizmos
    {
        public static void DrawRigidbodyGizmo(Rigidbody rb, GameObject go, Matrix4 view, Matrix4 projection, Vector4 color, float lineWidth)
        {
            var worldPos = go.Transform.GetWorldPosition();
            var worldRot = go.Transform.GetWorldRotation();
            var worldOffset = Vector3.Transform(rb.ColliderOffset, worldRot);
            var center = worldPos + worldOffset;
            var rotMat = Matrix4.CreateFromQuaternion(worldRot);
            var transMat = Matrix4.CreateTranslation(center);

            switch (rb.ShapeType)
            {
                case ShapeType.Box:
                    GizmoCube.Draw(Matrix4.CreateScale(rb.ShapeSize * 2f) * rotMat * transMat,
                        view, projection, color, lineWidth);
                    break;
                case ShapeType.Sphere:
                    GizmoSphere.Draw(Matrix4.CreateScale(rb.ShapeSize.X * 2f) * rotMat * transMat,
                        view, projection, color, lineWidth);
                    break;
                case ShapeType.Capsule:
                    float diameter = rb.ShapeSize.X * 2f;
                    float totalHeight = rb.ShapeSize.Y * 2f + diameter;
                    GizmoCapsule.Draw(Matrix4.CreateScale(diameter, totalHeight, diameter) * rotMat * transMat,
                        view, projection, color, lineWidth);
                    break;
            }
        }

        public static void DrawLightGizmo(Light light, Matrix4 view, Matrix4 projection, bool isSelected)
        {
            var position = light.GetPosition();
            var direction = light.GetDirection();
            float alpha = isSelected ? 1.0f : 0.50f;
            float lw = isSelected ? 2.5f : 1.2f;

            switch (light.Type)
            {
                case LightType.Point:
                    {
                        float radius = CalculateLightRadius(light.Intensity, light.Constant, light.Linear, light.Quadratic);
                        var color = new Vector4(1f, 1f, 0f, alpha);
                        GizmoCircle.Draw(Matrix4.CreateScale(radius * 2f) * Matrix4.CreateTranslation(position), view, projection, color, lw);
                        GizmoCircle.Draw(Matrix4.CreateRotationX(MathHelper.DegreesToRadians(90)) * Matrix4.CreateScale(radius * 2f) * Matrix4.CreateTranslation(position), view, projection, color, lw);
                        GizmoCircle.Draw(Matrix4.CreateRotationY(MathHelper.DegreesToRadians(90)) * Matrix4.CreateScale(radius * 2f) * Matrix4.CreateTranslation(position), view, projection, color, lw);
                        break;
                    }
                case LightType.Spot:
                    {
                        float coneLength = CalculateLightRadius(light.Intensity, light.Constant, light.Linear, light.Quadratic);
                        float coneRadius = MathF.Tan(MathHelper.DegreesToRadians(light.OuterCutOffDegrees)) * coneLength;
                        var color = new Vector4(1f, 0.5f, 0f, alpha);
                        var rotation = CreateLookAtRotation(Vector3.UnitZ, direction);

                        GizmoCone.Draw(Matrix4.CreateScale(coneRadius * 2f, coneRadius * 2f, coneLength) * rotation * Matrix4.CreateTranslation(position), view, projection, color, lw);
                        GizmoArrow.Draw(Matrix4.CreateScale(0.5f) * rotation * Matrix4.CreateTranslation(position), view, projection, new Vector4(1, 1, 1, alpha), lw);
                        break;
                    }
                case LightType.Directional:
                    {
                        var color = new Vector4(1f, 1f, 1f, alpha);
                        var rotDir = CreateLookAtRotation(Vector3.UnitZ, direction);
                        var modelDir = Matrix4.CreateScale(2f) * rotDir * Matrix4.CreateTranslation(position);

                        GizmoSphere.Draw(Matrix4.CreateScale(0.15f) * Matrix4.CreateTranslation(position), view, projection, color, lw);
                        GizmoArrow.Draw(modelDir, view, projection, color, lw);
                        break;
                    }
            }
        }

        public static void DrawCameraGizmo(GameObject go, Matrix4 view, Matrix4 projection, bool isSelected)
        {
            var cameraComp = go.GetComponent<CameraComponent>();
            var transform = go.GetComponent<Transform>();
            if (cameraComp?.RenderCamera == null || transform == null) return;

            float alpha = isSelected ? 1.0f : 0.45f;
            float lw = isSelected ? 2.5f : 1.2f;
            var color = new Vector4(0f, 1f, 0.5f, alpha);
            var position = transform.GetWorldPosition();
            var forward = transform.Forward;
            var up = transform.Up;

            if (cameraComp.ProjectionMode == ProjectionMode.Perspective)
                GizmoFrustum.DrawPerspective(position, forward, up, cameraComp.Fov, cameraComp.AspectRatio, cameraComp.NearPlane, cameraComp.FarPlane, view, projection, color, lw);
            else
                GizmoFrustum.DrawOrthographic(position, forward, up, cameraComp.OrthoSize, cameraComp.AspectRatio, cameraComp.NearPlane, cameraComp.FarPlane, view, projection, color, lw);
        }

        private static float CalculateLightRadius(float intensity, float constant, float linear, float quadratic)
        {
            float c = constant - (intensity / (5f / 256f));
            float discriminant = linear * linear - 4 * quadratic * c;
            if (discriminant < 0) return 10f;
            return MathF.Max((-linear + MathF.Sqrt(discriminant)) / (2 * quadratic), 1f);
        }

        private static Matrix4 CreateLookAtRotation(Vector3 from, Vector3 to)
        {
            var forward = Vector3.Normalize(to);
            var right = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, forward));
            if (right.LengthSquared < 0.001f)
                right = Vector3.Normalize(Vector3.Cross(Vector3.UnitX, forward));
            var up = Vector3.Cross(forward, right);

            return new Matrix4(
                new Vector4(right, 0),
                new Vector4(up, 0),
                new Vector4(forward, 0),
                new Vector4(0, 0, 0, 1));
        }
    }
}