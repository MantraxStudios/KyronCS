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

        // ── Motion Type ──

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

        // ── Shape ──

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

        // ── Physics properties ──

        [ToStorage] public float Mass { get; set; } = 1.0f;
        [ToStorage] public bool UseGravity { get; set; } = true;
        [ToStorage] public float SleepThreshold { get; set; } = 0.01f;

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

        /// <summary>
        /// The physics layer this body belongs to. Used for raycast filtering
        /// and can be used for collision filtering.
        /// </summary>
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

        /// <summary>
        /// When true, this body acts as a sensor/trigger: it detects overlaps
        /// but does not generate a physics response (objects pass through it).
        /// Fires OnTriggerEnter/Stay/Exit instead of OnCollisionEnter/Stay/Exit.
        /// </summary>
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

        // ── Freeze axes ──

        [ToStorage] public bool FreezePositionX { get; set; } = false;
        [ToStorage] public bool FreezePositionY { get; set; } = false;
        [ToStorage] public bool FreezePositionZ { get; set; } = false;
        [ToStorage] public bool FreezeRotationX { get; set; } = false;
        [ToStorage] public bool FreezeRotationY { get; set; } = false;
        [ToStorage] public bool FreezeRotationZ { get; set; } = false;

        // ── Material ──

        [ToStorage] public float LinearDamping { get; set; } = 0.05f;
        [ToStorage] public float AngularDamping { get; set; } = 0.05f;
        [ToStorage] public float Friction { get; set; } = 0.5f;
        [ToStorage] public float Restitution { get; set; } = 0.0f;

        // ── Public accessors ──

        public BodyHandle? BodyHandle => _bodyHandle;
        public StaticHandle? StaticHandle => _staticHandle;
        public bool HasBody => _bodyHandle.HasValue || _staticHandle.HasValue;

        // ─────────────────────────────────────────────────────────
        //  Collision / Trigger Events  (C# events for user code)
        // ─────────────────────────────────────────────────────────

        public event Action<ContactInfo> CollisionEnter;
        public event Action<ContactInfo> CollisionStay;
        public event Action<ContactInfo> CollisionExit;
        public event Action<ContactInfo> TriggerEnter;
        public event Action<ContactInfo> TriggerStay;
        public event Action<ContactInfo> TriggerExit;

        // ICollisionEventHandler implementation (called by CollisionEventSystem)
        void ICollisionEventHandler.OnCollisionEnter(ContactInfo contact) => CollisionEnter?.Invoke(contact);
        void ICollisionEventHandler.OnCollisionStay(ContactInfo contact) => CollisionStay?.Invoke(contact);
        void ICollisionEventHandler.OnCollisionExit(ContactInfo contact) => CollisionExit?.Invoke(contact);
        void ICollisionEventHandler.OnTriggerEnter(ContactInfo contact) => TriggerEnter?.Invoke(contact);
        void ICollisionEventHandler.OnTriggerStay(ContactInfo contact) => TriggerStay?.Invoke(contact);
        void ICollisionEventHandler.OnTriggerExit(ContactInfo contact) => TriggerExit?.Invoke(contact);

        // ─────────────────────────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────────────────────────

        public override void Awake()
        {
            base.Awake();
            CleanupPhysics();
        }

        public override void Start()
        {
            base.Start();
            InitializePhysics();

            TriggerEnter += (contact) => {
                Console.WriteLine($"Algo entró al trigger");
            };

            CollisionEnter += (contact) => {
                Console.WriteLine($"Colisión! Normal: {contact.ContactNormal}");
            };

            CollisionExit += (contact) => {
                 Console.WriteLine("Se separaron");
            };
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
            {
                UnregisterFromEventSystem();
            }

            _bodyHandle = null;
            _staticHandle = null;
            _isInitialized = false;
        }

        // ─────────────────────────────────────────────────────────
        //  Body creation
        // ─────────────────────────────────────────────────────────

        private BodyMotionType GetFinalMotionType()
        {
            return _isKinematic ? BodyMotionType.Kinematic : _motionType;
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
                _shapeSize.Z * scale.Z);
        }

        private void CreatePhysicsBody()
        {
            if (GameObject?.Scene?.PhysicsWorld == null)
                return;

            var physicsWorld = GameObject.Scene.PhysicsWorld;
            var transform = GameObject.Transform;

            System.Numerics.Vector3 position = ToNumerics(transform.GetWorldPosition());
            System.Numerics.Quaternion rotation = ToNumerics(transform.GetWorldRotation());

            BodyMotionType finalMotionType = GetFinalMotionType();
            Vector3 finalSize = GetFinalShapeSize();

            if (finalMotionType == BodyMotionType.Static)
                CreateStaticBody(physicsWorld, position, rotation, finalSize);
            else
                CreateDynamicOrKinematicBody(physicsWorld, position, rotation, finalSize,
                    isDynamic: finalMotionType == BodyMotionType.Dynamic);

            // Register with the event system & trigger registry
            RegisterWithEventSystem();

            _previousMotionType = finalMotionType;
            _previousIsKinematic = _isKinematic;
            _previousShapeType = _shapeType;
            _previousShapeSize = _shapeSize;
            _previousScale = GetCurrentScale();
            _previousIsTrigger = _isTrigger;
        }

        private void CreateStaticBody(WorldPhysic physicsWorld, System.Numerics.Vector3 position,
            System.Numerics.Quaternion rotation, Vector3 finalSize)
        {
            var sim = physicsWorld.Simulation;

            switch (_shapeType)
            {
                case ShapeType.Box:
                    _staticHandle = physicsWorld.CreateStaticBox(ToNumerics(finalSize), position, rotation, _layer);
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

        private void CreateDynamicOrKinematicBody(WorldPhysic physicsWorld, System.Numerics.Vector3 position,
            System.Numerics.Quaternion rotation, Vector3 finalSize, bool isDynamic)
        {
            switch (_shapeType)
            {
                case ShapeType.Box:
                    _bodyHandle = physicsWorld.CreateBox(ToNumerics(finalSize), position, rotation,
                        isDynamic, Mass, SleepThreshold, _layer);
                    break;

                case ShapeType.Sphere:
                    _bodyHandle = physicsWorld.CreateSphere(finalSize.X, position, rotation,
                        isDynamic, Mass, SleepThreshold, _layer);
                    break;

                case ShapeType.Capsule:
                    _bodyHandle = physicsWorld.CreateCapsule(finalSize.Y, finalSize.X, position, rotation,
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

        // ─────────────────────────────────────────────────────────
        //  Event system registration
        // ─────────────────────────────────────────────────────────

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

            // Register event handler
            physicsWorld.EventSystem.RegisterHandler(collidableId.Value, this);

            // Register as trigger if needed
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

            if (_isTrigger)
                physicsWorld.TriggerRegistry.Register(collidableId.Value);
            else
                physicsWorld.TriggerRegistry.Unregister(collidableId.Value);
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

        // ─────────────────────────────────────────────────────────
        //  Recreate
        // ─────────────────────────────────────────────────────────

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
                if (finalMotionType == BodyMotionType.Dynamic && _previousMotionType == BodyMotionType.Dynamic)
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

        // ─────────────────────────────────────────────────────────
        //  Update / Sync
        // ─────────────────────────────────────────────────────────

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

            if (finalMotionType == BodyMotionType.Static || finalMotionType == BodyMotionType.Kinematic)
                SyncToPhysics();
        }

        internal void SyncFromPhysics()
        {
            if (!_bodyHandle.HasValue || GameObject?.Scene?.PhysicsWorld == null || !Enabled)
                return;

            BodyMotionType finalMotionType = GetFinalMotionType();
            if (finalMotionType == BodyMotionType.Kinematic || finalMotionType == BodyMotionType.Static)
                return;

            var bodyRef = GameObject.Scene.PhysicsWorld.Simulation.Bodies[_bodyHandle.Value];

            Vector3 position = ToOpenTK(bodyRef.Pose.Position);
            Quaternion rotation = ToOpenTK(bodyRef.Pose.Orientation);

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

            if (!FreezeRotationX || !FreezeRotationY || !FreezeRotationZ)
                GameObject.Transform.SetWorldRotation(rotation);
        }

        public void SyncToPhysics()
        {
            if (GameObject?.Scene?.PhysicsWorld == null)
                return;

            var sim = GameObject.Scene.PhysicsWorld.Simulation;
            BodyMotionType finalMotionType = GetFinalMotionType();

            if (finalMotionType == BodyMotionType.Kinematic && _bodyHandle.HasValue)
            {
                var bodyRef = sim.Bodies[_bodyHandle.Value];
                var targetPos = ToNumerics(GameObject.Transform.GetWorldPosition());
                var targetRot = ToNumerics(GameObject.Transform.GetWorldRotation());

                const float dt = 0.016f;
                var currentPos = bodyRef.Pose.Position;
                bodyRef.Velocity.Linear = (targetPos - currentPos) / dt;
                bodyRef.Pose.Position = targetPos;
                bodyRef.Pose.Orientation = targetRot;

                GameObject.Scene.PhysicsWorld.Awaken(_bodyHandle.Value);
            }
            else if (finalMotionType == BodyMotionType.Static && _staticHandle.HasValue)
            {
                var staticRef = sim.Statics[_staticHandle.Value];
                staticRef.Pose.Position = ToNumerics(GameObject.Transform.GetWorldPosition());
                staticRef.Pose.Orientation = ToNumerics(GameObject.Transform.GetWorldRotation());
            }
        }

        // ─────────────────────────────────────────────────────────
        //  Physics API
        // ─────────────────────────────────────────────────────────

        public void AddForce(Vector3 force)
        {
            if (_bodyHandle.HasValue && GameObject?.Scene?.PhysicsWorld != null)
            {
                var bodyRef = GameObject.Scene.PhysicsWorld.Simulation.Bodies[_bodyHandle.Value];
                bodyRef.ApplyLinearImpulse(ToNumerics(force));
                GameObject.Scene.PhysicsWorld.Awaken(_bodyHandle.Value);
            }
        }

        public void AddImpulse(Vector3 impulse)
        {
            if (_bodyHandle.HasValue && GameObject?.Scene?.PhysicsWorld != null)
            {
                var bodyRef = GameObject.Scene.PhysicsWorld.Simulation.Bodies[_bodyHandle.Value];
                bodyRef.ApplyLinearImpulse(ToNumerics(impulse));
                GameObject.Scene.PhysicsWorld.Awaken(_bodyHandle.Value);
            }
        }

        public void AddTorque(Vector3 torque)
        {
            if (_bodyHandle.HasValue && GameObject?.Scene?.PhysicsWorld != null)
            {
                var bodyRef = GameObject.Scene.PhysicsWorld.Simulation.Bodies[_bodyHandle.Value];
                bodyRef.ApplyAngularImpulse(ToNumerics(torque));
                GameObject.Scene.PhysicsWorld.Awaken(_bodyHandle.Value);
            }
        }

        public void SetVelocity(Vector3 velocity)
        {
            if (_bodyHandle.HasValue && GameObject?.Scene?.PhysicsWorld != null)
            {
                var bodyRef = GameObject.Scene.PhysicsWorld.Simulation.Bodies[_bodyHandle.Value];
                bodyRef.Velocity.Linear = ToNumerics(velocity);
                GameObject.Scene.PhysicsWorld.Awaken(_bodyHandle.Value);
            }
        }

        public Vector3 GetVelocity()
        {
            if (_bodyHandle.HasValue && GameObject?.Scene?.PhysicsWorld != null)
                return ToOpenTK(GameObject.Scene.PhysicsWorld.Simulation.Bodies[_bodyHandle.Value].Velocity.Linear);
            return Vector3.Zero;
        }

        public void SetAngularVelocity(Vector3 angularVelocity)
        {
            if (_bodyHandle.HasValue && GameObject?.Scene?.PhysicsWorld != null)
            {
                var bodyRef = GameObject.Scene.PhysicsWorld.Simulation.Bodies[_bodyHandle.Value];
                bodyRef.Velocity.Angular = ToNumerics(angularVelocity);
                GameObject.Scene.PhysicsWorld.Awaken(_bodyHandle.Value);
            }
        }

        public Vector3 GetAngularVelocity()
        {
            if (_bodyHandle.HasValue && GameObject?.Scene?.PhysicsWorld != null)
                return ToOpenTK(GameObject.Scene.PhysicsWorld.Simulation.Bodies[_bodyHandle.Value].Velocity.Angular);
            return Vector3.Zero;
        }

        public void MovePosition(Vector3 position)
        {
            if (_bodyHandle.HasValue && GameObject?.Scene?.PhysicsWorld != null)
            {
                var bodyRef = GameObject.Scene.PhysicsWorld.Simulation.Bodies[_bodyHandle.Value];
                bodyRef.Pose.Position = ToNumerics(position);
                GameObject.Scene.PhysicsWorld.Awaken(_bodyHandle.Value);
            }
        }

        public void MoveRotation(Quaternion rotation)
        {
            if (_bodyHandle.HasValue && GameObject?.Scene?.PhysicsWorld != null)
            {
                var bodyRef = GameObject.Scene.PhysicsWorld.Simulation.Bodies[_bodyHandle.Value];
                bodyRef.Pose.Orientation = ToNumerics(rotation);
                GameObject.Scene.PhysicsWorld.Awaken(_bodyHandle.Value);
            }
        }

        // ─────────────────────────────────────────────────────────
        //  Destroy
        // ─────────────────────────────────────────────────────────

        public override void OnDestroy()
        {
            base.OnDestroy();

            if (GameObject?.Scene?.PhysicsWorld != null)
                RemoveCurrentBody(GameObject.Scene.PhysicsWorld);

            _isInitialized = false;
        }

        // ─────────────────────────────────────────────────────────
        //  Conversion helpers
        // ─────────────────────────────────────────────────────────

        private static System.Numerics.Vector3 ToNumerics(Vector3 v)
            => new(v.X, v.Y, v.Z);

        private static Vector3 ToOpenTK(System.Numerics.Vector3 v)
            => new(v.X, v.Y, v.Z);

        private static System.Numerics.Quaternion ToNumerics(Quaternion q)
            => new(q.X, q.Y, q.Z, q.W);

        private static Quaternion ToOpenTK(System.Numerics.Quaternion q)
            => new(q.X, q.Y, q.Z, q.W);
    }

    public enum BodyMotionType
    {
        Static,
        Kinematic,
        Dynamic
    }

    public enum ShapeType
    {
        Box,
        Sphere,
        Capsule
    }
}