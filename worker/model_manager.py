from __future__ import annotations

import threading
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, List, Optional, Tuple

from huggingface_hub import HfApi, snapshot_download


MODEL_SPECS = {
    "custom_voice": {
        "0.6b": "Qwen/Qwen3-TTS-12Hz-0.6B-CustomVoice",
        "1.7b": "Qwen/Qwen3-TTS-12Hz-1.7B-CustomVoice",
    },
    "base": {
        "0.6b": "Qwen/Qwen3-TTS-12Hz-0.6B-Base",
        "1.7b": "Qwen/Qwen3-TTS-12Hz-1.7B-Base",
    },
    "voice_design": {
        "1.7b": "Qwen/Qwen3-TTS-12Hz-1.7B-VoiceDesign",
    },
}


@dataclass
class ModelDownloadState:
    model_id: str
    local_dir: Path
    total_bytes: int
    downloaded_bytes: int = 0
    status: str = "pending"
    error: Optional[str] = None


class ModelManager:
    def __init__(self, root: Path) -> None:
        self.root = root
        self.cache: Dict[str, Path] = {}
        self.downloads: Dict[str, ModelDownloadState] = {}
        self._download_lock = threading.Lock()
        self._hf_api = HfApi()

    def resolve_model_id(self, model_kind: str, size: str) -> str:
        normalized = size.lower().replace(" ", "")
        if normalized.endswith("b"):
            normalized = normalized
        if normalized not in MODEL_SPECS.get(model_kind, {}):
            raise ValueError(f"Unsupported model size {size} for {model_kind}")
        return MODEL_SPECS[model_kind][normalized]

    def list_required_models(self) -> List[Dict[str, str]]:
        models = []
        for kind, sizes in MODEL_SPECS.items():
            for size, model_id in sizes.items():
                models.append({"model_id": model_id, "kind": kind, "size": size})
        return models

    def _local_dir_for(self, model_id: str) -> Path:
        return self.root / model_id.replace("/", "_")

    def get_total_bytes(self, model_id: str) -> int:
        info = self._hf_api.model_info(model_id)
        total = 0
        for sibling in info.siblings:
            size = sibling.size or 0
            total += size
        return total

    def get_downloaded_bytes(self, model_id: str) -> int:
        local_dir = self._local_dir_for(model_id)
        if not local_dir.exists():
            return 0
        total = 0
        for path in local_dir.rglob("*"):
            if path.is_file():
                total += path.stat().st_size
        return total

    def is_downloaded(self, model_id: str) -> bool:
        local_dir = self._local_dir_for(model_id)
        if not local_dir.exists():
            return False
        for suffix in ("*.safetensors", "*.bin"):
            if any(local_dir.rglob(suffix)):
                return True
        return (local_dir / "config.json").exists()

    def download(self, model_id: str) -> ModelDownloadState:
        with self._download_lock:
            if model_id in self.downloads and self.downloads[model_id].status in {"downloading", "completed"}:
                return self.downloads[model_id]
            local_dir = self._local_dir_for(model_id)
            total_bytes = self.get_total_bytes(model_id)
            state = ModelDownloadState(model_id=model_id, local_dir=local_dir, total_bytes=total_bytes)
            self.downloads[model_id] = state
        state.status = "downloading"
        try:
            path = snapshot_download(
                repo_id=model_id,
                local_dir=str(local_dir),
                resume_download=True,
                local_dir_use_symlinks=False,
            )
            self.cache[model_id] = Path(path)
            state.downloaded_bytes = self.get_downloaded_bytes(model_id)
            state.status = "completed"
        except Exception as exc:  # noqa: BLE001
            state.status = "error"
            state.error = str(exc)
        return state

    def ensure_download_state(self, model_id: str) -> ModelDownloadState:
        if model_id in self.downloads:
            return self.downloads[model_id]
        local_dir = self._local_dir_for(model_id)
        total_bytes = self.get_total_bytes(model_id)
        state = ModelDownloadState(model_id=model_id, local_dir=local_dir, total_bytes=total_bytes)
        state.downloaded_bytes = self.get_downloaded_bytes(model_id)
        if self.is_downloaded(model_id):
            state.status = "completed"
        self.downloads[model_id] = state
        return state

    def update_progress(self, model_id: str) -> None:
        state = self.ensure_download_state(model_id)
        state.downloaded_bytes = self.get_downloaded_bytes(model_id)
        if state.status == "downloading" and state.total_bytes and state.downloaded_bytes >= state.total_bytes:
            state.status = "completed"
