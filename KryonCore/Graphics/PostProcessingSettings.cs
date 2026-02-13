using KrayonCore.Core.Attributes;
using OpenTK.Mathematics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KrayonCore.Core.Rendering
{
    public class Vector3Converter : JsonConverter<Vector3>
    {
        public override Vector3 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                return Vector3.One;

            float x = 1.0f, y = 1.0f, z = 1.0f;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    break;

                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    string propertyName = reader.GetString() ?? "";
                    reader.Read();

                    switch (propertyName)
                    {
                        case "X":
                            x = reader.GetSingle();
                            break;
                        case "Y":
                            y = reader.GetSingle();
                            break;
                        case "Z":
                            z = reader.GetSingle();
                            break;
                    }
                }
            }

            return new Vector3(x, y, z);
        }

        public override void Write(Utf8JsonWriter writer, Vector3 value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteNumber("X", value.X);
            writer.WriteNumber("Y", value.Y);
            writer.WriteNumber("Z", value.Z);
            writer.WriteEndObject();
        }
    }

    public class PostProcessingSettings
    {
        public bool Enabled { get; set; } = true;

        public bool ColorCorrectionEnabled { get; set; } = true;
        public float Brightness { get; set; } = 0.0f;
        public float Contrast { get; set; } = 1.0f;
        public float Saturation { get; set; } = 1.0f;
        
        [JsonConverter(typeof(Vector3Converter))]
        public Vector3 ColorFilter { get; set; } = new Vector3(1.0f, 1.0f, 1.0f);

        public bool BloomEnabled { get; set; } = true;
        public float BloomThreshold { get; set; } = 0.9f;
        public float BloomSoftThreshold { get; set; } = 0.5f;
        public float BloomIntensity { get; set; } = 0.8f;
        public float BloomRadius { get; set; } = 4.0f;

        public bool GrainEnabled { get; set; } = true;
        public float GrainIntensity { get; set; } = 0.05f;
        public float GrainSize { get; set; } = 1.0f;

        public bool SSAOEnabled { get; set; } = true;
        public int SSAOKernelSize { get; set; } = 64;
        public float SSAORadius { get; set; } = 0.5f;
        public float SSAOBias { get; set; } = 0.025f;
        public float SSAOPower { get; set; } = 2.0f;

        public PostProcessingSettings()
        {
        }

        public void Reset()
        {
            Enabled = true;
            
            ColorCorrectionEnabled = true;
            Brightness = 0.0f;
            Contrast = 1.0f;
            Saturation = 1.0f;
            ColorFilter = new Vector3(1.0f, 1.0f, 1.0f);
            
            BloomEnabled = true;
            BloomThreshold = 0.9f;
            BloomSoftThreshold = 0.5f;
            BloomIntensity = 0.8f;
            BloomRadius = 4.0f;
            
            GrainEnabled = true;
            GrainIntensity = 0.05f;
            GrainSize = 1.0f;

            SSAOEnabled = true;
            SSAOKernelSize = 64;
            SSAORadius = 0.5f;
            SSAOBias = 0.025f;
            SSAOPower = 2.0f;
        }

        public void Save(string filePath)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            string json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(filePath, json);
        }

        public void Load(string filePath)
        {
            string json;

            if (AppInfo.IsCompiledGame)
            {
                // Leer desde Pak
                byte[] bytes = AssetManager.GetBytes("Engine.VFX");
                if (bytes == null)
                {
                    Console.WriteLine("[PostProcessing] Could not read settings from pak");
                    Reset();
                    return;
                }

                json = Encoding.UTF8.GetString(bytes);
            }
            else
            {
                // Leer desde filesystem
                if (!File.Exists(filePath))
                {
                    Reset();
                    return;
                }

                json = File.ReadAllText(filePath);
            }

            var loaded = JsonSerializer.Deserialize<PostProcessingSettings>(json);

            if (loaded != null)
            {
                Enabled = loaded.Enabled;
                ColorCorrectionEnabled = loaded.ColorCorrectionEnabled;
                Brightness = loaded.Brightness;
                Contrast = loaded.Contrast;
                Saturation = loaded.Saturation;
                ColorFilter = loaded.ColorFilter;
                BloomEnabled = loaded.BloomEnabled;
                BloomThreshold = loaded.BloomThreshold;
                BloomSoftThreshold = loaded.BloomSoftThreshold;
                BloomIntensity = loaded.BloomIntensity;
                BloomRadius = loaded.BloomRadius;
                GrainEnabled = loaded.GrainEnabled;
                GrainIntensity = loaded.GrainIntensity;
                GrainSize = loaded.GrainSize;
                SSAOEnabled = loaded.SSAOEnabled;
                SSAOKernelSize = loaded.SSAOKernelSize;
                SSAORadius = loaded.SSAORadius;
                SSAOBias = loaded.SSAOBias;
                SSAOPower = loaded.SSAOPower;
            }
        }
    }
}