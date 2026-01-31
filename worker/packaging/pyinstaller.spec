# -*- mode: python ; coding: utf-8 -*-
from __future__ import annotations

from pathlib import Path

from PyInstaller.utils.hooks import collect_data_files, collect_submodules

block_cipher = None

project_dir = Path(__file__).resolve().parents[1]

hiddenimports = [
    "uvicorn",
    "uvicorn.logging",
    "uvicorn.lifespan",
    "uvicorn.protocols.http",
    "uvicorn.protocols.websockets",
    "fastapi",
    "starlette",
]
hiddenimports += collect_submodules("torch")
hiddenimports += collect_submodules("qwen_tts")
hiddenimports += collect_submodules("transformers")

datas = []
datas += collect_data_files("qwen_tts")
datas += collect_data_files("transformers")

a = Analysis(
    [str(project_dir / "worker_main.py")],
    pathex=[str(project_dir)],
    binaries=[],
    datas=datas,
    hiddenimports=hiddenimports,
    hookspath=[],
    hooksconfig={},
    runtime_hooks=[],
    excludes=[],
    win_no_prefer_redirects=False,
    win_private_assemblies=False,
    cipher=block_cipher,
    noarchive=False,
)

pyz = PYZ(a.pure, a.zipped_data, cipher=block_cipher)

exe = EXE(
    pyz,
    a.scripts,
    [],
    exclude_binaries=True,
    name="OpenVoiceLab.Worker",
    debug=False,
    bootloader_ignore_signals=False,
    strip=False,
    upx=True,
    console=True,
    disable_windowed_traceback=False,
)

coll = COLLECT(
    exe,
    a.binaries,
    a.zipfiles,
    a.datas,
    strip=False,
    upx=True,
    name="OpenVoiceLab.Worker",
)
