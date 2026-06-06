namespace TimeGrapher.Core.Shared;

public interface ILiveAudioWorker : IAudioInputWorker
{
    void Start(int deviceNumber, int sampleRate, float volume);

    void SetVolume(float volume);
}
