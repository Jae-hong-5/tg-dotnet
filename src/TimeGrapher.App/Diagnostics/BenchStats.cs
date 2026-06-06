namespace TimeGrapher.App.Diagnostics;

/// <summary>Frame-interval statistics computed from consecutive composition timestamps.</summary>
internal sealed record BenchStats(
    int Frames,
    double TotalSeconds,
    double Fps,
    double MeanMs,
    double MinMs,
    double P50Ms,
    double P95Ms,
    double P99Ms,
    double MaxMs)
{
    public static BenchStats FromSamples(IReadOnlyList<double> samples)
    {
        if (samples.Count == 0)
        {
            return new BenchStats(0, 0, 0, 0, 0, 0, 0, 0, 0);
        }

        double[] sorted = samples.OrderBy(value => value).ToArray();
        double totalMs = sorted.Sum();
        double totalSeconds = totalMs / 1000.0;
        return new BenchStats(
            Frames: sorted.Length,
            TotalSeconds: totalSeconds,
            Fps: sorted.Length / totalSeconds,
            MeanMs: totalMs / sorted.Length,
            MinMs: sorted[0],
            P50Ms: Percentile(sorted, 50),
            P95Ms: Percentile(sorted, 95),
            P99Ms: Percentile(sorted, 99),
            MaxMs: sorted[^1]);
    }

    private static double Percentile(double[] sorted, double percentile)
    {
        double rank = percentile / 100.0 * (sorted.Length - 1);
        int lower = (int)Math.Floor(rank);
        int upper = (int)Math.Ceiling(rank);
        return sorted[lower] + (sorted[upper] - sorted[lower]) * (rank - lower);
    }
}
