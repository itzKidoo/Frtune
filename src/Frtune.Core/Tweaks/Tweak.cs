using Frtune.Core.Abstractions;
using Frtune.Core.Changes;
using Frtune.Core.Model;

namespace Frtune.Core.Tweaks;

/// <summary>
/// The standard tweak: a named list of <see cref="IChange"/>s. Almost every tweak in the catalog
/// is one of these, built with the fluent helpers below.
/// </summary>
public sealed class Tweak : ITweak
{
    private readonly List<IChange> _staticChanges = new();
    private readonly List<Func<TweakContext, IEnumerable<IChange>>> _dynamicChanges = new();
    private Func<SystemProfile, bool> _applicable = _ => true;
    private Func<SystemProfile, string?> _recommend = _ => null;

    public Tweak(string id, string name, string description, TweakCategory category, TweakRisk risk)
    {
        Id = id;
        Name = name;
        Description = description;
        Category = category;
        Risk = risk;
    }

    public string Id { get; }
    public string Name { get; }
    public string Description { get; }
    public TweakCategory Category { get; }
    public TweakRisk Risk { get; }
    public string? Warning { get; private set; }
    public RestartKind Restart { get; private set; }
    public Preset Presets { get; private set; }
    /// <summary>Shown when the tweak is hidden on this PC, e.g. "Windows 11 only".</summary>
    public string? ApplicabilityNote { get; private set; }

    // ---- fluent builder -------------------------------------------------------------------

    public Tweak Reg(RegHive hive, string key, string name, RegValue? value)
    {
        _staticChanges.Add(new RegistryChange(hive, key, name, value));
        return this;
    }

    public Tweak Hklm(string key, string name, uint dword) => Reg(RegHive.LocalMachine, key, name, RegValue.DWord(dword));
    public Tweak Hkcu(string key, string name, uint dword) => Reg(RegHive.CurrentUser, key, name, RegValue.DWord(dword));
    public Tweak Hkcu(string key, string name, string sz) => Reg(RegHive.CurrentUser, key, name, RegValue.Str(sz));
    public Tweak Hklm(string key, string name, string sz) => Reg(RegHive.LocalMachine, key, name, RegValue.Str(sz));

    public Tweak Service(string name, ServiceStartType type) => Add(new ServiceChange(name, type));
    public Tweak Task(string path, bool enable = false) => Add(new ScheduledTaskChange(path, enable));
    public Tweak Power(string sub, string setting, string label, uint ac, uint? dc = null) => Add(new PowerSettingChange(sub, setting, label, ac, dc));

    public Tweak Add(IChange change)
    {
        _staticChanges.Add(change);
        return this;
    }

    /// <summary>Changes that are only known at runtime (per network adapter, computed from RAM…).</summary>
    public Tweak AddDynamic(Func<TweakContext, IEnumerable<IChange>> factory)
    {
        _dynamicChanges.Add(factory);
        return this;
    }

    public Tweak In(Preset presets) { Presets = presets; return this; }
    public Tweak Needs(RestartKind restart) { Restart = restart; return this; }
    public Tweak Warn(string warning) { Warning = warning; return this; }

    public Tweak When(Func<SystemProfile, bool> applicable, string note)
    {
        var previous = _applicable;
        _applicable = s => previous(s) && applicable(s);
        ApplicabilityNote = ApplicabilityNote is null ? note : ApplicabilityNote + "; " + note;
        return this;
    }

    public Tweak Recommend(Func<SystemProfile, string?> reason) { _recommend = reason; return this; }

    // ---- ITweak ---------------------------------------------------------------------------

    public bool IsApplicable(SystemProfile system) => _applicable(system);
    public string? RecommendationFor(SystemProfile system) => _recommend(system);

    /// <summary>Static changes only, for docs and tooltips when no context is available.</summary>
    public IReadOnlyList<IChange> StaticChanges => _staticChanges;
    public bool HasDynamicChanges => _dynamicChanges.Count > 0;

    public IReadOnlyList<IChange> GetChanges(TweakContext ctx)
    {
        if (_dynamicChanges.Count == 0) return _staticChanges;
        var all = new List<IChange>(_staticChanges);
        foreach (var f in _dynamicChanges) all.AddRange(f(ctx));
        return all;
    }

    public bool IsApplied(TweakContext ctx)
    {
        var changes = GetChanges(ctx);
        return changes.Count > 0 && changes.All(c => c.IsApplied(ctx));
    }

    public TweakSnapshot Apply(TweakContext ctx)
    {
        var changes = GetChanges(ctx);
        var snapshot = Capture(ctx, changes);
        ApplyCaptured(ctx, changes, snapshot);
        return snapshot;
    }

    /// <summary>Reads the current state of every change. The engine persists this before applying.</summary>
    public TweakSnapshot Capture(TweakContext ctx) => Capture(ctx, GetChanges(ctx));

    private TweakSnapshot Capture(TweakContext ctx, IReadOnlyList<IChange> changes) => new()
    {
        TweakId = Id,
        CapturedAt = ctx.Clock.Now,
        Values = changes.ToDictionary(c => c.Key, c => c.Capture(ctx)),
        Targets = changes.Select(c => c.Describe()).ToList(),
    };

    /// <summary>
    /// Applies all changes. If one fails, the changes already applied are rolled back using the
    /// snapshot so the system is never left half-changed, then the error is rethrown.
    /// </summary>
    public void ApplyCaptured(TweakContext ctx, TweakSnapshot snapshot) => ApplyCaptured(ctx, GetChanges(ctx), snapshot);

    private static void ApplyCaptured(TweakContext ctx, IReadOnlyList<IChange> changes, TweakSnapshot snapshot)
    {
        var done = new List<IChange>();
        try
        {
            foreach (var c in changes)
            {
                c.Apply(ctx);
                done.Add(c);
            }
        }
        catch
        {
            for (var i = done.Count - 1; i >= 0; i--)
            {
                try { done[i].Restore(ctx, snapshot.Values.GetValueOrDefault(done[i].Key)); }
                catch { /* best effort: the original error is what matters */ }
            }
            throw;
        }
    }

    public void Revert(TweakContext ctx, TweakSnapshot snapshot)
    {
        var errors = new List<string>();
        var changes = GetChanges(ctx);
        // Undo in reverse order of application.
        for (var i = changes.Count - 1; i >= 0; i--)
        {
            var c = changes[i];
            if (!snapshot.Values.TryGetValue(c.Key, out var captured)) continue; // not part of the original apply
            try { c.Restore(ctx, captured); }
            catch (Exception ex) { errors.Add($"{c.Describe()}: {ex.Message}"); }
        }
        if (errors.Count > 0)
            throw new AggregateException($"Some changes could not be reverted:\n{string.Join("\n", errors)}",
                errors.Select(e => new InvalidOperationException(e)));
    }
}
