using System.Diagnostics;
using System.Text;
using Frtune.Core.Abstractions;

namespace Frtune.Windows.Platform;

/// <summary>Runs a program hidden (no console window) and captures its output. Never goes through a shell.</summary>
public sealed class ProcessCommandRunner : ICommandRunner
{
    public static readonly ProcessCommandRunner Instance = new();

    /// <summary>Raised for every command, so the UI can show exactly what was executed.</summary>
    public event Action<string>? CommandExecuted;

    public CommandResult Run(string fileName, string arguments, TimeSpan? timeout = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = ResolveSystemTool(fileName),
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        CommandExecuted?.Invoke(fileName.Equals("powershell.exe", StringComparison.OrdinalIgnoreCase) ? "powershell (script)" : $"{fileName} {arguments}");
        using var p = Process.Start(psi) ?? throw new InvalidOperationException($"Could not start {fileName}");
        var stdout = p.StandardOutput.ReadToEndAsync();
        var stderr = p.StandardError.ReadToEndAsync();
        if (!p.WaitForExit((int)(timeout ?? TimeSpan.FromMinutes(2)).TotalMilliseconds))
        {
            try { p.Kill(entireProcessTree: true); } catch { }
            return new CommandResult(-1, stdout.IsCompleted ? stdout.Result : "", "Timed out");
        }
        p.WaitForExit();
        return new CommandResult(p.ExitCode, stdout.Result, stderr.Result);
    }

    /// <summary>Always use the real System32 tool (never one found earlier on PATH or in the working folder).</summary>
    private static string ResolveSystemTool(string fileName)
    {
        if (Path.IsPathRooted(fileName)) return fileName;
        var sys = Environment.GetFolderPath(Environment.SpecialFolder.System);
        if (fileName.Equals("powershell.exe", StringComparison.OrdinalIgnoreCase))
            return Path.Combine(sys, @"WindowsPowerShell\v1.0\powershell.exe");
        var candidate = Path.Combine(sys, fileName);
        return File.Exists(candidate) ? candidate : fileName;
    }
}
