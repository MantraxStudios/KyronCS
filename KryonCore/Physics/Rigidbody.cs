using System;
using OpenTK.Mathematics;
using JoltPhysicsSharp;
using KrayonCore.Physics;

namespace KrayonCore
{
    public class Rigidbody : Component
    {
        private Body _body;
        private bool _isInitialized = false;

        private MotionType _previousMotionType;
        private bool _previousIsKinematic;
        private ShapeType _previousShapeType;
        private Vector3 _previousShapeSize;
        private ObjectLayer _previousLayer;
        private Vector3 _previousScale;

        private MotionType _motionType = MotionType.Dynamic;
        [ToStorage]
        public MotionType MotionType
        {
            get => _motionType;
            set
            {
                if (_motionType != value)
                {
                    _motionType = value;
                    if (_isInitialized && !_isKinematic)
                    {
                        RecreatePhysicsBody();
                    }
                }
            }
        }

        private ObjectLayer _layer = WorldPhysic.Layers.Moving;
        public ObjectLayer Layer
        {
            get => _layer;
            set
            {
                if (_layer != value)
                {
                    _layer = value;
                    if (_isInitialized)
                    {
                        RecreatePhysicsBody();
                    }
                }
            }
        }

        private ShapeType _shapeType = ShapeType.Box;
        [ToStorage]
        public ShapeType ShapeType
        {
            get => _shapeType;
            set
            {
                if (_shapeType != value)
                {
                    _shapeType = value;
                    if (_isInitialized)
                    {
                        RecreatePhysicsBody();
                    }
                }
            }
        }

        private Vector3 _shapeSize = Vector3.One;
        [ToStorage]
        public Vector3 ShapeSize
        {
            get => _shapeSize;
            set
            {
                if (_shapeSize != value)
                {
                    _shapeSize = value;
                    if (_isInitialized)
                    {
                        RecreatePhysicsBody();
                    }
                }
            }
        }

        [ToStorage] public float Mass { get; set; } = 1.0f;
        [ToStorage] public bool UseGravity { get; set; } = true;

        private bool _isKinematic = false;
        [ToStorage]
        public bool IsKinematic
        {
            get => _isKinematic;
            set
            {
                if (_isKinematic != value)
                {
                    _isKinematic = value;
                    if (_isInitialized)
                    {
                        RecreatePhysicsBody();
                    }
                }
            }
        }

        [ToStorage] public bool FreezePositionX { get; set; } = false;
        [ToStorage] public bool FreezePositionY { get; set; } = false;
        [ToStorage] public bool FreezePositionZ { get; set; } = false;
        [ToStorage] public bool FreezeRotationX { get; set; } = false;
        [ToStorage] public bool FreezeRotationY { get; set; } = false;
        [ToStorage] public bool FreezeRotationZ { get; set; } = false;

        [ToStorage] public float LinearDamping { get; set; } = 0.05f;
        [ToStorage] public float AngularDamping { get; set; } = 0.05f;
        [ToStorage] public float Friction { get; set; } = 0.5f;
        [ToStorage] public float Restitution { get; set; } = 0.0f;

        public Body Body => _body;

        public override void Awake()
        {
            base.Awake();
            CleanupPhysics();
        }

        public override void Start()
        {
            base.Start();
            InitializePhysics();
        }

        private void InitializePhysics()
        {
            if (_isInitialized || GameObject?.Scene?.PhysicsWorld == null)
                return;

            CreatePhysicsBody();
            _isInitialized = true;
        }

        public void ForceReinitialize()
        {
            CleanupPhysics();
            InitializePhysics();
        }

        public void CleanupPhysics()
        {
            _body = null;
            _isInitialized = false;
        }

        private Vector3 GetCurrentScale()
        {
            var transform = GameObject.Transform;
            return new Vector3(transform.ScaleX, transform.ScaleY, transform.ScaleZ);
        }

