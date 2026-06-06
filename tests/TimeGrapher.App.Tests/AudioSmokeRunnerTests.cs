using TimeGrapher.App.Audio;
using Xunit;

namespace TimeGrapher.App.Tests;

public sealed class AudioSmokeRunnerTests
{
    [Fact]
    public void ParsePositiveOptionReadsSeparateAndInlineValues()
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
    public void ParsePositiveOptionFallsBackForMissingOrInvalidValues()
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
