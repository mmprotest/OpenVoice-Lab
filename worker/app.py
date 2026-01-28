from __future__ import annotations

import asyncio
import io
import json
import logging
import os
import shutil
import tempfile
import time
import uuid
from datetime import datetime
from pathlib import Path
from typing import Dict, List, Optional, Tuple

import numpy as np
import soundfile as sf
import torch
from fastapi import BackgroundTasks, FastAPI, File, Form, HTTPException, UploadFile
from fastapi.responses import JSONResponse, StreamingResponse
from pydantic import BaseModel
from pydantic import ConfigDict

from model_manager import ModelManager
from audio_utils import resample_audio
from prompt_storage import load_clone_prompt_safe, save_clone_prompt_safe
from storage import Database, get_paths, read_json, write_json
from text_pipeline import (
    apply_pronunciation,
    break_to_seconds,
    chunk_text,
    hints_to_style,
    insert_silence,
    parse_break_sentinels,
    parse_ssml_lite,
    stitch_audio,
)
from tts_engine import TtsEngine


APP_VERSION = "1.0.0"
DEFAULT_SAMPLE_RATE = 24000


def _to_camel(string: str) -> str:
    parts = string.split("_")
    return parts[0] + "".join(word.capitalize() for word in parts[1:])


class ApiModel(BaseModel):
    model_config = ConfigDict(populate_by_name=True, alias_generator=_to_camel)


class TtsRequest(ApiModel):
    voice_id: str
    text: str
    language: str
    style: Optional[str] = None
    model_size: str
    backend: str
    sample_rate: int = DEFAULT_SAMPLE_RATE
    enable_ssml_lite: bool = True
    pronunciation_profile_id: Optional[str] = None
    project_id: Optional[str] = None


class VoiceDesignRequest(ApiModel):
    name: str
    description: str
    seed_text: str
    model_size: str
    backend: str


class ProjectCreateRequest(ApiModel):
    name: str


class PronunciationProfileCreate(ApiModel):
    name: str


class PronunciationProfileUpdate(ApiModel):
    entries: List[Dict[str, str]]


class VoicePatchRequest(ApiModel):
    name: Optional[str] = None
    tags: Optional[List[str]] = None


paths = get_paths()
logger = logging.getLogger("openvoice")
logger.setLevel(logging.INFO)
log_handler = logging.FileHandler(paths.logs / "worker.log")
log_handler.setFormatter(logging.Formatter("%(asctime)s %(levelname)s %(message)s"))
logger.addHandler(log_handler)

model_manager = ModelManager(paths.models)
engine = TtsEngine(model_manager)
db = Database(paths.db)

app = FastAPI(title="OpenVoiceLab Worker", version=APP_VERSION)


def _job_id() -> str:
    return uuid.uuid4().hex


def _now() -> str:
    return datetime.utcnow().isoformat() + "Z"


def _voice_dir(voice_id: str) -> Path:
    return paths.voices / "user" / voice_id


def _save_history(entry: Dict[str, str]) -> None:
    db.add_history(entry)


def _load_pronunciation(profile_id: Optional[str]) -> List[Dict[str, str]]:
    if not profile_id:
        return []
    profiles = db.list_pronunciation_profiles()
    for profile in profiles:
        if profile["profile_id"] == profile_id:
            return profile.get("entries", [])
    return []


def _write_wav(path: Path, audio: np.ndarray, sample_rate: int) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    sf.write(str(path), audio, sample_rate, subtype="PCM_16")


def _camelize_keys(data: Dict[str, object]) -> Dict[str, object]:
    return {_to_camel(key): value for key, value in data.items()}


def _apply_text_pipeline(request: TtsRequest) -> Tuple[str, Optional[str]]:
    text = request.text
    derived_style = None
    if request.enable_ssml_lite:
        text, hints = parse_ssml_lite(text)
        derived_style = hints_to_style(hints) or None
    entries = _load_pronunciation(request.pronunciation_profile_id)
    replacements = [(entry["from"], entry["to"]) for entry in entries]
    if replacements:
        text = apply_pronunciation(text, replacements)
    return text, derived_style


