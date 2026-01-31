$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")

& (Join-Path $repoRoot "scripts/package-worker.ps1")
& (Join-Path $repoRoot "scripts/package-app.ps1")
& (Join-Path $repoRoot "scripts/build-installer.ps1")

$installerPath = Join-Path $repoRoot "artifacts/installer/OpenVoiceLab-Setup.exe"
if (!(Test-Path $installerPath)) {
    throw "Installer not found at $installerPath"
}

Write-Host "Packaging complete. Installer at $installerPath"
