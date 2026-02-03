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

        // Propiedades de la cámara
        public float Fov { get; set; } = 45.0f;
        public float AspectRatio { get; set; }
        public float NearPlane { get; set; } = 0.1f;
        public float FarPlane { get; set; } = 100.0f;

        // Velocidades
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

        // Obtener matriz de vista
        public Matrix4 GetViewMatrix()
        {
            return Matrix4.LookAt(Position, Position + Front, Up);
        }

        // Obtener matriz de proyección
        public Matrix4 GetProjectionMatrix()
        {
            return Matrix4.CreatePerspectiveFieldOfView(
                MathHelper.DegreesToRadians(Fov),
                AspectRatio,
                NearPlane,
                FarPlane
            );
        }

        // Movimiento de la cámara
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

        // Rotar cámara con mouse
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

        // Zoom con scroll del mouse
        public void Zoom(float yOffset)
        {
            Fov -= yOffset;
            Fov = MathHelper.Clamp(Fov, 1.0f, 90.0f);
        }

        // Actualizar vectores de dirección
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
    }
}