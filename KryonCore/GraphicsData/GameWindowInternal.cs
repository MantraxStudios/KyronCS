using KrayonCore.Core;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace KrayonCore.GraphicsData
{
    internal class GameWindowInternal : GameWindow
    {
        private readonly GraphicsEngine _engine;

        public GameWindowInternal(
            int width,
            int height,
            string title,
            GraphicsEngine engine
        )
            : base(
                new GameWindowSettings
                {
                    UpdateFrequency = 0.0
                },
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
            _engine = engine;
        }

        protected override void OnLoad()
        {
            base.OnLoad();

            VSync = VSyncMode.Off;

            GL.ClearColor(0.2f, 0.3f, 0.6f, 1f);
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Less);
            GL.DepthMask(true);
            GL.ClearDepth(1.0);
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);
            GL.FrontFace(FrontFaceDirection.Ccw);

            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            // Suscribirse a eventos
            TextInput += OnTextInput;
            FileDrop += OnFileDrop;

            _engine.InternalLoad();

            Console.WriteLine($"OpenGL: {GL.GetString(StringName.Version)}");
            Console.WriteLine($"GPU: {GL.GetString(StringName.Renderer)}");
            Console.WriteLine($"Depth Test: {GL.IsEnabled(EnableCap.DepthTest)}");
        }

        private void OnTextInput(TextInputEventArgs e)
        {
            _engine.OnTextInput(e);
        }

        private void OnFileDrop(FileDropEventArgs e)
        {
            _engine.OnFileDrop(e);
        }

        protected override void OnUpdateFrame(FrameEventArgs args)
        {
            base.OnUpdateFrame(args);
            _engine.InternalUpdate((float)args.Time);
        }

        protected override void OnRenderFrame(FrameEventArgs args)
        {
            base.OnRenderFrame(args);

            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            _engine.InternalRender((float)args.Time);

            SwapBuffers();
        }

        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);

            GL.Viewport(0, 0, e.Width, e.Height);
            _engine.InternalResize(e.Width, e.Height);
        }

        protected override void OnUnload()
        {
            base.OnUnload();

            // Desuscribirse de eventos
            TextInput -= OnTextInput;
            FileDrop -= OnFileDrop;

            _engine.InternalClose();
        }
    }
}