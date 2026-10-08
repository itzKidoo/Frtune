using Frtune.Core.Abstractions;

namespace Frtune.Core.Engine;

/// <summary>Detects whether Windows itself is waiting for a reboot (updates, servicing, pending file renames).</summary>
public static class RestartDetector
{
    public static IReadOnlyList<string> PendingReasons(IRegistry registry)
    {
        var reasons = new List<string>();
        if (registry.KeyExists(RegHive.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending"))
            reasons.Add("Windows servicing (component store) needs a restart");
        if (registry.KeyExists(RegHive.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired"))
            reasons.Add("Windows Update needs a restart");
        if (registry.GetValue(RegHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Session Manager", "PendingFileRenameOperations") is not null)
            reasons.Add("Files are waiting to be replaced at the next restart");
        return reasons;
    }
}
