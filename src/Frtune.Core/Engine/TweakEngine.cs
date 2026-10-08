using Frtune.Core.Abstractions;
using Frtune.Core.Changes;
using Frtune.Core.Model;
using Frtune.Core.Tweaks;

namespace Frtune.Core.Engine;

public enum TweakStatus
{
    Off,
    /// <summary>Applied by frtune; a backup of the original values exists.</summary>
    On,
    /// <summary>Already in the tweaked state (Windows default or set elsewhere). No backup, so frtune won't revert it.</summary>
    AlreadySet,
    /// <summary>frtune applied it but something has since changed some of the values back.</summary>
    Partial,
    Error,
}

public sealed record TweakRequest(ITweak Tweak, bool Enable);

public sealed record PlanItem(ITweak Tweak, bool Enable, IReadOnlyList<string> Lines, string? Problem)
{
    public bool CanRun => Problem is null;
}

public sealed record ExecuteOptions
{
    public bool CreateRestorePoint { get; init; } = true;
    /// <summary>When false (default) a failed restore point aborts the whole run.</summary>
    public bool ContinueWithoutRestorePoint { get; init; }
    public bool ExportRawBackup { get; init; } = true;
}

public sealed record TweakOutcome(ITweak Tweak, bool Enable, bool Success, string Message);

public sealed record ExecuteReport
{
    public bool Aborted { get; init; }
    public string? AbortReason { get; init; }
    public RestorePointResult? RestorePoint { get; init; }
    public string? BackupFolder { get; init; }
    public List<TweakOutcome> Outcomes { get; init; } = new();
    public RestartKind Restart { get; init; }
    public int Succeeded => Outcomes.Count(o => o.Success);
    public int Failed => Outcomes.Count(o => !o.Success);
}

public sealed record EngineProgress(int Done, int Total, string Message);

/// <summary>
/// Applies and reverts tweaks safely:
/// restore point → raw .reg/service export → snapshot saved to disk → change → change log.
/// </summary>
public sealed class TweakEngine
{
    private readonly IBackupStore _backups;
    private readonly ChangeLog _log;
    private readonly IRestorePointService _restorePoints;
    private readonly IBackupExporter? _exporter;

    public TweakEngine(TweakContext context, IBackupStore backups, ChangeLog log,
        IRestorePointService restorePoints, IBackupExporter? exporter = null)
    {
        Context = context;
        _backups = backups;
        _log = log;
        _restorePoints = restorePoints;
        _exporter = exporter;
    }

    public TweakContext Context { get; set; }
    public ChangeLog Log => _log;
    public IBackupStore Backups => _backups;

    public TweakStatus GetStatus(ITweak tweak)
    {
        try
        {
            var applied = tweak.IsApplied(Context);
            var hasBackup = _backups.TryGet(tweak.Id) is not null;
            return (applied, hasBackup) switch
            {
                (true, true) => TweakStatus.On,
                (true, false) => TweakStatus.AlreadySet,
                (false, true) => TweakStatus.Partial,
                _ => TweakStatus.Off,
            };
        }
        catch
        {
            return TweakStatus.Error;
        }
    }

    /// <summary>Describes exactly what each request will do, for the confirmation dialog.</summary>
    public IReadOnlyList<PlanItem> Plan(IEnumerable<TweakRequest> requests)
    {
        var items = new List<PlanItem>();
        foreach (var r in Order(requests))
        {
            string? problem = null;
            List<string> lines;
            try
            {
                var changes = r.Tweak.GetChanges(Context);
                if (r.Enable)
                {
                    lines = changes.Select(c => c.Describe()).ToList();
                    if (!r.Tweak.IsApplicable(Context.System)) problem = "Not applicable to this PC.";
                }
                else
                {
                    var snap = _backups.TryGet(r.Tweak.Id);
                    if (snap is null)
                    {
                        problem = "No backup of the original values exists (it was already set before frtune). frtune won't guess the original.";
                        lines = new();
                    }
                    else
                    {
                        lines = changes.Where(c => snap.Values.ContainsKey(c.Key))
                            .Select(c => $"Restore {c.Target} to {Show(snap.Values[c.Key])}").ToList();
                    }
                }
            }
            catch (Exception ex)
            {
                lines = new();
                problem = ex.Message;
            }
            items.Add(new PlanItem(r.Tweak, r.Enable, lines, problem));
        }
        return items;
    }

    public Task<ExecuteReport> ExecuteAsync(IReadOnlyList<TweakRequest> requests, ExecuteOptions? options = null,
        IProgress<EngineProgress>? progress = null, CancellationToken ct = default)
        => Task.Run(() => ExecuteCore(requests, options ?? new ExecuteOptions(), progress, ct), ct);

    /// <summary>Reverts every tweak frtune has a backup for, newest first.</summary>
    public Task<ExecuteReport> RestoreAllAsync(IEnumerable<ITweak> catalog, IProgress<EngineProgress>? progress = null, CancellationToken ct = default)
    {
        var byId = catalog.ToDictionary(t => t.Id);
        var requests = _backups.All()
            .OrderByDescending(s => s.CapturedAt)
            .Where(s => byId.ContainsKey(s.TweakId))
            .Select(s => new TweakRequest(byId[s.TweakId], false))
            .ToList();
        return ExecuteAsync(requests, new ExecuteOptions { CreateRestorePoint = false, ExportRawBackup = false }, progress, ct);
    }

