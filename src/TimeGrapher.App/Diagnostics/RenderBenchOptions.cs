using System.Globalization;

namespace TimeGrapher.App.Diagnostics;

/// <summary>
/// Options for the on-device render benchmark (--render-bench). The benchmark
/// auto-starts a simulation run, redraws the active info tab at the maximum rate
/// the compositor allows, and prints frame-interval statistics to stdout.
/// </summary>
internal sealed class RenderBenchOptions
{
    /// <summary>Set once from Program.Main before the Avalonia app is built; null outside bench mode.</summary>
    public static RenderBenchOptions? Current { get; set; }

    public string Label { get; init; } = "default";
    public string RenderMode { get; init; } = "default";
    public int WarmupSeconds { get; init; } = 5;
    public int MeasureSeconds { get; init; } = 30;
    public bool Maximized { get; init; }

    public static RenderBenchOptions? TryParse(string[] args)
    {
        if (!args.Contains("--render-bench", StringComparer.Ordinal))
        {
            return null;
        }

        return new RenderBenchOptions
        {
            Label = ParseString(args, "--bench-label") ?? "default",
            RenderMode = ParseRenderMode(args) ?? "default",
            WarmupSeconds = ParseInt(args, "--bench-warmup", 5),
            MeasureSeconds = ParseInt(args, "--bench-seconds", 30),
            Maximized = args.Contains("--bench-maximized", StringComparer.Ordinal),
        };
    }

    /// <summary>--render-mode=glx|egl|vulkan|software; null when absent (platform default).</summary>
    public static string? ParseRenderMode(string[] args) => ParseString(args, "--render-mode");

    private static string? ParseString(string[] args, string name)
    {
        string prefix = name + "=";
        foreach (string arg in args)
        {
            if (arg.StartsWith(prefix, StringComparison.Ordinal))
            {
                return arg.Substring(prefix.Length);
            }
        }

        return null;
    }

    private static int ParseInt(string[] args, string name, int fallback)
    {
        string? text = ParseString(args, name);
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) && value > 0
            ? value
            : fallback;
    }
}
