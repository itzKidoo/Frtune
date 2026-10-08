using Frtune.Core.Abstractions;
using Frtune.Core.Model;

namespace Frtune.Core.Tweaks;

/// <summary>User-adjustable values some tweaks read when they apply.</summary>
public sealed record TweakSettings
{
    /// <summary>Days to pause Windows Update for (Windows allows at most 35).</summary>
    public int UpdatePauseDays { get; init; } = 7;
}

/// <summary>Everything a tweak may touch. Passed in, never global, so tests can swap in fakes.</summary>
public sealed record TweakContext(
    IRegistry Registry,
    IServiceManager Services,
    ICommandRunner Commands,
    SystemProfile System,
    IClock Clock,
    TweakSettings Settings)
{
    public TweakContext(IRegistry registry, IServiceManager services, ICommandRunner commands, SystemProfile system)
        : this(registry, services, commands, system, SystemClock.Instance, new TweakSettings()) { }
}
