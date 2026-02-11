using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.Constraints;
using BepuUtilities;
using BepuUtilities.Memory;

namespace KrayonCore.Physics
{
    /// <summary>
    /// Narrow phase callbacks that handle contact material properties.
    /// </summary>
    public struct NarrowPhaseCallbacks : INarrowPhaseCallbacks
    {
        public SpringSettings ContactSpringiness;
        public float FrictionCoefficient;

        public NarrowPhaseCallbacks(SpringSettings contactSpringiness, float frictionCoefficient = 1f)
        {
            ContactSpringiness = contactSpringiness;
            FrictionCoefficient = frictionCoefficient;
        }

        public void Initialize(Simulation simulation) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AllowContactGeneration(int workerIndex, CollidableReference a, CollidableReference b, ref float speculativeMargin)
        {
            return a.Mobility == CollidableMobility.Dynamic || b.Mobility == CollidableMobility.Dynamic;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AllowContactGeneration(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB)
        {
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ConfigureContactManifold<TManifold>(int workerIndex, CollidablePair pair, ref TManifold manifold,
            out PairMaterialProperties pairMaterial) where TManifold : unmanaged, IContactManifold<TManifold>
        {
            pairMaterial.FrictionCoefficient = FrictionCoefficient;
            pairMaterial.MaximumRecoveryVelocity = 2f;
            pairMaterial.SpringSettings = ContactSpringiness;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ConfigureContactManifold(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB,
            ref ConvexContactManifold manifold)
        {
            return true;
        }

        public void Dispose() { }
    }

    /// <summary>
    /// Pose integrator callbacks that apply gravity and linear/angular damping.
    /// </summary>
    public struct PoseIntegratorCallbacks : IPoseIntegratorCallbacks
    {
        public Vector3 Gravity;
        public float LinearDamping;
        public float AngularDamping;

        private Vector3Wide _gravityWideDt;
        private Vector<float> _linearDampingDt;
        private Vector<float> _angularDampingDt;

        public readonly AngularIntegrationMode AngularIntegrationMode => AngularIntegrationMode.Nonconserving;
        public readonly bool AllowSubstepsForUnconstrainedBodies => false;
        public readonly bool IntegrateVelocityForKinematics => false;

        public PoseIntegratorCallbacks(Vector3 gravity, float linearDamping = 0.03f, float angularDamping = 0.03f)
        {
            Gravity = gravity;
            LinearDamping = linearDamping;
            AngularDamping = angularDamping;
        }

        public void Initialize(Simulation simulation) { }

        public void PrepareForIntegration(float dt)
        {
            _gravityWideDt = Vector3Wide.Broadcast(Gravity * dt);
            _linearDampingDt = new Vector<float>(MathF.Pow(MathHelper.Clamp(1f - LinearDamping, 0f, 1f), dt));
            _angularDampingDt = new Vector<float>(MathF.Pow(MathHelper.Clamp(1f - AngularDamping, 0f, 1f), dt));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void IntegrateVelocity(Vector<int> bodyIndices, Vector3Wide position, QuaternionWide orientation,
            BodyInertiaWide localInertia, Vector<int> integrationMask, int workerIndex,
            Vector<float> dt, ref BodyVelocityWide velocity)
        {
            velocity.Linear = (velocity.Linear + _gravityWideDt) * _linearDampingDt;
            velocity.Angular = velocity.Angular * _angularDampingDt;
        }
    }

    /// <summary>
    /// Physics world wrapper using BepuPhysics2.
    /// Replaces the previous JoltPhysicsSharp implementation.
    /// </summary>
    public class WorldPhysic : IDisposable
    {
        private BufferPool _bufferPool;
        private ThreadDispatcher _threadDispatcher;
        private Simulation _simulation;
        private readonly List<BodyHandle> _dynamicBodies = new();
        private readonly List<StaticHandle> _staticBodies = new();

        /// <summary>
        /// The underlying BepuPhysics Simulation instance.
        /// </summary>
        public Simulation Simulation => _simulation;

        public WorldPhysic()
        {
            _bufferPool = new BufferPool();

            var targetThreadCount = int.Max(1,
                Environment.ProcessorCount > 4
                    ? Environment.ProcessorCount - 2
                    : Environment.ProcessorCount - 1);
            _threadDispatcher = new ThreadDispatcher(targetThreadCount);

            _simulation = Simulation.Create(
                _bufferPool,
                new NarrowPhaseCallbacks(new SpringSettings(30, 1)),
                new PoseIntegratorCallbacks(new Vector3(0, -9.81f, 0)),
                new SolveDescription(8, 1));
        }

        public void Update(float deltaTime)
        {
            _simulation?.Timestep(deltaTime, _threadDispatcher);
        }

        public void ClearAllBodies()
        {
            foreach (var handle in _dynamicBodies)
            {
                if (_simulation.Bodies.BodyExists(handle))
                    _simulation.Bodies.Remove(handle);
            }
            _dynamicBodies.Clear();

            foreach (var handle in _staticBodies)
            {
                if (_simulation.Statics.StaticExists(handle))
                    _simulation.Statics.Remove(handle);
            }
            _staticBodies.Clear();
        }

        /// <summary>
        /// Creates a static floor plane (large box) at y = -0.5.
        /// </summary>
        public StaticHandle CreateFloor(float size)
        {
            var shape = new Box(size * 2f, 1f, size * 2f);
            var shapeIndex = _simulation.Shapes.Add(shape);

            var staticHandle = _simulation.Statics.Add(new StaticDescription(
                new Vector3(0, -0.5f, 0),
                shapeIndex));

            _staticBodies.Add(staticHandle);
            return staticHandle;
        }

        /// <summary>
        /// Creates a dynamic or kinematic box body.
        /// </summary>
        public BodyHandle CreateBox(
            in Vector3 halfExtent,
            in Vector3 position,
            in Quaternion rotation,
            bool isDynamic,
            float mass = 1f,
            float sleepThreshold = 0.01f)
        {
            var shape = new Box(halfExtent.X * 2f, halfExtent.Y * 2f, halfExtent.Z * 2f);
            var shapeIndex = _simulation.Shapes.Add(shape);

            BodyHandle handle;
            if (isDynamic)
            {
                var inertia = shape.ComputeInertia(mass);
                handle = _simulation.Bodies.Add(BodyDescription.CreateDynamic(
                    new RigidPose(position, rotation),
                    inertia,
                    shapeIndex,
                    sleepThreshold));
            }
            else
            {
                handle = _simulation.Bodies.Add(BodyDescription.CreateKinematic(
                    new RigidPose(position, rotation),
                    shapeIndex,
                    sleepThreshold));
            }

            _dynamicBodies.Add(handle);
            return handle;
        }

        /// <summary>
        /// Creates a dynamic or kinematic sphere body.
        /// </summary>
        public BodyHandle CreateSphere(
            float radius,
            in Vector3 position,
            in Quaternion rotation,
            bool isDynamic,
            float mass = 1f,
            float sleepThreshold = 0.01f)
        {
            var shape = new Sphere(radius);
            var shapeIndex = _simulation.Shapes.Add(shape);

            BodyHandle handle;
            if (isDynamic)
            {
                var inertia = shape.ComputeInertia(mass);
                handle = _simulation.Bodies.Add(BodyDescription.CreateDynamic(
                    new RigidPose(position, rotation),
                    inertia,
                    shapeIndex,
                    sleepThreshold));
            }
            else
            {
                handle = _simulation.Bodies.Add(BodyDescription.CreateKinematic(
                    new RigidPose(position, rotation),
                    shapeIndex,
                    sleepThreshold));
            }

            _dynamicBodies.Add(handle);
            return handle;
        }

        /// <summary>
        /// Creates a dynamic or kinematic capsule body.
        /// </summary>
        public BodyHandle CreateCapsule(
            float halfLength,
            float radius,
            in Vector3 position,
            in Quaternion rotation,
            bool isDynamic,
            float mass = 1f,
            float sleepThreshold = 0.01f)
        {
            var shape = new Capsule(radius, halfLength * 2f);
            var shapeIndex = _simulation.Shapes.Add(shape);

            BodyHandle handle;
            if (isDynamic)
            {
                var inertia = shape.ComputeInertia(mass);
                handle = _simulation.Bodies.Add(BodyDescription.CreateDynamic(
                    new RigidPose(position, rotation),
                    inertia,
                    shapeIndex,
                    sleepThreshold));
            }
            else
            {
                handle = _simulation.Bodies.Add(BodyDescription.CreateKinematic(
                    new RigidPose(position, rotation),
                    shapeIndex,
                    sleepThreshold));
            }

            _dynamicBodies.Add(handle);
            return handle;
        }

        /// <summary>
        /// Creates a static body (immovable, no velocity/inertia).
        /// Ideal for level geometry.
        /// </summary>
        public StaticHandle CreateStaticBox(in Vector3 halfExtent, in Vector3 position, in Quaternion rotation)
        {
            var shape = new Box(halfExtent.X * 2f, halfExtent.Y * 2f, halfExtent.Z * 2f);
            var shapeIndex = _simulation.Shapes.Add(shape);

            var handle = _simulation.Statics.Add(new StaticDescription(
                new RigidPose(position, rotation),
                shapeIndex));

            _staticBodies.Add(handle);
            return handle;
        }

        /// <summary>
        /// Removes a dynamic/kinematic body from the simulation.
        /// </summary>
        public void RemoveBody(BodyHandle handle)
        {
            if (_simulation.Bodies.BodyExists(handle))
            {
                _simulation.Bodies.Remove(handle);
                _dynamicBodies.Remove(handle);
            }
        }

        /// <summary>
        /// Removes a static body from the simulation.
        /// </summary>
        public void RemoveStatic(StaticHandle handle)
        {
            if (_simulation.Statics.StaticExists(handle))
            {
                _simulation.Statics.Remove(handle);
                _staticBodies.Remove(handle);
            }
        }

        /// <summary>
        /// Gets a reference to a body for reading/writing pose, velocity, etc.
        /// </summary>
        public BodyReference GetBodyReference(BodyHandle handle)
        {
            return _simulation.Bodies[handle];
        }

        /// <summary>
        /// Sets the linear velocity of a body.
        /// </summary>
        public void SetLinearVelocity(BodyHandle handle, Vector3 velocity)
        {
            _simulation.Bodies[handle].Velocity.Linear = velocity;
        }

        /// <summary>
        /// Wakes up a sleeping body.
        /// </summary>
        public void Awaken(BodyHandle handle)
        {
            _simulation.Awakener.AwakenBody(handle);
        }

        public void Dispose()
        {
            ClearAllBodies();
            _simulation?.Dispose();
            _threadDispatcher?.Dispose();
            _bufferPool?.Clear();
            _simulation = null;
            _threadDispatcher = null;
            _bufferPool = null;
        }
    }
}