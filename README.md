# frtune

A personal Windows 10/11 optimizer with a dark, Hone-inspired UI. Every change is listed before it runs, backed up first, and individually reversible.

> `frtune` is a placeholder name. To rename the app, change `AssemblyName` in `src/Frtune.App/Frtune.App.csproj` and the visible strings in `MainWindow.xaml`.

- **89 tweaks** across Gaming, Performance, Network, Privacy, Debloat, Services, Input, Power, Visuals and Advanced. Each one lists the exact registry values, services, tasks or commands it touches ([TWEAKS.md](TWEAKS.md)).
- **Presets**: Gaming, Balanced, Low-End PC, Privacy, Max Performance and Custom. A preset only pre-selects tweaks; nothing changes until you press **Apply** and confirm.
- **Hardware-aware**: detects Windows version and edition, laptop or desktop, CPU, GPU vendor, RAM, SSD/HDD, Wi-Fi, Bluetooth and printers. It hides tweaks that don't apply and marks the ones that suit your PC.
- **Tools**: live dashboard and health score, DNS switcher with latency test, network repair, Store-app remover with a protected list, full service manager, startup manager, cleaner with sizes and weekly auto-clean, large-file and duplicate finders, per-game GPU/CPU priority, power plan creator, hardware info, before/after benchmark, process "kill & boost", and profile export/import.

Tuning notes for an **i3-9100F + GTX 1650 Super + 16 GB** PC are in [docs/YOUR-PC.md](docs/YOUR-PC.md).

## Safety

1. **Before applying**, frtune creates a System Restore point and exports every registry key (`.reg`) and service setting it is about to touch.
2. **Before each tweak**, the current value of every target is saved to `snapshots.json`. Turning the tweak off writes those exact values back, and a key that didn't exist is removed again. If a tweak fails halfway, its finished steps are rolled back.
3. If Windows was already in the tweaked state, there is no original to restore, so frtune locks the toggle ("Already set on this PC") instead of guessing.
4. Every action goes to `changelog.json`, shown on the **Backups & Restore** page. That page also has **Restore everything**.
5. Defender, firewall, CPU mitigations, VBS/HVCI and Windows Update pause live only on the **Advanced** page. They are off by default, never part of a preset or an imported profile, and need an explicit "I understand" tick.
6. No telemetry, ads, update checks or accounts. The network is only used when you press a button that needs it (DNS latency test, DNS change).

Data lives in `%LocalAppData%\frtune`.

## Build and run

Requirements: Windows 10/11 and the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
# one command: build, run the tests, and publish one self-contained exe
.\build.ps1

# the result: no .NET install needed on the target PC; asks for admin rights (UAC) on launch
.\publish\frtune.exe
```

Or step by step:

```powershell
dotnet build                                                   # build everything
dotnet test                                                    # 119 unit tests (fake registry, no admin needed)
dotnet run --project src/Frtune.App                            # run from source (start the terminal as admin)
dotnet publish src/Frtune.App -c Release -r win-x64 -o publish # single-file frtune.exe
```

The core library and its tests are platform-neutral, so `dotnet test` also runs on Linux and macOS.

## Project layout

```
src/Frtune.Core       tweak engine, change types, tweak catalog, tools (platform-neutral, unit-tested)
src/Frtune.Windows    real registry/services/WMI/powercfg implementations and Windows tools
src/Frtune.App        WPF UI (MVVM), app.manifest with requireAdministrator
tests/                xUnit tests against an in-memory registry
tools/Frtune.DocGen   regenerates TWEAKS.md from the catalog
docs/                 architecture, page descriptions, tuning notes for your PC
```

- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md): stack choice, layers, safety pipeline
- [docs/PAGES.md](docs/PAGES.md): what each page shows
- [CONTRIBUTING.md](CONTRIBUTING.md): how to add a tweak (usually 5–10 lines)
- [TWEAKS.md](TWEAKS.md): every tweak and exactly what it changes

## Status

The solution compiles (including all XAML), the single-file `frtune.exe` publishes, and the engine and catalog tests pass. The UI hasn't been run on Windows yet. Please report any page that misbehaves.
