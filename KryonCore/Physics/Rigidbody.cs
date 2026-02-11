using System;
using OpenTK.Mathematics;
using BepuPhysics;
using BepuPhysics.Collidables;
using KrayonCore.Physics;

namespace KrayonCore
{
    public class Rigidbody : Component
    {
        private BodyHandle? _bodyHandle;
        private StaticHandle? _staticHandle;
        private bool _isInitialized = false;

        private BodyMotionType _previousMotionType;
        private bool _previousIsKinematic;
        private ShapeType _previousShapeType;
        private Vector3 _previousShapeSize;
        private Vector3 _previousScale;

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
        [ToStorage] public float SleepThreshold { get; set; } = 0.01f;

        /// <summary>
        /// Returns the BodyHandle if this is a dynamic/kinematic body. Null for statics.
        /// </summary>
        public BodyHandle? BodyHandle => _bodyHandle;

        /// <summary>
        /// Returns the StaticHandle if this is a static body. Null for dynamic/kinematic.
        /// </summary>
        public StaticHandle? StaticHandle => _staticHandle;

        /// <summary>
        /// Whether this rigidbody currently has an active physics representation.
        /// </summary>
        public bool HasBody => _bodyHandle.HasValue || _staticHandle.HasValue;

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
            _bodyHandle = null;
            _staticHandle = null;
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

        private BodyMotionType GetFinalMotionType()
        {
            return _isKinematic ? BodyMotionType.Kinematic : _motionType;
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
            {
                CreateStaticBody(physicsWorld, position, rotation, finalSize);
            }
            else
            {
                bool isDynamic = finalMotionType == BodyMotionType.Dynamic;
                CreateDynamicOrKinematicBody(physicsWorld, position, rotation, finalSize, isDynamic);
            }

            _previousMotionType = finalMotionType;
            _previousIsKinematic = _isKinematic;
            _previousShapeType = _shapeType;
            _previousShapeSize = _shapeSize;
            _previousScale = GetCurrentScale();
        }

        private void CreateStaticBody(WorldPhysic physicsWorld, System.Numerics.Vector3 position,
            System.Numerics.Quaternion rotation, Vector3 finalSize)
        {
            switch (_shapeType)
            {
                case ShapeType.Box:
                    _staticHandle = physicsWorld.CreateStaticBox(
                        ToNumerics(finalSize),
                        position,
                        rotation);
                    break;

                case ShapeType.Sphere:
                    {
                        // For static spheres, create via the simulation directly
                        var sim = physicsWorld.Simulation;
                        var shape = new Sphere(finalSize.X);
                        var shapeIndex = sim.Shapes.Add(shape);
                        _staticHandle = sim.Statics.Add(new StaticDescription(
                            new RigidPose(position, rotation),
                            shapeIndex));
                        break;
                    }

                case ShapeType.Capsule:
                    {
                        var sim = physicsWorld.Simulation;
                        var shape = new Capsule(finalSize.X, finalSize.Y * 2f);
                        var shapeIndex = sim.Shapes.Add(shape);
                        _staticHandle = sim.Statics.Add(new StaticDescription(
                            new RigidPose(position, rotation),
                            shapeIndex));
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
                    _bodyHandle = physicsWorld.CreateBox(
                        ToNumerics(finalSize),
                        position,
                        rotation,
                        isDynamic,
                        Mass,
                        SleepThreshold);
                    break;

                case ShapeType.Sphere:
                    _bodyHandle = physicsWorld.CreateSphere(
                        finalSize.X,
                        position,
                        rotation,
                        isDynamic,
                        Mass,
                        SleepThreshold);
                    break;

                case ShapeType.Capsule:
                    _bodyHandle = physicsWorld.CreateCapsule(
                        finalSize.Y,
                        finalSize.X,
                        position,
                        rotation,
                        isDynamic,
                        Mass,
                        SleepThreshold);
                    break;
            }

            // Apply per-body settings after creation
            if (_bodyHandle.HasValue)
            {
                var bodyRef = physicsWorld.Simulation.Bodies[_bodyHandle.Value];

                // For dynamic bodies that don't use gravity, zero out velocity each frame
                // (BepuPhysics applies gravity globally via PoseIntegratorCallbacks;
                //  per-body gravity toggle requires custom integrator logic or manual compensation)

                if (isDynamic)
                {
                    physicsWorld.Awaken(_bodyHandle.Value);
                }

                if (!isDynamic)
                {
                    // Kinematic: ensure zero velocity
                    bodyRef.Velocity.Linear = System.Numerics.Vector3.Zero;
                    bodyRef.Velocity.Angular = System.Numerics.Vector3.Zero;
                }
            }
        }

        private void RecreatePhysicsBody()
        {
            if (!_isInitialized || GameObject?.Scene?.PhysicsWorld == null)
                return;

            var physicsWorld = GameObject.Scene.PhysicsWorld;

            System.Numerics.Vector3 linearVelocity = System.Numerics.Vector3.Zero;
            System.Numerics.Vector3 angularVelocity = System.Numerics.Vector3.Zero;

            // Preserve velocities from dynamic bodies
            if (_bodyHandle.HasValue && _previousMotionType == BodyMotionType.Dynamic)
            {
                var bodyRef = physicsWorld.Simulation.Bodies[_bodyHandle.Value];
                linearVelocity = bodyRef.Velocity.Linear;
                angularVelocity = bodyRef.Velocity.Angular;
            }

            // Remove old body
            RemoveCurrentBody(physicsWorld);

            // Create new body
            CreatePhysicsBody();

            // Restore velocities if transitioning dynamic -> dynamic
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
            {
                InitializePhysics();
            }

            if (!HasBody || GameObject?.Scene?.PhysicsWorld == null || !Enabled)
                return;

            Vector3 currentScale = GetCurrentScale();
            if (currentScale != _previousScale)
            {
                RecreatePhysicsBody();
            }

            BodyMotionType finalMotionType = GetFinalMotionType();

            if (finalMotionType == BodyMotionType.Static || finalMotionType == BodyMotionType.Kinematic)
            {
                SyncToPhysics();
            }
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

            // Enforce freeze constraints on velocity
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
            {
                GameObject.Transform.SetWorldRotation(rotation);
            }
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

                // For kinematic bodies, compute velocity to reach target pose
                const float dt = 0.016f;
                var currentPos = bodyRef.Pose.Position;
                bodyRef.Velocity.Linear = (targetPos - currentPos) / dt;

                // Set pose directly for rotation (kinematic doesn't need torque-based rotation)
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
            {
                var bodyRef = GameObject.Scene.PhysicsWorld.Simulation.Bodies[_bodyHandle.Value];
                return ToOpenTK(bodyRef.Velocity.Linear);
            }
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
            {
                var bodyRef = GameObject.Scene.PhysicsWorld.Simulation.Bodies[_bodyHandle.Value];
                return ToOpenTK(bodyRef.Velocity.Angular);
            }
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

        public override void OnDestroy()
        {
            base.OnDestroy();

            if (GameObject?.Scene?.PhysicsWorld != null)
            {
                RemoveCurrentBody(GameObject.Scene.PhysicsWorld);
            }

            _isInitialized = false;
        }

        // --- Conversion helpers ---

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
    /// Replaces JoltPhysicsSharp.MotionType with a custom enum.
    /// </summary>
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