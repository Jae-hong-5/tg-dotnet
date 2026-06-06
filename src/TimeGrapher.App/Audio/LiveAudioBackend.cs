using TimeGrapher.Core.Shared;
using TimeGrapher.Platform.WindowsAudio;

namespace TimeGrapher.App.Audio;

internal static class LiveAudioBackend
{
    private static readonly int[] StandardSampleRates = { 48000, 96000, 192000, 384000 };

    public static bool CanCapture =>
        OperatingSystem.IsWindows() ||
        OperatingSystem.IsLinux();

    public static IReadOnlyList<LiveAudioDevice> EnumerateInputDevices()
    {
        if (OperatingSystem.IsWindows())
        {
            IReadOnlyList<string> names = AudioCaptureWorker.EnumerateInputDevices();
            var devices = new List<LiveAudioDevice>(names.Count);
            for (int i = 0; i < names.Count; i++)
            {
                devices.Add(new LiveAudioDevice(i, names[i]));
            }

            return devices;
        }

        if (OperatingSystem.IsLinux())
        {
            return PipeWireAudioCaptureWorker.EnumerateInputDevices();
        }

        return Array.Empty<LiveAudioDevice>();
    }

    public static IReadOnlyList<int> GetCandidateSampleRates(int deviceNumber)
    {
        if (OperatingSystem.IsWindows())
        {
            return AudioCaptureWorker.GetCandidateSampleRates(deviceNumber);
        }

        return StandardSampleRates;
    }

    public static ILiveAudioWorker CreateWorker(MasterAudioBuffer buffer)
    {
        if (OperatingSystem.IsWindows())
        {
            return new WindowsLiveAudioWorker(buffer);
        }

        if (OperatingSystem.IsLinux())
        {
            return new PipeWireAudioCaptureWorker(buffer);
        }

        throw new PlatformNotSupportedException("Live audio capture is not supported on this platform.");
    }
}
