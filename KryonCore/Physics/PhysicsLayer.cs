using System;
using System.Collections.Generic;
using BepuPhysics;
using BepuPhysics.Collidables;

namespace KrayonCore.Physics
{
    // ─────────────────────────────────────────────────────────────
    //  Physics Layers (bitmask, up to 32 layers)
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Predefined physics layers. Each layer is a single bit.
    /// You can define up to 32 layers using powers of 2.
    /// </summary>
    [Flags]
    public enum PhysicsLayer : uint
    {
        None = 0,
        Default = 1 << 0,
        Static = 1 << 1,
        Player = 1 << 2,
        Enemy = 1 << 3,
        Projectile = 1 << 4,
        Trigger = 1 << 5,
        Environment = 1 << 6,
        UI = 1 << 7,
        Layer8 = 1 << 8,
        Layer9 = 1 << 9,
        Layer10 = 1 << 10,
        Layer11 = 1 << 11,
        Layer12 = 1 << 12,
        Layer13 = 1 << 13,
        Layer14 = 1 << 14,
        Layer15 = 1 << 15,
        Layer16 = 1 << 16,
        Layer17 = 1 << 17,
        Layer18 = 1 << 18,
        Layer19 = 1 << 19,
        Layer20 = 1 << 20,
        Layer21 = 1 << 21,
        Layer22 = 1 << 22,
        Layer23 = 1 << 23,
        Layer24 = 1 << 24,
        Layer25 = 1 << 25,
        Layer26 = 1 << 26,
        Layer27 = 1 << 27,
        Layer28 = 1 << 28,
        Layer29 = 1 << 29,
        Layer30 = 1 << 30,
        Layer31 = 1u << 31,

        /// <summary>All layers enabled.</summary>
        All = ~0u
    }

    // ─────────────────────────────────────────────────────────────
    //  Layer Registry
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Maps collidables (bodies and statics) to their physics layer.
    /// Thread-safe for reads; writes should happen on the main thread.
    /// </summary>
    public class PhysicsLayerRegistry
    {
        // Separate dictionaries for bodies and statics to avoid CollidableId overhead
        private readonly Dictionary<int, PhysicsLayer> _bodyLayers = new();
        private readonly Dictionary<int, PhysicsLayer> _staticLayers = new();

        /// <summary>
        /// Sets the layer for a dynamic/kinematic body.
        /// </summary>
        public void SetLayer(BodyHandle handle, PhysicsLayer layer)
        {
            lock (_bodyLayers)
                _bodyLayers[handle.Value] = layer;
        }

        /// <summary>
        /// Sets the layer for a static body.
        /// </summary>
        public void SetLayer(StaticHandle handle, PhysicsLayer layer)
        {
            lock (_staticLayers)
                _staticLayers[handle.Value] = layer;
        }

        /// <summary>
        /// Gets the layer for a collidable. Returns PhysicsLayer.Default if not registered.
        /// </summary>
        public PhysicsLayer GetLayer(CollidableReference collidable)
        {
            if (collidable.Mobility == CollidableMobility.Static)
            {
                lock (_staticLayers)
                    return _staticLayers.TryGetValue(collidable.StaticHandle.Value, out var layer)
                        ? layer : PhysicsLayer.Default;
            }
            else
            {
                lock (_bodyLayers)
                    return _bodyLayers.TryGetValue(collidable.BodyHandle.Value, out var layer)
                        ? layer : PhysicsLayer.Default;
            }
        }

        /// <summary>
        /// Gets the layer for a body handle. Returns PhysicsLayer.Default if not registered.
        /// </summary>
        public PhysicsLayer GetLayer(BodyHandle handle)
        {
            lock (_bodyLayers)
                return _bodyLayers.TryGetValue(handle.Value, out var layer)
                    ? layer : PhysicsLayer.Default;
        }

        /// <summary>
        /// Gets the layer for a static handle. Returns PhysicsLayer.Default if not registered.
        /// </summary>
        public PhysicsLayer GetLayer(StaticHandle handle)
        {
            lock (_staticLayers)
                return _staticLayers.TryGetValue(handle.Value, out var layer)
                    ? layer : PhysicsLayer.Default;
        }

        /// <summary>
        /// Checks if a collidable's layer passes a layer mask filter.
        /// </summary>
        public bool PassesFilter(CollidableReference collidable, PhysicsLayer layerMask)
        {
            return (GetLayer(collidable) & layerMask) != 0;
        }

        public void RemoveBody(BodyHandle handle)
        {
            lock (_bodyLayers)
                _bodyLayers.Remove(handle.Value);
        }

        public void RemoveStatic(StaticHandle handle)
        {
            lock (_staticLayers)
                _staticLayers.Remove(handle.Value);
        }

        public void Clear()
        {
            lock (_bodyLayers) _bodyLayers.Clear();
            lock (_staticLayers) _staticLayers.Clear();
        }
    }
}