using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;

namespace KrayonCore.Core.Input
{
    public class InputSystem
    {
        private static InputSystem? _instance;

        // ── Teclado ───────────────────────────────────────────────────────────
        private readonly HashSet<Keys> _keysDown = new();
        private readonly HashSet<Keys> _keysPressed = new();   // sólo el frame que bajó
        private readonly HashSet<Keys> _keysReleased = new();   // sólo el frame que subió

        // ── Mouse ─────────────────────────────────────────────────────────────
        private readonly HashSet<MouseButton> _mouseDown = new();
        private readonly HashSet<MouseButton> _mousePressed = new();
        private readonly HashSet<MouseButton> _mouseReleased = new();

        private Vector2 _mousePosition = Vector2.Zero;
        private Vector2 _mouseDelta = Vector2.Zero;
        private Vector2 _mouseScroll = Vector2.Zero;
        private Vector2 _mouseScrollDelta = Vector2.Zero;

        // ── Misc ──────────────────────────────────────────────────────────────
        private string _textInput = string.Empty;
        private readonly List<string> _droppedFiles = new();

        public event Action<string>? TextInputReceived;
        public event Action<string[]>? FilesDropped;

        // ─────────────────────────────────────────────────────────────────────
        internal InputSystem(GameWindow window)
        {
            _instance = this;

            // Teclado
            window.KeyDown += e =>
            {
                if (_keysDown.Add(e.Key))
                    _keysPressed.Add(e.Key);
            };

            window.KeyUp += e =>
            {
                if (_keysDown.Remove(e.Key))
                    _keysReleased.Add(e.Key);
            };

            window.TextInput += e =>
            {
                _textInput = ((char)e.Unicode).ToString();
                TextInputReceived?.Invoke(_textInput);
            };

            // Mouse botones
            window.MouseDown += e =>
            {
                if (_mouseDown.Add(e.Button))
                    _mousePressed.Add(e.Button);
            };

            window.MouseUp += e =>
            {
                if (_mouseDown.Remove(e.Button))
                    _mouseReleased.Add(e.Button);
            };

            // Mouse movimiento
            window.MouseMove += e =>
            {
                _mouseDelta = e.Delta;
                _mousePosition = e.Position;
            };

            // Mouse scroll
            window.MouseWheel += e =>
            {
                _mouseScrollDelta = e.Offset;
                _mouseScroll += e.Offset;
            };

            // Archivos arrastrados
            window.FileDrop += e =>
            {
                _droppedFiles.Clear();
                _droppedFiles.AddRange(e.FileNames);
                FilesDropped?.Invoke(e.FileNames);
            };
        }

        // ─── API pública ──────────────────────────────────────────────────────

        /// <summary>Tecla mantenida presionada.</summary>
        public static bool GetKeyDown(Keys key) => _instance?._keysDown.Contains(key) ?? false;

        /// <summary>Tecla NO presionada.</summary>
        public static bool GetKeyUp(Keys key) => !(_instance?._keysDown.Contains(key) ?? false);

        /// <summary>Tecla recién presionada (solo el primer frame).</summary>
        public static bool GetKeyPressed(Keys key) => _instance?._keysPressed.Contains(key) ?? false;

        /// <summary>Tecla recién soltada (solo el primer frame).</summary>
        public static bool GetKeyReleased(Keys key) => _instance?._keysReleased.Contains(key) ?? false;

        /// <summary>Botón de mouse mantenido.</summary>
        public static bool GetMouseButtonDown(MouseButton button) => _instance?._mouseDown.Contains(button) ?? false;

        /// <summary>Botón de mouse NO presionado.</summary>
        public static bool GetMouseButtonUp(MouseButton button) => !(_instance?._mouseDown.Contains(button) ?? false);

        /// <summary>Botón de mouse recién presionado (solo el primer frame).</summary>
        public static bool GetMouseButtonPressed(MouseButton button) => _instance?._mousePressed.Contains(button) ?? false;

        /// <summary>Botón de mouse recién soltado (solo el primer frame).</summary>
        public static bool GetMouseButtonReleased(MouseButton button) => _instance?._mouseReleased.Contains(button) ?? false;

        public static Vector2 GetMousePosition() => _instance?._mousePosition ?? Vector2.Zero;
        public static Vector2 GetMouseDelta() => _instance?._mouseDelta ?? Vector2.Zero;
        public static Vector2 GetMouseScroll() => _instance?._mouseScroll ?? Vector2.Zero;
        public static Vector2 GetMouseScrollDelta() => _instance?._mouseScrollDelta ?? Vector2.Zero;

        public static string GetTextInput() => _instance?._textInput ?? string.Empty;
        public static string[] GetDroppedFiles() => _instance?._droppedFiles.ToArray() ?? Array.Empty<string>();
        public static bool HasDroppedFiles() => _instance?._droppedFiles.Count > 0;

        // ─── Llamar al FINAL de cada frame (desde el game loop) ───────────────
        /// <summary>
        /// Limpia los estados de "pressed/released" y los deltas que solo
        /// deben vivir un frame. Llamar una vez por frame, al final del Update.
        /// </summary>
        internal void ClearFrameData()
        {
            _keysPressed.Clear();
            _keysReleased.Clear();
            _mousePressed.Clear();
            _mouseReleased.Clear();
            _mouseDelta = Vector2.Zero;
            _mouseScrollDelta = Vector2.Zero;
            _textInput = string.Empty;
            _droppedFiles.Clear();
        }
    }
}