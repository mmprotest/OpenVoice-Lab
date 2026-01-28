from __future__ import annotations

import asyncio
import json
import logging
import os
import time
import uuid
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path
from typing import Dict, List, Optional

import numpy as np
from fastapi import BackgroundTasks, FastAPI, File, Form, HTTPException, UploadFile
from fastapi.responses import JSONResponse, StreamingResponse
from huggingface_hub import snapshot_download
from pydantic import BaseModel

from text_pipeline import apply_pronunciation, chunk_text, parse_ssml_lite, stitch_audio
from storage import get_paths, read_json, write_json


APP_VERSION = "0.1.0"


@dataclass
class ModelInfo:
    model_id: str
    size: str


class ModelManager:
    def __init__(self, root: Path) -> None:
        self.root = root
        self.cache: Dict[str, Path] = {}

    def get_model_path(self, model_id: str) -> Optional[Path]:
        return self.cache.get(model_id)

    def download(self, model_id: str) -> Path:
        local_dir = self.root / model_id.replace("/", "_")
        local_dir.mkdir(parents=True, exist_ok=True)
        path = snapshot_download(repo_id=model_id, local_dir=str(local_dir), resume_download=True)
        self.cache[model_id] = Path(path)
        return Path(path)


class TtsRequest(BaseModel):
    voice_id: str
    text: str
    language: str
    style: Optional[str] = None
    model_size: str
    backend: str
    sample_rate: int
    enable_ssml_lite: bool = True
    pronunciation_profile_id: Optional[str] = None
    project_id: Optional[str] = None


class VoiceDesignRequest(BaseModel):
    name: str
    description: str
    seed_text: str
    model_size: str
    backend: str


class ProjectCreateRequest(BaseModel):
    name: str


class PronunciationProfileCreate(BaseModel):
    name: str


class PronunciationProfileUpdate(BaseModel):
    entries: List[Dict[str, str]]


class VoicePatchRequest(BaseModel):
    name: Optional[str] = None
    tags: Optional[List[str]] = None


paths = get_paths()
logger = logging.getLogger("openvoice")
logger.setLevel(logging.INFO)
log_handler = logging.FileHandler(paths.logs / "worker.log")
log_handler.setFormatter(logging.Formatter("%(asctime)s %(levelname)s %(message)s"))
logger.addHandler(log_handler)

model_manager = ModelManager(paths.models)

app = FastAPI(title="OpenVoiceLab Worker", version=APP_VERSION)


PRESET_VOICES = [
    {"voice_id": "preset_qwen_customvoice_a", "name": "Qwen CustomVoice A", "type": "preset"},
    {"voice_id": "preset_qwen_customvoice_b", "name": "Qwen CustomVoice B", "type": "preset"},
]


def _job_id() -> str:
    return uuid.uuid4().hex


def _now() -> str:
    return datetime.utcnow().isoformat() + "Z"


def _voice_dir(voice_id: str) -> Path:
    return paths.voices / "user" / voice_id


def _save_history(entry: Dict[str, str]) -> None:
    write_json(paths.history / f"{entry['job_id']}.json", entry)


def _load_pronunciation(profile_id: Optional[str]) -> List[Dict[str, str]]:
    if not profile_id:
        return []
    profile_path = paths.pronunciation / f"{profile_id}.json"
    data = read_json(profile_path)
    return data.get("entries", [])


def _generate_tone(duration_s: float, sample_rate: int) -> np.ndarray:
    t = np.linspace(0, duration_s, int(sample_rate * duration_s), endpoint=False)
    return 0.2 * np.sin(2 * np.pi * 220 * t).astype(np.float32)


def _write_wav(path: Path, audio: np.ndarray, sample_rate: int) -> None:
    import wave

    path.parent.mkdir(parents=True, exist_ok=True)
    audio_int16 = np.clip(audio, -1.0, 1.0)
    audio_int16 = (audio_int16 * 32767).astype(np.int16)
    with wave.open(str(path), "wb") as wf:
        wf.setnchannels(1)
        wf.setsampwidth(2)
        wf.setframerate(sample_rate)
        wf.writeframes(audio_int16.tobytes())


def _apply_text_pipeline(request: TtsRequest) -> str:
    text = request.text
    if request.enable_ssml_lite:
        text, _ = parse_ssml_lite(text)
    entries = _load_pronunciation(request.pronunciation_profile_id)
    replacements = [(entry["from"], entry["to"]) for entry in entries]
    if replacements:
        text = apply_pronunciation(text, replacements)
    return text


