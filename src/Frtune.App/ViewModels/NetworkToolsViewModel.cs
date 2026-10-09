using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Frtune.App.Services;
using Frtune.Core.Tools;

namespace Frtune.App.ViewModels;

public sealed partial class DnsRow : ObservableObject
{
    public DnsRow(DnsProvider p) => Provider = p;
    public DnsProvider Provider { get; }
    [ObservableProperty] private string _latency = "–";
    [ObservableProperty] private bool _isFastest;
}

public sealed partial class NetworkToolsViewModel : PageViewModel
{
    private readonly MainViewModel _main;

    public NetworkToolsViewModel(MainViewModel main) : base("Network tools", "")
    {
        _main = main;
        foreach (var p in DnsProviders.All) Providers.Add(new DnsRow(p));
    }

    public ObservableCollection<DnsRow> Providers { get; } = new();
    [ObservableProperty] private string _currentDns = "Reading…";
    [ObservableProperty] private string _customPrimary = "";
    [ObservableProperty] private string _customSecondary = "";
    [ObservableProperty] private bool _working;
    [ObservableProperty] private bool _hasBackup;

    protected override Task LoadAsync() => RefreshAsync();

    private async Task RefreshAsync()
    {
        var adapters = await Task.Run(() => _main.S.Network.GetDns());
        CurrentDns = adapters.Count == 0
            ? "No connected network adapter found."
            : string.Join("\n", adapters.Select(a => $"{a.Alias}: {(a.Servers.Length == 0 ? "automatic (from router)" : string.Join(", ", a.Servers))}"));
        HasBackup = _main.S.Network.HasDnsBackup;
    }

    /// <summary>Only runs when the user clicks it: sends 4 pings to each provider.</summary>
    [RelayCommand]
    private async Task TestLatencyAsync()
    {
        Working = true;
        foreach (var r in Providers) { r.Latency = "testing…"; r.IsFastest = false; }
        var results = await Task.WhenAll(Providers.Select(async r => (r, ms: await DnsProviders.MeasureLatencyAsync(r.Provider.Primary))));
        foreach (var (r, ms) in results) r.Latency = ms is null ? "no reply" : $"{ms:0} ms";
        var best = results.Where(x => x.ms is not null).OrderBy(x => x.ms).FirstOrDefault();
        if (best.r is not null) best.r.IsFastest = true;
        Working = false;
    }

    [RelayCommand]
    private Task UseProviderAsync(DnsRow row) => SetAsync(row.Provider.Primary, row.Provider.Secondary, row.Provider.Name);

    [RelayCommand]
    private Task UseCustomAsync()
    {
        if (!System.Net.IPAddress.TryParse(CustomPrimary, out _) || (CustomSecondary.Length > 0 && !System.Net.IPAddress.TryParse(CustomSecondary, out _)))
        {
            _main.S.Toasts.Show(ToastKind.Error, "Invalid address", "Enter IPv4 addresses like 1.1.1.1.");
            return Task.CompletedTask;
        }
        return SetAsync(CustomPrimary, CustomSecondary.Length > 0 ? CustomSecondary : CustomPrimary, "custom DNS");
    }

    private async Task SetAsync(string primary, string secondary, string name)
    {
        if (!_main.Dialogs.Ask("Change DNS", $"Set DNS on every connected adapter to {primary} and {secondary} ({name})?\n\nYour current DNS settings are saved first, and \"Restore original DNS\" puts them back.", "Change DNS"))
            return;
        await RunAction(() => _main.S.Network.SetDns(primary, secondary), $"DNS set to {name}");
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task RestoreDnsAsync()
    {
        await RunAction(_main.S.Network.RestoreDns, "Original DNS restored");
        await RefreshAsync();
    }

    [RelayCommand] private Task FlushDnsAsync() => RunText(() => _main.S.Network.FlushDns());

    [RelayCommand]
    private Task ResetWinsockAsync() =>
        _main.Dialogs.Ask("Reset Winsock", "Resets the Winsock catalog to defaults (netsh winsock reset). VPN and some security software may need reinstalling. Restart required.", "Reset")
            ? RunText(() => _main.S.Network.ResetWinsock(), restart: true) : Task.CompletedTask;

    [RelayCommand]
    private Task ResetTcpIpAsync() =>
        _main.Dialogs.Ask("Reset TCP/IP", "Resets TCP/IP settings to defaults (netsh int ip reset). Static IP addresses will need to be set again. Restart required.", "Reset")
            ? RunText(() => _main.S.Network.ResetTcpIp(), restart: true) : Task.CompletedTask;

    private async Task RunText(Func<string> action, bool restart = false)
    {
        Working = true;
        try
        {
            var msg = await Task.Run(action);
            _main.S.Toasts.Show(msg.Contains("failed") ? ToastKind.Error : ToastKind.Success, msg);
            if (restart && !msg.Contains("failed"))
            {
                _main.S.PendingRestart = Core.Model.RestartKind.Reboot;
                _main.UpdateRestartBanner();
            }
        }
        finally { Working = false; }
    }

    private async Task RunAction(Action action, string success)
    {
        Working = true;
        try
        {
            await Task.Run(action);
            _main.S.Toasts.Show(ToastKind.Success, success);
        }
        catch (Exception ex) { _main.S.Toasts.Show(ToastKind.Error, "Failed", ex.Message); }
        finally { Working = false; }
    }
}
