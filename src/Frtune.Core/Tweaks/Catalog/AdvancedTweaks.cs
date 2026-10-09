using Frtune.Core.Abstractions;
using Frtune.Core.Changes;
using Frtune.Core.Model;
using static Frtune.Core.Model.TweakCategory;
using static Frtune.Core.Model.TweakRisk;

namespace Frtune.Core.Tweaks.Catalog;

/// <summary>
/// Security-relevant switches. All are Advanced risk, so they are never part of a preset,
/// are off by default, and always show a warning in a confirmation dialog.
/// </summary>
internal static class AdvancedTweaks
{
    private const string WuUx = @"SOFTWARE\Microsoft\WindowsUpdate\UX\Settings";

    public static IEnumerable<Tweak> All()
    {
        yield return new Tweak("adv.spectre", "Disable Spectre/Meltdown mitigations",
                "Sets FeatureSettingsOverride/Mask = 3, which turns off the kernel's CPU side-channel mitigations. Gains are a few percent at best on recent CPUs.",
                TweakCategory.Advanced, TweakRisk.Advanced)
            .Hklm(@"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", "FeatureSettingsOverride", 3)
            .Hklm(@"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", "FeatureSettingsOverrideMask", 3)
            .Warn("This removes protection against CPU side-channel attacks. Malicious code, including JavaScript in a browser, could read memory belonging to other programs, such as passwords. Microsoft does not recommend this on any PC that browses the web.")
            .Needs(RestartKind.Reboot);

        yield return new Tweak("adv.pauseupdates", "Pause Windows Update",
                "Pauses quality and feature updates for the number of days set in Settings (default 7, maximum 35). This uses Windows' own pause feature, so updates resume automatically afterwards. frtune never fully disables Windows Update.",
                TweakCategory.Advanced, TweakRisk.Advanced)
            .Add(new RegistryChange(RegHive.LocalMachine, WuUx, "PauseUpdatesExpiryTime", ctx => RegValue.Str(Until(ctx)), "today + N days (ISO 8601)"))
            .Add(new RegistryChange(RegHive.LocalMachine, WuUx, "PauseFeatureUpdatesStartTime", ctx => RegValue.Str(Now(ctx)), "now (ISO 8601)"))
            .Add(new RegistryChange(RegHive.LocalMachine, WuUx, "PauseFeatureUpdatesEndTime", ctx => RegValue.Str(Until(ctx)), "today + N days"))
            .Add(new RegistryChange(RegHive.LocalMachine, WuUx, "PauseQualityUpdatesStartTime", ctx => RegValue.Str(Now(ctx)), "now"))
            .Add(new RegistryChange(RegHive.LocalMachine, WuUx, "PauseQualityUpdatesEndTime", ctx => RegValue.Str(Until(ctx)), "today + N days"))
            .Add(new RegistryChange(RegHive.LocalMachine, WuUx, "PauseUpdatesStartTime", ctx => RegValue.Str(Now(ctx)), "now"))
            .Warn("While paused you won't get security fixes. Keep the pause short.");

        yield return new Tweak("adv.vbs", "Disable Memory Integrity (HVCI) and VBS",
                "Turns off Virtualization-Based Security and Hypervisor-enforced Code Integrity. Can recover around 5% in some CPU-bound games.",
                TweakCategory.Advanced, TweakRisk.Advanced)
            .Hklm(@"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity", "Enabled", 0)
            .Hklm(@"SYSTEM\CurrentControlSet\Control\DeviceGuard", "EnableVirtualizationBasedSecurity", 0)
            .Warn("Memory Integrity stops malicious or vulnerable drivers from tampering with the Windows kernel. Some anti-cheat systems and work/school policies expect it to be on. Without it, kernel-level malware is easier to install.")
            .Needs(RestartKind.Reboot);

        yield return new Tweak("adv.defender", "Pause Defender real-time protection (temporary)",
                "Runs Set-MpPreference -DisableRealtimeMonitoring $true. Defender turns real-time protection back on by itself after a short time or a restart. Tamper Protection must be off in Windows Security, or Windows will refuse.",
                TweakCategory.Advanced, TweakRisk.Advanced)
            .Add(Helpers.PsBool("defender.rtp", "Defender real-time protection",
                "Set-MpPreference -DisableRealtimeMonitoring $true", "(Get-MpPreference).DisableRealtimeMonitoring", true,
                "Set-MpPreference -DisableRealtimeMonitoring $true", "Set-MpPreference -DisableRealtimeMonitoring $false"))
            .Warn("Your PC has no real-time malware protection until Defender re-enables itself. Don't download or run anything while it's paused.");
    }

    private static string Now(TweakContext ctx) => ctx.Clock.Now.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ");

    private static string Until(TweakContext ctx) =>
        ctx.Clock.Now.UtcDateTime.AddDays(Math.Clamp(ctx.Settings.UpdatePauseDays, 1, 35)).ToString("yyyy-MM-ddTHH:mm:ssZ");
}
