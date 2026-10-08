using Frtune.Core.Abstractions;
using Frtune.Core.Changes;
using Frtune.Core.Model;
using static Frtune.Core.Model.TweakCategory;
using static Frtune.Core.Model.TweakRisk;

namespace Frtune.Core.Tweaks.Catalog;

internal static class NetworkTweaks
{
    private const string TcpInternet = "(Get-NetTCPSetting -SettingName Internet)";

    public static IEnumerable<Tweak> All()
    {
        yield return new Tweak("net.tcpglobal", "TCP tuning for low latency",
                "Receive-window auto-tuning = normal, Receive Side Scaling on (spreads network work across cores), Receive Segment Coalescing off (lower per-packet latency), TCP timestamps off.",
                Network, Moderate)
            .Add(Helpers.NetshGlobal("tcp.autotuning", "Receive window auto-tuning", $"{TcpInternet}.AutoTuningLevelLocal", "autotuninglevel", "Normal"))
            .Add(Helpers.NetshGlobal("tcp.rss", "Receive Side Scaling", "(Get-NetOffloadGlobalSetting).ReceiveSideScaling", "rss", "Enabled"))
            .Add(Helpers.NetshGlobal("tcp.rsc", "Receive Segment Coalescing", "(Get-NetOffloadGlobalSetting).ReceiveSegmentCoalescing", "rsc", "Disabled"))
            .Add(Helpers.NetshGlobal("tcp.timestamps", "TCP timestamps", $"{TcpInternet}.Timestamps", "timestamps", "Disabled"))
            .In(Preset.Gaming | Preset.MaxPerformance);

        yield return new Tweak("net.ecn", "Disable ECN",
                "Turns off Explicit Congestion Notification. A few routers and games handle ECN-marked packets badly; on most networks this makes no difference.",
                Network, Moderate)
            .Add(Helpers.NetshGlobal("tcp.ecn", "ECN capability", $"{TcpInternet}.EcnCapability", "ecncapability", "Disabled"));

        yield return new Tweak("net.ctcp", "Use CTCP congestion control",
                "Switches the Internet TCP template from CUBIC to Compound TCP. Can ramp up faster on high-latency links; on a normal home connection the difference is small.",
                Network, Moderate)
            .Add(Helpers.NetshGlobal("tcp.congestion", "Congestion provider", $"{TcpInternet}.CongestionProvider", "congestionprovider", "CTCP", "supplemental"))
            .In(Preset.MaxPerformance);

        yield return new Tweak("net.nagle", "Disable Nagle's algorithm (active adapters)",
                "Sets TcpAckFrequency=1 and TCPNoDelay=1 on every adapter that has an IP address, so small game packets are sent and acknowledged immediately instead of batched.",
                Network, Moderate)
            .AddDynamic(ctx => Helpers.ActiveInterfaces(ctx).SelectMany(guid => new IChange[]
            {
                new RegistryChange(RegHive.LocalMachine, $@"{Helpers.TcpipInterfaces}\{guid}", "TcpAckFrequency", RegValue.DWord(1)),
                new RegistryChange(RegHive.LocalMachine, $@"{Helpers.TcpipInterfaces}\{guid}", "TCPNoDelay", RegValue.DWord(1)),
            }))
            .Needs(RestartKind.Reboot)
            .In(Preset.Gaming | Preset.MaxPerformance);

        yield return new Tweak("net.throttling", "Remove network throttling",
                "Sets NetworkThrottlingIndex to 0xFFFFFFFF. By default Windows limits non-multimedia network packet processing while media is playing.",
                Network, Safe)
            .Hklm(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "NetworkThrottlingIndex", 0xFFFFFFFF)
            .In(Preset.Gaming | Preset.MaxPerformance);

        yield return new Tweak("net.qos", "Disable QoS reserved bandwidth",
                "Sets the Packet Scheduler's NonBestEffortLimit policy to 0%. Honest note: Windows only reserves bandwidth when an app asks for QoS, so most PCs see no change.",
                Network, Safe)
            .Hklm(@"SOFTWARE\Policies\Microsoft\Windows\Psched", "NonBestEffortLimit", 0)
            .In(Preset.MaxPerformance);

        yield return new Tweak("net.adapterpower", "Disable network adapter power saving",
                "Unticks \"Allow the computer to turn off this device to save power\" on every physical network adapter (PnPCapabilities = 0x18). Prevents the adapter dozing between packets.",
                Network, Moderate)
            .AddDynamic(ctx => Helpers.PhysicalAdapterKeys(ctx).Select(k =>
                (IChange)new RegistryChange(RegHive.LocalMachine, k, "PnPCapabilities", RegValue.DWord(0x18))))
            .Needs(RestartKind.Reboot)
            .In(Preset.Gaming | Preset.MaxPerformance);

        yield return new Tweak("net.preferipv4", "Prefer IPv4 over IPv6",
                "Sets DisabledComponents = 0x20, Microsoft's supported way to prefer IPv4 without disabling IPv6 (fully disabling IPv6 breaks parts of Windows).",
                Network, Moderate)
            .Hklm(@"SYSTEM\CurrentControlSet\Services\Tcpip6\Parameters", "DisabledComponents", 0x20)
            .Needs(RestartKind.Reboot);

        yield return new Tweak("net.teredo", "Disable Teredo tunnelling",
                "Turns off the Teredo IPv6-over-IPv4 tunnel. Note: Xbox party chat on PC uses Teredo, so leave it on if you use that.",
                Network, Moderate)
            .Add(new CommandChange("teredo", "netsh interface teredo",
                "netsh interface teredo set state disabled",
                ctx => { var r = ctx.Commands.PowerShell("(Get-NetTeredoConfiguration).Type"); return r.Success ? r.StdOut.Trim() : null; },
                ctx => ctx.Commands.Run("netsh.exe", "interface teredo set state disabled").EnsureSuccess("netsh teredo"),
                (ctx, c) => { if (!string.IsNullOrEmpty(c)) ctx.Commands.Run("netsh.exe", $"interface teredo set state type={c.ToLowerInvariant()}").EnsureSuccess("netsh teredo"); },
                ctx => ctx.Commands.PowerShell("(Get-NetTeredoConfiguration).Type").StdOut.Trim().Equals("Disabled", StringComparison.OrdinalIgnoreCase)));

        yield return new Tweak("net.wifisense", "Disable Wi-Fi Sense hotspot sharing",
                "Stops automatic connection to suggested open hotspots and hotspot reporting.",
                Network, Safe)
            .Hklm(@"SOFTWARE\Microsoft\PolicyManager\default\WiFi\AllowWiFiHotSpotReporting", "value", 0)
            .Hklm(@"SOFTWARE\Microsoft\PolicyManager\default\WiFi\AllowAutoConnectToWiFiSenseHotspots", "value", 0)
            .When(s => s.HasWifi, "PCs with Wi-Fi")
            .In(Preset.Privacy | Preset.Balanced);

        yield return new Tweak("net.deliveryopt", "Disable Delivery Optimization peer sharing",
                "Windows Update downloads only from Microsoft (DODownloadMode = 0) and stops uploading updates to other PCs from your connection.",
                Network, Safe)
            .Hklm(@"SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization", "DODownloadMode", 0)
            .In(Preset.Gaming | Preset.Privacy | Preset.LowEnd | Preset.MaxPerformance | Preset.Balanced);
    }
}
