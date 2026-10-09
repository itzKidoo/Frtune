using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Frtune.App.Services;
using Frtune.Core.Abstractions;
using Frtune.Core.Changes;

namespace Frtune.App.ViewModels;

public sealed record PowerPlan(string Guid, string Name, bool Active);

/// <summary>Power plan list and a custom plan creator (duplicates a base plan and renames it).</summary>
public sealed partial class PowerToolsViewModel : PageViewModel
{
    private readonly MainViewModel _main;
    public PowerToolsViewModel(MainViewModel main) : base("Power plans", "")
    {
        _main = main;
        _base = Bases[1];
    }

    public static IReadOnlyList<Choice<string>> Bases { get; } = new[]
    {
        new Choice<string>(PowerGuids.Balanced, "Balanced"),
        new Choice<string>(PowerGuids.HighPerformance, "High performance"),
        new Choice<string>(PowerGuids.UltimatePerformanceTemplate, "Ultimate Performance"),
    };

    public ObservableCollection<PowerPlan> Plans { get; } = new();
    [ObservableProperty] private string _newName = "frtune Gaming";
    [ObservableProperty] private Choice<string> _base;

    protected override Task LoadAsync() => RefreshAsync();

    private async Task RefreshAsync()
    {
        var output = await Task.Run(() => _main.S.Commands.Run("powercfg.exe", "/list").StdOut);
        Plans.Clear();
        foreach (Match m in Regex.Matches(output, @"([0-9a-fA-F-]{36})\s+\((.+?)\)\s*(\*)?"))
            Plans.Add(new PowerPlan(m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Success));
    }

    [RelayCommand]
    private async Task ActivateAsync(PowerPlan plan)
    {
        await Task.Run(() => _main.S.Commands.Run("powercfg.exe", $"/setactive {plan.Guid}"));
        _main.S.Log.Add("tool", $"Activated power plan {plan.Name}", details: new[] { $"powercfg /setactive {plan.Guid}" });
        await RefreshAsync();
        await _main.RefreshStatusesAsync(_main.AllTweaks.Where(t => t.Tweak.Category == Core.Model.TweakCategory.Power || t.Tweak.Id is "perf.ultimateplan" or "perf.coreparking"));
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        var name = NewName.Trim().Replace("\"", "");
        if (name.Length == 0) return;
        try
        {
            var guid = await Task.Run(() =>
            {
                var dup = _main.S.Commands.Run("powercfg.exe", $"/duplicatescheme {Base.Value}").EnsureSuccess("powercfg /duplicatescheme");
                var g = Regex.Match(dup.StdOut, "[0-9a-fA-F]{8}-[0-9a-fA-F-]{27}").Value;
                _main.S.Commands.Run("powercfg.exe", $"/changename {g} \"{name}\" \"Created by frtune from {Base.Label}\"").EnsureSuccess("powercfg /changename");
                _main.S.Commands.Run("powercfg.exe", $"/setactive {g}").EnsureSuccess("powercfg /setactive");
                return g;
            });
            _main.S.Log.Add("tool", $"Created power plan \"{name}\"", details: new[] { $"powercfg /duplicatescheme {Base.Value} → {guid}", "Delete it in Control Panel › Power Options if you no longer want it." });
            _main.S.Toasts.Show(ToastKind.Success, $"Created and activated \"{name}\"", "The tweaks on this page now apply to this plan.");
            await RefreshAsync();
            await _main.RefreshStatusesAsync(_main.AllTweaks.Where(t => t.Tweak.Category == Core.Model.TweakCategory.Power));
        }
        catch (Exception ex)
        {
            _main.S.Toasts.Show(ToastKind.Error, "Couldn't create the plan", ex.Message);
        }
    }
}
