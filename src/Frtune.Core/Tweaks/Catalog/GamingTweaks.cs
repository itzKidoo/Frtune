using Frtune.Core.Abstractions;
using Frtune.Core.Changes;
using Frtune.Core.Model;
using static Frtune.Core.Model.TweakCategory;
using static Frtune.Core.Model.TweakRisk;

namespace Frtune.Core.Tweaks.Catalog;

internal static class GamingTweaks
{
    private const string GameConfig = @"System\GameConfigStore";
    private const string Mm = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
    private const string MmGames = Mm + @"\Tasks\Games";
    private const string GpuPrefs = @"Software\Microsoft\DirectX\UserGpuPreferences";

    public static IEnumerable<Tweak> All()
    {
        yield return new Tweak("gaming.gamemode", "Enable Game Mode",
                "Lets Windows prioritise the game you're playing and hold back Windows Update installs and notifications while it runs.",
                Gaming, Safe)
            .Hkcu(@"Software\Microsoft\GameBar", "AutoGameModeEnabled", 1)
            .Hkcu(@"Software\Microsoft\GameBar", "AllowAutoGameMode", 1)
            .In(Preset.Gaming | Preset.Balanced | Preset.MaxPerformance);

        yield return new Tweak("gaming.hags", "Hardware-accelerated GPU scheduling",
                "Moves GPU work scheduling from the CPU to a scheduler on the GPU. Needs a GPU and driver that support it (NVIDIA GTX 10-series or newer, AMD RX 5000 or newer).",
                Gaming, Moderate)
            .Hklm(@"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 2)
            .When(s => s.Build >= 19041 && s.HasDiscreteGpu, "Windows 10 2004+ with an NVIDIA or AMD GPU")
            .Recommend(s => s.HasGpu(GpuVendor.Nvidia) && s.CpuThreads <= 8
                ? $"Your {s.PrimaryGpu?.Name} supports it, and on a {s.CpuThreads}-thread CPU it takes some scheduling work off the CPU."
                : null)
            .Needs(RestartKind.Reboot)
            .In(Preset.Gaming | Preset.MaxPerformance);

        yield return new Tweak("gaming.gamedvr", "Disable Xbox Game DVR / Game Bar capture",
                "Stops background recording of gameplay, which costs GPU encoder time and disk writes. Game Bar itself still opens with Win+G.",
                Gaming, Safe)
            .Hkcu(GameConfig, "GameDVR_Enabled", 0)
            .Hkcu(@"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0)
            .Hklm(@"SOFTWARE\Policies\Microsoft\Windows\GameDVR", "AllowGameDVR", 0)
            .In(Preset.Gaming | Preset.LowEnd | Preset.MaxPerformance);

        yield return new Tweak("gaming.fso", "Disable fullscreen optimizations (global)",
                "Makes exclusive-fullscreen games run in true exclusive fullscreen instead of Windows' optimised borderless mode. Can help older DX9/DX11 games; can break Alt-Tab overlays in some titles.",
                Gaming, Moderate)
            .Hkcu(GameConfig, "GameDVR_FSEBehaviorMode", 2)
            .Hkcu(GameConfig, "GameDVR_FSEBehavior", 2)
            .Hkcu(GameConfig, "GameDVR_HonorUserFSEBehaviorMode", 1)
            .Hkcu(GameConfig, "GameDVR_DXGIHonorFSEWindowsCompatible", 1)
            .In(Preset.MaxPerformance);

        yield return new Tweak("gaming.mmcss", "Multimedia scheduler: prioritise games",
                "Tunes MMCSS: reserves only 10% CPU for background tasks (default 20%) and raises the CPU and GPU priority of the \"Games\" task class.",
                Gaming, Safe)
            .Hklm(Mm, "SystemResponsiveness", 10)
            .Hklm(MmGames, "GPU Priority", 8)
            .Hklm(MmGames, "Priority", 6)
            .Hklm(MmGames, "Scheduling Category", "High")
            .Hklm(MmGames, "SFIO Priority", "High")
            .In(Preset.Gaming | Preset.MaxPerformance);

        yield return new Tweak("gaming.priosep", "Foreground boost (Win32PrioritySeparation)",
                "Sets Win32PrioritySeparation to 0x26: short, variable quanta with a 3x boost for the foreground app. This favours the game window over background work.",
                Gaming, Moderate)
            .Hklm(@"SYSTEM\CurrentControlSet\Control\PriorityControl", "Win32PrioritySeparation", 0x26)
            .Recommend(s => s.CpuThreads <= 4 ? "With only 4 threads, giving the foreground game a bigger time slice helps 1% lows." : null)
            .In(Preset.Gaming | Preset.MaxPerformance);

        yield return new Tweak("gaming.windowed", "Optimizations for windowed games",
                "Windows 11 setting that upgrades DX10/11 games running in a window or borderless to the flip presentation model: lower latency and Auto HDR/VRR support.",
                Gaming, Safe)
            .Add(new RegistryListChange(RegHive.CurrentUser, GpuPrefs, "DirectXUserGlobalSettings", "SwapEffectUpgradeEnable", "1"))
            .When(s => s.IsWindows11, "Windows 11 only")
            .In(Preset.Gaming | Preset.Balanced | Preset.MaxPerformance);

        yield return new Tweak("gaming.vrr", "Disable the Windows VRR override",
                "Turns off \"Variable refresh rate\" in Graphics settings, which forces VRR on for DX11 games that don't support it themselves. Only turn this off if a game stutters or flickers with VRR.",
                Gaming, Moderate)
            .Add(new RegistryListChange(RegHive.CurrentUser, GpuPrefs, "DirectXUserGlobalSettings", "VRROptimizeEnable", "0"))
            .When(s => s.Build >= 18362, "Windows 10 1903+");

        yield return new Tweak("gaming.timerres", "Global timer resolution requests",
                "Since Windows 10 2004, one app raising the timer resolution no longer affects others. This restores the old global behaviour so a game's 0.5–1 ms timer request applies system-wide. Higher idle power use.",
                Gaming, Moderate)
            .Hklm(@"SYSTEM\CurrentControlSet\Control\Session Manager\kernel", "GlobalTimerResolutionRequests", 1)
            .When(s => s.Build >= 19041, "Windows 10 2004+")
            .Needs(RestartKind.Reboot)
            .In(Preset.MaxPerformance);
    }
}
