using System.Diagnostics;

namespace Frtune.Windows.Info;

public sealed record UsageSample(double Cpu, double Gpu, double Ram, double Disk, double RamUsedGb, double RamTotalGb);

/// <summary>
/// Samples CPU, GPU (3D engine), RAM and disk activity. Read once per second from a background
/// timer in the UI. Counters that aren't available (e.g. GPU on old drivers) report 0.
/// </summary>
public sealed class PerformanceMonitor : IDisposable
{
    private readonly PerformanceCounter? _cpu;
    private readonly PerformanceCounter? _disk;
    private List<PerformanceCounter> _gpu = new();
    private DateTime _gpuRefreshed = DateTime.MinValue;

    public PerformanceMonitor()
    {
        _cpu = TryCreate("Processor Information", "% Processor Utility", "_Total") ?? TryCreate("Processor", "% Processor Time", "_Total");
        _disk = TryCreate("PhysicalDisk", "% Idle Time", "_Total");
        _cpu?.NextValue();
        _disk?.NextValue();
    }

    public UsageSample Sample()
    {
        var cpu = Math.Clamp(Safe(_cpu), 0, 100);
        var disk = _disk is null ? 0 : Math.Clamp(100 - Safe(_disk), 0, 100);
        var (total, avail, _) = SystemInfoProvider.MemoryStatus();
        var ram = total == 0 ? 0 : 100.0 * (total - avail) / total;
        return new UsageSample(cpu, GpuUsage(), ram, disk, (total - avail) / 1073741824.0, total / 1073741824.0);
    }

    /// <summary>Sum of all "engtype_3D" engine instances, like Task Manager's GPU 3D graph.</summary>
    private double GpuUsage()
    {
        try
        {
            // Instances appear and disappear with processes; refresh the list every 10 s.
            if (DateTime.UtcNow - _gpuRefreshed > TimeSpan.FromSeconds(10))
            {
                foreach (var c in _gpu) c.Dispose();
                var cat = new PerformanceCounterCategory("GPU Engine");
                _gpu = cat.GetInstanceNames().Where(n => n.EndsWith("engtype_3D", StringComparison.OrdinalIgnoreCase))
                    .Select(n => new PerformanceCounter("GPU Engine", "Utilization Percentage", n, true)).ToList();
                foreach (var c in _gpu) c.NextValue();
                _gpuRefreshed = DateTime.UtcNow;
                return 0;
            }
            return Math.Clamp(_gpu.Sum(c => { try { return c.NextValue(); } catch { return 0f; } }), 0, 100);
        }
        catch { return 0; }
    }

    private static PerformanceCounter? TryCreate(string cat, string counter, string instance)
    {
        try { return new PerformanceCounter(cat, counter, instance, true); }
        catch { return null; }
    }

    private static double Safe(PerformanceCounter? c)
    {
        try { return c?.NextValue() ?? 0; } catch { return 0; }
    }

    public void Dispose()
    {
        _cpu?.Dispose();
        _disk?.Dispose();
        foreach (var c in _gpu) c.Dispose();
    }
}
