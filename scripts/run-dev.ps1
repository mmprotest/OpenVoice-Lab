$ErrorActionPreference = "Stop"

Write-Host "Preparing worker environment"
Push-Location worker
python -m venv .venv
.\.venv\Scripts\Activate.ps1
pip install -r requirements.txt
Pop-Location

Write-Host "Starting WinUI app"
dotnet build .\OpenVoiceLab.sln
Start-Process .\src\OpenVoiceLab.App\bin\Debug\net8.0-windows10.0.19041.0\OpenVoiceLab.App.exe
