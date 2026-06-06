using Avalonia;
using TimeGrapher.App.Audio;
using TimeGrapher.App.Diagnostics;
using TimeGrapher.Core.Shared;

namespace TimeGrapher.App;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Contains("--smoke", StringComparer.Ordinal))
        {
            _ = BuildAvaloniaApp();
            _ = typeof(AnalysisFrame).Assembly.FullName;
            Console.WriteLine("TimeGrapher.App smoke OK");
            return 0;
        }

        if (args.Contains("--audio-smoke", StringComparer.Ordinal))
        {
            return AudioSmokeRunner.Run(args, capture: false);
        }

        if (args.Contains("--capture-smoke", StringComparer.Ordinal))
        {
            return AudioSmokeRunner.Run(args, capture: true);
        }

        RenderBenchOptions.Current = RenderBenchOptions.TryParse(args);
        return BuildAvaloniaApp(args).StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => BuildAvaloniaApp(Array.Empty<string>());

    private static AppBuilder BuildAvaloniaApp(string[] args)
    {
        AppBuilder builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

        // --render-mode pins a single backend (no fallback) so render A/B tests fail
        // loudly instead of silently falling back; default keeps platform behavior.
        return RenderBenchOptions.ParseRenderMode(args) switch
        {
            "software" => builder
                .With(new X11PlatformOptions { RenderingMode = new[] { X11RenderingMode.Software } })
                .With(new Win32PlatformOptions { RenderingMode = new[] { Win32RenderingMode.Software } }),
            "glx" => builder.With(new X11PlatformOptions { RenderingMode = new[] { X11RenderingMode.Glx } }),
            "egl" => builder.With(new X11PlatformOptions { RenderingMode = new[] { X11RenderingMode.Egl } }),
            "vulkan" => builder.With(new X11PlatformOptions { RenderingMode = new[] { X11RenderingMode.Vulkan } }),
            _ => builder,
        };
    }
}