def _resolve_voice_meta(voice_id: str) -> Tuple[str, Optional[Path]]:
    if voice_id.startswith("preset::"):
        return "preset", None
    voice_path = _voice_dir(voice_id)
    if not voice_path.exists():
        raise HTTPException(status_code=404, detail="Voice not found")
    return "user", voice_path


def _load_clone_prompt(voice_path: Path):
    safe_path = voice_path / "clone_prompt.json"
    legacy_path = voice_path / "clone_prompt.pt"
    if safe_path.exists():
        try:
            return load_clone_prompt_safe(voice_path)
        except Exception as exc:  # noqa: BLE001
            logger.warning("Failed to load safe clone prompt from %s: %s", voice_path, exc)
            raise HTTPException(status_code=500, detail="Failed to load clone prompt") from exc
    if legacy_path.exists():
        if os.getenv("OPENVOICELAB_ALLOW_UNSAFE_TORCH_LOAD") == "1":
            logger.warning("Migrating legacy clone prompt at %s", legacy_path)
            prompt = torch.load(legacy_path, map_location="cpu")
            save_clone_prompt_safe(voice_path, prompt)
            legacy_path.unlink(missing_ok=True)
            return prompt
        raise HTTPException(
            status_code=409,
            detail=(
                "Legacy prompt format found. Recreate the voice or set "
                "OPENVOICELAB_ALLOW_UNSAFE_TORCH_LOAD=1 once to migrate."
            ),
        )
    raise HTTPException(status_code=404, detail="Clone prompt not found")


async def _ensure_wav_audio(upload: UploadFile) -> Tuple[np.ndarray, int]:
    suffix = Path(upload.filename or "").suffix.lower()
    data = await upload.read()
    if suffix == ".wav":
        with io.BytesIO(data) as buffer:
            audio, sr = sf.read(buffer, dtype="float32")
    else:
        ffmpeg = shutil.which("ffmpeg")
        if not ffmpeg:
            raise HTTPException(status_code=400, detail="Non-wav input requires ffmpeg installed")
        with tempfile.NamedTemporaryFile(delete=False, suffix=suffix) as src:
            src.write(data)
            src_path = src.name
        with tempfile.NamedTemporaryFile(delete=False, suffix=".wav") as dst:
            dst_path = dst.name
        cmd = [
            ffmpeg,
            "-y",
            "-i",
            src_path,
            "-ac",
            "1",
            "-ar",
            str(DEFAULT_SAMPLE_RATE),
            dst_path,
        ]
        try:
            result = os.spawnv(os.P_WAIT, ffmpeg, cmd)
            if result != 0:
                raise HTTPException(status_code=400, detail="ffmpeg failed to decode audio")
            audio, sr = sf.read(dst_path, dtype="float32")
        finally:
            Path(src_path).unlink(missing_ok=True)
            Path(dst_path).unlink(missing_ok=True)
    if audio.ndim > 1:
        audio = np.mean(audio, axis=1)
    if sr != DEFAULT_SAMPLE_RATE:
        audio = resample_audio(audio, orig_sr=sr, target_sr=DEFAULT_SAMPLE_RATE)
        sr = DEFAULT_SAMPLE_RATE
    return audio.astype(np.float32), sr


