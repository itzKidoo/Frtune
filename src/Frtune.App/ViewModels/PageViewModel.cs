using CommunityToolkit.Mvvm.ComponentModel;

namespace Frtune.App.ViewModels;

/// <summary>Base for every page shown in the content area.</summary>
public abstract partial class PageViewModel : ObservableObject
{
    protected PageViewModel(string title, string glyph, string subtitle = "")
    {
        Title = title;
        Glyph = glyph;
        Subtitle = subtitle;
    }

    public string Title { get; }
    public string Glyph { get; }
    public string Subtitle { get; }
    /// <summary>Sidebar group heading ("Optimize", "Tools", "Extras"…).</summary>
    public string Group { get; init; } = "";

    private bool _loaded;

    /// <summary>Called every time the page is shown. The first call runs <see cref="LoadAsync"/>.</summary>
    public async Task ActivateAsync()
    {
        if (_loaded) { await OnReactivatedAsync(); return; }
        _loaded = true;
        await LoadAsync();
    }

    protected virtual Task LoadAsync() => Task.CompletedTask;
    protected virtual Task OnReactivatedAsync() => Task.CompletedTask;
}
