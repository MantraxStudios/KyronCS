using KrayonCore;
using KrayonCore.Components;
using KrayonCore.Graphics;
using KrayonCore.Graphics.Camera;
using KrayonEditor.UI;
using System.Numerics;

namespace KrayonEditor
{
    public static class EditorActions
    {
        public static GameObject? SelectedObject { get; set; }
        public static bool IsHoveringScene = false;
        public static Vector2 ViewPortPosition;

        public static void CreateEmptyGameObject()
        {
            if (SceneManager.ActiveScene == null) return;
            int count = SceneManager.ActiveScene.GetAllGameObjects().Count;
            var go = SceneManager.ActiveScene.CreateGameObject($"GameObject_{count}");
            go.Transform.SetPosition(0, 0, 0);
            EngineEditor.SetSelectedObject(go);
            EngineEditor.LogMessage($"Created {go.Name}");
        }

        public static GameObject CreateCubeGameObject()
        {
            if (SceneManager.ActiveScene == null) return null;
            int count = SceneManager.ActiveScene.GetAllGameObjects().Count;
            var go = SceneManager.ActiveScene.CreateGameObject($"Cube_{count}");
            go.Transform.SetPosition(0, 0, 0);
            var meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.Model = Model.Load("models/Cube.fbx");
            EngineEditor.SetSelectedObject(go);
            EngineEditor.LogMessage($"Created {go.Name} with MeshRenderer");
            return go;
        }

        public static void SetupAllMaterials()
        {
            if (SceneManager.ActiveScene == null) return;
            
            for (int i = 0; i < SceneManager.ActiveScene.GetAllGameObjects().Count; i++)
            {
                if (SceneManager.ActiveScene.GetAllGameObjects()[i].HasComponent<MeshRenderer>())
                {
                    SceneManager.ActiveScene.GetAllGameObjects()[i].GetComponent<MeshRenderer>().SetupAutomaticMaterials();
                }
            }
        }

        public static void CreateDirectionalLight()
        {
            if (SceneManager.ActiveScene == null) return;
            int count = SceneManager.ActiveScene.GetAllGameObjects().Count;
            var go = SceneManager.ActiveScene.CreateGameObject($"DirectionalLight_{count}");
            go.Transform.SetPosition(0, 5, 0);
            go.Transform.SetRotation(45, 0, 0);
            var light = go.AddComponent<Light>();
            light.Type = LightType.Directional;
            light.Start();
            EngineEditor.SetSelectedObject(go);
            EngineEditor.LogMessage($"Created {go.Name} with Directional Light");
        }

        public static void CreateCamera()
        {
            if (SceneManager.ActiveScene == null) return;
            int count = SceneManager.ActiveScene.GetAllGameObjects().Count;
            var go = SceneManager.ActiveScene.CreateGameObject($"Camera_{count}");
            go.Transform.SetPosition(0, 5, 0);
            go.Transform.SetRotation(45, 0, 0);
            var cam = go.AddComponent<CameraComponent>();
            EngineEditor.SetSelectedObject(go);
            EngineEditor.LogMessage($"Created {go.Name} with Camera");
        }

        public static void CreatePointLight()
        {
            if (SceneManager.ActiveScene == null) return;
            int count = SceneManager.ActiveScene.GetAllGameObjects().Count;
            var go = SceneManager.ActiveScene.CreateGameObject($"PointLight_{count}");
            go.Transform.SetPosition(0, 2, 0);
            var light = go.AddComponent<Light>();
            light.Type = LightType.Point;
            light.Start();
            EngineEditor.SetSelectedObject(go);
            EngineEditor.LogMessage($"Created {go.Name} with Point Light");
        }

        public static void CreateSpotLight()
        {
            if (SceneManager.ActiveScene == null) return;
            int count = SceneManager.ActiveScene.GetAllGameObjects().Count;
            var go = SceneManager.ActiveScene.CreateGameObject($"SpotLight_{count}");
            go.Transform.SetPosition(0, 5, 0);
            go.Transform.SetRotation(45, 0, 0);
            var light = go.AddComponent<Light>();
            light.Type = LightType.Spot;
            light.Start();
            EngineEditor.SetSelectedObject(go);
            EngineEditor.LogMessage($"Created {go.Name} with Spot Light");
        }

        public static void CreateModelGameObject()
        {
            if (SceneManager.ActiveScene == null) return;
            int count = SceneManager.ActiveScene.GetAllGameObjects().Count;
            var go = SceneManager.ActiveScene.CreateGameObject($"Model_{count}");
            go.Transform.SetPosition(0, 0, 0);
            var meshRenderer = go.AddComponent<MeshRenderer>();
            EngineEditor.SetSelectedObject(go);
            EngineEditor.LogMessage($"Created {go.Name} with MeshRenderer (assign model in inspector)");
        }

        public static void CreateTileRendererGameObject()
        {
            if (SceneManager.ActiveScene == null) return;
            int count = SceneManager.ActiveScene.GetAllGameObjects().Count;
            var go = SceneManager.ActiveScene.CreateGameObject($"TileRenderer_{count}");
            go.Transform.SetPosition(0, 0, 0);
            var tileRenderer = go.AddComponent<TileRenderer>();
            tileRenderer.Start();
            EngineEditor.SetSelectedObject(go);
            EngineEditor.LogMessage($"Created {go.Name} with TileRenderer");
        }

        public static void DeleteSelectedObject()
        {
            var selected = EngineEditor.GetSelectedObject();
            if (selected != null)
            {
                DeleteGameObject(selected);
            }
        }

        public static void DeleteGameObject(GameObject go)
        {
            if (go.Tag == "MainCamera")
            {
                EngineEditor.LogMessage("Cannot delete MainCamera");
                return;
            }
            EngineEditor.LogMessage($"Deleted {go.Name}");
            if (EngineEditor.GetSelectedObject() == go)
            {
                EngineEditor.SetSelectedObject(null);
            }
        }
    }
}