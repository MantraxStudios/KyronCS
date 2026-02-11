using Jint;
using KrayonCore.Core.Attributes;
using KrayonEditor.UI;

namespace KrayonEditor
{
    internal static class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 0)
            {
                AssetManager.BasePath = $"{args[0]}/Content/";
                AssetManager.DataBase = $"{args[0]}/DataBaseFromAssets.json";
            }
            EngineEditor.Run();
        }
    }
}