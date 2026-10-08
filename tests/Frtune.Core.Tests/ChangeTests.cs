using Frtune.Core.Abstractions;
using Frtune.Core.Changes;
using Frtune.Core.Tests.Fakes;

namespace Frtune.Core.Tests;

public class ChangeTests
{
    [Fact]
    public void Registry_revert_restores_exact_original_value()
    {
        using var env = new TestEnv();
        env.Registry.SetValue(RegHive.LocalMachine, @"SOFTWARE\Test", "V", RegValue.DWord(7));
        var change = new RegistryChange(RegHive.LocalMachine, @"SOFTWARE\Test", "V", RegValue.DWord(1));

        var captured = change.Capture(env.Context);
        change.Apply(env.Context);
        Assert.True(change.IsApplied(env.Context));
        change.Restore(env.Context, captured);

        Assert.Equal(7u, env.Registry.GetValue(RegHive.LocalMachine, @"SOFTWARE\Test", "V")!.AsDWord);
    }

    [Fact]
    public void Registry_revert_deletes_value_that_did_not_exist()
    {
        using var env = new TestEnv();
        env.Registry.SetValue(RegHive.CurrentUser, @"Software\A", "Other", RegValue.Str("keep"));
        var change = new RegistryChange(RegHive.CurrentUser, @"Software\A", "New", RegValue.DWord(1));

        var captured = change.Capture(env.Context);
        Assert.Null(captured);
        change.Apply(env.Context);
        change.Restore(env.Context, captured);

        Assert.Null(env.Registry.GetValue(RegHive.CurrentUser, @"Software\A", "New"));
        Assert.Equal("keep", env.Registry.GetValue(RegHive.CurrentUser, @"Software\A", "Other")!.Data);
    }

    [Fact]
    public void Registry_revert_removes_keys_it_created()
    {
        using var env = new TestEnv();
        env.Registry.SetValue(RegHive.CurrentUser, @"Software\Classes", "x", RegValue.DWord(1));
        var before = env.Registry.Dump();
        var change = new RegistryChange(RegHive.CurrentUser, @"Software\Classes\CLSID\{X}\InprocServer32", "", RegValue.Str(""));

        var captured = change.Capture(env.Context);
        change.Apply(env.Context);
        Assert.True(env.Registry.KeyExists(RegHive.CurrentUser, @"Software\Classes\CLSID\{X}\InprocServer32"));
        change.Restore(env.Context, captured);

        Assert.False(env.Registry.KeyExists(RegHive.CurrentUser, @"Software\Classes\CLSID"));
        Assert.Equal(before, env.Registry.Dump());
    }

    [Fact]
    public void Registry_revert_keeps_created_key_if_something_else_now_lives_there()
    {
        using var env = new TestEnv();
        var change = new RegistryChange(RegHive.LocalMachine, @"SOFTWARE\Policies\New", "A", RegValue.DWord(1));
        var captured = change.Capture(env.Context);
        change.Apply(env.Context);
        env.Registry.SetValue(RegHive.LocalMachine, @"SOFTWARE\Policies\New", "B", RegValue.DWord(2));

        change.Restore(env.Context, captured);

        Assert.Null(env.Registry.GetValue(RegHive.LocalMachine, @"SOFTWARE\Policies\New", "A"));
        Assert.NotNull(env.Registry.GetValue(RegHive.LocalMachine, @"SOFTWARE\Policies\New", "B"));
    }

    [Theory]
    [InlineData("dword:4294967295")]
    [InlineData("qword:1")]
    [InlineData("sz:hello world: with colon")]
    [InlineData("expand:%SystemRoot%\\x")]
    [InlineData("multi:[\"a\",\"b c\"]")]
    [InlineData("bin:9012038010000000")]
    public void RegValue_tokens_round_trip(string token)
    {
        Assert.Equal(token, RegValue.FromToken(token).ToToken());
    }

