using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Rendering;
using Avalonia.Threading;

using TimeGrapher.App.Diagnostics;
using TimeGrapher.Core.Shared;

namespace TimeGrapher.App.Views;

public partial class MainWindow
{
    // --render-bench: auto-start a Sim run, redraw the active info tab on every
    // composition frame (RequestAnimationFrame chain), and print frame-interval
    // statistics to stdout as a single "RENDER_BENCH_RESULT {json}" line.
    private void AttachRenderBench(RenderBenchOptions options)
    {
        Opened += async (_, _) =>
        {
            try
            {
                await RunRenderBenchAsync(options);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("RENDER_BENCH_ERROR: " + ex);
                Environment.Exit(2);
            }
        };
    }

    private async Task RunRenderBenchAsync(RenderBenchOptions options)
    {
        if (options.Maximized)
        {
            WindowState = WindowState.Maximized;
        }

        // Same instrumentation as Avalonia discussion #18807 so numbers are comparable.
        RendererDiagnostics.DebugOverlays = RendererDebugOverlays.Fps | RendererDebugOverlays.RenderTimeGraph;

        GlInfo? glInfo = await CaptureGlInfoAsync();

        // Select Sim mode by name: the mode list omits Live when no capture device exists,
        // so the index differs between platforms.
        int simIndex = -1;
        for (int i = 0; i < mViewModel.ModeNames.Count; i++)
        {
            if (mViewModel.ModeNames[i] == ModeStrings[SIM])
            {
                simIndex = i;
                break;
            }
        }

        if (simIndex < 0)
        {
            throw new InvalidOperationException("Sim mode is not available on this platform.");
        }

        mSelectionCoordinator.SetSelectedModeIndex(simIndex);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);

        mViewModel.StartCommand.Execute(null);

        // Wait for the first analysis frame so the plots carry real sim content.
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
        while (mLastAnalysisFrame == null)
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("Sim run produced no analysis frame within 20s.");
            }

            await Task.Delay(100);
        }

        Console.Error.WriteLine("RENDER_BENCH: sim running, warming up " +
                                options.WarmupSeconds + "s then measuring " +
                                options.MeasureSeconds + "s");

        BenchStats stats = await MeasureFrameIntervalsAsync(options.WarmupSeconds, options.MeasureSeconds);

        var payload = new Dictionary<string, object?>
        {
            ["label"] = options.Label,
            ["renderMode"] = options.RenderMode,
            ["os"] = RuntimeInformation.OSDescription,
            ["arch"] = RuntimeInformation.ProcessArchitecture.ToString(),
            ["glVendor"] = glInfo?.Vendor,
            ["glRenderer"] = glInfo?.Renderer,
            ["glVersion"] = glInfo?.Version,
            ["windowWidth"] = Bounds.Width,
            ["windowHeight"] = Bounds.Height,
            ["renderScaling"] = RenderScaling,
            ["activeTab"] = ActiveInfoTabId(),
            ["warmupSeconds"] = options.WarmupSeconds,
            ["measuredSeconds"] = stats.TotalSeconds,
            ["frames"] = stats.Frames,
            ["fps"] = stats.Fps,
            ["frameMsMean"] = stats.MeanMs,
            ["frameMsMin"] = stats.MinMs,
            ["frameMsP50"] = stats.P50Ms,
            ["frameMsP95"] = stats.P95Ms,
            ["frameMsP99"] = stats.P99Ms,
            ["frameMsMax"] = stats.MaxMs,
        };
        Console.WriteLine("RENDER_BENCH_RESULT " + JsonSerializer.Serialize(payload));

        mViewModel.StopCommand.Execute(null);
        await Task.Delay(750);
        Close();

        // Safety net in case window close does not end the lifetime.
        await Task.Delay(5000);
        Environment.Exit(0);
    }

    private async Task<GlInfo?> CaptureGlInfoAsync()
    {
        var probe = new GlInfoProbe { Width = 32, Height = 32 };
        var probeWindow = new Window
        {
            Width = 64,
            Height = 64,
            ShowInTaskbar = false,
            ShowActivated = false,
            SystemDecorations = SystemDecorations.None,
            Content = probe,
        };

        probeWindow.Show(this);
        Task completed = await Task.WhenAny(probe.Captured, Task.Delay(3000));
        probeWindow.Close();
        return completed == probe.Captured ? await probe.Captured : null;
    }

    private Task<BenchStats> MeasureFrameIntervalsAsync(int warmupSeconds, int measureSeconds)
    {
        var samples = new List<double>(measureSeconds * 250);
        var tcs = new TaskCompletionSource<BenchStats>(TaskCreationOptions.RunContinuationsAsynchronously);
        TimeSpan warmup = TimeSpan.FromSeconds(warmupSeconds);
        TimeSpan total = warmup + TimeSpan.FromSeconds(measureSeconds);
        TimeSpan first = default;
        TimeSpan last = default;

        void OnFrame(TimeSpan ts)
        {
            if (first == default)
            {
                first = ts;
            }
            else if (ts - first > warmup)
            {
                samples.Add((ts - last).TotalMilliseconds);
            }

            last = ts;

            // Re-render the real workload: route the latest analysis frame through the
            // active tab's consumer (rebuilds ScottPlot plottables and refreshes).
            AnalysisFrame? frame = mLastAnalysisFrame;
            if (frame != null)
            {
                mFrameRouter.Route(frame, ActiveInfoTabId(), BuildTabRenderContext());
            }
            else
            {
                InvalidateVisual();
            }

            if (ts - first < total)
            {
                RequestAnimationFrame(OnFrame);
            }
            else
            {
                tcs.TrySetResult(BenchStats.FromSamples(samples));
            }
        }

        RequestAnimationFrame(OnFrame);
        return tcs.Task;
    }
}
