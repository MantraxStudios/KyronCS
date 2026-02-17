using OpenTK.Audio.OpenAL;
using NLayer;

namespace KrayonCore.Audio
{
    public class AudioManager : IDisposable
    {
        private readonly ALDevice _device;
        private readonly ALContext _context;
        private readonly List<AudioHandle> _activeHandles = new();
        private readonly object _lock = new();
        private bool _disposed;

        private float _masterVolume = 1f;
        public float MasterVolume
        {
            get => _masterVolume;
            set
            {
                _masterVolume = Math.Clamp(value, 0f, 1f);
                AL.Listener(ALListenerf.Gain, _masterVolume);
            }
        }

        public AudioManager(int sampleRate = 44100, int channelCount = 2)
        {
            _device = ALC.OpenDevice(null);
            if (_device == ALDevice.Null)
                throw new InvalidOperationException("Failed to open OpenAL audio device.");

            _context = ALC.CreateContext(_device, (int[])null!);
            if (_context == ALContext.Null)
                throw new InvalidOperationException("Failed to create OpenAL context.");

            ALC.MakeContextCurrent(_context);
            AL.Listener(ALListenerf.Gain, _masterVolume);
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

        public void StopAll()
        {
            lock (_lock)
            {
                foreach (var handle in _activeHandles.ToArray())
                    handle.Stop();
                _activeHandles.Clear();
            }
        }

        public void Pause()
        {
            lock (_lock)
            {
                foreach (var handle in _activeHandles)
                {
                    if (!handle.IsStopped)
                        AL.SourcePause(handle.SourceId);
                }
            }
        }

        public void Resume()
        {
            lock (_lock)
            {
                foreach (var handle in _activeHandles)
                {
                    if (!handle.IsStopped)
                        AL.SourcePlay(handle.SourceId);
                }
            }
        }

        // ── Pipeline ──────────────────────────────────────────────────────────────

        private AudioHandle PlayInternal(MemoryStream stream, AudioPlaySettings settings)
        {
            var (pcmData, sampleRate, channels, bitsPerSample) = DecodeAudio(stream);

            ALFormat format = GetALFormat(channels, bitsPerSample);

            int buffer = AL.GenBuffer();
            AL.BufferData(buffer, format, pcmData, sampleRate);

            int source = AL.GenSource();
            AL.Source(source, ALSourcei.Buffer, buffer);
            AL.Source(source, ALSourcef.Gain, Math.Clamp(settings.Volume, 0f, 1f));
            AL.Source(source, ALSourcef.Pitch, Math.Clamp(settings.Pitch, 0.5f, 2f)); // Support pitch
            AL.Source(source, ALSourceb.Looping, settings.Loop);

            if (settings.Spatial)
            {
                AL.Source(source, ALSourceb.SourceRelative, false);
                // Initial pan as X position
                AL.Source(source, ALSource3f.Position, settings.InitialPan, 0f, 0f);
            }
            else
            {
                // Non-spatial: source relative to listener at origin
                AL.Source(source, ALSourceb.SourceRelative, true);
                AL.Source(source, ALSource3f.Position, 0f, 0f, 0f);
            }

            AL.SourcePlay(source);

            var handle = new AudioHandle(source, buffer, settings.Spatial, this);

            lock (_lock)
                _activeHandles.Add(handle);

            return handle;
        }

        internal void RemoveHandle(AudioHandle handle)
        {
            lock (_lock)
                _activeHandles.Remove(handle);
        }

        // ── Listener Update (call each frame from game loop) ──────────────────────

        public void UpdateListener(float x, float y, float z,
                                    float forwardX, float forwardY, float forwardZ,
                                    float upX, float upY, float upZ)
        {
            AL.Listener(ALListener3f.Position, x, y, z);
            // OpenAL orientation: first 3 = "at" (forward), last 3 = "up"
            float[] orientation = { forwardX, forwardY, forwardZ, upX, upY, upZ };
            AL.Listener(ALListenerfv.Orientation, orientation);
        }

        // ── Audio Decoding ────────────────────────────────────────────────────────

        private static (byte[] pcmData, int sampleRate, int channels, int bitsPerSample) DecodeAudio(MemoryStream stream)
        {
            var hdr = new byte[4];
            _ = stream.Read(hdr, 0, 4);
            stream.Position = 0;

            // WAV (RIFF header)
            if (hdr[0] == 0x52 && hdr[1] == 0x49 && hdr[2] == 0x46 && hdr[3] == 0x46)
                return DecodeWav(stream);

            // MP3 (ID3v2 or sync frame)
            if ((hdr[0] == 0x49 && hdr[1] == 0x44 && hdr[2] == 0x33) ||
                (hdr[0] == 0xFF && (hdr[1] & 0xE0) == 0xE0))
                return DecodeMp3(stream);

            // AIFF (FORM header)
            if (hdr[0] == 0x46 && hdr[1] == 0x4F && hdr[2] == 0x52 && hdr[3] == 0x4D)
                return DecodeAiff(stream);

            throw new InvalidDataException("Unsupported audio format. Supported: WAV, MP3, AIFF.");
        }

        private static (byte[], int, int, int) DecodeWav(MemoryStream stream)
        {
            using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: false);

            // RIFF header
            reader.ReadBytes(4); // "RIFF"
            reader.ReadInt32();  // file size
            reader.ReadBytes(4); // "WAVE"

            int channels = 0, sampleRate = 0, bitsPerSample = 0;
            byte[] data = Array.Empty<byte>();

            while (stream.Position < stream.Length)
            {
                string chunkId;
                try
                {
                    chunkId = new string(reader.ReadChars(4));
                }
                catch { break; }

                int chunkSize = reader.ReadInt32();

                if (chunkId == "fmt ")
                {
                    int audioFormat = reader.ReadInt16();  // 1 = PCM, 3 = IEEE float
                    channels = reader.ReadInt16();
                    sampleRate = reader.ReadInt32();
                    reader.ReadInt32(); // byte rate
                    reader.ReadInt16(); // block align
                    bitsPerSample = reader.ReadInt16();

                    int remaining = chunkSize - 16;
                    if (remaining > 0) reader.ReadBytes(remaining);

                    // Convert IEEE float to PCM16
                    if (audioFormat == 3)
                        bitsPerSample = -bitsPerSample; // Mark as float for later conversion
                }
                else if (chunkId == "data")
                {
                    data = reader.ReadBytes(chunkSize);
                }
                else
                {
                    // Skip unknown chunk
                    if (chunkSize > 0 && stream.Position + chunkSize <= stream.Length)
                        reader.ReadBytes(chunkSize);
                    else
                        break;
                }
            }

            if (channels == 0 || sampleRate == 0)
                throw new InvalidDataException("Invalid WAV file: missing fmt chunk.");

            // Handle IEEE float WAV → convert to PCM16
            if (bitsPerSample < 0)
            {
                int floatBits = -bitsPerSample;
                data = ConvertFloatToPcm16(data, floatBits);
                bitsPerSample = 16;
            }

            return (data, sampleRate, channels, bitsPerSample);
        }

