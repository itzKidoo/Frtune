using Frtune.Core.Model;

namespace Frtune.Core.Tools;

public sealed record CleanResult(long BytesFreed, int FilesDeleted, int FilesSkipped);

/// <summary>Something the cleaner can measure and empty.</summary>
public interface ICleanTarget
{
    long Measure(CancellationToken ct);
    CleanResult Clean(IProgress<long>? bytesProgress, CancellationToken ct);
}

public sealed class CleanerCategory
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public TweakRisk Risk { get; init; } = TweakRisk.Safe;
    public bool DefaultSelected { get; init; } = true;
    /// <summary>Safe to run unattended from the weekly scheduled clean.</summary>
    public bool Scheduled { get; init; }
    public required ICleanTarget Target { get; init; }
}

/// <summary>
/// Deletes files under one or more folders. Path segments may contain '*' (e.g. Chrome profiles).
/// Files in use, read-only system files and reparse points (junctions) are skipped, never forced.
/// </summary>
public sealed class FolderTarget : ICleanTarget
{
    private readonly string[] _roots;
    private readonly string _pattern;
    private readonly TimeSpan _minAge;
    private readonly bool _deleteFolders;

    /// <param name="roots">Folders with environment variables already expanded. '*' allowed per segment.</param>
    /// <param name="pattern">File name pattern, e.g. "*" or "thumbcache_*.db".</param>
    /// <param name="minAge">Only delete files older than this (temp files in use by installers are usually recent).</param>
    public FolderTarget(IEnumerable<string> roots, string pattern = "*", TimeSpan? minAge = null, bool deleteEmptyFolders = true)
    {
        _roots = roots.ToArray();
        _pattern = pattern;
        _minAge = minAge ?? TimeSpan.Zero;
        _deleteFolders = deleteEmptyFolders;
    }

    public IReadOnlyList<string> Roots => _roots;

    public long Measure(CancellationToken ct) => Files(ct).Sum(f => SafeLength(f));

