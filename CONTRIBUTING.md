# Adding a tweak

Most tweaks need no new code, only a new entry in a catalog file.

## 1. Pick the page

Catalog files live in `src/Frtune.Core/Tweaks/Catalog/`, one per sidebar page (`GamingTweaks.cs`, `NetworkTweaks.cs`, …).
The tweak's `TweakCategory` decides which page shows it.

## 2. Describe it

```csharp
yield return new Tweak(
        "visuals.snapflyout",                       // unique id: <page>.<name>, never change it once shipped
        "Disable the Snap layouts flyout",          // card title
        "Hovering the maximise button no longer opens the Snap layouts menu.",  // plain-language description
        TweakCategory.Visuals,
        TweakRisk.Safe)                             // Safe | Moderate | Advanced
    .Hkcu(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "EnableSnapAssistFlyout", 0)
    .When(s => s.IsWindows11, "Windows 11 only")    // hidden on PCs where it doesn't apply
    .Needs(RestartKind.ExplorerRestart)             // shown on the card and in the restart banner
    .In(Preset.Balanced | Preset.LowEnd);           // presets that switch it on
```

Building blocks (all are reversible automatically):

| Helper | What it does |
|---|---|
| `.Hklm(key, name, uint)` / `.Hkcu(key, name, uint or string)` | Set a DWORD or REG_SZ value |
| `.Reg(hive, key, name, RegValue.X(...))` | Any value type (`QWord`, `Expand`, `Multi`, `Bin`, `FromHex`). Pass `null` to delete the value |
| `.Add(new RegistryChange(hive, key, name, ctx => ..., "description"))` | Value computed at apply time (RAM size, dates…) |
| `.Add(new RegistryListChange(...))` | Edit one `Key=Value;` entry inside a semicolon-separated string |
| `.Service(name, ServiceStartType.Disabled)` | Startup type of a service (missing services are skipped) |
| `.Task(@"\Path\Task", enable: false)` | Enable or disable a scheduled task |
| `.Power(subgroupGuid, settingGuid, label, ac, dc?)` | A setting of the active power plan |
| `.AddDynamic(ctx => ...)` | Changes only known at runtime (per adapter, per interface) |
| `.Add(new CommandChange(...))` / `Helpers.NetshGlobal` / `Helpers.PsBool` | Anything driven by a command. You supply capture, apply, restore and is-applied |
| `.Warn("...")` | Extra warning in the confirmation dialog (required for Advanced) |
| `.Recommend(s => reason or null)` | Shows "For your PC" with the reason on matching hardware |

## 3. Rules

- **Reversible or it doesn't ship.** If you write a `CommandChange`, `capture` must return enough to restore the exact original, not a default.
- **One owner per value.** Two tweaks must not touch the same registry value; a unit test enforces this.
- **Advanced means Advanced.** Anything that weakens security (Defender, firewall, mitigations, VBS, updates) goes in `AdvancedTweaks.cs` with `TweakRisk.Advanced`, a `.Warn(...)`, and no presets. Tests enforce this as well.
- **Be honest in the description.** If the benefit is unproven or small, say so; see `input.queue` and `net.qos` for examples.
- **Prefer `PowerShell` reads over parsing `netsh` or `powercfg` text**, because their output is translated on non-English Windows.

## 4. Test and document

```powershell
dotnet test                                                # includes an apply → revert round-trip of every registry/service tweak
dotnet run --project tools/Frtune.DocGen > TWEAKS.md       # regenerate the reference
```

Registry- and service-only tweaks are tested automatically. For a `CommandChange`, add a test in
`tests/Frtune.Core.Tests` using `FakeCommands.Handler` to simulate the command output.