    private async Task<ExecuteReport> ExecuteCore(IReadOnlyList<TweakRequest> requests, ExecuteOptions options,
        IProgress<EngineProgress>? progress, CancellationToken ct)
    {
        var ordered = Order(requests).ToList();
        var enabling = ordered.Where(r => r.Enable).ToList();
        RestorePointResult? rp = null;
        string? backupFolder = null;

        // 1. System Restore point (only when something new is being applied).
        if (enabling.Count > 0 && options.CreateRestorePoint)
        {
            progress?.Report(new EngineProgress(0, ordered.Count, "Creating a System Restore point…"));
            rp = await _restorePoints.CreateAsync($"frtune: before applying {enabling.Count} tweak(s)", ct).ConfigureAwait(false);
            _log.Add("restore-point", rp.Success ? "Restore point created" : rp.Skipped ? "Restore point skipped" : "Restore point failed",
                rp.Success || rp.Skipped, details: new[] { rp.Message });
            if (!rp.Success && !rp.Skipped && !options.ContinueWithoutRestorePoint)
                return new ExecuteReport { Aborted = true, AbortReason = "Could not create a System Restore point: " + rp.Message, RestorePoint = rp };
        }

        // 2. Raw export of every registry key and service we're about to touch.
        if (enabling.Count > 0 && options.ExportRawBackup && _exporter is not null)
        {
            progress?.Report(new EngineProgress(0, ordered.Count, "Exporting registry and service backup…"));
            var changes = enabling.SelectMany(r => SafeChanges(r.Tweak)).ToList();
            var keys = changes.OfType<RegistryChange>().Select(c => c.Target)
                .Concat(changes.OfType<RegistryListChange>().Select(c => c.Target))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var services = changes.OfType<ServiceChange>().Select(c => c.ServiceName).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            try
            {
                backupFolder = await _exporter.ExportAsync(keys, services, "before-apply", ct).ConfigureAwait(false);
                _log.Add("backup-export", "Exported registry/service backup", details: new[] { backupFolder });
            }
            catch (Exception ex)
            {
                // The JSON snapshot is still written per tweak below, so this is not fatal.
                _log.Add("backup-export", "Raw backup export failed", false, error: ex.Message);
            }
        }

        // 3. The tweaks themselves.
        var outcomes = new List<TweakOutcome>();
        var restart = RestartKind.None;
        for (var i = 0; i < ordered.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var (tweak, enable) = (ordered[i].Tweak, ordered[i].Enable);
            progress?.Report(new EngineProgress(i, ordered.Count, $"{(enable ? "Applying" : "Reverting")}: {tweak.Name}"));
            var outcome = enable ? ApplyOne(tweak) : RevertOne(tweak);
            outcomes.Add(outcome);
            if (outcome.Success && tweak.Restart > restart) restart = tweak.Restart;
        }
        progress?.Report(new EngineProgress(ordered.Count, ordered.Count, "Done"));

        return new ExecuteReport { RestorePoint = rp, BackupFolder = backupFolder, Outcomes = outcomes, Restart = restart };
    }

    private TweakOutcome ApplyOne(ITweak tweak)
    {
        if (!tweak.IsApplicable(Context.System))
            return Fail(tweak, true, "Not applicable to this PC.");
        TweakSnapshot snapshot;
        try
        {
            // If we already hold a backup (re-apply after a partial revert) keep the ORIGINAL one.
            snapshot = _backups.TryGet(tweak.Id) ?? tweak.Capture(Context);
            _backups.Save(snapshot); // persisted before anything changes
        }
        catch (Exception ex)
        {
            return Fail(tweak, true, "Could not back up current values: " + ex.Message);
        }

        try
        {
            tweak.ApplyCaptured(Context, snapshot);
            _log.Add("apply", tweak.Name, tweakId: tweak.Id, details: snapshot.Targets);
            return new TweakOutcome(tweak, true, true, "Applied");
        }
        catch (Exception ex)
        {
            // Tweak.ApplyCaptured already rolled back the partial changes.
            _backups.Remove(tweak.Id);
            return Fail(tweak, true, ex.Message);
        }
    }

    private TweakOutcome RevertOne(ITweak tweak)
    {
        var snapshot = _backups.TryGet(tweak.Id);
        if (snapshot is null)
            return Fail(tweak, false, "No backup of the original values. Nothing was changed.");
        try
        {
            tweak.Revert(Context, snapshot);
            _backups.Remove(tweak.Id);
            _log.Add("revert", tweak.Name, tweakId: tweak.Id,
                details: snapshot.Values.Select(kv => $"{kv.Key} → {Show(kv.Value)}"));
            return new TweakOutcome(tweak, false, true, "Reverted");
        }
        catch (Exception ex)
        {
            return Fail(tweak, false, ex.Message);
        }
    }

    private TweakOutcome Fail(ITweak tweak, bool enable, string message)
    {
        _log.Add(enable ? "apply" : "revert", tweak.Name, false, tweak.Id, error: message);
        return new TweakOutcome(tweak, enable, false, message);
    }

    private IEnumerable<IChange> SafeChanges(ITweak t)
    {
        try { return t.GetChanges(Context); }
        catch { return Array.Empty<IChange>(); }
    }

    /// <summary>Reverts run before applies so that replacing one tweak with another works cleanly.</summary>
    private static IEnumerable<TweakRequest> Order(IEnumerable<TweakRequest> requests) =>
        requests.Where(r => !r.Enable).Concat(requests.Where(r => r.Enable));

    private static string Show(string? token) => token switch
    {
        null => "(absent — will be deleted)",
        _ when token.StartsWith("<nokey>") => "(key did not exist — will be removed)",
        _ when token.Contains(':') && !token.Contains('|') => TryFormat(token),
        _ => token,
    };

    private static string TryFormat(string token)
    {
        try { return RegValue.FromToken(token).ToString(); }
        catch { return token; }
    }
}
