using Frtune.Core.Changes;
using Frtune.Core.Model;
using static Frtune.Core.Model.TweakCategory;
using static Frtune.Core.Model.TweakRisk;

namespace Frtune.Core.Tweaks.Catalog;

/// <summary>All of these change the ACTIVE power plan only and are recorded with that plan's GUID.</summary>
internal static class PowerTweaks
{
    public static IEnumerable<Tweak> All()
    {
        yield return new Tweak("power.usbsuspend", "Disable USB selective suspend",
                "USB devices (mouse, keyboard, headset, Wi-Fi dongle) are never put to sleep to save power. Avoids wake-up lag and dropouts.",
                Power, Safe)
            .Power(PowerGuids.SubUsb, PowerGuids.UsbSelectiveSuspend, "USB selective suspend", 0)
            .In(Preset.Gaming | Preset.MaxPerformance);

        yield return new Tweak("power.aspm", "Disable PCIe link state power management",
                "Keeps the PCIe links to your GPU and NVMe drive at full power. Slightly higher idle draw.",
                Power, Safe)
            .Power(PowerGuids.SubPciExpress, PowerGuids.PcieAspm, "PCI Express link state power management", 0)
            .When(s => !s.IsLaptop, "Desktops only")
            .In(Preset.Gaming | Preset.MaxPerformance);

        yield return new Tweak("power.disksleep", "Never turn off hard disks",
                "Disk idle timeout = 0 (never). Prevents spin-up stutter on HDDs; harmless on SSDs.",
                Power, Safe)
            .Power(PowerGuids.SubDisk, PowerGuids.DiskIdle, "Turn off hard disk after (seconds)", 0)
            .When(s => !s.IsLaptop, "Desktops only")
            .In(Preset.Gaming | Preset.MaxPerformance);

        yield return new Tweak("power.minstate", "Minimum processor state 100%",
                "The CPU never drops below base clock while plugged in. Removes the ramp-up delay, but idles warmer and uses more power.",
                Power, Moderate)
            .Power(PowerGuids.SubProcessor, PowerGuids.ProcThrottleMin, "Minimum processor state (%)", 100)
            .When(s => !s.IsLaptop, "Desktops only")
            .In(Preset.MaxPerformance);

        yield return new Tweak("power.maxstate", "Maximum processor state 100%",
                "Makes sure the CPU is allowed to reach full speed (some OEM plans cap it at 99%, which also disables turbo).",
                Power, Safe)
            .Power(PowerGuids.SubProcessor, PowerGuids.ProcThrottleMax, "Maximum processor state (%)", 100)
            .In(Preset.Gaming | Preset.Balanced | Preset.MaxPerformance);

        yield return new Tweak("power.boost", "Aggressive turbo boost",
                "Processor performance boost mode = Aggressive (2): the CPU turbos whenever it can.",
                Power, Safe)
            .Power(PowerGuids.SubProcessor, PowerGuids.PerfBoostMode, "Processor performance boost mode", 2)
            .Recommend(s => s.CpuName.Contains("i3-9100", StringComparison.OrdinalIgnoreCase) ? "Lets your i3-9100F hold its 4.2 GHz turbo more often." : null)
            .In(Preset.Gaming | Preset.MaxPerformance);

        yield return new Tweak("power.epp", "Energy performance preference: max performance",
                "EPP = 0 tells the CPU's own power manager to favour performance over efficiency. Has an effect on CPUs with Speed Shift (Intel 6th gen+, Ryzen with CPPC).",
                Power, Moderate)
            .Power(PowerGuids.SubProcessor, PowerGuids.PerfEpp, "Processor energy performance preference (%)", 0)
            .In(Preset.MaxPerformance);
    }
}
