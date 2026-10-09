using System.Text.Json;
using Frtune.Core.Abstractions;
using Frtune.Core.Engine;

namespace Frtune.Windows.Tools;

public sealed record AdapterDns(int InterfaceIndex, string Alias, string[] Servers);

/// <summary>DNS switcher and network reset buttons. Every action is written to the change log.</summary>
public sealed class NetworkTools
{
    private readonly ICommandRunner _cmd;
    private readonly ChangeLog _log;
    private readonly string _dnsBackupPath;

    public NetworkTools(ICommandRunner cmd, ChangeLog log, string dataDir)
    {
        _cmd = cmd;
        _log = log;
        _dnsBackupPath = Path.Combine(dataDir, "backups", "dns-original.json");
    }

    /// <summary>Connected physical adapters and their current IPv4 DNS servers.</summary>
    public IReadOnlyList<AdapterDns> GetDns()
    {
        var r = _cmd.PowerShell(
            "Get-NetAdapter -Physical | Where-Object Status -eq 'Up' | ForEach-Object { " +
            "$d = Get-DnsClientServerAddress -InterfaceIndex $_.ifIndex -AddressFamily IPv4; " +
            "[pscustomobject]@{ InterfaceIndex = $_.ifIndex; Alias = $_.Name; Servers = @($d.ServerAddresses) } } | ConvertTo-Json -Depth 3 -Compress");
        if (!r.Success || string.IsNullOrWhiteSpace(r.StdOut)) return Array.Empty<AdapterDns>();
        var json = r.StdOut.Trim();
        if (json.StartsWith('{')) json = "[" + json + "]";
        return JsonSerializer.Deserialize<List<AdapterDns>>(json) ?? new();
    }

    /// <summary>Sets DNS on all active adapters. The very first original setting is saved so "Restore DNS" can undo it.</summary>
    public void SetDns(string primary, string secondary)
    {
        var adapters = GetDns();
        if (!File.Exists(_dnsBackupPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_dnsBackupPath)!);
            File.WriteAllText(_dnsBackupPath, JsonSerializer.Serialize(adapters));
        }
        foreach (var a in adapters)
            _cmd.PowerShell($"Set-DnsClientServerAddress -InterfaceIndex {a.InterfaceIndex} -ServerAddresses ('{primary}','{secondary}')").EnsureSuccess("Set DNS");
        _cmd.Run("ipconfig.exe", "/flushdns");
        _log.Add("tool", $"DNS set to {primary}, {secondary}", details: adapters.Select(a => $"{a.Alias}: was {(a.Servers.Length == 0 ? "automatic" : string.Join(", ", a.Servers))}"));
    }

    public bool HasDnsBackup => File.Exists(_dnsBackupPath);

    /// <summary>Puts back the DNS servers that were set before frtune first changed them.</summary>
    public void RestoreDns()
    {
        if (!File.Exists(_dnsBackupPath)) return;
        var original = JsonSerializer.Deserialize<List<AdapterDns>>(File.ReadAllText(_dnsBackupPath)) ?? new();
        foreach (var a in original)
        {
            var script = a.Servers.Length == 0
                ? $"Set-DnsClientServerAddress -InterfaceIndex {a.InterfaceIndex} -ResetServerAddresses"
                : $"Set-DnsClientServerAddress -InterfaceIndex {a.InterfaceIndex} -ServerAddresses ({string.Join(",", a.Servers.Select(s => $"'{s}'"))})";
            _cmd.PowerShell(script);
        }
        File.Delete(_dnsBackupPath);
        _log.Add("tool", "DNS restored to original settings", details: original.Select(a => $"{a.Alias}: {(a.Servers.Length == 0 ? "automatic (DHCP)" : string.Join(", ", a.Servers))}"));
    }

    public string FlushDns() => Run("Flush DNS cache", "ipconfig.exe", "/flushdns");
    public string ResetWinsock() => Run("Reset Winsock (restart required)", "netsh.exe", "winsock reset");
    public string ResetTcpIp() => Run("Reset TCP/IP stack (restart required)", "netsh.exe", "int ip reset");

    private string Run(string title, string file, string args)
    {
        var r = _cmd.Run(file, args);
        _log.Add("tool", title, r.Success, details: new[] { $"{file} {args}" }, error: r.Success ? null : r.StdErr + r.StdOut);
        return r.Success ? title + ": done" : $"{title} failed: {r.StdOut}{r.StdErr}".Trim();
    }
}
