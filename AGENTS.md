# AGENTS.md — PomodoroCmdPal

## Overview

**PomodoroCmdPal** is a native **Windows 11 PowerToys Command Palette & Dock** Pomodoro timer extension packaged as an MSIX `com.microsoft.commandpalette` AppExtension. It renders a live anti-aliased color-coded progress ring (`MM:SS` + cycle subtitle) directly in the Command Palette Dock, provides one-click Start/Pause and right-click (`MoreCommands`) controls, exposes quick presets (Classic 25/5/15, Deep Work 50/10/20, Sprint 15/3/10) and native extension settings, sends interactive Windows 11 toast notifications, and persists daily focus stats.

## Tech Stack & Requirements

- **Language & Runtime**: C# (`LangVersion=latest`), `.NET 10` (`net10.0` for Core/Tests; `net10.0-windows10.0.26100.0`, `TargetPlatformMinVersion=10.0.19041.0` for Extension)
- **Extension Host & Packaging**: `Microsoft.CommandPalette.Extensions 0.12.260812002`, `Microsoft.WindowsAppSDK 1.8.260921001`, `Shmuelie.WinRTServer 2.2.1` (out-of-process COM server), MSIX (`WindowsPackageType=MSIX`, self-contained `win-x64` / `win-arm64`)
- **Runtime Target**: Windows 11 (x64 or ARM64), Microsoft PowerToys `0.98+` with Command Palette & Dock enabled
- **Tests**: xUnit `2.9.2` (`PomodoroCmdPal.Core.Tests`)

## Repository Structure

```text
pomodoro-cmdpal/
├── PomodoroCmdPal.sln                       # Solution: Core, Extension, Core.Tests
├── README.md                                # Features, install, build instructions
├── scripts/
│   ├── build-and-install.ps1                # Runs tests, self-signs cert, builds MSIX, installs package
│   └── install-from-release.ps1             # Installs release MSIX + dev certificate (Admin PowerShell)
├── src/
│   ├── PomodoroCmdPal.Core/                 # Platform-independent .NET 10 core library
│   │   ├── PomodoroEngine.cs                # Timer state machine, phases, cycles, presets, +5m, auto-start
│   │   ├── PomodoroModels.cs                # Phases, settings, presets, snapshots
│   │   ├── ProgressRingPngRenderer.cs       # Anti-aliased color-coded PNG progress ring generator
│   │   └── StatsStore.cs                    # Daily stats persistence (%LOCALAPPDATA%\PomodoroCmdPal\stats.json)
│   └── PomodoroCmdPal/                      # MSIX Command Palette extension (WinExe COM server)
│       ├── Program.cs                       # [MTAThread] entrypoint (-RegisterProcessAsComServer)
│       ├── PomodoroExtension.cs             # IExtension COM class (CLSID 7f4c8b2e-9a3d-4e61-b5c2-1d8a0f93e4a1)
│       ├── PomodoroCommandsProvider.cs      # Top-level Command Palette & Dock band provider
│       ├── Dock/
│       │   ├── PomodoroDockBandItem.cs      # Live Dock band item (left-click toggle, right-click MoreCommands)
│       │   └── DockIconRenderer.cs          # Adapts ProgressRingPngRenderer bytes to Command Palette icons
│       ├── Pages/PomodoroPage.cs            # Full Pomodoro Controls list page (presets, phase switches, stats)
│       ├── Commands/PomodoroActionCommand.cs# Invokable action commands
│       ├── Services/
│       │   ├── PomodoroSettingsManager.cs   # Native Command Palette extension settings schema & binding
│       │   └── NotificationService.cs       # Interactive Windows 11 toast notifications & completion chime
│       ├── Package.appxmanifest             # MSIX identity, COM ExeServer, com.microsoft.commandpalette AppExtension
│       ├── Properties/PublishProfiles/      # win-x64.pubxml, win-ARM64.pubxml
│       └── Assets/                          # MSIX tile/logo assets
└── tests/
    └── PomodoroCmdPal.Core.Tests/
        └── PomodoroEngineTests.cs           # xUnit tests for PomodoroEngine, presets, ring renderer, stats
```

