from __future__ import annotations

from typing import Optional

import numpy as np


def db_to_gain(db: float) -> float:
    return float(10 ** (db / 20))


def apply_gain(audio: np.ndarray, db: float) -> np.ndarray:
    if db == 0:
        return audio.astype(np.float32)
    gain = db_to_gain(db)
    return (audio * gain).astype(np.float32)


def apply_time_stretch(audio: np.ndarray, rate: float, sample_rate: int) -> np.ndarray:
    if rate == 1.0:
        return audio.astype(np.float32)
    if audio.size < max(1, int(sample_rate * 0.01)):
        return audio.astype(np.float32)
    import librosa

    stretched = librosa.effects.time_stretch(audio.astype(np.float32), rate=rate)
    return stretched.astype(np.float32)


def apply_pitch_shift(audio: np.ndarray, n_steps: float, sample_rate: int) -> np.ndarray:
    if n_steps == 0:
        return audio.astype(np.float32)
    import librosa

    shifted = librosa.effects.pitch_shift(audio.astype(np.float32), sr=sample_rate, n_steps=n_steps)
    return shifted.astype(np.float32)


def apply_limiter(audio: np.ndarray, ceiling: float = 0.99) -> np.ndarray:
    return np.clip(audio, -ceiling, ceiling).astype(np.float32)


def apply_style_dsp(
    audio: np.ndarray,
    sample_rate: int,
    rate: Optional[str],
    emphasis: Optional[str],
) -> np.ndarray:
    stretch_factor = 1.0
    gain_db = 0.0
    pitch_steps = 0.0

    if rate == "slow":
        stretch_factor *= 1.15
    elif rate == "fast":
        stretch_factor *= 0.87

    if emphasis == "moderate":
        stretch_factor *= 1.05
        gain_db += 2.0
        pitch_steps += 0.5
    elif emphasis == "strong":
        stretch_factor *= 1.10
        gain_db += 4.0
        pitch_steps += 1.0

    processed = audio.astype(np.float32)
    if pitch_steps:
        processed = apply_pitch_shift(processed, pitch_steps, sample_rate)
    if stretch_factor != 1.0:
        processed = apply_time_stretch(processed, rate=1.0 / stretch_factor, sample_rate=sample_rate)
    if gain_db:
        processed = apply_gain(processed, gain_db)
    return apply_limiter(processed)
