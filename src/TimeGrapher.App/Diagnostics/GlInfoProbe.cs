using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;

namespace TimeGrapher.App.Diagnostics;

internal sealed record GlInfo(string? Vendor, string? Renderer, string? Version);

/// <summary>
/// Minimal OpenGL control that captures the vendor/renderer/version strings of the
/// GL context Avalonia actually created (e.g. "V3D 7.1" vs "llvmpipe"). With a
/// software rendering backend no context is created and <see cref="Captured"/>
/// never completes; callers must time out.
/// </summary>
internal sealed class GlInfoProbe : OpenGlControlBase
{
    private const int GlVendorName = 0x1F00;
    private const int GlRendererName = 0x1F01;
    private const int GlVersionName = 0x1F02;

    private readonly TaskCompletionSource<GlInfo> _captured =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<GlInfo> Captured => _captured.Task;

    protected override void OnOpenGlInit(GlInterface gl)
    {
        base.OnOpenGlInit(gl);
        _captured.TrySetResult(new GlInfo(
            gl.GetString(GlVendorName),
            gl.GetString(GlRendererName),
            gl.GetString(GlVersionName)));
    }

    protected override void OnOpenGlRender(GlInterface gl, int fb)
    {
        // Nothing to draw; the probe exists only to observe context creation.
    }
}
