using Frtune.Core.Abstractions;
using Frtune.Core.Engine;
using Frtune.Core.Model;
using Frtune.Core.Tests.Fakes;
using Frtune.Core.Tweaks;

namespace Frtune.Core.Tests;

public class EngineTests
{
    private static Tweak Sample(string id = "t.sample") =>
        new Tweak(id, "Sample", "desc", TweakCategory.Gaming, TweakRisk.Safe)
            .Hklm(@"SOFTWARE\Frtune\Test", "A", 1)
            .Hklm(@"SOFTWARE\Frtune\Test", "B", 1);

    [Fact]
    public async Task Apply_then_revert_restores_original_state_and_logs_both()
    {
        using var env = new TestEnv();
        env.Registry.SetValue(RegHive.LocalMachine, @"SOFTWARE\Frtune\Test", "A", RegValue.DWord(5));
        var before = env.Registry.Dump();
        var tweak = Sample();

        var r1 = await env.Engine.ExecuteAsync(new[] { new TweakRequest(tweak, true) });
        Assert.Equal(1, r1.Succeeded);
        Assert.Equal(1, env.RestorePoints.Calls);
        Assert.Equal(TweakStatus.On, env.Engine.GetStatus(tweak));

        var r2 = await env.Engine.ExecuteAsync(new[] { new TweakRequest(tweak, false) });
        Assert.Equal(1, r2.Succeeded);
        Assert.Equal(before, env.Registry.Dump());
        Assert.Equal(TweakStatus.Off, env.Engine.GetStatus(tweak));
        Assert.Null(env.Backups.TryGet(tweak.Id));
        Assert.Contains(env.Log.Entries, e => e.Action == "apply" && e.TweakId == tweak.Id && e.Success);
        Assert.Contains(env.Log.Entries, e => e.Action == "revert" && e.TweakId == tweak.Id && e.Success);
    }

    [Fact]
    public async Task Snapshot_is_persisted_to_disk_and_survives_restart()
    {
        using var env = new TestEnv();
        env.Registry.SetValue(RegHive.LocalMachine, @"SOFTWARE\Frtune\Test", "A", RegValue.DWord(5));
        await env.Engine.ExecuteAsync(new[] { new TweakRequest(Sample(), true) });

        var reopened = new JsonBackupStore(env.Dir);
        var snap = reopened.TryGet("t.sample");
        Assert.NotNull(snap);
        Assert.Equal("dword:5", snap!.Values[@"reg:hklm\software\frtune\test\a"]);
        Assert.Null(snap.Values[@"reg:hklm\software\frtune\test\b"]);
    }

    [Fact]
    public async Task Failed_apply_rolls_back_partial_changes()
    {
        using var env = new TestEnv();
        env.Registry.SetValue(RegHive.LocalMachine, @"SOFTWARE\Frtune\Test", "A", RegValue.DWord(5));
        var before = env.Registry.Dump();
        env.Registry.FailOnName = "B";

        var r = await env.Engine.ExecuteAsync(new[] { new TweakRequest(Sample(), true) });

        Assert.Equal(1, r.Failed);
        env.Registry.FailOnName = null;
        Assert.Equal(before, env.Registry.Dump());
        Assert.Null(env.Backups.TryGet("t.sample"));
        Assert.Contains(env.Log.Entries, e => !e.Success && e.TweakId == "t.sample");
    }

    [Fact]
    public async Task Revert_without_backup_is_refused_rather_than_guessed()
    {
        using var env = new TestEnv();
        var tweak = Sample();
        env.Registry.SetValue(RegHive.LocalMachine, @"SOFTWARE\Frtune\Test", "A", RegValue.DWord(1));
        env.Registry.SetValue(RegHive.LocalMachine, @"SOFTWARE\Frtune\Test", "B", RegValue.DWord(1));
        Assert.Equal(TweakStatus.AlreadySet, env.Engine.GetStatus(tweak));

        var plan = env.Engine.Plan(new[] { new TweakRequest(tweak, false) });
        Assert.False(plan[0].CanRun);

        var r = await env.Engine.ExecuteAsync(new[] { new TweakRequest(tweak, false) });
        Assert.Equal(1, r.Failed);
        Assert.Equal(1u, env.Registry.GetValue(RegHive.LocalMachine, @"SOFTWARE\Frtune\Test", "A")!.AsDWord);
    }

