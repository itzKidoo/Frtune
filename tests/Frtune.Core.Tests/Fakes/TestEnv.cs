using Frtune.Core.Engine;
using Frtune.Core.Model;
using Frtune.Core.Tweaks;

namespace Frtune.Core.Tests.Fakes;

/// <summary>A complete fake machine plus an engine wired to it.</summary>
public sealed class TestEnv : IDisposable
{
    public FakeRegistry Registry { get; } = new();
    public FakeServices Services { get; } = new();
    public FakeCommands Commands { get; } = new();
    public FakeRestorePoints RestorePoints { get; } = new();
    public FixedClock Clock { get; } = new();
    public string Dir { get; } = Path.Combine(Path.GetTempPath(), "frtune-tests-" + Guid.NewGuid().ToString("N"));
    public JsonBackupStore Backups { get; }
    public ChangeLog Log { get; }
    public TweakContext Context { get; }
    public TweakEngine Engine { get; }

    /// <summary>The PC from the issue: i3-9100F (4C/4T, no iGPU), GTX 1650 Super, 16 GB, desktop.</summary>
    public static SystemProfile UserPc => new()
    {
        Build = 19045,
        Edition = "Professional",
        CpuName = "Intel(R) Core(TM) i3-9100F CPU @ 3.60GHz",
        CpuCores = 4,
        CpuThreads = 4,
        Gpus = new[] { new GpuInfo("NVIDIA GeForce GTX 1650 SUPER", GpuVendor.Nvidia, 4, "31.0.15.0") },
        TotalRamGb = 16,
        SystemDriveIsSsd = true,
    };

    public TestEnv(SystemProfile? profile = null)
    {
        Backups = new JsonBackupStore(Dir);
        Log = new ChangeLog(Path.Combine(Dir, "changelog.json"));
        Context = new TweakContext(Registry, Services, Commands, profile ?? UserPc, Clock, new TweakSettings());
        Engine = new TweakEngine(Context, Backups, Log, RestorePoints);
    }

    public void Dispose()
    {
        try { Directory.Delete(Dir, true); } catch { }
    }
}
