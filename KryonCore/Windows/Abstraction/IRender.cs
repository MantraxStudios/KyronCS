using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System;

namespace KrayonGraphics.GraphicEngine.Abstraction
{
    public interface IRender
    {
        // Render y actualización
        Action OnLoadRender { get; set; }
        Action OnRederFrame { get; set; }
        Action OnUpdateRender { get; set; }
        Action OnUnloadRender { get; set; }

        // Input de teclado
        Action<KeyboardKeyEventArgs> OnKeyDownEvent { get; set; }
        Action<KeyboardKeyEventArgs> OnKeyUpEvent { get; set; }

        // Input de mouse
        Action<MouseButtonEventArgs> OnMouseDownEvent { get; set; }
        Action<MouseButtonEventArgs> OnMouseUpEvent { get; set; }
        Action<MouseMoveEventArgs> OnMouseMoveEvent { get; set; }
        Action<MouseWheelEventArgs> OnMouseWheelEvent { get; set; }

        // Input de texto
        Action<TextInputEventArgs> OnTextInputEvent { get; set; }

        // Archivos arrastrados
        Action<FileDropEventArgs> OnDropFileEvent { get; set; }

        // Cambio de tamaño
        Action<ResizeEventArgs> OnResizeEvent { get; set; }

        // Métodos para ejecutar los callbacks
        void OnStartRender();
        void OnRender();
        void OnUpdate();
        void OnInputText(TextInputEventArgs e);
        void OnDropFile(FileDropEventArgs e);
        void OnKeyDown(KeyboardKeyEventArgs e);
        void OnKeyUp(KeyboardKeyEventArgs e);
        void OnMouseDown(MouseButtonEventArgs e);
        void OnMouseUp(MouseButtonEventArgs e);
        void OnMouseMove(MouseMoveEventArgs e);
        void OnMouseWheel(MouseWheelEventArgs e);
        void TriggerResize(ResizeEventArgs e);
    }
}