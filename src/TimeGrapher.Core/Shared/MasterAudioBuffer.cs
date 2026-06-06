namespace TimeGrapher.Core.Shared;

public readonly record struct MasterAudioBufferSnapshot(
    ulong TotalSamplesWritten,
    double Fps,
    double Spf,
    double Sps,
    int NumberOfAudioSamples);

public readonly record struct MasterAudioBufferReadResult(
    int SamplesCopied,
    ulong SourceSampleEnd,
    ulong OriginalPendingSamples,
    bool InputOverrun,
    ulong InputSamplesDropped,
    double Fps,
    double Spf,
    double Sps,
    int NumberOfAudioSamples);

/// <summary>
/// Port of TMasterAudioDataRaw (SharedAudio.h): a 30-second mono float ring buffer
/// shared between exactly one active input worker (writer) and the analysis worker (reader).
/// Writers and analysis reads both use <see cref="Lock"/> so a block copy cannot race with
/// ring-buffer writes. Detector work is performed after the copy, outside the lock.
/// </summary>
public sealed class MasterAudioBuffer
{
    public const int Channels = 1;
    public const int SecondsOfBuffer = 30;

    public readonly object Lock = new();

    private float[] _samples;
    private int _numberOfAudioSamples;

    private uint _writeIndex;
    private ulong _totalSamplesWritten;

    // C++: MainThrd_LastTotalSamplesWritten / MainThrd_LastWriteIndex
    // ("MainThrd" historically; owned by the analysis worker in this port.)
    private ulong _analysisLastTotalSamplesWritten;
    private uint _analysisLastWriteIndex;

    // Input-side throughput stats displayed in the status bar.
    private double _fps;
    private double _spf;
    private double _sps;

    public MasterAudioBuffer(int sampleRate)
    {
        _numberOfAudioSamples = sampleRate * SecondsOfBuffer;
        _samples = new float[_numberOfAudioSamples];
    }

    /// <summary>Ring-write a block of mono float samples (input worker thread).</summary>
    public void WriteSamples(ReadOnlySpan<float> data)
    {
        lock (Lock)
        {
            for (int i = 0; i < data.Length; i++)
            {
                _samples[_writeIndex] = data[i];
                _writeIndex = (_writeIndex + 1) % (uint)_numberOfAudioSamples;
            }
            _totalSamplesWritten += (ulong)data.Length;
        }
    }

    /// <summary>Update input-side throughput stats (input worker thread).</summary>
    public void SetStats(double fps, double spf, double sps)
    {
        lock (Lock)
        {
            _fps = fps;
            _spf = spf;
            _sps = sps;
        }
    }

    /// <summary>Read writer-side counters/stats as one consistent lock-protected snapshot.</summary>
    public MasterAudioBufferSnapshot GetSnapshot()
    {
        lock (Lock)
        {
            return new MasterAudioBufferSnapshot(_totalSamplesWritten, _fps, _spf, _sps, _numberOfAudioSamples);
        }
    }

    /// <summary>
    /// Copy the next unread analysis block up to a fixed source snapshot. The analysis worker
    /// passes the snapshot's TotalSamplesWritten so each wake-up processes a bounded unit even
    /// while live capture continues writing.
    /// </summary>
    public MasterAudioBufferReadResult CopyAnalysisSamples(
        Span<float> destination,
        ulong sourceSampleEnd)
    {
        lock (Lock)
        {
            ulong currentTotalSamplesWritten = _totalSamplesWritten;
            ulong targetSampleEnd = Math.Min(sourceSampleEnd, currentTotalSamplesWritten);
            ulong originalPendingSamples = targetSampleEnd > _analysisLastTotalSamplesWritten
                ? targetSampleEnd - _analysisLastTotalSamplesWritten
                : 0;

            bool inputOverrun = false;
            ulong inputSamplesDropped = 0;
            ulong retainedCapacity = (ulong)_numberOfAudioSamples;
            if (currentTotalSamplesWritten > _analysisLastTotalSamplesWritten &&
                currentTotalSamplesWritten - _analysisLastTotalSamplesWritten > retainedCapacity)
            {
                inputOverrun = true;
                inputSamplesDropped = currentTotalSamplesWritten - _analysisLastTotalSamplesWritten - retainedCapacity;
                _analysisLastTotalSamplesWritten = currentTotalSamplesWritten - retainedCapacity;
                _analysisLastWriteIndex = (uint)(_analysisLastTotalSamplesWritten % retainedCapacity);
            }

            int copyCount = 0;
            if (destination.Length > 0 && targetSampleEnd > _analysisLastTotalSamplesWritten)
            {
                ulong pendingSamples = targetSampleEnd - _analysisLastTotalSamplesWritten;
                copyCount = (int)Math.Min((ulong)destination.Length, pendingSamples);
                for (int i = 0; i < copyCount; i++)
                {
                    destination[i] = _samples[_analysisLastWriteIndex];
                    _analysisLastWriteIndex = (_analysisLastWriteIndex + 1) % (uint)_numberOfAudioSamples;
                }
                _analysisLastTotalSamplesWritten += (ulong)copyCount;
            }

            return new MasterAudioBufferReadResult(
                copyCount,
                sourceSampleEnd,
                originalPendingSamples,
                inputOverrun,
                inputSamplesDropped,
                _fps,
                _spf,
                _sps,
                _numberOfAudioSamples);
        }
    }

    /// <summary>Zero all counters and samples; called between sessions (UI thread, workers stopped).</summary>
    public void Reset()
    {
        int sampleRate;
        lock (Lock)
        {
            sampleRate = Math.Max(1, _numberOfAudioSamples / SecondsOfBuffer);
        }
        Reset(sampleRate);
    }

    /// <summary>Zero all counters and samples; called between sessions (UI thread, workers stopped).</summary>
    public void Reset(int sampleRate)
    {
        lock (Lock)
        {
            int wanted = sampleRate * SecondsOfBuffer;
            if (wanted != _numberOfAudioSamples)
            {
                _numberOfAudioSamples = wanted;
                _samples = new float[wanted];
            }
            else
            {
                Array.Clear(_samples);
            }
            _writeIndex = 0;
            _totalSamplesWritten = 0;
            _analysisLastTotalSamplesWritten = 0;
            _analysisLastWriteIndex = 0;
            _fps = _spf = _sps = 0.0;
        }
    }
}
