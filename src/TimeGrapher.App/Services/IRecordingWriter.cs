using TimeGrapher.Core.AudioIo;

namespace TimeGrapher.App.Services;

internal interface IRecordingWriter : ISampleWriter, IDisposable
{
    ulong DroppedBlocks { get; }

    /// <summary>true while the writer can still complete a close (a failed Close is retryable).</summary>
    bool IsOpen { get; }

    bool Open(string filePath, int sampleRate, int channels);
}
