using TimeGrapher.Core.Detection;
using Xunit;

namespace TimeGrapher.Core.Tests;

/// <summary>
/// Front-end DSP primitives feeding the burst detector: the DC-blocking high-pass and the
/// rectify+smooth envelope. Both are streaming single-pole filters with tiny per-sample state.
/// </summary>
public sealed class DspTests
{
    [Fact]
    public void Hpf_PassesFirstSampleThenBlocksDc()
    {
        var hpf = new TgHpf(48000, 200);
        var input = new float[2000];
        Array.Fill(input, 1.0f);
        var output = new float[input.Length];

        hpf.Process(input, output, input.Length);

        Assert.Equal(1.0f, output[0], 5);          // no prior state -> first sample passes through
        Assert.True(Math.Abs(output[^1]) < 1e-3f); // sustained DC decays toward zero
    }

    [Fact]
    public void Hpf_ResetRestoresInitialResponse()
    {
        var hpf = new TgHpf(48000, 200);
        var dc = new float[500];
        Array.Fill(dc, 1.0f);
        hpf.Process(dc, new float[dc.Length], dc.Length);

        hpf.Reset();
        var output = new float[1];
        hpf.Process(new float[] { 1.0f }, output, 1);

        Assert.Equal(1.0f, output[0], 5);
    }

    [Fact]
    public void Envelope_RectifiesAndConvergesToMagnitude()
    {
        var env = new TgEnvelope(48000, 0.15);
        var input = new float[5000];
        Array.Fill(input, -1.0f); // negative input must be rectified
        var output = new float[input.Length];

        env.Process(input, output, input.Length);

        Assert.All(output, v => Assert.True(v >= 0.0f));  // full-wave rectified
        Assert.True(Math.Abs(output[^1] - 1.0f) < 1e-3f); // converges to |x| = 1
    }

    [Fact]
    public void Envelope_RisesGraduallyFromZero()
    {
        var env = new TgEnvelope(48000, 1.0);
        var output = new float[3];
        env.Process(new[] { 1.0f, 1.0f, 1.0f }, output, 3);

        Assert.True(output[0] > 0.0f && output[0] < 1.0f); // first step is partial (smoothed)
        Assert.True(output[1] > output[0]);
        Assert.True(output[2] > output[1]);
    }
}
