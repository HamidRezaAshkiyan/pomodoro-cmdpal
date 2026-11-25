# PomodoroCmdPal

A minimal **Pomodoro timer** built as a native **Windows 11 PowerToys Command Palette Dock** extension.

## Features

- **Live Color-Coded Progress Ring in the Dock:** Anti-aliased progress ring that drains smoothly as your timer counts down — Coral Red (`Focus`), Mint Teal (`Short Break`), Calm Indigo (`Long Break`), and Warm Amber (`Paused`) — alongside a crisp `MM:SS` countdown and compact cycle subtitle (`Focus • 1/4`).
- **One-Click & In-Flow Controls:** Left-click the Dock band to **Start / Pause** immediately; right-click (`MoreCommands`) for **`+5 min`**, **Reset Timer**, **Skip Phase**, direct phase switches, and **Open Pomodoro Controls**.
- **Native Command Palette Settings:** Configure Focus / Short Break / Long Break durations, cycles before long break, auto-start next phase, and completion chime right inside PowerToys Command Palette's extension settings.
- **Quick Presets:** Switch instantly between **Classic (25/5/15)**, **Deep Work (50/10/20)**, and **Sprint (15/3/10)**.
- **Interactive Windows 11 Toast Notifications:** Click **Start Break**, **Start Focus**, or **+5 min Focus** directly from the completion notification.
- **Daily Focus Stats:** Tracks completed focus sessions and total focus time today (persisted in `%LOCALAPPDATA%\PomodoroCmdPal\stats.json`, auto-resets daily).

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

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) and Windows 10/11 SDK.

```powershell
# Run tests, self-sign, build MSIX, and install
.\scripts\build-and-install.ps1
```
