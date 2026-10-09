using System.Diagnostics;
using System.Text.Json;
using Frtune.Core.Abstractions;
using Frtune.Core.Engine;

namespace Frtune.Windows.Tools;

public enum StartupSource { RegistryUser, RegistryMachine, RegistryMachine32, FolderUser, FolderCommon, ScheduledTask }

public sealed record StartupItem
{
    public required string Name { get; init; }
    public required string Command { get; init; }
    public required StartupSource Source { get; init; }
    public required bool Enabled { get; init; }
    public string Publisher { get; init; } = "";
    /// <summary>Estimate, see <see cref="StartupManager.EstimateImpact"/>.</summary>
    public string Impact { get; init; } = "Low";
    public string Location { get; init; } = "";
}

/// <summary>
/// Startup apps from the Run keys, the Startup folders and logon scheduled tasks.
/// Enable/disable uses the same StartupApproved flags as Task Manager, so it is fully reversible
/// and Task Manager shows the same state.
/// </summary>
public sealed class StartupManager
{
    private const string Run = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Run32 = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run";
    private const string Approved = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved";

    private readonly IRegistry _reg;
    private readonly ICommandRunner _cmd;
    private readonly ChangeLog _log;

    public StartupManager(IRegistry reg, ICommandRunner cmd, ChangeLog log)
    {
        _reg = reg;
        _cmd = cmd;
        _log = log;
    }

