namespace Frtune.Core.Tools;

public sealed record HealthFactor(string Name, int Score, int Max, string Detail);

public sealed record HealthReport(int Score, IReadOnlyList<HealthFactor> Factors)
{
    public string Grade => Score switch { >= 85 => "Excellent", >= 70 => "Good", >= 50 => "Fair", _ => "Needs attention" };
}

/// <summary>
/// A transparent 0–100 score built from a few things this app can actually improve.
/// Every factor is shown on the dashboard, so the number is never a black box.
/// </summary>
public static class HealthScore
{
    public static HealthReport Compute(
        double systemDriveFreePercent,
        double ramUsedPercent,
        int startupItemsEnabled,
        int recommendedTweaks,
        int recommendedApplied,
        long junkBytes,
        bool restartPending)
    {
        var f = new List<HealthFactor>();

        var disk = systemDriveFreePercent switch { >= 25 => 25, >= 15 => 18, >= 10 => 10, _ => 3 };
        f.Add(new("Free space on system drive", disk, 25, $"{systemDriveFreePercent:0}% free"));

        var ram = ramUsedPercent switch { <= 60 => 15, <= 75 => 11, <= 90 => 6, _ => 2 };
        f.Add(new("Memory pressure", ram, 15, $"{ramUsedPercent:0}% in use"));

        var startup = startupItemsEnabled switch { <= 5 => 15, <= 10 => 11, <= 15 => 7, _ => 3 };
        f.Add(new("Startup apps", startup, 15, $"{startupItemsEnabled} enabled"));

        var tweaks = recommendedTweaks == 0 ? 30 : (int)Math.Round(30.0 * recommendedApplied / recommendedTweaks);
        f.Add(new("Recommended tweaks applied", tweaks, 30, $"{recommendedApplied} of {recommendedTweaks}"));

        var gb = junkBytes / 1073741824.0;
        var junk = gb switch { < 1 => 10, < 3 => 7, < 10 => 4, _ => 1 };
        f.Add(new("Junk files", junk, 10, junkBytes < 0 ? "not scanned" : $"{CleanerCatalog.FormatBytes(junkBytes)} cleanable"));

        f.Add(new("Pending restart", restartPending ? 0 : 5, 5, restartPending ? "restart needed" : "none"));

        return new HealthReport(f.Sum(x => x.Score), f);
    }
}
