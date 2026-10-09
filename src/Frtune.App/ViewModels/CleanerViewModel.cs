using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Frtune.App.Services;
using Frtune.Core.Tools;

namespace Frtune.App.ViewModels;

public sealed partial class CleanRow : ObservableObject
{
    public CleanRow(CleanerCategory c)
    {
        Category = c;
        _isSelected = c.DefaultSelected;
    }

    public CleanerCategory Category { get; }
    public string Name => Category.Name;
    public string Description => Category.Description;
    public string Risk => Category.Risk.ToString();
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private long _size = -1;
    public string SizeText => Size < 0 ? "–" : CleanerCatalog.FormatBytes(Size);
    partial void OnSizeChanged(long value) => OnPropertyChanged(nameof(SizeText));
}

public sealed record FileRow(string Path, string SizeText, string Info);

public sealed partial class CleanerViewModel : PageViewModel
{
    private readonly MainViewModel _main;
    private CancellationTokenSource? _cts;

    public CleanerViewModel(MainViewModel main) : base("Cleaner", "", "See how much space each category uses, choose what to clean, then clean. Files in use are skipped, never forced.")
        => _main = main;

    public ObservableCollection<CleanRow> Rows { get; } = new();
    public ObservableCollection<FileRow> Found { get; } = new();

    [ObservableProperty] private bool _busy;
    [ObservableProperty] private double _progress;
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private string _foundTitle = "";
    [ObservableProperty] private bool _weeklyClean;
    private bool _loadingWeekly;

    public string SelectedTotal => CleanerCatalog.FormatBytes(Rows.Where(r => r.IsSelected && r.Size > 0).Sum(r => r.Size));

    protected override async Task LoadAsync()
    {
        foreach (var c in _main.S.CleanerCategories())
        {
            var row = new CleanRow(c);
            row.PropertyChanged += (_, e) => { if (e.PropertyName is nameof(CleanRow.IsSelected) or nameof(CleanRow.Size)) OnPropertyChanged(nameof(SelectedTotal)); };
            Rows.Add(row);
        }
        _loadingWeekly = true;
        WeeklyClean = await Task.Run(() => _main.S.ScheduledClean.IsEnabled);
        _loadingWeekly = false;
        await ScanAsync();
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        Busy = true;
        _cts = new CancellationTokenSource();
        var i = 0;
        foreach (var r in Rows)
        {
            Status = $"Measuring {r.Name}…";
            Progress = 100.0 * i++ / Rows.Count;
            r.Size = await Task.Run(() => { try { return r.Category.Target.Measure(_cts.Token); } catch { return 0L; } });
        }
        Progress = 100;
        Status = $"Found {CleanerCatalog.FormatBytes(Rows.Sum(r => Math.Max(0, r.Size)))} across {Rows.Count} categories.";
        Busy = false;
    }

    [RelayCommand]
    private async Task CleanAsync()
    {
        var selected = Rows.Where(r => r.IsSelected).ToList();
        if (selected.Count == 0) return;
        var risky = selected.Where(r => r.Category.Risk != Core.Model.TweakRisk.Safe).ToList();
        if (!_main.Dialogs.Ask("Clean", $"Delete files in these categories?\n\n• {string.Join("\n• ", selected.Select(r => $"{r.Name} ({r.SizeText})"))}" +
                (risky.Count > 0 ? "\n\n⚠ " + string.Join("\n⚠ ", risky.Select(r => $"{r.Name}: {r.Description}")) : ""),
                "Clean", danger: risky.Count > 0)) return;

        Busy = true;
        _cts = new CancellationTokenSource();
        long freed = 0;
        int skipped = 0;
        for (var i = 0; i < selected.Count; i++)
        {
            var r = selected[i];
            Status = $"Cleaning {r.Name}…";
            Progress = 100.0 * i / selected.Count;
            var result = await Task.Run(() => { try { return r.Category.Target.Clean(null, _cts.Token); } catch { return new CleanResult(0, 0, 0); } });
            freed += result.BytesFreed;
            skipped += result.FilesSkipped;
            _main.S.Log.Add("clean", $"Cleaned {r.Name}", details: new[] { $"Freed {CleanerCatalog.FormatBytes(result.BytesFreed)}, {result.FilesDeleted} files deleted, {result.FilesSkipped} skipped (in use)" });
            r.Size = await Task.Run(() => { try { return r.Category.Target.Measure(_cts.Token); } catch { return 0L; } });
        }
        Progress = 100;
        Status = $"Freed {CleanerCatalog.FormatBytes(freed)}. {skipped} file(s) were in use and skipped.";
        _main.S.Toasts.Show(ToastKind.Success, $"Freed {CleanerCatalog.FormatBytes(freed)}", skipped > 0 ? $"{skipped} files in use were skipped." : "");
        Busy = false;
        _main.Dashboard.RecomputeHealth();
    }

