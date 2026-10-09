using System.Text;
using Frtune.Core.Abstractions;

namespace Frtune.Windows.Platform;

/// <summary>
/// Writes one .reg file per registry key (via reg.exe export) and a services.txt with
/// "sc qc" output to backups\raw\yyyyMMdd-HHmmss-label\. Double-click a .reg file to restore it by hand.
/// </summary>
public sealed class RegExportBackupExporter : IBackupExporter
{
    private readonly string _root;
    private readonly ICommandRunner _commands;

    public RegExportBackupExporter(string root, ICommandRunner commands)
    {
        _root = root;
        _commands = commands;
    }

    public Task<string> ExportAsync(IEnumerable<string> registryKeys, IEnumerable<string> services, string label, CancellationToken ct = default) => Task.Run(() =>
    {
        var dir = Path.Combine(_root, $"{DateTime.Now:yyyyMMdd-HHmmss}-{label}");
        Directory.CreateDirectory(dir);
        var index = new StringBuilder("frtune raw backup\r\n\r\n");
        var n = 0;
        foreach (var key in registryKeys)
        {
            ct.ThrowIfCancellationRequested();
            var file = Path.Combine(dir, $"{++n:000}.reg");
            var r = _commands.Run("reg.exe", $"export \"{key}\" \"{file}\" /y");
            index.AppendLine(r.Success ? $"{n:000}.reg  {key}" : $"---      {key}  (did not exist yet)");
        }
        var svc = new StringBuilder();
        foreach (var s in services)
        {
            var r = _commands.Run("sc.exe", $"qc \"{s}\"");
            svc.AppendLine(r.StdOut.Trim()).AppendLine();
        }
        if (svc.Length > 0) File.WriteAllText(Path.Combine(dir, "services.txt"), svc.ToString());
        File.WriteAllText(Path.Combine(dir, "index.txt"), index.ToString());
        return dir;
    }, ct);
}
