using System;
using System.IO;
using Jint;
using OpenTK.Mathematics;

namespace KrayonCore
{
    public class GameScript : Component
    {
        [ToStorage] public string ScriptPath = "scripts/game.js";
        private Engine _engine;

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

            // Cargar y ejecutar el script
            string path = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                ScriptPath
            );

            try
            {
                _engine.Execute(File.ReadAllText(path));

                if (!_engine.GetValue("OnStart").IsUndefined())
                {
                    _engine.Invoke("OnStart");
                }
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
                // Constantes
                PI = Math.PI,
                E = Math.E,

                // Funciones básicas
                abs = new Func<double, double>(Math.Abs),
                sqrt = new Func<double, double>(Math.Sqrt),
                pow = new Func<double, double, double>(Math.Pow),

                // Trigonometría
                sin = new Func<double, double>(Math.Sin),
                cos = new Func<double, double>(Math.Cos),
                tan = new Func<double, double>(Math.Tan),
                asin = new Func<double, double>(Math.Asin),
                acos = new Func<double, double>(Math.Acos),
                atan = new Func<double, double>(Math.Atan),
                atan2 = new Func<double, double, double>(Math.Atan2),

                // Redondeo
                floor = new Func<double, double>(Math.Floor),
                ceil = new Func<double, double>(Math.Ceiling),
                round = new Func<double, double>(Math.Round),

                // Min/Max
                min = new Func<double, double, double>(Math.Min),
                max = new Func<double, double, double>(Math.Max),

                // Clamp personalizado
                clamp = new Func<double, double, double, double>((value, min, max) =>
                    Math.Max(min, Math.Min(max, value))),

                // Lerp personalizado
                lerp = new Func<double, double, double, double>((a, b, t) =>
                    a + (b - a) * t),

                // Conversión de ángulos
                degToRad = new Func<double, double>(deg => deg * Math.PI / 180.0),
                radToDeg = new Func<double, double>(rad => rad * 180.0 / Math.PI),

                // Random
                random = new Func<double>(() => Random.Shared.NextDouble())
            });

            // Clase Vector3 para JavaScript
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
            // Funciones separadas para cada tipo de componente
            _engine.SetValue("GetTransform", new Func<Transform>(() =>
                GameObject?.GetComponent<Transform>()));

            _engine.SetValue("GetRigidbody", new Func<Rigidbody>(() =>
                GameObject?.GetComponent<Rigidbody>()));

            _engine.SetValue("GetMeshRenderer", new Func<MeshRenderer>(() =>
                GameObject?.GetComponent<MeshRenderer>()));

            // Transform functions
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

            // Rigidbody functions
            _engine.SetValue("CreateVector3", new Func<float, float, float, Vector3>((x, y, z) =>
                new Vector3(x, y, z)));

            _engine.Execute(@"
                const Rigidbody = {
                    getVelocity: function() {
                        const rb = GetRigidbody();
                        if (!rb) return Vector3.zero();
                        const v = rb.Velocity;
                        return new Vector3(v.X, v.Y, v.Z);
                    },
                    setVelocity: function(x, y, z) {
                        const rb = GetRigidbody();
                        if (rb) rb.Velocity = CreateVector3(x, y, z);
                    },
                    addForce: function(x, y, z) {
                        const rb = GetRigidbody();
                        if (rb) rb.AddForce(x, y, z);
                    },
                    getUseGravity: function() {
                        const rb = GetRigidbody();
                        return rb ? rb.UseGravity : false;
                    },
                    setUseGravity: function(value) {
                        const rb = GetRigidbody();
                        if (rb) rb.UseGravity = value;
                    },
                    getMass: function() {
                        const rb = GetRigidbody();
                        return rb ? rb.Mass : 1.0;
                    },
                    setMass: function(value) {
                        const rb = GetRigidbody();
                        if (rb) rb.Mass = value;
                    }
                };
            ");
        }

        public override void Update(float deltaTime)
        {
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