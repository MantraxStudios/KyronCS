using System.Collections.Generic;
using OpenTK.Mathematics;
using System;

namespace KrayonCore.Components.Components
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

        public Vector3 Forward => Vector3.Transform(-Vector3.UnitZ, Rotation);
        public Vector3 Right => Vector3.Transform(Vector3.UnitX, Rotation);
        public Vector3 Up => Vector3.Transform(Vector3.UnitY, Rotation);
        public Vector3 Back => -Forward;
        public Vector3 Left => -Right;
        public Vector3 Down => -Up;

        public Vector3 EulerAngles
        {
            get
            {
                var angles = Rotation.ToEulerAngles();
                return new Vector3(
                    angles.X * (180f / MathF.PI),
                    angles.Y * (180f / MathF.PI),
                    angles.Z * (180f / MathF.PI)
                );
            }
            set
            {
                Vector3 radians = new Vector3(
                    value.X * (MathF.PI / 180f),
                    value.Y * (MathF.PI / 180f),
                    value.Z * (MathF.PI / 180f)
                );
                Rotation = Quaternion.FromEulerAngles(radians);
            }
        }

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

        private struct TransformData
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Scale;
        }

        public Transform()
        {
            Position = Vector3.Zero;
            Rotation = Quaternion.Identity;
            Scale = Vector3.One;
        }

        public void SetParent(Transform parent, bool worldPositionStays = true)
        {
            if (Parent == parent)
                return;

            if (parent != null && IsDescendantOf(parent))
            {
                Console.WriteLine("Error: Cannot set parent - would create circular dependency");
                return;
            }

            if (worldPositionStays)
            {
                Dictionary<Transform, TransformData> worldTransforms = new Dictionary<Transform, TransformData>();
                SaveWorldTransforms(this, worldTransforms);

                if (Parent != null)
                {
                    Parent._children.Remove(this);
                }

                Parent = parent;

                if (parent != null)
                {
                    parent._children.Add(this);
                }

                RestoreWorldTransforms(worldTransforms);
            }
            else
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
        }

        private void SaveWorldTransforms(Transform root, Dictionary<Transform, TransformData> data)
        {
            data[root] = new TransformData
            {
                Position = root.GetWorldPosition(),
                Rotation = root.GetWorldRotation(),
                Scale = root.GetWorldScale()
            };

            foreach (var child in root._children)
            {
                SaveWorldTransforms(child, data);
            }
        }

        private void RestoreWorldTransforms(Dictionary<Transform, TransformData> data)
        {
            foreach (var kvp in data)
            {
                Transform t = kvp.Key;
                TransformData worldData = kvp.Value;

                t.SetWorldPosition(worldData.Position);
                t.SetWorldRotation(worldData.Rotation);
                t.SetWorldScale(worldData.Scale);
            }
        }

        private bool IsDescendantOf(Transform potentialAncestor)
        {
            Transform current = potentialAncestor;
            while (current != null)
            {
                if (current == this)
                    return true;
                current = current.Parent;
            }
            return false;
        }

        public Vector3 GetWorldPosition()
        {
            if (Parent != null)
            {
                Matrix4 parentWorld = Parent.GetWorldMatrix();
                Vector4 localPos4 = new Vector4(Position, 1.0f);
                Vector4 worldPos4 = localPos4 * parentWorld;
                return new Vector3(worldPos4.X, worldPos4.Y, worldPos4.Z);
            }
            return Position;
        }

        public void SetWorldPosition(Vector3 worldPosition)
        {
            if (Parent != null)
            {
                Matrix4 parentWorldInverse = Matrix4.Invert(Parent.GetWorldMatrix());
                Vector4 worldPos4 = new Vector4(worldPosition, 1.0f);
                Vector4 localPos4 = worldPos4 * parentWorldInverse;
                Position = new Vector3(localPos4.X, localPos4.Y, localPos4.Z);
            }
            else
            {
                Position = worldPosition;
            }
        }

        public Quaternion GetWorldRotation()
        {
            if (Parent != null)
            {
                return Parent.GetWorldRotation() * Rotation;
            }
            return Rotation;
        }

        public void SetWorldRotation(Quaternion worldRotation)
        {
            if (Parent != null)
            {
                Quaternion parentWorldRotation = Parent.GetWorldRotation();
                Rotation = Quaternion.Invert(parentWorldRotation) * worldRotation;
            }
            else
            {
                Rotation = worldRotation;
            }
        }

        public Vector3 GetWorldScale()
        {
            if (Parent != null)
            {
                Vector3 parentScale = Parent.GetWorldScale();
                return new Vector3(
                    Scale.X * parentScale.X,
                    Scale.Y * parentScale.Y,
                    Scale.Z * parentScale.Z
                );
            }
            return Scale;
        }

        public void SetWorldScale(Vector3 worldScale)
        {
            if (Parent != null)
            {
                Vector3 parentScale = Parent.GetWorldScale();
                Scale = new Vector3(
                    parentScale.X != 0 ? worldScale.X / parentScale.X : 1,
                    parentScale.Y != 0 ? worldScale.Y / parentScale.Y : 1,
                    parentScale.Z != 0 ? worldScale.Z / parentScale.Z : 1
                );
            }
            else
            {
                Scale = worldScale;
            }
        }

        public void SetPosition(float x, float y, float z)
        {
            Position = new Vector3(x, y, z);

            if (GameObject.HasComponent<Rigidbody>())
                GameObject.GetComponent<Rigidbody>().MovePosition(new Vector3(x, y, z));
        }

        public void SetPosition(Vector3 position)
        {
            Position = position;

            if (GameObject.HasComponent<Rigidbody>())
                GameObject.GetComponent<Rigidbody>().MovePosition(Position);
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
            Vector3 radians = new Vector3(
                x * (MathF.PI / 180f),
                y * (MathF.PI / 180f),
                z * (MathF.PI / 180f)
            );

            Quaternion deltaRotation = Quaternion.FromEulerAngles(radians);
            Rotation = Quaternion.Normalize(Rotation * deltaRotation);
        }

        public void Rotate(Vector3 eulerAngles)
        {
            Rotate(eulerAngles.X, eulerAngles.Y, eulerAngles.Z);
        }

        public void Rotate(Quaternion rotation)
        {
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

        public void LookAt(Vector3 target)
        {
            Vector3 direction = Vector3.Normalize(target - GetWorldPosition());
            LookDirection(direction);
        }

        public void LookDirection(Vector3 direction)
        {
            if (direction.LengthSquared < 0.0001f)
                return;

            direction = Vector3.Normalize(direction);
            Vector3 up = Vector3.UnitY;

            if (MathF.Abs(Vector3.Dot(direction, up)) > 0.999f)
            {
                up = Vector3.UnitX;
            }

            Matrix4 lookMatrix = Matrix4.LookAt(Vector3.Zero, -direction, up);
            Quaternion worldRotation = Quaternion.FromMatrix(new Matrix3(lookMatrix));
            SetWorldRotation(worldRotation);
        }

        public void LookAt(Transform target)
        {
            if (target != null)
            {
                LookAt(target.GetWorldPosition());
            }
        }

        public Matrix4 GetLocalMatrix()
        {
            Matrix4 scaleMatrix = Matrix4.CreateScale(Scale);
            Matrix4 rotationMatrix = Matrix4.CreateFromQuaternion(Rotation);
            Matrix4 translationMatrix = Matrix4.CreateTranslation(Position);
            
            return scaleMatrix * rotationMatrix * translationMatrix;
        }

        public Matrix4 GetWorldMatrix()
        {
            Matrix4 localMatrix = GetLocalMatrix();
            
            if (Parent != null)
            {
                return localMatrix * Parent.GetWorldMatrix();
            }

            return localMatrix;
        }
    }
}