using Jint;
using KrayonCore.Core.Attributes;
using KrayonEditor.UI;

namespace KrayonEditor
{
    internal static class Program
    {
        static void Main(string[] args)
        {
            AssetManager.BasePath = "Content/";
            AssetManager.DataBase = "DataBaseFromAssets.json"; 
            EngineLoader.Run();
        }
    }
}