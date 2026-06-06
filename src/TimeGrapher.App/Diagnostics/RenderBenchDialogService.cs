using TimeGrapher.App.Services;

namespace TimeGrapher.App.Diagnostics;

/// <summary>
/// Non-interactive dialog service used during render benchmarks so the run
/// never blocks on a modal dialog (sessions are driven headlessly over SSH).
/// </summary>
internal sealed class RenderBenchDialogService : ITimeGrapherDialogService
{
    public Task<RecordSessionChoice> AskRecordSessionAsync() => Task.FromResult(RecordSessionChoice.No);

    public Task<string?> PickOpenWavAsync(string currentDirectory) => Task.FromResult<string?>(null);

    public Task<string?> PickSaveWavAsync() => Task.FromResult<string?>(null);

    public Task ShowErrorAsync(string title, string message)
    {
        Console.Error.WriteLine($"RENDER_BENCH_DIALOG_ERROR: {title}: {message}");
        return Task.CompletedTask;
    }
}
