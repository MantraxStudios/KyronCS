using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KrayonEditor.UI
{
    public class TileEditor : UIBehaviour
    {
        public override void OnDrawUI()
        {
            if (!_isVisible)
                return;

            ImGui.Begin("Tile Editor", ref _isVisible);

            ImGui.End();
        }
    }
}
