using OpenTK.Mathematics;

namespace KrayonCore
{
    public enum CameraMovement
    {
        Forward,
        Backward,
        Left,
        Right,
        Up,
        Down
    }

    public enum ProjectionMode
    {
        Perspective,
        Orthographic
    }

    public class Camera
    {
        public Vector3 Position { get; set; }
        public Vector3 Front { get; private set; }
        public Vector3 Up { get; private set; }
        public Vector3 Right { get; private set; }

        private float _yaw = -90.0f;
        private float _pitch = 0.0f;

        public float Yaw
        {
            get => _yaw;
            set
            {
                _yaw = value;
                UpdateVectors();
            }
        }

        public float Pitch
        {
            get => _pitch;
            set
            {
                _pitch = MathHelper.Clamp(value, -89.0f, 89.0f);
                UpdateVectors();
            }
        }

        public ProjectionMode ProjectionMode { get; set; } = ProjectionMode.Orthographic;

        public float Fov { get; set; } = 45.0f;
        public float AspectRatio { get; set; }
        public float NearPlane { get; set; } = 0.1f;
        public float FarPlane { get; set; } = 100.0f;

        public float OrthoSize { get; set; } = 10.0f;
        public float OrthoNear { get; set; } = 0.1f;
        public float OrthoFar { get; set; } = 100.0f;

        public float MovementSpeed { get; set; } = 2.5f;
        public float MouseSensitivity { get; set; } = 0.1f;

        public Camera(Vector3 position, float aspectRatio)
        {
            Position = position;
            AspectRatio = aspectRatio;
            Front = -Vector3.UnitZ;
            Up = Vector3.UnitY;
            UpdateVectors();
        }

        public Matrix4 GetViewMatrix()
        {
            return Matrix4.LookAt(Position, Position + Front, Up);
        }

        public Matrix4 GetProjectionMatrix()
        {
            switch (ProjectionMode)
            {
                case ProjectionMode.Perspective:
                    return GetPerspectiveMatrix();

                case ProjectionMode.Orthographic:
                    return GetOrthographicMatrix();

                default:
                    return GetPerspectiveMatrix();
            }
        }

        private Matrix4 GetPerspectiveMatrix()
        {
            float aspectRatio = AspectRatio > 0 ? AspectRatio : 16.0f / 9.0f;

            return Matrix4.CreatePerspectiveFieldOfView(
                MathHelper.DegreesToRadians(Fov),
                aspectRatio,
                NearPlane,
                FarPlane
            );
        }

        private Matrix4 GetOrthographicMatrix()
        {
            float aspectRatio = AspectRatio > 0 ? AspectRatio : 16.0f / 9.0f;

            return Matrix4.CreateOrthographic(
                OrthoSize * aspectRatio,
                OrthoSize,
                OrthoNear,
                OrthoFar
            );
        }

        public void ToggleProjectionMode()
        {
            ProjectionMode = ProjectionMode == ProjectionMode.Perspective
                ? ProjectionMode.Orthographic
                : ProjectionMode.Perspective;
        }

        public void SetProjectionMode(ProjectionMode mode)
        {
            ProjectionMode = mode;
        }

        public void Move(CameraMovement direction, float deltaTime)
        {
            float velocity = MovementSpeed * deltaTime;

            switch (direction)
            {
                case CameraMovement.Forward:
                    Position += Front * velocity;
                    break;
                case CameraMovement.Backward:
                    Position -= Front * velocity;
                    break;
                case CameraMovement.Left:
                    Position -= Right * velocity;
                    break;
                case CameraMovement.Right:
                    Position += Right * velocity;
                    break;
                case CameraMovement.Up:
                    Position += Up * velocity;
                    break;
                case CameraMovement.Down:
                    Position -= Up * velocity;
                    break;
            }
        }

        public void Rotate(float xOffset, float yOffset, bool constrainPitch = true)
        {
            xOffset *= MouseSensitivity;
            yOffset *= MouseSensitivity;

            Yaw += xOffset;
            Pitch += yOffset;

            if (constrainPitch)
            {
                Pitch = MathHelper.Clamp(Pitch, -89.0f, 89.0f);
            }

            UpdateVectors();
        }

        public void Zoom(float yOffset)
        {
            if (ProjectionMode == ProjectionMode.Perspective)
            {
                Fov -= yOffset;
                Fov = MathHelper.Clamp(Fov, 1.0f, 90.0f);
            }
            else
            {
                float zoomFactor = 1.0f - (yOffset * 0.1f);
                OrthoSize *= zoomFactor;
                OrthoSize = MathHelper.Clamp(OrthoSize, 0.1f, 100.0f);
            }
        }

        private void UpdateVectors()
        {
            Vector3 front;
            front.X = MathF.Cos(MathHelper.DegreesToRadians(Yaw)) * MathF.Cos(MathHelper.DegreesToRadians(Pitch));
            front.Y = MathF.Sin(MathHelper.DegreesToRadians(Pitch));
            front.Z = MathF.Sin(MathHelper.DegreesToRadians(Yaw)) * MathF.Cos(MathHelper.DegreesToRadians(Pitch));

            Front = Vector3.Normalize(front);
            Right = Vector3.Normalize(Vector3.Cross(Front, Vector3.UnitY));
            Up = Vector3.Normalize(Vector3.Cross(Right, Front));
        }

        public void UpdateAspectRatio(int width, int height)
        {
            if (height > 0)
            {
                AspectRatio = (float)width / height;
            }
        }

        public void SetOrthographicSize(float size)
        {
            OrthoSize = size;
        }
    }
}