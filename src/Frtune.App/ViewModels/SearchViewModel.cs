using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Frtune.App.ViewModels;

/// <summary>Search results across every tweak page (name, description, id and the registry keys/commands it touches).</summary>
public sealed partial class SearchViewModel : PageViewModel
{
    private readonly MainViewModel _main;

    public SearchViewModel(MainViewModel main) : base("Search", "") => _main = main;

    public ObservableCollection<TweakItemViewModel> Results { get; } = new();
    public MainViewModel Main => _main;

    [ObservableProperty] private string _query = "";

    partial void OnQueryChanged(string value)
    {
        Results.Clear();
        foreach (var t in _main.AllTweaks.Where(t => t.Matches(value))) Results.Add(t);
        OnPropertyChanged(nameof(Summary));
    }

    public string Summary => $"{Results.Count} tweak(s) match \"{Query}\"";
}
