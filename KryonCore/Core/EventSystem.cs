using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KrayonCore.EventSystem
{
    public static class EventSystem
    {
        public static Vector2 ScreenToWorldPosition(Vector2 screenPos)
        {
            Vector3 cameraPos = GraphicsEngine.Instance.GetSceneRenderer().GetCamera().Position;
            float orthoSize = GraphicsEngine.Instance.GetSceneRenderer().GetCamera().OrthoSize;
            int screenWidth = GraphicsEngine.Instance.GetSceneFrameBuffer().Width;
            int screenHeight = GraphicsEngine.Instance.GetSceneFrameBuffer().Height;

            float aspect = (float)screenWidth / (float)screenHeight;

            float height = orthoSize;
            float width = height * aspect;

            float normalizedX = screenPos.X / screenWidth;
            float normalizedY = screenPos.Y / screenHeight;

            float ndcX = (normalizedX * 2.0f) - 1.0f;
            float ndcY = 1.0f - (normalizedY * 2.0f);

            Vector2 worldPos = new Vector2(
                cameraPos.X + (ndcX * width / 2.0f),
                cameraPos.Y + (ndcY * height / 2.0f)
            );
            return worldPos;
        }

        public static GameObject GetObjectAtWorldPosition(Vector2 worldPos)
        {
            for (int i = 0; i < SceneManager.ActiveScene.GetAllGameObjects().Count; i++)
            {
                GameObject obj = SceneManager.ActiveScene.GetAllGameObjects()[i];

                float minX = obj.Transform.Position.X - obj.Transform.Scale.X;
                float maxX = obj.Transform.Position.X + obj.Transform.Scale.X;
                float minY = obj.Transform.Position.Y - obj.Transform.Scale.Y;
                float maxY = obj.Transform.Position.Y + obj.Transform.Scale.Y;

                bool checkX = worldPos.X >= minX && worldPos.X <= maxX;
                bool checkY = worldPos.Y >= minY && worldPos.Y <= maxY;

                if (checkX && checkY)
                {
                    return obj;
                }
            }

            Console.WriteLine($"[GetObjectAt] NO HIT\n");
            return null;
        }

        public static GameObject OnClickObject(Vector2 viewportMousePos)
        {
            Vector2 worldPos = ScreenToWorldPosition(viewportMousePos);
            GameObject hit = GetObjectAtWorldPosition(worldPos);
            return hit;
        }
    }
}