    public CleanResult Clean(IProgress<long>? bytesProgress, CancellationToken ct)
    {
        long freed = 0;
        int deleted = 0, skipped = 0;
        var cutoff = DateTime.UtcNow - _minAge;
        foreach (var f in Files(ct).ToList())
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var info = new FileInfo(f);
                if (_minAge > TimeSpan.Zero && info.LastWriteTimeUtc > cutoff) { skipped++; continue; }
                var len = info.Length;
                if (info.IsReadOnly) info.IsReadOnly = false;
                info.Delete();
                freed += len;
                deleted++;
                bytesProgress?.Report(freed);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                skipped++; // in use or protected: leave it alone
            }
        }
        if (_deleteFolders)
            foreach (var root in ExpandRoots())
                DeleteEmptySubfolders(root);
        return new CleanResult(freed, deleted, skipped);
    }

    private IEnumerable<string> Files(CancellationToken ct)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.System,
        };
        foreach (var root in ExpandRoots())
        {
            IEnumerable<string> files;
            try { files = Directory.EnumerateFiles(root, _pattern, options); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { continue; }
            foreach (var f in files)
            {
                ct.ThrowIfCancellationRequested();
                yield return f;
            }
        }
    }

    /// <summary>Expands '*' segments ("C:\Users\me\AppData\Local\Google\Chrome\User Data\*\Cache").</summary>
    public IEnumerable<string> ExpandRoots()
    {
        foreach (var root in _roots)
        {
            if (!root.Contains('*')) { if (Directory.Exists(root)) yield return root; continue; }
            var parts = root.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            IEnumerable<string> current = new[] { parts[0] + Path.DirectorySeparatorChar };
            if (parts[0].Length == 0) current = new[] { Path.DirectorySeparatorChar.ToString() };
            foreach (var part in parts.Skip(1).Where(p => p.Length > 0))
            {
                current = part.Contains('*')
                    ? current.SelectMany(dir => SafeDirs(dir, part))
                    : current.Select(dir => Path.Combine(dir, part)).Where(Directory.Exists);
            }
            foreach (var dir in current) yield return dir;
        }
    }

    private static IEnumerable<string> SafeDirs(string dir, string pattern)
    {
        try { return Directory.Exists(dir) ? Directory.GetDirectories(dir, pattern) : Array.Empty<string>(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return Array.Empty<string>(); }
    }

    private static void DeleteEmptySubfolders(string root)
    {
        try
        {
            foreach (var dir in Directory.GetDirectories(root))
            {
                if ((File.GetAttributes(dir) & FileAttributes.ReparsePoint) != 0) continue;
                DeleteEmptySubfolders(dir);
                if (!Directory.EnumerateFileSystemEntries(dir).Any()) Directory.Delete(dir);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    private static long SafeLength(string f)
    {
        try { return new FileInfo(f).Length; } catch { return 0; }
    }
}

/// <summary>A target backed by delegates, for things like the Recycle Bin that need OS APIs.</summary>
public sealed class DelegateTarget : ICleanTarget
{
    private readonly Func<CancellationToken, long> _measure;
    private readonly Func<IProgress<long>?, CancellationToken, CleanResult> _clean;

    public DelegateTarget(Func<CancellationToken, long> measure, Func<IProgress<long>?, CancellationToken, CleanResult> clean)
    {
        _measure = measure;
        _clean = clean;
    }

    public long Measure(CancellationToken ct) => _measure(ct);
    public CleanResult Clean(IProgress<long>? p, CancellationToken ct) => _clean(p, ct);
}

/// <summary>Built-in cleaning categories. The Windows layer adds the Recycle Bin, Update cache and Windows.old.</summary>
public static class CleanerCatalog
{
    public static List<CleanerCategory> FolderCategories(Func<string, string> expand)
    {
        string E(string p) => expand(p);
        string[] Browser(string userData) => new[] { "Cache", "Code Cache", "GPUCache" }
            .Select(c => E($@"{userData}\*\{c}")).ToArray();

        return new List<CleanerCategory>
        {
            new() { Id = "temp", Name = "Temporary files", Description = "%TEMP% and C:\\Windows\\Temp (files older than 24 hours).", Scheduled = true,
                Target = new FolderTarget(new[] { E("%TEMP%"), E(@"%SystemRoot%\Temp") }, minAge: TimeSpan.FromHours(24)) },
            new() { Id = "thumbs", Name = "Thumbnail cache", Description = "Explorer thumbnail databases. Rebuilt as you browse folders.",
                Target = new FolderTarget(new[] { E(@"%LocalAppData%\Microsoft\Windows\Explorer") }, "thumbcache_*.db", deleteEmptyFolders: false) },
            new() { Id = "dxcache", Name = "DirectX shader cache", Description = "Compiled shaders. Games rebuild them, so the first launch may stutter briefly.", Scheduled = false,
                Target = new FolderTarget(new[] { E(@"%LocalAppData%\D3DSCache") }) },
            new() { Id = "nvcache", Name = "NVIDIA shader cache", Description = "DXCache, GLCache and NV_Cache. Clear after a driver update if games stutter.", DefaultSelected = false,
                Target = new FolderTarget(new[] { E(@"%LocalAppData%\NVIDIA\DXCache"), E(@"%LocalAppData%\NVIDIA\GLCache"), E(@"%ProgramData%\NVIDIA Corporation\NV_Cache") }) },
            new() { Id = "amdcache", Name = "AMD shader cache", Description = "DxCache, DxcCache, GLCache and VkCache.", DefaultSelected = false,
                Target = new FolderTarget(new[] { E(@"%LocalAppData%\AMD\DxCache"), E(@"%LocalAppData%\AMD\DxcCache"), E(@"%LocalAppData%\AMD\GLCache"), E(@"%LocalAppData%\AMD\VkCache") }) },
            new() { Id = "chrome", Name = "Chrome cache", Description = "Cached web files for every Chrome profile. Logins and history are kept.", Scheduled = true,
                Target = new FolderTarget(Browser(@"%LocalAppData%\Google\Chrome\User Data")) },
            new() { Id = "edge", Name = "Edge cache", Description = "Cached web files for every Edge profile. Logins and history are kept.", Scheduled = true,
                Target = new FolderTarget(Browser(@"%LocalAppData%\Microsoft\Edge\User Data")) },
            new() { Id = "brave", Name = "Brave cache", Description = "Cached web files for every Brave profile.", Scheduled = true,
                Target = new FolderTarget(Browser(@"%LocalAppData%\BraveSoftware\Brave-Browser\User Data")) },
            new() { Id = "firefox", Name = "Firefox cache", Description = "cache2 folder of every Firefox profile.", Scheduled = true,
                Target = new FolderTarget(new[] { E(@"%LocalAppData%\Mozilla\Firefox\Profiles\*\cache2") }) },
            new() { Id = "dumps", Name = "Crash dumps", Description = "Minidumps, MEMORY.DMP and app crash dumps. Only useful if you're debugging a crash.", Scheduled = true,
                Target = new FolderTarget(new[] { E(@"%SystemRoot%\Minidump"), E(@"%LocalAppData%\CrashDumps"), E(@"%ProgramData%\Microsoft\Windows\WER\ReportArchive"), E(@"%ProgramData%\Microsoft\Windows\WER\ReportQueue") }) },
            new() { Id = "logs", Name = "Old log files", Description = "*.log files older than 7 days in C:\\Windows\\Logs and the setup log folders.", DefaultSelected = false,
                Target = new FolderTarget(new[] { E(@"%SystemRoot%\Logs\CBS"), E(@"%SystemRoot%\Logs\DISM"), E(@"%SystemRoot%\Panther") }, "*.log", TimeSpan.FromDays(7), deleteEmptyFolders: false) },
            new() { Id = "prefetch", Name = "Prefetch", Description = "Windows rebuilds it, and apps launch slower until it does. Only clean it when troubleshooting.", Risk = TweakRisk.Moderate, DefaultSelected = false,
                Target = new FolderTarget(new[] { E(@"%SystemRoot%\Prefetch") }, "*.pf") },
        };
    }

    public static string FormatBytes(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double v = bytes;
        var u = 0;
        while (v >= 1024 && u < units.Length - 1) { v /= 1024; u++; }
        return u == 0 ? $"{bytes} B" : $"{v:0.##} {units[u]}";
    }
}
