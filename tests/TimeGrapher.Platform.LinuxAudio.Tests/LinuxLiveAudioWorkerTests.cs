using System.Diagnostics;
using TimeGrapher.Core.Shared;
using TimeGrapher.Platform.LinuxAudio;
using Xunit;

namespace TimeGrapher.Platform.LinuxAudio.Tests;

public sealed class LinuxLiveAudioWorkerTests
{
    [Fact]
    public void ParseWpctlSources_ReturnsSourceNodesOnly()
    {
        const string status = """
Audio
 Devices:
        48. Built-in Audio [alsa]
 Sinks:
    *   56. Built-in Audio Digital Stereo (HDMI) [vol: 0.40]
 Sources:
    *   65. USB PnP Sound Device Mono [vol: 1.00]
        66. Cubilux CA7 Mono [vol: 0.80]
 Filters:
 Streams:
""";

        IReadOnlyList<LiveAudioDevice> devices = LinuxLiveAudioWorker.ParseWpctlSources(status);

        Assert.Collection(
            devices,
            first =>
            {
                Assert.Equal(65, first.Number);
                Assert.Equal("USB PnP Sound Device Mono", first.Name);
            },
            second =>
            {
                Assert.Equal(66, second.Number);
                Assert.Equal("Cubilux CA7 Mono", second.Name);
            });
    }

    [Fact]
    public void ParseWpctlSources_ReturnsEmptyWhenNoSources()
    {
        const string status = """
Audio
 Devices:
        48. Built-in Audio [alsa]
 Sinks:
    *   56. Built-in Audio Digital Stereo (HDMI) [vol: 0.40]
 Sources:
 Filters:
 Streams:
""";

        IReadOnlyList<LiveAudioDevice> devices = LinuxLiveAudioWorker.ParseWpctlSources(status);

        Assert.Empty(devices);
    }

    [Fact]
    public void ParseAlsaCaptureDevices_ReturnsHardwareDevices()
    {
        const string arecordList = """
**** List of CAPTURE Hardware Devices ****
card 3: Device [USB PnP Sound Device], device 0: USB Audio [USB Audio]
  Subdevices: 1/1
  Subdevice #0: subdevice #0
card 4: CA7 [Cubilux CA7], device 0: USB Audio [USB Audio]
  Subdevices: 1/1
  Subdevice #0: subdevice #0
""";

        IReadOnlyList<LiveAudioDevice> devices = LinuxLiveAudioWorker.ParseAlsaCaptureDevices(arecordList);

        Assert.Collection(
            devices,
            first =>
            {
                Assert.True(LinuxLiveAudioWorker.TryDecodeAlsaDeviceNumber(first.Number, out int card, out int device));
                Assert.Equal(3, card);
                Assert.Equal(0, device);
                Assert.Equal("ALSA hw:3,0 USB PnP Sound Device - USB Audio", first.Name);
            },
            second =>
            {
                Assert.True(LinuxLiveAudioWorker.TryDecodeAlsaDeviceNumber(second.Number, out int card, out int device));
                Assert.Equal(4, card);
                Assert.Equal(0, device);
                Assert.Equal("ALSA hw:4,0 Cubilux CA7 - USB Audio", second.Name);
            });
    }

    [Fact]
    public void ParseAlsaCaptureDevices_ReturnsEmptyWhenNoHardwareDevices()
    {
        const string arecordList = """
**** List of CAPTURE Hardware Devices ****
""";

        IReadOnlyList<LiveAudioDevice> devices = LinuxLiveAudioWorker.ParseAlsaCaptureDevices(arecordList);

        Assert.Empty(devices);
    }

    [Fact]
    public void RunCommand_ReturnsOutputForSuccessfulProcess()
    {
        (string fileName, string[] args) = ShellCommand("echo ok");

        string output = LinuxLiveAudioWorker.RunCommand(fileName, TimeSpan.FromSeconds(2), args);

        Assert.Equal("ok", output.Trim());
    }

    [Fact]
    public void RunCommand_ReturnsEmptyWhenProcessExceedsTimeout()
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        (string fileName, string[] args) = ShellCommand(OperatingSystem.IsWindows()
            ? "ping 127.0.0.1 -n 6 > nul & echo done"
            : "sleep 2; echo done");

        string output = LinuxLiveAudioWorker.RunCommand(fileName, TimeSpan.FromMilliseconds(200), args);

        Assert.Equal("", output);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2));
    }

    private static (string FileName, string[] Arguments) ShellCommand(string command)
    {
        return OperatingSystem.IsWindows()
            ? ("cmd.exe", new[] { "/c", command })
            : ("/bin/sh", new[] { "-c", command });
    }
}
