using KrayonCore.Core.Attributes;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;

namespace KrayonCore.Audio
{
    public static class AudioEngine
    {
        private static IWavePlayer outputDevice;
        private static AudioFileReader audioFile;
        private static ISampleProvider sampleProvider;
        private static PanningSampleProvider panProviderMono;  // Para audio mono
        private static StereoPanProvider panProviderStereo;    // Para audio estéreo
        private static VolumeSampleProvider volumeProvider;
        private static bool loop;

        public static event Action PlaybackFinished;

        public static AudioFileReader Play(string rutaArchivo, bool loop = false)
        {
            Stop();
            AudioEngine.loop = loop;

            try
            {
                audioFile = new AudioFileReader(AssetManager.BasePath + rutaArchivo);
                sampleProvider = audioFile;

                Console.WriteLine($"Canales de audio: {sampleProvider.WaveFormat.Channels}");

                // ============================================
                // PROCESAMIENTO DE AUDIO: ORDEN CORRECTO
                // ============================================

                // 1. Si tiene más de 2 canales, reducir a estéreo primero
                if (sampleProvider.WaveFormat.Channels > 2)
                {
                    sampleProvider = new MultichannelToStereoProvider(sampleProvider);
                }

                // 2. Aplicar VOLUMEN (funciona con cualquier número de canales)
                volumeProvider = new VolumeSampleProvider(sampleProvider)
                {
                    Volume = 1f
                };

                // 3. Aplicar PAN según el tipo de audio
                if (volumeProvider.WaveFormat.Channels == 1)
                {
                    // MONO: usar PanningSampleProvider nativo (convierte mono→estéreo automáticamente)
                    panProviderMono = new PanningSampleProvider(volumeProvider)
                    {
                        Pan = 0f
                    };
                    sampleProvider = panProviderMono;
                    panProviderStereo = null;
                    Console.WriteLine("Usando PanningSampleProvider para MONO");
                }
                else if (volumeProvider.WaveFormat.Channels == 2)
                {
                    // ESTÉREO: usar nuestro proveedor personalizado
                    panProviderStereo = new StereoPanProvider(volumeProvider)
                    {
                        Pan = 0f
                    };
                    sampleProvider = panProviderStereo;
                    panProviderMono = null;
                    Console.WriteLine("Usando StereoPanProvider para ESTÉREO");
                }
                else
                {
                    // Más de 2 canales: sin pan
                    sampleProvider = volumeProvider;
                    panProviderMono = null;
                    panProviderStereo = null;
                }

                outputDevice = new WaveOutEvent();
                outputDevice.Init(sampleProvider);
                outputDevice.PlaybackStopped += OnPlaybackStopped;
                outputDevice.Play();

                Console.WriteLine("Audio iniciado correctamente");
                return audioFile;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al reproducir audio: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
                return null;
            }
        }

        private static void OnPlaybackStopped(object sender, StoppedEventArgs e)
        {
            if (loop && audioFile != null)
            {
                audioFile.Position = 0;
                outputDevice?.Play();
                return;
            }

            PlaybackFinished?.Invoke();
        }

        public static void Pause()
        {
            if (outputDevice?.PlaybackState == PlaybackState.Playing)
                outputDevice.Pause();
        }

        public static void Resume()
        {
            if (outputDevice?.PlaybackState == PlaybackState.Paused)
                outputDevice.Play();
        }

        public static void Stop()
        {
            outputDevice?.Stop();
            outputDevice?.Dispose();
            audioFile?.Dispose();

            outputDevice = null;
            audioFile = null;
            sampleProvider = null;
            panProviderMono = null;
            panProviderStereo = null;
            volumeProvider = null;
        }

        public static void SetVolume(float volume)
        {
            volume = Math.Clamp(volume, 0f, 1f);

            if (volumeProvider != null)
                volumeProvider.Volume = volume;
        }

        public static void SetPan(float pan)
        {
            pan = Math.Clamp(pan, -1f, 1f);

            // Aplicar a cualquiera que esté activo
            if (panProviderMono != null)
            {
                panProviderMono.Pan = pan;
                Console.WriteLine($"Pan (Mono): {pan:F2}");
            }
            else if (panProviderStereo != null)
            {
                panProviderStereo.Pan = pan;
                Console.WriteLine($"Pan (Stereo): {pan:F2}");
            }
        }

