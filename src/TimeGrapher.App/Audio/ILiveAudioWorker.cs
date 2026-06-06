namespace TimeGrapher.App.Audio;

internal interface ILiveAudioWorker : IDisposable
{
    event Action? DataReady;

    void Start(int deviceNumber, int sampleRate, float volume);

    void SetVolume(float volume);

    bool TryStop(TimeSpan timeout);
}
