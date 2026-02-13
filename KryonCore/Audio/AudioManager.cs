using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace KrayonCore.Audio
{
    /// <summary>
    /// Pipeline (spatial):
    ///   WaveStream → [Loop] → [Pitch] → EnsureMono → Resample → SpatialPanningProvider → Mixer
    ///
    /// Pipeline (non-spatial):
    ///   WaveStream → [Loop] → [Pitch] → ChannelConvert → Resample → VolumeSampleProvider → Mixer
    ///
    /// For spatial sounds, SpatialPanningProvider owns both Volume and Pan.
    /// VolumeSampleProvider is intentionally NOT placed on top of SpatialPanningProvider
    /// because it does not correctly forward a stereo WaveFormat from a custom provider,
    /// which makes NAudio treat the output as mono and kills the pan effect entirely.
    /// </summary>
    public class AudioManager : IDisposable
    {
        private readonly IWavePlayer _outputDevice;
        private readonly MixingSampleProvider _mixer;
        private bool _disposed;

        public float MasterVolume
        {
            get => _outputDevice.Volume;
            set => _outputDevice.Volume = Math.Clamp(value, 0f, 1f);
        }

        public AudioManager(int sampleRate = 44100, int channelCount = 2)
        {
            _outputDevice = new WaveOutEvent();

            var mixerFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channelCount);
            _mixer = new MixingSampleProvider(mixerFormat) { ReadFully = true };

            _outputDevice.Init(_mixer);
            _outputDevice.Play();
        }

        public AudioHandle Play(byte[] audioData, AudioPlaySettings? settings = null)
        {
            if (audioData == null || audioData.Length == 0)
                throw new ArgumentException("Audio data is empty.", nameof(audioData));

            return PlayInternal(new MemoryStream(audioData), settings ?? new AudioPlaySettings());
        }

        public AudioHandle Play(byte[] audioData, float volume = 1f)
            => Play(audioData, new AudioPlaySettings { Volume = volume });

        public AudioHandle Play(Stream stream, AudioPlaySettings? settings = null)
        {
            MemoryStream mem;
            if (stream is MemoryStream ms)
            {
                mem = ms;
            }
            else
            {
                mem = new MemoryStream();
                stream.CopyTo(mem);
                mem.Position = 0;
            }
            return PlayInternal(mem, settings ?? new AudioPlaySettings());
        }

        public void Stop(AudioHandle handle) => handle.Stop();
        public void StopAll() => _mixer.RemoveAllMixerInputs();
        public void Pause() => _outputDevice.Pause();
        public void Resume() => _outputDevice.Play();

        // ── Pipeline ──────────────────────────────────────────────────────────────

        private AudioHandle PlayInternal(MemoryStream stream, AudioPlaySettings settings)
        {
            var (waveStream, provider) = OpenStream(stream, settings.Loop);

            ISampleProvider pipeline = provider;

            // 1. Pitch shift via sample-rate trick
            if (MathF.Abs(settings.Pitch - 1f) > 0.001f)
            {
                int pitchedRate = Math.Clamp((int)(pipeline.WaveFormat.SampleRate * settings.Pitch), 8000, 192000);
                pipeline = new WdlResamplingSampleProvider(pipeline, pitchedRate);
            }

            if (settings.Spatial)
            {
                // 2. Collapse to mono
                if (pipeline.WaveFormat.Channels != 1)
                    pipeline = new StereoToMonoSampleProvider(pipeline);

                // 3. Resample mono to mixer rate
                if (pipeline.WaveFormat.SampleRate != _mixer.WaveFormat.SampleRate)
                    pipeline = new WdlResamplingSampleProvider(pipeline, _mixer.WaveFormat.SampleRate);

                // 4. Pan + volume in one node — no wrapper on top
                var panner = new SpatialPanningProvider(pipeline)
                {
                    Volume = Math.Clamp(settings.Volume, 0f, 1f),
                    Pan = settings.InitialPan,
                };

                _mixer.AddMixerInput(panner);

                return new AudioHandle(panner, waveStream, () => _mixer.RemoveMixerInput(panner));
            }
            else
            {
                // Non-spatial: standard conversion then VolumeSampleProvider
                if (pipeline.WaveFormat.Channels == 1 && _mixer.WaveFormat.Channels == 2)
                    pipeline = new MonoToStereoSampleProvider(pipeline);
                else if (pipeline.WaveFormat.Channels == 2 && _mixer.WaveFormat.Channels == 1)
                    pipeline = new StereoToMonoSampleProvider(pipeline);

                if (pipeline.WaveFormat.SampleRate != _mixer.WaveFormat.SampleRate)
                    pipeline = new WdlResamplingSampleProvider(pipeline, _mixer.WaveFormat.SampleRate);

                var vol = new VolumeSampleProvider(pipeline)
                {
                    Volume = Math.Clamp(settings.Volume, 0f, 1f)
                };

                _mixer.AddMixerInput(vol);

                return new AudioHandle(vol, waveStream, () => _mixer.RemoveMixerInput(vol));
            }
        }

        private static (WaveStream, ISampleProvider) OpenStream(MemoryStream stream, bool loop)
        {
            var hdr = new byte[4];
            _ = stream.Read(hdr, 0, 4);
            stream.Position = 0;

            WaveStream reader;
            if (hdr[0] == 0x52 && hdr[1] == 0x49 && hdr[2] == 0x46 && hdr[3] == 0x46) reader = new WaveFileReader(stream);
            else if (hdr[0] == 0x49 && hdr[1] == 0x44 && hdr[2] == 0x33) reader = new Mp3FileReader(stream);
            else if (hdr[0] == 0xFF && (hdr[1] & 0xE0) == 0xE0) reader = new Mp3FileReader(stream);
            else if (hdr[0] == 0x46 && hdr[1] == 0x4F && hdr[2] == 0x52 && hdr[3] == 0x4D) reader = new AiffFileReader(stream);
            else throw new InvalidDataException("Unsupported audio format. Supported: WAV, MP3, AIFF.");

            ISampleProvider provider = loop ? new LoopingSampleProvider(reader) : reader.ToSampleProvider();
            return (reader, provider);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _outputDevice.Stop();
            _outputDevice.Dispose();
        }
    }

    public class AudioPlaySettings
    {
        public float Volume { get; set; } = 1f;
        public float Pitch { get; set; } = 1f;
        public bool Loop { get; set; } = false;
        public bool Spatial { get; set; } = false;
        public float InitialPan { get; set; } = 0f;
    }

    /// <summary>
    /// Handle for a playing sound. Wraps either a SpatialPanningProvider (spatial)
    /// or a VolumeSampleProvider (non-spatial) through a common interface.
    /// </summary>
    public class AudioHandle
    {
        // One of these two is set, not both
        private readonly SpatialPanningProvider? _spatial;
        private readonly VolumeSampleProvider? _flat;

        private readonly WaveStream _waveStream;
        private readonly Action _removeFromMixer;
        private bool _stopped;

        public bool IsStopped => _stopped;

        /// <summary>Volume (0–1). For spatial sounds, also controls distance-attenuated volume.</summary>
        public float Volume
        {
            get => _spatial != null ? _spatial.Volume : (_flat?.Volume ?? 0f);
            set
            {
                if (_spatial != null) _spatial.Volume = value;
                else if (_flat != null) _flat.Volume = Math.Clamp(value, 0f, 1f);
            }
        }

        /// <summary>
        /// Stereo pan [-1…+1]. Only has effect on spatial sounds.
        /// Written from the game thread, read by the NAudio thread — safe for float.
        /// </summary>
        public float Pan
        {
            get => _spatial?.Pan ?? 0f;
            set { if (_spatial != null) _spatial.Pan = value; }
        }

        // Spatial constructor
        internal AudioHandle(SpatialPanningProvider panner, WaveStream ws, Action remove)
        {
            _spatial = panner;
            _waveStream = ws;
            _removeFromMixer = remove;
        }

        // Non-spatial constructor
        internal AudioHandle(VolumeSampleProvider vol, WaveStream ws, Action remove)
        {
            _flat = vol;
            _waveStream = ws;
            _removeFromMixer = remove;
        }

        public void Stop()
        {
            if (_stopped) return;
            _stopped = true;
            _removeFromMixer();
            _waveStream?.Dispose();
        }
    }
}