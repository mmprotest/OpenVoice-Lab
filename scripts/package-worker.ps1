$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$workerDir = Join-Path $repoRoot "worker"
$venvDir = Join-Path $workerDir ".venv_build"
$python = Join-Path $venvDir "Scripts/python.exe"
$requirements = Join-Path $workerDir "requirements.txt"
$specPath = Join-Path $workerDir "packaging/pyinstaller.spec"
$distDir = Join-Path $repoRoot "artifacts/worker"
$workDir = Join-Path $workerDir "build"

if (!(Test-Path $python)) {
    Write-Host "Creating build venv at $venvDir"
    python -m venv $venvDir
}

& $python -m pip install --upgrade pip
& $python -m pip install -r $requirements
& $python -m pip install pyinstaller

if (Test-Path $distDir) {
    Remove-Item $distDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $distDir | Out-Null

& $python -m PyInstaller $specPath --distpath $distDir --workpath $workDir --clean

Copy-Item (Join-Path $workerDir "requirements*.txt") $distDir -Force

Write-Host "Worker packaged at $distDir"
