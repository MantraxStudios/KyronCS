using OpenTK.Mathematics;

namespace KrayonCore
{
    public enum CameraMovement { Forward, Backward, Left, Right, Up, Down }
    public enum ProjectionMode { Perspective, Orthographic }

    public class Camera
    {
        // ── Transformación ───────────────────────────────────────────────────
        public Vector3 Position { get; set; }
        public Vector3 Front { get; private set; }
        public Vector3 Up { get; private set; }
        public Vector3 Right { get; private set; }

        // ── Rotación ─────────────────────────────────────────────────────────
        private float _yaw = -90.0f;
        private float _pitch = 0.0f;

        public float Yaw
        {
            get => _yaw;
            set { _yaw = value; UpdateVectors(); }
        }

        public float Pitch
        {
            get => _pitch;
            set { _pitch = MathHelper.Clamp(value, -89.0f, 89.0f); UpdateVectors(); }
        }

        // ── Proyección ───────────────────────────────────────────────────────
        public ProjectionMode ProjectionMode { get; set; } = ProjectionMode.Orthographic;
        public bool IsPerspective => ProjectionMode == ProjectionMode.Perspective;

        public float Fov { get; set; } = 45.0f;
        public float AspectRatio { get; set; }
        public float NearPlane { get; set; } = 0.1f;
        public float FarPlane { get; set; } = 1000.0f;

        public float OrthoSize { get; set; } = 10.0f;
        public float OrthoNear { get; set; } = 0.1f;
        public float OrthoFar { get; set; } = 1000.0f;

        // ── Input ────────────────────────────────────────────────────────────
        public float MovementSpeed { get; set; } = 2.5f;
        public float MouseSensitivity { get; set; } = 0.1f;

        // ── Constructor ──────────────────────────────────────────────────────
        public Camera(Vector3 position, float aspectRatio)
        {
            Position = position;
            AspectRatio = aspectRatio;
            Front = -Vector3.UnitZ;
            Up = Vector3.UnitY;
            UpdateVectors();
        }

        // ── Matrices ─────────────────────────────────────────────────────────
        public Matrix4 GetViewMatrix()
            => Matrix4.LookAt(Position, Position + Front, Up);

        public Matrix4 GetProjectionMatrix()
            => ProjectionMode == ProjectionMode.Perspective
                ? GetPerspectiveMatrix()
                : GetOrthographicMatrix();

        private Matrix4 GetPerspectiveMatrix()
        {
            float aspect = AspectRatio > 0 ? AspectRatio : 16f / 9f;
            return Matrix4.CreatePerspectiveFieldOfView(
                MathHelper.DegreesToRadians(Fov), aspect, NearPlane, FarPlane);
        }

        private Matrix4 GetOrthographicMatrix()
        {
            float aspect = AspectRatio > 0 ? AspectRatio : 16f / 9f;
            return Matrix4.CreateOrthographic(
                OrthoSize * aspect, OrthoSize, OrthoNear, OrthoFar);
        }

        // ── Controles ────────────────────────────────────────────────────────
        public void ToggleProjectionMode()
            => ProjectionMode = IsPerspective
                ? ProjectionMode.Orthographic
                : ProjectionMode.Perspective;

        public void SetProjectionMode(ProjectionMode mode)
            => ProjectionMode = mode;

        public void Move(CameraMovement direction, float deltaTime)
        {
            float v = MovementSpeed * deltaTime;
            Position += direction switch
            {
                CameraMovement.Forward => Front * v,
                CameraMovement.Backward => -Front * v,
                CameraMovement.Left => -Right * v,
                CameraMovement.Right => Right * v,
                CameraMovement.Up => Up * v,
                CameraMovement.Down => -Up * v,
                _ => Vector3.Zero
            };
        }

        public void Rotate(float xOffset, float yOffset, bool constrainPitch = true)
        {
            Yaw += xOffset * MouseSensitivity;
            Pitch += yOffset * MouseSensitivity;

            if (constrainPitch)
                Pitch = MathHelper.Clamp(Pitch, -89.0f, 89.0f);

            UpdateVectors();
        }

        public void Zoom(float yOffset)
        {
            if (IsPerspective)
                Fov = MathHelper.Clamp(Fov - yOffset, 1.0f, 90.0f);
            else
                OrthoSize = MathHelper.Clamp(OrthoSize * (1f - yOffset * 0.1f), 0.1f, 100.0f);
        }

        public void UpdateAspectRatio(int width, int height)
        {
            if (height > 0)
                AspectRatio = (float)width / height;
        }

        public void SetOrthographicSize(float size)
            => OrthoSize = size;

        // ── Interno ──────────────────────────────────────────────────────────
        private void UpdateVectors()
        {
            float yawRad = MathHelper.DegreesToRadians(_yaw);
            float pitchRad = MathHelper.DegreesToRadians(_pitch);

            Front = Vector3.Normalize(new Vector3(
                MathF.Cos(yawRad) * MathF.Cos(pitchRad),
                MathF.Sin(pitchRad),
                MathF.Sin(yawRad) * MathF.Cos(pitchRad)
            ));

            Right = Vector3.Normalize(Vector3.Cross(Front, Vector3.UnitY));
            Up = Vector3.Normalize(Vector3.Cross(Right, Front));
        }
    }
}