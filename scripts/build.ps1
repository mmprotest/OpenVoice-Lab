$ErrorActionPreference = "Stop"

Write-Host "Running Python tests"
Push-Location worker
python -m venv .venv
.\.venv\Scripts\Activate.ps1
pip install -r requirements.txt
pytest
Pop-Location

Write-Host "Building .NET solution"
dotnet build .\OpenVoiceLab.sln
