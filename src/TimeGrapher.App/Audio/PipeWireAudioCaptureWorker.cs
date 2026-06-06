using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using TimeGrapher.Core.Shared;

namespace TimeGrapher.App.Audio;

internal sealed class PipeWireAudioCaptureWorker : ILiveAudioWorker
{
    private const int Channels = MasterAudioBuffer.Channels;

    private static readonly Regex SourceLineRegex = new(
        @"(?:^|\s)(?:\*\s*)?(?<id>\d+)\.\s+(?<name>.+?)(?:\s+\[|$)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly MasterAudioBuffer _rawAudio;
    private readonly Stopwatch _timer = new();
    private readonly StringBuilder _stderr = new();

    private Process? _process;
    private Thread? _stdoutThread;
    private Thread? _stderrThread;
    private bool _timerStarted;
    private double _lastTime;
    private ulong _frameCount;
    private ulong _sampleCount;
    private float _volume = 1.0f;

    public PipeWireAudioCaptureWorker(MasterAudioBuffer buffer)
    {
        _rawAudio = buffer;
        _rawAudio.Reset();
    }

    public event Action? DataReady;

    public static IReadOnlyList<LiveAudioDevice> EnumerateInputDevices()
    {
        string status = RunCommand("wpctl", "status");
        IReadOnlyList<LiveAudioDevice> devices = ParseWpctlSources(status);
        if (devices.Count > 0)
        {
            return devices;
        }

        return Array.Empty<LiveAudioDevice>();
    }

    internal static IReadOnlyList<LiveAudioDevice> ParseWpctlSources(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return Array.Empty<LiveAudioDevice>();
        }

        var devices = new List<LiveAudioDevice>();
        bool inSources = false;
        foreach (string rawLine in status.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
        {
            string line = rawLine.Trim();
            if (line.Contains("Sources:", StringComparison.Ordinal))
            {
                inSources = true;
                continue;
            }

            if (!inSources)
            {
                continue;
            }

            if (line.Contains("Filters:", StringComparison.Ordinal) ||
                line.Contains("Streams:", StringComparison.Ordinal) ||
                line.Contains("Video", StringComparison.Ordinal) ||
                line.Contains("Settings", StringComparison.Ordinal))
            {
                break;
            }

            Match match = SourceLineRegex.Match(line);
            if (!match.Success ||
                !int.TryParse(match.Groups["id"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
            {
                continue;
            }

            string name = match.Groups["name"].Value.Trim();
            if (name.Length == 0)
            {
                continue;
            }

            devices.Add(new LiveAudioDevice(id, name));
        }

        return devices;
    }

    public void Start(int deviceNumber, int sampleRate, float volume)
    {
        _volume = volume;
        if (_process != null)
        {
            _process.Dispose();
            _process = null;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "pw-record",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("--media-category");
        startInfo.ArgumentList.Add("Capture");
        startInfo.ArgumentList.Add("--rate");
        startInfo.ArgumentList.Add(sampleRate.ToString(CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("--channels");
        startInfo.ArgumentList.Add(Channels.ToString(CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("--format");
        startInfo.ArgumentList.Add("f32");
        startInfo.ArgumentList.Add("--raw");
        if (deviceNumber > 0)
        {
            startInfo.ArgumentList.Add("--target");
            startInfo.ArgumentList.Add(deviceNumber.ToString(CultureInfo.InvariantCulture));
        }
        startInfo.ArgumentList.Add("-");

        Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start pw-record.");

        _process = process;
        _stderr.Clear();

        _stdoutThread = new Thread(() => ReadPcm(process))
        {
            Name = "PipeWireAudioCaptureRead",
            IsBackground = true,
        };
        _stderrThread = new Thread(() => ReadStderr(process))
        {
            Name = "PipeWireAudioCaptureErr",
            IsBackground = true,
        };
        _stdoutThread.Start();
        _stderrThread.Start();

        Thread.Sleep(250);
        if (process.HasExited)
        {
            string error = _stderr.ToString().Trim();
            process.Dispose();
            _process = null;
            throw new InvalidOperationException(
                error.Length == 0 ? "pw-record exited before capture started." : "pw-record exited: " + error);
        }
    }

    public void SetVolume(float volume)
    {
        _volume = volume;
    }

    public bool TryStop(TimeSpan timeout)
    {
        Process? process = Interlocked.Exchange(ref _process, null);
        if (process == null)
        {
            return true;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            if (timeout == Timeout.InfiniteTimeSpan)
            {
                process.WaitForExit();
            }
            else if (!process.WaitForExit(timeout))
            {
                return false;
            }

            _stdoutThread?.Join(TimeSpan.FromMilliseconds(250));
            _stderrThread?.Join(TimeSpan.FromMilliseconds(250));
            return true;
        }
        finally
        {
            process.Dispose();
        }
    }

    public void Dispose()
    {
        TryStop(Timeout.InfiniteTimeSpan);
    }

    private void ReadPcm(Process process)
    {
        var pending = Array.Empty<byte>();
        var readBuffer = new byte[8192];

        try
        {
            Stream stream = process.StandardOutput.BaseStream;
            while (true)
            {
                int read = stream.Read(readBuffer, 0, readBuffer.Length);
                if (read <= 0)
                {
                    return;
                }

                int combinedLength = pending.Length + read;
                var combined = new byte[combinedLength];
                if (pending.Length > 0)
                {
                    Array.Copy(pending, combined, pending.Length);
                }
                Array.Copy(readBuffer, 0, combined, pending.Length, read);

                int usableBytes = combinedLength - (combinedLength % sizeof(float));
                int leftoverBytes = combinedLength - usableBytes;
                if (usableBytes > 0)
                {
                    WriteFloatSamples(combined.AsSpan(0, usableBytes));
                }

                if (leftoverBytes > 0)
                {
                    pending = new byte[leftoverBytes];
                    Array.Copy(combined, usableBytes, pending, 0, leftoverBytes);
                }
                else
                {
                    pending = Array.Empty<byte>();
                }
            }
        }
        catch (ObjectDisposedException)
        {
        }
        catch (IOException)
        {
        }
    }

    private void ReadStderr(Process process)
    {
        try
        {
            while (!process.StandardError.EndOfStream)
            {
                string? line = process.StandardError.ReadLine();
                if (line == null)
                {
                    return;
                }

                if (_stderr.Length < 4096)
                {
                    _stderr.AppendLine(line);
                }

                Console.Error.WriteLine("pw-record: " + line);
            }
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void WriteFloatSamples(ReadOnlySpan<byte> bytes)
    {
        int sampleCount = bytes.Length / sizeof(float);
        if (sampleCount <= 0)
        {
            DataReady?.Invoke();
            return;
        }

        float volume = _volume;
        float[] block = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            int bits = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(i * sizeof(float), sizeof(float)));
            block[i] = BitConverter.Int32BitsToSingle(bits) * volume;
        }

        _rawAudio.WriteSamples(block);
        UpdateStats((ulong)sampleCount);
        DataReady?.Invoke();
    }

    private void UpdateStats(ulong sampleCount)
    {
        if (!_timerStarted)
        {
            _timerStarted = true;
            _timer.Start();
        }

        ++_frameCount;
        _sampleCount += sampleCount;
        double currentTime = _timer.ElapsedMilliseconds / 1000.0;
        if (currentTime - _lastTime > 2)
        {
            double delta = currentTime - _lastTime;
            double fps = _frameCount / delta;
            double sps = _sampleCount / delta;
            double spf = _sampleCount / _frameCount;
            _rawAudio.SetStats(fps, spf, sps);
            _lastTime = currentTime;
            _frameCount = 0;
            _sampleCount = 0;
        }
    }

    private static string RunCommand(string fileName, string argument)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                },
            };
            process.StartInfo.ArgumentList.Add(argument);
            if (!process.Start())
            {
                return "";
            }

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(2000);
            return process.ExitCode == 0 ? output : "";
        }
        catch
        {
            return "";
        }
    }
}
