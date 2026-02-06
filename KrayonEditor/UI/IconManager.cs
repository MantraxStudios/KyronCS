using System;
using System.Collections.Generic;
using System.IO;
using KrayonCore;

namespace KrayonEditor.UI
{
    public static class IconManager
    {
        private static Dictionary<string, TextureLoader> _icons = new Dictionary<string, TextureLoader>();
        private static bool _initialized = false;

        public static void Initialize()
        {
            if (_initialized)
                return;

            LoadIcons();
            _initialized = true;
        }

        private static void LoadIcons()
        {
            string iconsPath = "Icons/";

            // Cargar iconos de transformación
            LoadIcon("move", Path.Combine(iconsPath, "move.png"));
            LoadIcon("rotate", Path.Combine(iconsPath, "rotate.png"));
            LoadIcon("scale", Path.Combine(iconsPath, "scale.png"));

            // Cargar iconos de reproducción
            LoadIcon("play", Path.Combine(iconsPath, "play.png"));
            LoadIcon("pause", Path.Combine(iconsPath, "pause.png"));
            LoadIcon("stop", Path.Combine(iconsPath, "stop.png"));

            // Cargar iconos de vista
            LoadIcon("camera", Path.Combine(iconsPath, "camera.png"));
            LoadIcon("wireframe", Path.Combine(iconsPath, "wireframe.png"));

            // Puedes agregar más iconos aquí
            Console.WriteLine($"IconManager: {_icons.Count} icons loaded");
        }

        private static void LoadIcon(string name, string relativePath)
        {
            try
            {
                TextureLoader texture = TextureLoader.FromRelativePath(
                    name,
                    relativePath,
                    generateMipmaps: false,
                    flip: false
                );

                _icons[name] = texture;

                Console.WriteLine($"IconManager: Registered icon '{name}' from '{relativePath}'");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"IconManager: Error loading icon '{name}': {ex.Message}");
            }
        }

        public static IntPtr GetIcon(string name)
        {
            if (!_initialized)
            {
                Console.WriteLine("IconManager: Not initialized! Call Initialize() first.");
                return IntPtr.Zero;
            }

            if (_icons.TryGetValue(name, out TextureLoader texture))
            {
                // Asegurarse de que la textura esté cargada
                if (!texture.IsLoaded)
                {
                    texture.Load();
                }

                // Retornar el TextureId como IntPtr para ImGui
                if (texture.IsLoaded)
                {
                    return (IntPtr)texture.TextureId;
                }
            }

            Console.WriteLine($"IconManager: Icon '{name}' not found or not loaded");
            return IntPtr.Zero;
        }

        public static bool HasIcon(string name)
        {
            return _icons.ContainsKey(name);
        }

        public static void EnsureIconsLoaded()
        {
            // Forzar la carga de todos los iconos
            foreach (var icon in _icons.Values)
            {
                if (!icon.IsLoaded)
                {
                    icon.Load();
                }
            }
        }

        public static void Dispose()
        {
            foreach (var icon in _icons.Values)
            {
                icon.Dispose();
            }

            _icons.Clear();
            _initialized = false;

            Console.WriteLine("IconManager: All icons disposed");
        }
    }
}