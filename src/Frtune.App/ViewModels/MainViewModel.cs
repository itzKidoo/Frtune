using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Frtune.App.Services;
using Frtune.Core.Engine;
using Frtune.Core.Model;
using Frtune.Core.Tweaks;

namespace Frtune.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private PageViewModel? _pageBeforeSearch;

    public MainViewModel(AppServices services, IDialogService dialogs)
    {
        S = services;
        Dialogs = dialogs;
        AllTweaks = services.Catalog.Select(t => new TweakItemViewModel(t, services.Profile, OnPendingChanged)).ToList();

        Dashboard = new DashboardViewModel(this) { Group = "" };
        Search = new SearchViewModel(this);
        Pages = new ObservableCollection<PageViewModel>
        {
            Dashboard,
            new QuickOptimizeViewModel(this) { Group = "" },
            Tweaks(TweakCategory.Gaming, "Gaming", "", "Latency, GPU scheduling and game priority.", new GameProfilesViewModel(this)),
            Tweaks(TweakCategory.Performance, "Performance", "", "Power plan, CPU parking, services and file system."),
            Tweaks(TweakCategory.Network, "Network", "", "TCP tuning, Nagle, DNS and network resets.", new NetworkToolsViewModel(this)),
            Tweaks(TweakCategory.Privacy, "Privacy", "", "Telemetry, advertising, activity history, Copilot and Recall."),
            Tweaks(TweakCategory.Debloat, "Debloat", "", "Preinstalled apps, Edge nags, widgets and OneDrive.", new AppsViewModel(this)),
            Tweaks(TweakCategory.Services, "Services", "", "Safe-to-disable presets and every service on this PC.", new ServiceListViewModel(this)),
            new StartupViewModel(this) { Group = "" },
            new CleanerViewModel(this) { Group = "" },
            Tweaks(TweakCategory.Input, "Input", "", "Mouse acceleration, keyboard repeat and accessibility prompts."),
            Tweaks(TweakCategory.Power, "Power", "", "Power-plan settings for the active plan, and a custom plan creator.", new PowerToolsViewModel(this)),
            Tweaks(TweakCategory.Visuals, "Visuals", "", "Visual effects, Explorer and taskbar."),
            Tweaks(TweakCategory.Advanced, "Advanced", "", "Security trade-offs. Off by default, never part of a preset, and every one asks for confirmation."),
            new BackupsViewModel(this) { Group = "" },
            new HardwareViewModel(this) { Group = "Extras" },
            new BenchmarkViewModel(this) { Group = "Extras" },
            new ProcessesViewModel(this) { Group = "Extras" },
            new SettingsViewModel(this) { Group = "Extras" },
        };
        _currentPage = Dashboard;
        Toasts = services.Toasts.Items;
        UpdateRestartBanner();
    }

    public AppServices S { get; }
    public IDialogService Dialogs { get; }
    public IReadOnlyList<TweakItemViewModel> AllTweaks { get; }
    public ObservableCollection<PageViewModel> Pages { get; }
    public DashboardViewModel Dashboard { get; }
    public SearchViewModel Search { get; }
    public ObservableCollection<Toast> Toasts { get; }
    public string SystemSummary => S.Profile.Summary;

    private PageViewModel _currentPage;

    /// <summary>
    /// The page in the content area. Null is ignored: the sidebar ListBox pushes null when the
    /// current page (e.g. search results) isn't one of its items.
    /// </summary>
    public PageViewModel CurrentPage
    {
        get => _currentPage;
        set
        {
            if (value is null || ReferenceEquals(value, _currentPage)) return;
            _currentPage = value;
            OnPropertyChanged();
            _ = value.ActivateAsync();
        }
    }
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private int _pendingCount;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _busyText = "";
    [ObservableProperty] private double _busyProgress;
    [ObservableProperty] private string? _restartBanner;
    [ObservableProperty] private Preset _selectedPreset;

    public bool HasPending => PendingCount > 0;

    private TweakPageViewModel Tweaks(TweakCategory c, string title, string glyph, string subtitle, object? extra = null) =>
        new(this, c, title, glyph, subtitle, extra);

    partial void OnSearchTextChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            if (CurrentPage == Search) CurrentPage = _pageBeforeSearch ?? Dashboard;
            return;
        }
        if (CurrentPage != Search) _pageBeforeSearch = CurrentPage;
        Search.Query = value.Trim();
        CurrentPage = Search;
    }

    [RelayCommand]
    private void Navigate(PageViewModel page)
    {
        SearchText = "";
        CurrentPage = page;
    }

    public void NavigateTo<T>() where T : PageViewModel => Navigate(Pages.OfType<T>().First());

    public void NavigateToCategory(TweakCategory c) => Navigate(Pages.OfType<TweakPageViewModel>().First(p => p.Category == c));

    // ---------------------------------------------------------------- status

    public async Task InitializeAsync()
    {
        _ = Dashboard.ActivateAsync();
        await RefreshStatusesAsync(AllTweaks);
        var reasons = Core.Engine.RestartDetector.PendingReasons(S.Registry);
        if (reasons.Count > 0) RestartBanner = "Windows is waiting for a restart: " + string.Join("; ", reasons);
    }

    /// <summary>Reads each tweak's real state on background threads (some need PowerShell and take ~0.5 s).</summary>
    public async Task RefreshStatusesAsync(IEnumerable<TweakItemViewModel> items)
    {
        var list = items.ToList();
        foreach (var i in list) i.IsChecking = true;
        using var gate = new SemaphoreSlim(6);
        await Task.WhenAll(list.Select(async item =>
        {
            await gate.WaitAsync();
            try
            {
                var status = await Task.Run(() => S.Engine.GetStatus(item.Tweak));
                item.SetActual(status);
            }
            finally { gate.Release(); }
        }));
        Dashboard.RecomputeHealth();
    }

    private void OnPendingChanged()
    {
        // Called while AllTweaks is still being built in the constructor.
        if (AllTweaks is null) return;
        PendingCount = AllTweaks.Count(t => t.IsPending);
        OnPropertyChanged(nameof(HasPending));
        if (Pages is not null) foreach (var q in Pages.OfType<QuickOptimizeViewModel>()) q.RefreshPending();
    }

    // ---------------------------------------------------------------- presets

    /// <summary>Pre-toggles a preset's tweaks. Nothing changes until Apply; anything already on stays on.</summary>
    [RelayCommand]
    public void SelectPreset(Preset preset)
    {
        SelectedPreset = preset;
        if (preset == Preset.None) { DiscardPending(); return; }
        var ids = Presets.Select(S.Catalog, preset, S.Profile).Select(t => t.Id).ToHashSet();
        foreach (var t in AllTweaks.Where(t => t.CanToggle))
            t.IsOn = t.ActuallyOn || ids.Contains(t.Tweak.Id);
    }

    [RelayCommand]
    private void DiscardPending()
    {
        foreach (var t in AllTweaks) t.DiscardPending();
        SelectedPreset = Preset.None;
    }

    /// <summary>The dashboard's big button: Balanced preset plus everything recommended for this PC.</summary>
    [RelayCommand]
    private async Task OptimizeAsync()
    {
        var ids = Presets.Select(S.Catalog, Preset.Balanced, S.Profile).Select(t => t.Id).ToHashSet();
        foreach (var t in AllTweaks.Where(t => t.CanToggle && t.Tweak.Risk != TweakRisk.Advanced))
            if (ids.Contains(t.Tweak.Id) || t.IsRecommended) t.IsOn = true;
        if (PendingCount == 0)
        {
            S.Toasts.Show(ToastKind.Success, "Already optimised", "Every recommended tweak is already on.");
            return;
        }
        await ApplyAsync();
    }

    // ---------------------------------------------------------------- apply

    [RelayCommand]
    private async Task ApplyAsync()
    {
        var requests = AllTweaks.Where(t => t.IsPending).Select(t => new TweakRequest(t.Tweak, t.IsOn)).ToList();
        if (requests.Count == 0) return;
        await RunAsync(requests);
    }

    public async Task RunAsync(IReadOnlyList<TweakRequest> requests)
    {
        IsBusy = true;
        BusyText = "Reading current values…";
        BusyProgress = 0;
        IReadOnlyList<PlanItem> plan;
        try { plan = await Task.Run(() => S.Engine.Plan(requests)); }
        finally { IsBusy = false; }

        var creates = S.Settings.CreateRestorePoint && plan.Any(p => p.Enable && p.CanRun);
        if (!Dialogs.ConfirmPlan(plan, creates)) return;

        var runnable = plan.Where(p => p.CanRun).Select(p => new TweakRequest(p.Tweak, p.Enable)).ToList();
        if (runnable.Count == 0) return;

        var options = new ExecuteOptions { CreateRestorePoint = S.Settings.CreateRestorePoint, ExportRawBackup = S.Settings.ExportRawBackup };
        var report = await ExecuteWithProgressAsync(runnable, options);
        if (report.Aborted)
        {
            if (!Dialogs.Ask("Restore point failed", report.AbortReason +
                    "\n\nfrtune still saves its own backup of every value before changing it, so each tweak stays individually reversible. Continue without a restore point?",
                    "Continue anyway", danger: true))
            {
                S.Toasts.Show(ToastKind.Warning, "Nothing was changed", "The restore point couldn't be created.");
                return;
            }
            report = await ExecuteWithProgressAsync(runnable, options with { ContinueWithoutRestorePoint = true });
        }
        await ReportAsync(report);
    }

    private async Task<ExecuteReport> ExecuteWithProgressAsync(IReadOnlyList<TweakRequest> requests, ExecuteOptions options)
    {
        IsBusy = true;
        try
        {
            var progress = new Progress<EngineProgress>(p =>
            {
                BusyText = p.Message;
                BusyProgress = p.Total == 0 ? 0 : 100.0 * p.Done / p.Total;
            });
            return await S.Engine.ExecuteAsync(requests, options, progress);
        }
        finally { IsBusy = false; }
    }

    private async Task ReportAsync(ExecuteReport report)
    {
        if (report.RestorePoint is { Success: true }) S.Toasts.Show(ToastKind.Info, "Restore point created");
        if (report.Succeeded > 0)
            S.Toasts.Show(ToastKind.Success, $"{report.Succeeded} change(s) done", string.Join(", ", report.Outcomes.Where(o => o.Success).Select(o => o.Tweak.Name).Take(4)) + (report.Succeeded > 4 ? "…" : ""));
        foreach (var f in report.Outcomes.Where(o => !o.Success))
            S.Toasts.Show(ToastKind.Error, $"Failed: {f.Tweak.Name}", f.Message);
        if (report.Restart > S.PendingRestart) S.PendingRestart = report.Restart;
        UpdateRestartBanner();
        if (report.Restart != RestartKind.None)
            S.Toasts.Show(ToastKind.Restart, "Restart required", RestartBanner ?? "");

        var touched = report.Outcomes.Select(o => o.Tweak.Id).ToHashSet();
        await RefreshStatusesAsync(AllTweaks.Where(t => touched.Contains(t.Tweak.Id)));
        SelectedPreset = Preset.None;
    }

    // ---------------------------------------------------------------- restart

    public void UpdateRestartBanner()
    {
        RestartBanner = S.PendingRestart switch
        {
            RestartKind.Reboot => "Some changes take effect after you restart your PC.",
            RestartKind.SignOut => "Some changes take effect after you sign out and back in.",
            RestartKind.ExplorerRestart => "Some changes take effect after Explorer restarts.",
            _ => RestartBanner,
        };
        RestartActionText = S.PendingRestart switch
        {
            RestartKind.SignOut => "Sign out now",
            RestartKind.ExplorerRestart => "Restart Explorer",
            _ => "Restart now",
        };
    }

    [ObservableProperty] private string _restartActionText = "Restart now";

    [RelayCommand]
    private void RestartNow()
    {
        switch (S.PendingRestart)
        {
            case RestartKind.ExplorerRestart:
                foreach (var p in Process.GetProcessesByName("explorer")) { try { p.Kill(); } catch { } }
                Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true });
                S.PendingRestart = RestartKind.None;
                RestartBanner = null;
                break;
            case RestartKind.SignOut:
                if (Dialogs.Ask("Sign out", "Save your work in other apps first. Sign out now?", "Sign out"))
                    S.Commands.Run("shutdown.exe", "/l");
                break;
            default:
                if (Dialogs.Ask("Restart", "Save your work in other apps first. Restart in 5 seconds?", "Restart"))
                    S.Commands.Run("shutdown.exe", "/r /t 5 /c \"frtune: restarting to finish applying changes\"");
                break;
        }
    }

    [RelayCommand]
    private void DismissRestart() => RestartBanner = null;

    [RelayCommand]
    private void DismissToast(Toast t) => S.Toasts.Dismiss(t);
}
