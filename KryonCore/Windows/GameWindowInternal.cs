using KrayonCore.Core;
using KrayonGraphics.GraphicEngine.Abstraction;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using System;
using System.Diagnostics;

namespace KrayonCore.GraphicsData
{
    internal class GameWindowInternal : GameWindow, IRender
    {
        private readonly GraphicsEngine _engine;

        private readonly Stopwatch _renderTimer = Stopwatch.StartNew();

        public Action OnLoadRender { get; set; }
        public Action OnRederFrame { get; set; }
        public Action OnUpdateRender { get; set; }
        public Action OnUnloadRender { get; set; }

        public Action<KeyboardKeyEventArgs> OnKeyDownEvent { get; set; }
        public Action<KeyboardKeyEventArgs> OnKeyUpEvent { get; set; }

        public Action<MouseButtonEventArgs> OnMouseDownEvent { get; set; }
        public Action<MouseButtonEventArgs> OnMouseUpEvent { get; set; }
        public Action<MouseMoveEventArgs> OnMouseMoveEvent { get; set; }
        public Action<MouseWheelEventArgs> OnMouseWheelEvent { get; set; }

        public Action<TextInputEventArgs> OnTextInputEvent { get; set; }
        public Action<FileDropEventArgs> OnDropFileEvent { get; set; }
        public Action<ResizeEventArgs> OnResizeEvent { get; set; }

        public GameWindowInternal(int width, int height, string title, GraphicsEngine engine)
            : base(
                new GameWindowSettings
                {
                    UpdateFrequency = 120.0
                },
                new NativeWindowSettings
                {
                    ClientSize = new Vector2i(width, height),
                    Title = title,
                    APIVersion = new Version(4, 5),
                    Profile = ContextProfile.Core,
                    Flags = ContextFlags.ForwardCompatible,
                    NumberOfSamples = 0,
                    WindowState = WindowState.Maximized
                })
        {
            _engine = engine;

            KeyDown += e => OnKeyDownEvent?.Invoke(e);
            KeyUp += e => OnKeyUpEvent?.Invoke(e);
            MouseDown += e => OnMouseDownEvent?.Invoke(e);
            MouseUp += e => OnMouseUpEvent?.Invoke(e);
            MouseMove += e => OnMouseMoveEvent?.Invoke(e);
            MouseWheel += e => OnMouseWheelEvent?.Invoke(e);
            FileDrop += e => OnDropFileEvent?.Invoke(e);

            TextInput += e =>
            {
                _engine.InternalTextInput(e);
                OnTextInputEvent?.Invoke(e);
            };

            FileDrop += e =>
            {
                _engine.InternalFileDrop(e);
                OnDropFileEvent?.Invoke(e);
            };
        }

        protected override void OnLoad()
        {
            base.OnLoad();
            VSync = VSyncMode.Off;

            GL.ClearColor(WindowConfig.ColorClear.X, WindowConfig.ColorClear.Y, WindowConfig.ColorClear.Z, WindowConfig.ColorClear.W);
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Less);
            GL.DepthMask(true);
            GL.ClearDepth(1.0);
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);
            GL.FrontFace(FrontFaceDirection.Ccw);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            _engine.InternalLoad();
            OnStartRender();

            Console.WriteLine($"OpenGL: {GL.GetString(StringName.Version)}");
            Console.WriteLine($"GPU:    {GL.GetString(StringName.Renderer)}");
            Console.WriteLine($"Depth Test: {GL.IsEnabled(EnableCap.DepthTest)}");
        }

        protected override void OnUpdateFrame(FrameEventArgs args)
        {
            base.OnUpdateFrame(args);

            float delta = (float)_renderTimer.Elapsed.TotalSeconds;
            _renderTimer.Restart();

            TimerData.DeltaTime = delta;
            TimerData.DeltaTimeFixed = (float)(1.0 / 60.0);
            TimerData.Time += delta;

            _engine.InternalUpdate(delta);
            OnUpdate();
        }

        protected override void OnRenderFrame(FrameEventArgs args)
        {
            base.OnRenderFrame(args);

            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            _engine.InternalRender(TimerData.DeltaTime);
            OnRender();
            SwapBuffers();
        }

        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);
            GL.Viewport(0, 0, e.Width, e.Height);
            _engine.InternalResize(e.Width, e.Height);
            TriggerResize(e);
        }

        protected override void OnUnload()
        {
            base.OnUnload();
            _engine.InternalClose();
            OnUnloadRender?.Invoke();
        }

        public void OnStartRender() => OnLoadRender?.Invoke();
        public void OnRender() => OnRederFrame?.Invoke();
        public void OnUpdate() => OnUpdateRender?.Invoke();
        public void OnInputText(TextInputEventArgs e) => OnTextInputEvent?.Invoke(e);
        public void OnDropFile(FileDropEventArgs e) => OnDropFileEvent?.Invoke(e);
        public void OnKeyDown(KeyboardKeyEventArgs e) => OnKeyDownEvent?.Invoke(e);
        public void OnKeyUp(KeyboardKeyEventArgs e) => OnKeyUpEvent?.Invoke(e);
        public void OnMouseDown(MouseButtonEventArgs e) => OnMouseDownEvent?.Invoke(e);
        public void OnMouseUp(MouseButtonEventArgs e) => OnMouseUpEvent?.Invoke(e);
        public void OnMouseMove(MouseMoveEventArgs e) => OnMouseMoveEvent?.Invoke(e);
        public void OnMouseWheel(MouseWheelEventArgs e) => OnMouseWheelEvent?.Invoke(e);
        public void TriggerResize(ResizeEventArgs e) => OnResizeEvent?.Invoke(e);
    }
}