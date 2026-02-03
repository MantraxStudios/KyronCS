using System;
using System.IO;
using Jint;

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
                    .MaxStatements(1000)
                    .TimeoutInterval(TimeSpan.FromSeconds(2));
            });

            _engine.SetValue("console", new
            {
                log = new Action<object, object>((a, b) =>
                {
                    Console.WriteLine($"{a} {b}");
                })
            });
            
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
            }
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
