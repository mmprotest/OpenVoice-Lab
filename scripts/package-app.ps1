$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$solution = Join-Path $repoRoot "OpenVoiceLab.sln"
$project = Join-Path $repoRoot "src/OpenVoiceLab.App/OpenVoiceLab.App.csproj"
$publishDir = Join-Path $repoRoot "artifacts/app"
$exePath = Join-Path $publishDir "OpenVoiceLab.App.exe"

dotnet restore $solution
dotnet publish $project -c Release -o $publishDir

if (!(Test-Path $exePath)) {
    throw "OpenVoiceLab.App.exe not found in $publishDir"
}

Write-Host "App published to $publishDir"
