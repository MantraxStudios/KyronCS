using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KrayonCore.Core.Attributes
{
    public class AssetData
    {
        public Guid Guid { get; set; } = Guid.NewGuid();

        public AssetData()
        {
            // El Guid ya se inicializa arriba
        }
    }
}