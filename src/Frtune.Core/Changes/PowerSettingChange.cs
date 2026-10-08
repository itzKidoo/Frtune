using System.Globalization;
using System.Text.RegularExpressions;
using Frtune.Core.Abstractions;
using Frtune.Core.Tweaks;

namespace Frtune.Core.Changes;

/// <summary>
/// Changes one setting of the active power plan through powercfg. The backup records which plan
/// was active, so revert writes the old value back into the same plan even if you switched plans since.
/// </summary>
public sealed partial class PowerSettingChange : IChange
{
    public string Subgroup { get; }
    public string Setting { get; }
    public uint AcValue { get; }
    public uint? DcValue { get; }
    public string Label { get; }

    public PowerSettingChange(string subgroup, string setting, string label, uint acValue, uint? dcValue = null)
    {
        Subgroup = subgroup;
        Setting = setting;
        Label = label;
        AcValue = acValue;
        DcValue = dcValue;
    }

    public string Key => $"power:{Subgroup}/{Setting}".ToLowerInvariant();
    public ChangeKind Kind => ChangeKind.PowerSetting;
    public string Target => $"powercfg {Subgroup} {Setting}";
    public string Describe() =>
        $"Active power plan: {Label} → {AcValue} (plugged in){(DcValue is { } dc ? $", {dc} (battery)" : "")}  [powercfg /setacvalueindex SCHEME_CURRENT {Subgroup} {Setting} {AcValue}]";

    public string? Capture(TweakContext ctx)
    {
        var scheme = ActiveScheme(ctx);
        var (ac, dc) = Query(ctx, scheme);
        return $"{scheme}|{ac}|{dc}";
    }

    public void Apply(TweakContext ctx)
    {
        var scheme = ActiveScheme(ctx);
        Set(ctx, scheme, AcValue, DcValue);
    }

    public void Restore(TweakContext ctx, string? captured)
    {
        if (captured is null) return;
        var parts = captured.Split('|');
        Set(ctx, parts[0], uint.Parse(parts[1], CultureInfo.InvariantCulture), uint.Parse(parts[2], CultureInfo.InvariantCulture));
    }

    public bool IsApplied(TweakContext ctx)
    {
        var (ac, dc) = Query(ctx, ActiveScheme(ctx));
        return ac == AcValue && (DcValue is null || dc == DcValue);
    }

    private void Set(TweakContext ctx, string scheme, uint ac, uint? dc)
    {
        ctx.Commands.Run("powercfg.exe", $"/setacvalueindex {scheme} {Subgroup} {Setting} {ac}").EnsureSuccess("powercfg");
        if (dc is { } d)
            ctx.Commands.Run("powercfg.exe", $"/setdcvalueindex {scheme} {Subgroup} {Setting} {d}").EnsureSuccess("powercfg");
        ctx.Commands.Run("powercfg.exe", $"/setactive {ActiveScheme(ctx)}");
    }

    public static string ActiveScheme(TweakContext ctx)
    {
        var r = ctx.Commands.Run("powercfg.exe", "/getactivescheme").EnsureSuccess("powercfg /getactivescheme");
        var m = GuidRegex().Match(r.StdOut);
        if (!m.Success) throw new InvalidOperationException("Could not read the active power plan.");
        return m.Value;
    }

    /// <summary>
    /// powercfg output is localised, so instead of matching labels we take the last two hex
    /// numbers, which are always the current AC and DC index.
    /// </summary>
    public static (uint Ac, uint Dc) ParseQuery(string output)
    {
        var hex = HexRegex().Matches(output).Select(m => uint.Parse(m.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToList();
        if (hex.Count < 2) throw new InvalidOperationException("Unexpected powercfg output.");
        return (hex[^2], hex[^1]);
    }

    private (uint, uint) Query(TweakContext ctx, string scheme) =>
        ParseQuery(ctx.Commands.Run("powercfg.exe", $"/query {scheme} {Subgroup} {Setting}").EnsureSuccess("powercfg /query").StdOut);

    [GeneratedRegex(@"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")]
    private static partial Regex GuidRegex();

    [GeneratedRegex(@"0x([0-9a-fA-F]{8})")]
    private static partial Regex HexRegex();
}

/// <summary>Well-known power setting GUIDs (from powercfg /qh).</summary>
public static class PowerGuids
{
    public const string SubProcessor = "54533251-82be-4824-96c1-47b60b740d00";
    public const string CoreParkingMinCores = "0cc5b647-c1df-4637-891a-dec35c318583";
    public const string ProcThrottleMin = "893dee8e-2bef-41e0-89c6-b55d0929964c";
    public const string ProcThrottleMax = "bc5038f7-23e0-4960-96da-33abaf5935ec";
    public const string PerfBoostMode = "be337238-0d82-4146-a960-4f3749d470c7";
    public const string PerfEpp = "36687f9e-e3a5-4dbf-b1dc-15eb381c6863";
    public const string SubDisk = "0012ee47-9041-4b5d-9b77-535fba8b1442";
    public const string DiskIdle = "6738e2c4-e8a5-4a42-b16a-e040e769756e";
    public const string SubUsb = "2a737441-1930-4402-8d77-b2bebba308a3";
    public const string UsbSelectiveSuspend = "48e6b7a6-50f5-4782-a5d4-53bb8f07e226";
    public const string SubPciExpress = "501a4d13-42af-4429-9fd1-a8218c268e20";
    public const string PcieAspm = "ee12f906-d277-404b-b6da-e5fa1a576df5";
    public const string UltimatePerformanceTemplate = "e9a42b02-d5df-448d-aa00-03f14749eb61";
    public const string HighPerformance = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
    public const string Balanced = "381b4222-f694-41f0-9685-ff5bb260df2e";
}
