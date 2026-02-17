using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace KrayonCore.Animation
{
    // ═══════════════════════════════════════════════════════════════════════
    //  ANIMATOR CONTROLLER DATA
    //  Estructura que se serializa en el archivo .animcontroller (JSON).
    //  El inspector guarda el GUID del asset; en runtime el Animator lo carga.
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Tipo de condición para evaluar una transición.
    /// </summary>
    public enum ConditionMode
    {
        /// <summary>El parámetro bool es true.</summary>
        True,
        /// <summary>El parámetro bool es false.</summary>
        False,
        /// <summary>El parámetro float/int es mayor que el umbral.</summary>
        Greater,
        /// <summary>El parámetro float/int es menor que el umbral.</summary>
        Less,
        /// <summary>El parámetro float/int es igual al umbral (solo int).</summary>
        Equals,
        /// <summary>El parámetro trigger fue activado (se consume al usarse).</summary>
        Trigger
    }

    /// <summary>
    /// Tipo de parámetro del AnimatorController.
    /// </summary>
    public enum ParameterType
    {
        Float,
        Int,
        Bool,
        Trigger
    }

    // ───────────────────────────────────────────────────────────────────────
    //  Parámetros
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Parámetro del controlador (float, int, bool o trigger).
    /// </summary>
    public class AnimatorParameterData
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("type")]
        public ParameterType Type { get; set; }

        /// <summary>Valor por defecto (se interpreta según Type).</summary>
        [JsonPropertyName("defaultValue")]
        public float DefaultValue { get; set; } = 0f;
    }

    // ───────────────────────────────────────────────────────────────────────
    //  Condiciones de transición
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Una condición individual dentro de una transición.
    /// Todas las condiciones de una transición deben cumplirse a la vez (AND).
    /// </summary>
    public class TransitionConditionData
    {
        [JsonPropertyName("parameter")]
        public string Parameter { get; set; }

        [JsonPropertyName("mode")]
        public ConditionMode Mode { get; set; }

        /// <summary>Umbral para comparaciones Greater / Less / Equals.</summary>
        [JsonPropertyName("threshold")]
        public float Threshold { get; set; } = 0f;
    }

    // ───────────────────────────────────────────────────────────────────────
    //  Transiciones
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Transición entre dos estados.
    /// </summary>
    public class StateTransitionData
    {
        /// <summary>Nombre del estado destino.</summary>
        [JsonPropertyName("toState")]
        public string ToState { get; set; }

        /// <summary>Duración del crossfade en segundos.</summary>
        [JsonPropertyName("duration")]
        public float Duration { get; set; } = 0.2f;

        /// <summary>
        /// Si true, la transición puede interrumpir la animación actual antes
        /// de que termine (no espera al final del clip).
        /// </summary>
        [JsonPropertyName("canInterrupt")]
        public bool CanInterrupt { get; set; } = true;

        /// <summary>
        /// Tiempo normalizado [0-1] del clip actual a partir del cual esta
        /// transición puede activarse. 0 = siempre, 0.9 = solo en el 90% final.
        /// </summary>
        [JsonPropertyName("exitTime")]
        public float ExitTime { get; set; } = 0f;

        /// <summary>
        /// Si true, la condición ExitTime se evalúa además de las condiciones.
        /// Si false, ExitTime se ignora.
        /// </summary>
        [JsonPropertyName("hasExitTime")]
        public bool HasExitTime { get; set; } = false;

        /// <summary>Lista de condiciones (todas deben cumplirse → AND).</summary>
        [JsonPropertyName("conditions")]
        public List<TransitionConditionData> Conditions { get; set; } = new();
    }

    // ───────────────────────────────────────────────────────────────────────
    //  Estados
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Estado del AnimatorController. Cada estado referencia un clip de animación.
    /// </summary>
    public class AnimatorStateData
    {
        /// <summary>Nombre único del estado (p.ej. "Idle", "Run", "Jump").</summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>
        /// GUID del asset FBX que contiene el clip.
        /// Si el FBX tiene varios clips, se usa ClipName para identificarlo.
        /// </summary>
        [JsonPropertyName("clipGuid")]
        public string ClipGuid { get; set; }

        /// <summary>
        /// Nombre del clip dentro del FBX.
        /// Si está vacío se usa el primero que se encuentre.
        /// </summary>
        [JsonPropertyName("clipName")]
        public string ClipName { get; set; } = "";

        /// <summary>¿El clip se repite en bucle?</summary>
        [JsonPropertyName("loop")]
        public bool Loop { get; set; } = true;

        /// <summary>Velocidad de reproducción del clip en este estado.</summary>
        [JsonPropertyName("speed")]
        public float Speed { get; set; } = 1f;

        /// <summary>Transiciones que salen de este estado.</summary>
        [JsonPropertyName("transitions")]
        public List<StateTransitionData> Transitions { get; set; } = new();
    }

    // ───────────────────────────────────────────────────────────────────────
    //  Root del asset
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Raíz del archivo .animcontroller. Contiene parámetros y estados.
    /// Se serializa directamente a/desde JSON.
    /// </summary>
    public class AnimatorControllerData
    {
        /// <summary>Nombre legible del controlador.</summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = "AnimatorController";

        /// <summary>Nombre del estado que se reproduce al inicio.</summary>
        [JsonPropertyName("defaultState")]
        public string DefaultState { get; set; } = "";

        /// <summary>Lista de parámetros (float, int, bool, trigger).</summary>
        [JsonPropertyName("parameters")]
        public List<AnimatorParameterData> Parameters { get; set; } = new();

        /// <summary>Lista de estados (cada uno referencia un clip).</summary>
        [JsonPropertyName("states")]
        public List<AnimatorStateData> States { get; set; } = new();
    }
}