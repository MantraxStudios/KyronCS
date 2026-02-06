using System;
using System.Collections.Generic;
using System.Numerics;
using JoltPhysicsSharp;

namespace KrayonCore.Physics
{
    public class WorldPhysic : IDisposable
    {
        private const int MaxBodies = 65536;
        private const int MaxBodyPairs = 65536;
        private const int MaxContactConstraints = 65536;
        private const int NumBodyMutexes = 0;

        public static class Layers
        {
            public static readonly ObjectLayer NonMoving = 0;
            public static readonly ObjectLayer Moving = 1;
        }

        public static class BroadPhaseLayers
        {
            public static readonly BroadPhaseLayer NonMoving = 0;
            public static readonly BroadPhaseLayer Moving = 1;
        }

        private PhysicsSystemSettings _settings;
        private PhysicsSystem _physicsSystem;
        private JobSystem _jobSystem;
        private readonly List<Body> _bodies = new();

        public BodyInterface BodyInterface => _physicsSystem.BodyInterface;
        public BodyLockInterface BodyLockInterface => _physicsSystem.BodyLockInterface;
        public PhysicsSystem System => _physicsSystem;

        public WorldPhysic()
        {
            Foundation.Init();

            JobSystemThreadPoolConfig jobConfig = new JobSystemThreadPoolConfig();
            _jobSystem = new JobSystemThreadPool(jobConfig);

            ObjectLayerPairFilterTable objectLayerPairFilter = new(2);
            objectLayerPairFilter.EnableCollision(Layers.NonMoving, Layers.Moving);
            objectLayerPairFilter.EnableCollision(Layers.Moving, Layers.Moving);

            BroadPhaseLayerInterfaceTable broadPhaseLayerInterface = new(2, 2);
            broadPhaseLayerInterface.MapObjectToBroadPhaseLayer(Layers.NonMoving, BroadPhaseLayers.NonMoving);
            broadPhaseLayerInterface.MapObjectToBroadPhaseLayer(Layers.Moving, BroadPhaseLayers.Moving);

            ObjectVsBroadPhaseLayerFilterTable objectVsBroadPhaseLayerFilter = new(
                broadPhaseLayerInterface,
                2,
                objectLayerPairFilter,
                2
            );

            _settings = new PhysicsSystemSettings()
            {
                MaxBodies = MaxBodies,
                MaxBodyPairs = MaxBodyPairs,
                MaxContactConstraints = MaxContactConstraints,
                NumBodyMutexes = NumBodyMutexes,
                ObjectLayerPairFilter = objectLayerPairFilter,
                BroadPhaseLayerInterface = broadPhaseLayerInterface,
                ObjectVsBroadPhaseLayerFilter = objectVsBroadPhaseLayerFilter
            };

            _physicsSystem = new PhysicsSystem(_settings);
            _physicsSystem.Gravity = new Vector3(0, -9.81f, 0);
        }

        public void Update(float deltaTime, int collisionSteps = 1)
        {
            if (_physicsSystem != null && _jobSystem != null)
            {
                _physicsSystem.Update(deltaTime, collisionSteps, _jobSystem);
            }
        }

        public void ClearAllBodies()
        {
            foreach (Body body in _bodies)
            {
                BodyInterface.RemoveAndDestroyBody(body.ID);
            }
            _bodies.Clear();
        }

        public Body CreateFloor(float size, ObjectLayer layer)
        {
            BoxShape shape = new(new Vector3(size, 0.5f, size));
            using BodyCreationSettings creationSettings = new(
                shape,
                new Vector3(0, -0.5f, 0.0f),
                Quaternion.Identity,
                MotionType.Static,
                layer
            );

            Body body = BodyInterface.CreateBody(creationSettings);
            BodyInterface.AddBody(body.ID, Activation.DontActivate);
            _bodies.Add(body);

            return body;
        }

        public Body CreateBox(
            in Vector3 halfExtent,
            in Vector3 position,
            in Quaternion rotation,
            MotionType motionType,
            ObjectLayer layer,
            Activation activation = Activation.Activate)
        {
            BoxShape shape = new(halfExtent);
            using BodyCreationSettings creationSettings = new(
                shape,
                position,
                rotation,
                motionType,
                layer
            );

            Body body = BodyInterface.CreateBody(creationSettings);
            BodyInterface.AddBody(body.ID, activation);
            _bodies.Add(body);

            return body;
        }

        public Body CreateSphere(
            float radius,
            in Vector3 position,
            in Quaternion rotation,
            MotionType motionType,
            ObjectLayer layer,
            Activation activation = Activation.Activate)
        {
            SphereShape shape = new(radius);
            using BodyCreationSettings creationSettings = new(
                shape,
                position,
                rotation,
                motionType,
                layer
            );

            Body body = BodyInterface.CreateBody(creationSettings);
            BodyInterface.AddBody(body.ID, activation);
            _bodies.Add(body);

            return body;
        }

        public Body CreateCapsule(
            float halfHeight,
            float radius,
            in Vector3 position,
            in Quaternion rotation,
            MotionType motionType,
            ObjectLayer layer,
            Activation activation = Activation.Activate)
        {
            CapsuleShape shape = new(halfHeight, radius);
            using BodyCreationSettings creationSettings = new(
                shape,
                position,
                rotation,
                motionType,
                layer
            );

            Body body = BodyInterface.CreateBody(creationSettings);
            BodyInterface.AddBody(body.ID, activation);
            _bodies.Add(body);

            return body;
        }

        public void RemoveBody(Body body)
        {
            if (body != null)
            {
                BodyInterface.RemoveAndDestroyBody(body.ID);
                _bodies.Remove(body);
            }
        }

        public void RemoveBody(BodyID bodyID)
        {
            BodyInterface.RemoveAndDestroyBody(bodyID);
            _bodies.RemoveAll(b => b.ID == bodyID);
        }

        public void Dispose()
        {
            ClearAllBodies();
            _physicsSystem?.Dispose();
            _jobSystem?.Dispose();
            _physicsSystem = null;
            _jobSystem = null;
        }
    }
}