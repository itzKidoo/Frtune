using Frtune.Core.Abstractions;
using Frtune.Core.Changes;
using Frtune.Core.Model;
using static Frtune.Core.Model.TweakCategory;
using static Frtune.Core.Model.TweakRisk;

namespace Frtune.Core.Tweaks.Catalog;

internal static class PerformanceTweaks
{
    private const string Cdm = @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";
    private const string FileSystem = @"SYSTEM\CurrentControlSet\Control\FileSystem";

    public static IEnumerable<Tweak> All()
    {
        yield return new Tweak("perf.ultimateplan", "Ultimate Performance power plan",
                "Activates the hidden Ultimate Performance plan (creates it from Windows' template if needed). It removes micro power-saving delays. Desktops only: on a laptop it drains the battery.",
                Performance, Safe)
            .Add(UltimatePlan())
            .When(s => !s.IsLaptop, "Desktops only")
            .Recommend(s => "You're on a desktop, so there's no battery to save.")
            .In(Preset.Gaming | Preset.MaxPerformance);

        yield return new Tweak("perf.coreparking", "Disable CPU core parking",
                "Keeps all cores unparked in the active power plan (CPMINCORES = 100%), so the CPU doesn't wake parked cores at the start of a load spike.",
                Performance, Safe)
            .Power(PowerGuids.SubProcessor, PowerGuids.CoreParkingMinCores, "Processor performance core parking min cores", 100)
            .Recommend(s => !s.IsLaptop && s.CpuCores <= 4 ? $"Your CPU has only {s.CpuCores} cores; parking any of them hurts frame-time consistency." : null)
            .In(Preset.Gaming | Preset.MaxPerformance);

        yield return new Tweak("perf.powerthrottling", "Disable power throttling",
                "Stops Windows from running background apps on efficient (slower) CPU states to save power.",
                Performance, Safe)
            .Hklm(@"SYSTEM\CurrentControlSet\Control\Power\PowerThrottling", "PowerThrottlingOff", 1)
            .When(s => s.Build >= 16299, "Windows 10 1709+")
            .In(Preset.Gaming | Preset.MaxPerformance);

        yield return new Tweak("perf.sysmain", "Disable SysMain (Superfetch)",
                "Stops SysMain from pre-loading apps into RAM. On an SSD the benefit is small and it costs disk and CPU time in the background.",
                Performance, Moderate)
            .Service("SysMain", ServiceStartType.Disabled)
            .When(s => s.SystemDriveIsSsd, "SSD system drives only")
            .In(Preset.LowEnd | Preset.MaxPerformance);

        yield return new Tweak("perf.search", "Disable Windows Search indexing",
                "Stops the indexer service. Start-menu and Explorer search still work, but slower and without file contents.",
                Performance, Moderate)
            .Service("WSearch", ServiceStartType.Disabled)
            .In(Preset.LowEnd);

        yield return new Tweak("perf.faststartup", "Disable Fast Startup",
                "Makes Shut down a real shutdown instead of a hibernated kernel. Fixes driver and uptime oddities; cold boots take a little longer.",
                Performance, Safe)
            .Hklm(@"SYSTEM\CurrentControlSet\Control\Session Manager\Power", "HiberbootEnabled", 0)
            .In(Preset.Gaming | Preset.MaxPerformance);

        yield return new Tweak("perf.hibernate", "Disable hibernation",
                "Runs powercfg /hibernate off. Deletes hiberfil.sys (several GB on C:). Also turns off Fast Startup.",
                Performance, Moderate)
            .Add(new CommandChange("hibernate", "powercfg /hibernate",
                "powercfg /hibernate off  (HKLM\\SYSTEM\\CurrentControlSet\\Control\\Power\\HibernateEnabled → 0)",
                ctx => ctx.Registry.GetValue(RegHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Power", "HibernateEnabled")?.ToToken(),
                ctx => ctx.Commands.Run("powercfg.exe", "/hibernate off").EnsureSuccess("powercfg /hibernate off"),
                (ctx, c) => { if (c == "dword:1") ctx.Commands.Run("powercfg.exe", "/hibernate on").EnsureSuccess("powercfg /hibernate on"); },
                ctx => ctx.Registry.GetValue(RegHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Power", "HibernateEnabled")?.ToToken() == "dword:0"))
            .When(s => !s.IsLaptop, "Desktops only (laptops rely on hibernate when the battery runs low)")
            .In(Preset.LowEnd);

        yield return new Tweak("perf.memcompression", "Disable memory compression",
                "Turns off the compressed memory store (Disable-MMAgent -MemoryCompression). Saves a little CPU but pages to disk sooner. Only worth it with plenty of RAM.",
                Performance, TweakRisk.Advanced)
            .Add(Helpers.PsBool("mmagent", "MMAgent memory compression",
                "Disable-MMAgent -MemoryCompression", "(Get-MMAgent).MemoryCompression", false,
                "Enable-MMAgent -MemoryCompression", "Disable-MMAgent -MemoryCompression"))
            .Recommend(s => null)
            .Needs(RestartKind.Reboot);

        yield return new Tweak("perf.ntfslastaccess", "NTFS: stop updating last-access times",
                "Stops NTFS writing a timestamp every time a file is read. Fewer disk writes.",
                Performance, Safe)
            .Hklm(FileSystem, "NtfsDisableLastAccessUpdate", 0x80000001)
            .Needs(RestartKind.Reboot)
            .In(Preset.LowEnd | Preset.MaxPerformance | Preset.Balanced);

        yield return new Tweak("perf.ntfs83", "NTFS: stop creating 8.3 short names",
                "Stops creating legacy DOS short names (PROGRA~1) for new files on all volumes. Very old 16-bit installers may need them.",
                Performance, Moderate)
            .Hklm(FileSystem, "NtfsDisable8dot3NameCreation", 1)
            .Needs(RestartKind.Reboot)
            .In(Preset.MaxPerformance);

        yield return new Tweak("perf.trim", "Make sure TRIM and scheduled optimisation are on",
                "Turns on TRIM notifications and the weekly ScheduledDefrag task (which runs TRIM on SSDs, not defrag). These are Windows defaults; this repairs them if something turned them off.",
                Performance, Safe)
            .Hklm(FileSystem, "DisableDeleteNotification", 0)
            .Task(@"\Microsoft\Windows\Defrag\ScheduledDefrag", enable: true)
            .When(s => s.SystemDriveIsSsd, "SSD system drives only")
            .In(Preset.Balanced | Preset.Gaming | Preset.LowEnd | Preset.MaxPerformance);

        yield return new Tweak("perf.svchostsplit", "Group service hosts by RAM size",
                "Sets SvcHostSplitThresholdInKB to your RAM size. Windows then groups services into fewer svchost.exe processes: less overhead, but one crash can take several services down.",
                Performance, Moderate)
            .Add(new RegistryChange(RegHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control", "SvcHostSplitThresholdInKB",
                ctx => RegValue.DWord((uint)Math.Min(ctx.System.TotalRamKb, uint.MaxValue)), "your RAM size in KB"))
            .Needs(RestartKind.Reboot)
            .In(Preset.LowEnd);

        yield return new Tweak("perf.backgroundapps", "Disable background apps",
                "Stops Store apps running in the background (mail sync, live tiles, etc.). Desktop programs aren't affected.",
                Performance, Safe)
            .Hkcu(@"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled", 1)
            .Hklm(@"SOFTWARE\Policies\Microsoft\Windows\AppPrivacy", "LetAppsRunInBackground", 2)
            .In(Preset.LowEnd | Preset.Gaming | Preset.MaxPerformance | Preset.Privacy);

        yield return new Tweak("perf.tips", "Disable tips, suggestions and Spotlight ads",
                "Turns off Start suggestions, \"tips and tricks\" notifications, Settings suggestions and lock-screen Spotlight ads.",
                Performance, Safe)
            .Hkcu(Cdm, "SubscribedContent-338388Enabled", 0)
            .Hkcu(Cdm, "SubscribedContent-338389Enabled", 0)
            .Hkcu(Cdm, "SubscribedContent-338393Enabled", 0)
            .Hkcu(Cdm, "SubscribedContent-353694Enabled", 0)
            .Hkcu(Cdm, "SubscribedContent-353696Enabled", 0)
            .Hkcu(Cdm, "SystemPaneSuggestionsEnabled", 0)
            .Hkcu(Cdm, "SoftLandingEnabled", 0)
            .Hkcu(Cdm, "RotatingLockScreenOverlayEnabled", 0)
            .In(Preset.Balanced | Preset.Gaming | Preset.LowEnd | Preset.Privacy | Preset.MaxPerformance);
    }

    /// <summary>Activates Ultimate Performance; revert re-activates whatever plan was active before.</summary>
    private static IChange UltimatePlan() => new CommandChange(
        "ultimateplan", "powercfg active scheme",
        $"powercfg /setactive {PowerGuids.UltimatePerformanceTemplate} (or /duplicatescheme it first if it's hidden)",
        ctx => PowerSettingChange.ActiveScheme(ctx),
        ctx =>
        {
            if (ctx.Commands.Run("powercfg.exe", $"/setactive {PowerGuids.UltimatePerformanceTemplate}").Success) return;
            var dup = ctx.Commands.Run("powercfg.exe", $"/duplicatescheme {PowerGuids.UltimatePerformanceTemplate}").EnsureSuccess("powercfg /duplicatescheme");
            var guid = System.Text.RegularExpressions.Regex.Match(dup.StdOut, "[0-9a-fA-F-]{36}").Value;
            // Plan names are translated on non-English Windows, so tag ours to recognise it later.
            ctx.Commands.Run("powercfg.exe", $"/changename {guid} \"Ultimate Performance (frtune)\"");
            ctx.Commands.Run("powercfg.exe", $"/setactive {guid}").EnsureSuccess("powercfg /setactive");
        },
        (ctx, captured) => { if (captured is not null) ctx.Commands.Run("powercfg.exe", $"/setactive {captured}").EnsureSuccess("powercfg /setactive"); },
        ctx =>
        {
            var r = ctx.Commands.Run("powercfg.exe", "/getactivescheme");
            return r.StdOut.Contains(PowerGuids.UltimatePerformanceTemplate, StringComparison.OrdinalIgnoreCase)
                || r.StdOut.Contains("(frtune)", StringComparison.OrdinalIgnoreCase)
                || r.StdOut.Contains("Ultimate Performance", StringComparison.OrdinalIgnoreCase);
        });
}
