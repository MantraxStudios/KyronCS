using System;
using System.Collections.Generic;
using OpenTK.Mathematics;
using BepuPhysics;
using BepuPhysics.Collidables;
using KrayonCore.Physics;

namespace KrayonCore
{
    public class Rigidbody : Component, ICollisionEventHandler
    {
        private BodyHandle? _bodyHandle;
        private StaticHandle? _staticHandle;
        private bool _isInitialized = false;

        private BodyMotionType _previousMotionType;
        private bool _previousIsKinematic;
        private ShapeType _previousShapeType;
        private Vector3 _previousShapeSize;
        private Vector3 _previousScale;
        private bool _previousIsTrigger;
        private Vector3 _previousColliderOffset;

        private BodyMotionType _motionType = BodyMotionType.Dynamic;
        [ToStorage]
        public BodyMotionType MotionType
        {
            get => _motionType;
            set
            {
                if (_motionType != value)
                {
                    _motionType = value;
                    if (_isInitialized && !_isKinematic)
                        RecreatePhysicsBody();
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
                    if (_isInitialized) RecreatePhysicsBody();
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
                    if (_isInitialized) RecreatePhysicsBody();
                }
            }
        }

        /// <summary>
        /// Offset del colisionador en espacio local del GameObject (igual que Unity).
        /// Se rota junto con el objeto antes de aplicarse en mundo.
        /// </summary>
        private Vector3 _colliderOffset = Vector3.Zero;
        [ToStorage]
        public Vector3 ColliderOffset
        {
            get => _colliderOffset;
            set
            {
                if (_colliderOffset != value)
                {
                    _colliderOffset = value;
                    if (_isInitialized) RecreatePhysicsBody();
                }
            }
        }

        [ToStorage] public float Mass { get; set; } = 1.0f;

        private bool _useGravity = true;
        [ToStorage]
        public bool UseGravity
        {
            get => _useGravity;
            set => _useGravity = value;
        }

