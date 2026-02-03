using ImGuiNET;

namespace KrayonEditor.UI
{
    public abstract class UIBehaviour
    {
        protected bool _isVisible = true;

        public bool IsVisible
        {
            get => _isVisible;
            set => _isVisible = value;
        }

        public abstract void OnDrawUI();
    }
}