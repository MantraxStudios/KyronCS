using System.Text.Json.Serialization;

namespace KrayonCore.Core.Attributes
{
    public class EngineData
    {
        [JsonPropertyName("defaultScene")]
        public string DefaultScene { get; set; } = "/DefaultScene.scene";
    }
}