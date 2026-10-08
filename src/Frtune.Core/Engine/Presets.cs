using Frtune.Core.Model;
using Frtune.Core.Tweaks;

namespace Frtune.Core.Engine;

public sealed record PresetInfo(Preset Preset, string Name, string Description);

public static class Presets
{
    public static readonly IReadOnlyList<PresetInfo> All = new[]
    {
        new PresetInfo(Preset.Gaming, "Gaming", "Lower input and network latency, GPU scheduling and MMCSS game priority. Keeps privacy defaults."),
        new PresetInfo(Preset.Balanced, "Balanced", "Safe tweaks only: Game Mode, fewer tips and ads, sensible visuals. Good for any PC."),
        new PresetInfo(Preset.LowEnd, "Low-End PC", "Trims background work: fewer services, no indexing, lighter visuals and fewer svchost processes."),
        new PresetInfo(Preset.Privacy, "Privacy", "Minimum telemetry, no advertising ID, no activity history, web search or Copilot."),
        new PresetInfo(Preset.MaxPerformance, "Max Performance", "Everything in Gaming plus aggressive power settings. Uses more power and runs warmer."),
    };

    /// <summary>
    /// Tweaks a preset turns on for this PC. Advanced-risk tweaks are never included in a preset,
    /// whatever their flags say, and neither are tweaks that don't apply to this PC.
    /// </summary>
    public static IReadOnlyList<ITweak> Select(IEnumerable<ITweak> catalog, Preset preset, SystemProfile system) =>
        catalog.Where(t => t.Presets.HasFlag(preset) && t.Risk != TweakRisk.Advanced && t.IsApplicable(system)).ToList();
}
