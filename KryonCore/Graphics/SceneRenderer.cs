using Assimp;
using KrayonCore;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace KrayonCore
{
    public class SceneRenderer
    {
        private Camera _camera;
        private CameraComponent? _mainCamera;

        public void Initialize()
        {
            _camera = new Camera(new Vector3(0, 0, 5), WindowConfig.Width / WindowConfig.Height);
        }

        public void Render()
        {
            if (_mainCamera != null)
            {
                _camera.Position = _mainCamera.Position;
                _camera.Yaw = _mainCamera.Yaw;
                _camera.Pitch = _mainCamera.Pitch;
                _camera.Fov = _mainCamera.Fov;
                _camera.AspectRatio = _mainCamera.AspectRatio;
            }

            Matrix4 view = _camera.GetViewMatrix();
            Matrix4 projection = _camera.GetProjectionMatrix();

            var meshRenderers = SceneManager.ActiveScene?.FindGameObjectsWithComponent<MeshRenderer>();
            if (meshRenderers != null)
            {
                foreach (var go in meshRenderers)
                {
                    var renderer = go.GetComponent<MeshRenderer>();
                    if (renderer != null && renderer.Enabled)
                    {
                        if (renderer.MaterialCount == 0)
                        {
                            var basicMaterial = GraphicsEngine.Instance.Materials.Get("basic");
                            if (basicMaterial != null)
                            {
                                basicMaterial.SetVector3Cached("u_Color", new Vector3(1.0f, 1.0f, 1.0f));
                                renderer.AddMaterial(basicMaterial);
                            }
                        }

                        renderer.Render(view, projection);
                    }
                }
            }
        }

        public void Resize(int width, int height)
        {
            GL.Viewport(0, 0, width, height);
            _camera.AspectRatio = (float)width / height;
            if (_mainCamera != null)
            {
                _mainCamera.AspectRatio = (float)width / height;
            }
        }

        public void SetCamera(CameraComponent camera)
        {
            _mainCamera = camera;
            _camera = new Camera(camera.Position, camera.AspectRatio);
        }

        public void Update(float deltaTime)
        {
            // Actualizar lógica si es necesario
        }

        public void Shutdown()
        {
        }

        public Camera GetCamera() => _camera;
    }
}