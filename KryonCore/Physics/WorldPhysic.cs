using System;
using System.Collections.Concurrent;
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
    // ─────────────────────────────────────────────────────────────
    //  Data types
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Identifies one collidable in a collision/trigger pair.
    /// Works for both Bodies (dynamic/kinematic) and Statics.
    /// </summary>
    public readonly struct CollidableId : IEquatable<CollidableId>
    {
        public readonly CollidableMobility Mobility;
        public readonly int RawHandle;

        public CollidableId(CollidableReference reference)
        {
            Mobility = reference.Mobility;
            RawHandle = reference.Mobility == CollidableMobility.Static
                ? reference.StaticHandle.Value
                : reference.BodyHandle.Value;
        }

        public bool IsStatic => Mobility == CollidableMobility.Static;
        public BodyHandle BodyHandle => new(RawHandle);
        public StaticHandle StaticHandle => new(RawHandle);

        public bool Equals(CollidableId other) => Mobility == other.Mobility && RawHandle == other.RawHandle;
        public override bool Equals(object obj) => obj is CollidableId other && Equals(other);
        public override int GetHashCode() => HashCode.Combine((int)Mobility, RawHandle);
        public static bool operator ==(CollidableId a, CollidableId b) => a.Equals(b);
        public static bool operator !=(CollidableId a, CollidableId b) => !a.Equals(b);
    }

    /// <summary>
    /// An ordered pair of collidables. A is always <= B to ensure uniqueness.
    /// </summary>
    public readonly struct CollisionPairKey : IEquatable<CollisionPairKey>
    {
        public readonly CollidableId A;
        public readonly CollidableId B;

        public CollisionPairKey(CollidableId a, CollidableId b)
        {
            // Canonical ordering to avoid duplicate pairs
            if (a.GetHashCode() <= b.GetHashCode()) { A = a; B = b; }
            else { A = b; B = a; }
        }

        public bool Equals(CollisionPairKey other) => A == other.A && B == other.B;
        public override bool Equals(object obj) => obj is CollisionPairKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(A, B);
    }

    /// <summary>
    /// Contact data passed to collision event handlers.
    /// </summary>
    public struct ContactInfo
    {
        public Vector3 ContactPosition;
        public Vector3 ContactNormal;
        public float PenetrationDepth;
        public CollidableId OtherCollidable;
    }

    /// <summary>
    /// Collision event types.
    /// </summary>
    public enum CollisionEventType
    {
        CollisionEnter,
        CollisionStay,
        CollisionExit,
        TriggerEnter,
        TriggerStay,
        TriggerExit
    }

    /// <summary>
    /// A deferred collision/trigger event ready to be dispatched on the main thread.
    /// </summary>
    public struct DeferredCollisionEvent
    {
        public CollisionEventType EventType;
        public CollidableId Source;
        public CollidableId Other;
        public ContactInfo Contact;
    }

    // ─────────────────────────────────────────────────────────────
    //  Trigger registry
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Tracks which collidables are triggers (overlap-only, no physics response).
    /// Thread-safe for reads during narrow phase.
    /// </summary>
    public class TriggerRegistry
    {
        private readonly HashSet<CollidableId> _triggers = new();

        public void Register(CollidableId id) { lock (_triggers) _triggers.Add(id); }
        public void Unregister(CollidableId id) { lock (_triggers) _triggers.Remove(id); }
        public bool IsTrigger(CollidableId id) { lock (_triggers) return _triggers.Contains(id); }
        public void Clear() { lock (_triggers) _triggers.Clear(); }

        public bool IsTrigger(CollidableReference reference) => IsTrigger(new CollidableId(reference));
    }

    // ─────────────────────────────────────────────────────────────
    //  Collision event handler interface
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Implement this on components to receive collision/trigger events.
    /// </summary>
    public interface ICollisionEventHandler
    {
        void OnCollisionEnter(ContactInfo contact) { }
        void OnCollisionStay(ContactInfo contact) { }
        void OnCollisionExit(ContactInfo contact) { }
        void OnTriggerEnter(ContactInfo contact) { }
        void OnTriggerStay(ContactInfo contact) { }
        void OnTriggerExit(ContactInfo contact) { }
    }

    // ─────────────────────────────────────────────────────────────
    //  Collision Event System
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Collects contact manifold data from the narrow phase (multithreaded)
    /// and produces deferred Enter/Stay/Exit events dispatched on the main thread.
    /// </summary>
    public class CollisionEventSystem
    {
        // Handler registration: CollidableId -> handler
        private readonly Dictionary<CollidableId, ICollisionEventHandler> _handlers = new();

        // Pair tracking for Enter / Stay / Exit detection
        private readonly HashSet<CollisionPairKey> _activePairsThisFrame = new();
        private readonly HashSet<CollisionPairKey> _activePairsLastFrame = new();

        // Trigger pair tracking (separate because trigger pairs don't generate constraints)
        private readonly HashSet<CollisionPairKey> _activeTriggerPairsThisFrame = new();
        private readonly HashSet<CollisionPairKey> _activeTriggerPairsLastFrame = new();

        // Thread-safe queue filled during narrow phase
        private readonly ConcurrentBag<PairReport> _narrowPhaseReports = new();

        // Deferred events dispatched on main thread
        private readonly List<DeferredCollisionEvent> _deferredEvents = new();

        private readonly TriggerRegistry _triggerRegistry;

        public CollisionEventSystem(TriggerRegistry triggerRegistry)
        {
            _triggerRegistry = triggerRegistry;
        }

        // ── Handler registration ──

        public void RegisterHandler(CollidableId id, ICollisionEventHandler handler)
        {
            _handlers[id] = handler;
        }

        public void UnregisterHandler(CollidableId id)
        {
            _handlers.Remove(id);
        }

        public ICollisionEventHandler GetHandler(CollidableId id)
        {
            _handlers.TryGetValue(id, out var handler);
            return handler;
        }

        /// <summary>
        /// Resolves a CollidableId to its owning GameObject (if the handler is a Component).
        /// </summary>
        public GameObject GetGameObject(CollidableId id)
        {
            if (_handlers.TryGetValue(id, out var handler) && handler is Component comp)
                return comp.GameObject;
            return null;
        }

        // ── Called from narrow phase (multithreaded!) ──

        internal struct PairReport
        {
            public CollidableId A;
            public CollidableId B;
            public Vector3 ContactPosition;
            public Vector3 ContactNormal;
            public float Depth;
            public bool IsTrigger;
        }

        /// <summary>
        /// Called from INarrowPhaseCallbacks.ConfigureContactManifold on worker threads.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ReportContact(CollidableReference refA, CollidableReference refB,
            Vector3 contactPosition, Vector3 contactNormal, float depth, bool isTrigger)
        {
            _narrowPhaseReports.Add(new PairReport
            {
                A = new CollidableId(refA),
                B = new CollidableId(refB),
                ContactPosition = contactPosition,
                ContactNormal = contactNormal,
                Depth = depth,
                IsTrigger = isTrigger
            });
        }

        // ── Main thread: call after Simulation.Timestep ──

        /// <summary>
        /// Process all narrow phase reports and dispatch Enter/Stay/Exit events.
        /// Must be called on the main thread after each simulation timestep.
        /// </summary>
        public void FlushEvents()
        {
            _deferredEvents.Clear();

            // Move last frame -> swap
            _activePairsLastFrame.Clear();
            foreach (var p in _activePairsThisFrame) _activePairsLastFrame.Add(p);
            _activePairsThisFrame.Clear();

            _activeTriggerPairsLastFrame.Clear();
            foreach (var p in _activeTriggerPairsThisFrame) _activeTriggerPairsLastFrame.Add(p);
            _activeTriggerPairsThisFrame.Clear();

            // Process all reports from narrow phase
            while (_narrowPhaseReports.TryTake(out var report))
            {
                var pairKey = new CollisionPairKey(report.A, report.B);

                if (report.IsTrigger)
                {
                    _activeTriggerPairsThisFrame.Add(pairKey);
                    bool wasActive = _activeTriggerPairsLastFrame.Contains(pairKey);

                    var eventType = wasActive ? CollisionEventType.TriggerStay : CollisionEventType.TriggerEnter;
                    EnqueueEvent(eventType, report);
                }
                else
                {
                    _activePairsThisFrame.Add(pairKey);
                    bool wasActive = _activePairsLastFrame.Contains(pairKey);

                    var eventType = wasActive ? CollisionEventType.CollisionStay : CollisionEventType.CollisionEnter;
                    EnqueueEvent(eventType, report);
                }
            }

            // Detect Exit: pairs that were active last frame but not this frame
            foreach (var pairKey in _activePairsLastFrame)
            {
                if (!_activePairsThisFrame.Contains(pairKey))
                {
                    EnqueueExitEvent(CollisionEventType.CollisionExit, pairKey);
                }
            }

            foreach (var pairKey in _activeTriggerPairsLastFrame)
            {
                if (!_activeTriggerPairsThisFrame.Contains(pairKey))
                {
                    EnqueueExitEvent(CollisionEventType.TriggerExit, pairKey);
                }
            }

            // Dispatch all deferred events to handlers
            DispatchEvents();
        }

        private void EnqueueEvent(CollisionEventType type, in PairReport report)
        {
            // Enqueue for A (other = B)
            if (_handlers.ContainsKey(report.A))
            {
                _deferredEvents.Add(new DeferredCollisionEvent
                {
                    EventType = type,
                    Source = report.A,
                    Other = report.B,
                    Contact = new ContactInfo
                    {
                        ContactPosition = report.ContactPosition,
                        ContactNormal = report.ContactNormal,
                        PenetrationDepth = report.Depth,
                        OtherCollidable = report.B
                    }
                });
            }

            // Enqueue for B (other = A)
            if (_handlers.ContainsKey(report.B))
            {
                _deferredEvents.Add(new DeferredCollisionEvent
                {
                    EventType = type,
                    Source = report.B,
                    Other = report.A,
                    Contact = new ContactInfo
                    {
                        ContactPosition = report.ContactPosition,
                        ContactNormal = -report.ContactNormal,
                        PenetrationDepth = report.Depth,
                        OtherCollidable = report.A
                    }
                });
            }
        }

        private void EnqueueExitEvent(CollisionEventType type, CollisionPairKey pairKey)
        {
            var emptyContact = new ContactInfo
            {
                ContactPosition = Vector3.Zero,
                ContactNormal = Vector3.Zero,
                PenetrationDepth = 0
            };

            if (_handlers.ContainsKey(pairKey.A))
            {
                emptyContact.OtherCollidable = pairKey.B;
                _deferredEvents.Add(new DeferredCollisionEvent
                {
                    EventType = type,
                    Source = pairKey.A,
                    Other = pairKey.B,
                    Contact = emptyContact
                });
            }

            if (_handlers.ContainsKey(pairKey.B))
            {
                emptyContact.OtherCollidable = pairKey.A;
                _deferredEvents.Add(new DeferredCollisionEvent
                {
                    EventType = type,
                    Source = pairKey.B,
                    Other = pairKey.A,
                    Contact = emptyContact
                });
            }
        }

        private void DispatchEvents()
        {
            foreach (var evt in _deferredEvents)
            {
                if (!_handlers.TryGetValue(evt.Source, out var handler))
                    continue;

                switch (evt.EventType)
                {
                    case CollisionEventType.CollisionEnter: handler.OnCollisionEnter(evt.Contact); break;
                    case CollisionEventType.CollisionStay: handler.OnCollisionStay(evt.Contact); break;
                    case CollisionEventType.CollisionExit: handler.OnCollisionExit(evt.Contact); break;
                    case CollisionEventType.TriggerEnter: handler.OnTriggerEnter(evt.Contact); break;
                    case CollisionEventType.TriggerStay: handler.OnTriggerStay(evt.Contact); break;
                    case CollisionEventType.TriggerExit: handler.OnTriggerExit(evt.Contact); break;
                }
            }
        }

        public void Clear()
        {
            _handlers.Clear();
            _activePairsThisFrame.Clear();
            _activePairsLastFrame.Clear();
            _activeTriggerPairsThisFrame.Clear();
            _activeTriggerPairsLastFrame.Clear();
            while (_narrowPhaseReports.TryTake(out _)) { }
            _deferredEvents.Clear();
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  Narrow Phase Callbacks (replaces the old simple one)
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// NarrowPhase callbacks that feed contact data into the CollisionEventSystem
    /// and handle trigger (sensor) bodies by suppressing constraint generation.
    /// </summary>
    public struct CollisionNarrowPhaseCallbacks : INarrowPhaseCallbacks
    {
        public SpringSettings ContactSpringiness;
        public float FrictionCoefficient;

        /// <summary>
        /// Shared references (boxed/heap) accessible from the struct.
        /// Because INarrowPhaseCallbacks must be a struct, we hold references to the
        /// shared systems through a helper object.
        /// </summary>
        public CollisionCallbackSharedData Shared;

        public CollisionNarrowPhaseCallbacks(SpringSettings contactSpringiness, float friction,
            CollisionCallbackSharedData shared)
        {
            ContactSpringiness = contactSpringiness;
            FrictionCoefficient = friction;
            Shared = shared;
        }

        public void Initialize(Simulation simulation) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AllowContactGeneration(int workerIndex, CollidableReference a, CollidableReference b,
            ref float speculativeMargin)
        {
            // Always generate contacts so we can detect triggers too.
            // At minimum one must be dynamic for a constraint to be meaningful,
            // but we also want kinematic-kinematic trigger detection.
            return a.Mobility == CollidableMobility.Dynamic
                || b.Mobility == CollidableMobility.Dynamic
                || Shared.TriggerRegistry.IsTrigger(a)
                || Shared.TriggerRegistry.IsTrigger(b);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AllowContactGeneration(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB)
        {
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ConfigureContactManifold<TManifold>(int workerIndex, CollidablePair pair,
            ref TManifold manifold, out PairMaterialProperties pairMaterial)
            where TManifold : unmanaged, IContactManifold<TManifold>
        {
            bool aIsTrigger = Shared.TriggerRegistry.IsTrigger(pair.A);
            bool bIsTrigger = Shared.TriggerRegistry.IsTrigger(pair.B);
            bool isTriggerPair = aIsTrigger || bIsTrigger;

            // Extract first contact point for the event report
            Vector3 contactPos = Vector3.Zero;
            Vector3 contactNormal = Vector3.Zero;
            float depth = 0;

            if (manifold.Count > 0)
            {
                // Get the first contact's data
                manifold.GetContact(0, out var offset, out contactNormal, out depth, out _);
                contactPos = offset;
            }

            // Report to the event system
            Shared.EventSystem.ReportContact(pair.A, pair.B, contactPos, contactNormal, depth, isTriggerPair);

            if (isTriggerPair)
            {
                // Triggers: generate no constraint (objects pass through each other)
                pairMaterial = default;
                return false;
            }

            // Normal collision: generate constraint
            pairMaterial.FrictionCoefficient = FrictionCoefficient;
            pairMaterial.MaximumRecoveryVelocity = 2f;
            pairMaterial.SpringSettings = ContactSpringiness;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ConfigureContactManifold(int workerIndex, CollidablePair pair,
            int childIndexA, int childIndexB, ref ConvexContactManifold manifold)
        {
            return true;
        }

        public void Dispose() { }
    }

    /// <summary>
    /// Heap-allocated shared data accessible from the NarrowPhaseCallbacks struct.
    /// </summary>
    public class CollisionCallbackSharedData
    {
        public CollisionEventSystem EventSystem;
        public TriggerRegistry TriggerRegistry;

        public CollisionCallbackSharedData(CollisionEventSystem eventSystem, TriggerRegistry triggerRegistry)
        {
            EventSystem = eventSystem;
            TriggerRegistry = triggerRegistry;
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  Pose Integrator Callbacks (unchanged)
    // ─────────────────────────────────────────────────────────────

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

    // ─────────────────────────────────────────────────────────────
    //  WorldPhysic (updated with event system + triggers)
    // ─────────────────────────────────────────────────────────────

    public class WorldPhysic : IDisposable
    {
        private BufferPool _bufferPool;
        private ThreadDispatcher _threadDispatcher;
        private Simulation _simulation;
        private readonly List<BodyHandle> _dynamicBodies = new();
        private readonly List<StaticHandle> _staticBodies = new();

        public TriggerRegistry TriggerRegistry { get; private set; }
        public CollisionEventSystem EventSystem { get; private set; }
        public PhysicsLayerRegistry LayerRegistry { get; private set; }
        public Simulation Simulation => _simulation;

        public WorldPhysic()
        {
            _bufferPool = new BufferPool();

            var targetThreadCount = int.Max(1,
                Environment.ProcessorCount > 4
                    ? Environment.ProcessorCount - 2
                    : Environment.ProcessorCount - 1);
            _threadDispatcher = new ThreadDispatcher(targetThreadCount);

            TriggerRegistry = new TriggerRegistry();
            EventSystem = new CollisionEventSystem(TriggerRegistry);
            LayerRegistry = new PhysicsLayerRegistry();

            var sharedData = new CollisionCallbackSharedData(EventSystem, TriggerRegistry);

            _simulation = Simulation.Create(
                _bufferPool,
                new CollisionNarrowPhaseCallbacks(
                    new SpringSettings(30, 1),
                    1f,
                    sharedData),
                new PoseIntegratorCallbacks(new Vector3(0, -9.81f, 0)),
                new SolveDescription(8, 1));
        }

        /// <summary>
        /// Steps the simulation and flushes collision/trigger events.
        /// </summary>
        public void Update(float deltaTime)
        {
            _simulation?.Timestep(deltaTime, _threadDispatcher);
            EventSystem?.FlushEvents();
        }

        // ── Body creation ──

        public BodyHandle CreateBox(
            in Vector3 halfExtent,
            in Vector3 position,
            in Quaternion rotation,
            bool isDynamic,
            float mass = 1f,
            float sleepThreshold = 0.01f,
            PhysicsLayer layer = PhysicsLayer.Default)
        {
            var shape = new Box(halfExtent.X * 2f, halfExtent.Y * 2f, halfExtent.Z * 2f);
            var shapeIndex = _simulation.Shapes.Add(shape);

            BodyHandle handle;
            if (isDynamic)
            {
                var inertia = shape.ComputeInertia(mass);
                handle = _simulation.Bodies.Add(BodyDescription.CreateDynamic(
                    new RigidPose(position, rotation), inertia, shapeIndex, sleepThreshold));
            }
            else
            {
                handle = _simulation.Bodies.Add(BodyDescription.CreateKinematic(
                    new RigidPose(position, rotation), shapeIndex, sleepThreshold));
            }

            _dynamicBodies.Add(handle);
            LayerRegistry.SetLayer(handle, layer);
            return handle;
        }

        public BodyHandle CreateSphere(
            float radius,
            in Vector3 position,
            in Quaternion rotation,
            bool isDynamic,
            float mass = 1f,
            float sleepThreshold = 0.01f,
            PhysicsLayer layer = PhysicsLayer.Default)
        {
            var shape = new Sphere(radius);
            var shapeIndex = _simulation.Shapes.Add(shape);

            BodyHandle handle;
            if (isDynamic)
            {
                var inertia = shape.ComputeInertia(mass);
                handle = _simulation.Bodies.Add(BodyDescription.CreateDynamic(
                    new RigidPose(position, rotation), inertia, shapeIndex, sleepThreshold));
            }
            else
            {
                handle = _simulation.Bodies.Add(BodyDescription.CreateKinematic(
                    new RigidPose(position, rotation), shapeIndex, sleepThreshold));
            }

            _dynamicBodies.Add(handle);
            LayerRegistry.SetLayer(handle, layer);
            return handle;
        }

        public BodyHandle CreateCapsule(
            float halfLength,
            float radius,
            in Vector3 position,
            in Quaternion rotation,
            bool isDynamic,
            float mass = 1f,
            float sleepThreshold = 0.01f,
            PhysicsLayer layer = PhysicsLayer.Default)
        {
            var shape = new Capsule(radius, halfLength * 2f);
            var shapeIndex = _simulation.Shapes.Add(shape);

            BodyHandle handle;
            if (isDynamic)
            {
                var inertia = shape.ComputeInertia(mass);
                handle = _simulation.Bodies.Add(BodyDescription.CreateDynamic(
                    new RigidPose(position, rotation), inertia, shapeIndex, sleepThreshold));
            }
            else
            {
                handle = _simulation.Bodies.Add(BodyDescription.CreateKinematic(
                    new RigidPose(position, rotation), shapeIndex, sleepThreshold));
            }

            _dynamicBodies.Add(handle);
            LayerRegistry.SetLayer(handle, layer);
            return handle;
        }

        public StaticHandle CreateStaticBox(in Vector3 halfExtent, in Vector3 position, in Quaternion rotation,
            PhysicsLayer layer = PhysicsLayer.Static)
        {
            var shape = new Box(halfExtent.X * 2f, halfExtent.Y * 2f, halfExtent.Z * 2f);
            var shapeIndex = _simulation.Shapes.Add(shape);
            var handle = _simulation.Statics.Add(new StaticDescription(new RigidPose(position, rotation), shapeIndex));
            _staticBodies.Add(handle);
            LayerRegistry.SetLayer(handle, layer);
            return handle;
        }

        public StaticHandle CreateFloor(float size, PhysicsLayer layer = PhysicsLayer.Static)
        {
            var shape = new Box(size * 2f, 1f, size * 2f);
            var shapeIndex = _simulation.Shapes.Add(shape);
            var handle = _simulation.Statics.Add(new StaticDescription(new Vector3(0, -0.5f, 0), shapeIndex));
            _staticBodies.Add(handle);
            LayerRegistry.SetLayer(handle, layer);
            return handle;
        }

        // ── Removal ──

        public void RemoveBody(BodyHandle handle)
        {
            if (_simulation.Bodies.BodyExists(handle))
            {
                var collidableId = new CollidableId(
                    _simulation.Bodies[handle].CollidableReference);
                TriggerRegistry.Unregister(collidableId);
                EventSystem.UnregisterHandler(collidableId);
                LayerRegistry.RemoveBody(handle);

                _simulation.Bodies.Remove(handle);
                _dynamicBodies.Remove(handle);
            }
        }

        public void RemoveStatic(StaticHandle handle)
        {
            if (_simulation.Statics.StaticExists(handle))
            {
                var collidableId = new CollidableId(
                    new CollidableReference(handle));
                TriggerRegistry.Unregister(collidableId);
                EventSystem.UnregisterHandler(collidableId);
                LayerRegistry.RemoveStatic(handle);

                _simulation.Statics.Remove(handle);
                _staticBodies.Remove(handle);
            }
        }

        // ── Helpers ──

        public BodyReference GetBodyReference(BodyHandle handle) => _simulation.Bodies[handle];

        public void SetLinearVelocity(BodyHandle handle, Vector3 velocity)
        {
            _simulation.Bodies[handle].Velocity.Linear = velocity;
        }

        public void Awaken(BodyHandle handle)
        {
            _simulation.Awakener.AwakenBody(handle);
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

            TriggerRegistry.Clear();
            EventSystem.Clear();
            LayerRegistry.Clear();
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