using System.Windows;
using System.Windows.Threading;
using Frtune.App.Services;
using Frtune.App.ViewModels;
using Frtune.App.Views;
using Frtune.Core.Tools;

namespace Frtune.App;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandled;

        // Headless mode used by the weekly scheduled task: clean, log, exit. No window.
        if (e.Args.Contains("--autoclean", StringComparer.OrdinalIgnoreCase))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            await Task.Run(RunAutoClean);
            Shutdown();
            return;
        }

        AppServices services;
        try
        {
            services = new AppServices();
        }
        catch (Exception ex)
        {
            var log = WriteCrashLog(ex);
            MessageBox.Show($"frtune couldn't start:\n\n{ex.Message}\n\nDetails were saved to:\n{log}", "frtune", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }
        ThemeService.ApplyAccent(services.Settings.AccentHex);
        var vm = new MainViewModel(services, new DialogService());
        var window = new MainWindow { DataContext = vm };
        MainWindow = window;
        window.Show();
        await vm.InitializeAsync();
    }

    /// <summary>Writes the full exception (with stack trace) so startup problems can be diagnosed.</summary>
    private static string WriteCrashLog(Exception ex)
    {
        try
        {
            System.IO.Directory.CreateDirectory(AppServices.DataDir);
            var path = System.IO.Path.Combine(AppServices.DataDir, "crash.log");
            System.IO.File.AppendAllText(path, $"[{DateTime.Now:u}] {ex}\r\n\r\n");
            return path;
        }
        catch { return "(could not write crash.log)"; }
    }

    private static void RunAutoClean()
    {
        var services = new AppServices();
        long freed = 0;
        foreach (var c in services.CleanerCategories().Where(c => c.Scheduled))
        {
            try { freed += c.Target.Clean(null, CancellationToken.None).BytesFreed; }
            catch { /* skip category */ }
        }
        services.Log.Add("clean", "Scheduled weekly clean", details: new[] { $"Freed {CleanerCatalog.FormatBytes(freed)}" });
    }

    private void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        try
        {
            if (MainWindow?.DataContext is MainViewModel vm)
            {
                vm.S.Log.Add("error", "Unexpected error", false, error: e.Exception.ToString());
                vm.S.Toasts.Show(ToastKind.Error, "Something went wrong", e.Exception.Message);
                return;
            }
        }
        catch { }
        MessageBox.Show(e.Exception.Message, "frtune", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
