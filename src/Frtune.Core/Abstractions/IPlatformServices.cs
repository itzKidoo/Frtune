namespace Frtune.Core.Abstractions;

public sealed record RestorePointResult(bool Success, bool Skipped, string Message);

public interface IRestorePointService
{
    /// <summary>Creates a System Restore point. Must never throw: failures are reported in the result.</summary>
    Task<RestorePointResult> CreateAsync(string description, CancellationToken ct = default);
}

/// <summary>
/// Exports the raw state of everything that is about to change (".reg" files for registry keys,
/// a text dump of service configuration) so a user can restore by hand even without the app.
/// </summary>
public interface IBackupExporter
{
    /// <summary>Returns the folder the export was written to.</summary>
    Task<string> ExportAsync(IEnumerable<string> registryKeys, IEnumerable<string> services, string label, CancellationToken ct = default);
}

public interface IClock
{
    DateTimeOffset Now { get; }
}

public sealed class SystemClock : IClock
{
    public static readonly SystemClock Instance = new();
    public DateTimeOffset Now => DateTimeOffset.Now;
}
