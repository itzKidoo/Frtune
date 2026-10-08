using Frtune.Core.Abstractions;

namespace Frtune.Core.Tests.Fakes;

public sealed class FakeServices : IServiceManager
{
    public Dictionary<string, ServiceStartType> Start { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Stopped { get; } = new();

    public ServiceStartType? GetStartType(string name) => Start.TryGetValue(name, out var t) ? t : null;
    public void SetStartType(string name, ServiceStartType startType) => Start[name] = startType;
    public void TryStop(string name) => Stopped.Add(name);

    public IReadOnlyList<ServiceEntry> GetAll() =>
        Start.Select(kv => new ServiceEntry(kv.Key, kv.Key, "", kv.Value, "Stopped")).ToList();
}

/// <summary>Records commands; replies come from a handler so tests can simulate powercfg etc.</summary>
public sealed class FakeCommands : ICommandRunner
{
    public List<string> Calls { get; } = new();
    public Func<string, string, CommandResult> Handler { get; set; } = (_, _) => new CommandResult(0, "", "");

    public CommandResult Run(string fileName, string arguments, TimeSpan? timeout = null)
    {
        Calls.Add($"{fileName} {arguments}");
        return Handler(fileName, arguments);
    }
}

public sealed class FakeRestorePoints : IRestorePointService
{
    public RestorePointResult Result { get; set; } = new(true, false, "ok");
    public int Calls { get; private set; }

    public Task<RestorePointResult> CreateAsync(string description, CancellationToken ct = default)
    {
        Calls++;
        return Task.FromResult(Result);
    }
}

public sealed class FixedClock : IClock
{
    public DateTimeOffset Now { get; set; } = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);
}
