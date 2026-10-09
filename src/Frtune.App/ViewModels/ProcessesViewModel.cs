using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Frtune.App.Services;
using Frtune.Windows.Tools;

namespace Frtune.App.ViewModels;

public sealed partial class ProcessItem : ObservableObject
{
    public ProcessItem(ProcessRow row) => Row = row;
    public ProcessRow Row { get; }
    public string Name => Row.Name;
    public int Pid => Row.Pid;
    public string Memory => $"{Row.MemoryMb:0} MB";
    public string Priority => Row.Priority;
    public bool IsCritical => ProcessTools.IsCritical(Row.Name);
    public bool CanKill => !IsCritical;
    [ObservableProperty] private bool _kill;
}

public sealed partial class ProcessesViewModel : PageViewModel
{
    private readonly MainViewModel _main;

    public ProcessesViewModel(MainViewModel main) : base("Processes", "",
        "Tick background apps to end, pick the game to boost, then Kill & boost. Windows and security processes can't be ended here.")
        => _main = main;

    public ObservableCollection<ProcessItem> Items { get; } = new();
    [ObservableProperty] private ProcessItem? _boostTarget;
    [ObservableProperty] private bool _loading;

    protected override Task LoadAsync() => RefreshAsync();
    protected override Task OnReactivatedAsync() => RefreshAsync();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        Loading = true;
        var list = await Task.Run(() => _main.S.Processes.List());
        Items.Clear();
        foreach (var r in list) Items.Add(new ProcessItem(r));
        Loading = false;
    }

    [RelayCommand]
    private async Task KillAndBoostAsync()
    {
        var kill = Items.Where(i => i.Kill && i.CanKill).ToList();
        if (kill.Count == 0 && BoostTarget is null) return;
        if (!_main.Dialogs.Ask("Kill & boost",
                (kill.Count > 0 ? $"End these processes (unsaved work in them is lost):\n• {string.Join("\n• ", kill.Select(k => $"{k.Name} ({k.Pid})"))}\n\n" : "") +
                (BoostTarget is not null ? $"Set {BoostTarget.Name} to High priority until it closes." : ""),
                "Do it", danger: kill.Count > 0)) return;
        var msg = await Task.Run(() => _main.S.Processes.KillAndBoost(kill.Select(k => k.Row), BoostTarget?.Row));
        _main.S.Toasts.Show(ToastKind.Success, "Kill & boost", msg);
        await RefreshAsync();
    }
}
