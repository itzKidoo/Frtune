using Frtune.Core.Model;
using static Frtune.Core.Model.TweakCategory;
using static Frtune.Core.Model.TweakRisk;

namespace Frtune.Core.Tweaks.Catalog;

/// <summary>Reversible debloat settings. App removal is a separate tool on the Debloat page.</summary>
internal static class DebloatTweaks
{
    private const string Cdm = @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";
    private const string Edge = @"SOFTWARE\Policies\Microsoft\Edge";

    public static IEnumerable<Tweak> All()
    {
        yield return new Tweak("debloat.consumer", "Stop auto-installing suggested apps",
                "Turns off Windows consumer features and silent app installs, so removed apps (Candy Crush, TikTok…) aren't reinstalled. The policy value only works on Enterprise/Education; the per-user values work on every edition.",
                Debloat, Safe)
            .Hklm(@"SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableWindowsConsumerFeatures", 1)
            .Hkcu(Cdm, "SilentInstalledAppsEnabled", 0)
            .Hkcu(Cdm, "OemPreInstalledAppsEnabled", 0)
            .Hkcu(Cdm, "PreInstalledAppsEnabled", 0)
            .Hkcu(Cdm, "ContentDeliveryAllowed", 0)
            .In(Preset.Balanced | Preset.Privacy | Preset.LowEnd | Preset.Gaming | Preset.MaxPerformance);

        yield return new Tweak("debloat.widgets11", "Disable Widgets and the news feed",
                "Turns off the Widgets board (news and interests) by policy.",
                Debloat, Safe)
            .Hklm(@"SOFTWARE\Policies\Microsoft\Dsh", "AllowNewsAndInterests", 0)
            .When(s => s.IsWindows11, "Windows 11 only")
            .Needs(RestartKind.ExplorerRestart)
            .In(Preset.LowEnd | Preset.Privacy | Preset.Gaming | Preset.MaxPerformance);

        yield return new Tweak("debloat.news10", "Disable News and Interests on the taskbar",
                "Removes the weather/news widget from the Windows 10 taskbar by policy.",
                Debloat, Safe)
            .Hklm(@"SOFTWARE\Policies\Microsoft\Windows\Windows Feeds", "EnableFeeds", 0)
            .When(s => !s.IsWindows11, "Windows 10 only")
            .Needs(RestartKind.ExplorerRestart)
            .In(Preset.LowEnd | Preset.Privacy | Preset.Gaming | Preset.MaxPerformance);

        yield return new Tweak("debloat.edge", "Quiet Microsoft Edge",
                "Edge policies: no first-run wizard, no \"make Edge default\" nags, no Startup Boost and no running in the background after you close it. Edge itself keeps working.",
                Debloat, Safe)
            .Hklm(Edge, "HideFirstRunExperience", 1)
            .Hklm(Edge, "DefaultBrowserSettingEnabled", 0)
            .Hklm(Edge, "StartupBoostEnabled", 0)
            .Hklm(Edge, "BackgroundModeEnabled", 0)
            .In(Preset.Balanced | Preset.LowEnd | Preset.Gaming | Preset.MaxPerformance);

        yield return new Tweak("debloat.onedrive", "Disable OneDrive sync",
                "Stops OneDrive syncing by policy (DisableFileSyncNGSC). It doesn't uninstall it or touch your files. To fully remove OneDrive, use the uninstall button on this page.",
                Debloat, Moderate)
            .Hklm(@"SOFTWARE\Policies\Microsoft\Windows\OneDrive", "DisableFileSyncNGSC", 1)
            .Warn("Files that only exist in OneDrive (\"online-only\") will no longer be reachable from File Explorer until you turn this back off.")
            .Needs(RestartKind.SignOut);
    }
}
