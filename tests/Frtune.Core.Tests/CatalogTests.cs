using Frtune.Core.Abstractions;
using Frtune.Core.Changes;
using Frtune.Core.Engine;
using Frtune.Core.Model;
using Frtune.Core.Tests.Fakes;
using Frtune.Core.Tweaks;
using Frtune.Core.Tweaks.Catalog;

namespace Frtune.Core.Tests;

public class CatalogTests
{
    private static IReadOnlyList<Tweak> All => TweakCatalog.All;

    public static IEnumerable<object[]> OfflineTweakIds() =>
        All.Where(t => t.StaticChanges.All(c => c is RegistryChange or RegistryListChange or ServiceChange))
           .Select(t => new object[] { t.Id });

    [Fact]
    public void Ids_are_unique_and_namespaced()
    {
        Assert.Equal(All.Count, All.Select(t => t.Id).Distinct().Count());
        Assert.All(All, t => Assert.Contains('.', t.Id));
        Assert.True(All.Count >= 80, $"catalog has {All.Count} tweaks");
    }

    [Fact]
    public void No_two_tweaks_touch_the_same_value()
    {
        // Overlapping tweaks would make snapshots capture each other's values instead of the original.
        var owners = All.SelectMany(t => t.StaticChanges.Select(c => (c.Key, t.Id)))
            .GroupBy(x => x.Key).Where(g => g.Select(x => x.Id).Distinct().Count() > 1)
            .Select(g => $"{g.Key}: {string.Join(", ", g.Select(x => x.Id))}").ToList();
        Assert.Empty(owners);
    }

    [Fact]
    public void Every_tweak_has_text_and_a_description_of_its_changes()
    {
        Assert.All(All, t =>
        {
            Assert.False(string.IsNullOrWhiteSpace(t.Name));
            Assert.False(string.IsNullOrWhiteSpace(t.Description));
            Assert.True(t.StaticChanges.Count > 0 || t.HasDynamicChanges, t.Id);
            Assert.All(t.StaticChanges, c => Assert.False(string.IsNullOrWhiteSpace(c.Describe())));
        });
    }

    [Fact]
    public void Advanced_page_tweaks_are_advanced_risk_warned_and_never_in_presets()
    {
        var adv = All.Where(t => t.Category == TweakCategory.Advanced).ToList();
        Assert.NotEmpty(adv);
        Assert.All(adv, t =>
        {
            Assert.Equal(TweakRisk.Advanced, t.Risk);
            Assert.False(string.IsNullOrWhiteSpace(t.Warning), t.Id);
            Assert.Equal(Preset.None, t.Presets);
        });
    }

    [Theory]
    [InlineData(Preset.Gaming)]
    [InlineData(Preset.Balanced)]
    [InlineData(Preset.LowEnd)]
    [InlineData(Preset.Privacy)]
    [InlineData(Preset.MaxPerformance)]
    public void Presets_never_touch_defender_firewall_mitigations_or_vbs(Preset preset)
    {
        var selected = Presets.Select(All, preset, TestEnv.UserPc);
        Assert.NotEmpty(selected);
        Assert.DoesNotContain(selected, t => t.Risk == TweakRisk.Advanced);
        var text = string.Join("\n", selected.Cast<Tweak>().SelectMany(t => t.StaticChanges).Select(c => c.Describe()));
        foreach (var forbidden in new[] { "FeatureSettingsOverride", "MpPreference", "WinDefend", "MpsSvc", "DeviceGuard", "WindowsFirewall", "PauseUpdates" })
            Assert.DoesNotContain(forbidden, text, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(OfflineTweakIds))]
    public void Apply_then_revert_leaves_registry_and_services_exactly_as_before(string id)
    {
        using var env = new TestEnv();
        var tweak = All.Single(t => t.Id == id);
        SeedMachine(env);
        // Give every second target a non-default original value so we test real restoration.
        var i = 0;
        foreach (var c in tweak.GetChanges(env.Context))
        {
            if (i++ % 2 == 1) continue;
            if (c is RegistryChange r) env.Registry.SetValue(r.Hive, r.KeyPath, r.Name, RegValue.DWord(12345));
            if (c is ServiceChange s) env.Services.Start[s.ServiceName] = ServiceStartType.Automatic;
        }
        var before = env.Registry.Dump();
        var servicesBefore = string.Join(",", env.Services.Start.OrderBy(k => k.Key).Select(k => $"{k.Key}={k.Value}"));

        var snapshot = tweak.Apply(env.Context);
        Assert.True(tweak.IsApplied(env.Context), "IsApplied should be true right after Apply");
        tweak.Revert(env.Context, snapshot);

        Assert.Equal(before, env.Registry.Dump());
        Assert.Equal(servicesBefore, string.Join(",", env.Services.Start.OrderBy(k => k.Key).Select(k => $"{k.Key}={k.Value}")));
    }

