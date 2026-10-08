using Frtune.Core.Abstractions;

namespace Frtune.Core.Tests.Fakes;

/// <summary>In-memory registry: hive + case-insensitive key path → values.</summary>
public sealed class FakeRegistry : IRegistry
{
    private readonly Dictionary<string, Dictionary<string, RegValue>> _keys = new(StringComparer.OrdinalIgnoreCase);

    public int Writes { get; private set; }
    /// <summary>When set, SetValue throws for this value name (to test rollback).</summary>
    public string? FailOnName { get; set; }

    private static string K(RegHive h, string path) => $"{h}\\{path.Trim('\\')}";

    public RegValue? GetValue(RegHive hive, string keyPath, string name) =>
        _keys.TryGetValue(K(hive, keyPath), out var v) && v.TryGetValue(name, out var r) ? r : null;

    public void SetValue(RegHive hive, string keyPath, string name, RegValue value)
    {
        if (FailOnName is not null && name == FailOnName) throw new UnauthorizedAccessException("denied: " + name);
        EnsureKey(hive, keyPath)[name] = value;
        Writes++;
    }

    public void DeleteValue(RegHive hive, string keyPath, string name)
    {
        if (_keys.TryGetValue(K(hive, keyPath), out var v)) v.Remove(name);
    }

    public bool KeyExists(RegHive hive, string keyPath) => _keys.ContainsKey(K(hive, keyPath));

    public void DeleteKeyTree(RegHive hive, string keyPath)
    {
        var prefix = K(hive, keyPath);
        foreach (var k in _keys.Keys.Where(k => k.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
                                                 k.StartsWith(prefix + "\\", StringComparison.OrdinalIgnoreCase)).ToList())
            _keys.Remove(k);
    }

    public bool IsTreeEmpty(RegHive hive, string keyPath)
    {
        var prefix = K(hive, keyPath);
        return _keys.Where(k => k.Key.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
                                k.Key.StartsWith(prefix + "\\", StringComparison.OrdinalIgnoreCase))
            .All(k => k.Value.Count == 0);
    }

    public IReadOnlyList<string> GetSubKeyNames(RegHive hive, string keyPath)
    {
        var prefix = K(hive, keyPath) + "\\";
        return _keys.Keys.Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(k => k[prefix.Length..].Split('\\')[0])
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private Dictionary<string, RegValue> EnsureKey(RegHive hive, string keyPath)
    {
        var parts = keyPath.Trim('\\').Split('\\');
        for (var i = 1; i <= parts.Length; i++)
        {
            var k = K(hive, string.Join('\\', parts.Take(i)));
            if (!_keys.ContainsKey(k)) _keys[k] = new(StringComparer.OrdinalIgnoreCase);
        }
        return _keys[K(hive, keyPath)];
    }

    /// <summary>Deterministic dump used to assert that a revert restored everything exactly.</summary>
    public string Dump() => string.Join("\n", _keys.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase)
        .Select(k => k.Key.ToLowerInvariant() + " {" + string.Join(", ", k.Value.OrderBy(v => v.Key, StringComparer.OrdinalIgnoreCase)
            .Select(v => $"{v.Key}={v.Value.ToToken()}")) + "}"));
}
