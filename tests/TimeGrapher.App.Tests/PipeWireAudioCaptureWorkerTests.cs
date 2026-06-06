using TimeGrapher.App.Audio;
using Xunit;

namespace TimeGrapher.App.Tests;

public sealed class PipeWireAudioCaptureWorkerTests
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

        IReadOnlyList<LiveAudioDevice> devices = PipeWireAudioCaptureWorker.ParseWpctlSources(status);

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

        IReadOnlyList<LiveAudioDevice> devices = PipeWireAudioCaptureWorker.ParseWpctlSources(status);

        Assert.Empty(devices);
    }

    [Fact]
    public void AudioSmokeRunner_ParsePositiveOptionReadsSeparateAndInlineValues()
    {
        Assert.Equal(96000, AudioSmokeRunner.ParsePositiveOption(
            new[] { "--capture-smoke", "--rate", "96000" },
            "--rate",
            48000));

        Assert.Equal(2500, AudioSmokeRunner.ParsePositiveOption(
            new[] { "--capture-smoke", "--duration-ms=2500" },
            "--duration-ms",
            1500));
    }

    [Fact]
    public void AudioSmokeRunner_ParsePositiveOptionFallsBackForMissingOrInvalidValues()
    {
        Assert.Equal(48000, AudioSmokeRunner.ParsePositiveOption(
            new[] { "--capture-smoke", "--rate" },
            "--rate",
            48000));

        Assert.Equal(48000, AudioSmokeRunner.ParsePositiveOption(
            new[] { "--capture-smoke", "--rate", "0" },
            "--rate",
            48000));

        Assert.Equal(48000, AudioSmokeRunner.ParsePositiveOption(
            new[] { "--capture-smoke", "--rate=abc" },
            "--rate",
            48000));
    }
}
