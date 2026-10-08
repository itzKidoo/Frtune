using System.Text;

namespace Frtune.Core.Abstractions;

public sealed record CommandResult(int ExitCode, string StdOut, string StdErr)
{
    public bool Success => ExitCode == 0;

    public CommandResult EnsureSuccess(string what)
    {
        if (!Success)
            throw new InvalidOperationException($"{what} failed (exit {ExitCode}): {(string.IsNullOrWhiteSpace(StdErr) ? StdOut : StdErr).Trim()}");
        return this;
    }
}

/// <summary>Runs external commands (powercfg, netsh, sc, schtasks, powershell). Always hidden, never a shell.</summary>
public interface ICommandRunner
{
    CommandResult Run(string fileName, string arguments, TimeSpan? timeout = null);
}

public static class CommandRunnerExtensions
{
    /// <summary>
    /// Runs a PowerShell script using -EncodedCommand so that quoting never breaks the script.
    /// </summary>
    public static CommandResult PowerShell(this ICommandRunner runner, string script, TimeSpan? timeout = null)
    {
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(
            "$ProgressPreference='SilentlyContinue';$ErrorActionPreference='Stop';" + script));
        return runner.Run("powershell.exe", $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encoded}", timeout);
    }
}
