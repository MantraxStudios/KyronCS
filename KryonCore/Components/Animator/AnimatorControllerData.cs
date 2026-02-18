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
    //  Capas (Layers)
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Modo de mezcla de una capa sobre las capas inferiores.
    /// </summary>
    public enum LayerBlendingMode
    {
        /// <summary>
        /// La capa sobreescribe completamente el resultado de las capas anteriores
        /// (modulado por Weight). Equivale al modo "Override" de Unity.
        /// </summary>
        Override,

        /// <summary>
        /// La capa suma su resultado al de las capas anteriores
        /// (modulado por Weight). Equivale al modo "Additive" de Unity.
        /// </summary>
        Additive
    }

    /// <summary>
    /// Una capa del AnimatorController. Cada capa tiene su propia máquina de
    /// estados independiente. La capa 0 ("Base Layer") es la principal; las
    /// capas superiores se mezclan sobre ella usando Weight y BlendingMode.
    /// </summary>
    public class AnimatorLayerData
    {
        /// <summary>Nombre legible de la capa (p.ej. "Base Layer", "Upper Body").</summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = "Base Layer";

        /// <summary>
        /// Peso de mezcla de la capa [0-1].
        /// La capa 0 siempre tiene peso efectivo 1 (se ignora este valor en runtime).
        /// </summary>
        [JsonPropertyName("weight")]
        public float Weight { get; set; } = 1f;

        /// <summary>Modo de mezcla con las capas inferiores.</summary>
        [JsonPropertyName("blendingMode")]
        public LayerBlendingMode BlendingMode { get; set; } = LayerBlendingMode.Override;

        /// <summary>
        /// GUID del AvatarMask que limita qué huesos afecta esta capa.
        /// Vacío = sin máscara (afecta a todos los huesos).
        /// </summary>
        [JsonPropertyName("avatarMask")]
        public string AvatarMask { get; set; } = "";

        /// <summary>Nombre del estado inicial de esta capa.</summary>
        [JsonPropertyName("defaultState")]
        public string DefaultState { get; set; } = "";

        /// <summary>Estados de la máquina de estados de esta capa.</summary>
        [JsonPropertyName("states")]
        public List<AnimatorStateData> States { get; set; } = new();
    }

    // ───────────────────────────────────────────────────────────────────────
    //  Root del asset
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Raíz del archivo .animcontroller. Contiene parámetros y capas.
    /// Se serializa directamente a/desde JSON.
    /// <para>
    /// Compatibilidad con versiones antiguas: si <see cref="Layers"/> está vacía
    /// y <see cref="States"/> contiene datos, el runtime los trata como la
    /// Base Layer implícita (índice 0) para no romper assets existentes.
    /// </para>
    /// </summary>
    public class AnimatorControllerData
    {
        /// <summary>Nombre legible del controlador.</summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = "AnimatorController";

        // ── Campos legacy (Base Layer implícita) ────────────────────────────
        // Mantenidos para retro-compatibilidad con assets guardados antes de
        // introducir el sistema de capas. El editor los migra a Layers[0] al
        // abrir un archivo que todavía los use.

        /// <summary>
        /// [LEGACY] Nombre del estado inicial de la Base Layer.
        /// Usar <c>Layers[0].DefaultState</c> en assets nuevos.
        /// </summary>
        [JsonPropertyName("defaultState")]
        public string DefaultState
        {
            get => Layers.Count > 0 ? Layers[0].DefaultState : _legacyDefaultState;
            set
            {
                if (Layers.Count > 0) Layers[0].DefaultState = value;
                else _legacyDefaultState = value;
            }
        }
        private string _legacyDefaultState = "";

        /// <summary>
        /// [LEGACY] Estados de la Base Layer.
        /// Usar <c>Layers[0].States</c> en assets nuevos.
        /// </summary>
        [JsonPropertyName("states")]
        public List<AnimatorStateData> States
        {
            get => Layers.Count > 0 ? Layers[0].States : _legacyStates;
            set
            {
                if (Layers.Count > 0) Layers[0].States = value;
                else _legacyStates = value;
            }
        }
        private List<AnimatorStateData> _legacyStates = new();

        // ── Datos actuales ──────────────────────────────────────────────────

        /// <summary>Lista de parámetros globales (float, int, bool, trigger).</summary>
        [JsonPropertyName("parameters")]
        public List<AnimatorParameterData> Parameters { get; set; } = new();

        /// <summary>
        /// Lista de capas. El índice 0 es siempre la "Base Layer".
        /// Si está vacía, el runtime usa los campos legacy <see cref="States"/>
        /// y <see cref="DefaultState"/> como Base Layer implícita.
        /// </summary>
        [JsonPropertyName("layers")]
        public List<AnimatorLayerData> Layers { get; set; } = new();

        // ── Helpers ─────────────────────────────────────────────────────────

        /// <summary>
        /// Devuelve la capa en el índice dado, o null si no existe.
        /// </summary>
        public AnimatorLayerData GetLayer(int index)
            => (index >= 0 && index < Layers.Count) ? Layers[index] : null;

        /// <summary>
        /// Garantiza que exista al menos una capa (Base Layer).
        /// Migra los campos legacy si hace falta.
        /// Llamar desde el editor al abrir un asset.
        /// </summary>
        public void EnsureBaseLayer()
        {
            if (Layers.Count == 0)
            {
                Layers.Add(new AnimatorLayerData
                {
                    Name = "Base Layer",
                    Weight = 1f,
                    BlendingMode = LayerBlendingMode.Override,
                    DefaultState = _legacyDefaultState,
                    States = _legacyStates
                });
                // Limpiar legacy para evitar doble serialización
                _legacyDefaultState = "";
                _legacyStates = new();
            }
        }
    }
}