# OpenVoice Lab Build Guide

## Requirements
- Windows 11
- Python 3.11+
- .NET 8 SDK

## Worker Setup
1. Open PowerShell in `worker/`.
2. Create venv:
   ```powershell
   python -m venv .venv
   .\.venv\Scripts\Activate.ps1
   ```
3. Install dependencies:
   ```powershell
   pip install -r requirements.txt
   ```
4. Run the worker:
   ```powershell
   uvicorn app:app --host 127.0.0.1 --port 23456
   ```

## WinUI App
1. Open `OpenVoiceLab.sln` in Visual Studio 2022.
2. Set `OpenVoiceLab.App` as startup project.
3. Restore NuGet packages and run.

## Development Scripts
- `scripts/run-dev.ps1` starts the worker and the WinUI app in sequence.
- `scripts/build.ps1` builds the .NET solution and runs Python tests.
