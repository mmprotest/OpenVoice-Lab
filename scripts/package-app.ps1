$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$solution = Join-Path $repoRoot "OpenVoiceLab.sln"
$project = Join-Path $repoRoot "src/OpenVoiceLab.App/OpenVoiceLab.App.csproj"
$artifactsDir = Join-Path $repoRoot "artifacts"
$publishDir = Join-Path $artifactsDir "app"
$exePath = Join-Path $publishDir "OpenVoiceLab.App.exe"
$binlogPath = Join-Path $artifactsDir "app-publish.binlog"

dotnet restore $solution
New-Item -ItemType Directory -Path $artifactsDir -Force | Out-Null
dotnet publish $project -c Release -r win-x64 --self-contained false -o $publishDir -v normal -bl:$binlogPath

if (!(Test-Path $exePath)) {
    throw "OpenVoiceLab.App.exe not found in $publishDir"
}

Write-Host "App published to $publishDir"