    [Fact]
    public void RegistryList_change_edits_one_entry_and_keeps_others()
    {
        using var env = new TestEnv();
        const string key = @"Software\Microsoft\DirectX\UserGpuPreferences";
        env.Registry.SetValue(RegHive.CurrentUser, key, "DirectXUserGlobalSettings", RegValue.Str("VRROptimizeEnable=1;AutoHDREnable=1;"));
        var change = new RegistryListChange(RegHive.CurrentUser, key, "DirectXUserGlobalSettings", "SwapEffectUpgradeEnable", "1");

        var captured = change.Capture(env.Context);
        change.Apply(env.Context);
        Assert.Equal("VRROptimizeEnable=1;AutoHDREnable=1;SwapEffectUpgradeEnable=1;",
            env.Registry.GetValue(RegHive.CurrentUser, key, "DirectXUserGlobalSettings")!.Data);

        change.Restore(env.Context, captured);
        Assert.Equal("VRROptimizeEnable=1;AutoHDREnable=1;",
            env.Registry.GetValue(RegHive.CurrentUser, key, "DirectXUserGlobalSettings")!.Data);
    }

    [Fact]
    public void Service_change_restores_original_start_type_and_skips_missing_services()
    {
        using var env = new TestEnv();
        env.Services.Start["SysMain"] = ServiceStartType.Automatic;
        var change = new ServiceChange("SysMain", ServiceStartType.Disabled);
        var missing = new ServiceChange("NotInstalled", ServiceStartType.Disabled);

        var c = change.Capture(env.Context);
        change.Apply(env.Context);
        missing.Apply(env.Context);
        Assert.Equal(ServiceStartType.Disabled, env.Services.Start["SysMain"]);
        Assert.Contains("SysMain", env.Services.Stopped);
        Assert.True(missing.IsApplied(env.Context));
        Assert.False(env.Services.Start.ContainsKey("NotInstalled"));

        change.Restore(env.Context, c);
        Assert.Equal(ServiceStartType.Automatic, env.Services.Start["SysMain"]);
    }

    [Fact]
    public void Powercfg_query_parsing_is_language_independent()
    {
        const string german = """
            GUID des Energieschemas: 381b4222-f694-41f0-9685-ff5bb260df2e  (Ausbalanciert)
              GUID der Untergruppe: 54533251-82be-4824-96c1-47b60b740d00  (Prozessorenergieverwaltung)
                GUID der Energieeinstellung: 0cc5b647-c1df-4637-891a-dec35c318583
                  Minimaler möglicher Wert: 0x00000000
                  Maximaler möglicher Wert: 0x00000064
                  Mögliche Einstellungen (Inkrement): 0x00000001
                  Mögliche Einstellungseinheiten: %
                Index der aktuellen Wechselstromeinstellung: 0x00000005
                Index der aktuellen Gleichstromeinstellung: 0x0000000a
            """;
        Assert.Equal((5u, 10u), PowerSettingChange.ParseQuery(german));
    }

    [Fact]
    public void Power_setting_revert_targets_the_plan_that_was_active_at_capture()
    {
        using var env = new TestEnv();
        var active = "381b4222-f694-41f0-9685-ff5bb260df2e";
        env.Commands.Handler = (file, args) => args switch
        {
            "/getactivescheme" => new(0, $"Power Scheme GUID: {active}  (Balanced)", ""),
            _ when args.StartsWith("/query") => new(0, "Current AC Power Setting Index: 0x00000001\nCurrent DC Power Setting Index: 0x00000001", ""),
            _ => new(0, "", ""),
        };
        var change = new PowerSettingChange(PowerGuids.SubUsb, PowerGuids.UsbSelectiveSuspend, "USB", 0);

        var captured = change.Capture(env.Context);
        active = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c"; // user switched plan afterwards
        change.Restore(env.Context, captured);

        Assert.Contains(env.Commands.Calls, c => c.Contains($"/setacvalueindex 381b4222-f694-41f0-9685-ff5bb260df2e {PowerGuids.SubUsb} {PowerGuids.UsbSelectiveSuspend} 1"));
    }
}
