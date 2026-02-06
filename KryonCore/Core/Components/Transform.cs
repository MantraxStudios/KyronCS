using System.Collections.Generic;
using OpenTK.Mathematics;

namespace KrayonCore
{
    public class Transform : Component
    {
        [ToStorage] public Vector3 Position { get; set; } = Vector3.Zero;

        [ToStorage] public Quaternion Rotation { get; set; } = Quaternion.Identity;

        [ToStorage] public Vector3 Scale { get; set; } = Vector3.One;

        public float X
        {
            get => Position.X;
            set => Position = new Vector3(value, Position.Y, Position.Z);
        }

        public float Y
        {
            get => Position.Y;
            set => Position = new Vector3(Position.X, value, Position.Z);
        }

        public float Z
        {
            get => Position.Z;
            set => Position = new Vector3(Position.X, Position.Y, value);
        }

        // ========== VECTORES DIRECCIONALES ==========

        /// <summary>
        /// Vector Forward (frente) del transform. En OpenGL: -Z por defecto
        /// </summary>
        public Vector3 Forward
        {
            get => Vector3.Transform(-Vector3.UnitZ, Rotation);
        }

        /// <summary>
        /// Vector Right (derecha) del transform. En OpenGL: +X por defecto
        /// </summary>
        public Vector3 Right
        {
            get => Vector3.Transform(Vector3.UnitX, Rotation);
        }

        /// <summary>
        /// Vector Up (arriba) del transform. En OpenGL: +Y por defecto
        /// </summary>
        public Vector3 Up
        {
            get => Vector3.Transform(Vector3.UnitY, Rotation);
        }

        /// <summary>
        /// Vector Back (atrás) del transform. Opuesto a Forward
        /// </summary>
        public Vector3 Back
        {
            get => -Forward;
        }

        /// <summary>
        /// Vector Left (izquierda) del transform. Opuesto a Right
        /// </summary>
        public Vector3 Left
        {
            get => -Right;
        }

        /// <summary>
        /// Vector Down (abajo) del transform. Opuesto a Up
        /// </summary>
        public Vector3 Down
        {
            get => -Up;
        }

        // ========== ROTACIÓN EN EULER ==========

        // Rotación en ángulos de Euler (en grados) para conveniencia
        public Vector3 EulerAngles
        {
            get
            {
                // Extraer ángulos de Euler del quaternion
                var angles = Rotation.ToEulerAngles();
                // Convertir de radianes a grados
                return new Vector3(
                    angles.X * (180f / MathF.PI),
                    angles.Y * (180f / MathF.PI),
                    angles.Z * (180f / MathF.PI)
                );
            }
            set
            {
                // Convertir de grados a radianes y crear quaternion
                Vector3 radians = new Vector3(
                    value.X * (MathF.PI / 180f),
                    value.Y * (MathF.PI / 180f),
                    value.Z * (MathF.PI / 180f)
                );
                Rotation = Quaternion.FromEulerAngles(radians);
            }
        }

        // Propiedades individuales de rotación en Euler (grados)
        public float RotationX
        {
            get => EulerAngles.X;
            set
            {
                Vector3 euler = EulerAngles;
                euler.X = value;
                EulerAngles = euler;
            }
        }

        public float RotationY
        {
            get => EulerAngles.Y;
            set
            {
                Vector3 euler = EulerAngles;
                euler.Y = value;
                EulerAngles = euler;
            }
        }

        public float RotationZ
        {
            get => EulerAngles.Z;
            set
            {
                Vector3 euler = EulerAngles;
                euler.Z = value;
                EulerAngles = euler;
            }
        }

        // Propiedades de escala individual
        public float ScaleX
        {
            get => Scale.X;
            set => Scale = new Vector3(value, Scale.Y, Scale.Z);
        }

        public float ScaleY
        {
            get => Scale.Y;
            set => Scale = new Vector3(Scale.X, value, Scale.Z);
        }

        public float ScaleZ
        {
            get => Scale.Z;
            set => Scale = new Vector3(Scale.X, Scale.Y, value);
        }

        // Propiedades para conveniencia (compatible con código anterior)
        public Vector3 LocalPosition
        {
            get => Position;
            set => Position = value;
        }

        public Vector3 LocalRotation
        {
            get => EulerAngles;
            set => EulerAngles = value;
        }

