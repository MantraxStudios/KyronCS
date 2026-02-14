using KrayonGraphics.GraphicEngine.Abstraction;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace KrayonGraphics.GraphicEngine.Window.OpenGL
{
    public class OpenGLSettings : ISettings
    {
        public void Setup()
        {
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

        }
    }
}