    [Fact]
    public void Tailored_to_the_users_pc()
    {
        var pc = TestEnv.UserPc;
        Tweak T(string id) => All.Single(t => t.Id == id);

        Assert.True(T("gaming.hags").IsApplicable(pc));
        Assert.NotNull(T("gaming.hags").RecommendationFor(pc));
        Assert.NotNull(T("perf.coreparking").RecommendationFor(pc));
        Assert.True(T("perf.ultimateplan").IsApplicable(pc));
        Assert.False(T("visuals.classicmenu").IsApplicable(pc)); // Windows 10 build
        Assert.False(T("perf.ultimateplan").IsApplicable(pc with { IsLaptop = true }));
        Assert.False(T("gaming.hags").IsApplicable(pc with { Gpus = new[] { new GpuInfo("Intel UHD 630", GpuVendor.Intel, 0, "") } }));
        Assert.False(T("perf.sysmain").IsApplicable(pc with { SystemDriveIsSsd = false }));
    }

    [Fact]
    public void Svchost_threshold_is_computed_from_ram()
    {
        using var env = new TestEnv();
        All.Single(t => t.Id == "perf.svchostsplit").Apply(env.Context);
        Assert.Equal(16u * 1024 * 1024, env.Registry.GetValue(RegHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control", "SvcHostSplitThresholdInKB")!.AsDWord);
    }

    [Theory]
    [InlineData("Professional", 1u)]
    [InlineData("Core", 1u)]
    [InlineData("Enterprise", 0u)]
    [InlineData("Education", 0u)]
    public void Telemetry_uses_the_lowest_level_the_edition_honours(string edition, uint expected)
    {
        using var env = new TestEnv(TestEnv.UserPc with { Edition = edition });
        env.Services.Start["DiagTrack"] = ServiceStartType.Automatic;
        All.Single(t => t.Id == "privacy.telemetry").Apply(env.Context);
        Assert.Equal(expected, env.Registry.GetValue(RegHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry")!.AsDWord);
        Assert.Equal(ServiceStartType.Disabled, env.Services.Start["DiagTrack"]);
    }

    [Fact]
    public void Nagle_targets_only_interfaces_with_an_address()
    {
        using var env = new TestEnv();
        SeedMachine(env);
        var tweak = All.Single(t => t.Id == "net.nagle");
        var changes = tweak.GetChanges(env.Context).OfType<RegistryChange>().ToList();
        Assert.Equal(2, changes.Count);
        Assert.All(changes, c => Assert.Contains("{ACTIVE}", c.KeyPath));
    }

    [Fact]
    public void Pause_updates_uses_the_configured_number_of_days()
    {
        using var env = new TestEnv();
        All.Single(t => t.Id == "adv.pauseupdates").Apply(env.Context);
        Assert.Equal("2026-01-22T12:00:00Z", env.Registry.GetValue(RegHive.LocalMachine, @"SOFTWARE\Microsoft\WindowsUpdate\UX\Settings", "PauseUpdatesExpiryTime")!.Data);
    }

    private static void SeedMachine(TestEnv env)
    {
        const string ifs = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces";
        env.Registry.SetValue(RegHive.LocalMachine, ifs + @"\{ACTIVE}", "DhcpIPAddress", RegValue.Str("192.168.1.20"));
        env.Registry.SetValue(RegHive.LocalMachine, ifs + @"\{IDLE}", "DhcpIPAddress", RegValue.Str("0.0.0.0"));
        const string cls = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}";
        env.Registry.SetValue(RegHive.LocalMachine, cls + @"\0001", "Characteristics", RegValue.DWord(0x84));
        env.Registry.SetValue(RegHive.LocalMachine, cls + @"\0002", "Characteristics", RegValue.DWord(0x1)); // virtual
        foreach (var s in new[] { "SysMain", "WSearch", "DiagTrack", "dmwappushservice", "Spooler", "Fax" })
            env.Services.Start[s] = ServiceStartType.Manual;
    }
}