def _synthesize(request: TtsRequest) -> np.ndarray:
    text = _apply_text_pipeline(request)
    chunks = chunk_text(text)
    duration = max(1.0, min(8.0, len(text) / 40.0))
    audio_chunks = [_generate_tone(duration / len(chunks), request.sample_rate) for _ in chunks]
    return stitch_audio(audio_chunks, request.sample_rate)


@app.get("/health")
async def health() -> Dict[str, str]:
    return {"ok": True, "version": APP_VERSION}


@app.get("/system")
async def system_info() -> Dict[str, object]:
    cuda_available = bool(os.environ.get("CUDA_AVAILABLE", ""))
    return {
        "cuda_available": cuda_available,
        "gpus": [{"name": "CUDA"}] if cuda_available else [],
        "backends": ["auto", "cpu", "cuda"],
        "models_supported": [
            {"model_id": "qwen3-tts-0.6b", "size": "0.6b"},
            {"model_id": "qwen3-tts-1.7b", "size": "1.7b"},
        ],
        "default_sample_rate": 24000,
    }


@app.get("/models/status")
async def models_status() -> Dict[str, object]:
    downloaded = []
    for model_id, path in model_manager.cache.items():
        downloaded.append({"model_id": model_id, "path": str(path)})
    return {"downloaded": downloaded}


@app.post("/models/download")
async def models_download(payload: Dict[str, str]) -> Dict[str, str]:
    model_id = payload.get("model_id")
    if not model_id:
        raise HTTPException(status_code=400, detail="model_id required")
    path = model_manager.download(model_id)
    return {"ok": True, "path": str(path)}


@app.get("/models/download/events")
async def models_download_events(model_id: str):
    async def event_stream():
        for pct, stage in [(0, "starting"), (50, "downloading"), (100, "done")]:
            await asyncio.sleep(0.1)
            yield f"data: {json.dumps({'pct': pct, 'stage': stage})}\n\n"

    return StreamingResponse(event_stream(), media_type="text/event-stream")


@app.get("/voices")
async def voices_list() -> Dict[str, object]:
    voices = list(PRESET_VOICES)
    user_dir = paths.voices / "user"
    for voice_folder in user_dir.glob("*"):
        meta = read_json(voice_folder / "meta.json")
        if meta:
            voices.append(meta)
    return {"voices": voices}


@app.post("/voices/clone")
async def voices_clone(
    name: str = Form(...),
    keep_ref_audio: bool = Form(False),
    ref_text: Optional[str] = Form(None),
    audio: UploadFile = File(...),
) -> Dict[str, str]:
    voice_id = f"voice_{_job_id()}"
    voice_path = _voice_dir(voice_id)
    voice_path.mkdir(parents=True, exist_ok=True)
    meta = {
        "voice_id": voice_id,
        "name": name,
        "type": "clone",
        "tags": [],
        "created_at": _now(),
        "ref_text": ref_text,
    }
    write_json(voice_path / "meta.json", meta)
    if keep_ref_audio:
        content = await audio.read()
        (voice_path / "ref_audio.wav").write_bytes(content)
    (voice_path / "clone_prompt.bin").write_bytes(b"placeholder_prompt")
    return {"voice_id": voice_id}


@app.post("/voices/design")
async def voices_design(payload: VoiceDesignRequest) -> Dict[str, str]:
    voice_id = f"voice_{_job_id()}"
    voice_path = _voice_dir(voice_id)
    voice_path.mkdir(parents=True, exist_ok=True)
    meta = {
        "voice_id": voice_id,
        "name": payload.name,
        "type": "design",
        "tags": [],
        "created_at": _now(),
        "description": payload.description,
    }
    write_json(voice_path / "meta.json", meta)
    (voice_path / "clone_prompt.bin").write_bytes(b"design_prompt")
    return {"voice_id": voice_id}


@app.patch("/voices/{voice_id}")
async def voices_patch(voice_id: str, payload: VoicePatchRequest) -> Dict[str, str]:
    voice_path = _voice_dir(voice_id)
    meta_path = voice_path / "meta.json"
    if not meta_path.exists():
        raise HTTPException(status_code=404, detail="Voice not found")
    meta = read_json(meta_path)
    if payload.name:
        meta["name"] = payload.name
    if payload.tags is not None:
        meta["tags"] = payload.tags
    write_json(meta_path, meta)
    return {"ok": True}


