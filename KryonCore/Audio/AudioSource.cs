using KrayonCore.Audio;
using KrayonCore.Core.Attributes;
using KrayonCore.GraphicsData;
using OpenTK.Audio.OpenAL;
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

            // Update OpenAL source 3D position
            Vector3 objPos = GameObject.Transform.GetWorldPosition();
            ThisAudio.SetPosition(objPos.X, objPos.Y, objPos.Z);

            // Update OpenAL distance attenuation parameters
            int sourceId = ThisAudio.SourceId;
            AL.Source(sourceId, ALSourcef.Gain, Volume);
            AL.Source(sourceId, ALSourcef.ReferenceDistance, MathF.Max(MinDistance, 0.001f));
            AL.Source(sourceId, ALSourcef.MaxDistance, MathF.Max(MaxDistance, MinDistance + 0.001f));

            if (RolloffMode == AudioRolloffMode.Linear)
                AL.Source(sourceId, ALSourcef.RolloffFactor, 1f);
            else
                AL.Source(sourceId, ALSourcef.RolloffFactor, 1f);

            // Update listener from camera
            var camera = GraphicsEngine.Instance.GetSceneRenderer().GetCamera();
            GraphicsEngine.Instance.Audio.UpdateListener(
                camera.Position.X, camera.Position.Y, camera.Position.Z,
                camera.Front.X, camera.Front.Y, camera.Front.Z,
                camera.Up.X, camera.Up.Y, camera.Up.Z);
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
            ConfigureSpatial();
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
            ConfigureSpatial();
        }

        private void ConfigureSpatial()
        {
            if (ThisAudio == null || !Spatial) return;

            int sourceId = ThisAudio.SourceId;
            AL.Source(sourceId, ALSourcef.ReferenceDistance, MathF.Max(MinDistance, 0.001f));
            AL.Source(sourceId, ALSourcef.MaxDistance, MathF.Max(MaxDistance, MinDistance + 0.001f));
            AL.Source(sourceId, ALSourcef.RolloffFactor, 1f);

            Vector3 objPos = GameObject.Transform.GetWorldPosition();
            ThisAudio.SetPosition(objPos.X, objPos.Y, objPos.Z);
        }

        private AudioPlaySettings MakeSettings() => new AudioPlaySettings
        {
            Volume = Volume,
            Pitch = Pitch,
            Loop = Loop,
            Spatial = Spatial,
            InitialPan = 0f,
        };
    }

    public enum AudioRolloffMode
    {
        Linear,
        Logarithmic,
    }
}
