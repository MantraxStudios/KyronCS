using KrayonCore;
using KrayonCore.Components;
using KrayonCore.Graphics;
using KrayonEditor.UI;

namespace KrayonEditor
{
    public static class EditorActions
    {
        public static GameObject? SelectedObject { get; set; }

        public static void CreateEmptyGameObject()
        {
            if (SceneManager.ActiveScene == null) return;
            int count = SceneManager.ActiveScene.GetAllGameObjects().Count;
            var go = SceneManager.ActiveScene.CreateGameObject($"GameObject_{count}");
            go.Transform.SetPosition(0, 0, 0);
            EngineEditor.SetSelectedObject(go);
            EngineEditor.LogMessage($"Created {go.Name}");
        }

        public static void CreateCubeGameObject()
        {
            if (SceneManager.ActiveScene == null) return;
            int count = SceneManager.ActiveScene.GetAllGameObjects().Count;
            var go = SceneManager.ActiveScene.CreateGameObject($"Cube_{count}");
            go.Transform.SetPosition(0, 0, 0);
            var meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.Model = Model.Load("models/Cube.fbx");
            EngineEditor.SetSelectedObject(go);
            EngineEditor.LogMessage($"Created {go.Name} with MeshRenderer");
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