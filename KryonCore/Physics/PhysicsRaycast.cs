using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.Trees;
using BepuUtilities.Memory;

namespace KrayonCore.Physics
{
    // ─────────────────────────────────────────────────────────────
    //  Raycast Hit Result
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Result of a single raycast hit.
    /// </summary>
    public struct RaycastHit
    {
        /// <summary>Was anything hit?</summary>
        public bool Hit;

        /// <summary>World-space hit point.</summary>
        public Vector3 Point;

        /// <summary>Surface normal at the hit point.</summary>
        public Vector3 Normal;

        /// <summary>Distance along the ray direction to the hit.</summary>
        public float Distance;

        /// <summary>The collidable that was hit.</summary>
        public CollidableReference Collidable;

        /// <summary>Physics layer of the hit collidable.</summary>
        public PhysicsLayer Layer;

        /// <summary>
        /// If the hit collidable is a body (dynamic/kinematic), its BodyHandle.
        /// Check Collidable.Mobility to know if this is valid.
        /// </summary>
        public BodyHandle BodyHandle => Collidable.BodyHandle;

        /// <summary>
        /// If the hit collidable is static, its StaticHandle.
        /// </summary>
        public StaticHandle StaticHandle => Collidable.StaticHandle;

        /// <summary>True if the hit object is a dynamic or kinematic body.</summary>
        public bool IsBody => Collidable.Mobility != CollidableMobility.Static;

        /// <summary>True if the hit object is a static collidable.</summary>
        public bool IsStatic => Collidable.Mobility == CollidableMobility.Static;
    }

    // ─────────────────────────────────────────────────────────────
    //  IRayHitHandler: Closest hit with layer filtering
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Finds the closest hit along a ray, filtered by a layer mask.
    /// </summary>
    internal struct ClosestHitHandler : IRayHitHandler
    {
        public PhysicsLayerRegistry LayerRegistry;
        public PhysicsLayer LayerMask;
        public bool IgnoreTriggers;
        public TriggerRegistry TriggerRegistry;

        public float T;
        public Vector3 HitNormal;
        public CollidableReference HitCollidable;
        public bool FoundHit;

        public readonly Vector3 Origin;
        public readonly Vector3 Direction;

