using System.Text.Json;
using Frtune.Core.Abstractions;
using Frtune.Core.Engine;
using Frtune.Core.Tools;

namespace Frtune.Windows.Tools;

/// <summary>Lists and removes Store (Appx) apps for the current user. Protected apps can't be removed.</summary>
public sealed class AppxService
{
    private readonly ICommandRunner _cmd;
    private readonly ChangeLog _log;

    public AppxService(ICommandRunner cmd, ChangeLog log)
    {
        _cmd = cmd;
        _log = log;
    }

    public IReadOnlyList<AppxPackage> List()
    {
        var r = _cmd.PowerShell("Get-AppxPackage | Select-Object Name, PackageFullName, Publisher, IsFramework, NonRemovable | ConvertTo-Json -Compress", TimeSpan.FromMinutes(1));
        if (!r.Success || string.IsNullOrWhiteSpace(r.StdOut)) return Array.Empty<AppxPackage>();
        using var doc = JsonDocument.Parse(r.StdOut);
        var items = doc.RootElement.ValueKind == JsonValueKind.Array ? doc.RootElement.EnumerateArray().ToList() : new() { doc.RootElement };
        return items.Select(e => new AppxPackage(
                e.GetProperty("Name").GetString() ?? "",
                e.GetProperty("PackageFullName").GetString() ?? "",
                e.TryGetProperty("Publisher", out var p) ? p.GetString() ?? "" : "",
                e.TryGetProperty("IsFramework", out var f) && f.ValueKind == JsonValueKind.True,
                e.TryGetProperty("NonRemovable", out var n) && n.ValueKind == JsonValueKind.True))
            .Where(a => !a.IsFramework && !a.NonRemovable)
            .GroupBy(a => a.Name).Select(g => g.First())
            .OrderBy(a => a.Name).ToList();
    }

    public (bool Ok, string Message) Remove(AppxPackage app)
    {
        if (AppxCatalog.IsProtected(app.Name))
            return (false, $"{app.Name} is protected and won't be removed.");
        var r = _cmd.PowerShell($"Remove-AppxPackage -Package '{app.FullName.Replace("'", "''")}'", TimeSpan.FromMinutes(3));
        _log.Add("debloat", $"Removed app {app.Name}", r.Success,
            details: new[] { $"Remove-AppxPackage {app.FullName}", "Reinstall from the Microsoft Store if you need it again." },
            error: r.Success ? null : r.StdErr.Trim());
        return (r.Success, r.Success ? $"Removed {app.Name}" : r.StdErr.Trim());
    }

    /// <summary>Uninstalls OneDrive with Microsoft's own uninstaller. Files in the OneDrive folder are left on disk.</summary>
    public (bool Ok, string Message) UninstallOneDrive()
    {
        _cmd.Run("taskkill.exe", "/f /im OneDrive.exe");
        var win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var setup = new[] { Path.Combine(win, "System32", "OneDriveSetup.exe"), Path.Combine(win, "SysWOW64", "OneDriveSetup.exe") }
            .FirstOrDefault(File.Exists);
        CommandResult r;
        if (setup is not null) r = _cmd.Run(setup, "/uninstall", TimeSpan.FromMinutes(5));
        else r = _cmd.Run("winget.exe", "uninstall --id Microsoft.OneDrive --silent --accept-source-agreements", TimeSpan.FromMinutes(5));
        _log.Add("debloat", "Uninstalled OneDrive", r.Success,
            details: new[] { setup is not null ? $"{setup} /uninstall" : "winget uninstall Microsoft.OneDrive", "Reinstall: winget install Microsoft.OneDrive" },
            error: r.Success ? null : r.StdErr + r.StdOut);
        return (r.Success, r.Success ? "OneDrive uninstalled. Your files in the OneDrive folder were not touched." : "OneDrive uninstall failed: " + r.StdErr + r.StdOut);
    }
}
