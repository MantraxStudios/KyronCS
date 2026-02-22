using KrayonCore.Editor.Panels;
using System;
using System.Collections.Generic;

namespace KrayonEditor.UI
{
    public static class UIRender
    {
        private static Dictionary<Type, UIBehaviour> _uiBehaviours = new Dictionary<Type, UIBehaviour>();

        public static void StartUI()
        {
            RegisterUI(new MainMenuBarUI());
            RegisterUI(new DockSpaceUI());
            RegisterUI(new HierarchyUI());
            RegisterUI(new InspectorUI());
            RegisterUI(new SceneViewUI());
            RegisterUI(new ConsoleUI());
            RegisterUI(new AssetsUI());
            RegisterUI(new MaterialUI());
            RegisterUI(new TileEditor());
            RegisterUI(new SpriteAnimationUI());
            RegisterUI(new CompilerUI());
            RegisterUI(new AnimatorEditorUI());
            //RegisterUI(new UICanvasEditor());
        }

        private static void RegisterUI(UIBehaviour behaviour)
        {
            _uiBehaviours[behaviour.GetType()] = behaviour;
        }

        public static T GetUI<T>() where T : UIBehaviour
        {
            return _uiBehaviours[typeof(T)] as T;
        }

        public static void Render()
        {
            foreach (var behaviour in _uiBehaviours.Values)
            {
                try
                {
                    behaviour.OnDrawUI();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[UIRender] ❌ CRASH en: {behaviour.GetType().Name}");
                    Console.WriteLine($"[UIRender] Mensaje: {ex.Message}");
                    Console.WriteLine($"[UIRender] Stack: {ex.StackTrace}");
                    if (ex.InnerException != null)
                        Console.WriteLine($"[UIRender] Inner: {ex.InnerException.Message}");
                    throw;
                }
            }
        }
    }
}