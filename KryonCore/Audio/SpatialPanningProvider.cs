using NAudio.Wave;

namespace KrayonCore.Audio
{
    public class SpatialPanningProvider : ISampleProvider
    {
        private readonly ISampleProvider _monoSource;

        private volatile float _leftGain = 0.7071f;
        private volatile float _rightGain = 0.7071f;
        private volatile float _volume = 1f;

        private float _pan = 0f;

        private float[] _monoBuffer = Array.Empty<float>();

        public WaveFormat WaveFormat { get; }

        public float Volume
        {
            get => _volume;
            set => _volume = Math.Clamp(value, 0f, 1f);
        }

        public float Pan
        {
            get => _pan;
            set
            {
                _pan = Math.Clamp(value, -1f, 1f);
                float angle = (_pan + 1f) * 0.5f * MathF.PI * 0.5f;
                float newLeft = MathF.Cos(angle);
                float newRight = MathF.Sin(angle);
                _rightGain = newRight;
                _leftGain = newLeft;
            }
        }

        public SpatialPanningProvider(ISampleProvider monoSource)
        {
            if (monoSource.WaveFormat.Channels != 1)
                throw new ArgumentException(
                    "SpatialPanningProvider requires a mono source.",
                    nameof(monoSource));

            _monoSource = monoSource;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(monoSource.WaveFormat.SampleRate, 2);
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int monoNeeded = count / 2;

            if (_monoBuffer.Length < monoNeeded)
                _monoBuffer = new float[monoNeeded];

            int monoRead = _monoSource.Read(_monoBuffer, 0, monoNeeded);
            if (monoRead == 0) return 0;

            float vol = _volume;
            float left = _leftGain * vol;
            float right = _rightGain * vol;

            int dst = offset;
            for (int i = 0; i < monoRead; i++)
            {
                float s = _monoBuffer[i];
                buffer[dst++] = s * left;
                buffer[dst++] = s * right;
            }

            return monoRead * 2;
        }
    }
}