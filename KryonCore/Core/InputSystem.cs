using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTK.Windowing.Common;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;

namespace KrayonCore.Core.Input
{
    public class InputSystem
    {
        private static InputSystem? _instance;

        private KeyboardState _currentKeyboard;
        private KeyboardState _previousKeyboard;

        private MouseState _currentMouse;
        private MouseState _previousMouse;

        private string _textInput = string.Empty;
        private List<string> _droppedFiles = new List<string>();

        public event Action<string>? TextInputReceived;
        public event Action<string[]>? FilesDropped;

        internal InputSystem()
        {
            _instance = this;
        }

        public static bool GetKeyDown(Keys key) => _instance?._currentKeyboard.IsKeyDown(key) ?? false;
        public static bool GetKeyUp(Keys key) => !(_instance?._currentKeyboard.IsKeyDown(key) ?? false);
        public static bool GetKeyPressed(Keys key) => (_instance?._currentKeyboard.IsKeyDown(key) ?? false) && !(_instance?._previousKeyboard.IsKeyDown(key) ?? false);
        public static bool GetKeyReleased(Keys key) => !(_instance?._currentKeyboard.IsKeyDown(key) ?? false) && (_instance?._previousKeyboard.IsKeyDown(key) ?? false);

        public static bool GetMouseButtonDown(MouseButton button) => _instance?._currentMouse.IsButtonDown(button) ?? false;
        public static bool GetMouseButtonUp(MouseButton button) => !(_instance?._currentMouse.IsButtonDown(button) ?? false);
        public static bool GetMouseButtonPressed(MouseButton button) => (_instance?._currentMouse.IsButtonDown(button) ?? false) && !(_instance?._previousMouse.IsButtonDown(button) ?? false);
        public static bool GetMouseButtonReleased(MouseButton button) => !(_instance?._currentMouse.IsButtonDown(button) ?? false) && (_instance?._previousMouse.IsButtonDown(button) ?? false);

        public static Vector2 GetMousePosition() => _instance?._currentMouse.Position ?? Vector2.Zero;
        public static Vector2 GetMouseDelta() => _instance?._currentMouse.Delta ?? Vector2.Zero;
        public static Vector2 GetMouseScroll() => _instance?._currentMouse.Scroll ?? Vector2.Zero;
        public static Vector2 GetMouseScrollDelta() => (_instance?._currentMouse.Scroll ?? Vector2.Zero) - (_instance?._previousMouse.Scroll ?? Vector2.Zero);

        public static string GetTextInput() => _instance?._textInput ?? string.Empty;

        public static string[] GetDroppedFiles() => _instance?._droppedFiles.ToArray() ?? Array.Empty<string>();
        public static bool HasDroppedFiles() => _instance?._droppedFiles.Count > 0;

        internal void UpdateStates(KeyboardState keyboard, MouseState mouse)
        {
            _previousKeyboard = _currentKeyboard;
            _previousMouse = _currentMouse;

            _currentKeyboard = keyboard;
            _currentMouse = mouse;
        }

        internal void OnTextInput(TextInputEventArgs e)
        {
            _textInput = ((char)e.Unicode).ToString();
            TextInputReceived?.Invoke(_textInput);
        }

        internal void OnFileDrop(string[] files)
        {
            _droppedFiles.Clear();
            _droppedFiles.AddRange(files);
            FilesDropped?.Invoke(files);
        }

        internal void ClearFrameData()
        {
            _textInput = string.Empty;
            _droppedFiles.Clear();
        }
    }
}