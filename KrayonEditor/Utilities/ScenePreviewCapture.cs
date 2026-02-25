using KrayonCore;
using KrayonCore.Components.RenderComponents;
using KrayonCore.Core;
using KrayonCore.Core.Attributes;
using OpenTK.Graphics.OpenGL4;
using System;

namespace KrayonEditor.Utilities
{
    public static class ScenePreviewCapture
    {
        private static bool _isRunning = false;

        public static void StartCapture()
        {
            if (_isRunning) return;
            _isRunning = true;

            try
            {
                GameScene scene = SceneManager.CreateScene("Entity Scene");
                SceneManager.RegisterSceneOnly(scene);

                GameObject _Light = scene.CreateGameObject();
                Light _LightCMP = _Light.AddComponent<Light>();
                SkyboxRenderer _SkyBox = _Light.AddComponent<SkyboxRenderer>();
                _SkyBox.MaterialPath = "Sky";
                _SkyBox.Start();
                _Light.Transform.Rotate(-45, 0, 0);
                _LightCMP.Intensity = 5.0f;
                _LightCMP.Start();

                scene.SelfRenderScene.Resize(1024, 1024);
                scene.SelfRenderScene.Resize(1024, 1024);

                scene.SelfRenderScene.GetCamera().OrthoSize = 1;
                scene.SelfRenderScene.GetCamera().SetProjectionMode(ProjectionMode.Orthographic);

                foreach (var item in AssetManager.GetAllModelGuids())
                {
                    string currentAsset = item.ToString();
                    string path = $"{AssetManager.TotalBase}/Cache/{currentAsset}.png";

                    if (File.Exists(path))
                        continue;

                    GameObject obj = scene.CreateGameObject();
                    MeshRenderer renderer = obj.AddComponent<MeshRenderer>();
                    renderer.ModelPath = currentAsset;
                    renderer.Start();

                    scene.SelfRenderScene.Update(TimerData.DeltaTime);
                    scene.SelfRenderScene.Render();

                    var sceneBuffer = scene.SelfRenderScene.Buffers.Get("scene");
                    int width = sceneBuffer.Width;
                    int height = sceneBuffer.Height;

                    GL.BindFramebuffer(FramebufferTarget.Framebuffer, sceneBuffer.Handle);

                    var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
                    if (status != FramebufferErrorCode.FramebufferComplete)
                    {
                        Console.Error.WriteLine($"[Capture] Framebuffer incompleto: {status} — saltando {currentAsset}");
                        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
                        scene.DestroyGameObject(obj);
                        continue;
                    }

                    byte[] pixels = new byte[width * height * 4];
                    GL.Finish();
                    GL.ReadPixels(0, 0, width, height, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);
                    GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

                    ImageCapture.SaveFramebuffer(pixels, width, height, path);

                    scene.DestroyGameObject(obj);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ScenePreviewCapture] Error: {ex}");
            }
            finally
            {
                _isRunning = false;
            }
        }
    }
}