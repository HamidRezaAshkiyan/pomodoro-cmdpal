# PomodoroCmdPal

A minimal **Pomodoro timer** built as a native **Windows 11 PowerToys Command Palette Dock** extension.

## Features

- **Animated Progress Ring in the Dock:** Anti-aliased progress ring with rounded arc end-caps and soft radial glow — Coral Red (`Focus`), Mint Teal (`Short Break`), Calm Indigo (`Long Break`), and Warm Amber (`Paused`).
- **Micro-Animations:** 5-frame expanding halo + checkmark completion celebration burst at `00:00`, plus a 1-second smooth ring drain and breathing urgency pulse during the final 60 seconds.
- **Minimal Cycle Dots & Focus Labels:** Displays `Focus • ●●○○` (or your active task label like `API refactor • ●●○○`) directly in the Dock subtitle.
- **Quick Custom Duration & Label Search:** Open **Pomodoro Timer** in Command Palette and type `15`, `45m`, `15 code review`, or any task name to start a custom focus session or label your Dock pill immediately.
- **One-Click & In-Flow Controls:** Left-click the Dock band to **Start / Pause**; right-click (`MoreCommands`) for **`+5 min`**, **Reset**, **Skip Phase**, direct phase switches, and **Clear Focus Label**.
- **Native Command Palette Settings & Presets:** Custom durations, cycles, auto-start next phase, sound toggle, cycle dots toggle, and **Auto-Pause on PC Lock (`Win + L`)**, plus **Classic (25/5/15)**, **Deep Work (50/10/20)**, and **Sprint (15/3/10)** presets.
- **Interactive Windows 11 Toast Notifications:** Click **Start Break**, **Start Focus**, or **+5 min Focus** directly from the completion notification.
- **Daily Focus Stats & Streak:** Tracks completed focus sessions, total focus time today, and consecutive-day focus streaks (`%LOCALAPPDATA%\PomodoroCmdPal\stats.json`).

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

## Publishing to Microsoft Store / Command Palette Gallery / WinGet

Ready-to-submit manifests and metadata live under `distribution/`. See [PUBLISHING.md](./PUBLISHING.md) for the exact steps (including free Microsoft Store signing for `microsoft/CmdPal-Extensions`).

## Build from Source (Windows 11)

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) and Windows 10/11 SDK.

```powershell
.\scripts\build-and-install.ps1
```
