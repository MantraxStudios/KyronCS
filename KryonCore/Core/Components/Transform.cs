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

        // Rotación en ángulos de Euler (en grados) para conveniencia
        public Vector3 EulerAngles
        {
            get => Rotation.ToEulerAngles() * (180f / MathF.PI); // Convertir a grados
            set
            {
                // Convertir de grados a radianes y crear quaternion
                Vector3 radians = value * (MathF.PI / 180f);
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
            Vector3 euler = EulerAngles;
            euler.X += x;
            euler.Y += y;
            euler.Z += z;
            EulerAngles = euler;
        }

        public void Rotate(Vector3 eulerAngles)
        {
            EulerAngles += eulerAngles;
        }

        public void Rotate(Quaternion rotation)
        {
            Rotation = rotation * Rotation;
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

        public Matrix4 GetLocalMatrix()
        {
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