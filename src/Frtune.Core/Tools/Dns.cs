using System.Diagnostics;
using System.Net.NetworkInformation;

namespace Frtune.Core.Tools;

public sealed record DnsProvider(string Name, string Primary, string Secondary, string Note);

public static class DnsProviders
{
    public static readonly IReadOnlyList<DnsProvider> All = new[]
    {
        new DnsProvider("Cloudflare", "1.1.1.1", "1.0.0.1", "Fast, privacy-focused"),
        new DnsProvider("Google", "8.8.8.8", "8.8.4.4", "Reliable everywhere"),
        new DnsProvider("Quad9", "9.9.9.9", "149.112.112.112", "Blocks known malware domains"),
        new DnsProvider("AdGuard", "94.140.14.14", "94.140.15.15", "Blocks ads and trackers"),
    };

    /// <summary>
    /// Pings a DNS server (ICMP). Runs only when the user presses "Test latency".
    /// Returns the median of <paramref name="attempts"/> replies, or null if none answered.
    /// </summary>
    public static async Task<double?> MeasureLatencyAsync(string address, int attempts = 4, CancellationToken ct = default)
    {
        using var ping = new Ping();
        var times = new List<long>();
        for (var i = 0; i < attempts; i++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var reply = await ping.SendPingAsync(address, 1000).ConfigureAwait(false);
                if (reply.Status == IPStatus.Success) times.Add(reply.RoundtripTime);
            }
            catch (PingException) { }
        }
        if (times.Count == 0) return null;
        times.Sort();
        return times[times.Count / 2];
    }
}