        public ClosestHitHandler(
            Vector3 origin,
            Vector3 direction,
            PhysicsLayerRegistry layerRegistry,
            PhysicsLayer layerMask,
            TriggerRegistry triggerRegistry,
            bool ignoreTriggers)
        {
            Origin = origin;
            Direction = direction;
            LayerRegistry = layerRegistry;
            LayerMask = layerMask;
            TriggerRegistry = triggerRegistry;
            IgnoreTriggers = ignoreTriggers;
            T = float.MaxValue;
            HitNormal = Vector3.Zero;
            HitCollidable = default;
            FoundHit = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AllowTest(CollidableReference collidable)
        {
            // Filter by layer
            if (!LayerRegistry.PassesFilter(collidable, LayerMask))
                return false;

            // Optionally skip triggers
            if (IgnoreTriggers && TriggerRegistry != null && TriggerRegistry.IsTrigger(collidable))
                return false;

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AllowTest(CollidableReference collidable, int childIndex)
        {
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnRayHit(in RayData ray, ref float maximumT, float t, Vector3 normal,
            CollidableReference collidable, int childIndex)
        {
            if (t < T)
            {
                T = t;
                HitNormal = normal;
                HitCollidable = collidable;
                FoundHit = true;
                // Narrow the search to closer hits only
                maximumT = t;
            }
        }

        public RaycastHit ToResult()
        {
            if (!FoundHit)
                return new RaycastHit { Hit = false };

            return new RaycastHit
            {
                Hit = true,
                Point = Origin + Direction * T,
                Normal = HitNormal,
                Distance = T,
                Collidable = HitCollidable,
                Layer = LayerRegistry.GetLayer(HitCollidable)
            };
        }

        public void OnRayHit(in RayData ray, ref float maximumT, float t, in Vector3 normal, CollidableReference collidable, int childIndex)
        {
            throw new NotImplementedException();
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  IRayHitHandler: All hits with layer filtering
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Collects ALL hits along a ray (unsorted), filtered by a layer mask.
    /// </summary>
    internal struct AllHitsHandler : IRayHitHandler
    {
        public PhysicsLayerRegistry LayerRegistry;
        public PhysicsLayer LayerMask;
        public bool IgnoreTriggers;
        public TriggerRegistry TriggerRegistry;
        public List<RaycastHit> Results;

        public readonly Vector3 Origin;
        public readonly Vector3 Direction;

        public AllHitsHandler(
            Vector3 origin,
            Vector3 direction,
            PhysicsLayerRegistry layerRegistry,
            PhysicsLayer layerMask,
            TriggerRegistry triggerRegistry,
            bool ignoreTriggers,
            List<RaycastHit> results)
        {
            Origin = origin;
            Direction = direction;
            LayerRegistry = layerRegistry;
            LayerMask = layerMask;
            TriggerRegistry = triggerRegistry;
            IgnoreTriggers = ignoreTriggers;
            Results = results;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AllowTest(CollidableReference collidable)
        {
            if (!LayerRegistry.PassesFilter(collidable, LayerMask))
                return false;

            if (IgnoreTriggers && TriggerRegistry != null && TriggerRegistry.IsTrigger(collidable))
                return false;

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AllowTest(CollidableReference collidable, int childIndex)
        {
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnRayHit(in RayData ray, ref float maximumT, float t, Vector3 normal,
            CollidableReference collidable, int childIndex)
        {
            Results.Add(new RaycastHit
            {
                Hit = true,
                Point = Origin + Direction * t,
                Normal = normal,
                Distance = t,
                Collidable = collidable,
                Layer = LayerRegistry.GetLayer(collidable)
            });
            // Don't narrow maximumT — we want all hits
        }

        public void OnRayHit(in RayData ray, ref float maximumT, float t, in Vector3 normal, CollidableReference collidable, int childIndex)
        {
            throw new NotImplementedException();
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  PhysicsRaycast — Static utility
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Provides convenient raycast methods with layer filtering on top of BepuPhysics2.
    /// All methods are designed to be called from the main thread.
    /// </summary>
    public static class PhysicsRaycast
    {
        // ── Single closest hit ──

        /// <summary>
        /// Casts a ray and returns the closest hit matching the layer mask.
        /// </summary>
        /// <param name="world">The physics world.</param>
        /// <param name="origin">Ray origin in world space.</param>
        /// <param name="direction">Ray direction (will be used as-is; normalize before calling if needed).</param>
        /// <param name="maxDistance">Maximum ray distance.</param>
        /// <param name="hit">The resulting hit info.</param>
        /// <param name="layerMask">Which layers to include. Defaults to All.</param>
        /// <param name="ignoreTriggers">If true, trigger collidables are skipped.</param>
        /// <returns>True if something was hit.</returns>
        public static bool Raycast(
            WorldPhysic world,
            Vector3 origin,
            Vector3 direction,
            float maxDistance,
            out RaycastHit hit,
            PhysicsLayer layerMask = PhysicsLayer.All,
            bool ignoreTriggers = true)
        {
            if (world?.Simulation == null)
            {
                hit = default;
                return false;
            }

            var handler = new ClosestHitHandler(
                origin, direction,
                world.LayerRegistry, layerMask,
                world.TriggerRegistry, ignoreTriggers);

            world.Simulation.RayCast(origin, direction, maxDistance, ref handler);

            hit = handler.ToResult();
            return hit.Hit;
        }

        /// <summary>
        /// Casts a ray with unlimited distance and returns the closest hit.
        /// </summary>
        public static bool Raycast(
            WorldPhysic world,
            Vector3 origin,
            Vector3 direction,
            out RaycastHit hit,
            PhysicsLayer layerMask = PhysicsLayer.All,
            bool ignoreTriggers = true)
        {
            return Raycast(world, origin, direction, float.MaxValue, out hit, layerMask, ignoreTriggers);
        }

        // ── All hits ──

        /// <summary>
        /// Casts a ray and returns ALL hits matching the layer mask, sorted by distance.
        /// </summary>
        public static List<RaycastHit> RaycastAll(
            WorldPhysic world,
            Vector3 origin,
            Vector3 direction,
            float maxDistance,
            PhysicsLayer layerMask = PhysicsLayer.All,
            bool ignoreTriggers = true)
        {
            var results = new List<RaycastHit>();

            if (world?.Simulation == null)
                return results;

            var handler = new AllHitsHandler(
                origin, direction,
                world.LayerRegistry, layerMask,
                world.TriggerRegistry, ignoreTriggers,
                results);

            world.Simulation.RayCast(origin, direction, maxDistance, ref handler);

            // Sort by distance (ascending)
            results.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            return results;
        }

        /// <summary>
        /// Non-allocating RaycastAll — fills an existing list.
        /// </summary>
        public static int RaycastAllNonAlloc(
            WorldPhysic world,
            Vector3 origin,
            Vector3 direction,
            float maxDistance,
            List<RaycastHit> results,
            PhysicsLayer layerMask = PhysicsLayer.All,
            bool ignoreTriggers = true)
        {
            results.Clear();

            if (world?.Simulation == null)
                return 0;

            var handler = new AllHitsHandler(
                origin, direction,
                world.LayerRegistry, layerMask,
                world.TriggerRegistry, ignoreTriggers,
                results);

            world.Simulation.RayCast(origin, direction, maxDistance, ref handler);

            results.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            return results.Count;
        }

        // ── Simple boolean check ──

        /// <summary>
        /// Returns true if anything would be hit by this ray (no allocation, fastest check).
        /// </summary>
        public static bool Check(
            WorldPhysic world,
            Vector3 origin,
            Vector3 direction,
            float maxDistance,
            PhysicsLayer layerMask = PhysicsLayer.All,
            bool ignoreTriggers = true)
        {
            return Raycast(world, origin, direction, maxDistance, out _, layerMask, ignoreTriggers);
        }

        // ── Line cast (point A → point B) ──

        /// <summary>
        /// Casts a ray between two points and returns the closest hit.
        /// </summary>
        public static bool Linecast(
            WorldPhysic world,
            Vector3 start,
            Vector3 end,
            out RaycastHit hit,
            PhysicsLayer layerMask = PhysicsLayer.All,
            bool ignoreTriggers = true)
        {
            var diff = end - start;
            var distance = diff.Length();
            if (distance < 1e-8f)
            {
                hit = default;
                return false;
            }

            var direction = diff / distance;
            return Raycast(world, start, direction, distance, out hit, layerMask, ignoreTriggers);
        }
    }
}