using Frtune.Core.Abstractions;
using Frtune.Core.Tweaks;

namespace Frtune.Core.Changes;

/// <summary>Sets a service's startup type. A service that isn't installed is skipped and reported as applied.</summary>
public sealed class ServiceChange : IChange
{
    public string ServiceName { get; }
    public ServiceStartType StartType { get; }
    public bool StopWhenDisabled { get; }

    public ServiceChange(string serviceName, ServiceStartType startType, bool stopWhenDisabled = true)
    {
        ServiceName = serviceName;
        StartType = startType;
        StopWhenDisabled = stopWhenDisabled;
    }

    public string Key => $"svc:{ServiceName}".ToLowerInvariant();
    public ChangeKind Kind => ChangeKind.Service;
    public string Target => ServiceName;
    public string Describe() => $"Service {ServiceName}: startup type → {StartType}";

    public string? Capture(TweakContext ctx) => ctx.Services.GetStartType(ServiceName)?.ToString();

    public void Apply(TweakContext ctx)
    {
        if (ctx.Services.GetStartType(ServiceName) is null) return;
        ctx.Services.SetStartType(ServiceName, StartType);
        if (StartType == ServiceStartType.Disabled && StopWhenDisabled) ctx.Services.TryStop(ServiceName);
    }

    public void Restore(TweakContext ctx, string? captured)
    {
        if (captured is null || ctx.Services.GetStartType(ServiceName) is null) return;
        ctx.Services.SetStartType(ServiceName, Enum.Parse<ServiceStartType>(captured));
    }

    public bool IsApplied(TweakContext ctx)
    {
        var current = ctx.Services.GetStartType(ServiceName);
        return current is null || current == StartType;
    }
}
