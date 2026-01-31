$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$appExe = Join-Path $repoRoot "artifacts/app/OpenVoiceLab.App.exe"
$workerDir = Join-Path $repoRoot "artifacts/app/worker"
$workerExe = Join-Path $workerDir "OpenVoiceLab.Worker.exe"
$packagedWorker = Join-Path $repoRoot "artifacts/worker/OpenVoiceLab.Worker"

if (!(Test-Path $appExe)) {
    throw "App executable not found at $appExe. Run scripts/package-app.ps1 first."
}
if (!(Test-Path $workerExe)) {
    if (!(Test-Path $packagedWorker)) {
        throw "Worker package not found at $packagedWorker. Run scripts/package-worker.ps1 first."
    }
    New-Item -ItemType Directory -Force -Path $workerDir | Out-Null
    Copy-Item "$packagedWorker/*" $workerDir -Recurse -Force
}

Start-Process -FilePath $appExe -WorkingDirectory (Split-Path $appExe)
Write-Host "Launched OpenVoiceLab.App.exe from installed layout."
