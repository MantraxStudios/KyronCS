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

        // Capas de objetos
        public static class Layers
        {
            public static readonly ObjectLayer NonMoving = 0;
            public static readonly ObjectLayer Moving = 1;
        }

        // Capas de BroadPhase
        public static class BroadPhaseLayers
        {
            public static readonly BroadPhaseLayer NonMoving = 0;
            public static readonly BroadPhaseLayer Moving = 1;
        }

        private PhysicsSystemSettings _settings;
        private PhysicsSystem _physicsSystem;
        private JobSystem _jobSystem;  // Cambiado a JobSystem en lugar de JobSystemThreadPool
        private readonly List<Body> _bodies = new();

        public BodyInterface BodyInterface => _physicsSystem.BodyInterface;
        public BodyLockInterface BodyLockInterface => _physicsSystem.BodyLockInterface;
        public PhysicsSystem System => _physicsSystem;

        public WorldPhysic()
        {
            // 1. FUNDAMENTAL: Inicializar la base nativa
            Foundation.Init();

            // Definir la configuración del Job System
            JobSystemThreadPoolConfig jobConfig = new JobSystemThreadPoolConfig();
            
            _jobSystem = new JobSystemThreadPool(jobConfig);

            // 3. Crear los filtros (Mantenlos como variables de clase si el error persiste)
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

            // 4. Configurar Settings
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

            // 5. Instanciar el sistema
            _physicsSystem = new PhysicsSystem(_settings);
            _physicsSystem.Gravity = new Vector3(0, -9.81f, 0);
        }

        private void SetupCollisionFiltering()
        {
            // Configurar filtro de pares de capas de objetos
            ObjectLayerPairFilterTable objectLayerPairFilter = new(2);
            objectLayerPairFilter.EnableCollision(Layers.NonMoving, Layers.Moving);
            objectLayerPairFilter.EnableCollision(Layers.Moving, Layers.Moving);

            // Mapeo 1-a-1 entre capas de objetos y capas de broadphase
            BroadPhaseLayerInterfaceTable broadPhaseLayerInterface = new(2, 2);
            broadPhaseLayerInterface.MapObjectToBroadPhaseLayer(Layers.NonMoving, BroadPhaseLayers.NonMoving);
            broadPhaseLayerInterface.MapObjectToBroadPhaseLayer(Layers.Moving, BroadPhaseLayers.Moving);

            ObjectVsBroadPhaseLayerFilterTable objectVsBroadPhaseLayerFilter = new(
                broadPhaseLayerInterface,
                2,
                objectLayerPairFilter,
                2
            );

            _settings.ObjectLayerPairFilter = objectLayerPairFilter;
            _settings.BroadPhaseLayerInterface = broadPhaseLayerInterface;
            _settings.ObjectVsBroadPhaseLayerFilter = objectVsBroadPhaseLayerFilter;
        }

        /// <summary>
        /// Actualiza la simulación de física
        /// </summary>
        public void Update(float deltaTime, int collisionSteps = 1)
        {
            if (_physicsSystem != null && _jobSystem != null)
            {
                _physicsSystem.Update(deltaTime, collisionSteps, _jobSystem);
            }
        }

        /// <summary>
        /// Crea un suelo estático
        /// </summary>
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

        /// <summary>
        /// Crea una caja
        /// </summary>
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

        /// <summary>
        /// Crea una esfera
        /// </summary>
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

        /// <summary>
        /// Crea una cápsula
        /// </summary>
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

        /// <summary>
        /// Remueve y destruye un cuerpo
        /// </summary>
        public void RemoveBody(Body body)
        {
            if (body != null)
            {
                BodyInterface.RemoveAndDestroyBody(body.ID);
                _bodies.Remove(body);
            }
        }

        /// <summary>
        /// Remueve y destruye un cuerpo por ID
        /// </summary>
        public void RemoveBody(BodyID bodyID)
        {
            BodyInterface.RemoveAndDestroyBody(bodyID);
            _bodies.RemoveAll(b => b.ID == bodyID);
        }

        public void Dispose()
        {
            // Remover todos los cuerpos
            foreach (Body body in _bodies)
            {
                BodyInterface.RemoveAndDestroyBody(body.ID);
            }
            _bodies.Clear();

            // Liberar recursos en el orden correcto
            _physicsSystem?.Dispose();
            _jobSystem?.Dispose();

            _physicsSystem = null;
            _jobSystem = null;
        }
    }
}