def _synthesize_chunks(request: TtsRequest) -> Tuple[List[np.ndarray], int, str, Optional[str]]:
    audio_chunks: List[np.ndarray] = []
    sample_rate = request.sample_rate

    voice_kind, voice_path = _resolve_voice_meta(request.voice_id)
    if voice_kind == "preset":
        voice_name = request.voice_id.split("::", 1)[1]
        model_id = engine.model_manager.resolve_model_id("custom_voice", request.model_size)
        def _run(model):
            text, derived_style = _apply_text_pipeline(request)
            combined_style = ", ".join(filter(None, [request.style, derived_style])) if request.style or derived_style else None
            segments = parse_break_sentinels(text)
            audio_chunks_local: List[np.ndarray] = []
            sample_rate_local = DEFAULT_SAMPLE_RATE
            for kind, value in segments:
                if kind == "text":
                    for chunk in chunk_text(value):
                        if not chunk.strip():
                            continue
                        wavs, sample_rate_local = model.generate_custom_voice(
                            text=chunk,
                            speaker=voice_name,
                            language=request.language,
                            instruct=combined_style or "",
                            non_streaming_mode=True,
                        )
                        audio_chunks_local.append(wavs[0])
                elif kind == "break":
                    seconds = break_to_seconds(value)
                    audio_chunks_local.append(insert_silence(sample_rate_local, seconds))
            return audio_chunks_local, sample_rate_local

        (audio_chunks, sample_rate), backend_used, warning = engine.run_with_backend(
            model_id,
            request.backend,
            _run,
        )
    else:
        prompt = _load_clone_prompt(voice_path)
        model_id = engine.model_manager.resolve_model_id("base", request.model_size)
        def _run(model):
            text, _ = _apply_text_pipeline(request)
            segments = parse_break_sentinels(text)
            audio_chunks_local: List[np.ndarray] = []
            sample_rate_local = DEFAULT_SAMPLE_RATE
            for kind, value in segments:
                if kind == "text":
                    for chunk in chunk_text(value):
                        if not chunk.strip():
                            continue
                        try:
                            wavs, sample_rate_local = model.generate_voice_clone(
                                text=chunk,
                                language=request.language,
                                voice_clone_prompt=prompt,
                                non_streaming_mode=True,
                            )
                        except TypeError as exc:
                            if isinstance(prompt, list) and prompt and isinstance(prompt[0], dict):
                                raise RuntimeError(
                                    "Voice clone prompt reconstruction failed; "
                                    f"type mismatch: {exc}"
                                ) from exc
                            raise
                        audio_chunks_local.append(wavs[0])
                elif kind == "break":
                    seconds = break_to_seconds(value)
                    audio_chunks_local.append(insert_silence(sample_rate_local, seconds))
            return audio_chunks_local, sample_rate_local

        (audio_chunks, sample_rate), backend_used, warning = engine.run_with_backend(
            model_id,
            request.backend,
            _run,
        )
    return audio_chunks, sample_rate, backend_used, warning


def _synthesize(request: TtsRequest) -> Tuple[np.ndarray, int, str, Optional[str]]:
    audio_chunks, sample_rate, backend_used, warning = _synthesize_chunks(request)
    stitched = stitch_audio(audio_chunks, sample_rate)
    if sample_rate != request.sample_rate:
        stitched = resample_audio(stitched, orig_sr=sample_rate, target_sr=request.sample_rate)
        sample_rate = request.sample_rate
    return stitched, sample_rate, backend_used, warning


@app.get("/health")
async def health() -> Dict[str, str]:
    return {"ok": True, "version": APP_VERSION}


@app.get("/system")
async def system_info() -> Dict[str, object]:
    cuda_available = torch.cuda.is_available()
    gpus = []
    if cuda_available:
        for idx in range(torch.cuda.device_count()):
            gpus.append({"name": torch.cuda.get_device_name(idx)})
    return {
        "cudaAvailable": cuda_available,
        "gpus": gpus,
        "backends": ["auto", "cpu", "cuda"],
        "modelsSupported": [_camelize_keys(entry) for entry in model_manager.list_required_models()],
        "defaultSampleRate": DEFAULT_SAMPLE_RATE,
    }


@app.get("/models/status")
async def models_status() -> Dict[str, object]:
    status = []
    for entry in model_manager.list_required_models():
        model_id = entry["model_id"]
        state = model_manager.ensure_download_state(model_id)
        model_manager.update_progress(model_id)
        pct = 0
        if state.total_bytes:
            pct = min(100, int(state.downloaded_bytes / state.total_bytes * 100))
        status.append(
            _camelize_keys(
                {
                    "model_id": model_id,
                    "kind": entry["kind"],
                    "size": entry["size"],
                    "status": state.status,
                    "downloaded_bytes": state.downloaded_bytes,
                    "total_bytes": state.total_bytes,
                    "progress": pct,
                    "path": str(state.local_dir),
                    "error": state.error,
                }
            )
        )
    return {"models": status}


