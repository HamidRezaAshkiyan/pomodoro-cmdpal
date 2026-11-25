# PomodoroCmdPal

A minimal **Classic Pomodoro timer** (25m Focus / 5m Short Break / 15m Long Break) built as a native **Windows 11 PowerToys Command Palette Dock** extension.

## Features

- **Lives in the Command Palette Dock:** Displays a live `MM:SS` countdown (`25:00`) with a compact phase & cycle subtitle (`Focus • 1/4`, `Paused • Focus`, `Short Break • 1/4`) and dynamic Segoe Fluent status glyph.
- **One-Click Controls:** Left-click the Dock band to **Start / Pause** instantly.
- **Context Menu (`MoreCommands`):** Right-click the Dock band to **Reset Timer**, **Skip Phase**, jump directly to **Focus (25m)** / **Short Break (5m)** / **Long Break (15m)**, **Reset All Cycles**, or open the full **Pomodoro Controls** page.
- **Native Windows 11 Notifications:** Fires a Windows Toast Notification with a reminder chime whenever a Focus or Break session finishes.
- **Command Palette Search:** Also accessible anytime via `Win + Alt + Space` → `Pomodoro Timer`.

## Requirements

- **Windows 11** (x64 or ARM64)
- **Microsoft PowerToys 0.98+** with **Command Palette** and **Dock** enabled

## Install from Releases

1. Download `PomodoroCmdPal-x64.msix` (or `ARM64`), `PomodoroCmdPalDev.cer`, and `install-from-release.ps1` from the [Releases](../../releases) page into the same folder.
2. Open **PowerShell as Administrator** in that folder and run:
   ```powershell
   .\install-from-release.ps1
   ```
3. Open **PowerToys Settings → Command Palette → Dock**, enable the Dock, and pin **Pomodoro Timer** to your preferred Dock region (Start, Center, or End).

## Build from Source (Windows 11)

Requires [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) and Windows 10/11 SDK.

```powershell
# Run tests, self-sign, build MSIX, and install
.\scripts\build-and-install.ps1
```

## Project Structure

- `src/PomodoroCmdPal.Core/` — Pure .NET 9 Pomodoro state machine (`PomodoroEngine`, `PomodoroConfig`, `PomodoroSnapshot`).
- `src/PomodoroCmdPal/` — Out-of-process COM/WinRT PowerToys Command Palette extension (`ICommandProvider3.GetDockBands()`, `PomodoroDockBandItem`, `PomodoroPage`, `NotificationService`).
- `tests/PomodoroCmdPal.Core.Tests/` — xUnit tests covering full 4-cycle Pomodoro progression, pause/resume, reset, and phase switching.
- `.github/workflows/build-msix.yml` — Automated Windows CI build, test, and MSIX GitHub Release pipeline.
