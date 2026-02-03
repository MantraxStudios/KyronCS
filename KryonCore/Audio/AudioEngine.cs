using NAudio.Wave;

public class AudioEngine
{
    private IWavePlayer outputDevice;
    private AudioFileReader audioFile;

    public void Play(string rutaArchivo)
    {
        Stop();

        outputDevice = new WaveOutEvent();
        audioFile = new AudioFileReader(rutaArchivo);

        outputDevice.Init(audioFile);
        outputDevice.Play();
    }

    public void Pause()
    {
        outputDevice?.Pause();
    }

    public void Resume()
    {
        outputDevice?.Play();
    }

    public void Stop()
    {
        outputDevice?.Stop();
        audioFile?.Dispose();
        outputDevice?.Dispose();

        audioFile = null;
        outputDevice = null;
    }

    public void SetVolume(float volume)
    {
        if (audioFile != null)
            audioFile.Volume = Math.Clamp(volume, 0f, 1f);
    }

    public bool IsPlaying =>
        outputDevice?.PlaybackState == PlaybackState.Playing;
}
