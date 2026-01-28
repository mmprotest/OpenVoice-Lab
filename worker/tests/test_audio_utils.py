import numpy as np

from audio_utils import resample_audio


def test_resample_audio_length_and_dtype():
    orig_sr = 24000
    target_sr = 16000
    duration = 1.0
    t = np.linspace(0, duration, int(orig_sr * duration), endpoint=False)
    audio = np.sin(2 * np.pi * 440 * t).astype(np.float32)
    resampled = resample_audio(audio, orig_sr=orig_sr, target_sr=target_sr)
    expected_len = int(target_sr * duration)
    assert resampled.dtype == np.float32
    assert abs(len(resampled) - expected_len) <= 2