        public static bool IsPlaying =>
            outputDevice?.PlaybackState == PlaybackState.Playing;

        public static bool IsPaused =>
            outputDevice?.PlaybackState == PlaybackState.Paused;
    }

    // ============================================
    // PROVIDER PERSONALIZADO PARA PAN EN ESTÉREO
    // ============================================
    public class StereoPanProvider : ISampleProvider
    {
        private readonly ISampleProvider source;
        private float pan;

        public WaveFormat WaveFormat { get; private set; }

        public float Pan
        {
            get => pan;
            set => pan = Math.Clamp(value, -1f, 1f);
        }

        public StereoPanProvider(ISampleProvider source)
        {
            if (source.WaveFormat.Channels != 2)
                throw new ArgumentException("Source must be stereo (2 channels)");

            this.source = source;
            WaveFormat = source.WaveFormat;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int samplesRead = source.Read(buffer, offset, count);

            // Aplicar pan a cada frame estéreo
            for (int i = 0; i < samplesRead; i += 2)
            {
                float left = buffer[offset + i];
                float right = buffer[offset + i + 1];

                if (pan < 0)
                {
                    // Pan hacia la izquierda: atenuar derecha
                    float rightAttenuation = 1f + pan; // pan=-1 → 0, pan=0 → 1
                    buffer[offset + i] = left;
                    buffer[offset + i + 1] = right * rightAttenuation;
                }
                else if (pan > 0)
                {
                    // Pan hacia la derecha: atenuar izquierda
                    float leftAttenuation = 1f - pan; // pan=1 → 0, pan=0 → 1
                    buffer[offset + i] = left * leftAttenuation;
                    buffer[offset + i + 1] = right;
                }
                // Si pan == 0, no modificar nada
            }

            return samplesRead;
        }
    }

    // ============================================
    // PROVIDER PERSONALIZADO MONO → ESTÉREO
    // ============================================
    public class MonoToStereoProvider : ISampleProvider
    {
        private readonly ISampleProvider source;

        public WaveFormat WaveFormat { get; private set; }

        public MonoToStereoProvider(ISampleProvider source)
        {
            if (source.WaveFormat.Channels != 1)
                throw new ArgumentException("Source must be mono");

            this.source = source;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(
                source.WaveFormat.SampleRate,
                2  // Estéreo
            );
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int sourceSamplesNeeded = count / 2;
            float[] sourceBuffer = new float[sourceSamplesNeeded];
            int samplesRead = source.Read(sourceBuffer, 0, sourceSamplesNeeded);

            int outIndex = offset;
            for (int n = 0; n < samplesRead; n++)
            {
                buffer[outIndex++] = sourceBuffer[n]; // Canal izquierdo
                buffer[outIndex++] = sourceBuffer[n]; // Canal derecho (mismo valor)
            }

            return samplesRead * 2;
        }
    }

    // ============================================
    // PROVIDER MULTICANAL → ESTÉREO
    // ============================================
    public class MultichannelToStereoProvider : ISampleProvider
    {
        private readonly ISampleProvider source;

        public WaveFormat WaveFormat { get; private set; }

        public MultichannelToStereoProvider(ISampleProvider source)
        {
            if (source.WaveFormat.Channels <= 2)
                throw new ArgumentException("Source must have more than 2 channels");

            this.source = source;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(
                source.WaveFormat.SampleRate,
                2
            );
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int sourceChannels = source.WaveFormat.Channels;
            int sourceSamplesNeeded = (count / 2) * sourceChannels;
            float[] sourceBuffer = new float[sourceSamplesNeeded];
            int samplesRead = source.Read(sourceBuffer, 0, sourceSamplesNeeded);

            int outIndex = offset;
            int framesRead = samplesRead / sourceChannels;

            for (int frame = 0; frame < framesRead; frame++)
            {
                int sourceIndex = frame * sourceChannels;

                // Mezclar canales izquierdos
                float left = sourceBuffer[sourceIndex];
                // Mezclar canales derechos
                float right = sourceChannels > 1 ? sourceBuffer[sourceIndex + 1] : left;

                buffer[outIndex++] = left;
                buffer[outIndex++] = right;
            }

            return framesRead * 2;
        }
    }
}