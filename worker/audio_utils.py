from __future__ import annotations

from typing import cast

import numpy as np


def resample_audio(audio: np.ndarray, orig_sr: int, target_sr: int) -> np.ndarray:
    if orig_sr == target_sr:
        return audio.astype(np.float32)
    try:
        from scipy.signal import resample_poly

        audio_resampled = resample_poly(audio, target_sr, orig_sr)
    except Exception:
        import librosa

        audio_resampled = librosa.resample(audio, orig_sr=orig_sr, target_sr=target_sr)
    return cast(np.ndarray, audio_resampled).astype(np.float32)
