using Frtune.Core.Abstractions;
using Frtune.Core.Changes;

namespace Frtune.Core.Tweaks.Catalog;

/// <summary>Reusable change factories shared by several catalog files.</summary>
internal static class Helpers
{
    public const string TcpipInterfaces = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces";
    public const string NetAdapterClass = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}";

    /// <summary>TCP/IP interface GUIDs that currently have an IPv4 address (i.e. the adapters actually in use).</summary>
    public static IEnumerable<string> ActiveInterfaces(TweakContext ctx)
    {
        foreach (var guid in ctx.Registry.GetSubKeyNames(RegHive.LocalMachine, TcpipInterfaces))
        {
            var key = $@"{TcpipInterfaces}\{guid}";
            var dhcp = ctx.Registry.GetValue(RegHive.LocalMachine, key, "DhcpIPAddress")?.Data as string;
            var statics = ctx.Registry.GetValue(RegHive.LocalMachine, key, "IPAddress")?.Data as string[];
            bool Valid(string? ip) => !string.IsNullOrWhiteSpace(ip) && ip != "0.0.0.0";
            if (Valid(dhcp) || (statics?.Any(Valid) ?? false)) yield return guid;
        }
    }

    /// <summary>Driver keys of physical network adapters (Characteristics has NCF_PHYSICAL = 0x4).</summary>
    public static IEnumerable<string> PhysicalAdapterKeys(TweakContext ctx)
    {
        foreach (var sub in ctx.Registry.GetSubKeyNames(RegHive.LocalMachine, NetAdapterClass))
        {
            if (sub.Length != 4 || !sub.All(char.IsDigit)) continue;
            var key = $@"{NetAdapterClass}\{sub}";
            var ch = ctx.Registry.GetValue(RegHive.LocalMachine, key, "Characteristics");
            if (ch is { Kind: RegKind.DWord } && (ch.AsDWord & 0x4) != 0) yield return key;
        }
    }

    /// <summary>
    /// A TCP global setting read through PowerShell (enum names are not localised, unlike netsh output)
    /// and written with netsh.
    /// </summary>
    public static IChange NetshGlobal(string key, string label, string readExpr, string netshName, string desired, string netshContext = "global")
    {
        string? Read(TweakContext ctx)
        {
            var r = ctx.Commands.PowerShell(readExpr);
            return r.Success ? r.StdOut.Trim() : null;
        }
        void Write(TweakContext ctx, string value)
        {
            var args = netshContext == "global"
                ? $"int tcp set global {netshName}={value.ToLowerInvariant()}"
                : $"int tcp set supplemental template=internet {netshName}={value.ToLowerInvariant()}";
            ctx.Commands.Run("netsh.exe", args).EnsureSuccess("netsh " + netshName);
        }
        return new CommandChange(
            key,
            $"netsh int tcp {netshName}",
            $"{label} → {desired}  [netsh int tcp set {(netshContext == "global" ? "global" : "supplemental template=internet")} {netshName}={desired.ToLowerInvariant()}]",
            Read,
            ctx => Write(ctx, desired),
            (ctx, captured) => { if (!string.IsNullOrEmpty(captured)) Write(ctx, captured); },
            ctx => string.Equals(Read(ctx), desired, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>A PowerShell boolean state, e.g. (Get-MMAgent).MemoryCompression.</summary>
    public static IChange PsBool(string key, string target, string description, string readExpr, bool desired, string setTrue, string setFalse)
    {
        string? Read(TweakContext ctx)
        {
            var r = ctx.Commands.PowerShell(readExpr);
            return r.Success ? r.StdOut.Trim() : null;
        }
        void Set(TweakContext ctx, bool v) => ctx.Commands.PowerShell(v ? setTrue : setFalse).EnsureSuccess(target);
        return new CommandChange(key, target, description, Read,
            ctx => Set(ctx, desired),
            (ctx, captured) => { if (bool.TryParse(captured, out var b)) Set(ctx, b); },
            ctx => bool.TryParse(Read(ctx), out var b) && b == desired);
    }
}