@app.post("/models/download")
async def models_download(payload: Dict[str, str]) -> Dict[str, str]:
    model_id = payload.get("model_id")
    if not model_id:
        raise HTTPException(status_code=400, detail="model_id required")
    state = model_manager.ensure_download_state(model_id)
    if state.status == "completed":
        return {"ok": True, "path": str(state.local_dir)}

    loop = asyncio.get_running_loop()
    await loop.run_in_executor(None, model_manager.download, model_id)
    return {"ok": True, "path": str(state.local_dir)}


@app.get("/models/download/events")
async def models_download_events(model_id: str):
    async def event_stream():
        state = model_manager.ensure_download_state(model_id)
        while state.status in {"pending", "downloading"}:
            model_manager.update_progress(model_id)
            pct = 0
            if state.total_bytes:
                pct = min(100, int(state.downloaded_bytes / state.total_bytes * 100))
            payload = _camelize_keys(
                {
                    "pct": pct,
                    "stage": state.status,
                    "downloaded_bytes": state.downloaded_bytes,
                    "total_bytes": state.total_bytes,
                    "error": state.error,
                }
            )
            yield f"data: {json.dumps(payload)}\n\n"
            await asyncio.sleep(0.5)
        model_manager.update_progress(model_id)
        pct = 100 if state.status == "completed" else 0
        payload = _camelize_keys(
            {
                "pct": pct,
                "stage": state.status,
                "downloaded_bytes": state.downloaded_bytes,
                "total_bytes": state.total_bytes,
                "error": state.error,
            }
        )
        yield f"data: {json.dumps(payload)}\n\n"

    return StreamingResponse(event_stream(), media_type="text/event-stream")


@app.get("/voices")
async def voices_list() -> Dict[str, object]:
    voices = [_camelize_keys(voice) for voice in engine.list_preset_voices()]
    user_dir = paths.voices / "user"
    for voice_folder in user_dir.glob("*"):
        meta = read_json(voice_folder / "meta.json")
        if meta:
            voices.append(_camelize_keys(meta))
    return {"voices": voices}


@app.post("/voices/clone")
async def voices_clone(
    name: str = Form(...),
    model_size: str = Form("0.6b"),
    backend: str = Form("auto"),
    keep_ref_audio: bool = Form(False),
    consent: bool = Form(False),
    ref_text: Optional[str] = Form(None),
    audio: UploadFile = File(...),
) -> Dict[str, str]:
    if not consent:
        raise HTTPException(status_code=400, detail="consent flag required")
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
        "model_size": model_size,
        "backend": backend,
    }
    write_json(voice_path / "meta.json", meta)

    audio_np, sr = await _ensure_wav_audio(audio)
    prompt = engine.create_clone_prompt((audio_np, sr), ref_text, model_size, backend)
    save_clone_prompt_safe(voice_path, prompt)

    if keep_ref_audio:
        ref_path = voice_path / "ref_audio.wav"
        sf.write(str(ref_path), audio_np, sr, subtype="PCM_16")
    return {"voiceId": voice_id}


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
        "model_size": payload.model_size,
        "backend": payload.backend,
    }
    write_json(voice_path / "meta.json", meta)
    design = engine.synthesize_voice_design(payload.description, payload.seed_text, payload.backend)
    preview_path = voice_path / "preview.wav"
    sf.write(str(preview_path), design.audio, design.sample_rate, subtype="PCM_16")
    prompt = engine.create_clone_prompt((design.audio, design.sample_rate), payload.seed_text, payload.model_size, payload.backend)
    save_clone_prompt_safe(voice_path, prompt)
    return {"voiceId": voice_id}


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
    audio, sample_rate, backend_used, warning = _synthesize(request)
    output_path = paths.outputs / f"{job_id}.wav"
    _write_wav(output_path, audio, sample_rate)
    duration_ms = int(len(audio) / sample_rate * 1000)
    entry = {
        "job_id": job_id,
        "text": request.text,
        "voice_id": request.voice_id,
        "output_path": str(output_path),
        "created_at": _now(),
        "project_id": request.project_id,
        "pronunciation_profile_id": request.pronunciation_profile_id,
    }
    background_tasks.add_task(_save_history, entry)
    return {
        "jobId": job_id,
        "outputPath": str(output_path),
        "durationMs": duration_ms,
        "backendUsed": backend_used,
        "warning": warning,
    }