        private Vector3 GetFinalShapeSize()
        {
            Vector3 scale = GetCurrentScale();
            return new Vector3(
                _shapeSize.X * scale.X,
                _shapeSize.Y * scale.Y,
                _shapeSize.Z * scale.Z
            );
        }

        private void CreatePhysicsBody()
        {
            if (GameObject?.Scene?.PhysicsWorld == null)
                return;

            var physicsWorld = GameObject.Scene.PhysicsWorld;
            var transform = GameObject.Transform;

            System.Numerics.Vector3 position = ToNumerics(transform.GetWorldPosition());
            System.Numerics.Quaternion rotation = ToNumerics(transform.GetWorldRotation());

            MotionType finalMotionType = _isKinematic ? MotionType.Kinematic : _motionType;

            Vector3 finalSize = GetFinalShapeSize();

            switch (_shapeType)
            {
                case ShapeType.Box:
                    System.Numerics.Vector3 halfExtent = ToNumerics(finalSize);
                    _body = physicsWorld.CreateBox(
                        halfExtent,
                        position,
                        rotation,
                        finalMotionType,
                        _layer
                    );
                    break;

                case ShapeType.Sphere:
                    _body = physicsWorld.CreateSphere(
                        finalSize.X,
                        position,
                        rotation,
                        finalMotionType,
                        _layer
                    );
                    break;

                case ShapeType.Capsule:
                    _body = physicsWorld.CreateCapsule(
                        finalSize.Y,
                        finalSize.X,
                        position,
                        rotation,
                        finalMotionType,
                        _layer
                    );
                    break;
            }

            if (_body != null)
            {
                var bodyInterface = physicsWorld.BodyInterface;

                bodyInterface.SetFriction(_body.ID, Friction);
                bodyInterface.SetRestitution(_body.ID, Restitution);

                if (finalMotionType == MotionType.Dynamic || finalMotionType == MotionType.Kinematic)
                {
                    var motionProps = _body.MotionProperties;
                    if (motionProps != null)
                    {
                        motionProps.LinearDamping = LinearDamping;
                        motionProps.AngularDamping = AngularDamping;

                        if (!UseGravity && finalMotionType == MotionType.Dynamic)
                        {
                            bodyInterface.SetGravityFactor(_body.ID, 0.0f);
                        }
                    }
                }

                if (finalMotionType == MotionType.Kinematic)
                {
                    bodyInterface.SetLinearVelocity(_body.ID, System.Numerics.Vector3.Zero);
                    bodyInterface.SetAngularVelocity(_body.ID, System.Numerics.Vector3.Zero);
                }

                if (finalMotionType == MotionType.Dynamic)
                {
                    bodyInterface.ActivateBody(_body.ID);
                }
            }

            _previousMotionType = finalMotionType;
            _previousIsKinematic = _isKinematic;
            _previousShapeType = _shapeType;
            _previousShapeSize = _shapeSize;
            _previousLayer = _layer;
            _previousScale = GetCurrentScale();
        }

        private void RecreatePhysicsBody()
        {
            if (!_isInitialized || GameObject?.Scene?.PhysicsWorld == null)
                return;

            System.Numerics.Vector3 linearVelocity = System.Numerics.Vector3.Zero;
            System.Numerics.Vector3 angularVelocity = System.Numerics.Vector3.Zero;

            if (_body != null && _previousMotionType == MotionType.Dynamic)
            {
                var bodyInterface = GameObject.Scene.PhysicsWorld.BodyInterface;
                linearVelocity = bodyInterface.GetLinearVelocity(_body.ID);
                angularVelocity = bodyInterface.GetAngularVelocity(_body.ID);
            }

            if (_body != null)
            {
                GameObject.Scene.PhysicsWorld.RemoveBody(_body);
                _body = null;
            }

            CreatePhysicsBody();

            if (_body != null)
            {
                MotionType finalMotionType = _isKinematic ? MotionType.Kinematic : _motionType;

                if (finalMotionType == MotionType.Dynamic && _previousMotionType == MotionType.Dynamic)
                {
                    var bodyInterface = GameObject.Scene.PhysicsWorld.BodyInterface;
                    bodyInterface.SetLinearVelocity(_body.ID, linearVelocity);
                    bodyInterface.SetAngularVelocity(_body.ID, angularVelocity);
                }
            }
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);

