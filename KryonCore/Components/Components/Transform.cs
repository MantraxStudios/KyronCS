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

        // -----------------------------------------------------------------------
        // Direcciones locales: se transforman con la rotación world del objeto
        // -----------------------------------------------------------------------
        public Vector3 Forward => Vector3.Normalize(Vector3.Transform(-Vector3.UnitZ, GetWorldRotation()));
        public Vector3 Right => Vector3.Normalize(Vector3.Transform(Vector3.UnitX, GetWorldRotation()));
        public Vector3 Up => Vector3.Normalize(Vector3.Transform(Vector3.UnitY, GetWorldRotation()));
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
            set { Vector3 e = EulerAngles; e.X = value; EulerAngles = e; }
        }

        public float RotationY
        {
            get => EulerAngles.Y;
            set { Vector3 e = EulerAngles; e.Y = value; EulerAngles = e; }
        }

        public float RotationZ
        {
            get => EulerAngles.Z;
            set { Vector3 e = EulerAngles; e.Z = value; EulerAngles = e; }
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

        // -----------------------------------------------------------------------
        // Jerarquía
        // -----------------------------------------------------------------------
        public void SetParent(Transform parent, bool worldPositionStays = true)
        {
            if (Parent == parent) return;

            if (parent != null && IsDescendantOf(parent))
            {
                Console.WriteLine("Error: Cannot set parent - would create circular dependency");
                return;
            }

            if (worldPositionStays)
            {
                var worldTransforms = new Dictionary<Transform, TransformData>();
                SaveWorldTransforms(this, worldTransforms);

                Parent?._children.Remove(this);
                Parent = parent;
                parent?._children.Add(this);

                RestoreWorldTransforms(worldTransforms);
            }
            else
            {
                Parent?._children.Remove(this);
                Parent = parent;
                parent?._children.Add(this);
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
                SaveWorldTransforms(child, data);
        }

        private void RestoreWorldTransforms(Dictionary<Transform, TransformData> data)
        {
            foreach (var kvp in data)
            {
                kvp.Key.SetWorldPosition(kvp.Value.Position);
                kvp.Key.SetWorldRotation(kvp.Value.Rotation);
                kvp.Key.SetWorldScale(kvp.Value.Scale);
            }
        }

        private bool IsDescendantOf(Transform potentialAncestor)
        {
            Transform current = potentialAncestor;
            while (current != null)
            {
                if (current == this) return true;
                current = current.Parent;
            }
            return false;
        }

        // -----------------------------------------------------------------------
        // MATRICES
        //
        // OpenTK es ROW-MAJOR y usa vectores-fila: resultado = vec4 * matrix
        // El orden TRS correcto para row-major es: Scale * Rotation * Translation
        // (equivale a aplicar primero escala, luego rotación, luego traslación)
        //
        // GetWorldMatrix() = local * parent  (child-to-parent, luego parent-to-world)
        // -----------------------------------------------------------------------

        /// <summary>
        /// Matriz local (object-space → parent-space).
        /// Orden correcto para OpenTK row-major: S * R * T
        /// </summary>
        public Matrix4 GetLocalMatrix()
        {
            Matrix4 S = Matrix4.CreateScale(Scale);
            Matrix4 R = Matrix4.CreateFromQuaternion(Rotation);
            Matrix4 T = Matrix4.CreateTranslation(Position);
            // Con vectores-fila: punto * S * R * T  → escala, rota, traslada. CORRECTO.
            return S * R * T;
        }

        /// <summary>
        /// Matriz world (object-space → world-space).
        /// Se construye concatenando la local con la del padre recursivamente.
        /// </summary>
        public Matrix4 GetWorldMatrix()
        {
            Matrix4 local = GetLocalMatrix();
            if (Parent != null)
                return local * Parent.GetWorldMatrix();
            return local;
        }

        // -----------------------------------------------------------------------
        // Posición world
        // -----------------------------------------------------------------------
        public Vector3 GetWorldPosition()
        {
            if (Parent == null) return Position;

            // Transformamos la posición local con la matriz world del padre.
            // OpenTK: vec4 * matrix  (row-vector × row-major matrix)
            Vector4 worldPos = new Vector4(Position, 1f) * Parent.GetWorldMatrix();
            return worldPos.Xyz;
        }

        public void SetWorldPosition(Vector3 worldPosition)
        {
            if (Parent == null)
            {
                Position = worldPosition;
                return;
            }
            // Invertimos la world matrix del padre para obtener la posición local.
            Matrix4 parentInv = Matrix4.Invert(Parent.GetWorldMatrix());
            Vector4 localPos = new Vector4(worldPosition, 1f) * parentInv;
            Position = localPos.Xyz;
        }

        // -----------------------------------------------------------------------
        // Rotación world
        //
        // La rotación world se compone padre-primero: parentWorld * localRotation
        // (en espacio de quaterniones: rot_world = rot_parent * rot_local)
        // -----------------------------------------------------------------------
        public Quaternion GetWorldRotation()
        {
            if (Parent == null) return Rotation;
            // Orden correcto: primero aplica la rotación del padre, luego la local.
            return Parent.GetWorldRotation() * Rotation;
        }

        public void SetWorldRotation(Quaternion worldRotation)
        {
            if (Parent == null)
            {
                Rotation = worldRotation;
                return;
            }
            // local = inverse(parentWorld) * worldRotation
            Quaternion parentWorldInv = Quaternion.Invert(Parent.GetWorldRotation());
            Rotation = Quaternion.Normalize(parentWorldInv * worldRotation);
        }

        // -----------------------------------------------------------------------
        // Escala world (escala no-uniforme puede distorsionar si hay rotaciones,
        // pero para escala uniforme y casos simples este cálculo es correcto)
        // -----------------------------------------------------------------------
        public Vector3 GetWorldScale()
        {
            if (Parent == null) return Scale;
            Vector3 ps = Parent.GetWorldScale();
            return new Vector3(Scale.X * ps.X, Scale.Y * ps.Y, Scale.Z * ps.Z);
        }

        public void SetWorldScale(Vector3 worldScale)
        {
            if (Parent == null)
            {
                Scale = worldScale;
                return;
            }
            Vector3 ps = Parent.GetWorldScale();
            Scale = new Vector3(
                ps.X != 0f ? worldScale.X / ps.X : 1f,
                ps.Y != 0f ? worldScale.Y / ps.Y : 1f,
                ps.Z != 0f ? worldScale.Z / ps.Z : 1f
            );
        }

        // -----------------------------------------------------------------------
        // Descomposición
        // -----------------------------------------------------------------------
        public void Decompose(out Vector3 scale, out Quaternion rotation, out Vector3 translation)
        {
            translation = GetWorldPosition();
            rotation = GetWorldRotation();
            scale = GetWorldScale();
        }

        public void DecomposeLocal(out Vector3 scale, out Quaternion rotation, out Vector3 translation)
        {
            translation = Position;
            rotation = Rotation;
            scale = Scale;
        }

        // -----------------------------------------------------------------------
        // Helpers de posición
        // -----------------------------------------------------------------------
        public void SetPosition(float x, float y, float z) => SetPosition(new Vector3(x, y, z));

        public void SetPosition(Vector3 position)
        {
            Position = position;
            if (GameObject.HasComponent<Rigidbody>())
                GameObject.GetComponent<Rigidbody>().MovePosition(GetWorldPosition());
        }

        public void Translate(float x, float y, float z) => Position += new Vector3(x, y, z);
        public void Translate(Vector3 translation) => Position += translation;

        // -----------------------------------------------------------------------
        // Helpers de rotación
        // -----------------------------------------------------------------------
        public void SetRotation(float x, float y, float z) => EulerAngles = new Vector3(x, y, z);
        public void SetRotation(Vector3 eulerAngles) => EulerAngles = eulerAngles;
        public void SetRotation(Quaternion rotation) => Rotation = rotation;

        public void Rotate(float x, float y, float z)
        {
            Vector3 radians = new Vector3(
                x * (MathF.PI / 180f),
                y * (MathF.PI / 180f),
                z * (MathF.PI / 180f)
            );
            Quaternion delta = Quaternion.FromEulerAngles(radians);
            // Pre-multiplicar: rota en espacio local del objeto
            Rotation = Quaternion.Normalize(Rotation * delta);
        }

        public void Rotate(Vector3 eulerAngles) => Rotate(eulerAngles.X, eulerAngles.Y, eulerAngles.Z);
        public void Rotate(Quaternion rotation) => Rotation = Quaternion.Normalize(Rotation * rotation);

        // -----------------------------------------------------------------------
        // Helpers de escala
        // -----------------------------------------------------------------------
        public void SetScale(float x, float y, float z) => Scale = new Vector3(x, y, z);
        public void SetScale(Vector3 scale) => Scale = scale;
        public void SetScale(float uniform) => Scale = new Vector3(uniform, uniform, uniform);

        // -----------------------------------------------------------------------
        // LookAt / LookDirection
        //
        // Matrix4.LookAt genera una VIEW matrix (mundo→cámara, con negativo).
        // Para una MODEL matrix necesitamos construirla a mano como base
        // ortonormal: Forward = dirección, Right = cross(up,fwd), Up = cross(fwd,right).
        // -----------------------------------------------------------------------
        public void LookAt(Vector3 target)
        {
            Vector3 worldPos = GetWorldPosition();
            Vector3 dir = target - worldPos;
            if (dir.LengthSquared < 1e-6f) return;
            LookDirection(Vector3.Normalize(dir));
        }

        public void LookAt(Transform target)
        {
            if (target != null) LookAt(target.GetWorldPosition());
        }

        public void LookDirection(Vector3 direction)
        {
            if (direction.LengthSquared < 1e-6f) return;

            direction = Vector3.Normalize(direction);

            // Elegir un vector "up" que no sea paralelo a direction
            Vector3 up = Vector3.UnitY;
            if (MathF.Abs(Vector3.Dot(direction, up)) > 0.999f)
                up = Vector3.UnitZ;

            // Construir base ortonormal (convenio mano-derecha, -Z = forward)
            // forward = -Z en espacio de objeto → direction en world
            Vector3 fwd = direction;                              // -Z local → direction
            Vector3 right = Vector3.Normalize(Vector3.Cross(up, fwd));   // +X local
            Vector3 newUp = Vector3.Cross(fwd, right);             // +Y local (ya normalizado)

            // Construir la matriz de rotación 3x3 (row-major: filas = ejes X,Y,Z)
            Matrix3 rotMat = new Matrix3(
                right.X, right.Y, right.Z,   // Row0 = Right (+X)
                newUp.X, newUp.Y, newUp.Z,   // Row1 = Up    (+Y)
                -fwd.X, -fwd.Y, -fwd.Z    // Row2 = Back  (+Z, porque forward = -Z)
            );

            Quaternion worldRot = Quaternion.FromMatrix(rotMat);
            SetWorldRotation(Quaternion.Normalize(worldRot));
        }
    }
}