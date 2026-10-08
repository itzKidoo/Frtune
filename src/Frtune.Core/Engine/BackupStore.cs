using System.Text.Json;
using Frtune.Core.Tweaks;

namespace Frtune.Core.Engine;

public interface IBackupStore
{
    void Save(TweakSnapshot snapshot);
    TweakSnapshot? TryGet(string tweakId);
    void Remove(string tweakId);
    IReadOnlyList<TweakSnapshot> All();
}

/// <summary>
/// Original values of every applied tweak, written to snapshots.json before anything changes.
/// The file is rewritten atomically (temp file + move) so a crash can't corrupt it.
/// </summary>
public sealed class JsonBackupStore : IBackupStore
{
    private readonly string _path;
    private readonly object _gate = new();
    private Dictionary<string, TweakSnapshot> _items;

    public JsonBackupStore(string directory)
    {
        Directory.CreateDirectory(directory);
        _path = Path.Combine(directory, "snapshots.json");
        _items = Load();
    }

    public string FilePath => _path;

    public void Save(TweakSnapshot snapshot)
    {
        lock (_gate)
        {
            _items[snapshot.TweakId] = snapshot;
            Flush();
        }
    }

    public TweakSnapshot? TryGet(string tweakId)
    {
        lock (_gate) return _items.GetValueOrDefault(tweakId);
    }

    public void Remove(string tweakId)
    {
        lock (_gate)
        {
            if (_items.Remove(tweakId)) Flush();
        }
    }

    public IReadOnlyList<TweakSnapshot> All()
    {
        lock (_gate) return _items.Values.OrderBy(s => s.CapturedAt).ToList();
    }

    private Dictionary<string, TweakSnapshot> Load()
    {
        if (!File.Exists(_path)) return new();
        var list = JsonSerializer.Deserialize<List<TweakSnapshot>>(File.ReadAllText(_path), Json.Options) ?? new();
        return list.ToDictionary(s => s.TweakId);
    }

    private void Flush() => Json.WriteAtomic(_path, _items.Values.OrderBy(s => s.CapturedAt).ToList());
}

internal static class Json
{
    public static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static void WriteAtomic<T>(string path, T value)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(value, Options));
        File.Move(tmp, path, overwrite: true);
    }
}
