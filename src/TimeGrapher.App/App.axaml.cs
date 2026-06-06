using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace TimeGrapher.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            ShowStartupWindow(desktop);
        }
        base.OnFrameworkInitializationCompleted();
    }

    private static void ShowStartupWindow(IClassicDesktopStyleApplicationLifetime desktop)
    {
        if (Diagnostics.RenderBenchOptions.Current != null)
        {
            // Render benchmark: skip the splash so measurement starts deterministically.
            desktop.MainWindow = new Views.MainWindow();
            return;
        }

        Views.SplashWindow splashWindow;
        try
        {
            splashWindow = new Views.SplashWindow();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Splash startup failed: " + ex.Message);
            desktop.MainWindow = new Views.MainWindow();
            return;
        }

        Views.MainWindow? mainWindow = null;
        bool switchedToMain = false;

        void PrepareMainWindow()
        {
            mainWindow ??= new Views.MainWindow();
        }

        void SwitchToMainWindow()
        {
            if (switchedToMain)
            {
                return;
            }

            switchedToMain = true;
            PrepareMainWindow();
            Views.MainWindow window = mainWindow ?? throw new InvalidOperationException("Main window was not created.");
            desktop.MainWindow = window;
            window.Show();
            splashWindow.Close();
        }

        splashWindow.Opened += (_, _) =>
        {
            Dispatcher.UIThread.Post(PrepareMainWindow, DispatcherPriority.Background);
        };
        splashWindow.PlaybackCompleted += (_, _) => SwitchToMainWindow();
        desktop.MainWindow = splashWindow;
    }
}
