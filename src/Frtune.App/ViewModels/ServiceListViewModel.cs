using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Frtune.App.Services;
using Frtune.Core.Abstractions;

namespace Frtune.App.ViewModels;

public sealed partial class ServiceRow : ObservableObject
{
    private readonly Func<ServiceRow, ServiceStartType, ServiceStartType, Task> _changed;

    public ServiceRow(ServiceEntry e, string? recommendation, string? original, Func<ServiceRow, ServiceStartType, ServiceStartType, Task> changed)
    {
        Entry = e;
        Recommendation = recommendation;
        _startType = e.StartType;
        _original = original;
        _changed = changed;
    }

    /// <summary>Sets the start type shown without triggering a change (used for revert/after undo).</summary>
    public void Set(ServiceStartType t) => StartType = t;

    /// <summary>Segmented buttons call this; the row only changes after the user confirms.</summary>
    [RelayCommand]
    private Task Change(ServiceStartType to) => to == StartType ? Task.CompletedTask : _changed(this, StartType, to);

    public ServiceEntry Entry { get; }
    public string Name => Entry.Name;
    public string DisplayName => Entry.DisplayName;
    public string Description => Entry.Description;
    public string State => Entry.State;
    public string? Recommendation { get; }
    public bool IsKeep => Recommendation?.StartsWith("Keep") == true;
    [ObservableProperty] private ServiceStartType _startType;
    [ObservableProperty] private string? _original;
    public bool CanUndo => Original is not null;
    partial void OnOriginalChanged(string? value) => OnPropertyChanged(nameof(CanUndo));
}

