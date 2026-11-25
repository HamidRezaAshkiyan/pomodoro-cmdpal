param(
    [ValidateSet("x64", "ARM64")]
    [string]$Platform = "x64",
    [string]$Configuration = "Release",
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"
$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$ProjectPath = Join-Path $RepoRoot "src\PomodoroCmdPal\PomodoroCmdPal.csproj"
$TestProjectPath = Join-Path $RepoRoot "tests\PomodoroCmdPal.Core.Tests\PomodoroCmdPal.Core.Tests.csproj"
$CertDir = Join-Path $RepoRoot ".certs"
$CertPfx = Join-Path $CertDir "PomodoroCmdPalDev.pfx"
$CertCer = Join-Path $CertDir "PomodoroCmdPalDev.cer"
$CertPassword = ConvertTo-SecureString -String "PomodoroCmdPalDev!" -Force -AsPlainText

if (-not $SkipTests) {
    Write-Host "Running unit tests..." -ForegroundColor Cyan
    dotnet test $TestProjectPath -c $Configuration
}

$cert = Get-ChildItem "Cert:\CurrentUser\My" |
    Where-Object { $_.Subject -eq "CN=PomodoroCmdPalDev" } |
    Sort-Object NotAfter -Descending |
    Select-Object -First 1

if (-not $cert) {
    Write-Host "Creating self-signed development certificate..." -ForegroundColor Cyan
    New-Item -ItemType Directory -Force -Path $CertDir | Out-Null
    $cert = New-SelfSignedCertificate `
        -Type Custom `
        -Subject "CN=PomodoroCmdPalDev" `
        -KeyUsage DigitalSignature `
        -FriendlyName "PomodoroCmdPal Dev Certificate" `
        -CertStoreLocation "Cert:\CurrentUser\My" `
        -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")

    Export-Certificate -Cert $cert -FilePath $CertCer | Out-Null
    Write-Host "Importing certificate to LocalMachine\TrustedPeople (requires elevation if not already trusted)..." -ForegroundColor Yellow
    try {
        Import-Certificate -FilePath $CertCer -CertStoreLocation "Cert:\LocalMachine\TrustedPeople" | Out-Null
    } catch {
        Write-Warning "Could not trust certificate automatically. Run PowerShell as Administrator and import $CertCer into LocalMachine\TrustedPeople."
    }
}

Write-Host "Building MSIX package ($Platform / $Configuration)..." -ForegroundColor Cyan
dotnet build $ProjectPath `
    -c $Configuration `
    -p:Platform=$Platform `
    -p:GenerateAppxPackageOnBuild=true `
    -p:AppxPackageSigningEnabled=true `
    -p:PackageCertificateThumbprint="$($cert.Thumbprint)"

$msix = Get-ChildItem -Path (Join-Path $RepoRoot "src\PomodoroCmdPal\AppPackages") -Filter "*.msix" -Recurse |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if (-not $msix) {
    throw "MSIX package was not found after build."
}

Write-Host "Installing $($msix.FullName)..." -ForegroundColor Cyan
Add-AppxPackage -Path $msix.FullName -ForceApplicationShutdown

Write-Host ""
Write-Host "Installed! Next steps:" -ForegroundColor Green
Write-Host "  1. Open PowerToys Settings -> Command Palette -> Dock"
Write-Host "  2. Enable the Dock (if not already enabled)"
Write-Host "  3. Add 'Pomodoro Timer' to Start, Center, or End"
