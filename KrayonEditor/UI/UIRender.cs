using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KrayonEditor.UI
{
    public class UIRender
    {
        public List<UIBehaviour> _UIS = new List<UIBehaviour>();

        public void RenderUI()
        {
            for (int i = 0; i < _UIS.Count; i++)
            {
                if (_UIS[i].IsVisible)
                    _UIS[i].OnDrawUI();
            }
        }
    }
}