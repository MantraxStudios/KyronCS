using Jint;
using KrayonCore.Core;
using KrayonCore.Core.Attributes;
using KrayonEditor.UI;
using System.Reflection;

namespace KrayonEditor
{
    internal static class Program
    {
        static void Main(string[] args)
        {
            AssetManager.TotalBase = "";
            AppInfo.IsCompiledGame = true;
            EngineLoader.Run();
        }
    }
}