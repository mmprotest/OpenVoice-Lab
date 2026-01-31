from __future__ import annotations

import logging
from dataclasses import dataclass
from typing import Callable, Dict, List, Optional, Tuple, TypeVar

import numpy as np
import torch
from model_manager import ModelManager
from qwen_tts import Qwen3TTSModel, VoiceClonePromptItem

logger = logging.getLogger("openvoice")
T = TypeVar("T")


@dataclass
class SynthesisResult:
    audio: np.ndarray
    sample_rate: int


class TtsEngine:
    def __init__(self, model_manager: ModelManager) -> None:
        self.model_manager = model_manager
        self._model_cache: Dict[Tuple[str, str], Qwen3TTSModel] = {}
        self._preset_cache: Optional[List[Dict[str, str]]] = None

    def _resolve_device(self, backend: str) -> Tuple[str, Optional[str]]:
        backend = backend.lower()
        if backend == "auto":
            if torch.cuda.is_available():
                return "cuda", None
            return "cpu", "CUDA not available; fell back to CPU."
        if backend == "cuda" and not torch.cuda.is_available():
            return "cpu", "CUDA not available; fell back to CPU."
        return backend, None

    def _is_cuda_failure(self, exc: Exception) -> bool:
        message = str(exc).lower()
        return any(
            needle in message
            for needle in (
                "cuda",
                "cublas",
                "cudnn",
                "no kernel image",
                "device-side assert",
                "nvml",
            )
        )

    def _resolve_dtype(self, device: str) -> torch.dtype:
        if device.startswith("cuda"):
            return torch.bfloat16 if torch.cuda.is_available() else torch.float16
        return torch.float32

    def _get_model_for_device(self, model_id: str, device: str) -> Qwen3TTSModel:
        key = (model_id, device)
        if key in self._model_cache:
            return self._model_cache[key]
        local_dir = self.model_manager._local_dir_for(model_id)
        if not local_dir.exists():
            raise RuntimeError(f"Model {model_id} is not downloaded")
        dtype = self._resolve_dtype(device)
        device_map = device
        model = Qwen3TTSModel.from_pretrained(
            str(local_dir),
            device_map=device_map,
            torch_dtype=dtype,
        )
        self._model_cache[key] = model
        return model

    def get_model_for_backend(
        self, model_id: str, backend: str
    ) -> Tuple[Qwen3TTSModel, str, Optional[str]]:
        device, warning = self._resolve_device(backend)
        try:
            return self._get_model_for_device(model_id, device), device, warning
        except Exception as exc:  # noqa: BLE001
            if device.startswith("cuda") and self._is_cuda_failure(exc):
                logger.warning("CUDA backend failed, falling back to CPU: %s", exc)
                fallback_warning = "CUDA backend failed; fell back to CPU."
                model = self._get_model_for_device(model_id, "cpu")
                return model, "cpu", warning or fallback_warning
            raise

    def run_with_backend(
        self,
        model_id: str,
        backend: str,
        action: Callable[[Qwen3TTSModel], T],
    ) -> Tuple[T, str, Optional[str]]:
        model, device, warning = self.get_model_for_backend(model_id, backend)
        try:
            return action(model), device, warning
        except Exception as exc:  # noqa: BLE001
            if device.startswith("cuda") and self._is_cuda_failure(exc):
                logger.warning("CUDA backend failed during inference, falling back to CPU: %s", exc)
                fallback_warning = "CUDA backend failed during inference; fell back to CPU."
                model = self._get_model_for_device(model_id, "cpu")
                return action(model), "cpu", warning or fallback_warning
            raise

    def list_preset_voices(self) -> List[Dict[str, str]]:
        if self._preset_cache is not None:
            return self._preset_cache
        voices: List[Dict[str, str]] = []
        try:
            model_id = self.model_manager.resolve_model_id("custom_voice", "0.6b")
            model, _, _ = self.get_model_for_backend(model_id, "cpu")
            speakers = model.model.get_supported_speakers()
            for speaker in speakers:
                voices.append({"voice_id": f"preset::{speaker}", "name": speaker, "type": "preset"})
            if voices:
                _ = model.generate_custom_voice(text="hello", speaker=speakers[0], language="Auto")
        except Exception as exc:  # noqa: BLE001
            logger.warning("Failed to load preset voices: %s", exc)
            voices = [
                {"voice_id": "preset::female-1", "name": "Female 1", "type": "preset"},
                {"voice_id": "preset::male-1", "name": "Male 1", "type": "preset"},
            ]
        self._preset_cache = voices
        return voices

    def synthesize_custom_voice(
        self,
        text: str,
        voice_name: str,
        model_size: str,
        backend: str,
        language: str,
        instruct: Optional[str] = None,
    ) -> SynthesisResult:
        model_id = self.model_manager.resolve_model_id("custom_voice", model_size)
        model, _, _ = self.get_model_for_backend(model_id, backend)
        wavs, sample_rate = model.generate_custom_voice(
            text=text,
            speaker=voice_name,
            language=language,
            instruct=instruct or "",
            non_streaming_mode=True,
        )
        return SynthesisResult(audio=wavs[0], sample_rate=sample_rate)

    def synthesize_clone(
        self,
        text: str,
        voice_clone_prompt: List[VoiceClonePromptItem],
        model_size: str,
        backend: str,
        language: str,
    ) -> SynthesisResult:
        model_id = self.model_manager.resolve_model_id("base", model_size)
        model, _, _ = self.get_model_for_backend(model_id, backend)
        wavs, sample_rate = model.generate_voice_clone(
            text=text,
            language=language,
            voice_clone_prompt=voice_clone_prompt,
            non_streaming_mode=True,
        )
        return SynthesisResult(audio=wavs[0], sample_rate=sample_rate)

    def create_clone_prompt(
        self,
        audio: Tuple[np.ndarray, int],
        ref_text: Optional[str],
        model_size: str,
        backend: str,
    ) -> List[VoiceClonePromptItem]:
        model_id = self.model_manager.resolve_model_id("base", model_size)
        model, _, _ = self.get_model_for_backend(model_id, backend)
        x_vector_only_mode = not bool(ref_text)
        prompt = model.create_voice_clone_prompt(
            ref_audio=audio,
            ref_text=ref_text,
            x_vector_only_mode=x_vector_only_mode,
        )
        return prompt

    def synthesize_voice_design(
        self,
        description: str,
        seed_text: str,
        backend: str,
    ) -> SynthesisResult:
        model_id = self.model_manager.resolve_model_id("voice_design", "1.7b")
        model, _, _ = self.get_model_for_backend(model_id, backend)
        wavs, sample_rate = model.generate_voice_design(
            text=seed_text,
            language="Auto",
            instruct=description,
            non_streaming_mode=True,
        )
        return SynthesisResult(audio=wavs[0], sample_rate=sample_rate)
