from __future__ import annotations

import json
import os
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Dict


@dataclass
class StoragePaths:
    root: Path
    models: Path
    voices: Path
    outputs: Path
    logs: Path
    pronunciation: Path
    history: Path
    projects: Path


def resolve_root() -> Path:
    local_appdata = os.environ.get("LOCALAPPDATA")
    if local_appdata:
        root = Path(local_appdata) / "OpenVoiceLab"
    else:
        root = Path.home() / ".local" / "share" / "OpenVoiceLab"
    root.mkdir(parents=True, exist_ok=True)
    return root


def get_paths() -> StoragePaths:
    root = resolve_root()
    models = root / "models"
    voices = root / "voices"
    outputs = root / "outputs"
    logs = root / "logs"
    pronunciation = root / "pronunciation"
    history = root / "history"
    projects = root / "projects"
    for path in [models, voices, outputs, logs, pronunciation, history, projects]:
        path.mkdir(parents=True, exist_ok=True)
    (voices / "user").mkdir(parents=True, exist_ok=True)
    return StoragePaths(
        root=root,
        models=models,
        voices=voices,
        outputs=outputs,
        logs=logs,
        pronunciation=pronunciation,
        history=history,
        projects=projects,
    )


def read_json(path: Path) -> Dict[str, Any]:
    if not path.exists():
        return {}
    return json.loads(path.read_text(encoding="utf-8"))


def write_json(path: Path, data: Dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, indent=2), encoding="utf-8")
