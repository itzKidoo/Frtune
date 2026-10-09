using System.IO;
using Frtune.Core.Abstractions;
using Frtune.Core.Engine;
using Frtune.Core.Model;
using Frtune.Core.Tools;
using Frtune.Core.Tweaks;
using Frtune.Core.Tweaks.Catalog;
using Frtune.Windows.Info;
using Frtune.Windows.Platform;
using Frtune.Windows.Tools;

namespace Frtune.App.Services;

/// <summary>Composition root: creates every service once. View models receive this object.</summary>
public sealed class AppServices
{
    public static string DataDir { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "frtune");
    public static string SettingsPath => Path.Combine(DataDir, "settings.json");

    public AppSettings Settings { get; }
    public IRegistry Registry { get; } = new WindowsRegistry();
    public ProcessCommandRunner Commands { get; } = ProcessCommandRunner.Instance;
    public WindowsServiceManager Services { get; }
    public SystemInfoProvider SystemInfo { get; }
    public SystemProfile Profile { get; }
    public ChangeLog Log { get; }
    public JsonBackupStore Backups { get; }
    public TweakEngine Engine { get; }
    public IReadOnlyList<ITweak> Catalog { get; } = TweakCatalog.All;
    public ToastService Toasts { get; } = new();
    public NetworkTools Network { get; }
    public AppxService Appx { get; }
    public StartupManager Startup { get; }
    public ProcessTools Processes { get; }
    public GameProfiles Games { get; }
    public ScheduledClean ScheduledClean { get; }
    public BenchmarkHistory Benchmarks { get; }

    /// <summary>Highest restart level needed by changes made in this session.</summary>
    public RestartKind PendingRestart { get; set; }

    public AppServices()
    {
        Directory.CreateDirectory(DataDir);
        Settings = AppSettings.Load(SettingsPath);
        Services = new WindowsServiceManager(Registry, Commands);
        SystemInfo = new SystemInfoProvider(Registry);
        Profile = SystemInfo.Detect();
        Log = new ChangeLog(Path.Combine(DataDir, "changelog.json"));
        Backups = new JsonBackupStore(Path.Combine(DataDir, "backups"));
        Engine = new TweakEngine(MakeContext(), Backups, Log, new RestorePointService(Commands),
            new RegExportBackupExporter(Path.Combine(DataDir, "backups", "raw"), Commands));
        Network = new NetworkTools(Commands, Log, DataDir);
        Appx = new AppxService(Commands, Log);
        Startup = new StartupManager(Registry, Commands, Log);
        Processes = new ProcessTools(Log);
        Games = new GameProfiles(Registry, Log);
        ScheduledClean = new ScheduledClean(Commands, Log);
        Benchmarks = new BenchmarkHistory(Path.Combine(DataDir, "benchmarks.json"));
    }

    /// <summary>Rebuilds the tweak context after settings change (e.g. update pause days).</summary>
    public void RefreshContext() => Engine.Context = MakeContext();

    private TweakContext MakeContext() => new(Registry, Services, Commands, Profile, SystemClock.Instance,
        new TweakSettings { UpdatePauseDays = Settings.UpdatePauseDays });

    public List<CleanerCategory> CleanerCategories() => WindowsCleanTargets.All(Commands, Services);

    public void SaveSettings() => Settings.Save(SettingsPath);
}
