using KrayonCore.Audio;
using KrayonCore.Core.Attributes;
using KrayonCore.GraphicsData;
using OpenTK.Mathematics;

namespace KrayonCore.Core.Components
{
    public class AudioSource : Component
    {
        [NoSerializeToInspector] private string _audioPath = "";

        [ToStorage]
        public string AudioPath
        {
            get => _audioPath;
            set
            {
                if (_audioPath == value) return;
                _audioPath = value;
                if (_started) LoadAudio();
            }
        }

        [ToStorage] public float Volume { get; set; } = 1f;
        [ToStorage] public float Pitch { get; set; } = 1f;
        [ToStorage] public bool Loop { get; set; } = false;

        [ToStorage]
        public bool Spatial
        {
            get => _spatial;
            set
            {
                if (_spatial == value) return;
                _spatial = value;
                if (_started) LoadAudio();
            }
        }
        private bool _spatial = false;

        [ToStorage] public float MinDistance { get; set; } = 1f;
        [ToStorage] public float MaxDistance { get; set; } = 20f;
        [ToStorage] public AudioRolloffMode RolloffMode { get; set; } = AudioRolloffMode.Logarithmic;
        [ToStorage] public float StereoPanBlend { get; set; } = 1f;

        [ToStorage] public bool PlayOnAwake { get; set; } = true;


        public AudioHandle? ThisAudio = null;
        private bool _started = false;

        public override void Start()
        {
            _started = true;
            if (PlayOnAwake && !string.IsNullOrEmpty(_audioPath))
                LoadAudio();
        }

        public override void OnWillRenderObject()
        {
            if (ThisAudio == null || ThisAudio.IsStopped) return;

            if (!Spatial)
            {
                ThisAudio.Volume = Volume;
                return;
            }

            var camera = GraphicsEngine.Instance.GetSceneRenderer().GetCamera();
            Vector3 objPos = GameObject.Transform.GetWorldPosition();
            Vector3 camPos = camera.Position;
            Vector3 toObj = objPos - camPos;
            float dist = toObj.Length;

            ThisAudio.Volume = Volume * Attenuate(dist);

            if (dist < 0.001f)
            {
                ThisAudio.Pan = 0f;
                return;
            }

            Vector3 dir = toObj / dist;
            float pan = Vector3.Dot(dir, camera.Right);

            float forwardDot = Vector3.Dot(dir, camera.Front);
            if (forwardDot < 0f)
                pan = MathHelper.Lerp(pan, 0f, -forwardDot * 0.5f);

            ThisAudio.Pan = Math.Clamp(pan * StereoPanBlend, -1f, 1f);
        }

        public void Play()
        {
            Stop();
            if (string.IsNullOrEmpty(_audioPath)) return;

            var bytes = AssetManager.GetBytes(Guid.Parse(_audioPath));
            if (bytes == null || bytes.Length == 0)
            {
                Console.WriteLine($"[AudioSource] Missing audio data for: {_audioPath}");
                return;
            }

            ThisAudio = GraphicsEngine.Instance.Audio.Play(bytes, MakeSettings());
        }

        public void Stop()
        {
            ThisAudio?.Stop();
            ThisAudio = null;
        }

        public bool IsPlaying => ThisAudio != null && !ThisAudio.IsStopped;

        private void LoadAudio()
        {
            Stop();
            if (string.IsNullOrEmpty(_audioPath)) return;

            var bytes = AssetManager.GetBytes(Guid.Parse(_audioPath));
            if (bytes == null || bytes.Length == 0)
            {
                Console.WriteLine($"[AudioSource] Missing audio data for: {_audioPath}");
                return;
            }

            ThisAudio = GraphicsEngine.Instance.Audio.Play(bytes, MakeSettings());
        }

        private AudioPlaySettings MakeSettings() => new AudioPlaySettings
        {
            Volume = Volume,
            Pitch = Pitch,
            Loop = Loop,
            Spatial = Spatial,
            InitialPan = 0f,
        };

        private float Attenuate(float distance)
        {
            float min = MathF.Max(MinDistance, 0.001f);
            float max = MathF.Max(MaxDistance, min + 0.001f);

            if (distance <= min) return 1f;
            if (distance >= max) return 0f;

            return RolloffMode switch
            {
                AudioRolloffMode.Linear => 1f - (distance - min) / (max - min),
                AudioRolloffMode.Logarithmic => Math.Clamp(min / distance, 0f, 1f),
                _ => 1f - (distance - min) / (max - min),
            };
        }
    }

    public enum AudioRolloffMode
    {
        Linear,
        Logarithmic,
    }
}