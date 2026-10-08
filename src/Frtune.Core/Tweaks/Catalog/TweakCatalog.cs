namespace Frtune.Core.Tweaks.Catalog;

/// <summary>Every built-in tweak. To add a tweak, add it to one of the category files (see CONTRIBUTING.md).</summary>
public static class TweakCatalog
{
    public static IReadOnlyList<Tweak> All { get; } = Build();

    private static IReadOnlyList<Tweak> Build() =>
        GamingTweaks.All()
            .Concat(PerformanceTweaks.All())
            .Concat(NetworkTweaks.All())
            .Concat(PrivacyTweaks.All())
            .Concat(DebloatTweaks.All())
            .Concat(ServiceTweaks.All())
            .Concat(InputTweaks.All())
            .Concat(PowerTweaks.All())
            .Concat(VisualTweaks.All())
            .Concat(AdvancedTweaks.All())
            .ToList();

    public static IReadOnlyList<string> TelemetryTasks => PrivacyTweaks.TelemetryTasks;
}
