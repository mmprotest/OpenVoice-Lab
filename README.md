# OpenVoice Lab

OpenVoice Lab pairs a WinUI desktop app with a Python worker to manage models, voices, and TTS
jobs locally.

## Table of contents
- [Requirements](#requirements)
- [Installation (Windows 11)](#installation-windows-11)
  - [Clone the repository](#clone-the-repository)
  - [Set up the Python worker](#set-up-the-python-worker)
  - [Run the WinUI app](#run-the-winui-app)
  - [Download models](#download-models)
- [Development scripts](#development-scripts)
- [Troubleshooting](#troubleshooting)

## Requirements
- Windows 11
- Python 3.11+
- .NET 8 SDK
- Visual Studio 2022 with the **.NET desktop development** workload
- (Optional) NVIDIA CUDA drivers + matching PyTorch CUDA build

## Installation (Windows 11)

### Clone the repository
```powershell
git clone https://github.com/YourOrg/OpenVoice-Lab.git
cd OpenVoice-Lab
```

### Set up the Python worker
Open PowerShell in `worker/`:
```powershell
cd worker
python -m venv .venv
.\.venv\Scripts\Activate.ps1
pip install -r requirements.txt
```

Optional CUDA-enabled PyTorch (if you have compatible NVIDIA drivers):
```powershell
pip install torch==2.3.1+cu121 --index-url https://download.pytorch.org/whl/cu121
```
You can also use `worker/requirements-cuda.txt` as a reminder of the CUDA build.

To run the worker manually for debugging:
```powershell
uvicorn app:app --host 127.0.0.1 --port 23456
```
The WinUI app normally starts the worker automatically via `WorkerSupervisor`.

### Run the WinUI app
1. Open `OpenVoiceLab.sln` in Visual Studio 2022.
2. Set `OpenVoiceLab.App` as the startup project.
3. Restore NuGet packages and run.

### Download models
Models are stored in `%LOCALAPPDATA%\OpenVoiceLab\models\`.

You can download models directly in the app (Models page) or via the worker API:
```powershell
curl -X POST http://127.0.0.1:23456/models/download `
  -H "Content-Type: application/json" `
  -d "{\"model_id\":\"Qwen/Qwen3-TTS-12Hz-0.6B-CustomVoice\"}"
```
Expect several GB of disk usage for all sizes.

## Development scripts
- `scripts/run-dev.ps1` prepares the worker venv/deps and starts the WinUI app (the app starts the worker).
- `scripts/build.ps1` builds the .NET solution and runs Python tests.

## Troubleshooting
- **Worker won’t start**: confirm the venv is active and `pip install -r requirements.txt` completed.
- **CUDA errors**: install the CPU-only torch build or match the CUDA wheel to your driver.
- **Models page is empty**: ensure the worker is running and reachable at `http://127.0.0.1:23456`.
