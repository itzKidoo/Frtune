using Frtune.Core.Tweaks;

namespace Frtune.Core.Changes;

/// <summary>
/// A change implemented by commands (netsh, powercfg, PowerShell…). The tweak author supplies
/// how to read the current state, how to apply, and how to put a captured state back.
/// </summary>
public sealed class CommandChange : IChange
{
    private readonly Func<TweakContext, string?> _capture;
    private readonly Action<TweakContext> _apply;
    private readonly Action<TweakContext, string?> _restore;
    private readonly Func<TweakContext, bool> _isApplied;
    private readonly string _description;

    public CommandChange(
        string key,
        string target,
        string description,
        Func<TweakContext, string?> capture,
        Action<TweakContext> apply,
        Action<TweakContext, string?> restore,
        Func<TweakContext, bool> isApplied)
    {
        Key = "cmd:" + key;
        Target = target;
        _description = description;
        _capture = capture;
        _apply = apply;
        _restore = restore;
        _isApplied = isApplied;
    }

    public string Key { get; }
    public ChangeKind Kind => ChangeKind.Command;
    public string Target { get; }
    public string Describe() => _description;
    public string? Capture(TweakContext ctx) => _capture(ctx);
    public void Apply(TweakContext ctx) => _apply(ctx);
    public void Restore(TweakContext ctx, string? captured) => _restore(ctx, captured);
    public bool IsApplied(TweakContext ctx) => _isApplied(ctx);
}
