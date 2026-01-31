# OpenVoice Lab

OpenVoice Lab is a Windows desktop app for local text-to-speech (TTS). It pairs a WinUI
front-end with a bundled worker that runs entirely on your machine—no cloud account or
Python install required.

## What it does
- Generate speech from text using local TTS models.
- Manage voices, projects, and pronunciation profiles.
- Download and store models on your PC for offline use.

## System requirements
- Windows 11 (x64)
- 10+ GB free disk space for models (more if you keep multiple sizes)
- Optional: NVIDIA GPU with compatible CUDA drivers for faster generation

## Install (Windows)
1. Download the latest installer: `OpenVoiceLab-Setup.exe`.
2. Run the installer (no admin rights required).
3. Launch **OpenVoice Lab** from the Start menu.

By default, the app installs to:
```
%LOCALAPPDATA%\Programs\OpenVoiceLab
```

App data (models, voices, logs) is stored in:
```
%LOCALAPPDATA%\OpenVoiceLab
```

## First run
1. Open the app.
2. Go to **Models** and download a model.
3. Go to **Playground** to generate speech.

The worker runtime starts automatically in the background and uses a local-only
connection (127.0.0.1). You never need to open a terminal or manage Python.

## Uninstall
- Use **Settings → Apps → Installed apps → OpenVoice Lab → Uninstall**.
- The uninstaller includes an option to remove user data at
  `%LOCALAPPDATA%\OpenVoiceLab`.

## Troubleshooting
- **App says worker is unavailable**: restart the app to relaunch the worker.
- **Slow generation**: ensure you downloaded a model and that your GPU drivers are up to date.
- **Disk usage is high**: remove unused models from the **Models** page or delete
  `%LOCALAPPDATA%\OpenVoiceLab\models\`.

## For developers
See the scripts in `scripts/` for building and packaging the app and worker.
