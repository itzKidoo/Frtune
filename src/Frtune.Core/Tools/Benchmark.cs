using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace Frtune.Core.Tools;

public sealed record BenchmarkResult
{
    public DateTimeOffset When { get; init; } = DateTimeOffset.Now;
    public string Label { get; init; } = "";
    /// <summary>SHA-256 MB/s on one thread.</summary>
    public double CpuSingle { get; init; }
    /// <summary>SHA-256 MB/s on all threads.</summary>
    public double CpuMulti { get; init; }
    /// <summary>Memory copy bandwidth, GB/s.</summary>
    public double MemoryGbps { get; init; }
    /// <summary>Sequential write MB/s (write-through, so the OS cache doesn't inflate it).</summary>
    public double DiskWriteMBps { get; init; }
    /// <summary>Sequential read MB/s of the same file (may be partly cached).</summary>
    public double DiskReadMBps { get; init; }
}

/// <summary>
/// A quick, repeatable mini-benchmark for before/after comparisons on the same PC.
/// It is not comparable with other tools' scores.
/// </summary>
public static class Benchmark
{
    public static Task<BenchmarkResult> RunAsync(string label, string tempFolder, IProgress<string>? progress, CancellationToken ct) => Task.Run(() =>
    {
        progress?.Report("CPU (single thread)…");
        var single = HashThroughput(1, ct);
        progress?.Report("CPU (all threads)…");
        var multi = HashThroughput(Environment.ProcessorCount, ct);
        progress?.Report("Memory bandwidth…");
        var mem = MemoryBandwidth(ct);
        progress?.Report("Disk (256 MB sequential)…");
        var (w, r) = Disk(tempFolder, ct);
        return new BenchmarkResult { Label = label, CpuSingle = single, CpuMulti = multi, MemoryGbps = mem, DiskWriteMBps = w, DiskReadMBps = r };
    }, ct);

    private static double HashThroughput(int threads, CancellationToken ct)
    {
        const int chunk = 1 << 20;
        var duration = TimeSpan.FromSeconds(2);
        long total = 0;
        var sw = Stopwatch.StartNew();
        Parallel.For(0, threads, new ParallelOptions { MaxDegreeOfParallelism = threads, CancellationToken = ct }, _ =>
        {
            var buf = new byte[chunk];
            Random.Shared.NextBytes(buf);
            var hash = new byte[32];
            long local = 0;
            while (sw.Elapsed < duration)
            {
                SHA256.HashData(buf, hash);
                local += chunk;
            }
            Interlocked.Add(ref total, local);
        });
        return total / 1048576.0 / sw.Elapsed.TotalSeconds;
    }

    private static double MemoryBandwidth(CancellationToken ct)
    {
        const int size = 256 * 1024 * 1024;
        var a = new byte[size];
        var b = new byte[size];
        a.AsSpan().Fill(1);
        var sw = Stopwatch.StartNew();
        var copies = 0;
        while (sw.Elapsed < TimeSpan.FromSeconds(2))
        {
            ct.ThrowIfCancellationRequested();
            a.AsSpan().CopyTo(b);
            copies++;
        }
        // Each copy reads and writes `size` bytes.
        return copies * (double)size * 2 / 1e9 / sw.Elapsed.TotalSeconds;
    }

    private static (double Write, double Read) Disk(string folder, CancellationToken ct)
    {
        const int block = 4 * 1024 * 1024;
        const int blocks = 64; // 256 MB
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"frtune-bench-{Guid.NewGuid():N}.tmp");
        var buf = new byte[block];
        Random.Shared.NextBytes(buf);
        try
        {
            var sw = Stopwatch.StartNew();
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 1, FileOptions.WriteThrough))
            {
                for (var i = 0; i < blocks; i++) { ct.ThrowIfCancellationRequested(); fs.Write(buf); }
                fs.Flush(true);
            }
            var write = blocks * (double)block / 1048576 / sw.Elapsed.TotalSeconds;

            sw.Restart();
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None, 1, FileOptions.SequentialScan))
            {
                while (fs.Read(buf) > 0) ct.ThrowIfCancellationRequested();
            }
            var read = blocks * (double)block / 1048576 / sw.Elapsed.TotalSeconds;
            return (write, read);
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }
}

/// <summary>Benchmark history (benchmarks.json) for the before/after view.</summary>
public sealed class BenchmarkHistory
{
    private readonly string _path;
    public BenchmarkHistory(string path) => _path = path;

    public List<BenchmarkResult> Load() =>
        File.Exists(_path) ? JsonSerializer.Deserialize<List<BenchmarkResult>>(File.ReadAllText(_path)) ?? new() : new();

    public void Add(BenchmarkResult r)
    {
        var all = Load();
        all.Add(r);
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(all, new JsonSerializerOptions { WriteIndented = true }));
    }
}
