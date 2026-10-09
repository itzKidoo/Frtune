# Architecture

## Stack: C# 12 + .NET 8 + WPF (MVVM)

I chose WPF over WinUI 3 and Tauri:

| | WPF (.NET 8) | WinUI 3 | Tauri (Rust + React) |
|---|---|---|---|
| Single self-contained `.exe` | Yes (`PublishSingleFile`) | Awkward (Windows App SDK runtime, MSIX-oriented) | Yes, but needs WebView2 |
| Registry, services, WMI, P/Invoke | First-class in .NET | First-class in .NET | Rust crates + IPC bridge to the UI |
| Custom frameless dark UI | Full control with `WindowChrome` and templates | Good, but less templating freedom | Easy (HTML/CSS) |
| Builds with one command | `dotnet publish` | Needs the Windows App SDK tooling | Needs Rust and Node toolchains |
| Unit-testing the engine | xUnit, same language | Same | Rust tests, but two languages |

An optimizer is mostly registry, service and command plumbing, so having the system APIs and the UI in one language matters more than web-style styling. WPF's templating is enough for a Hone-style UI.

## Projects

```
Frtune.sln
├─ src/Frtune.Core        net8.0, platform-neutral, no UI. Everything here is unit tested.
│  ├─ Abstractions/       IRegistry, IServiceManager, ICommandRunner, IRestorePointService, IBackupExporter, IClock
│  ├─ Changes/            Atomic reversible operations: RegistryChange, RegistryListChange, ServiceChange,
│  │                      ScheduledTaskChange, PowerSettingChange, CommandChange
│  ├─ Tweaks/             ITweak, Tweak (fluent builder), TweakContext
│  │  └─ Catalog/         The tweak definitions, one file per page
│  ├─ Engine/             TweakEngine, JsonBackupStore, ChangeLog, Presets, ProfileFile, RestartDetector
│  ├─ Model/              SystemProfile, enums (category, risk, preset, restart kind, GPU vendor)
│  └─ Tools/              Cleaner, file finder, benchmark, DNS providers, health score, Appx catalog
├─ src/Frtune.Windows     net8.0-windows. Real implementations of the abstractions plus Windows-only tools
│  ├─ Platform/           WindowsRegistry (64-bit view), WindowsServiceManager (sc.exe + WMI),
│  │                      ProcessCommandRunner, RestorePointService, RegExportBackupExporter
│  ├─ Info/               SystemInfoProvider (WMI + registry detection), PerformanceMonitor (perf counters)
│  └─ Tools/              DNS/network tools, Appx removal, startup manager, process tools, game profiles,
│                         scheduled clean, Recycle Bin / Windows Update cache / Windows.old cleaner targets
├─ src/Frtune.App         WPF UI (MVVM with CommunityToolkit.Mvvm), app.manifest (requireAdministrator)
│  ├─ Themes/             Colors.xaml (tokens + accent), Controls.xaml (all control styles)
│  ├─ ViewModels/         One per page; MainViewModel owns navigation, presets, pending changes and Apply
│  ├─ Views/              One UserControl per page; Templates.xaml maps view models to views
│  ├─ Controls/           Sparkline and RingGauge (drawn in OnRender, so no chart library is needed)
│  └─ Services/           Composition root (AppServices), settings, theme, toasts, dialogs
├─ tests/Frtune.Core.Tests  xUnit tests against an in-memory registry, service manager and command runner
└─ tools/Frtune.DocGen    Generates TWEAKS.md from the catalog
```

The dependency direction is `App → Windows → Core`. Core never references Windows APIs, which is why every tweak can be tested on any OS.

## How a tweak is modelled

A tweak is a list of **changes**. Each `IChange` can:

- `Capture()`: read the current state and serialise it (`null` means "didn't exist").
- `Apply()`: make the change.
- `Restore(captured)`: write the captured state back exactly.
- `IsApplied()`: report whether the system is currently in the tweaked state.

`Tweak` (implements `ITweak`: `Id, Name, Description, Category, Risk, IsApplicable(), IsApplied(), Apply(), Revert()`)
composes changes and adds metadata: presets, restart requirement, warning text, applicability rule
(for example "Windows 11 only" or "NVIDIA/AMD GPU"), and a per-PC recommendation.

Some changes are only known at runtime, such as one registry key per active network adapter, or a value computed from RAM size. `AddDynamic` covers these.

## Safety pipeline (TweakEngine.ExecuteAsync)

```
User presses Apply
  → Plan(): list every value/service/command per tweak     → ConfirmPlanWindow (Advanced items need an extra tick)
  → System Restore point (Checkpoint-Computer)              → if it fails: abort, or ask to continue
  → Raw export: reg.exe export per key + sc qc per service → backups\raw\<timestamp>\*.reg, services.txt
  → for each tweak:
       snapshot = Capture()  → saved to backups\snapshots.json BEFORE changing anything
       ApplyCaptured()       → if any change throws, the ones already done are restored from the snapshot
       ChangeLog.Add()       → changelog.json
  → restart requirement aggregated → banner + "Restart now"
```

Turning a tweak off calls `Revert(snapshot)`, which writes the saved values back in reverse order, then deletes the snapshot.
If no snapshot exists (Windows was already in that state before frtune ran), the toggle is locked and shows
"Already set on this PC": frtune never guesses an original value.

A unit test checks that no two catalog tweaks touch the same registry value, because overlapping tweaks would capture each other's values instead of the original.

## Threading

Everything that touches the system runs on the thread pool (`Task.Run`). The UI thread only updates view models.
Status checks run at most 6 at a time, because some tweaks need PowerShell, which takes about 0.5 s each.
Live graphs sample once per second on a background thread and draw with a single `StreamGeometry`.

## Data on disk (`%LocalAppData%\frtune`)

| File | Purpose |
|---|---|
| `backups\snapshots.json` | Original values for every applied tweak (used by revert and "Restore everything") |
| `backups\raw\<time>-before-apply\` | `.reg` exports and `services.txt`, so you can restore by hand without the app |
| `backups\dns-original.json`, `services-original.json` | Originals for the DNS switcher and manual service changes |
| `changelog.json` | Every action, with timestamps |
| `settings.json`, `benchmarks.json` | Preferences and benchmark history |

## Privacy

There is no telemetry and no update check. The only network traffic is ICMP pings when you press "Test latency", and the DNS servers you choose to switch to.