    partial void OnWeeklyCleanChanged(bool value)
    {
        if (_loadingWeekly) return;
        try
        {
            if (value) _main.S.ScheduledClean.Enable(Environment.ProcessPath!);
            else _main.S.ScheduledClean.Disable();
            _main.S.Toasts.Show(ToastKind.Success, value ? "Weekly clean scheduled" : "Weekly clean removed",
                value ? "Sundays at 12:00: temp files, browser caches and crash dumps." : "");
        }
        catch (Exception ex)
        {
            _main.S.Toasts.Show(ToastKind.Error, "Task Scheduler error", ex.Message);
        }
    }

    [RelayCommand]
    private async Task FindLargeAsync()
    {
        var folder = _main.Dialogs.PickFolder();
        if (folder is null) return;
        Busy = true;
        Status = $"Scanning {folder}…";
        _cts = new CancellationTokenSource();
        var files = await Task.Run(() => FileFinder.LargestFiles(folder, 50, 50L << 20, _cts.Token));
        Found.Clear();
        foreach (var f in files) Found.Add(new FileRow(f.Path, CleanerCatalog.FormatBytes(f.Size), f.Modified.ToString("yyyy-MM-dd")));
        FoundTitle = $"Largest files in {folder} (over 50 MB)";
        Status = $"{files.Count} large files found.";
        Busy = false;
    }

    [RelayCommand]
    private async Task FindDuplicatesAsync()
    {
        var folder = _main.Dialogs.PickFolder();
        if (folder is null) return;
        Busy = true;
        _cts = new CancellationTokenSource();
        var progress = new Progress<string>(p => Status = "Hashing " + Path.GetFileName(p));
        var groups = await Task.Run(() => FileFinder.Duplicates(folder, 1L << 20, progress, _cts.Token));
        Found.Clear();
        foreach (var g in groups.Take(100))
            foreach (var p in g.Paths)
                Found.Add(new FileRow(p, CleanerCatalog.FormatBytes(g.Size), $"copy of {g.Hash[..8]} ({g.Paths.Count}×)"));
        FoundTitle = $"Duplicate files in {folder} (over 1 MB)";
        Status = $"{groups.Count} groups of duplicates, {CleanerCatalog.FormatBytes(groups.Sum(g => g.Wasted))} wasted.";
        Busy = false;
    }

    [RelayCommand]
    private void Cancel() => _cts?.Cancel();

    [RelayCommand]
    private void Reveal(FileRow row) => Process.Start("explorer.exe", $"/select,\"{row.Path}\"");

    [RelayCommand]
    private void Recycle(FileRow row)
    {
        if (!_main.Dialogs.Ask("Move to Recycle Bin", $"Move this file to the Recycle Bin?\n\n{row.Path}", "Move")) return;
        if (RecycleBin.Send(row.Path))
        {
            Found.Remove(row);
            _main.S.Log.Add("clean", "Moved file to Recycle Bin", details: new[] { row.Path });
        }
        else _main.S.Toasts.Show(ToastKind.Error, "Couldn't move the file", row.Path);
    }
}