        public Vector3 LocalScale
        {
            get => Scale;
            set => Scale = value;
        }

        public Transform Parent { get; private set; }
        private List<Transform> _children = new List<Transform>();
        public IReadOnlyList<Transform> Children => _children.AsReadOnly();

        public Transform()
        {
            Position = Vector3.Zero;
            Rotation = Quaternion.Identity;
            Scale = Vector3.One;
        }

        public void SetParent(Transform parent)
        {
            if (Parent != null)
            {
                Parent._children.Remove(this);
            }

            Parent = parent;

            if (parent != null)
            {
                parent._children.Add(this);
            }
        }

        public void SetPosition(float x, float y, float z)
        {
            Position = new Vector3(x, y, z);
        }

        public void SetPosition(Vector3 position)
        {
            Position = position;
        }

        public void Translate(float x, float y, float z)
        {
            Position += new Vector3(x, y, z);
        }

        public void Translate(Vector3 translation)
        {
            Position += translation;
        }

        public void SetRotation(float x, float y, float z)
        {
            EulerAngles = new Vector3(x, y, z);
        }

        public void SetRotation(Vector3 eulerAngles)
        {
            EulerAngles = eulerAngles;
        }

        public void SetRotation(Quaternion rotation)
        {
            Rotation = rotation;
        }

        public void Rotate(float x, float y, float z)
        {
            // Convertir ángulos de Euler (grados) a radianes
            Vector3 radians = new Vector3(
                x * (MathF.PI / 180f),
                y * (MathF.PI / 180f),
                z * (MathF.PI / 180f)
            );

            // Crear quaternion de la rotación incremental
            Quaternion deltaRotation = Quaternion.FromEulerAngles(radians);

            // Aplicar la rotación incremental (multiplicar a la derecha para rotación local)
            Rotation = Quaternion.Normalize(Rotation * deltaRotation);
        }

        public void Rotate(Vector3 eulerAngles)
        {
            Rotate(eulerAngles.X, eulerAngles.Y, eulerAngles.Z);
        }

        public void Rotate(Quaternion rotation)
        {
            // Aplicar rotación quaternion directamente (multiplicar a la derecha para rotación local)
            Rotation = Quaternion.Normalize(Rotation * rotation);
        }

        public void SetScale(float x, float y, float z)
        {
            Scale = new Vector3(x, y, z);
        }

        public void SetScale(Vector3 scale)
        {
            Scale = scale;
        }

        public void SetScale(float uniformScale)
        {
            Scale = new Vector3(uniformScale, uniformScale, uniformScale);
        }

        // ========== MÉTODOS DE ORIENTACIÓN ==========

        /// <summary>
        /// Hace que el transform mire hacia un punto específico
        /// </summary>
        public void LookAt(Vector3 target)
        {
            Vector3 direction = Vector3.Normalize(target - Position);
            LookDirection(direction);
        }

        /// <summary>
        /// Hace que el transform mire en una dirección específica
        /// </summary>
        public void LookDirection(Vector3 direction)
        {
            if (direction.LengthSquared < 0.0001f)
                return;

            direction = Vector3.Normalize(direction);

            // Calcular rotación usando LookAt
            // En OpenGL, forward es -Z
            Vector3 up = Vector3.UnitY;

            // Evitar gimbal lock cuando la dirección es paralela al up
            if (MathF.Abs(Vector3.Dot(direction, up)) > 0.999f)
            {
                up = Vector3.UnitX;
            }

            Matrix4 lookMatrix = Matrix4.LookAt(Vector3.Zero, -direction, up);
            Rotation = Quaternion.FromMatrix(new Matrix3(lookMatrix));
        }

        /// <summary>
        /// Hace que el transform mire hacia otro transform
        /// </summary>
        public void LookAt(Transform target)
        {
            if (target != null)
            {
                LookAt(target.Position);
            }
        }

        public Matrix4 GetLocalMatrix()
        {
            // Orden: S * R * T
            // Se aplica de derecha a izquierda cuando se multiplica con un vector
            return Matrix4.CreateScale(Scale) *
                   Matrix4.CreateFromQuaternion(Rotation) *
                   Matrix4.CreateTranslation(Position);
        }

        public Matrix4 GetWorldMatrix()
        {
            if (Parent != null)
            {
                return GetLocalMatrix() * Parent.GetWorldMatrix();
            }

            return GetLocalMatrix();
        }
    }
}