    [Fact]
    public async Task Reapplying_keeps_the_first_original_snapshot()
    {
        using var env = new TestEnv();
        env.Registry.SetValue(RegHive.LocalMachine, @"SOFTWARE\Frtune\Test", "A", RegValue.DWord(5));
        var tweak = Sample();
        await env.Engine.ExecuteAsync(new[] { new TweakRequest(tweak, true) });
        // Something resets A, so the tweak is now "partial"; user re-applies.
        env.Registry.SetValue(RegHive.LocalMachine, @"SOFTWARE\Frtune\Test", "A", RegValue.DWord(9));
        Assert.Equal(TweakStatus.Partial, env.Engine.GetStatus(tweak));
        await env.Engine.ExecuteAsync(new[] { new TweakRequest(tweak, true) });

        await env.Engine.ExecuteAsync(new[] { new TweakRequest(tweak, false) });
        Assert.Equal(5u, env.Registry.GetValue(RegHive.LocalMachine, @"SOFTWARE\Frtune\Test", "A")!.AsDWord);
    }

    [Fact]
    public async Task Failed_restore_point_aborts_unless_user_allows_continuing()
    {
        using var env = new TestEnv();
        env.RestorePoints.Result = new RestorePointResult(false, false, "System Protection is off");

        var r = await env.Engine.ExecuteAsync(new[] { new TweakRequest(Sample(), true) });
        Assert.True(r.Aborted);
        Assert.Equal(0, env.Registry.Writes);

        var r2 = await env.Engine.ExecuteAsync(new[] { new TweakRequest(Sample(), true) },
            new ExecuteOptions { ContinueWithoutRestorePoint = true });
        Assert.False(r2.Aborted);
        Assert.Equal(1, r2.Succeeded);
    }

    [Fact]
    public async Task Restore_all_reverts_everything_frtune_applied()
    {
        using var env = new TestEnv();
        var before = env.Registry.Dump();
        var t1 = Sample("t.one");
        var t2 = new Tweak("t.two", "Two", "", TweakCategory.Visuals, TweakRisk.Safe).Hkcu(@"Control Panel\Desktop", "MenuShowDelay", "50");
        await env.Engine.ExecuteAsync(new[] { new TweakRequest(t1, true), new TweakRequest(t2, true) });

        var r = await env.Engine.RestoreAllAsync(new ITweak[] { t1, t2 });

        Assert.Equal(2, r.Succeeded);
        Assert.Equal(before, env.Registry.Dump());
        Assert.Empty(env.Backups.All());
    }

    [Fact]
    public async Task Restart_requirement_is_reported()
    {
        using var env = new TestEnv();
        var t = Sample().Needs(RestartKind.Reboot);
        var r = await env.Engine.ExecuteAsync(new[] { new TweakRequest(t, true) });
        Assert.Equal(RestartKind.Reboot, r.Restart);
    }

    [Fact]
    public void Profile_file_round_trips_and_rejects_foreign_json()
    {
        var p = new ProfileFile { Name = "Mine", Tweaks = { "gaming.gamemode", "net.nagle" } };
        var back = ProfileFile.FromJson(p.ToJson());
        Assert.Equal(p.Tweaks, back.Tweaks);
        Assert.Throws<FormatException>(() => ProfileFile.FromJson("""{"App":"other","Tweaks":[]}"""));
    }

    [Fact]
    public void Restart_detector_reads_pending_reboot_keys()
    {
        using var env = new TestEnv();
        Assert.Empty(RestartDetector.PendingReasons(env.Registry));
        env.Registry.SetValue(RegHive.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired", "x", RegValue.DWord(1));
        Assert.Single(RestartDetector.PendingReasons(env.Registry));
    }
}