        private static byte[] ConvertFloatToPcm16(byte[] floatData, int bitsPerSample)
        {
            int bytesPerSample = bitsPerSample / 8;
            int sampleCount = floatData.Length / bytesPerSample;
            byte[] pcm16 = new byte[sampleCount * 2];

            for (int i = 0; i < sampleCount; i++)
            {
                float sample;
                if (bytesPerSample == 4)
                    sample = BitConverter.ToSingle(floatData, i * 4);
                else // 64-bit double
                    sample = (float)BitConverter.ToDouble(floatData, i * 8);

                sample = Math.Clamp(sample, -1f, 1f);
                short pcmSample = (short)(sample * 32767f);
                BitConverter.TryWriteBytes(pcm16.AsSpan(i * 2), pcmSample);
            }

            return pcm16;
        }

        private static (byte[], int, int, int) DecodeMp3(MemoryStream stream)
        {
            var mpegFile = new MpegFile(stream);
            int sampleRate = mpegFile.SampleRate;
            int channels = mpegFile.Channels;

            // Read all float samples
            var floatSamples = new List<float>();
            var readBuffer = new float[4096];
            int samplesRead;
            while ((samplesRead = mpegFile.ReadSamples(readBuffer, 0, readBuffer.Length)) > 0)
            {
                for (int i = 0; i < samplesRead; i++)
                    floatSamples.Add(readBuffer[i]);
            }

            // Convert float samples to PCM16
            byte[] pcm16 = new byte[floatSamples.Count * 2];
            for (int i = 0; i < floatSamples.Count; i++)
            {
                float s = Math.Clamp(floatSamples[i], -1f, 1f);
                short pcmSample = (short)(s * 32767f);
                BitConverter.TryWriteBytes(pcm16.AsSpan(i * 2), pcmSample);
            }

            return (pcm16, sampleRate, channels, 16);
        }

