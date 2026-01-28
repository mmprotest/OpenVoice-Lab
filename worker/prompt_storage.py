from __future__ import annotations

import json
import logging
from dataclasses import asdict, is_dataclass
from pathlib import Path
from typing import Any, Dict

import numpy as np
import torch
from safetensors.torch import load_file, save_file

logger = logging.getLogger("openvoice")

TENSOR_SENTINEL = "__tensor__"


def save_clone_prompt_safe(path_base: Path, prompt: Any) -> None:
    path_base.mkdir(parents=True, exist_ok=True)
    tensor_store: Dict[str, torch.Tensor] = {}
    tree = _serialize_tree(prompt, tensor_store)
    json_path = path_base / "clone_prompt.json"
    json_path.write_text(json.dumps(tree, ensure_ascii=False, indent=2))
    if tensor_store:
        save_file(tensor_store, str(path_base / "clone_prompt.safetensors"))


def load_clone_prompt_safe(path_base: Path) -> Any:
    json_path = path_base / "clone_prompt.json"
    if not json_path.exists():
        raise FileNotFoundError(f"{json_path} not found")
    tree = json.loads(json_path.read_text())
    tensors_path = path_base / "clone_prompt.safetensors"
    tensors = load_file(str(tensors_path)) if tensors_path.exists() else {}
    restored = _restore_tree(tree, tensors)
    return _reconstruct_prompt(restored)


def _serialize_tree(obj: Any, tensor_store: Dict[str, torch.Tensor]) -> Any:
    if isinstance(obj, torch.Tensor) or isinstance(obj, np.ndarray):
        key = f"tensor_{len(tensor_store)}"
        tensor_store[key] = torch.as_tensor(obj).detach().cpu()
        return {TENSOR_SENTINEL: key}
    if is_dataclass(obj):
        return _serialize_tree(asdict(obj), tensor_store)
    if hasattr(obj, "model_dump") and callable(getattr(obj, "model_dump")):
        return _serialize_tree(obj.model_dump(), tensor_store)
    if isinstance(obj, dict):
        serialized: Dict[str, Any] = {}
        for key, value in obj.items():
            if not isinstance(key, str):
                raise TypeError(f"Prompt dict keys must be strings, got {type(key)}")
            serialized[key] = _serialize_tree(value, tensor_store)
        return serialized
    if isinstance(obj, (list, tuple)):
        return [_serialize_tree(item, tensor_store) for item in obj]
    if _is_json_primitive(obj):
        return obj
    if hasattr(obj, "__dict__"):
        return _serialize_tree(obj.__dict__, tensor_store)
    raise TypeError(f"Unsupported prompt object type: {type(obj)}")


def _restore_tree(obj: Any, tensors: Dict[str, torch.Tensor]) -> Any:
    if isinstance(obj, dict) and TENSOR_SENTINEL in obj and len(obj) == 1:
        key = obj[TENSOR_SENTINEL]
        if key not in tensors:
            raise KeyError(f"Tensor key {key} missing from safetensors file")
        return tensors[key]
    if isinstance(obj, dict):
        return {key: _restore_tree(value, tensors) for key, value in obj.items()}
    if isinstance(obj, list):
        return [_restore_tree(item, tensors) for item in obj]
    return obj


def _reconstruct_prompt(data: Any) -> Any:
    try:
        from qwen_tts import VoiceClonePromptItem  # pylint: disable=import-error
    except Exception:
        VoiceClonePromptItem = None

    if VoiceClonePromptItem and isinstance(data, list):
        if all(isinstance(item, dict) for item in data):
            try:
                return [VoiceClonePromptItem(**item) for item in data]
            except Exception as exc:  # noqa: BLE001
                logger.warning("VoiceClonePromptItem reconstruction failed; using dicts. Error: %s", exc)
                return data
    return data


def _is_json_primitive(value: Any) -> bool:
    return value is None or isinstance(value, (str, int, float, bool))
