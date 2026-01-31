import numpy as np
from dsp_utils import apply_style_dsp


def _sine_wave(freq: float, sample_rate: int, duration: float) -> np.ndarray:
    t = np.linspace(0, duration, int(sample_rate * duration), endpoint=False)
    return np.sin(2 * np.pi * freq * t).astype(np.float32)


def _rms(audio: np.ndarray) -> float:
    return float(np.sqrt(np.mean(np.square(audio))))


def test_apply_style_dsp_emphasis_and_rate():
    sample_rate = 24000
    audio = _sine_wave(440.0, sample_rate, 0.2)
    baseline_rms = _rms(audio)
    styled = apply_style_dsp(audio, sample_rate, rate="slow", emphasis="moderate")
    assert styled.dtype == np.float32
    assert _rms(styled) > baseline_rms
    assert len(styled) > len(audio)