        private static (byte[], int, int, int) DecodeAiff(MemoryStream stream)
        {
            using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: false);

            reader.ReadBytes(4); // "FORM"
            reader.ReadInt32();  // file size (big-endian, but we don't need it)
            reader.ReadBytes(4); // "AIFF"

            int channels = 0, sampleRate = 0, bitsPerSample = 0;
            int numSampleFrames = 0;
            byte[] data = Array.Empty<byte>();

            while (stream.Position < stream.Length - 8)
            {
                string chunkId;
                try
                {
                    chunkId = new string(reader.ReadChars(4));
                }
                catch { break; }

                int chunkSize = ReadBigEndianInt32(reader);

                if (chunkId == "COMM")
                {
                    channels = ReadBigEndianInt16(reader);
                    numSampleFrames = ReadBigEndianInt32(reader);
                    bitsPerSample = ReadBigEndianInt16(reader);
                    // Sample rate is 80-bit extended float
                    sampleRate = (int)ReadIeee80(reader);

                    int remaining = chunkSize - 18;
                    if (remaining > 0) reader.ReadBytes(remaining);
                }
                else if (chunkId == "SSND")
                {
                    int offset = ReadBigEndianInt32(reader);
                    int blockSize = ReadBigEndianInt32(reader);
                    if (offset > 0) reader.ReadBytes(offset);

                    int dataSize = chunkSize - 8 - offset;
                    data = reader.ReadBytes(dataSize);
                }
                else
                {
                    long skip = chunkSize;
                    if (chunkSize % 2 != 0) skip++; // AIFF chunks are padded to even
                    if (stream.Position + skip <= stream.Length)
                        reader.ReadBytes((int)skip);
                    else
                        break;
                }
            }

            if (channels == 0 || sampleRate == 0)
                throw new InvalidDataException("Invalid AIFF file: missing COMM chunk.");

            // AIFF data is big-endian — convert to little-endian PCM16
            if (bitsPerSample == 16)
            {
                for (int i = 0; i < data.Length - 1; i += 2)
                    (data[i], data[i + 1]) = (data[i + 1], data[i]);
            }
            else if (bitsPerSample == 24)
            {
                // Convert 24-bit big-endian to 16-bit little-endian
                int sampleCount = data.Length / 3;
                byte[] pcm16 = new byte[sampleCount * 2];
                for (int i = 0; i < sampleCount; i++)
                {
                    // Big-endian 24-bit: take top 2 bytes
                    short s = (short)((data[i * 3] << 8) | data[i * 3 + 1]);
                    BitConverter.TryWriteBytes(pcm16.AsSpan(i * 2), s);
                }
                data = pcm16;
                bitsPerSample = 16;
            }
            else if (bitsPerSample == 8)
            {
                // 8-bit AIFF is signed, OpenAL expects unsigned for 8-bit
                for (int i = 0; i < data.Length; i++)
                    data[i] = (byte)(data[i] + 128);
            }

            return (data, sampleRate, channels, bitsPerSample);
        }

