namespace Frtune.Core.Abstractions;

public enum ServiceStartType
{
    Boot = 0,
    System = 1,
    Automatic = 2,
    Manual = 3,
    Disabled = 4,
    AutomaticDelayed = 5,
}

public sealed record ServiceEntry(
    string Name,
    string DisplayName,
    string Description,
    ServiceStartType StartType,
    string State);

public interface IServiceManager
{
    /// <summary>Null when the service is not installed.</summary>
    ServiceStartType? GetStartType(string name);

    void SetStartType(string name, ServiceStartType startType);

    /// <summary>Best-effort stop, used after disabling a service.</summary>
    void TryStop(string name);

    IReadOnlyList<ServiceEntry> GetAll();
}
