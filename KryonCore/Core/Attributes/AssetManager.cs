using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KrayonCore.Core.Attributes
{
    public static class AssetManager
    {
        private static Dictionary<Guid, AssetData> _assets = new();

        public static void Register(AssetData asset)
        {
            if (_assets.ContainsKey(asset.Guid))
                throw new Exception($"Asset duplicado: {asset.Guid}");

            _assets.Add(asset.Guid, asset);

            Console.WriteLine($"New Asset Register With GUID: {asset.Guid}");
        }

        public static T Get<T>(Guid guid) where T : AssetData
        {
            return _assets.TryGetValue(guid, out var asset)
                ? asset as T
                : null;
        }

        public static bool Exists(Guid guid)
        {
            return _assets.ContainsKey(guid);
        }
    }

}