        private static short ReadBigEndianInt16(BinaryReader reader)
        {
            byte[] b = reader.ReadBytes(2);
            return (short)((b[0] << 8) | b[1]);
        }

        private static int ReadBigEndianInt32(BinaryReader reader)
        {
            byte[] b = reader.ReadBytes(4);
            return (b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3];
        }

        private static double ReadIeee80(BinaryReader reader)
        {
            byte[] b = reader.ReadBytes(10);
            int exponent = ((b[0] & 0x7F) << 8) | b[1];
            long mantissa = 0;
            for (int i = 2; i < 10; i++)
                mantissa = (mantissa << 8) | b[i];

            double f = mantissa / (double)(1L << 63);
            f = Math.Pow(2, exponent - 16383) * f;
            if ((b[0] & 0x80) != 0) f = -f;
            return f;
        }

        private static ALFormat GetALFormat(int channels, int bitsPerSample)
        {
            return (channels, bitsPerSample) switch
            {
                (1, 8) => ALFormat.Mono8,
                (1, 16) => ALFormat.Mono16,
                (2, 8) => ALFormat.Stereo8,
                (2, 16) => ALFormat.Stereo16,
                _ => throw new InvalidDataException($"Unsupported audio format: {channels}ch {bitsPerSample}bit")
            };
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            StopAll();

            if (_context != ALContext.Null)
            {
                ALC.MakeContextCurrent(ALContext.Null);
                ALC.DestroyContext(_context);
            }

            if (_device != ALDevice.Null)
                ALC.CloseDevice(_device);
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

    public class AudioHandle
    {
        private readonly AudioManager _manager;
        private bool _stopped;

        internal int SourceId { get; }
        internal int BufferId { get; }
        internal bool IsSpatial { get; }

        public bool IsStopped
        {
            get
            {
                if (_stopped) return true;
                // Check if OpenAL source has finished playing naturally
                AL.GetSource(SourceId, ALGetSourcei.SourceState, out int state);
                if (state == (int)ALSourceState.Stopped)
                {
                    Cleanup();
                    return true;
                }
                return false;
            }
        }

        public float Volume
        {
            get
            {
                if (_stopped) return 0f;
                AL.GetSource(SourceId, ALSourcef.Gain, out float v);
                return v;
            }
            set
            {
                if (!_stopped)
                    AL.Source(SourceId, ALSourcef.Gain, Math.Clamp(value, 0f, 1f));
            }
        }

        public float Pitch
        {
            get
            {
                if (_stopped) return 1f;
                AL.GetSource(SourceId, ALSourcef.Pitch, out float p);
                return p;
            }
            set
            {
                if (!_stopped)
                    AL.Source(SourceId, ALSourcef.Pitch, Math.Clamp(value, 0.5f, 2f));
            }
        }

        public float Pan
        {
            get
            {
                if (_stopped || !IsSpatial) return 0f;
                AL.GetSource(SourceId, ALSource3f.Position, out float x, out _, out _);
                return x;
            }
            set
            {
                if (!_stopped && IsSpatial)
                    AL.Source(SourceId, ALSource3f.Position, Math.Clamp(value, -1f, 1f), 0f, 0f);
            }
        }

        internal AudioHandle(int sourceId, int bufferId, bool isSpatial, AudioManager manager)
        {
            SourceId = sourceId;
            BufferId = bufferId;
            IsSpatial = isSpatial;
            _manager = manager;
        }

        public void Stop()
        {
            if (_stopped) return;
            AL.SourceStop(SourceId);
            Cleanup();
        }

        private void Cleanup()
        {
            if (_stopped) return;
            _stopped = true;
            AL.DeleteSource(SourceId);
            AL.DeleteBuffer(BufferId);
            _manager.RemoveHandle(this);
        }

        /// <summary>
        /// Set full 3D position for spatial audio sources.
        /// </summary>
        public void SetPosition(float x, float y, float z)
        {
            if (!_stopped && IsSpatial)
                AL.Source(SourceId, ALSource3f.Position, x, y, z);
        }
    }
}