    public IReadOnlyList<StartupItem> List()
    {
        var items = new List<StartupItem>();
        AddRun(items, RegHive.CurrentUser, Run, StartupSource.RegistryUser, RegHive.CurrentUser, @"\Run");
        AddRun(items, RegHive.LocalMachine, Run, StartupSource.RegistryMachine, RegHive.LocalMachine, @"\Run");
        AddRun(items, RegHive.LocalMachine, Run32, StartupSource.RegistryMachine32, RegHive.LocalMachine, @"\Run32");
        AddFolder(items, Environment.GetFolderPath(Environment.SpecialFolder.Startup), StartupSource.FolderUser, RegHive.CurrentUser);
        AddFolder(items, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), StartupSource.FolderCommon, RegHive.LocalMachine);
        items.AddRange(LogonTasks());
        return items.OrderByDescending(i => i.Enabled).ThenBy(i => i.Name).ToList();
    }

    public void SetEnabled(StartupItem item, bool enabled)
    {
        if (item.Source == StartupSource.ScheduledTask)
        {
            _cmd.Run("schtasks.exe", $"/Change /TN \"{item.Location}\" {(enabled ? "/ENABLE" : "/DISABLE")}").EnsureSuccess("schtasks");
        }
        else
        {
            var (hive, sub) = ApprovedKey(item.Source);
            // Byte 0: 02 = enabled, 03 = disabled; bytes 4-11: FILETIME of the change (as Task Manager writes it).
            var data = new byte[12];
            data[0] = (byte)(enabled ? 2 : 3);
            if (!enabled) BitConverter.GetBytes(DateTime.UtcNow.ToFileTimeUtc()).CopyTo(data, 4);
            _reg.SetValue(hive, Approved + sub, item.Name, RegValue.Bin(data));
        }
        _log.Add("startup", $"{(enabled ? "Enabled" : "Disabled")} startup item {item.Name}", details: new[] { item.Command, item.Source.ToString() });
    }

    private void AddRun(List<StartupItem> items, RegHive hive, string key, StartupSource source, RegHive approvedHive, string approvedSub)
    {
        foreach (var name in ValueNames(hive, key))
        {
            var cmd = _reg.GetValue(hive, key, name)?.Data as string ?? "";
            items.Add(Make(name, cmd, source, IsApproved(approvedHive, approvedSub, name), $@"{RegValue.HiveName(hive)}\{key}"));
        }
    }

    private void AddFolder(List<StartupItem> items, string folder, StartupSource source, RegHive approvedHive)
    {
        if (!Directory.Exists(folder)) return;
        foreach (var f in Directory.GetFiles(folder).Where(f => !f.EndsWith("desktop.ini", StringComparison.OrdinalIgnoreCase)))
        {
            var name = Path.GetFileName(f);
            items.Add(Make(name, f, source, IsApproved(approvedHive, @"\StartupFolder", name), folder));
        }
    }

    private IEnumerable<StartupItem> LogonTasks()
    {
        var r = _cmd.PowerShell(
            "Get-ScheduledTask | Where-Object { $_.Triggers | Where-Object { $_.CimClass.CimClassName -eq 'MSFT_TaskLogonTrigger' } } | " +
            "Where-Object { $_.TaskPath -notlike '\\Microsoft\\*' } | " +
            "ForEach-Object { [pscustomobject]@{ Path = $_.TaskPath + $_.TaskName; Name = $_.TaskName; State = [string]$_.State; Exe = [string]($_.Actions | Select-Object -First 1).Execute } } | ConvertTo-Json -Compress",
            TimeSpan.FromMinutes(1));
        if (!r.Success || string.IsNullOrWhiteSpace(r.StdOut)) yield break;
        var json = r.StdOut.Trim();
        if (json.StartsWith('{')) json = "[" + json + "]";
        foreach (var e in JsonDocument.Parse(json).RootElement.EnumerateArray())
        {
            var exe = e.GetProperty("Exe").GetString() ?? "";
            yield return Make(e.GetProperty("Name").GetString() ?? "", exe, StartupSource.ScheduledTask,
                e.GetProperty("State").GetString() != "Disabled", e.GetProperty("Path").GetString() ?? "");
        }
    }

    private static StartupItem Make(string name, string command, StartupSource source, bool enabled, string location)
    {
        var exe = ExtractExe(command);
        string publisher = "";
        long size = 0;
        try
        {
            if (exe is not null && File.Exists(exe))
            {
                publisher = FileVersionInfo.GetVersionInfo(exe).CompanyName ?? "";
                size = new FileInfo(exe).Length;
            }
        }
        catch { }
        return new StartupItem
        {
            Name = name, Command = command, Source = source, Enabled = enabled, Publisher = publisher,
            Impact = EstimateImpact(publisher, size), Location = location,
        };
    }

    /// <summary>
    /// Windows' own impact rating comes from boot traces that aren't readable without ETW tooling,
    /// so this is a labelled estimate based on executable size.
    /// </summary>
    public static string EstimateImpact(string publisher, long size)
    {
        if (size > 50_000_000) return "High";
        if (size > 5_000_000) return "Medium";
        return "Low";
    }

    private static string? ExtractExe(string command)
    {
        command = Environment.ExpandEnvironmentVariables(command.Trim());
        if (command.StartsWith('"')) { var end = command.IndexOf('"', 1); return end > 1 ? command[1..end] : null; }
        var idx = command.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        return idx > 0 ? command[..(idx + 4)] : command.Split(' ')[0];
    }

    private bool IsApproved(RegHive hive, string sub, string name)
    {
        var v = _reg.GetValue(hive, Approved + sub, name);
        return v is not { Kind: RegKind.Binary } || ((byte[])v.Data).Length == 0 || ((byte[])v.Data)[0] % 2 == 0;
    }

    private static (RegHive, string) ApprovedKey(StartupSource s) => s switch
    {
        StartupSource.RegistryUser => (RegHive.CurrentUser, @"\Run"),
        StartupSource.RegistryMachine => (RegHive.LocalMachine, @"\Run"),
        StartupSource.RegistryMachine32 => (RegHive.LocalMachine, @"\Run32"),
        StartupSource.FolderUser => (RegHive.CurrentUser, @"\StartupFolder"),
        StartupSource.FolderCommon => (RegHive.LocalMachine, @"\StartupFolder"),
        _ => throw new ArgumentOutOfRangeException(nameof(s)),
    };

    private IEnumerable<string> ValueNames(RegHive hive, string key)
    {
        using var b = Microsoft.Win32.RegistryKey.OpenBaseKey(hive == RegHive.LocalMachine ? Microsoft.Win32.RegistryHive.LocalMachine : Microsoft.Win32.RegistryHive.CurrentUser, Microsoft.Win32.RegistryView.Registry64);
        using var k = b.OpenSubKey(key);
        return k?.GetValueNames().Where(n => n.Length > 0).ToArray() ?? Array.Empty<string>();
    }
}
