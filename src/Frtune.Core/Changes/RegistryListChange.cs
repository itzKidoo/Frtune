using Frtune.Core.Abstractions;
using Frtune.Core.Tweaks;

namespace Frtune.Core.Changes;

/// <summary>
/// Edits one "Key=Value;" entry inside a semicolon-separated REG_SZ value, leaving the other
/// entries untouched. Windows stores several graphics settings in
/// HKCU\Software\Microsoft\DirectX\UserGpuPreferences\DirectXUserGlobalSettings this way.
/// </summary>
public sealed class RegistryListChange : IChange
{
    public RegHive Hive { get; }
    public string KeyPath { get; }
    public string Name { get; }
    public string Entry { get; }
    public string EntryValue { get; }

    public RegistryListChange(RegHive hive, string keyPath, string name, string entry, string entryValue)
    {
        Hive = hive;
        KeyPath = keyPath;
        Name = name;
        Entry = entry;
        EntryValue = entryValue;
    }

    public string Key => $"reglist:{RegValue.HiveName(Hive)}\\{KeyPath}\\{Name}#{Entry}".ToLowerInvariant();
    public ChangeKind Kind => ChangeKind.Registry;
    public string Target => $"{RegValue.HiveName(Hive)}\\{KeyPath}";
    public string Describe() => $"{Target}\\{Name}: set entry {Entry}={EntryValue}";

    // Capture tokens: "=v" entry had value v; "<noentry>" value existed without the entry;
    // "<novalue>" the value didn't exist; "<nokey>path" the key didn't exist (path = first missing key).
    public string? Capture(TweakContext ctx)
    {
        if (!ctx.Registry.KeyExists(Hive, KeyPath))
        {
            var parts = KeyPath.Split('\\');
            var first = Enumerable.Range(1, parts.Length).Select(i => string.Join('\\', parts.Take(i)))
                .First(p => !ctx.Registry.KeyExists(Hive, p));
            return "<nokey>" + first;
        }
        var raw = ctx.Registry.GetValue(Hive, KeyPath, Name);
        if (raw is null) return "<novalue>";
        return Parse(raw.Data as string).TryGetValue(Entry, out var v) ? "=" + v : "<noentry>";
    }

    public void Apply(TweakContext ctx) => Write(ctx, EntryValue);

    public void Restore(TweakContext ctx, string? captured)
    {
        if (captured is not null && captured.StartsWith('='))
        {
            Write(ctx, captured[1..]);
            return;
        }
        Write(ctx, null);
        if (captured is null || captured == "<noentry>") return;

        // We created the value (and maybe the key). Remove them if nothing else lives there now.
        if (ctx.Registry.GetValue(Hive, KeyPath, Name)?.Data is string s && s.Length == 0)
            ctx.Registry.DeleteValue(Hive, KeyPath, Name);
        if (captured.StartsWith("<nokey>"))
        {
            var created = captured["<nokey>".Length..];
            if (ctx.Registry.KeyExists(Hive, created) && ctx.Registry.IsTreeEmpty(Hive, created))
                ctx.Registry.DeleteKeyTree(Hive, created);
        }
    }

    public bool IsApplied(TweakContext ctx) => Read(ctx).TryGetValue(Entry, out var v) && v == EntryValue;

    private Dictionary<string, string> Read(TweakContext ctx) => Parse(ctx.Registry.GetValue(Hive, KeyPath, Name)?.Data as string);

    private void Write(TweakContext ctx, string? value)
    {
        var raw = ctx.Registry.GetValue(Hive, KeyPath, Name)?.Data as string;
        var entries = ParseOrdered(raw);
        var idx = entries.FindIndex(e => e.Key.Equals(Entry, StringComparison.OrdinalIgnoreCase));
        if (value is null) { if (idx < 0) return; entries.RemoveAt(idx); }
        else if (idx >= 0) entries[idx] = new(Entry, value);
        else entries.Add(new(Entry, value));

        ctx.Registry.SetValue(Hive, KeyPath, Name, RegValue.Str(string.Concat(entries.Select(e => $"{e.Key}={e.Value};"))));
    }

    public static Dictionary<string, string> Parse(string? raw) =>
        ParseOrdered(raw).ToDictionary(e => e.Key, e => e.Value, StringComparer.OrdinalIgnoreCase);

    private static List<KeyValuePair<string, string>> ParseOrdered(string? raw)
    {
        var list = new List<KeyValuePair<string, string>>();
        if (string.IsNullOrEmpty(raw)) return list;
        foreach (var part in raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eq = part.IndexOf('=');
            if (eq > 0) list.Add(new(part[..eq], part[(eq + 1)..]));
        }
        return list;
    }
}
