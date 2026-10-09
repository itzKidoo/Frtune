using System.Management;
using System.ServiceProcess;
using Frtune.Core.Abstractions;

namespace Frtune.Windows.Platform;

public sealed class WindowsServiceManager : IServiceManager
{
    private readonly IRegistry _registry;
    private readonly ICommandRunner _commands;

    public WindowsServiceManager(IRegistry registry, ICommandRunner commands)
    {
        _registry = registry;
        _commands = commands;
    }

    private static string Key(string name) => $@"SYSTEM\CurrentControlSet\Services\{name}";

    public ServiceStartType? GetStartType(string name)
    {
        var start = _registry.GetValue(RegHive.LocalMachine, Key(name), "Start");
        if (start is null) return null;
        var value = (int)start.AsDWord;
        if (value == 2 && _registry.GetValue(RegHive.LocalMachine, Key(name), "DelayedAutostart")?.AsDWord == 1)
            return ServiceStartType.AutomaticDelayed;
        return (ServiceStartType)value;
    }

    public void SetStartType(string name, ServiceStartType startType)
    {
        var arg = startType switch
        {
            ServiceStartType.Automatic => "auto",
            ServiceStartType.AutomaticDelayed => "delayed-auto",
            ServiceStartType.Manual => "demand",
            ServiceStartType.Disabled => "disabled",
            ServiceStartType.System => "system",
            ServiceStartType.Boot => "boot",
            _ => throw new ArgumentOutOfRangeException(nameof(startType)),
        };
        _commands.Run("sc.exe", $"config \"{name}\" start= {arg}").EnsureSuccess($"sc config {name}");
    }

    public void TryStop(string name)
    {
        try
        {
            using var sc = new ServiceController(name);
            if (sc.Status is ServiceControllerStatus.Running or ServiceControllerStatus.StartPending && sc.CanStop)
            {
                sc.Stop();
                sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));
            }
        }
        catch { /* best effort: the start type change is what persists */ }
    }

    public void Start(string name)
    {
        using var sc = new ServiceController(name);
        if (sc.Status == ServiceControllerStatus.Stopped) sc.Start();
    }

    public IReadOnlyList<ServiceEntry> GetAll()
    {
        var list = new List<ServiceEntry>();
        using var searcher = new ManagementObjectSearcher("SELECT Name, DisplayName, Description, State FROM Win32_Service");
        foreach (ManagementObject mo in searcher.Get())
        {
            using (mo)
            {
                var name = (string)mo["Name"];
                list.Add(new ServiceEntry(
                    name,
                    mo["DisplayName"] as string ?? name,
                    mo["Description"] as string ?? "",
                    GetStartType(name) ?? ServiceStartType.Manual,
                    mo["State"] as string ?? "Unknown"));
            }
        }
        return list.OrderBy(s => s.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList();
    }
}
