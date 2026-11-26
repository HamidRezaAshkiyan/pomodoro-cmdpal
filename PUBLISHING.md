# Publishing PomodoroCmdPal to Microsoft Store, Command Palette Gallery & WinGet

## Why a Self-Signed MSIX Cannot Go Straight to `microsoft/winget-pkgs`

Windows AppX deployment rejects any `.msix` signed with a self-signed certificate unless that `.cer` is manually imported into `LocalMachine\TrustedPeople` first. Because `winget install` runs unattended, **`microsoft/winget-pkgs` rejects self-signed MSIX packages**.

The two official, free paths for open-source developers are below.

---

## Option A — Microsoft Store + PowerToys Command Palette Extension Gallery (Recommended & Free)

Microsoft offers **free individual developer registration** in Microsoft Partner Center, and **automatically signs your MSIX** with Microsoft's globally trusted certificate.

1. **Register (free)** at [Microsoft Partner Center](https://partner.microsoft.com/dashboard) as an Individual developer.
2. **Reserve app name** (e.g., `Pomodoro Timer for Command Palette`) and note the assigned `Publisher` (`CN=...`) and `Identity Name`.
3. Update `src/PomodoroCmdPal/Package.appxmanifest` `<Identity Name="..." Publisher="CN=..." />` to match Partner Center and upload the `.msix` packages (`x64` & `ARM64`).
4. Once approved in the Microsoft Store, copy your Store Product ID (e.g., `9N...`) into `distribution/cmdpal-extensions/extension.json`.
5. Open a Pull Request to **[microsoft/CmdPal-Extensions](https://github.com/microsoft/CmdPal-Extensions)** adding your `extension.json` + icon — once merged, **Pomodoro Timer** appears directly inside PowerToys Command Palette's built-in Extension Gallery!

---

## Option B — WinGet (`microsoft/winget-pkgs`) with Trusted Code Signing

If you obtain a publicly trusted code-signing certificate (e.g., **SignPath Foundation**, which is free for open-source projects, or Azure Trusted Signing):

1. Sign `PomodoroCmdPal-x64.msix` and `PomodoroCmdPal-ARM64.msix` with your trusted cert in `.github/workflows/build-msix.yml`.
2. Copy the SHA256 hashes from the release's `SHA256SUMS.txt` into `distribution/winget/HamidRezaAshkiyan.PomodoroCmdPal.installer.yaml` (and set `SignatureSha256` via `winget hash -m <file.msix>`).
3. Submit via `wingetcreate submit` or a PR to **[microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs)**.
4. Note: `distribution/winget/HamidRezaAshkiyan.PomodoroCmdPal.locale.en-US.yaml` already includes the required `windows-commandpalette-extension` tag so PowerToys Command Palette can discover it natively via WinGet.
