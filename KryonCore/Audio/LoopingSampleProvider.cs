using NAudio.Wave;

namespace KrayonCore.Audio
{
    /// <summary>
    /// Wraps any ISampleProvider and loops it indefinitely.
    /// The underlying stream must support seeking (MemoryStream-backed readers do).
    /// </summary>
    public class LoopingSampleProvider : ISampleProvider
    {
        private readonly WaveStream _waveStream;
        private readonly ISampleProvider _sourceProvider;

        public WaveFormat WaveFormat => _sourceProvider.WaveFormat;

        public LoopingSampleProvider(WaveStream waveStream)
        {
            _waveStream = waveStream;
            _sourceProvider = waveStream.ToSampleProvider();
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int totalRead = 0;

            while (totalRead < count)
            {
                int read = _sourceProvider.Read(buffer, offset + totalRead, count - totalRead);

                if (read == 0)
                {
                    // End of stream — seek back to start and loop
                    if (_waveStream.CanSeek)
                        _waveStream.Position = 0;
                    else
                        break; // Can't loop without seek support
                }
                else
                {
                    totalRead += read;
                }
            }

            return totalRead;
        }
    }
}