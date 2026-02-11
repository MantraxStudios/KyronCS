using System;
using System.Collections.Generic;
using System.IO;
using Jint;
using KrayonCore.Core.Attributes;
using KrayonCore.Physics;
using OpenTK.Mathematics;

namespace KrayonCore
{
    public class GameScript : Component
    {
        [ToStorage] public string ScriptPath = "scripts/game.js";
        private Engine _engine;
        private bool _collisionEventsSubscribed = false;
        private List<RaycastHit> _raycastAllResults = new();

        public override void Awake()
        {
            Console.WriteLine("JS Started");

            _engine = new Engine(options =>
            {
                options
                    .LimitRecursion(100)
                    .MaxStatements(10000)
                    .TimeoutInterval(TimeSpan.FromSeconds(5));
            });

            // Console básico
            _engine.SetValue("console", new
            {
                log = new Action<object>(msg => Console.WriteLine(msg)),
                warn = new Action<object>(msg => Console.WriteLine($"[WARN] {msg}")),
                error = new Action<object>(msg => Console.WriteLine($"[ERROR] {msg}"))
            });

            // Librería matemática
            SetupMathLibrary();

            // Exponer el GameObject actual
            _engine.SetValue("gameObject", GameObject);

            // Funciones de componentes
            SetupComponentFunctions();

            // Eventos de colisión y API de física
            SetupCollisionEvents();
            SetupPhysicsRaycast();

            // Cargar y ejecutar el script
            string path = Path.Combine(
                AssetManager.BasePath,
                ScriptPath
            );

            try
            {
                _engine.Execute(File.ReadAllText(path));

                if (!_engine.GetValue("OnStart").IsUndefined())
                {
                    _engine.Invoke("OnStart");
                }

                TrySubscribeCollisionEvents();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error en JS:");
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }

        private void SetupMathLibrary()
        {
            _engine.SetValue("Math", new
            {
                PI = Math.PI,
                E = Math.E,

                abs = new Func<double, double>(Math.Abs),
                sqrt = new Func<double, double>(Math.Sqrt),
                pow = new Func<double, double, double>(Math.Pow),

                sin = new Func<double, double>(Math.Sin),
                cos = new Func<double, double>(Math.Cos),
                tan = new Func<double, double>(Math.Tan),
                asin = new Func<double, double>(Math.Asin),
                acos = new Func<double, double>(Math.Acos),
                atan = new Func<double, double>(Math.Atan),
                atan2 = new Func<double, double, double>(Math.Atan2),

                floor = new Func<double, double>(Math.Floor),
                ceil = new Func<double, double>(Math.Ceiling),
                round = new Func<double, double>(Math.Round),

                min = new Func<double, double, double>(Math.Min),
                max = new Func<double, double, double>(Math.Max),

                clamp = new Func<double, double, double, double>((value, min, max) =>
                    Math.Max(min, Math.Min(max, value))),

                lerp = new Func<double, double, double, double>((a, b, t) =>
                    a + (b - a) * t),

                degToRad = new Func<double, double>(deg => deg * Math.PI / 180.0),
                radToDeg = new Func<double, double>(rad => rad * 180.0 / Math.PI),

                random = new Func<double>(() => Random.Shared.NextDouble())
            });

            _engine.Execute(@"
                class Vector3 {
                    constructor(x = 0, y = 0, z = 0) {
                        this.x = x;
                        this.y = y;
                        this.z = z;
                    }

                    static zero() { return new Vector3(0, 0, 0); }
                    static one() { return new Vector3(1, 1, 1); }
                    static up() { return new Vector3(0, 1, 0); }
                    static down() { return new Vector3(0, -1, 0); }
                    static left() { return new Vector3(-1, 0, 0); }
                    static right() { return new Vector3(1, 0, 0); }
                    static forward() { return new Vector3(0, 0, -1); }
                    static back() { return new Vector3(0, 0, 1); }

                    add(other) {
                        return new Vector3(this.x + other.x, this.y + other.y, this.z + other.z);
                    }

                    subtract(other) {
                        return new Vector3(this.x - other.x, this.y - other.y, this.z - other.z);
                    }

                    multiply(scalar) {
                        return new Vector3(this.x * scalar, this.y * scalar, this.z * scalar);
                    }

                    magnitude() {
                        return Math.sqrt(this.x * this.x + this.y * this.y + this.z * this.z);
                    }

                    normalized() {
                        const mag = this.magnitude();
                        if (mag === 0) return Vector3.zero();
                        return this.multiply(1 / mag);
                    }

                    dot(other) {
                        return this.x * other.x + this.y * other.y + this.z * other.z;
                    }

                    cross(other) {
                        return new Vector3(
                            this.y * other.z - this.z * other.y,
                            this.z * other.x - this.x * other.z,
                            this.x * other.y - this.y * other.x
                        );
                    }

                    toString() {
                        return `Vector3(${this.x.toFixed(2)}, ${this.y.toFixed(2)}, ${this.z.toFixed(2)})`;
                    }
                }
            ");
        }

        private void SetupComponentFunctions()
        {
            // ── Raw getters (C# objects) ──
            _engine.SetValue("GetTransform", new Func<Transform>(() =>
                GameObject?.GetComponent<Transform>()));

            _engine.SetValue("GetRigidbody", new Func<Rigidbody>(() =>
                GameObject?.GetComponent<Rigidbody>()));

            _engine.SetValue("GetMeshRenderer", new Func<MeshRenderer>(() =>
                GameObject?.GetComponent<MeshRenderer>()));

            _engine.SetValue("CreateVector3", new Func<float, float, float, Vector3>((x, y, z) =>
                new Vector3(x, y, z)));

            // ── Transform wrapper ──
            _engine.Execute(@"
                const Transform = {
                    getPosition: function() {
                        const t = GetTransform();
                        if (!t) return Vector3.zero();
                        return new Vector3(t.X, t.Y, t.Z);
                    },
                    setPosition: function(x, y, z) {
                        const t = GetTransform();
                        if (t) t.SetPosition(x, y, z);
                    },
                    translate: function(x, y, z) {
                        const t = GetTransform();
                        if (t) t.Translate(x, y, z);
                    },
                    getRotation: function() {
                        const t = GetTransform();
                        if (!t) return Vector3.zero();
                        return new Vector3(t.RotationX, t.RotationY, t.RotationZ);
                    },
                    setRotation: function(x, y, z) {
                        const t = GetTransform();
                        if (t) t.SetRotation(x, y, z);
                    },
                    rotate: function(x, y, z) {
                        const t = GetTransform();
                        if (t) t.Rotate(x, y, z);
                    },
                    getScale: function() {
                        const t = GetTransform();
                        if (!t) return Vector3.one();
                        return new Vector3(t.ScaleX, t.ScaleY, t.ScaleZ);
                    },
                    setScale: function(x, y, z) {
                        const t = GetTransform();
                        if (t) t.SetScale(x, y, z);
                    }
                };
            ");

            // ── Rigidbody C# wrappers (double → float casting) ──
            SetupRigidbodyWrappers();
        }

        private void SetupRigidbodyWrappers()
        {
            // --- Forces / Impulses ---
            _engine.SetValue("_rb_addForce", new Action<double, double, double>((x, y, z) =>
            {
                var rb = GameObject?.GetComponent<Rigidbody>();
                rb?.AddForce(new Vector3((float)x, (float)y, (float)z));
            }));

            _engine.SetValue("_rb_addImpulse", new Action<double, double, double>((x, y, z) =>
            {
                var rb = GameObject?.GetComponent<Rigidbody>();
                rb?.AddImpulse(new Vector3((float)x, (float)y, (float)z));
            }));

            _engine.SetValue("_rb_addTorque", new Action<double, double, double>((x, y, z) =>
            {
                var rb = GameObject?.GetComponent<Rigidbody>();
                rb?.AddTorque(new Vector3((float)x, (float)y, (float)z));
            }));

            // --- Velocity ---
            _engine.SetValue("_rb_setVelocity", new Action<double, double, double>((x, y, z) =>
            {
                var rb = GameObject?.GetComponent<Rigidbody>();
                rb?.SetVelocity(new Vector3((float)x, (float)y, (float)z));
            }));

            _engine.SetValue("_rb_getVelocity", new Func<double[]>(() =>
            {
                var rb = GameObject?.GetComponent<Rigidbody>();
                if (rb == null) return new double[] { 0, 0, 0 };
                var v = rb.GetVelocity();
                return new double[] { v.X, v.Y, v.Z };
            }));

            _engine.SetValue("_rb_setAngularVelocity", new Action<double, double, double>((x, y, z) =>
            {
                var rb = GameObject?.GetComponent<Rigidbody>();
                rb?.SetAngularVelocity(new Vector3((float)x, (float)y, (float)z));
            }));

            _engine.SetValue("_rb_getAngularVelocity", new Func<double[]>(() =>
            {
                var rb = GameObject?.GetComponent<Rigidbody>();
                if (rb == null) return new double[] { 0, 0, 0 };
                var v = rb.GetAngularVelocity();
                return new double[] { v.X, v.Y, v.Z };
            }));

            // --- Move ---
            _engine.SetValue("_rb_movePosition", new Action<double, double, double>((x, y, z) =>
            {
                var rb = GameObject?.GetComponent<Rigidbody>();
                rb?.MovePosition(new Vector3((float)x, (float)y, (float)z));
            }));

            // --- Mass ---
            _engine.SetValue("_rb_getMass", new Func<double>(() =>
            {
                var rb = GameObject?.GetComponent<Rigidbody>();
                return rb?.Mass ?? 1.0;
            }));

            _engine.SetValue("_rb_setMass", new Action<double>((value) =>
            {
                var rb = GameObject?.GetComponent<Rigidbody>();
                if (rb != null) rb.Mass = (float)value;
            }));

            // --- UseGravity ---
            _engine.SetValue("_rb_getUseGravity", new Func<bool>(() =>
            {
                var rb = GameObject?.GetComponent<Rigidbody>();
                return rb?.UseGravity ?? false;
            }));

            _engine.SetValue("_rb_setUseGravity", new Action<bool>((value) =>
            {
                var rb = GameObject?.GetComponent<Rigidbody>();
                if (rb != null) rb.UseGravity = value;
            }));

            // --- IsKinematic ---
            _engine.SetValue("_rb_getIsKinematic", new Func<bool>(() =>
            {
                var rb = GameObject?.GetComponent<Rigidbody>();
                return rb?.IsKinematic ?? false;
            }));

            _engine.SetValue("_rb_setIsKinematic", new Action<bool>((value) =>
            {
                var rb = GameObject?.GetComponent<Rigidbody>();
                if (rb != null) rb.IsKinematic = value;
            }));

            // --- IsTrigger ---
            _engine.SetValue("_rb_getIsTrigger", new Func<bool>(() =>
            {
                var rb = GameObject?.GetComponent<Rigidbody>();
                return rb?.IsTrigger ?? false;
            }));

            _engine.SetValue("_rb_setIsTrigger", new Action<bool>((value) =>
            {
                var rb = GameObject?.GetComponent<Rigidbody>();
                if (rb != null) rb.IsTrigger = value;
            }));

            // --- MotionType (0=Static, 1=Kinematic, 2=Dynamic) ---
            _engine.SetValue("_rb_getMotionType", new Func<int>(() =>
            {
                var rb = GameObject?.GetComponent<Rigidbody>();
                return rb != null ? (int)rb.MotionType : 2;
            }));

            _engine.SetValue("_rb_setMotionType", new Action<int>((value) =>
            {
                var rb = GameObject?.GetComponent<Rigidbody>();
                if (rb != null) rb.MotionType = (BodyMotionType)value;
            }));

            // --- ShapeType (0=Box, 1=Sphere, 2=Capsule) ---
            _engine.SetValue("_rb_getShapeType", new Func<int>(() =>
            {
                var rb = GameObject?.GetComponent<Rigidbody>();
                return rb != null ? (int)rb.ShapeType : 0;
            }));

            _engine.SetValue("_rb_setShapeType", new Action<int>((value) =>
            {
                var rb = GameObject?.GetComponent<Rigidbody>();
                if (rb != null) rb.ShapeType = (ShapeType)value;
            }));

            // --- ShapeSize ---
            _engine.SetValue("_rb_getShapeSize", new Func<double[]>(() =>
            {
                var rb = GameObject?.GetComponent<Rigidbody>();
                if (rb == null) return new double[] { 1, 1, 1 };
                var s = rb.ShapeSize;
                return new double[] { s.X, s.Y, s.Z };
            }));

            _engine.SetValue("_rb_setShapeSize", new Action<double, double, double>((x, y, z) =>
            {
                var rb = GameObject?.GetComponent<Rigidbody>();
                if (rb != null) rb.ShapeSize = new Vector3((float)x, (float)y, (float)z);
            }));

            // --- Layer (int) ---
            _engine.SetValue("_rb_getLayer", new Func<int>(() =>
            {
                var rb = GameObject?.GetComponent<Rigidbody>();
                return rb != null ? (int)rb.Layer : 0;
            }));

            _engine.SetValue("_rb_setLayer", new Action<int>((value) =>
            {
                var rb = GameObject?.GetComponent<Rigidbody>();
                if (rb != null) rb.Layer = (PhysicsLayer)value;
            }));

            // --- Material: Friction, Restitution, Damping ---
            _engine.SetValue("_rb_getFriction", new Func<double>(() =>
            {
                var rb = GameObject?.GetComponent<Rigidbody>();
                return rb?.Friction ?? 0.5;
            }));

            _engine.SetValue("_rb_setFriction", new Action<double>((value) =>
            {
                var rb = GameObject?.GetComponent<Rigidbody>();
                if (rb != null) rb.Friction = (float)value;
            }));

            _engine.SetValue("_rb_getRestitution", new Func<double>(() =>
            {
                var rb = GameObject?.GetComponent<Rigidbody>();
                return rb?.Restitution ?? 0.0;
            }));

            _engine.SetValue("_rb_setRestitution", new Action<double>((value) =>
            {
                var rb = GameObject?.GetComponent<Rigidbody>();
                if (rb != null) rb.Restitution = (float)value;
            }));

            _engine.SetValue("_rb_getLinearDamping", new Func<double>(() =>
            {
                var rb = GameObject?.GetComponent<Rigidbody>();
                return rb?.LinearDamping ?? 0.05;
            }));

            _engine.SetValue("_rb_setLinearDamping", new Action<double>((value) =>
            {
                var rb = GameObject?.GetComponent<Rigidbody>();
                if (rb != null) rb.LinearDamping = (float)value;
            }));

            _engine.SetValue("_rb_getAngularDamping", new Func<double>(() =>
            {
                var rb = GameObject?.GetComponent<Rigidbody>();
                return rb?.AngularDamping ?? 0.05;
            }));

            _engine.SetValue("_rb_setAngularDamping", new Action<double>((value) =>
            {
                var rb = GameObject?.GetComponent<Rigidbody>();
                if (rb != null) rb.AngularDamping = (float)value;
            }));

            // --- SleepThreshold ---
            _engine.SetValue("_rb_getSleepThreshold", new Func<double>(() =>
            {
                var rb = GameObject?.GetComponent<Rigidbody>();
                return rb?.SleepThreshold ?? 0.01;
            }));

            _engine.SetValue("_rb_setSleepThreshold", new Action<double>((value) =>
            {
                var rb = GameObject?.GetComponent<Rigidbody>();
                if (rb != null) rb.SleepThreshold = (float)value;
            }));

            // --- Freeze Axes ---
            _engine.SetValue("_rb_getFreezePositionX", new Func<bool>(() => GameObject?.GetComponent<Rigidbody>()?.FreezePositionX ?? false));
            _engine.SetValue("_rb_setFreezePositionX", new Action<bool>((v) => { var rb = GameObject?.GetComponent<Rigidbody>(); if (rb != null) rb.FreezePositionX = v; }));

            _engine.SetValue("_rb_getFreezePositionY", new Func<bool>(() => GameObject?.GetComponent<Rigidbody>()?.FreezePositionY ?? false));
            _engine.SetValue("_rb_setFreezePositionY", new Action<bool>((v) => { var rb = GameObject?.GetComponent<Rigidbody>(); if (rb != null) rb.FreezePositionY = v; }));

            _engine.SetValue("_rb_getFreezePositionZ", new Func<bool>(() => GameObject?.GetComponent<Rigidbody>()?.FreezePositionZ ?? false));
            _engine.SetValue("_rb_setFreezePositionZ", new Action<bool>((v) => { var rb = GameObject?.GetComponent<Rigidbody>(); if (rb != null) rb.FreezePositionZ = v; }));

            _engine.SetValue("_rb_getFreezeRotationX", new Func<bool>(() => GameObject?.GetComponent<Rigidbody>()?.FreezeRotationX ?? false));
            _engine.SetValue("_rb_setFreezeRotationX", new Action<bool>((v) => { var rb = GameObject?.GetComponent<Rigidbody>(); if (rb != null) rb.FreezeRotationX = v; }));

            _engine.SetValue("_rb_getFreezeRotationY", new Func<bool>(() => GameObject?.GetComponent<Rigidbody>()?.FreezeRotationY ?? false));
            _engine.SetValue("_rb_setFreezeRotationY", new Action<bool>((v) => { var rb = GameObject?.GetComponent<Rigidbody>(); if (rb != null) rb.FreezeRotationY = v; }));

            _engine.SetValue("_rb_getFreezeRotationZ", new Func<bool>(() => GameObject?.GetComponent<Rigidbody>()?.FreezeRotationZ ?? false));
            _engine.SetValue("_rb_setFreezeRotationZ", new Action<bool>((v) => { var rb = GameObject?.GetComponent<Rigidbody>(); if (rb != null) rb.FreezeRotationZ = v; }));

            // --- ForceReinitialize ---
            _engine.SetValue("_rb_forceReinitialize", new Action(() =>
            {
                var rb = GameObject?.GetComponent<Rigidbody>();
                rb?.ForceReinitialize();
            }));

            // ── JS enums y Rigidbody wrapper object ──
            _engine.Execute(@"
                const MotionType = { Static: 0, Kinematic: 1, Dynamic: 2 };
                const Shape = { Box: 0, Sphere: 1, Capsule: 2 };

                const Rigidbody = {
                    // Forces
                    addForce: function(x, y, z) { _rb_addForce(x, y, z); },
                    addImpulse: function(x, y, z) { _rb_addImpulse(x, y, z); },
                    addTorque: function(x, y, z) { _rb_addTorque(x, y, z); },

                    // Velocity
                    getVelocity: function() {
                        const v = _rb_getVelocity();
                        return new Vector3(v[0], v[1], v[2]);
                    },
                    setVelocity: function(x, y, z) { _rb_setVelocity(x, y, z); },

                    getAngularVelocity: function() {
                        const v = _rb_getAngularVelocity();
                        return new Vector3(v[0], v[1], v[2]);
                    },
                    setAngularVelocity: function(x, y, z) { _rb_setAngularVelocity(x, y, z); },

                    // Move
                    movePosition: function(x, y, z) { _rb_movePosition(x, y, z); },

                    // Mass
                    getMass: function() { return _rb_getMass(); },
                    setMass: function(value) { _rb_setMass(value); },

                    // Gravity
                    getUseGravity: function() { return _rb_getUseGravity(); },
                    setUseGravity: function(value) { _rb_setUseGravity(value); },

                    // Kinematic
                    getIsKinematic: function() { return _rb_getIsKinematic(); },
                    setIsKinematic: function(value) { _rb_setIsKinematic(value); },

                    // Trigger
                    getIsTrigger: function() { return _rb_getIsTrigger(); },
                    setIsTrigger: function(value) { _rb_setIsTrigger(value); },

                    // Motion Type: MotionType.Static / Kinematic / Dynamic
                    getMotionType: function() { return _rb_getMotionType(); },
                    setMotionType: function(value) { _rb_setMotionType(value); },

                    // Shape Type: Shape.Box / Sphere / Capsule
                    getShapeType: function() { return _rb_getShapeType(); },
                    setShapeType: function(value) { _rb_setShapeType(value); },

                    // Shape Size
                    getShapeSize: function() {
                        const s = _rb_getShapeSize();
                        return new Vector3(s[0], s[1], s[2]);
                    },
                    setShapeSize: function(x, y, z) { _rb_setShapeSize(x, y, z); },

                    // Layer
                    getLayer: function() { return _rb_getLayer(); },
                    setLayer: function(value) { _rb_setLayer(value); },

                    // Material
                    getFriction: function() { return _rb_getFriction(); },
                    setFriction: function(value) { _rb_setFriction(value); },
                    getRestitution: function() { return _rb_getRestitution(); },
                    setRestitution: function(value) { _rb_setRestitution(value); },
                    getLinearDamping: function() { return _rb_getLinearDamping(); },
                    setLinearDamping: function(value) { _rb_setLinearDamping(value); },
                    getAngularDamping: function() { return _rb_getAngularDamping(); },
                    setAngularDamping: function(value) { _rb_setAngularDamping(value); },

                    // Sleep
                    getSleepThreshold: function() { return _rb_getSleepThreshold(); },
                    setSleepThreshold: function(value) { _rb_setSleepThreshold(value); },

                    // Freeze Axes
                    getFreezePositionX: function() { return _rb_getFreezePositionX(); },
                    setFreezePositionX: function(v) { _rb_setFreezePositionX(v); },
                    getFreezePositionY: function() { return _rb_getFreezePositionY(); },
                    setFreezePositionY: function(v) { _rb_setFreezePositionY(v); },
                    getFreezePositionZ: function() { return _rb_getFreezePositionZ(); },
                    setFreezePositionZ: function(v) { _rb_setFreezePositionZ(v); },
                    getFreezeRotationX: function() { return _rb_getFreezeRotationX(); },
                    setFreezeRotationX: function(v) { _rb_setFreezeRotationX(v); },
                    getFreezeRotationY: function() { return _rb_getFreezeRotationY(); },
                    setFreezeRotationY: function(v) { _rb_setFreezeRotationY(v); },
                    getFreezeRotationZ: function() { return _rb_getFreezeRotationZ(); },
                    setFreezeRotationZ: function(v) { _rb_setFreezeRotationZ(v); },

                    // Reinitialize
                    forceReinitialize: function() { _rb_forceReinitialize(); }
                };
            ");
        }

        // ─────────────────────────────────────────────────────────────
        //  Collision / Trigger Events → JS
        // ─────────────────────────────────────────────────────────────

        private void SetupCollisionEvents()
        {
            // --- C# bridges para operar sobre cualquier GameObject ---
            _engine.SetValue("_go_rb_addForce", new Action<object, double, double, double>((go, x, y, z) =>
            {
                if (go is GameObject g) g.GetComponent<Rigidbody>()?.AddForce(new Vector3((float)x, (float)y, (float)z));
            }));
            _engine.SetValue("_go_rb_addImpulse", new Action<object, double, double, double>((go, x, y, z) =>
            {
                if (go is GameObject g) g.GetComponent<Rigidbody>()?.AddImpulse(new Vector3((float)x, (float)y, (float)z));
            }));
            _engine.SetValue("_go_rb_addTorque", new Action<object, double, double, double>((go, x, y, z) =>
            {
                if (go is GameObject g) g.GetComponent<Rigidbody>()?.AddTorque(new Vector3((float)x, (float)y, (float)z));
            }));
            _engine.SetValue("_go_rb_setVelocity", new Action<object, double, double, double>((go, x, y, z) =>
            {
                if (go is GameObject g) g.GetComponent<Rigidbody>()?.SetVelocity(new Vector3((float)x, (float)y, (float)z));
            }));
            _engine.SetValue("_go_rb_getVelocity", new Func<object, double[]>((go) =>
            {
                if (go is GameObject g) { var v = g.GetComponent<Rigidbody>()?.GetVelocity() ?? Vector3.Zero; return new double[] { v.X, v.Y, v.Z }; }
                return new double[] { 0, 0, 0 };
            }));
            _engine.SetValue("_go_rb_getMass", new Func<object, double>((go) =>
            {
                if (go is GameObject g) return g.GetComponent<Rigidbody>()?.Mass ?? 1.0;
                return 1.0;
            }));
            _engine.SetValue("_go_rb_setMass", new Action<object, double>((go, v) =>
            {
                if (go is GameObject g) { var rb = g.GetComponent<Rigidbody>(); if (rb != null) rb.Mass = (float)v; }
            }));
            _engine.SetValue("_go_t_getPosition", new Func<object, double[]>((go) =>
            {
                if (go is GameObject g) { var t = g.Transform; return new double[] { t.X, t.Y, t.Z }; }
                return new double[] { 0, 0, 0 };
            }));
            _engine.SetValue("_go_t_setPosition", new Action<object, double, double, double>((go, x, y, z) =>
            {
                if (go is GameObject g) g.Transform?.SetPosition((float)x, (float)y, (float)z);
            }));

            // --- GetComponent por nombre de tipo ---
            _engine.SetValue("_go_getComponent", new Func<object, string, object>((go, typeName) =>
            {
                if (go is GameObject g)
                {
                    foreach (var comp in g.GetAllComponents())
                    {
                        if (comp.GetType().Name == typeName)
                            return comp;
                    }
                }
                return null;
            }));
            _engine.SetValue("_go_hasComponent", new Func<object, string, bool>((go, typeName) =>
            {
                if (go is GameObject g)
                {
                    foreach (var comp in g.GetAllComponents())
                    {
                        if (comp.GetType().Name == typeName)
                            return true;
                    }
                }
                return false;
            }));

            // --- JS helper que crea el contact con wrapper del otro objeto ---
            _engine.Execute(@"
                function __createContactInfo(px, py, pz, nx, ny, nz, depth, otherGO) {
                    var other = null;
                    if (otherGO) {
                        other = {
                            gameObject: otherGO,
                            name: otherGO.Name,
                            getPosition: function() {
                                var p = _go_t_getPosition(otherGO);
                                return new Vector3(p[0], p[1], p[2]);
                            },
                            setPosition: function(x, y, z) { _go_t_setPosition(otherGO, x, y, z); },
                            addForce: function(x, y, z) { _go_rb_addForce(otherGO, x, y, z); },
                            addImpulse: function(x, y, z) { _go_rb_addImpulse(otherGO, x, y, z); },
                            addTorque: function(x, y, z) { _go_rb_addTorque(otherGO, x, y, z); },
                            setVelocity: function(x, y, z) { _go_rb_setVelocity(otherGO, x, y, z); },
                            getVelocity: function() {
                                var v = _go_rb_getVelocity(otherGO);
                                return new Vector3(v[0], v[1], v[2]);
                            },
                            getMass: function() { return _go_rb_getMass(otherGO); },
                            setMass: function(v) { _go_rb_setMass(otherGO, v); },
                            getComponent: function(name) { return _go_getComponent(otherGO, name); },
                            hasComponent: function(name) { return _go_hasComponent(otherGO, name); }
                        };
                    }
                    return {
                        position: new Vector3(px, py, pz),
                        normal: new Vector3(nx, ny, nz),
                        depth: depth,
                        other: other
                    };
                }
            ");
        }

        private void TrySubscribeCollisionEvents()
        {
            if (_collisionEventsSubscribed || _engine == null) return;

            var rb = GameObject?.GetComponent<Rigidbody>();
            if (rb == null) return;

            rb.CollisionEnter += (contact) => InvokeCollisionCallback("OnCollisionEnter", contact);
            rb.CollisionStay += (contact) => InvokeCollisionCallback("OnCollisionStay", contact);
            rb.CollisionExit += (contact) => InvokeCollisionCallback("OnCollisionExit", contact);
            rb.TriggerEnter += (contact) => InvokeCollisionCallback("OnTriggerEnter", contact);
            rb.TriggerStay += (contact) => InvokeCollisionCallback("OnTriggerStay", contact);
            rb.TriggerExit += (contact) => InvokeCollisionCallback("OnTriggerExit", contact);

            _collisionEventsSubscribed = true;
        }

        private void InvokeCollisionCallback(string functionName, ContactInfo contact)
        {
            try
            {
                if (_engine == null || _engine.GetValue(functionName).IsUndefined()) return;

                // Resolver el GameObject del otro collidable
                object otherGameObject = null;
                var world = GameObject?.Scene?.PhysicsWorld;
                if (world != null)
                {
                    var handler = world.EventSystem.GetHandler(contact.OtherCollidable);
                    if (handler is Rigidbody otherRb)
                        otherGameObject = otherRb.GameObject;
                }

                var contactObj = _engine.Invoke("__createContactInfo",
                    (double)contact.ContactPosition.X, (double)contact.ContactPosition.Y, (double)contact.ContactPosition.Z,
                    (double)contact.ContactNormal.X, (double)contact.ContactNormal.Y, (double)contact.ContactNormal.Z,
                    (double)contact.PenetrationDepth,
                    otherGameObject);

                _engine.Invoke(functionName, contactObj);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en {functionName}: {ex.Message}");
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  Physics Raycast API → JS
        // ─────────────────────────────────────────────────────────────

        private void SetupPhysicsRaycast()
        {
            // --- Raycast (closest hit) ---
            _engine.SetValue("_physics_raycast", new Func<double, double, double, double, double, double, double, double, double[]>(
                (ox, oy, oz, dx, dy, dz, maxDist, layerMask) =>
                {
                    var world = GameObject?.Scene?.PhysicsWorld;
                    if (world == null) return null;

                    var origin = new System.Numerics.Vector3((float)ox, (float)oy, (float)oz);
                    var direction = new System.Numerics.Vector3((float)dx, (float)dy, (float)dz);

                    if (PhysicsRaycast.Raycast(world, origin, direction, (float)maxDist, out var hit, (PhysicsLayer)(uint)layerMask))
                    {
                        return new double[]
                        {
                            hit.Point.X, hit.Point.Y, hit.Point.Z,
                            hit.Normal.X, hit.Normal.Y, hit.Normal.Z,
                            hit.Distance,
                            (double)(uint)hit.Layer
                        };
                    }
                    return null;
                }));

            // --- RaycastAll (count + query pattern) ---
            _engine.SetValue("_physics_raycastAll", new Func<double, double, double, double, double, double, double, double, int>(
                (ox, oy, oz, dx, dy, dz, maxDist, layerMask) =>
                {
                    _raycastAllResults.Clear();
                    var world = GameObject?.Scene?.PhysicsWorld;
                    if (world == null) return 0;

                    var origin = new System.Numerics.Vector3((float)ox, (float)oy, (float)oz);
                    var direction = new System.Numerics.Vector3((float)dx, (float)dy, (float)dz);

                    _raycastAllResults = PhysicsRaycast.RaycastAll(world, origin, direction, (float)maxDist, (PhysicsLayer)(uint)layerMask);
                    return _raycastAllResults.Count;
                }));

            _engine.SetValue("_physics_raycastAll_get", new Func<int, double[]>(
                (index) =>
                {
                    if (index < 0 || index >= _raycastAllResults.Count) return null;
                    var hit = _raycastAllResults[index];
                    return new double[]
                    {
                        hit.Point.X, hit.Point.Y, hit.Point.Z,
                        hit.Normal.X, hit.Normal.Y, hit.Normal.Z,
                        hit.Distance,
                        (double)(uint)hit.Layer
                    };
                }));

            // --- Linecast ---
            _engine.SetValue("_physics_linecast", new Func<double, double, double, double, double, double, double, double[]>(
                (sx, sy, sz, ex, ey, ez, layerMask) =>
                {
                    var world = GameObject?.Scene?.PhysicsWorld;
                    if (world == null) return null;

                    var start = new System.Numerics.Vector3((float)sx, (float)sy, (float)sz);
                    var end = new System.Numerics.Vector3((float)ex, (float)ey, (float)ez);

                    if (PhysicsRaycast.Linecast(world, start, end, out var hit, (PhysicsLayer)(uint)layerMask))
                    {
                        return new double[]
                        {
                            hit.Point.X, hit.Point.Y, hit.Point.Z,
                            hit.Normal.X, hit.Normal.Y, hit.Normal.Z,
                            hit.Distance,
                            (double)(uint)hit.Layer
                        };
                    }
                    return null;
                }));

            // --- Check (boolean) ---
            _engine.SetValue("_physics_check", new Func<double, double, double, double, double, double, double, double, bool>(
                (ox, oy, oz, dx, dy, dz, maxDist, layerMask) =>
                {
                    var world = GameObject?.Scene?.PhysicsWorld;
                    if (world == null) return false;

                    var origin = new System.Numerics.Vector3((float)ox, (float)oy, (float)oz);
                    var direction = new System.Numerics.Vector3((float)dx, (float)dy, (float)dz);

                    return PhysicsRaycast.Check(world, origin, direction, (float)maxDist, (PhysicsLayer)(uint)layerMask);
                }));

            // --- JS Physics wrapper ---
            _engine.Execute(@"
                const Physics = {
                    Layer: {
                        None: 0,
                        Default: 1,
                        Static: 2,
                        Player: 4,
                        Enemy: 8,
                        Projectile: 16,
                        Trigger: 32,
                        Environment: 64,
                        UI: 128,
                        All: 4294967295
                    },

                    raycast: function(origin, direction, maxDistance, layerMask) {
                        if (maxDistance === undefined) maxDistance = 99999;
                        if (layerMask === undefined) layerMask = 4294967295;
                        const r = _physics_raycast(origin.x, origin.y, origin.z, direction.x, direction.y, direction.z, maxDistance, layerMask);
                        if (!r) return null;
                        return {
                            hit: true,
                            point: new Vector3(r[0], r[1], r[2]),
                            normal: new Vector3(r[3], r[4], r[5]),
                            distance: r[6],
                            layer: r[7]
                        };
                    },

                    raycastAll: function(origin, direction, maxDistance, layerMask) {
                        if (maxDistance === undefined) maxDistance = 99999;
                        if (layerMask === undefined) layerMask = 4294967295;
                        const count = _physics_raycastAll(origin.x, origin.y, origin.z, direction.x, direction.y, direction.z, maxDistance, layerMask);
                        const hits = [];
                        for (let i = 0; i < count; i++) {
                            const r = _physics_raycastAll_get(i);
                            hits.push({
                                hit: true,
                                point: new Vector3(r[0], r[1], r[2]),
                                normal: new Vector3(r[3], r[4], r[5]),
                                distance: r[6],
                                layer: r[7]
                            });
                        }
                        return hits;
                    },

                    linecast: function(start, end, layerMask) {
                        if (layerMask === undefined) layerMask = 4294967295;
                        const r = _physics_linecast(start.x, start.y, start.z, end.x, end.y, end.z, layerMask);
                        if (!r) return null;
                        return {
                            hit: true,
                            point: new Vector3(r[0], r[1], r[2]),
                            normal: new Vector3(r[3], r[4], r[5]),
                            distance: r[6],
                            layer: r[7]
                        };
                    },

                    check: function(origin, direction, maxDistance, layerMask) {
                        if (maxDistance === undefined) maxDistance = 99999;
                        if (layerMask === undefined) layerMask = 4294967295;
                        return _physics_check(origin.x, origin.y, origin.z, direction.x, direction.y, direction.z, maxDistance, layerMask);
                    }
                };
            ");
        }

        public override void Update(float deltaTime)
        {
            TrySubscribeCollisionEvents();

            try
            {
                if (!_engine.GetValue("OnTick").IsUndefined())
                {
                    _engine.Invoke("OnTick", deltaTime);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error en OnTick:");
                Console.WriteLine(ex.Message);
            }
        }
    }
}