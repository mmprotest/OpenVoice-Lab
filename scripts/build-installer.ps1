$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$appDir = Join-Path $repoRoot "artifacts/app"
$workerDir = Join-Path $repoRoot "artifacts/worker/OpenVoiceLab.Worker"
$installerScript = Join-Path $repoRoot "installer/OpenVoiceLab.iss"
$outputInstaller = Join-Path $repoRoot "artifacts/installer/OpenVoiceLab-Setup.exe"

if (!(Test-Path $appDir)) {
    throw "Missing artifacts/app. Run scripts/package-app.ps1 first."
}
if (!(Test-Path $workerDir)) {
    throw "Missing artifacts/worker/OpenVoiceLab.Worker. Run scripts/package-worker.ps1 first."
}

$iscc = $null
$isccCommand = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
if ($isccCommand) {
    $iscc = $isccCommand.Source
}

if (-not $iscc) {
    $candidate = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
    if (Test-Path $candidate) {
        $iscc = $candidate
    }
}

if (-not $iscc) {
    $candidate = "C:\Program Files\Inno Setup 6\ISCC.exe"
    if (Test-Path $candidate) {
        $iscc = $candidate
    }
}

if (-not $iscc) {
    throw "ISCC.exe not found. Install Inno Setup 6 or add ISCC.exe to PATH. Expected path: C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
}

& $iscc $installerScript

if (!(Test-Path $outputInstaller)) {
    throw "Installer not found at $outputInstaller"
}

Write-Host "Installer built at $outputInstaller"
