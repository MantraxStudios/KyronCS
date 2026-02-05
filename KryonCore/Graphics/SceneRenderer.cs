using Assimp;
using KrayonCore;
using KrayonCore.Graphics;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace KrayonCore
{
    public class SceneRenderer
    {
        private Camera _camera;

        public void Initialize()
        {
            _camera = new Camera(new Vector3(0, 0, 5), WindowConfig.Width / (float)WindowConfig.Height);
        }

        public void Render()
        {
            Matrix4 view = _camera.GetViewMatrix();
            Matrix4 projection = _camera.GetProjectionMatrix();

            // Renderizar MeshRenderers
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

            // Renderizar TileRenderers
            var tileRenderers = SceneManager.ActiveScene?.FindGameObjectsWithComponent<TileRenderer>();
            if (tileRenderers != null)
            {
                foreach (var go in tileRenderers)
                {
                    var renderer = go.GetComponent<TileRenderer>();
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
            _camera.UpdateAspectRatio(width, height);
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