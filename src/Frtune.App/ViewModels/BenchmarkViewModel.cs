using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Frtune.App.Services;
using Frtune.Core.Tools;

namespace Frtune.App.ViewModels;

public sealed record CompareRow(string Metric, string Before, string After, string Change, bool Better);

public sealed partial class BenchmarkViewModel : PageViewModel
{
    private readonly MainViewModel _main;
    private CancellationTokenSource? _cts;

    public BenchmarkViewModel(MainViewModel main) : base("Benchmark", "",
        "A ~10 second CPU, memory and disk test. Run it before and after optimising to compare on this PC (the scores aren't comparable with other tools).")
        => _main = main;

    public ObservableCollection<BenchmarkResult> History { get; } = new();
    public ObservableCollection<CompareRow> Comparison { get; } = new();
    [ObservableProperty] private bool _running;
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private string _label = "Before";

    protected override Task LoadAsync()
    {
        Reload();
        return Task.CompletedTask;
    }

    private void Reload()
    {
        History.Clear();
        foreach (var r in _main.S.Benchmarks.Load().OrderByDescending(r => r.When)) History.Add(r);
        Comparison.Clear();
        if (History.Count < 2) return;
        var (after, before) = (History[0], History[1]);
        void Row(string m, double b, double a, string unit) =>
            Comparison.Add(new CompareRow(m, $"{b:0.#} {unit}", $"{a:0.#} {unit}", b == 0 ? "–" : $"{(a - b) / b * 100:+0.0;-0.0}%", a >= b));
        Row("CPU single-thread", before.CpuSingle, after.CpuSingle, "MB/s");
        Row("CPU multi-thread", before.CpuMulti, after.CpuMulti, "MB/s");
        Row("Memory bandwidth", before.MemoryGbps, after.MemoryGbps, "GB/s");
        Row("Disk write", before.DiskWriteMBps, after.DiskWriteMBps, "MB/s");
        Row("Disk read", before.DiskReadMBps, after.DiskReadMBps, "MB/s");
        CompareTitle = $"\"{before.Label}\" ({before.When.LocalDateTime:g}) vs \"{after.Label}\" ({after.When.LocalDateTime:g})";
    }

    [ObservableProperty] private string _compareTitle = "";

    [RelayCommand]
    private async Task RunAsync()
    {
        Running = true;
        _cts = new CancellationTokenSource();
        try
        {
            var progress = new Progress<string>(s => Status = s);
            var r = await Benchmark.RunAsync(Label, System.IO.Path.GetTempPath(), progress, _cts.Token);
            _main.S.Benchmarks.Add(r);
            Status = "Done.";
            Label = Label == "Before" ? "After" : Label;
            Reload();
        }
        catch (OperationCanceledException) { Status = "Cancelled."; }
        catch (Exception ex) { _main.S.Toasts.Show(ToastKind.Error, "Benchmark failed", ex.Message); }
        finally { Running = false; }
    }

    [RelayCommand]
    private void Cancel() => _cts?.Cancel();
}
