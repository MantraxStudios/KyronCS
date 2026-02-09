using OpenTK.Mathematics;

namespace KrayonCore.Core.Rendering
{
    public class PostProcessingSettings
    {
        public bool Enabled { get; set; } = true;

        public bool ColorCorrectionEnabled { get; set; } = true;
        public float Brightness { get; set; } = 0.0f;
        public float Contrast { get; set; } = 1.0f;
        public float Saturation { get; set; } = 1.0f;
        public Vector3 ColorFilter { get; set; } = new Vector3(1.0f, 1.0f, 1.0f);

        public bool BloomEnabled { get; set; } = true;
        public float BloomThreshold { get; set; } = 0.9f;
        public float BloomSoftThreshold { get; set; } = 0.5f;
        public float BloomIntensity { get; set; } = 0.8f;
        public float BloomRadius { get; set; } = 4.0f;

        public bool GrainEnabled { get; set; } = true;
        public float GrainIntensity { get; set; } = 0.05f;
        public float GrainSize { get; set; } = 1.0f;

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
        }
    }
}