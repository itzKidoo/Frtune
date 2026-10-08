using Frtune.Core.Abstractions;
using Frtune.Core.Tweaks;

namespace Frtune.Core.Changes;

/// <summary>
/// Sets (or deletes, when the desired value is null) one registry value.
/// If the key did not exist before, the backup remembers the first missing key so that
/// revert removes the keys we created rather than leaving empty ones behind.
/// </summary>
public sealed class RegistryChange : IChange
{
    private const string MissingKeyPrefix = "<nokey>";
    private readonly Func<TweakContext, RegValue?>? _factory;
    private readonly RegValue? _value;
    private readonly string? _valueDescription;

    public RegHive Hive { get; }
    public string KeyPath { get; }
    public string Name { get; }
    /// <summary>True when the change deletes the value instead of setting it.</summary>
    public bool IsDelete { get; }

    public RegistryChange(RegHive hive, string keyPath, string name, RegValue? value)
    {
        Hive = hive;
        KeyPath = keyPath.Trim('\\');
        Name = name;
        _value = value;
        IsDelete = value is null;
    }

    /// <summary>A value computed at apply time (e.g. from RAM size or today's date).</summary>
    public RegistryChange(RegHive hive, string keyPath, string name, Func<TweakContext, RegValue> factory, string valueDescription)
    {
        Hive = hive;
        KeyPath = keyPath.Trim('\\');
        Name = name;
        _factory = factory;
        _valueDescription = valueDescription;
    }

    public string Key => $"reg:{RegValue.HiveName(Hive)}\\{KeyPath}\\{Name}".ToLowerInvariant();
    public ChangeKind Kind => ChangeKind.Registry;
    public string Target => $"{RegValue.HiveName(Hive)}\\{KeyPath}";
    private string ValueLabel => Name.Length == 0 ? "(Default)" : Name;

    public RegValue? DesiredValue(TweakContext ctx) => _factory is not null ? _factory(ctx) : _value;

    public string Describe() => IsDelete
        ? $"Delete {Target}\\{ValueLabel}"
        : $"{Target}\\{ValueLabel} = {(_factory is not null ? _valueDescription : _value!.ToString())}";

    public string? Capture(TweakContext ctx)
    {
        if (!ctx.Registry.KeyExists(Hive, KeyPath))
            return MissingKeyPrefix + FirstMissingKey(ctx);
        return ctx.Registry.GetValue(Hive, KeyPath, Name)?.ToToken();
    }

    public void Apply(TweakContext ctx)
    {
        var desired = DesiredValue(ctx);
        if (desired is null) ctx.Registry.DeleteValue(Hive, KeyPath, Name);
        else ctx.Registry.SetValue(Hive, KeyPath, Name, desired);
    }

    public void Restore(TweakContext ctx, string? captured)
    {
        if (captured is null)
        {
            ctx.Registry.DeleteValue(Hive, KeyPath, Name);
        }
        else if (captured.StartsWith(MissingKeyPrefix, StringComparison.Ordinal))
        {
            // Value lived in a key we created. Remove the value; remove the key tree only if
            // nothing else has been written to it since (another tweak may share the key).
            ctx.Registry.DeleteValue(Hive, KeyPath, Name);
            var created = captured[MissingKeyPrefix.Length..];
            if (created.Length > 0 && IsEmptyTree(ctx, created))
                ctx.Registry.DeleteKeyTree(Hive, created);
        }
        else
        {
            ctx.Registry.SetValue(Hive, KeyPath, Name, RegValue.FromToken(captured));
        }
    }

    public bool IsApplied(TweakContext ctx)
    {
        var current = ctx.Registry.GetValue(Hive, KeyPath, Name);
        var desired = DesiredValue(ctx);
        if (desired is null) return current is null;
        // Computed values (dates) can't be compared exactly; "present" counts as applied.
        if (_factory is not null && desired.Kind == Abstractions.RegKind.String) return current is not null;
        return desired.ValueEquals(current);
    }

    private string FirstMissingKey(TweakContext ctx)
    {
        var parts = KeyPath.Split('\\');
        for (var i = 1; i <= parts.Length; i++)
        {
            var path = string.Join('\\', parts.Take(i));
            if (!ctx.Registry.KeyExists(Hive, path)) return path;
        }
        return KeyPath;
    }

    private bool IsEmptyTree(TweakContext ctx, string path) =>
        ctx.Registry.KeyExists(Hive, path) && ctx.Registry.IsTreeEmpty(Hive, path);
}
