using Frtune.Core.Abstractions;
using Frtune.Core.Model;
using static Frtune.Core.Model.TweakCategory;
using static Frtune.Core.Model.TweakRisk;

namespace Frtune.Core.Tweaks.Catalog;

internal static class VisualTweaks
{
    private const string Adv = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
    private const string Personalize = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string Desktop = @"Control Panel\Desktop";

    public static IEnumerable<Tweak> All()
    {
        yield return new Tweak("visuals.performance", "Visual effects: best performance (keep smooth fonts)",
                "Same as \"Adjust for best performance\" in System Properties, but keeps font smoothing and thumbnails. Sets VisualFXSetting = 3 (custom) and UserPreferencesMask = 90 12 03 80 10 00 00 00.",
                Visuals, Safe)
            .Hkcu(@"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", 3)
            .Reg(RegHive.CurrentUser, Desktop, "UserPreferencesMask", RegValue.FromHex("9012038010000000"))
            .Hkcu(Desktop, "FontSmoothing", "2")
            .Needs(RestartKind.SignOut)
            .In(Preset.LowEnd);

        yield return new Tweak("visuals.transparency", "Disable transparency",
                "Turns off the acrylic/mica transparency on the taskbar, Start and window frames.",
                Visuals, Safe)
            .Hkcu(Personalize, "EnableTransparency", 0)
            .In(Preset.LowEnd | Preset.MaxPerformance);

        yield return new Tweak("visuals.animations", "Disable window and taskbar animations",
                "No minimise/maximise animation and no taskbar animations. Windows feel snappier.",
                Visuals, Safe)
            .Hkcu(@"Control Panel\Desktop\WindowMetrics", "MinAnimate", "0")
            .Hkcu(Adv, "TaskbarAnimations", 0)
            .Hkcu(@"Software\Microsoft\Windows\DWM", "EnableAeroPeek", 0)
            .Needs(RestartKind.SignOut)
            .In(Preset.LowEnd | Preset.MaxPerformance);

        yield return new Tweak("visuals.shadows", "Disable shadows and translucent selection",
                "Removes icon-label shadows on the desktop and the translucent selection rectangle.",
                Visuals, Safe)
            .Hkcu(Adv, "ListviewShadow", 0)
            .Hkcu(Adv, "ListviewAlphaSelect", 0)
            .Needs(RestartKind.ExplorerRestart)
            .In(Preset.LowEnd);

        yield return new Tweak("visuals.menudelay", "Faster menus",
                "MenuShowDelay 400 → 50 ms: cascading menus open almost immediately.",
                Visuals, Safe)
            .Hkcu(Desktop, "MenuShowDelay", "50")
            .Needs(RestartKind.SignOut)
            .In(Preset.Balanced | Preset.Gaming | Preset.LowEnd | Preset.MaxPerformance);

        yield return new Tweak("visuals.classicmenu", "Classic right-click menu",
                "Windows 11 shows the full context menu straight away instead of \"Show more options\". Works by registering an empty InprocServer32 for the new menu's CLSID; revert removes the key.",
                Visuals, Safe)
            .Reg(RegHive.CurrentUser, @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32", "", RegValue.Str(""))
            .When(s => s.IsWindows11, "Windows 11 only")
            .Needs(RestartKind.ExplorerRestart)
            .In(Preset.Balanced);

        yield return new Tweak("visuals.extensions", "Show file extensions",
                "Shows .exe, .pdf and so on in File Explorer. It's also safer: you can spot \"invoice.pdf.exe\".",
                Visuals, Safe)
            .Hkcu(Adv, "HideFileExt", 0)
            .Needs(RestartKind.ExplorerRestart)
            .In(Preset.Balanced | Preset.Privacy);

        yield return new Tweak("visuals.hidden", "Show hidden files",
                "Shows hidden files and folders in File Explorer (protected system files stay hidden).",
                Visuals, Safe)
            .Hkcu(Adv, "Hidden", 1)
            .Needs(RestartKind.ExplorerRestart);

        yield return new Tweak("visuals.thispc", "Open File Explorer to This PC",
                "File Explorer opens on This PC instead of Quick Access/Home, and stops listing recent and frequent files there.",
                Visuals, Safe)
            .Hkcu(Adv, "LaunchTo", 1)
            .Hkcu(@"Software\Microsoft\Windows\CurrentVersion\Explorer", "ShowRecent", 0)
            .Hkcu(@"Software\Microsoft\Windows\CurrentVersion\Explorer", "ShowFrequent", 0)
            .Needs(RestartKind.ExplorerRestart)
            .In(Preset.Privacy);

        yield return new Tweak("visuals.darkmode", "Dark mode",
                "Dark theme for apps and for Windows itself (taskbar, Start).",
                Visuals, Safe)
            .Hkcu(Personalize, "AppsUseLightTheme", 0)
            .Hkcu(Personalize, "SystemUsesLightTheme", 0);

        yield return new Tweak("visuals.taskbarleft", "Left-align the taskbar",
                "Moves the Windows 11 Start button and taskbar icons to the left like Windows 10.",
                Visuals, Safe)
            .Hkcu(Adv, "TaskbarAl", 0)
            .When(s => s.IsWindows11, "Windows 11 only");

        yield return new Tweak("visuals.taskview", "Hide the Task View button",
                "Removes the Task View button from the taskbar. Win+Tab still works.",
                Visuals, Safe)
            .Hkcu(Adv, "ShowTaskViewButton", 0);
    }
}
