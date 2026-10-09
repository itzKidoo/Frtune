using Frtune.Core.Abstractions;

namespace Frtune.Windows.Platform;

/// <summary>
/// Creates restore points with Checkpoint-Computer. Windows allows one per 24 hours by default;
/// when one already exists it is reported as "skipped" (you're still covered by the recent one).
/// </summary>
public sealed class RestorePointService : IRestorePointService
{
    private readonly ICommandRunner _commands;
    public RestorePointService(ICommandRunner commands) => _commands = commands;

    public Task<RestorePointResult> CreateAsync(string description, CancellationToken ct = default) => Task.Run(() =>
    {
        try
        {
            var safe = description.Replace("'", "''");
            var r = _commands.PowerShell(
                "$WarningPreference='Continue';" +
                $"Checkpoint-Computer -Description '{safe}' -RestorePointType MODIFY_SETTINGS -WarningVariable w 3>$null;" +
                "if ($w) { 'WARN:' + ($w -join ' ') } else { 'OK' }",
                TimeSpan.FromMinutes(5));
            var output = r.StdOut.Trim();
            if (r.Success && output.StartsWith("OK")) return new RestorePointResult(true, false, "Restore point created.");
            if (r.Success && output.StartsWith("WARN:"))
                return new RestorePointResult(false, true, "Windows only allows one restore point every 24 hours and one already exists, so that one covers this change. " + output[5..]);
            var err = string.IsNullOrWhiteSpace(r.StdErr) ? output : r.StdErr.Trim();
            if (err.Contains("disabled", StringComparison.OrdinalIgnoreCase) || err.Contains("0x80042306"))
                err = "System Protection is turned off for the system drive. Turn it on in Settings › System › About › System protection.";
            return new RestorePointResult(false, false, err);
        }
        catch (Exception ex)
        {
            return new RestorePointResult(false, false, ex.Message);
        }
    }, ct);
}