@app.post("/tts/stream")
async def tts_stream(request: TtsRequest):
    audio_chunks, sample_rate, _, _ = _synthesize_chunks(request)

    def generator():
        target_sample_rate = request.sample_rate
        frame_samples = max(1, int(target_sample_rate * 0.02))
        for chunk in audio_chunks:
            if sample_rate != target_sample_rate:
                chunk = resample_audio(chunk, orig_sr=sample_rate, target_sr=target_sample_rate)
            audio_int16 = np.clip(chunk, -1.0, 1.0)
            audio_int16 = (audio_int16 * 32767).astype(np.int16)
            raw = audio_int16.tobytes()
            frame_bytes = frame_samples * 2
            for start in range(0, len(raw), frame_bytes):
                frame = raw[start : start + frame_bytes]
                yield frame
                time.sleep(frame_samples / target_sample_rate)

    headers = {
        "X-Sample-Rate": str(request.sample_rate),
        "X-PCM-Format": "s16le",
        "X-Channels": "1",
    }
    return StreamingResponse(generator(), media_type="application/octet-stream", headers=headers)


@app.get("/projects")
async def projects_list() -> Dict[str, object]:
    projects = [_camelize_keys(project) for project in db.list_projects()]
    return {"projects": projects}


@app.post("/projects")
async def projects_create(payload: ProjectCreateRequest) -> Dict[str, str]:
    project_id = _job_id()
    entry = {"project_id": project_id, "name": payload.name, "created_at": _now()}
    db.create_project(project_id, payload.name, entry["created_at"])
    return {"projectId": project_id}


@app.get("/history")
async def history_list(limit: int = 50, project_id: Optional[str] = None, q: Optional[str] = None):
    entries = [_camelize_keys(entry) for entry in db.list_history(limit, project_id, q)]
    return {"history": entries}


@app.get("/history/{job_id}")
async def history_get(job_id: str) -> Dict[str, object]:
    entry = db.get_history(job_id)
    if not entry:
        raise HTTPException(status_code=404, detail="History not found")
    return _camelize_keys(entry)


@app.get("/pronunciation/profiles")
async def pronunciation_profiles():
    profiles = []
    for profile in db.list_pronunciation_profiles():
        entries = profile.get("entries", [])
        profile_dict = {
            "profile_id": profile["profile_id"],
            "name": profile["name"],
            "created_at": profile["created_at"],
            "entries": entries,
        }
        profiles.append(_camelize_keys(profile_dict))
    return {"profiles": profiles}


@app.post("/pronunciation/profiles")
async def pronunciation_create(payload: PronunciationProfileCreate):
    profile_id = _job_id()
    db.create_pronunciation_profile(profile_id, payload.name, _now())
    return {"id": profile_id}


@app.put("/pronunciation/profiles/{profile_id}")
async def pronunciation_update(profile_id: str, payload: PronunciationProfileUpdate):
    profiles = db.list_pronunciation_profiles()
    if not any(profile["profile_id"] == profile_id for profile in profiles):
        raise HTTPException(status_code=404, detail="Profile not found")
    db.update_pronunciation_entries(profile_id, payload.entries)
    return {"ok": True}


@app.delete("/pronunciation/profiles/{profile_id}")
async def pronunciation_delete(profile_id: str):
    profiles = db.list_pronunciation_profiles()
    if not any(profile["profile_id"] == profile_id for profile in profiles):
        raise HTTPException(status_code=404, detail="Profile not found")
    db.delete_pronunciation_profile(profile_id)
    return {"ok": True}


@app.delete("/data")
async def delete_all_data():
    for folder in [paths.voices, paths.outputs]:
        for item in folder.glob("**/*"):
            if item.is_file():
                item.unlink()
    db.delete_all()
    return {"ok": True}


@app.middleware("http")
async def add_request_id(request, call_next):
    request_id = uuid.uuid4().hex
    response = await call_next(request)
    response.headers["X-Request-ID"] = request_id
    logger.info("request=%s path=%s status=%s", request_id, request.url.path, response.status_code)
    return response
