using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Frtune.App.Services;
using Frtune.Core.Engine;

namespace Frtune.App.ViewModels;

public sealed record AppliedRow(string TweakId, string Name, string When, int Values, TweakItemViewModel? Item);

public sealed record LogRow(string When, string Action, string Title, bool Success, string Details)
{
    public string Glyph => Success ? "" : "";
}

public sealed partial class BackupsViewModel : PageViewModel
{
    private readonly MainViewModel _main;

    public BackupsViewModel(MainViewModel main) : base("Backups & Restore", "", "Everything frtune changed, the original values it saved, and a full change log.")
    {
        _main = main;
        _main.S.Log.EntryAdded += _ => System.Windows.Application.Current.Dispatcher.BeginInvoke(Refresh);
    }

    public ObservableCollection<AppliedRow> Applied { get; } = new();
    public ObservableCollection<LogRow> Log { get; } = new();
    public string DataFolder => AppServices.DataDir;

    protected override Task LoadAsync() { Refresh(); return Task.CompletedTask; }
    protected override Task OnReactivatedAsync() { Refresh(); return Task.CompletedTask; }

    private void Refresh()
    {
        Applied.Clear();
        foreach (var s in _main.S.Backups.All().OrderByDescending(s => s.CapturedAt))
        {
            var item = _main.AllTweaks.FirstOrDefault(t => t.Tweak.Id == s.TweakId);
            Applied.Add(new AppliedRow(s.TweakId, item?.Name ?? s.TweakId, s.CapturedAt.LocalDateTime.ToString("g"), s.Values.Count, item));
        }
        Log.Clear();
        foreach (var e in _main.S.Log.Entries.AsEnumerable().Reverse().Take(300))
            Log.Add(new LogRow(e.Timestamp.LocalDateTime.ToString("g"), e.Action, e.Title, e.Success,
                string.Join("\n", e.Details.Concat(e.Error is null ? Array.Empty<string>() : new[] { "Error: " + e.Error }))));
    }

    [RelayCommand]
    private async Task RevertAsync(AppliedRow row)
    {
        if (row.Item is null) return;
        await _main.RunAsync(new[] { new TweakRequest(row.Item.Tweak, false) });
        Refresh();
    }

    [RelayCommand]
    private async Task RestoreAllAsync()
    {
        var all = _main.S.Backups.All();
        if (all.Count == 0)
        {
            _main.S.Toasts.Show(ToastKind.Info, "Nothing to restore", "frtune hasn't changed anything.");
            return;
        }
        var byId = _main.AllTweaks.ToDictionary(t => t.Tweak.Id);
        var requests = all.Where(s => byId.ContainsKey(s.TweakId)).Select(s => new TweakRequest(byId[s.TweakId].Tweak, false)).ToList();
        await _main.RunAsync(requests);
        Refresh();
    }

    [RelayCommand]
    private async Task CreateRestorePointAsync()
    {
        var r = await new Frtune.Windows.Platform.RestorePointService(_main.S.Commands).CreateAsync("frtune: manual restore point");
        _main.S.Log.Add("restore-point", r.Success ? "Manual restore point created" : "Manual restore point not created", r.Success || r.Skipped, details: new[] { r.Message });
        _main.S.Toasts.Show(r.Success ? ToastKind.Success : r.Skipped ? ToastKind.Info : ToastKind.Error, r.Success ? "Restore point created" : "Restore point", r.Message);
    }

    [RelayCommand]
    private void OpenSystemRestore() => Process.Start(new ProcessStartInfo("rstrui.exe") { UseShellExecute = true });

    [RelayCommand]
    private void OpenFolder() => Process.Start(new ProcessStartInfo("explorer.exe", $"\"{AppServices.DataDir}\"") { UseShellExecute = true });

    [RelayCommand]
    private void OpenLogFile()
    {
        var p = _main.S.Log.FilePath;
        if (p is not null && File.Exists(p)) Process.Start(new ProcessStartInfo("notepad.exe", $"\"{p}\"") { UseShellExecute = true });
    }
}
