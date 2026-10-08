using Frtune.Core.Abstractions;
using Frtune.Core.Tweaks;

namespace Frtune.Core.Changes;

/// <summary>Enables or disables a scheduled task. Missing tasks are skipped.</summary>
public sealed class ScheduledTaskChange : IChange
{
    public string TaskPath { get; }
    public bool Enable { get; }

    public ScheduledTaskChange(string taskPath, bool enable)
    {
        TaskPath = taskPath;
        Enable = enable;
    }

    public string Key => $"task:{TaskPath}".ToLowerInvariant();
    public ChangeKind Kind => ChangeKind.ScheduledTask;
    public string Target => TaskPath;
    public string Describe() => $"Scheduled task {TaskPath} → {(Enable ? "enabled" : "disabled")}";

    public string? Capture(TweakContext ctx) => Query(ctx);

    public void Apply(TweakContext ctx) => Set(ctx, Enable);

    public void Restore(TweakContext ctx, string? captured)
    {
        if (captured is null) return;
        Set(ctx, captured == "enabled");
    }

    public bool IsApplied(TweakContext ctx)
    {
        var state = Query(ctx);
        return state is null || state == (Enable ? "enabled" : "disabled");
    }

    private void Set(TweakContext ctx, bool enable)
    {
        if (Query(ctx) is null) return;
        ctx.Commands.Run("schtasks.exe", $"/Change /TN \"{TaskPath}\" {(enable ? "/ENABLE" : "/DISABLE")}")
            .EnsureSuccess($"schtasks {TaskPath}");
    }

    /// <summary>"enabled", "disabled", or null when the task doesn't exist. Uses the XML export, which isn't localised.</summary>
    private string? Query(TweakContext ctx)
    {
        var r = ctx.Commands.Run("schtasks.exe", $"/Query /TN \"{TaskPath}\" /XML");
        if (!r.Success) return null;
        return r.StdOut.Contains("<Enabled>false</Enabled>", StringComparison.OrdinalIgnoreCase) ? "disabled" : "enabled";
    }
}
