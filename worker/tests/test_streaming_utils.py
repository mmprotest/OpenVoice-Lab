from app import _iter_pcm_frames


def test_iter_pcm_frames_handles_partial_frame():
    sample_rate = 24000
    total_samples = 1000
    raw = b"\x00" * (total_samples * 2)
    frames = list(_iter_pcm_frames(raw, sample_rate))
    assert frames
    assert len(frames[0]) == int(sample_rate * 0.02) * 2
    assert len(frames[-1]) < len(frames[0])
