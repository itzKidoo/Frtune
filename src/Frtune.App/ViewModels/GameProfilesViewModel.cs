using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Frtune.App.Services;

namespace Frtune.App.ViewModels;

public sealed record GameRow(string Exe, string Name, string Gpu, string Priority);
public sealed record Choice<T>(T Value, string Label)
{
    public override string ToString() => Label;
}

/// <summary>Per-game GPU preference and CPU priority (persisted by Windows, applied on every launch).</summary>
public sealed partial class GameProfilesViewModel : PageViewModel
{
    private readonly MainViewModel _main;

    public GameProfilesViewModel(MainViewModel main) : base("Game profiles", "")
    {
        _main = main;
        _gpu = GpuChoices[2];
        _priority = PriorityChoices[1];
    }

    public static IReadOnlyList<Choice<int>> GpuChoices { get; } = new[] { new Choice<int>(0, "Let Windows decide"), new Choice<int>(1, "Power saving"), new Choice<int>(2, "High performance") };
    public static IReadOnlyList<Choice<uint>> PriorityChoices { get; } = new[] { new Choice<uint>(2, "Normal"), new Choice<uint>(6, "Above normal"), new Choice<uint>(3, "High") };

    public ObservableCollection<GameRow> Games { get; } = new();
    [ObservableProperty] private Choice<int> _gpu;
    [ObservableProperty] private Choice<uint> _priority;

    public string GpuNote => _main.S.Profile.IsMultiGpu
        ? "Your PC has more than one GPU: set games to High performance so they run on the dedicated card."
        : $"Your PC has one GPU ({_main.S.Profile.PrimaryGpu?.Name ?? "unknown"}), so GPU preference changes nothing. CPU priority still applies on every launch.";

    protected override Task LoadAsync()
    {
        Refresh();
        return Task.CompletedTask;
    }

    private void Refresh()
    {
        Games.Clear();
        foreach (var (exe, gpu, prio) in _main.S.Games.List())
            Games.Add(new GameRow(exe, Path.GetFileName(exe),
                GpuChoices.FirstOrDefault(c => c.Value == gpu)?.Label ?? "?",
                PriorityChoices.FirstOrDefault(c => c.Value == prio)?.Label ?? "Normal"));
    }

    [RelayCommand]
    private void AddGame()
    {
        var exe = _main.Dialogs.PickFile("Games (*.exe)|*.exe");
        if (exe is null) return;
        if (Priority.Value == 3 && !_main.Dialogs.Ask("High priority", "High priority can starve audio, Discord and input drivers on a 4-thread CPU if the game uses 100% CPU. Above normal is usually the better choice. Use High anyway?", "Use High"))
            return;
        _main.S.Games.Set(exe, Gpu.Value, Priority.Value);
        _main.S.Toasts.Show(ToastKind.Success, $"Profile saved for {Path.GetFileName(exe)}", "Applies the next time the game starts.");
        Refresh();
    }

    [RelayCommand]
    private void RemoveGame(GameRow row)
    {
        _main.S.Games.Remove(row.Exe);
        Refresh();
    }
}