## Architecture & Key Flows

- **COM Activation (`Program.cs` → `PomodoroExtension.cs`)**: PowerToys Command Palette launches `PomodoroCmdPal.exe -RegisterProcessAsComServer` via the MSIX `windows.comServer` declaration (`CLSID 7f4c8b2e-9a3d-4e61-b5c2-1d8a0f93e4a1`). `Program.Main` registers `PomodoroExtension` as `IExtension` via `Shmuelie.WinRTServer.ComServer` and blocks until disposed.
- **Provider & Dock Band**: `PomodoroCommandsProvider` wires `PomodoroEngine`, `PomodoroSettingsManager`, `StatsStore`, and `NotificationService`, exposing both `PomodoroDockBandItem` (for the PowerToys Dock) and `PomodoroPage` (for Command Palette search/controls).
- **Progress Ring Rendering**: `ProgressRingPngRenderer` in `PomodoroCmdPal.Core` generates anti-aliased PNG progress rings (Coral Red = Focus, Mint Teal = Short Break, Calm Indigo = Long Break, Warm Amber = Paused) without UI-thread dependencies; `DockIconRenderer` feeds them into the Dock band icon stream.
- **Interactive Toasts**: `NotificationService` posts Windows 11 toast notifications on phase completion with action buttons (**Start Break**, **Start Focus**, **+5 min Focus**) routed back into `PomodoroEngine`.

## Development & Commands

```powershell
# Run core unit tests (cross-platform .NET 10, no Windows SDK required for Core.Tests)
dotnet test tests/PomodoroCmdPal.Core.Tests/PomodoroCmdPal.Core.Tests.csproj

# Build the MSIX extension (Windows 11 with .NET 10 + Windows SDK)
dotnet build src/PomodoroCmdPal/PomodoroCmdPal.csproj -c Release -p:Platform=x64

# Full local loop: test, self-sign, package MSIX, and install (Windows PowerShell)
.\scripts\build-and-install.ps1
```

## Configuration & Environment

- **Daily Focus Stats**: Persisted to `%LOCALAPPDATA%\PomodoroCmdPal\stats.json` (auto-resets daily).
- **Extension Settings**: Managed by PowerToys Command Palette via `PomodoroSettingsManager` (focus/short/long durations, cycles before long break, auto-start next phase, completion chime).
- **MSIX Signing**: Local builds use a self-signed certificate matching `Publisher="CN=PomodoroCmdPalDev"` in `Package.appxmanifest` (handled by `scripts/build-and-install.ps1`). Never commit private `.pfx` keys.

## Conventions & Gotchas

- **Keep Core Platform-Independent**: `PomodoroCmdPal.Core` targets plain `net10.0` so timer logic, stats, and PNG ring rendering stay unit-testable without Windows/WinRT dependencies.
- **COM CLSID Consistency**: The GUID `7f4c8b2e-9a3d-4e61-b5c2-1d8a0f93e4a1` must match across `Package.appxmanifest` (`windows.comServer` and `CmdPalProvider/Activation/CreateInstance`) and `PomodoroExtension.cs`.
- **WinRT / CsWinRT Build Flags**: `PomodoroCmdPal.csproj` sets `<CsWinRTAotOptimizerEnabled>false</CsWinRTAotOptimizerEnabled>` and `<GenerateAppxPackageOnBuild>false</GenerateAppxPackageOnBuild>`; use the publish profiles or `scripts/build-and-install.ps1` to produce MSIX packages.
- **Always Pass Platform**: Specify `-p:Platform=x64` or `-p:Platform=ARM64` when building `src/PomodoroCmdPal/PomodoroCmdPal.csproj`.
