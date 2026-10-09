using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Frtune.App.Services;
using Frtune.Windows.Tools;

namespace Frtune.App.ViewModels;

public sealed partial class StartupRow : ObservableObject
{
    private readonly Func<StartupRow, bool, Task> _toggle;
    private bool _suppress;

    public StartupRow(StartupItem item, Func<StartupRow, bool, Task> toggle)
    {
        Item = item;
        _toggle = toggle;
        _enabled = item.Enabled;
    }

    public StartupItem Item { get; set; }
    public string Name => Item.Name;
    public string Publisher => string.IsNullOrWhiteSpace(Item.Publisher) ? "Unknown publisher" : Item.Publisher;
    public string Impact => Item.Impact;
    public string Command => Item.Command;
    public string Source => Item.Source switch
    {
        StartupSource.RegistryUser => "Registry (you)",
        StartupSource.RegistryMachine => "Registry (all users)",
        StartupSource.RegistryMachine32 => "Registry (32-bit)",
        StartupSource.FolderUser => "Startup folder (you)",
        StartupSource.FolderCommon => "Startup folder (all users)",
        _ => "Scheduled task",
    };

    [ObservableProperty] private bool _enabled;

    partial void OnEnabledChanged(bool value)
    {
        if (_suppress) return;
        _ = _toggle(this, value);
    }

    public void Reset(bool value)
    {
        _suppress = true;
        Enabled = value;
        _suppress = false;
    }
}

public sealed partial class StartupViewModel : PageViewModel
{
    private readonly MainViewModel _main;

    public StartupViewModel(MainViewModel main) : base("Startup", "", "Apps that start with Windows. Disabling uses the same switch as Task Manager, so it's fully reversible.")
        => _main = main;

    public ObservableCollection<StartupRow> Items { get; } = new();
    [ObservableProperty] private bool _loading;
    [ObservableProperty] private string _summary = "";

    protected override Task LoadAsync() => RefreshAsync();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        Loading = true;
        var list = await Task.Run(() => _main.S.Startup.List());
        Items.Clear();
        foreach (var i in list) Items.Add(new StartupRow(i, ToggleAsync));
        UpdateSummary();
        Loading = false;
    }

    private async Task ToggleAsync(StartupRow row, bool enabled)
    {
        try
        {
            await Task.Run(() => _main.S.Startup.SetEnabled(row.Item, enabled));
            row.Item = row.Item with { Enabled = enabled };
            _main.S.Toasts.Show(ToastKind.Success, $"{row.Name} {(enabled ? "will start" : "won't start")} with Windows");
        }
        catch (Exception ex)
        {
            row.Reset(!enabled);
            _main.S.Toasts.Show(ToastKind.Error, $"Couldn't change {row.Name}", ex.Message);
        }
        UpdateSummary();
    }

    private void UpdateSummary() =>
        Summary = $"{Items.Count(i => i.Enabled)} of {Items.Count} enabled · Impact is estimated from program size; Windows doesn't expose its own rating.";
}