            if (!_isInitialized && GameObject?.Scene?.PhysicsWorld != null)
            {
                InitializePhysics();
            }

            if (_body == null || GameObject?.Scene?.PhysicsWorld == null || !Enabled)
                return;

            Vector3 currentScale = GetCurrentScale();
            if (currentScale != _previousScale)
            {
                RecreatePhysicsBody();
            }

            MotionType finalMotionType = _isKinematic ? MotionType.Kinematic : _motionType;

            if (finalMotionType == MotionType.Static || finalMotionType == MotionType.Kinematic)
            {
                SyncToPhysics();
            }
        }

        internal void SyncFromPhysics()
        {
            if (_body == null || GameObject?.Scene?.PhysicsWorld == null || !Enabled)
                return;

            MotionType finalMotionType = _isKinematic ? MotionType.Kinematic : _motionType;

            if (finalMotionType == MotionType.Kinematic || finalMotionType == MotionType.Static)
                return;

            var bodyInterface = GameObject.Scene.PhysicsWorld.BodyInterface;

            System.Numerics.Vector3 physicsPosition = bodyInterface.GetPosition(_body.ID);
            System.Numerics.Quaternion physicsRotation = bodyInterface.GetRotation(_body.ID);

            Vector3 position = ToOpenTK(physicsPosition);
            Quaternion rotation = ToOpenTK(physicsRotation);

            if (FreezePositionX) position.X = GameObject.Transform.GetWorldPosition().X;
            if (FreezePositionY) position.Y = GameObject.Transform.GetWorldPosition().Y;
            if (FreezePositionZ) position.Z = GameObject.Transform.GetWorldPosition().Z;

            GameObject.Transform.SetWorldPosition(position);

            if (!FreezeRotationX || !FreezeRotationY || !FreezeRotationZ)
            {
                GameObject.Transform.SetWorldRotation(rotation);
            }
        }

        public void SyncToPhysics()
        {
            if (_body == null || GameObject?.Scene?.PhysicsWorld == null)
                return;

            var bodyInterface = GameObject.Scene.PhysicsWorld.BodyInterface;

            MotionType finalMotionType = _isKinematic ? MotionType.Kinematic : _motionType;

            if (finalMotionType == MotionType.Kinematic)
            {
                bodyInterface.MoveKinematic(
                    _body.ID,
                    ToNumerics(GameObject.Transform.GetWorldPosition()),
                    ToNumerics(GameObject.Transform.GetWorldRotation()),
                    0.016f
                );
            }
            else if (finalMotionType == MotionType.Static)
            {
                bodyInterface.SetPosition(
                    _body.ID,
                    ToNumerics(GameObject.Transform.GetWorldPosition()),
                    Activation.DontActivate
                );

                bodyInterface.SetRotation(
                    _body.ID,
                    ToNumerics(GameObject.Transform.GetWorldRotation()),
                    Activation.DontActivate
                );
            }
        }

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

        public void MovePosition(Vector3 position)
        {
            if (_body != null && GameObject?.Scene?.PhysicsWorld != null)
            {
                GameObject.Scene.PhysicsWorld.BodyInterface.MoveKinematic(
                    _body.ID,
                    ToNumerics(position),
                    ToNumerics(GameObject.Transform.GetWorldRotation()),
                    0.016f
                );
            }
        }

        public void MoveRotation(Quaternion rotation)
        {
            if (_body != null && GameObject?.Scene?.PhysicsWorld != null)
            {
                GameObject.Scene.PhysicsWorld.BodyInterface.MoveKinematic(
                    _body.ID,
                    ToNumerics(GameObject.Transform.GetWorldPosition()),
                    ToNumerics(rotation),
                    0.016f
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

    public enum ShapeType
    {
        Box,
        Sphere,
        Capsule
    }
}