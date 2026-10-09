using CommunityToolkit.Mvvm.ComponentModel;
using Frtune.Core.Engine;
using Frtune.Core.Model;

namespace Frtune.App.ViewModels;

public sealed record PresetCard(Preset Preset, string Name, string Description, IReadOnlyList<string> TweakNames)
{
    public string CountText => $"{TweakNames.Count} tweaks";
    public string Preview => string.Join(" · ", TweakNames);
}

public sealed partial class QuickOptimizeViewModel : PageViewModel
{
    public QuickOptimizeViewModel(MainViewModel main) : base("Quick Optimize", "", "Pick a profile. It pre-selects matching tweaks for this PC. Fine-tune on each page, then press Apply.")
    {
        Main = main;
        Cards = Presets.All.Select(p => new PresetCard(p.Preset, p.Name, p.Description,
            Presets.Select(main.S.Catalog, p.Preset, main.S.Profile).Select(t => t.Name).ToList())).ToList();
    }

    public MainViewModel Main { get; }
    public IReadOnlyList<PresetCard> Cards { get; }
    public IEnumerable<TweakItemViewModel> Pending => Main.AllTweaks.Where(t => t.IsPending);

    protected override Task OnReactivatedAsync()
    {
        OnPropertyChanged(nameof(Pending));
        return Task.CompletedTask;
    }

    public void RefreshPending() => OnPropertyChanged(nameof(Pending));
}
