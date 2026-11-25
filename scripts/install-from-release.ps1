param(
    [ValidateSet("x64", "ARM64")]
    [string]$Architecture = $(if ($env:PROCESSOR_ARCHITECTURE -eq "ARM64") { "ARM64" } else { "x64" })
)

$ErrorActionPreference = "Stop"
$Dir = $PSScriptRoot
$Cer = Join-Path $Dir "PomodoroCmdPalDev.cer"
$Msix = Get-ChildItem -Path $Dir -Filter "*$Architecture*.msix" | Select-Object -First 1

if (-not $Msix) {
    $Msix = Get-ChildItem -Path $Dir -Filter "*.msix" | Select-Object -First 1
}

if (-not $Msix) {
    throw "No .msix file found in $Dir"
}

if (Test-Path $Cer) {
    Write-Host "Importing signing certificate into LocalMachine\TrustedPeople..." -ForegroundColor Cyan
    try {
        Import-Certificate -FilePath $Cer -CertStoreLocation "Cert:\LocalMachine\TrustedPeople" | Out-Null
    } catch {
        Write-Warning "Run PowerShell as Administrator once to trust $Cer, then re-run this script."
        throw
    }
}

Write-Host "Installing $($Msix.Name)..." -ForegroundColor Cyan
Add-AppxPackage -Path $Msix.FullName -ForceApplicationShutdown

Write-Host ""
Write-Host "Done! Open PowerToys Settings -> Command Palette -> Dock and pin 'Pomodoro Timer'." -ForegroundColor Green
