using Frtune.Core.Tweaks;

namespace Frtune.Core.Changes;

public enum ChangeKind
{
    Registry,
    Service,
    ScheduledTask,
    PowerSetting,
    Command,
}

/// <summary>
/// One atomic, reversible operation. A tweak is a list of changes.
/// The engine calls <see cref="Capture"/> on every change and saves the result to disk
/// BEFORE calling <see cref="Apply"/>, then hands the same string back to <see cref="Restore"/>.
/// That is what makes "toggle off" restore the real original value instead of a guess.
/// </summary>
public interface IChange
{
    /// <summary>Stable id, unique within the tweak, used as the backup key.</summary>
    string Key { get; }

    ChangeKind Kind { get; }

    /// <summary>Human-readable target, e.g. a registry path, service name or command.</summary>
    string Target { get; }

    /// <summary>One line describing what Apply does, shown in tooltips and confirmation dialogs.</summary>
    string Describe();

    /// <summary>Serialised current state. Null means "did not exist".</summary>
    string? Capture(TweakContext ctx);

    void Apply(TweakContext ctx);

    void Restore(TweakContext ctx, string? captured);

    bool IsApplied(TweakContext ctx);
}
