using System.Runtime.InteropServices;
using Frtune.Core.Abstractions;
using Frtune.Core.Model;
using Frtune.Core.Tools;

namespace Frtune.Windows.Tools;

/// <summary>Cleaner categories that need Windows APIs or services.</summary>
public static class WindowsCleanTargets
{
    public static List<CleanerCategory> All(ICommandRunner cmd, IServiceManager services)
    {
        var list = CleanerCatalog.FolderCategories(Environment.ExpandEnvironmentVariables);
        list.Add(new CleanerCategory
        {
            Id = "recyclebin", Name = "Recycle Bin", Description = "Permanently deletes everything in the Recycle Bin on all drives.",
            Target = new DelegateTarget(_ => RecycleBinSize(), (_, _) => EmptyRecycleBin()),
        });
        var wu = new FolderTarget(new[] { Environment.ExpandEnvironmentVariables(@"%SystemRoot%\SoftwareDistribution\Download") });
        list.Add(new CleanerCategory
        {
            Id = "wucache", Name = "Windows Update cache", Description = "Already-installed update downloads. Stops the Windows Update service while cleaning and starts it again afterwards.",
            Target = new DelegateTarget(wu.Measure, (p, ct) =>
            {
                cmd.Run("net.exe", "stop wuauserv");
                cmd.Run("net.exe", "stop bits");
                try { return wu.Clean(p, ct); }
                finally
                {
                    cmd.Run("net.exe", "start bits");
                    cmd.Run("net.exe", "start wuauserv");
                }
            }),
        });
        var winOld = Path.Combine(Environment.GetEnvironmentVariable("SystemDrive") + "\\", "Windows.old");
        list.Add(new CleanerCategory
        {
            Id = "windowsold", Name = "Previous Windows installation (Windows.old)", Risk = TweakRisk.Moderate, DefaultSelected = false,
            Description = "Left behind after a feature update. After deleting it you can no longer roll back to the previous Windows version.",
            Target = new DelegateTarget(ct => Directory.Exists(winOld) ? new FolderTarget(new[] { winOld }).Measure(ct) : 0, (p, ct) =>
            {
                if (!Directory.Exists(winOld)) return new CleanResult(0, 0, 0);
                var size = new FolderTarget(new[] { winOld }).Measure(ct);
                cmd.Run("takeown.exe", $"/F \"{winOld}\" /R /A /D Y", TimeSpan.FromMinutes(20));
                cmd.Run("icacls.exe", $"\"{winOld}\" /grant *S-1-5-32-544:F /T /C /Q", TimeSpan.FromMinutes(20));
                cmd.Run("cmd.exe", $"/c rd /s /q \"{winOld}\"", TimeSpan.FromMinutes(20));
                return Directory.Exists(winOld) ? new CleanResult(size - new FolderTarget(new[] { winOld }).Measure(ct), 0, 1) : new CleanResult(size, 1, 0);
            }),
        });
        return list;
    }

    private static long RecycleBinSize()
    {
        var info = new ShQueryRbInfo { cbSize = Marshal.SizeOf<ShQueryRbInfo>() };
        return SHQueryRecycleBin(null, ref info) == 0 ? info.i64Size : 0;
    }

    private static CleanResult EmptyRecycleBin()
    {
        var info = new ShQueryRbInfo { cbSize = Marshal.SizeOf<ShQueryRbInfo>() };
        SHQueryRecycleBin(null, ref info);
        const uint noConfirm = 0x1, noProgress = 0x2, noSound = 0x4;
        var hr = SHEmptyRecycleBin(IntPtr.Zero, null, noConfirm | noProgress | noSound);
        return hr == 0 || info.i64NumItems == 0 ? new CleanResult(info.i64Size, (int)info.i64NumItems, 0) : new CleanResult(0, 0, (int)info.i64NumItems);
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct ShQueryRbInfo
    {
        public int cbSize;
        public long i64Size;
        public long i64NumItems;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHQueryRecycleBin(string? pszRootPath, ref ShQueryRbInfo pSHQueryRBInfo);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);
}
