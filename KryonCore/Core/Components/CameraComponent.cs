using OpenTK.Mathematics;
using KrayonCore;

namespace KrayonCore
{
    public class CameraComponent : Component
    {
        public Vector3 Position
        {
            get => _position;
            set
            {
                _position = value;
                if (GameObject != null && GameObject.Transform != null)
                {
                    GameObject.Transform.SetPosition(value.X, value.Y, value.Z);
                }
            }
        }

        private Vector3 _position;
        public Vector3 Front { get; private set; }
        public Vector3 Up { get; private set; }
        public Vector3 Right { get; private set; }

        [ToStorage] private float _yaw = -90.0f;
        [ToStorage] private float _pitch = 0.0f;

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

        [ToStorage] public float Fov { get; set; } = 45.0f;
        [ToStorage] public float AspectRatio { get; set; } = 16.0f / 9.0f; // Valor por defecto 16:9
        [ToStorage] public float NearPlane { get; set; } = 0.1f;
        [ToStorage] public float FarPlane { get; set; } = 100.0f;

        [ToStorage] public float MovementSpeed { get; set; } = 2.5f;
        [ToStorage] public float MouseSensitivity { get; set; } = 0.1f;

        public CameraComponent()
        {
            Front = -Vector3.UnitZ;
            Up = Vector3.UnitY;
            AspectRatio = 16.0f / 9.0f; // Asegurar valor por defecto
        }

        public override void Awake()
        {
            // Asegurar que AspectRatio nunca sea 0
            if (AspectRatio <= 0)
            {
                AspectRatio = 16.0f / 9.0f;
            }
        }

        public override void Start()
        {
            UpdateVectors();
            SyncWithTransform();
        }

        public override void Update(float deltaTime)
        {
            SyncWithTransform();
        }

        private void SyncWithTransform()
        {
            if (GameObject?.Transform != null)
            {
                _position = new Vector3(
                    GameObject.Transform.X,
                    GameObject.Transform.Y,
                    GameObject.Transform.Z
                );
            }
        }

        public Matrix4 GetViewMatrix()
        {
            return Matrix4.LookAt(Position, Position + Front, Up);
        }

        public Matrix4 GetProjectionMatrix()
        {
            // Protección contra AspectRatio inválido
            float aspectRatio = AspectRatio > 0 ? AspectRatio : 16.0f / 9.0f;

            return Matrix4.CreatePerspectiveFieldOfView(
                MathHelper.DegreesToRadians(Fov),
                aspectRatio,
                NearPlane,
                FarPlane
            );
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
            Fov -= yOffset;
            Fov = MathHelper.Clamp(Fov, 1.0f, 90.0f);
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

        /// <summary>
        /// Actualiza el AspectRatio basado en el tamaño de la ventana
        /// </summary>
        public void UpdateAspectRatio(int width, int height)
        {
            if (height > 0)
            {
                AspectRatio = (float)width / height;
            }
        }
    }
}