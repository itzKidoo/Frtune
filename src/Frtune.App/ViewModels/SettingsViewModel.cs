using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Frtune.App.Services;
using Frtune.Core.Engine;

namespace Frtune.App.ViewModels;

public sealed record AccentChoice(string Name, string Hex);

public sealed partial class SettingsViewModel : PageViewModel
{
    private readonly MainViewModel _main;

    public SettingsViewModel(MainViewModel main) : base("Settings", "")
    {
        _main = main;
        var s = main.S.Settings;
        _accentHex = s.AccentHex;
        _updatePauseDays = s.UpdatePauseDays;
        _createRestorePoint = s.CreateRestorePoint;
        _exportRawBackup = s.ExportRawBackup;
    }

    public IReadOnlyList<AccentChoice> Accents { get; } = ThemeService.Accents.Select(a => new AccentChoice(a.Name, a.Hex)).ToList();
    public string SystemSummary => _main.S.Profile.Summary;
    public string DataFolder => AppServices.DataDir;

    [ObservableProperty] private string _accentHex;
    [ObservableProperty] private int _updatePauseDays;
    [ObservableProperty] private bool _createRestorePoint;
    [ObservableProperty] private bool _exportRawBackup;

    partial void OnAccentHexChanged(string value)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(value, "^#[0-9a-fA-F]{6}$")) return;
        ThemeService.ApplyAccent(value);
        Save(s => s.AccentHex = value);
    }

    partial void OnUpdatePauseDaysChanged(int value)
    {
        var v = Math.Clamp(value, 1, 35);
        Save(s => s.UpdatePauseDays = v);
        _main.S.RefreshContext();
    }

    partial void OnCreateRestorePointChanged(bool value)
    {
        if (!value && !_main.Dialogs.Ask("Turn off restore points?",
                "frtune will still save its own backup of every value before changing it, but you won't have a System Restore point to roll back to if Windows won't boot.",
                "Turn off", danger: true))
        {
            CreateRestorePoint = true;
            return;
        }
        Save(s => s.CreateRestorePoint = value);
    }

    partial void OnExportRawBackupChanged(bool value) => Save(s => s.ExportRawBackup = value);

    [RelayCommand]
    private void PickAccent(AccentChoice c) => AccentHex = c.Hex;

    private void Save(Action<AppSettings> change)
    {
        change(_main.S.Settings);
        _main.S.SaveSettings();
    }

    /// <summary>Exports the ids of every tweak that is currently on or selected.</summary>
    [RelayCommand]
    private void ExportProfile()
    {
        var path = _main.Dialogs.SaveFile("frtune profile (*.json)|*.json", "frtune-profile.json");
        if (path is null) return;
        var profile = new ProfileFile
        {
            Name = Path.GetFileNameWithoutExtension(path),
            Tweaks = _main.AllTweaks.Where(t => t.IsOn).Select(t => t.Tweak.Id).ToList(),
        };
        File.WriteAllText(path, profile.ToJson());
        _main.S.Toasts.Show(ToastKind.Success, "Profile exported", $"{profile.Tweaks.Count} tweaks saved to {Path.GetFileName(path)}");
    }

    /// <summary>Imports a profile as pending changes: nothing is applied until you press Apply.</summary>
    [RelayCommand]
    private void ImportProfile()
    {
        var path = _main.Dialogs.PickFile("frtune profile (*.json)|*.json");
        if (path is null) return;
        try
        {
            var profile = ProfileFile.FromJson(File.ReadAllText(path));
            var ids = profile.Tweaks.ToHashSet();
            var matched = 0;
            foreach (var t in _main.AllTweaks.Where(t => t.CanToggle))
            {
                var want = ids.Contains(t.Tweak.Id);
                if (want) matched++;
                // Advanced tweaks are never switched on by an imported file.
                if (want && t.Tweak.Risk == Core.Model.TweakRisk.Advanced && !t.ActuallyOn) continue;
                t.IsOn = want;
            }
            _main.S.Toasts.Show(ToastKind.Info, $"Imported \"{profile.Name}\"",
                $"{matched} tweaks match this PC. Review the pending changes and press Apply. Advanced tweaks are never imported.");
        }
        catch (Exception ex)
        {
            _main.S.Toasts.Show(ToastKind.Error, "Couldn't import profile", ex.Message);
        }
    }

    [RelayCommand]
    private void OpenDataFolder() => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{DataFolder}\"") { UseShellExecute = true });
}
