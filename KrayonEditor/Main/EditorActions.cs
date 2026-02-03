using KrayonEditor.UI;
using KrayonCore;

namespace KrayonEditor
{
    internal static class EditorActions
    {
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