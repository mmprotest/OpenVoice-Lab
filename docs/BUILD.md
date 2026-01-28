# OpenVoice Lab Build Guide

## Requirements
- Windows 11
- Python 3.11+
- .NET 8 SDK
- (Optional) NVIDIA CUDA drivers + matching PyTorch CUDA build

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
4. (Optional CUDA) install a CUDA build of torch:
   ```powershell
   pip install torch==2.3.1+cu121 --index-url https://download.pytorch.org/whl/cu121
   ```
   You can also use `worker/requirements-cuda.txt` as a reminder.
5. The WinUI app will start the worker automatically (via WorkerSupervisor). If you want to run
   the worker manually for debugging, you can still launch it:
   ```powershell
   uvicorn app:app --host 127.0.0.1 --port 23456
   ```

## Model download
Models are stored in `%LOCALAPPDATA%\OpenVoiceLab\models\`.
You can download models directly in the app (Models page) or via the worker API:
```powershell
curl -X POST http://127.0.0.1:23456/models/download -H "Content-Type: application/json" -d "{\"model_id\":\"Qwen/Qwen3-TTS-12Hz-0.6B-CustomVoice\"}"
```
Expect several GB of disk usage for all sizes.

## WinUI App
1. Open `OpenVoiceLab.sln` in Visual Studio 2022.
2. Set `OpenVoiceLab.App` as startup project.
3. Restore NuGet packages and run.

## Development Scripts
- `scripts/run-dev.ps1` prepares the worker venv/deps and starts the WinUI app (the app starts the worker).
- `scripts/build.ps1` builds the .NET solution and runs Python tests.