@app.delete("/voices/{voice_id}")
async def voices_delete(voice_id: str) -> Dict[str, str]:
    voice_path = _voice_dir(voice_id)
    if not voice_path.exists():
        raise HTTPException(status_code=404, detail="Voice not found")
    for item in voice_path.glob("*"):
        item.unlink()
    voice_path.rmdir()
    return {"ok": True}


@app.post("/tts")
async def tts(request: TtsRequest, background_tasks: BackgroundTasks):
    job_id = _job_id()
    audio = _synthesize(request)
    output_path = paths.outputs / f"{job_id}.wav"
    _write_wav(output_path, audio, request.sample_rate)
    duration_ms = int(len(audio) / request.sample_rate * 1000)
    entry = {
        "job_id": job_id,
        "text": request.text,
        "voice_id": request.voice_id,
        "output_path": str(output_path),
        "created_at": _now(),
        "project_id": request.project_id,
    }
    background_tasks.add_task(_save_history, entry)
    return {
        "job_id": job_id,
        "output_path": str(output_path),
        "duration_ms": duration_ms,
        "backend_used": request.backend,
        "warning": None,
    }


@app.post("/tts/stream")
async def tts_stream(request: TtsRequest):
    audio = _synthesize(request)
    audio_int16 = np.clip(audio, -1.0, 1.0)
    audio_int16 = (audio_int16 * 32767).astype(np.int16)

    def generator():
        chunk_size = int(request.sample_rate * 0.5)
        for idx in range(0, len(audio_int16), chunk_size):
            yield audio_int16[idx : idx + chunk_size].tobytes()
            time.sleep(0.05)

    return StreamingResponse(generator(), media_type="application/octet-stream")


@app.get("/projects")
async def projects_list() -> Dict[str, object]:
    projects = []
    for project_path in paths.projects.glob("*.json"):
        projects.append(read_json(project_path))
    return {"projects": projects}


@app.post("/projects")
async def projects_create(payload: ProjectCreateRequest) -> Dict[str, str]:
    project_id = _job_id()
    entry = {"project_id": project_id, "name": payload.name, "created_at": _now()}
    write_json(paths.projects / f"{project_id}.json", entry)
    return {"project_id": project_id}


@app.get("/history")
async def history_list(limit: int = 50, project_id: Optional[str] = None, q: Optional[str] = None):
    entries = []
    for entry_path in sorted(paths.history.glob("*.json"), reverse=True):
        entry = read_json(entry_path)
        if project_id and entry.get("project_id") != project_id:
            continue
        if q and q.lower() not in entry.get("text", "").lower():
            continue
        entries.append(entry)
        if len(entries) >= limit:
            break
    return {"history": entries}


@app.get("/history/{job_id}")
async def history_get(job_id: str) -> Dict[str, object]:
    entry = read_json(paths.history / f"{job_id}.json")
    if not entry:
        raise HTTPException(status_code=404, detail="History not found")
    return entry


@app.get("/pronunciation/profiles")
async def pronunciation_profiles():
    profiles = []
    for profile_path in paths.pronunciation.glob("*.json"):
        profiles.append(read_json(profile_path))
    return {"profiles": profiles}


@app.post("/pronunciation/profiles")
async def pronunciation_create(payload: PronunciationProfileCreate):
    profile_id = _job_id()
    entry = {"id": profile_id, "name": payload.name, "entries": []}
    write_json(paths.pronunciation / f"{profile_id}.json", entry)
    return {"id": profile_id}


@app.put("/pronunciation/profiles/{profile_id}")
async def pronunciation_update(profile_id: str, payload: PronunciationProfileUpdate):
    profile_path = paths.pronunciation / f"{profile_id}.json"
    entry = read_json(profile_path)
    if not entry:
        raise HTTPException(status_code=404, detail="Profile not found")
    entry["entries"] = payload.entries
    write_json(profile_path, entry)
    return {"ok": True}


@app.delete("/data")
async def delete_all_data():
    for folder in [paths.voices, paths.outputs, paths.history, paths.projects, paths.pronunciation]:
        for item in folder.glob("**/*"):
            if item.is_file():
                item.unlink()
    return {"ok": True}


@app.middleware("http")
async def add_request_id(request, call_next):
    request_id = uuid.uuid4().hex
    response = await call_next(request)
    response.headers["X-Request-ID"] = request_id
    logger.info("request=%s path=%s status=%s", request_id, request.url.path, response.status_code)
    return response
