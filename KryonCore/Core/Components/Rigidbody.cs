using System;
using OpenTK.Mathematics;
using JoltPhysicsSharp;
using KrayonCore.Physics;

namespace KrayonCore
{
    /// <summary>
    /// Componente que agrega física a un GameObject
    /// </summary>
    public class Rigidbody : Component
    {
        private Body _body;
        private bool _isInitialized = false;

        // Propiedades de configuración
        [ToStorage] public MotionType MotionType { get; set; } = MotionType.Dynamic;
        [ToStorage] public ObjectLayer Layer { get; set; } = WorldPhysic.Layers.Moving;
        [ToStorage] public ShapeType ShapeType { get; set; } = ShapeType.Box;
        [ToStorage] public Vector3 ShapeSize { get; set; } = Vector3.One;
        [ToStorage] public float Mass { get; set; } = 1.0f;
        [ToStorage] public bool UseGravity { get; set; } = true;
        [ToStorage] public bool IsKinematic { get; set; } = false;

        // Restricciones de movimiento
        [ToStorage] public bool FreezePositionX { get; set; } = false;
        [ToStorage] public bool FreezePositionY { get; set; } = false;
        [ToStorage] public bool FreezePositionZ { get; set; } = false;
        [ToStorage] public bool FreezeRotationX { get; set; } = false;
        [ToStorage] public bool FreezeRotationY { get; set; } = false;
        [ToStorage] public bool FreezeRotationZ { get; set; } = false;

        // Propiedades físicas
        [ToStorage] public float LinearDamping { get; set; } = 0.05f;
        [ToStorage] public float AngularDamping { get; set; } = 0.05f;
        [ToStorage] public float Friction { get; set; } = 0.5f;
        [ToStorage] public float Restitution { get; set; } = 0.0f;

        // Referencia al cuerpo de física
        public Body Body => _body;

        public override void Awake()
        {
            base.Start();
            InitializePhysics();
        }

        private void InitializePhysics()
        {
            if (_isInitialized || GameObject?.Scene?.PhysicsWorld == null)
                return;

            var physicsWorld = GameObject.Scene.PhysicsWorld;
            var transform = GameObject.Transform;

            // Convertir de OpenTK a System.Numerics para JoltPhysics
            System.Numerics.Vector3 position = ToNumerics(transform.Position);
            System.Numerics.Quaternion rotation = ToNumerics(transform.Rotation);

            // Determinar el MotionType final
            MotionType finalMotionType = IsKinematic ? MotionType.Kinematic : MotionType;

            // Crear el cuerpo físico según el tipo de forma
            switch (ShapeType)
            {
                case ShapeType.Box:
                    System.Numerics.Vector3 halfExtent = ToNumerics(ShapeSize * 0.5f);
                    _body = physicsWorld.CreateBox(
                        halfExtent,
                        position,
                        rotation,
                        finalMotionType,
                        Layer
                    );
                    break;

                case ShapeType.Sphere:
                    _body = physicsWorld.CreateSphere(
                        ShapeSize.X * 0.5f,  // Radio
                        position,
                        rotation,
                        finalMotionType,
                        Layer
                    );
                    break;

                case ShapeType.Capsule:
                    _body = physicsWorld.CreateCapsule(
                        ShapeSize.Y * 0.5f,  // Half height
                        ShapeSize.X * 0.5f,  // Radio
                        position,
                        rotation,
                        finalMotionType,
                        Layer
                    );
                    break;
            }

            if (_body != null)
            {
                var bodyInterface = physicsWorld.BodyInterface;

                // Configurar propiedades físicas
                // Nota: Algunas propiedades pueden variar según la versión de JoltPhysicsSharp
                // bodyInterface.SetFriction(_body.ID, Friction);
                // bodyInterface.SetRestitution(_body.ID, Restitution);

                // Configurar gravedad
                if (!UseGravity && finalMotionType == MotionType.Dynamic)
                {
                    // Desactivar gravedad
                    // bodyInterface.SetGravityFactor(_body.ID, 0.0f);
                }
            }

            _isInitialized = true;
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);
            // La sincronización se hace en GameScene.SyncPhysicsToGameObjects()
        }

        /// <summary>
        /// Sincroniza la posición y rotación desde el motor de física al Transform
        /// </summary>
        internal void SyncFromPhysics()
        {
            if (_body == null || GameObject?.Scene?.PhysicsWorld == null || !Enabled)
                return;

            var bodyInterface = GameObject.Scene.PhysicsWorld.BodyInterface;

            // Obtener posición y rotación del cuerpo físico
            System.Numerics.Vector3 physicsPosition = bodyInterface.GetPosition(_body.ID);
            System.Numerics.Quaternion physicsRotation = bodyInterface.GetRotation(_body.ID);

            // Convertir de System.Numerics a OpenTK
            Vector3 position = ToOpenTK(physicsPosition);
            Quaternion rotation = ToOpenTK(physicsRotation);

            // Aplicar restricciones de posición
            if (FreezePositionX) position.X = GameObject.Transform.Position.X;
            if (FreezePositionY) position.Y = GameObject.Transform.Position.Y;
            if (FreezePositionZ) position.Z = GameObject.Transform.Position.Z;

            // Actualizar el Transform
            GameObject.Transform.Position = position;

            // Solo actualizar rotación si no está completamente congelada
            if (!FreezeRotationX || !FreezeRotationY || !FreezeRotationZ)
            {
                GameObject.Transform.Rotation = rotation;
            }

        }

