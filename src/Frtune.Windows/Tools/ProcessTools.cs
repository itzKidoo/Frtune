using System.Diagnostics;
using Frtune.Core.Abstractions;
using Frtune.Core.Engine;

namespace Frtune.Windows.Tools;

public sealed record ProcessRow(int Pid, string Name, double MemoryMb, string Priority, string? Path);

/// <summary>Process list with "kill and boost": end selected background apps, then raise a game's priority.</summary>
public sealed class ProcessTools
{
    private static readonly HashSet<string> Critical = new(StringComparer.OrdinalIgnoreCase)
    {
        "System", "Idle", "smss", "csrss", "wininit", "winlogon", "services", "lsass", "svchost", "dwm", "explorer",
        "fontdrvhost", "Registry", "Memory Compression", "MsMpEng", "SecurityHealthService", "audiodg", "sihost", "ctfmon", "conhost", "frtune",
    };

    private readonly ChangeLog _log;
    public ProcessTools(ChangeLog log) => _log = log;

    public static bool IsCritical(string name) => Critical.Contains(name);

    public IReadOnlyList<ProcessRow> List() =>
        Process.GetProcesses().Select(p =>
        {
            using (p)
            {
                string prio = "?", path = null!;
                try { prio = p.PriorityClass.ToString(); } catch { }
                try { path = p.MainModule?.FileName!; } catch { }
                return new ProcessRow(p.Id, p.ProcessName, p.WorkingSet64 / 1048576.0, prio, path);
            }
        }).OrderByDescending(r => r.MemoryMb).ToList();

    public string KillAndBoost(IEnumerable<ProcessRow> kill, ProcessRow? boost)
    {
        var killed = new List<string>();
        foreach (var r in kill.Where(r => !IsCritical(r.Name)))
        {
            try { using var p = Process.GetProcessById(r.Pid); p.Kill(entireProcessTree: true); killed.Add($"{r.Name} ({r.Pid})"); }
            catch { }
        }
        string? boosted = null;
        if (boost is not null)
        {
            try { using var p = Process.GetProcessById(boost.Pid); p.PriorityClass = ProcessPriorityClass.High; boosted = boost.Name; }
            catch { }
        }
        _log.Add("tool", "Kill and boost", details: killed.Select(k => "Ended " + k).Concat(boosted is null ? Array.Empty<string>() : new[] { $"{boosted} → High priority" }));
        return $"Ended {killed.Count} process(es){(boosted is null ? "" : $", {boosted} set to High priority")}.";
    }
}

/// <summary>
/// Per-game settings that persist across launches, stored where Windows reads them:
/// GPU preference (Settings › Display › Graphics) and CPU priority via Image File Execution Options.
/// </summary>
public sealed class GameProfiles
{
    private const string GpuPrefs = @"Software\Microsoft\DirectX\UserGpuPreferences";
    private const string Ifeo = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options";
    private readonly IRegistry _reg;
    private readonly ChangeLog _log;

    public GameProfiles(IRegistry reg, ChangeLog log)
    {
        _reg = reg;
        _log = log;
    }

    /// <summary>gpu: 0 = let Windows decide, 1 = power saving, 2 = high performance. priority: 2 normal, 6 above normal, 3 high.</summary>
    public void Set(string exePath, int gpu, uint cpuPriority)
    {
        _reg.SetValue(RegHive.CurrentUser, GpuPrefs, exePath, RegValue.Str($"GpuPreference={gpu};"));
        _reg.SetValue(RegHive.LocalMachine, $@"{Ifeo}\{Path.GetFileName(exePath)}\PerfOptions", "CpuPriorityClass", RegValue.DWord(cpuPriority));
        _log.Add("tool", $"Game profile for {Path.GetFileName(exePath)}", details: new[]
        {
            $@"HKCU\{GpuPrefs}\{exePath} = GpuPreference={gpu};",
            $@"HKLM\{Ifeo}\{Path.GetFileName(exePath)}\PerfOptions\CpuPriorityClass = {cpuPriority}",
        });
    }

    public void Remove(string exePath)
    {
        _reg.DeleteValue(RegHive.CurrentUser, GpuPrefs, exePath);
        var exeKey = $@"{Ifeo}\{Path.GetFileName(exePath)}";
        _reg.DeleteKeyTree(RegHive.LocalMachine, exeKey + @"\PerfOptions");
        if (_reg.IsTreeEmpty(RegHive.LocalMachine, exeKey)) _reg.DeleteKeyTree(RegHive.LocalMachine, exeKey);
        _log.Add("tool", $"Removed game profile for {Path.GetFileName(exePath)}");
    }

    public IReadOnlyList<(string Exe, int Gpu, uint Priority)> List()
    {
        using var b = Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.CurrentUser, Microsoft.Win32.RegistryView.Registry64);
        using var k = b.OpenSubKey(GpuPrefs);
        var names = k?.GetValueNames().Where(n => n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)).ToArray() ?? Array.Empty<string>();
        return names.Select(n =>
        {
            var gpu = Core.Changes.RegistryListChange.Parse(_reg.GetValue(RegHive.CurrentUser, GpuPrefs, n)?.Data as string)
                .TryGetValue("GpuPreference", out var g) && int.TryParse(g, out var gi) ? gi : 0;
            var prio = _reg.GetValue(RegHive.LocalMachine, $@"{Ifeo}\{Path.GetFileName(n)}\PerfOptions", "CpuPriorityClass")?.AsDWord ?? 2;
            return (n, gpu, prio);
        }).ToList();
    }
}

/// <summary>Weekly auto-clean through Task Scheduler. The task runs "frtune.exe --autoclean".</summary>
public sealed class ScheduledClean
{
    public const string TaskName = @"\frtune\Weekly clean";
    private readonly ICommandRunner _cmd;
    private readonly ChangeLog _log;

    public ScheduledClean(ICommandRunner cmd, ChangeLog log)
    {
        _cmd = cmd;
        _log = log;
    }

    public bool IsEnabled => _cmd.Run("schtasks.exe", $"/Query /TN \"{TaskName}\"").Success;

    public void Enable(string exePath)
    {
        _cmd.Run("schtasks.exe", $"/Create /TN \"{TaskName}\" /TR \"\\\"{exePath}\\\" --autoclean\" /SC WEEKLY /D SUN /ST 12:00 /RL HIGHEST /F")
            .EnsureSuccess("schtasks /Create");
        _log.Add("tool", "Enabled weekly auto-clean", details: new[] { $"Task Scheduler: {TaskName}, Sundays 12:00, runs {exePath} --autoclean" });
    }

    public void Disable()
    {
        _cmd.Run("schtasks.exe", $"/Delete /TN \"{TaskName}\" /F");
        _log.Add("tool", "Disabled weekly auto-clean");
    }
}
