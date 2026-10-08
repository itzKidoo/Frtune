using System.Text.Json;

namespace Frtune.Core.Engine;

public sealed record ChangeLogEntry
{
    public DateTimeOffset Timestamp { get; init; }
    /// <summary>apply, revert, restore-point, backup-export, tool, error…</summary>
    public required string Action { get; init; }
    public string? TweakId { get; init; }
    public required string Title { get; init; }
    public bool Success { get; init; } = true;
    public List<string> Details { get; init; } = new();
    public string? Error { get; init; }
}

/// <summary>Append-only JSON log of everything the app did. Shown on the Backups page.</summary>
public sealed class ChangeLog
{
    private readonly string? _path;
    private readonly object _gate = new();
    private readonly List<ChangeLogEntry> _entries;

    public event Action<ChangeLogEntry>? EntryAdded;

    /// <param name="path">changelog.json path, or null for an in-memory log (tests).</param>
    public ChangeLog(string? path)
    {
        _path = path;
        _entries = path is not null && File.Exists(path)
            ? JsonSerializer.Deserialize<List<ChangeLogEntry>>(File.ReadAllText(path), Json.Options) ?? new()
            : new();
        if (path is not null) Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    }

    public string? FilePath => _path;

    public IReadOnlyList<ChangeLogEntry> Entries
    {
        get { lock (_gate) return _entries.ToList(); }
    }

    public ChangeLogEntry Add(string action, string title, bool success = true, string? tweakId = null,
        IEnumerable<string>? details = null, string? error = null)
    {
        var entry = new ChangeLogEntry
        {
            Timestamp = DateTimeOffset.Now,
            Action = action,
            Title = title,
            Success = success,
            TweakId = tweakId,
            Details = details?.ToList() ?? new(),
            Error = error,
        };
        lock (_gate)
        {
            _entries.Add(entry);
            if (_path is not null) Json.WriteAtomic(_path, _entries);
        }
        EntryAdded?.Invoke(entry);
        return entry;
    }
}
