$ErrorActionPreference = "Stop"

$workerPort = 23456

Write-Host "Starting worker"
Push-Location worker
python -m venv .venv
.\.venv\Scripts\Activate.ps1
pip install -r requirements.txt
Start-Process uvicorn -ArgumentList "app:app --host 127.0.0.1 --port $workerPort"
Pop-Location

Write-Host "Starting WinUI app"
dotnet build .\OpenVoiceLab.sln
Start-Process .\src\OpenVoiceLab.App\bin\Debug\net8.0-windows10.0.19041.0\OpenVoiceLab.App.exe
