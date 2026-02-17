using OpenTK.Mathematics;

namespace KrayonCore
{
    /// <summary>
    /// Interfaz común para todos los componentes que se renderizan en la escena.
    /// Los componentes que implementen esta interfaz deben auto-registrarse en el SceneRenderer.
    /// </summary>
    public interface IRenderable
    {
        /// <summary>Tipo de renderizado para clasificación interna</summary>
        RenderableType RenderType { get; }

        /// <summary>Si el componente está habilitado y debe renderizarse</summary>
        bool Enabled { get; }

        /// <summary>GameObject al que pertenece este renderer</summary>
        GameObject? GameObject { get; }

        /// <summary>Prioridad de renderizado (menor = primero). Default: 0</summary>
        int RenderPriority => 0;
    }

    /// <summary>
    /// Tipos de componentes renderizables para clasificación interna
    /// </summary>
    public enum RenderableType
    {
        Skybox,
        Mesh,
        Sprite,
        Tile,
        Custom
    }
}