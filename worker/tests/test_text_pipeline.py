import numpy as np

from text_pipeline import apply_pronunciation, chunk_text, parse_ssml_lite, stitch_audio


def test_parse_ssml_lite_strips_tags():
    text, hints = parse_ssml_lite(
        'Hello <break time="300ms"/> <prosody rate="fast">world</prosody> <emphasis level="strong">!</emphasis>'
    )
    assert "Hello" in text
    assert "world" in text
    assert len(hints) == 3


def test_apply_pronunciation_replaces_words():
    result = apply_pronunciation("Hello world", [("world", "wurld")])
    assert result == "Hello wurld"


def test_chunking_and_stitching():
    chunks = chunk_text("Hello world. How are you? I am fine.", max_chars=10)
    assert len(chunks) >= 2
    sample_rate = 1000
    audio_chunks = [np.ones(1000, dtype=np.float32) for _ in chunks]
    stitched = stitch_audio(audio_chunks, sample_rate, crossfade_ms=50)
    assert stitched.shape[0] > 0
