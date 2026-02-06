using Assimp;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTK.Mathematics;
using KrayonCore.Core.Attributes;
using KrayonCore.Core;

namespace KrayonCore
{
    public class GraphicsEngine
    {
        private GameWindowInternal? _window;
        private SceneRenderer _sceneRenderer;
        private FrameBuffer? _sceneFrameBuffer;


        public event Action? LoadEvent;
        public event Action<float>? UpdateEvent;
        public event Action<float>? RenderEvent;
        public event Action<int, int>? ResizeEvent;
        public event Action? CloseEvent;

        public event Action<TextInputEventArgs>? TextInputEvent;
        public static GraphicsEngine? Instance { get; private set; }

        private MaterialManager _materials;
        public MaterialManager Materials => _materials;

        public GraphicsEngine()
        {
            Instance = this;

            _materials = new MaterialManager();
            _sceneRenderer = new SceneRenderer();
        }

        private void CreateDefaultMaterials()
        {

            var textureMaterial = _materials.Create("basic", "shaders/basic");
            textureMaterial?.SetVector3Cached("u_Color", new Vector3(1.0f, 1.0f, 1.0f));

            if (textureMaterial != null)
            {
                textureMaterial.LoadMainTexture("textures/sprites/Environment/dirt.png", generateMipmaps: true, flip: true);
            }
        }

        public void CreateWindow(int width, int height, string title)
        {
            _window = new GameWindowInternal(width, height, title, this);
        }

        public void Run()
        {
            _window?.Run();
        }

        public KeyboardState GetKeyboardState() => _window?.KeyboardState ?? default;
        public MouseState GetMouseState() => _window?.MouseState ?? default;
        public SceneRenderer GetSceneRenderer() => _sceneRenderer;

        public GameWindow GetWindow() => _window;

        public FrameBuffer GetSceneFrameBuffer()
        {
            if (_sceneFrameBuffer == null)
            {
                _sceneFrameBuffer = new FrameBuffer(1280, 720);
            }
            return _sceneFrameBuffer;
        }

        public void ResizeSceneFrameBuffer(int width, int height)
        {
            if (_sceneFrameBuffer != null)
            {
                _sceneFrameBuffer.Resize(width, height);
            }
        }

        public void OnTextInput(TextInputEventArgs e)
        {
            TextInputEvent?.Invoke(e);
        }

        public void InternalLoad()
        {
            AssetManager.Initialize ();

            if (SceneManager.ActiveScene == null)
            {
                var defaultScene = SceneManager.CreateScene("DefaultScene");
                SceneManager.LoadScene("DefaultScene");
            }

            _materials.LoadMaterialsData();

            CreateDefaultMaterials();

            _sceneFrameBuffer = new FrameBuffer(1280, 720);
            _sceneRenderer.Initialize();
            LoadEvent?.Invoke();
        }

        public void InternalUpdate(float deltaTime)
        {
            SceneManager.Update(deltaTime);
            _sceneRenderer.Update(deltaTime);
            UpdateEvent?.Invoke(deltaTime);
        }

        public void InternalRender(float deltaTime)
        {
            if (_sceneFrameBuffer != null)
            {
                _sceneFrameBuffer.Bind();
                GL.ClearColor(0.1f, 0.1f, 0.1f, 1.0f);
                GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
                _sceneRenderer.Render();
                _sceneFrameBuffer.Unbind();
            }

            if (_window != null)
            {
                GL.Viewport(0, 0, _window.ClientSize.X, _window.ClientSize.Y);
                GL.ClearColor(0.0f, 0.0f, 0.0f, 1.0f);
                GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            }

            RenderEvent?.Invoke(deltaTime);
        }

        public void InternalResize(int width, int height)
        {
            _sceneRenderer.Resize(width, height);
            ResizeEvent?.Invoke(width, height);
        }

        public void InternalClose()
        {
            if (SceneManager.ActiveScene != null)
            {
                SceneManager.ActiveScene.OnUnload();
            }

            _sceneFrameBuffer?.Dispose();
            _sceneRenderer.Shutdown();
            CloseEvent?.Invoke();
        }
    }
}