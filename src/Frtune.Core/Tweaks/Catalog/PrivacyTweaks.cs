using Frtune.Core.Abstractions;
using Frtune.Core.Changes;
using Frtune.Core.Model;
using static Frtune.Core.Model.TweakCategory;
using static Frtune.Core.Model.TweakRisk;

namespace Frtune.Core.Tweaks.Catalog;

internal static class PrivacyTweaks
{
    private const string DataCollection = @"SOFTWARE\Policies\Microsoft\Windows\DataCollection";
    private const string SystemPolicy = @"SOFTWARE\Policies\Microsoft\Windows\System";
    private const string SearchPolicy = @"SOFTWARE\Policies\Microsoft\Windows\Windows Search";

    /// <summary>Visible list of telemetry tasks. Missing ones are skipped.</summary>
    public static readonly string[] TelemetryTasks =
    {
        @"\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser",
        @"\Microsoft\Windows\Application Experience\ProgramDataUpdater",
        @"\Microsoft\Windows\Autochk\Proxy",
        @"\Microsoft\Windows\Customer Experience Improvement Program\Consolidator",
        @"\Microsoft\Windows\Customer Experience Improvement Program\UsbCeip",
        @"\Microsoft\Windows\DiskDiagnostic\Microsoft-Windows-DiskDiagnosticDataCollector",
        @"\Microsoft\Windows\Feedback\Siuf\DmClient",
        @"\Microsoft\Windows\Feedback\Siuf\DmClientOnScenarioDownload",
    };

    public static IEnumerable<Tweak> All()
    {
        yield return new Tweak("privacy.telemetry", "Minimum telemetry",
                "Disables the Connected User Experiences (DiagTrack) and WAP push (dmwappushservice) services and sets AllowTelemetry to the lowest level your edition honours: 0 (Security) on Enterprise/Education, 1 (Required) on Home/Pro.",
                Privacy, Safe)
            .Service("DiagTrack", ServiceStartType.Disabled)
            .Service("dmwappushservice", ServiceStartType.Disabled)
            .Add(new RegistryChange(RegHive.LocalMachine, DataCollection, "AllowTelemetry",
                ctx => RegValue.DWord(ctx.System.IsEnterpriseOrEducation ? 0u : 1u), "0 on Enterprise/Education, otherwise 1"))
            .In(Preset.Privacy | Preset.LowEnd);

        yield return new Tweak("privacy.adid", "Disable advertising ID",
                "Stops apps using your advertising ID for personalised ads.",
                Privacy, Safe)
            .Hkcu(@"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 0)
            .Hklm(@"SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo", "DisabledByGroupPolicy", 1)
            .In(Preset.Privacy | Preset.Balanced);

        yield return new Tweak("privacy.activity", "Disable activity history",
                "Stops Windows collecting and uploading the list of apps, files and sites you used (Timeline / \"Recommended\").",
                Privacy, Safe)
            .Hklm(SystemPolicy, "EnableActivityFeed", 0)
            .Hklm(SystemPolicy, "PublishUserActivities", 0)
            .Hklm(SystemPolicy, "UploadUserActivities", 0)
            .In(Preset.Privacy);

        yield return new Tweak("privacy.tailored", "Disable tailored experiences",
                "Stops Microsoft using diagnostic data to show you personalised tips, ads and recommendations.",
                Privacy, Safe)
            .Hkcu(@"Software\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled", 0)
            .In(Preset.Privacy | Preset.Balanced);

        yield return new Tweak("privacy.websearch", "Disable Cortana, Bing and web results in Start",
                "Start search shows only local results. Disables Cortana by policy.",
                Privacy, Safe)
            .Hklm(SearchPolicy, "AllowCortana", 0)
            .Hklm(SearchPolicy, "DisableWebSearch", 1)
            .Hklm(SearchPolicy, "ConnectedSearchUseWeb", 0)
            .Hkcu(@"Software\Microsoft\Windows\CurrentVersion\Search", "BingSearchEnabled", 0)
            .Hkcu(@"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", 1)
            .Needs(RestartKind.ExplorerRestart)
            .In(Preset.Privacy | Preset.LowEnd);

        yield return new Tweak("privacy.recall", "Disable Windows Recall",
                "Turns off Recall snapshots (AI analysis of your screen) by policy, for the current user and machine-wide.",
                Privacy, Safe)
            .Hkcu(@"Software\Policies\Microsoft\Windows\WindowsAI", "DisableAIDataAnalysis", 1)
            .Hklm(@"SOFTWARE\Policies\Microsoft\Windows\WindowsAI", "DisableAIDataAnalysis", 1)
            .When(s => s.Build >= 26100, "Windows 11 24H2+")
            .In(Preset.Privacy);

        yield return new Tweak("privacy.copilot", "Disable Copilot",
                "Turns off Windows Copilot by policy and hides its taskbar button.",
                Privacy, Safe)
            .Hkcu(@"Software\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1)
            .Hkcu(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowCopilotButton", 0)
            .When(s => s.IsWindows11, "Windows 11 only")
            .Needs(RestartKind.ExplorerRestart)
            .In(Preset.Privacy | Preset.LowEnd);

        yield return new Tweak("privacy.location", "Disable location services",
                "Turns off Windows location for all apps by policy. Weather, Maps and \"Find my device\" won't know where you are.",
                Privacy, Moderate)
            .Hklm(@"SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors", "DisableLocation", 1)
            .When(s => !s.IsLaptop, "Hidden on laptops (Find my device relies on it)")
            .In(Preset.Privacy);

        yield return new Tweak("privacy.feedback", "Disable feedback prompts and the Diagnostic Data Viewer",
                "Windows stops asking for feedback, and the Diagnostic Data Viewer is turned off.",
                Privacy, Safe)
            .Hkcu(@"Software\Microsoft\Siuf\Rules", "NumberOfSIUFInPeriod", 0)
            .Hklm(DataCollection, "DoNotShowFeedbackNotifications", 1)
            .Hklm(DataCollection, "DisableDiagnosticDataViewer", 1)
            .In(Preset.Privacy | Preset.Balanced);

        yield return new Tweak("privacy.applaunch", "Disable app launch tracking",
                "Stops Windows tracking which apps you launch to personalise Start and search.",
                Privacy, Safe)
            .Hkcu(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Start_TrackProgs", 0)
            .In(Preset.Privacy);

        yield return new Tweak("privacy.clipboardsync", "Disable clipboard sync across devices",
                "Clipboard history stays on this PC; it is no longer synced through your Microsoft account.",
                Privacy, Safe)
            .Hklm(SystemPolicy, "AllowCrossDeviceClipboard", 0)
            .In(Preset.Privacy);

        var tasks = new Tweak("privacy.tasks", "Disable telemetry scheduled tasks",
                "Disables these tasks: " + string.Join(", ", TelemetryTasks.Select(t => t[(t.LastIndexOf('\\') + 1)..])) + ". Tasks that don't exist on this PC are skipped.",
                Privacy, Safe)
            .In(Preset.Privacy);
        foreach (var t in TelemetryTasks) tasks.Task(t);
        yield return tasks;
    }
}
