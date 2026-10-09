using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Frtune.App.Services;
using Frtune.Core.Tools;

namespace Frtune.App.ViewModels;

public sealed partial class AppRow : ObservableObject
{
    public AppRow(AppxPackage p)
    {
        Package = p;
        IsProtected = AppxCatalog.IsProtected(p.Name);
        Friendly = AppxCatalog.FriendlyName(p.Name);
        _isSelected = !IsProtected && Friendly is not null;
    }

    public AppxPackage Package { get; }
    public string Name => Friendly ?? Package.Name;
    public string PackageName => Package.Name;
    public string? Friendly { get; }
    public bool IsProtected { get; }
    public bool IsSuggested => Friendly is not null;
    public bool CanSelect => !IsProtected;
    [ObservableProperty] private bool _isSelected;
}

/// <summary>Store app removal checklist with a protected whitelist.</summary>
public sealed partial class AppsViewModel : PageViewModel
{
    private readonly MainViewModel _main;
    public AppsViewModel(MainViewModel main) : base("Installed apps", "") => _main = main;

    public ObservableCollection<AppRow> Apps { get; } = new();
    [ObservableProperty] private bool _loading;
    [ObservableProperty] private string _status = "";

    protected override Task LoadAsync() => RefreshAsync();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        Loading = true;
        Status = "Reading installed Store apps…";
        var list = await Task.Run(() => _main.S.Appx.List());
        Apps.Clear();
        foreach (var a in list.Select(p => new AppRow(p)).OrderByDescending(r => r.IsSuggested).ThenBy(r => r.IsProtected).ThenBy(r => r.Name))
            Apps.Add(a);
        Status = $"{Apps.Count} apps · {Apps.Count(a => a.IsSuggested)} suggested for removal · {Apps.Count(a => a.IsProtected)} protected";
        Loading = false;
    }

    [RelayCommand]
    private async Task RemoveSelectedAsync()
    {
        var selected = Apps.Where(a => a.IsSelected && !a.IsProtected).ToList();
        if (selected.Count == 0) return;
        if (!_main.Dialogs.Ask("Remove apps",
                $"Remove these {selected.Count} app(s) for your user account?\n\n• " + string.Join("\n• ", selected.Select(a => a.Name)) +
                "\n\nThis can't be undone with a toggle. Reinstall any of them from the Microsoft Store if you need them again. Every removal is written to the change log.",
                "Remove", danger: true)) return;
        Loading = true;
        var ok = 0;
        foreach (var a in selected)
        {
            Status = $"Removing {a.Name}…";
            var (success, msg) = await Task.Run(() => _main.S.Appx.Remove(a.Package));
            if (success) ok++;
            else _main.S.Toasts.Show(ToastKind.Error, $"Couldn't remove {a.Name}", msg);
        }
        _main.S.Toasts.Show(ToastKind.Success, $"Removed {ok} app(s)");
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task UninstallOneDriveAsync()
    {
        if (!_main.Dialogs.Ask("Uninstall OneDrive",
                "This runs Microsoft's OneDrive uninstaller.\n\n⚠ Files that are \"online-only\" (cloud icon) are NOT on this PC and won't be reachable until you reinstall OneDrive or open onedrive.com. Files already downloaded stay in your OneDrive folder.\n\nReinstall later with: winget install Microsoft.OneDrive",
                "Uninstall OneDrive", danger: true)) return;
        Loading = true;
        var (ok, msg) = await Task.Run(() => _main.S.Appx.UninstallOneDrive());
        _main.S.Toasts.Show(ok ? ToastKind.Success : ToastKind.Error, ok ? "OneDrive uninstalled" : "OneDrive uninstall failed", msg);
        Loading = false;
    }
}
