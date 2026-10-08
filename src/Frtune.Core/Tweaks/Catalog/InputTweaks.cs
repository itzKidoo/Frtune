using Frtune.Core.Abstractions;
using Frtune.Core.Model;
using static Frtune.Core.Model.TweakCategory;
using static Frtune.Core.Model.TweakRisk;

namespace Frtune.Core.Tweaks.Catalog;

internal static class InputTweaks
{
    private const string Mouse = @"Control Panel\Mouse";

    // MarkC mouse fix curve for 100% display scaling: makes pointer movement exactly 1:1 even
    // when a game or app turns "Enhance pointer precision" back on.
    private const string MarkCX = "0000000000000000C0CC0C0000000000809919000000000040662600000000000033330000000000";
    private const string MarkCY = "0000000000000000000038000000000000007000000000000000A800000000000000E00000000000";

    public static IEnumerable<Tweak> All()
    {
        yield return new Tweak("input.mouseaccel", "Disable mouse acceleration (1:1 MarkC fix)",
                "Turns off \"Enhance pointer precision\", sets pointer speed to 10/20 (6/11, no multiplier) and installs the MarkC 1:1 curve for 100% scaling, so movement is raw even if a game re-enables acceleration. Takes effect after you sign out.",
                Input, Safe)
            .Hkcu(Mouse, "MouseSpeed", "0")
            .Hkcu(Mouse, "MouseThreshold1", "0")
            .Hkcu(Mouse, "MouseThreshold2", "0")
            .Hkcu(Mouse, "MouseSensitivity", "10")
            .Reg(RegHive.CurrentUser, Mouse, "SmoothMouseXCurve", RegValue.FromHex(MarkCX))
            .Reg(RegHive.CurrentUser, Mouse, "SmoothMouseYCurve", RegValue.FromHex(MarkCY))
            .Needs(RestartKind.SignOut)
            .In(Preset.Gaming | Preset.MaxPerformance);

        yield return new Tweak("input.keyboard", "Fastest keyboard repeat",
                "Shortest delay before a held key repeats (0) and the fastest repeat rate (31). Windows has no setting for keyboard polling rate; that's set by the keyboard's firmware.",
                Input, Safe)
            .Hkcu(@"Control Panel\Keyboard", "KeyboardDelay", "0")
            .Hkcu(@"Control Panel\Keyboard", "KeyboardSpeed", "31")
            .Needs(RestartKind.SignOut)
            .In(Preset.Gaming | Preset.MaxPerformance);

        yield return new Tweak("input.accessibility", "Disable Sticky/Filter/Toggle Keys shortcuts",
                "Pressing Shift five times or holding it in a game no longer pops up the accessibility prompts. The features themselves still work from Settings.",
                Input, Safe)
            .Hkcu(@"Control Panel\Accessibility\StickyKeys", "Flags", "506")
            .Hkcu(@"Control Panel\Accessibility\Keyboard Response", "Flags", "122")
            .Hkcu(@"Control Panel\Accessibility\ToggleKeys", "Flags", "58")
            .In(Preset.Gaming | Preset.Balanced | Preset.MaxPerformance);

        yield return new Tweak("input.queue", "Smaller mouse and keyboard input buffers",
                "Sets the mouclass/kbdclass data queue sizes to 20 (default 100). A popular latency tweak with no measured benefit; included for completeness. Very fast polling mice (4–8 kHz) can drop input with a small queue.",
                Input, TweakRisk.Advanced)
            .Hklm(@"SYSTEM\CurrentControlSet\Services\mouclass\Parameters", "MouseDataQueueSize", 20)
            .Hklm(@"SYSTEM\CurrentControlSet\Services\kbdclass\Parameters", "KeyboardDataQueueSize", 20)
            .Needs(RestartKind.Reboot);

        yield return new Tweak("input.hover", "Faster hover response",
                "Lowers the hover delay (MouseHoverTime) from 400 ms to 10 ms, so tooltips and taskbar previews react instantly.",
                Input, Safe)
            .Hkcu(Mouse, "MouseHoverTime", "10")
            .Needs(RestartKind.SignOut)
            .In(Preset.MaxPerformance);
    }
}
