using System.Security.Cryptography;

namespace Frtune.Core.Tools;

public sealed record FoundFile(string Path, long Size, DateTime Modified);

public sealed record DuplicateGroup(string Hash, long Size, IReadOnlyList<string> Paths)
{
    public long Wasted => Size * (Paths.Count - 1);
}

/// <summary>Read-only scanners. They never delete anything; the UI asks per file.</summary>
public static class FileFinder
{
    private static readonly EnumerationOptions Options = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint,
    };

    public static IReadOnlyList<FoundFile> LargestFiles(string root, int count, long minSize, CancellationToken ct)
    {
        var heap = new PriorityQueue<FoundFile, long>();
        foreach (var path in Directory.EnumerateFiles(root, "*", Options))
        {
            ct.ThrowIfCancellationRequested();
            FileInfo fi;
            try { fi = new FileInfo(path); if (fi.Length < minSize) continue; }
            catch { continue; }
            heap.Enqueue(new FoundFile(path, fi.Length, fi.LastWriteTime), fi.Length);
            if (heap.Count > count) heap.Dequeue();
        }
        var list = new List<FoundFile>();
        while (heap.Count > 0) list.Add(heap.Dequeue());
        list.Reverse();
        return list;
    }

    /// <summary>Groups by size first, then by SHA-256, so only same-sized files are hashed.</summary>
    public static IReadOnlyList<DuplicateGroup> Duplicates(string root, long minSize, IProgress<string>? progress, CancellationToken ct)
    {
        var bySize = new Dictionary<long, List<string>>();
        foreach (var path in Directory.EnumerateFiles(root, "*", Options))
        {
            ct.ThrowIfCancellationRequested();
            long len;
            try { len = new FileInfo(path).Length; } catch { continue; }
            if (len < minSize) continue;
            if (!bySize.TryGetValue(len, out var l)) bySize[len] = l = new();
            l.Add(path);
        }

        var groups = new List<DuplicateGroup>();
        foreach (var (size, paths) in bySize.Where(kv => kv.Value.Count > 1))
        {
            var byHash = new Dictionary<string, List<string>>();
            foreach (var p in paths)
            {
                ct.ThrowIfCancellationRequested();
                progress?.Report(p);
                string hash;
                try
                {
                    using var s = File.OpenRead(p);
                    hash = Convert.ToHexString(SHA256.HashData(s));
                }
                catch { continue; }
                if (!byHash.TryGetValue(hash, out var l)) byHash[hash] = l = new();
                l.Add(p);
            }
            groups.AddRange(byHash.Where(kv => kv.Value.Count > 1).Select(kv => new DuplicateGroup(kv.Key, size, kv.Value)));
        }
        return groups.OrderByDescending(g => g.Wasted).ToList();
    }
}
