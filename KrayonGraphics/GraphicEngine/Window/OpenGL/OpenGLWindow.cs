using KrayonGraphics.GraphicEngine.Abstraction;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Graphics.OpenGL4;
using System;

namespace KrayonGraphics.GraphicEngine.Window.OpenGL
{
    public class OpenGLWindow : GameWindow, IRender
    {
        public ISettings EngineSettings { get; private set; }

        // ── IRender: callbacks ──────────────────────────────────────────────
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

        // ── Constructor ─────────────────────────────────────────────────────
        public OpenGLWindow(int width, int height, string title)
            : base(
                new GameWindowSettings { UpdateFrequency = 0.0 },
                new NativeWindowSettings
                {
                    ClientSize = new Vector2i(width, height),
                    Title = title,
                    APIVersion = new Version(4, 5),
                    Profile = ContextProfile.Core,
                    Flags = ContextFlags.ForwardCompatible,
                    NumberOfSamples = 0
                })
        {
            KeyDown += e => OnKeyDownEvent?.Invoke(e);
            KeyUp += e => OnKeyUpEvent?.Invoke(e);
            MouseDown += e => OnMouseDownEvent?.Invoke(e);
            MouseUp += e => OnMouseUpEvent?.Invoke(e);
            MouseMove += e => OnMouseMoveEvent?.Invoke(e);
            MouseWheel += e => OnMouseWheelEvent?.Invoke(e);
            TextInput += e => OnTextInputEvent?.Invoke(e);
            FileDrop += e => OnDropFileEvent?.Invoke(e);
        }

        // ── Ciclo de vida OpenTK ─────────────────────────────────────────────
        protected override void OnLoad()
        {
            base.OnLoad();
            EngineSettings = new OpenGLSettings();
            EngineSettings.Setup();
            OnStartRender();
        }

        protected override void OnUnload()
        {
            base.OnUnload();
            OnUnloadRender?.Invoke();
        }

        protected override void OnUpdateFrame(FrameEventArgs args)
        {
            base.OnUpdateFrame(args);
            OnUpdate();
        }

        protected override void OnRenderFrame(FrameEventArgs args)
        {
            base.OnRenderFrame(args);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            OnRender();
            SwapBuffers();
        }

        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);
            GL.Viewport(0, 0, e.Width, e.Height);
            TriggerResize(e); // <── delega al método público de IRender
        }

        // ── Implementación de IRender ────────────────────────────────────────
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