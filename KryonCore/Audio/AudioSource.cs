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
        [NoSerializeToInspector] private float _volume = 1f;
        [NoSerializeToInspector] private float _pitch = 1f;
        [NoSerializeToInspector] private bool _loop = false;
        [NoSerializeToInspector] private bool _spatial = false;
        [NoSerializeToInspector] private float _minDistance = 1f;
        [NoSerializeToInspector] private float _maxDistance = 20f;
        [NoSerializeToInspector] private AudioRolloffMode _rolloffMode = AudioRolloffMode.Logarithmic;

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

        [ToStorage]
        public float Volume
        {
            get => _volume;
            set
            {
                _volume = value;
                if (ThisAudio != null && !ThisAudio.IsStopped)
                    ThisAudio.Volume = value;
            }
        }

        [ToStorage]
        public float Pitch
        {
            get => _pitch;
            set
            {
                _pitch = value;
                if (ThisAudio != null && !ThisAudio.IsStopped)
                    ThisAudio.Pitch = value;
            }
        }

        [ToStorage]
        public bool Loop
        {
            get => _loop;
            set
            {
                _loop = value;
                if (ThisAudio != null && !ThisAudio.IsStopped)
                    AL.Source(ThisAudio.SourceId, ALSourceb.Looping, value);
            }
        }

        [ToStorage]
        public bool Spatial
        {
            get => _spatial;
            set
            {
                if (_spatial == value) return;
                _spatial = value;
                if (_started && ThisAudio != null && !ThisAudio.IsStopped)
                {
                    ReconfigureSpatial();
                }
            }
        }

        [ToStorage]
        public float MinDistance
        {
            get => _minDistance;
            set
            {
                _minDistance = value;
                UpdateSpatialParameters();
            }
        }

        [ToStorage]
        public float MaxDistance
        {
            get => _maxDistance;
            set
            {
                _maxDistance = value;
                UpdateSpatialParameters();
            }
        }

        [ToStorage]
        public AudioRolloffMode RolloffMode
        {
            get => _rolloffMode;
            set
            {
                _rolloffMode = value;
                UpdateSpatialParameters();
            }
        }

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

            // Update listener from camera
            var camera = GraphicsEngine.Instance.GetSceneRenderer().GetCamera();
            GraphicsEngine.Instance.Audio.UpdateListener(
                camera.Position.X, camera.Position.Y, camera.Position.Z,
                camera.Front.X, camera.Front.Y, camera.Front.Z,
                camera.Up.X, camera.Up.Y, camera.Up.Z);

            if (!Spatial)
            {
                // Ensure source is relative to listener
                int sourceId = ThisAudio.SourceId;
                AL.Source(sourceId, ALSourceb.SourceRelative, true);
                AL.Source(sourceId, ALSource3f.Position, 0f, 0f, 0f);
                return;
            }

            // Update 3D position for spatial audio
            Vector3 objPos = GameObject.Transform.GetWorldPosition();
            ThisAudio.SetPosition(objPos.X, objPos.Y, objPos.Z);

            // Update spatial parameters
            UpdateSpatialParameters();
        }

        private void UpdateSpatialParameters()
        {
            if (ThisAudio == null || ThisAudio.IsStopped || !Spatial) return;

            int sourceId = ThisAudio.SourceId;

            // Ensure source is NOT relative to listener (world space)
            AL.Source(sourceId, ALSourceb.SourceRelative, false);

            // Set distance attenuation
            AL.Source(sourceId, ALSourcef.ReferenceDistance, MathF.Max(MinDistance, 0.001f));
            AL.Source(sourceId, ALSourcef.MaxDistance, MathF.Max(MaxDistance, MinDistance + 0.001f));

            // Configure rolloff based on mode
            if (RolloffMode == AudioRolloffMode.Linear)
            {
                // Linear rolloff: sound decreases linearly with distance
                AL.Source(sourceId, ALSourcef.RolloffFactor, 1f);
                // For linear model, you might need to use AL_LINEAR_DISTANCE model
                // but OpenAL-Soft uses inverse distance by default
            }
            else
            {
                // Logarithmic/Inverse rolloff: more realistic (default in OpenAL)
                // RolloffFactor controls how quickly sound attenuates
                // Higher values = faster attenuation
                AL.Source(sourceId, ALSourcef.RolloffFactor, 1f);
            }
        }

        private void ReconfigureSpatial()
        {
            if (ThisAudio == null || ThisAudio.IsStopped) return;

            int sourceId = ThisAudio.SourceId;

            if (Spatial)
            {
                // Enable 3D spatial audio
                AL.Source(sourceId, ALSourceb.SourceRelative, false);
                Vector3 objPos = GameObject.Transform.GetWorldPosition();
                ThisAudio.SetPosition(objPos.X, objPos.Y, objPos.Z);
                UpdateSpatialParameters();
            }
            else
            {
                // Disable spatial, make it 2D
                AL.Source(sourceId, ALSourceb.SourceRelative, true);
                AL.Source(sourceId, ALSource3f.Position, 0f, 0f, 0f);
            }
        }

        public void Play()
        {
            if (!AppInfo.IsCompiledGame) return;

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
            if (!AppInfo.IsCompiledGame) return;

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
            AL.Source(sourceId, ALSourceb.SourceRelative, false);
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