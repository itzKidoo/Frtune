using Frtune.Core.Changes;
using Frtune.Core.Model;

namespace Frtune.Core.Tweaks;

/// <summary>
/// One user-facing toggle. Implementations must be reversible: <see cref="Apply"/> returns a
/// snapshot of the original state and <see cref="Revert"/> puts exactly that state back.
/// </summary>
public interface ITweak
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    TweakCategory Category { get; }
    TweakRisk Risk { get; }

    /// <summary>Extra warning shown in a confirmation dialog before applying. Null for none.</summary>
    string? Warning { get; }

    RestartKind Restart { get; }
    Preset Presets { get; }

    bool IsApplicable(SystemProfile system);

    /// <summary>A reason this tweak suits this PC, or null if it isn't specifically recommended.</summary>
    string? RecommendationFor(SystemProfile system);

    /// <summary>The concrete changes on this machine (some depend on adapters, RAM size, etc.).</summary>
    IReadOnlyList<IChange> GetChanges(TweakContext ctx);

    bool IsApplied(TweakContext ctx);

    /// <summary>Captures, then applies. Convenience for callers that don't persist the snapshot first.</summary>
    TweakSnapshot Apply(TweakContext ctx);

    /// <summary>Reads the current state of everything this tweak would change.</summary>
    TweakSnapshot Capture(TweakContext ctx);

    /// <summary>Applies using a snapshot the caller already saved (used by the engine: save first, then change).</summary>
    void ApplyCaptured(TweakContext ctx, TweakSnapshot snapshot);

    void Revert(TweakContext ctx, TweakSnapshot snapshot);
}

/// <summary>Original state captured before a tweak was applied. Persisted in backups/snapshots.json.</summary>
public sealed record TweakSnapshot
{
    public required string TweakId { get; init; }
    public required DateTimeOffset CapturedAt { get; init; }
    /// <summary>Change key → captured state (null = did not exist).</summary>
    public required Dictionary<string, string?> Values { get; init; }
    /// <summary>Human-readable targets, kept so the backup is understandable on its own.</summary>
    public List<string> Targets { get; init; } = new();
}