        [ToStorage] public float SleepThreshold { get; set; } = -1f;

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
                    if (_isInitialized) RecreatePhysicsBody();
                }
            }
        }

        private PhysicsLayer _layer = PhysicsLayer.Default;
        [ToStorage]
        public PhysicsLayer Layer
        {
            get => _layer;
            set
            {
                if (_layer != value)
                {
                    _layer = value;
                    if (_isInitialized) UpdateLayerRegistration();
                }
            }
        }

        private bool _isTrigger = false;
        [ToStorage]
        public bool IsTrigger
        {
            get => _isTrigger;
            set
            {
                if (_isTrigger != value)
                {
                    _isTrigger = value;
                    if (_isInitialized) UpdateTriggerRegistration();
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

        public BodyHandle? BodyHandle => _bodyHandle;
        public StaticHandle? StaticHandle => _staticHandle;
        public bool HasBody => _bodyHandle.HasValue || _staticHandle.HasValue;

        public event Action<ContactInfo> CollisionEnter;
        public event Action<ContactInfo> CollisionStay;
        public event Action<ContactInfo> CollisionExit;
        public event Action<ContactInfo> TriggerEnter;
        public event Action<ContactInfo> TriggerStay;
        public event Action<ContactInfo> TriggerExit;

        void ICollisionEventHandler.OnCollisionEnter(ContactInfo contact) => CollisionEnter?.Invoke(contact);
        void ICollisionEventHandler.OnCollisionStay(ContactInfo contact) => CollisionStay?.Invoke(contact);
        void ICollisionEventHandler.OnCollisionExit(ContactInfo contact) => CollisionExit?.Invoke(contact);
        void ICollisionEventHandler.OnTriggerEnter(ContactInfo contact) => TriggerEnter?.Invoke(contact);
        void ICollisionEventHandler.OnTriggerStay(ContactInfo contact) => TriggerStay?.Invoke(contact);
        void ICollisionEventHandler.OnTriggerExit(ContactInfo contact) => TriggerExit?.Invoke(contact);

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
            if (_isInitialized && GameObject?.Scene?.PhysicsWorld != null)
                UnregisterFromEventSystem();

            _bodyHandle = null;
            _staticHandle = null;
            _isInitialized = false;
        }

        private BodyMotionType GetFinalMotionType()
            => _isKinematic ? BodyMotionType.Kinematic : _motionType;

        private Vector3 GetCurrentScale()
        {
            var t = GameObject.Transform;
            return new Vector3(t.ScaleX, t.ScaleY, t.ScaleZ);
        }

        private Vector3 GetFinalShapeSize()
        {
            Vector3 scale = GetCurrentScale();
            return new Vector3(
                _shapeSize.X * scale.X,
                _shapeSize.Y * scale.Y,
                _shapeSize.Z * scale.Z);
        }

        /// <summary>
        /// Convierte el offset local en offset de mundo rotando por la
        /// rotación mundial del objeto, igual que Unity hace internamente.
        /// </summary>
        private Vector3 GetWorldOffset()
        {
            if (_colliderOffset == Vector3.Zero)
                return Vector3.Zero;

            Quaternion worldRot = GameObject.Transform.GetWorldRotation();
            return Vector3.Transform(_colliderOffset, worldRot);
        }

        private void CreatePhysicsBody()
        {
            if (GameObject?.Scene?.PhysicsWorld == null)
                return;

            var physicsWorld = GameObject.Scene.PhysicsWorld;
            var transform = GameObject.Transform;

            // Posición = posición mundial + offset rotado
            Vector3 worldPos = transform.GetWorldPosition();
            Vector3 worldOffset = GetWorldOffset();
            System.Numerics.Vector3 position =
                ToNumerics(worldPos + worldOffset);

            System.Numerics.Quaternion rotation = ToNumerics(transform.GetWorldRotation());

            BodyMotionType finalMotionType = GetFinalMotionType();
            Vector3 finalSize = GetFinalShapeSize();

            if (finalMotionType == BodyMotionType.Static)
                CreateStaticBody(physicsWorld, position, rotation, finalSize);
            else
                CreateDynamicOrKinematicBody(physicsWorld, position, rotation, finalSize,
                    isDynamic: finalMotionType == BodyMotionType.Dynamic);

            RegisterWithEventSystem();

            _previousMotionType = finalMotionType;
            _previousIsKinematic = _isKinematic;
            _previousShapeType = _shapeType;
            _previousShapeSize = _shapeSize;
            _previousScale = GetCurrentScale();
            _previousIsTrigger = _isTrigger;
            _previousColliderOffset = _colliderOffset;
        }

        private void CreateStaticBody(WorldPhysic physicsWorld, System.Numerics.Vector3 position,
            System.Numerics.Quaternion rotation, Vector3 finalSize)
        {
            var sim = physicsWorld.Simulation;

            switch (_shapeType)
            {
                case ShapeType.Box:
                    _staticHandle = physicsWorld.CreateStaticBox(
                        ToNumerics(finalSize), position, rotation, _layer);
                    break;

                case ShapeType.Sphere:
                    {
                        var shape = new Sphere(finalSize.X);
                        var shapeIndex = sim.Shapes.Add(shape);
                        _staticHandle = sim.Statics.Add(new StaticDescription(
                            new RigidPose(position, rotation), shapeIndex));
                        physicsWorld.LayerRegistry.SetLayer(_staticHandle.Value, _layer);
                        break;
                    }

                case ShapeType.Capsule:
                    {
                        var shape = new Capsule(finalSize.X, finalSize.Y * 2f);
                        var shapeIndex = sim.Shapes.Add(shape);
                        _staticHandle = sim.Statics.Add(new StaticDescription(
                            new RigidPose(position, rotation), shapeIndex));
                        physicsWorld.LayerRegistry.SetLayer(_staticHandle.Value, _layer);
                        break;
                    }
            }
        }

        private void CreateDynamicOrKinematicBody(WorldPhysic physicsWorld,
            System.Numerics.Vector3 position, System.Numerics.Quaternion rotation,
            Vector3 finalSize, bool isDynamic)
        {
            switch (_shapeType)
            {
                case ShapeType.Box:
                    _bodyHandle = physicsWorld.CreateBox(
                        ToNumerics(finalSize), position, rotation,
                        isDynamic, Mass, SleepThreshold, _layer);
                    break;

                case ShapeType.Sphere:
                    _bodyHandle = physicsWorld.CreateSphere(
                        finalSize.X, position, rotation,
                        isDynamic, Mass, SleepThreshold, _layer);
                    break;

                case ShapeType.Capsule:
                    _bodyHandle = physicsWorld.CreateCapsule(
                        finalSize.Y, finalSize.X, position, rotation,
                        isDynamic, Mass, SleepThreshold, _layer);
                    break;
            }

            if (_bodyHandle.HasValue)
            {
                var bodyRef = physicsWorld.Simulation.Bodies[_bodyHandle.Value];

                if (isDynamic)
                    physicsWorld.Awaken(_bodyHandle.Value);
                else
                {
                    bodyRef.Velocity.Linear = System.Numerics.Vector3.Zero;
                    bodyRef.Velocity.Angular = System.Numerics.Vector3.Zero;
                }
            }
        }

        private CollidableId? GetCollidableId()
        {
            if (GameObject?.Scene?.PhysicsWorld == null) return null;
            var sim = GameObject.Scene.PhysicsWorld.Simulation;

            if (_bodyHandle.HasValue && sim.Bodies.BodyExists(_bodyHandle.Value))
                return new CollidableId(sim.Bodies[_bodyHandle.Value].CollidableReference);

            if (_staticHandle.HasValue && sim.Statics.StaticExists(_staticHandle.Value))
                return new CollidableId(new CollidableReference(_staticHandle.Value));

            return null;
        }

        private void RegisterWithEventSystem()
        {
            var physicsWorld = GameObject?.Scene?.PhysicsWorld;
            if (physicsWorld == null) return;

            var collidableId = GetCollidableId();
            if (!collidableId.HasValue) return;

            physicsWorld.EventSystem.RegisterHandler(collidableId.Value, this);

            if (_isTrigger)
                physicsWorld.TriggerRegistry.Register(collidableId.Value);
        }

        private void UnregisterFromEventSystem()
        {
            var physicsWorld = GameObject?.Scene?.PhysicsWorld;
            if (physicsWorld == null) return;

            var collidableId = GetCollidableId();
            if (!collidableId.HasValue) return;

            physicsWorld.EventSystem.UnregisterHandler(collidableId.Value);
            physicsWorld.TriggerRegistry.Unregister(collidableId.Value);
        }

        private void UpdateTriggerRegistration()
        {
            var physicsWorld = GameObject?.Scene?.PhysicsWorld;
            if (physicsWorld == null) return;

            var collidableId = GetCollidableId();
            if (!collidableId.HasValue) return;

            if (_isTrigger) physicsWorld.TriggerRegistry.Register(collidableId.Value);
            else physicsWorld.TriggerRegistry.Unregister(collidableId.Value);
        }

        private void UpdateLayerRegistration()
        {
            var physicsWorld = GameObject?.Scene?.PhysicsWorld;
            if (physicsWorld == null) return;

            if (_bodyHandle.HasValue)
                physicsWorld.LayerRegistry.SetLayer(_bodyHandle.Value, _layer);
            else if (_staticHandle.HasValue)
                physicsWorld.LayerRegistry.SetLayer(_staticHandle.Value, _layer);
        }

        private void RecreatePhysicsBody()
        {
            if (!_isInitialized || GameObject?.Scene?.PhysicsWorld == null)
                return;

            var physicsWorld = GameObject.Scene.PhysicsWorld;

            System.Numerics.Vector3 linearVelocity = System.Numerics.Vector3.Zero;
            System.Numerics.Vector3 angularVelocity = System.Numerics.Vector3.Zero;

            if (_bodyHandle.HasValue && _previousMotionType == BodyMotionType.Dynamic)
            {
                var bodyRef = physicsWorld.Simulation.Bodies[_bodyHandle.Value];
                linearVelocity = bodyRef.Velocity.Linear;
                angularVelocity = bodyRef.Velocity.Angular;
            }

            RemoveCurrentBody(physicsWorld);
            CreatePhysicsBody();

            if (_bodyHandle.HasValue)
            {
                BodyMotionType finalMotionType = GetFinalMotionType();
                if (finalMotionType == BodyMotionType.Dynamic &&
                    _previousMotionType == BodyMotionType.Dynamic)
                {
                    var bodyRef = physicsWorld.Simulation.Bodies[_bodyHandle.Value];
                    bodyRef.Velocity.Linear = linearVelocity;
                    bodyRef.Velocity.Angular = angularVelocity;
                }
            }
        }

        private void RemoveCurrentBody(WorldPhysic physicsWorld)
        {
            UnregisterFromEventSystem();

            if (_bodyHandle.HasValue)
            {
                physicsWorld.RemoveBody(_bodyHandle.Value);
                _bodyHandle = null;
            }

            if (_staticHandle.HasValue)
            {
                physicsWorld.RemoveStatic(_staticHandle.Value);
                _staticHandle = null;
            }
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);

            if (!_isInitialized && GameObject?.Scene?.PhysicsWorld != null)
                InitializePhysics();

            if (!HasBody || GameObject?.Scene?.PhysicsWorld == null || !Enabled)
                return;

            Vector3 currentScale = GetCurrentScale();
            if (currentScale != _previousScale)
                RecreatePhysicsBody();

            BodyMotionType finalMotionType = GetFinalMotionType();

            if (finalMotionType == BodyMotionType.Static ||
                finalMotionType == BodyMotionType.Kinematic)
                SyncToPhysics();

            if (!_useGravity && _bodyHandle.HasValue &&
                finalMotionType == BodyMotionType.Dynamic)
            {
                var physicsWorld = GameObject.Scene.PhysicsWorld;
                var bodyRef = physicsWorld.Simulation.Bodies[_bodyHandle.Value];
                var gravity = physicsWorld.Gravity;
                bodyRef.ApplyLinearImpulse(new System.Numerics.Vector3(
                    -gravity.X * Mass * deltaTime,
                    -gravity.Y * Mass * deltaTime,
                    -gravity.Z * Mass * deltaTime));
            }
        }

        internal void SyncFromPhysics()
        {
            if (!_bodyHandle.HasValue || GameObject?.Scene?.PhysicsWorld == null || !Enabled)
                return;

            BodyMotionType finalMotionType = GetFinalMotionType();
            if (finalMotionType == BodyMotionType.Kinematic ||
                finalMotionType == BodyMotionType.Static) return;

            var physicsWorld = GameObject.Scene.PhysicsWorld;
            var bodyRef = physicsWorld.Simulation.Bodies[_bodyHandle.Value];

            physicsWorld.Awaken(_bodyHandle.Value);

            // La física devuelve la posición del centro del colisionador.
            // Hay que restar el offset (ya rotado) para obtener la posición del GameObject.
            Vector3 physicsPos = ToOpenTK(bodyRef.Pose.Position);
            Quaternion physicsRotation = ToOpenTK(bodyRef.Pose.Orientation);
            Vector3 worldOffset = Vector3.Transform(_colliderOffset, physicsRotation);
            Vector3 position = physicsPos - worldOffset;

            if (FreezePositionX) position.X = GameObject.Transform.GetWorldPosition().X;
            if (FreezePositionY) position.Y = GameObject.Transform.GetWorldPosition().Y;
            if (FreezePositionZ) position.Z = GameObject.Transform.GetWorldPosition().Z;

            if (FreezePositionX || FreezePositionY || FreezePositionZ)
            {
                var vel = bodyRef.Velocity.Linear;
                if (FreezePositionX) vel.X = 0;
                if (FreezePositionY) vel.Y = 0;
                if (FreezePositionZ) vel.Z = 0;
                bodyRef.Velocity.Linear = vel;
            }

            if (FreezeRotationX || FreezeRotationY || FreezeRotationZ)
            {
                var angVel = bodyRef.Velocity.Angular;
                if (FreezeRotationX) angVel.X = 0;
                if (FreezeRotationY) angVel.Y = 0;
                if (FreezeRotationZ) angVel.Z = 0;
                bodyRef.Velocity.Angular = angVel;
            }

            GameObject.Transform.SetWorldPosition(position);

            if (!FreezeRotationX && !FreezeRotationY && !FreezeRotationZ)
            {
                GameObject.Transform.SetWorldRotation(physicsRotation);
            }
            else if (!(FreezeRotationX && FreezeRotationY && FreezeRotationZ))
            {
                Vector3 physicsEuler = physicsRotation.ToEulerAngles();
                Vector3 currentEuler = GameObject.Transform.GetWorldRotation().ToEulerAngles();

                GameObject.Transform.SetWorldRotation(Quaternion.FromEulerAngles(new Vector3(
                    FreezeRotationX ? currentEuler.X : physicsEuler.X,
                    FreezeRotationY ? currentEuler.Y : physicsEuler.Y,
                    FreezeRotationZ ? currentEuler.Z : physicsEuler.Z)));
            }
        }

        public void SyncToPhysics()
        {
            if (GameObject?.Scene?.PhysicsWorld == null) return;

            var sim = GameObject.Scene.PhysicsWorld.Simulation;
            BodyMotionType fmt = GetFinalMotionType();

            if (fmt == BodyMotionType.Kinematic && _bodyHandle.HasValue)
            {
                var bodyRef = sim.Bodies[_bodyHandle.Value];
                Vector3 goPos = GameObject.Transform.GetWorldPosition();
                Vector3 offset = GetWorldOffset();

                var targetPos = ToNumerics(goPos + offset);
                var targetRot = ToNumerics(GameObject.Transform.GetWorldRotation());

                const float dt = 0.016f;
                bodyRef.Velocity.Linear = (targetPos - bodyRef.Pose.Position) / dt;
                bodyRef.Pose.Position = targetPos;
                bodyRef.Pose.Orientation = targetRot;

                GameObject.Scene.PhysicsWorld.Awaken(_bodyHandle.Value);
            }
            else if (fmt == BodyMotionType.Static && _staticHandle.HasValue)
            {
                var staticRef = sim.Statics[_staticHandle.Value];
                staticRef.Pose.Position = ToNumerics(
                    GameObject.Transform.GetWorldPosition() + GetWorldOffset());
                staticRef.Pose.Orientation = ToNumerics(
                    GameObject.Transform.GetWorldRotation());
            }
        }

        // ── API pública ───────────────────────────────────────────────────────

        public void AddForce(Vector3 force)
        {
            if (!_bodyHandle.HasValue || GameObject?.Scene?.PhysicsWorld == null) return;
            var bodyRef = GameObject.Scene.PhysicsWorld.Simulation.Bodies[_bodyHandle.Value];
            bodyRef.ApplyLinearImpulse(ToNumerics(force));
            GameObject.Scene.PhysicsWorld.Awaken(_bodyHandle.Value);
        }

        public void AddImpulse(Vector3 impulse)
        {
            if (!_bodyHandle.HasValue || GameObject?.Scene?.PhysicsWorld == null) return;
            var bodyRef = GameObject.Scene.PhysicsWorld.Simulation.Bodies[_bodyHandle.Value];
            bodyRef.ApplyLinearImpulse(ToNumerics(impulse));
            GameObject.Scene.PhysicsWorld.Awaken(_bodyHandle.Value);
        }

        public void AddTorque(Vector3 torque)
        {
            if (!_bodyHandle.HasValue || GameObject?.Scene?.PhysicsWorld == null) return;
            var bodyRef = GameObject.Scene.PhysicsWorld.Simulation.Bodies[_bodyHandle.Value];
            bodyRef.ApplyAngularImpulse(ToNumerics(torque));
            GameObject.Scene.PhysicsWorld.Awaken(_bodyHandle.Value);
        }

        public void SetVelocity(Vector3 velocity)
        {
            if (!_bodyHandle.HasValue || GameObject?.Scene?.PhysicsWorld == null) return;
            var bodyRef = GameObject.Scene.PhysicsWorld.Simulation.Bodies[_bodyHandle.Value];
            bodyRef.Velocity.Linear = ToNumerics(velocity);
            GameObject.Scene.PhysicsWorld.Awaken(_bodyHandle.Value);
        }

        public Vector3 GetVelocity()
        {
            if (_bodyHandle.HasValue && GameObject?.Scene?.PhysicsWorld != null)
                return ToOpenTK(
                    GameObject.Scene.PhysicsWorld.Simulation.Bodies[_bodyHandle.Value].Velocity.Linear);
            return Vector3.Zero;
        }

        public void SetAngularVelocity(Vector3 angularVelocity)
        {
            if (!_bodyHandle.HasValue || GameObject?.Scene?.PhysicsWorld == null) return;
            var bodyRef = GameObject.Scene.PhysicsWorld.Simulation.Bodies[_bodyHandle.Value];
            bodyRef.Velocity.Angular = ToNumerics(angularVelocity);
            GameObject.Scene.PhysicsWorld.Awaken(_bodyHandle.Value);
        }

        public Vector3 GetAngularVelocity()
        {
            if (_bodyHandle.HasValue && GameObject?.Scene?.PhysicsWorld != null)
                return ToOpenTK(
                    GameObject.Scene.PhysicsWorld.Simulation.Bodies[_bodyHandle.Value].Velocity.Angular);
            return Vector3.Zero;
        }

        public void MovePosition(Vector3 position)
        {
            if (!_bodyHandle.HasValue || GameObject?.Scene?.PhysicsWorld == null) return;
            var bodyRef = GameObject.Scene.PhysicsWorld.Simulation.Bodies[_bodyHandle.Value];
            bodyRef.Pose.Position = ToNumerics(position + GetWorldOffset());
            GameObject.Scene.PhysicsWorld.Awaken(_bodyHandle.Value);
        }

        public void MoveRotation(Quaternion rotation)
        {
            if (!_bodyHandle.HasValue || GameObject?.Scene?.PhysicsWorld == null) return;
            var bodyRef = GameObject.Scene.PhysicsWorld.Simulation.Bodies[_bodyHandle.Value];
            bodyRef.Pose.Orientation = ToNumerics(rotation);
            GameObject.Scene.PhysicsWorld.Awaken(_bodyHandle.Value);
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            if (GameObject?.Scene?.PhysicsWorld != null)
                RemoveCurrentBody(GameObject.Scene.PhysicsWorld);
            _isInitialized = false;
        }

        // ── Conversiones ──────────────────────────────────────────────────────
        private static System.Numerics.Vector3 ToNumerics(Vector3 v) => new(v.X, v.Y, v.Z);
        private static Vector3 ToOpenTK(System.Numerics.Vector3 v) => new(v.X, v.Y, v.Z);
        private static System.Numerics.Quaternion ToNumerics(Quaternion q) => new(q.X, q.Y, q.Z, q.W);
        private static Quaternion ToOpenTK(System.Numerics.Quaternion q) => new(q.X, q.Y, q.Z, q.W);
    }

    public enum BodyMotionType { Static, Kinematic, Dynamic }
    public enum ShapeType { Box, Sphere, Capsule }
}