        /// <summary>
        /// Sincroniza la posición y rotación desde el Transform al motor de física
        /// </summary>
        public void SyncToPhysics()
        {
            if (_body == null || GameObject?.Scene?.PhysicsWorld == null)
                return;

            var bodyInterface = GameObject.Scene.PhysicsWorld.BodyInterface;

            bodyInterface.SetPosition(
                _body.ID,
                ToNumerics(GameObject.Transform.Position),
                Activation.Activate
            );

            bodyInterface.SetRotation(
                _body.ID,
                ToNumerics(GameObject.Transform.Rotation),
                Activation.Activate
            );
        }

        // ============================================================
        // Métodos de física
        // ============================================================

        /// <summary>
        /// Aplica una fuerza continua al cuerpo
        /// </summary>
        public void AddForce(Vector3 force)
        {
            if (_body != null && GameObject?.Scene?.PhysicsWorld != null)
            {
                GameObject.Scene.PhysicsWorld.BodyInterface.AddForce(
                    _body.ID,
                    ToNumerics(force)
                );
            }
        }

        /// <summary>
        /// Aplica un impulso instantáneo al cuerpo
        /// </summary>
        public void AddImpulse(Vector3 impulse)
        {
            if (_body != null && GameObject?.Scene?.PhysicsWorld != null)
            {
                GameObject.Scene.PhysicsWorld.BodyInterface.AddImpulse(
                    _body.ID,
                    ToNumerics(impulse)
                );
            }
        }

        /// <summary>
        /// Aplica torque (rotación) al cuerpo
        /// </summary>
        public void AddTorque(Vector3 torque)
        {
            if (_body != null && GameObject?.Scene?.PhysicsWorld != null)
            {
                GameObject.Scene.PhysicsWorld.BodyInterface.AddTorque(
                    _body.ID,
                    ToNumerics(torque)
                );
            }
        }

        /// <summary>
        /// Establece la velocidad lineal del cuerpo
        /// </summary>
        public void SetVelocity(Vector3 velocity)
        {
            if (_body != null && GameObject?.Scene?.PhysicsWorld != null)
            {
                GameObject.Scene.PhysicsWorld.BodyInterface.SetLinearVelocity(
                    _body.ID,
                    ToNumerics(velocity)
                );
            }
        }

        /// <summary>
        /// Obtiene la velocidad lineal del cuerpo
        /// </summary>
        public Vector3 GetVelocity()
        {
            if (_body != null && GameObject?.Scene?.PhysicsWorld != null)
            {
                return ToOpenTK(
                    GameObject.Scene.PhysicsWorld.BodyInterface.GetLinearVelocity(_body.ID)
                );
            }
            return Vector3.Zero;
        }

        /// <summary>
        /// Establece la velocidad angular del cuerpo
        /// </summary>
        public void SetAngularVelocity(Vector3 angularVelocity)
        {
            if (_body != null && GameObject?.Scene?.PhysicsWorld != null)
            {
                GameObject.Scene.PhysicsWorld.BodyInterface.SetAngularVelocity(
                    _body.ID,
                    ToNumerics(angularVelocity)
                );
            }
        }

        /// <summary>
        /// Obtiene la velocidad angular del cuerpo
        /// </summary>
        public Vector3 GetAngularVelocity()
        {
            if (_body != null && GameObject?.Scene?.PhysicsWorld != null)
            {
                return ToOpenTK(
                    GameObject.Scene.PhysicsWorld.BodyInterface.GetAngularVelocity(_body.ID)
                );
            }
            return Vector3.Zero;
        }

        /// <summary>
        /// Mueve el cuerpo a una posición específica (solo para cuerpos cinemáticos)
        /// </summary>
        public void MovePosition(Vector3 position)
        {
            if (_body != null && GameObject?.Scene?.PhysicsWorld != null)
            {
                GameObject.Scene.PhysicsWorld.BodyInterface.MoveKinematic(
                    _body.ID,
                    ToNumerics(position),
                    ToNumerics(GameObject.Transform.Rotation),
                    0.016f // deltaTime por defecto
                );
            }
        }

        /// <summary>
        /// Rota el cuerpo a una rotación específica (solo para cuerpos cinemáticos)
        /// </summary>
        public void MoveRotation(Quaternion rotation)
        {
            if (_body != null && GameObject?.Scene?.PhysicsWorld != null)
            {
                GameObject.Scene.PhysicsWorld.BodyInterface.MoveKinematic(
                    _body.ID,
                    ToNumerics(GameObject.Transform.Position),
                    ToNumerics(rotation),
                    0.016f // deltaTime por defecto
                );
            }
        }

        public override void OnDestroy()
        {
            base.OnDestroy();

            if (_body != null && GameObject?.Scene?.PhysicsWorld != null)
            {
                GameObject.Scene.PhysicsWorld.RemoveBody(_body);
                _body = null;
            }

            _isInitialized = false;
        }

        // ============================================================
        // Métodos de conversión entre OpenTK y System.Numerics
        // ============================================================

        private static System.Numerics.Vector3 ToNumerics(Vector3 v)
        {
            return new System.Numerics.Vector3(v.X, v.Y, v.Z);
        }

        private static Vector3 ToOpenTK(System.Numerics.Vector3 v)
        {
            return new Vector3(v.X, v.Y, v.Z);
        }

        private static System.Numerics.Quaternion ToNumerics(Quaternion q)
        {
            return new System.Numerics.Quaternion(q.X, q.Y, q.Z, q.W);
        }

        private static Quaternion ToOpenTK(System.Numerics.Quaternion q)
        {
            return new Quaternion(q.X, q.Y, q.Z, q.W);
        }
    }

    /// <summary>
    /// Tipos de formas disponibles para colisiones
    /// </summary>
    public enum ShapeType
    {
        Box,
        Sphere,
        Capsule
    }
}