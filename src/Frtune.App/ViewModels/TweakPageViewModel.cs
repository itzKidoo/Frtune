using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using Frtune.Core.Model;

namespace Frtune.App.ViewModels;

/// <summary>A page of tweak cards for one category, optionally with an extra tool section.</summary>
public sealed partial class TweakPageViewModel : PageViewModel
{
    private readonly MainViewModel _main;

    public TweakPageViewModel(MainViewModel main, TweakCategory category, string title, string glyph, string subtitle, object? extra = null)
        : base(title, glyph, subtitle)
    {
        _main = main;
        Category = category;
        Extra = extra;
        Items = new ObservableCollection<TweakItemViewModel>(main.AllTweaks.Where(t => t.Tweak.Category == category));
        View = CollectionViewSource.GetDefaultView(Items);
        View.Filter = o => o is TweakItemViewModel t && (t.IsApplicable || ShowNotApplicable);
    }

    public TweakCategory Category { get; }
    public object? Extra { get; }
    public ObservableCollection<TweakItemViewModel> Items { get; }
    public ICollectionView View { get; }
    public MainViewModel Main => _main;

    public int HiddenCount => Items.Count(t => !t.IsApplicable);

    [ObservableProperty] private bool _showNotApplicable;
    partial void OnShowNotApplicableChanged(bool value) => View.Refresh();

    protected override Task LoadAsync() => Extra is PageViewModel p ? p.ActivateAsync() : Task.CompletedTask;
}