/// <summary>Every service with its start type. Manual changes save the original start type for one-click undo.</summary>
public sealed partial class ServiceListViewModel : PageViewModel
{
    private readonly MainViewModel _main;
    private readonly string _originalsPath = Path.Combine(AppServices.DataDir, "backups", "services-original.json");
    private Dictionary<string, string> _originals = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Plain-language advice shown next to well-known services.</summary>
    public static readonly Dictionary<string, string> Advice = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Fax"] = "Safe to disable",
        ["RemoteRegistry"] = "Safe to disable",
        ["RetailDemo"] = "Safe to disable",
        ["MapsBroker"] = "Safe to disable",
        ["DiagTrack"] = "Telemetry: safe to disable",
        ["dmwappushservice"] = "Telemetry: safe to disable",
        ["Spooler"] = "Disable if you have no printer",
        ["bthserv"] = "Disable if you don't use Bluetooth",
        ["TabletInputService"] = "Disable on desktops without touch/pen",
        ["WSearch"] = "Optional: search is slower without it",
        ["SysMain"] = "Optional on an SSD",
        ["lfsvc"] = "Optional: location for apps",
        ["WerSvc"] = "Manual is fine",
        ["PhoneSvc"] = "Manual is fine",
        ["XblAuthManager"] = "Keep: needed for Xbox/Game Pass games",
        ["XblGameSave"] = "Keep: Xbox cloud saves",
        ["GamingServices"] = "Keep: Game Pass",
        ["WinDefend"] = "Keep: antivirus",
        ["mpssvc"] = "Keep: firewall",
        ["wuauserv"] = "Keep: Windows Update",
        ["BITS"] = "Keep: used by Windows Update",
        ["Dnscache"] = "Keep: DNS",
        ["Dhcp"] = "Keep: networking",
        ["AudioSrv"] = "Keep: sound",
        ["Audiosrv"] = "Keep: sound",
        ["RpcSs"] = "Keep: core Windows",
        ["DcomLaunch"] = "Keep: core Windows",
        ["EventLog"] = "Keep: core Windows",
        ["Schedule"] = "Keep: core Windows",
        ["Winmgmt"] = "Keep: core Windows",
        ["CryptSvc"] = "Keep: core Windows",
        ["nvagent"] = "Keep: NVIDIA",
        ["NVDisplay.ContainerLocalSystem"] = "Keep: NVIDIA control panel",
    };

    /// <summary>Disabling these breaks Windows or removes protection, so frtune refuses.</summary>
    private static readonly HashSet<string> NeverDisable = new(StringComparer.OrdinalIgnoreCase)
    {
        "RpcSs", "RpcEptMapper", "DcomLaunch", "LSM", "SamSs", "EventLog", "Schedule", "Winmgmt", "CryptSvc", "Power", "ProfSvc",
        "BrokerInfrastructure", "CoreMessagingRegistrar", "gpsvc", "WinDefend", "mpssvc", "BFE", "SecurityHealthService", "wscsvc",
        "AudioSrv", "Audiosrv", "AudioEndpointBuilder", "Dhcp", "Dnscache", "nsi", "UserManager", "StateRepository", "TrustedInstaller", "wuauserv",
    };

    public ServiceListViewModel(MainViewModel main) : base("All services", "")
    {
        _main = main;
        View = CollectionViewSource.GetDefaultView(Rows);
        View.Filter = o => o is ServiceRow r && (Filter.Length == 0 ||
            r.Name.Contains(Filter, StringComparison.OrdinalIgnoreCase) || r.DisplayName.Contains(Filter, StringComparison.OrdinalIgnoreCase));
    }

    public ObservableCollection<ServiceRow> Rows { get; } = new();
    public ICollectionView View { get; }
    public static IReadOnlyList<ServiceStartType> StartTypes { get; } = new[]
        { ServiceStartType.Automatic, ServiceStartType.AutomaticDelayed, ServiceStartType.Manual, ServiceStartType.Disabled };

    [ObservableProperty] private string _filter = "";
    [ObservableProperty] private bool _loading;
    [ObservableProperty] private bool _expanded;
    partial void OnFilterChanged(string value) => View.Refresh();

    partial void OnExpandedChanged(bool value)
    {
        if (value && Rows.Count == 0) _ = RefreshAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        Loading = true;
        if (File.Exists(_originalsPath))
            _originals = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(_originalsPath)) ?? new();
        _originals = new Dictionary<string, string>(_originals, StringComparer.OrdinalIgnoreCase);
        var all = await Task.Run(() => _main.S.Services.GetAll());
        Rows.Clear();
        foreach (var e in all) Rows.Add(new ServiceRow(e, Advice.GetValueOrDefault(e.Name), _originals.GetValueOrDefault(e.Name), OnRowChangedAsync));
        Loading = false;
    }

    private async Task OnRowChangedAsync(ServiceRow row, ServiceStartType from, ServiceStartType to)
    {
        if (to == ServiceStartType.Disabled && NeverDisable.Contains(row.Name))
        {
            _main.Dialogs.Info("Not allowed", $"{row.DisplayName} is essential to Windows or its security, so frtune won't disable it.");
            return;
        }
        if (!_main.Dialogs.Ask("Change service", $"Set {row.DisplayName} ({row.Name}) to {to}?\n\nCurrent: {from}. The original start type is saved and can be restored with Undo.", "Change"))
            return;
        await Change(row, to);
    }

    [RelayCommand]
    private async Task UndoAsync(ServiceRow row)
    {
        if (row.Original is null) return;
        await Change(row, Enum.Parse<ServiceStartType>(row.Original), undo: true);
    }

    private async Task<bool> Change(ServiceRow row, ServiceStartType type, bool undo = false)
    {
        try
        {
            var before = _main.S.Services.GetStartType(row.Name);
            if (!undo && !_originals.ContainsKey(row.Name) && before is { } b)
            {
                _originals[row.Name] = b.ToString();
                Directory.CreateDirectory(Path.GetDirectoryName(_originalsPath)!);
                File.WriteAllText(_originalsPath, JsonSerializer.Serialize(_originals));
            }
            await Task.Run(() =>
            {
                _main.S.Services.SetStartType(row.Name, type);
                if (type == ServiceStartType.Disabled) _main.S.Services.TryStop(row.Name);
            });
            if (undo)
            {
                _originals.Remove(row.Name);
                File.WriteAllText(_originalsPath, JsonSerializer.Serialize(_originals));
            }
            _main.S.Log.Add("service", $"{row.Name}: {before} → {type}", details: new[] { $"sc config {row.Name} start= {type}" });
            row.Set(type);
            row.Original = _originals.GetValueOrDefault(row.Name);
            _main.S.Toasts.Show(ToastKind.Success, $"{row.DisplayName}: {type}");
            return true;
        }
        catch (Exception ex)
        {
            _main.S.Toasts.Show(ToastKind.Error, "Couldn't change service", ex.Message);
            return false;
        }
    }
}
