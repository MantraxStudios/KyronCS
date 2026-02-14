using Jint;
using KrayonCore;
using KrayonCore.Core.Attributes;
using KrayonCore.Core.Components;
using KrayonCore.Scripting;
using KrayonEditor.UI;

namespace KrayonEditor
{
    internal static class Program
    {
        static void Main(string[] args)
        {
            if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
            {
                AssetManager.TotalBase = args[0];
            }

            ScriptWatcher _scriptWatcher;

            _scriptWatcher = new ScriptWatcher();

            _scriptWatcher.OnBuildSuccess += () =>
            {
                Console.WriteLine("[Engine] Scripts recompilados, recargando DLL...");

                CSharpScriptManager.Instance.Reload();

                Console.WriteLine("[Engine] Reiniciando scripts en la escena...");

                foreach (GameObject item in SceneManager.ActiveScene.GetAllGameObjects())
                {
                    if (item.HasComponent<CSharpLogic>())
                    {
                        item.GetComponent<CSharpLogic>().RestartScript();
                    }
                }

                Console.WriteLine("[Engine] Hot-reload completado!");
            };

            _scriptWatcher.OnBuildFailed += (error) =>
            {
                Console.WriteLine($"[Engine] Error en scripts:\n{error}");
            };

            _scriptWatcher.Start();

            EngineEditor.Run();

            _scriptWatcher?.Dispose();
        }
    }
}