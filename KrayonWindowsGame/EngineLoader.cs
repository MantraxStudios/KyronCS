using KrayonCore;
using KrayonCore.Core.Attributes;
using KrayonCore.GraphicsData;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System;
using System.Text;
using System.Text.Json;
using Vector2 = System.Numerics.Vector2;

namespace KrayonEditor.UI
{
    internal static class EngineLoader
    {
        private static GraphicsEngine? _engine;

        public static void Run()
        {
            _engine = new GraphicsEngine();
            _engine.OnLoad += OnLoadEngine;
            _engine.CreateWindow(WindowConfig.Width, WindowConfig.Height, "Krayon Game");
            _engine.Run();
        }

        public static void OnLoadEngine()
        {
            string defaultScene = GetDefaultScene();

            if (string.IsNullOrEmpty(defaultScene))
                defaultScene = AssetManager.DefaultScene;

            Console.WriteLine($"[EngineLoader] Loading default scene: {defaultScene}");
            SceneManager.LoadScene(defaultScene);
        }

        private static string GetDefaultScene()
        {
            try
            {
                byte[] bytes = AssetManager.GetBytes("Engine.DefaultScene");
                if (bytes == null || bytes.Length == 0)
                    return AssetManager.DefaultScene;

                if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                    bytes = bytes[3..];

                string json = Encoding.UTF8.GetString(bytes);
                var engineData = JsonSerializer.Deserialize<EngineData>(json);

                if (engineData != null && !string.IsNullOrEmpty(engineData.DefaultScene))
                    return engineData.DefaultScene;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EngineLoader] Error loading default scene: {ex.Message}");
            }

            return AssetManager.DefaultScene;
        }
    }
}