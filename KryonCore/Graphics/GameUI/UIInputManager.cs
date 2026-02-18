using KrayonCore.Graphics.GameUI;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace KrayonCore.UI
{
    public static class UIInputManager
    {
        public static Vector2 MouseScreenPos { get; private set; }
        public static bool LeftDown { get; private set; }
        public static bool IsInsideViewport { get; private set; } = true;

        private static readonly Queue<char> _charQueue = new();
        private static readonly Queue<Keys> _keyQueue = new();
        private static bool _prevLeftDown = false;

        // ── Mouse ─────────────────────────────────────────────────────────

        public static void SetMousePos(float x, float y)
        {
            MouseScreenPos = new Vector2(x, y);
            IsInsideViewport = true;
        }

        public static void SetLeftButton(bool down) => LeftDown = down;

        public static void SetMousePosFromViewport(
            Vector2 globalMousePos,
            Vector2 viewportOrigin,
            Vector2 viewportSize)
        {
            float relX = globalMousePos.X - viewportOrigin.X;
            float relY = globalMousePos.Y - viewportOrigin.Y;

            IsInsideViewport = relX >= 0 && relX <= viewportSize.X
                             && relY >= 0 && relY <= viewportSize.Y;

            MouseScreenPos = new Vector2(relX, relY);
        }

        // ── Keyboard events (conectar en InternalLoad) ────────────────────

        /// <summary>
        /// Conectar al evento TextInput de la ventana:
        ///   _window.TextInput += e => UIInputManager.AddTypedChar(e.AsString);
        /// </summary>
        public static void AddTypedChar(string text)
        {
            foreach (char c in text)
                _charQueue.Enqueue(c);
        }

        /// <summary>
        /// Conectar al evento KeyDown de la ventana:
        ///   _window.KeyDown += e => UIInputManager.AddKeyDown(e.Key);
        /// </summary>
        public static void AddKeyDown(Keys key) => _keyQueue.Enqueue(key);

        // ── SetKeyboardState ya no es necesario, se deja vacío por compatibilidad ──
        public static void SetKeyboardState(OpenTK.Windowing.GraphicsLibraryFramework.KeyboardState state) { }

        // ── Update ────────────────────────────────────────────────────────

        public static void Update(UICanvas canvas)
        {
            Vector2 refPos = IsInsideViewport
                ? canvas.ScreenToReference(MouseScreenPos)
                : new Vector2(-9999f, -9999f);

            bool effectiveLeft = IsInsideViewport && LeftDown;
            bool leftJustPressed = IsInsideViewport && LeftDown && !_prevLeftDown;

            foreach (var e in canvas.Elements)
            {
                if (!e.Visible || !e.Enabled) continue;

                switch (e)
                {
                    case UIButton btn:
                        btn.UpdateInput(refPos, effectiveLeft);
                        break;
                    case UISlider sld:
                        sld.UpdateInput(refPos, effectiveLeft);
                        break;
                    case UIInputText txt:
                        txt.UpdateInput(refPos, effectiveLeft, leftJustPressed);
                        break;
                }
            }

            DispatchKeyboard(canvas);
        }

        public static void UpdateAll()
        {
            foreach (var canvas in UICanvasManager.All())
                if (canvas.Visible) Update(canvas);

            _charQueue.Clear();
            _keyQueue.Clear();
            _prevLeftDown = LeftDown;
        }

        // ── Dispatch ──────────────────────────────────────────────────────

        private static void DispatchKeyboard(UICanvas canvas)
        {
            var focused = canvas.Elements
                .OfType<UIInputText>()
                .FirstOrDefault(t => t.IsFocused && t.Visible && t.Enabled);

            if (focused is null) return;

            // Caracteres imprimibles + espacio desde TextInput
            foreach (char c in _charQueue)
            {
                switch (c)
                {
                    case '\b': focused.ProcessBackspace(); break;
                    case '\r':
                    case '\n': focused.ProcessEnter(); break;
                    default:
                        if (!char.IsControl(c)) focused.ProcessChar(c);
                        break;
                }
            }

            // Teclas especiales desde KeyDown event
            foreach (Keys key in _keyQueue)
            {
                switch (key)
                {
                    case Keys.Backspace: focused.ProcessBackspace(); break;
                    case Keys.Delete: focused.ProcessDelete(); break;
                    case Keys.Enter:
                    case Keys.KeyPadEnter: focused.ProcessEnter(); break;
                    case Keys.Home: focused.ProcessHome(); break;
                    case Keys.End: focused.ProcessEnd(); break;
                    case Keys.Left: focused.ProcessLeft(); break;
                    case Keys.Right: focused.ProcessRight(); break;
                    case Keys.Escape: focused.Unfocus(); break;
                }
            }
        }
    }
}