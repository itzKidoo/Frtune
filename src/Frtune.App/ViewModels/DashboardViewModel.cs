using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Frtune.Core.Tools;
using Frtune.Windows.Info;

namespace Frtune.App.ViewModels;

public sealed partial class UsageCard : ObservableObject
{
    private readonly Queue<double> _samples = new();

    public UsageCard(string title, string glyph, string detail)
    {
        Title = title;
        Glyph = glyph;
        Detail = detail;
        for (var i = 0; i < 60; i++) _samples.Enqueue(0);
        _history = _samples.ToArray();
    }

    public string Title { get; }
    public string Glyph { get; }
    [ObservableProperty] private string _detail;
    [ObservableProperty] private double _value;
    [ObservableProperty] private string _valueText = "–";
    /// <summary>Last 60 samples. Replaced (not mutated) each tick so the sparkline redraws.</summary>
    [ObservableProperty] private double[] _history;

    public void Push(double v, string? text = null)
    {
        Value = v;
        ValueText = text ?? $"{v:0}%";
        _samples.Enqueue(v);
        while (_samples.Count > 60) _samples.Dequeue();
        History = _samples.ToArray();
    }
}

public sealed partial class DashboardViewModel : PageViewModel
{
    private readonly MainViewModel _main;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private PerformanceMonitor? _monitor;
    private bool _sampling;
    private long _junkBytes = -1;

    public DashboardViewModel(MainViewModel main) : base("Dashboard", "", "Live usage, system health and one-click optimisation.")
    {
        _main = main;
        var p = main.S.Profile;
        Cpu = new UsageCard("CPU", "", $"{p.CpuName} · {p.CpuCores}C/{p.CpuThreads}T");
        Gpu = new UsageCard("GPU", "", p.PrimaryGpu is { } g ? $"{g.Name}{(g.VramGb > 0 ? $" · {g.VramGb:0.#} GB" : "")}" : "No GPU detected");
        Ram = new UsageCard("Memory", "", $"{p.TotalRamGb:0.#} GB installed");
        Disk = new UsageCard("Disk", "", $"System drive {p.SystemDrive} · {(p.SystemDriveIsSsd ? "SSD" : "HDD")}");
        Os = $"{p.OsName} {p.DisplayVersion}".Trim();
        OsDetail = $"Build {p.Build}.{p.Ubr} · {p.Edition} · {(p.IsLaptop ? "Laptop" : "Desktop")}";
        _timer.Tick += async (_, _) => await TickAsync();
    }

    public MainViewModel Main => _main;
    public UsageCard Cpu { get; }
    public UsageCard Gpu { get; }
    public UsageCard Ram { get; }
    public UsageCard Disk { get; }
    public string Os { get; }
    public string OsDetail { get; }
    public ObservableCollection<HealthFactor> HealthFactors { get; } = new();
    public IEnumerable<TweakItemViewModel> Recommended => _main.AllTweaks.Where(t => t.IsRecommended && t.IsApplicable);

    [ObservableProperty] private int _healthScore;
    [ObservableProperty] private string _healthGrade = "…";

    protected override async Task LoadAsync()
    {
        _monitor = await Task.Run(() => new PerformanceMonitor());
        _timer.Start();
        _junkBytes = await Task.Run(() =>
        {
            try
            {
                return _main.S.CleanerCategories().Where(c => c.DefaultSelected && c.Id != "recyclebin")
                    .Sum(c => { try { return c.Target.Measure(CancellationToken.None); } catch { return 0L; } });
            }
            catch { return -1L; }
        });
        RecomputeHealth();
    }

    private async Task TickAsync()
    {
        if (_monitor is null || _sampling) return;
        _sampling = true;
        try
        {
            var s = await Task.Run(_monitor.Sample);
            Cpu.Push(s.Cpu);
            Gpu.Push(s.Gpu);
            Ram.Push(s.Ram, $"{s.Ram:0}%");
            Ram.Detail = $"{s.RamUsedGb:0.0} of {s.RamTotalGb:0.#} GB in use";
            Disk.Push(s.Disk);
        }
        finally { _sampling = false; }
    }

    private int _startupCount = -1;
    private DateTime _startupCountedAt;

    /// <summary>Recalculates the score. Slow inputs (startup list uses PowerShell) are read off the UI thread and cached for 30 s.</summary>
    public async void RecomputeHealth()
    {
        try
        {
            if (_startupCount < 0 || DateTime.UtcNow - _startupCountedAt > TimeSpan.FromSeconds(30))
            {
                _startupCount = await Task.Run(() =>
                {
                    try { return _main.S.Startup.List().Count(i => i.Enabled); } catch { return 0; }
                });
                _startupCountedAt = DateTime.UtcNow;
            }
            var drive = new DriveInfo(_main.S.Profile.SystemDrive);
            var free = drive.IsReady ? 100.0 * drive.AvailableFreeSpace / drive.TotalSize : 50;
            var rec = _main.AllTweaks.Where(t => t.IsRecommended && t.IsApplicable && !t.IsChecking).ToList();
            var report = Core.Tools.HealthScore.Compute(free, Ram.Value, _startupCount, rec.Count, rec.Count(t => t.ActuallyOn), _junkBytes,
                _main.S.PendingRestart != Core.Model.RestartKind.None);
            HealthScore = report.Score;
            HealthGrade = report.Grade;
            HealthFactors.Clear();
            foreach (var f in report.Factors) HealthFactors.Add(f);
            OnPropertyChanged(nameof(Recommended));
        }
        catch (Exception ex)
        {
            HealthGrade = "Unavailable";
            _main.S.Log.Add("error", "Health score failed", false, error: ex.Message);
        }
    }

    [RelayCommand]
    private void OpenCleaner() => _main.NavigateTo<CleanerViewModel>();

    [RelayCommand]
    private void OpenStartup() => _main.NavigateTo<StartupViewModel>();
}
