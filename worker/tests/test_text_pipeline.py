import numpy as np
from text_pipeline import (
    BreakSegment,
    TextSegment,
    apply_pronunciation,
    break_to_seconds,
    chunk_text,
    hints_to_style,
    parse_break_sentinels,
    parse_ssml_lite,
    parse_ssml_lite_segments,
    stitch_audio,
)


def test_parse_ssml_lite_strips_tags():
    text, hints = parse_ssml_lite(
        'Hello <break time="300ms"/> <prosody rate="fast">world</prosody> '
        '<emphasis level="strong">!</emphasis>'
    )
    assert "Hello" in text
    assert "world" in text
    assert "[[BREAK:300ms]]" in text
    assert len(hints) == 3


def test_hints_to_style():
    _, hints = parse_ssml_lite(
        '<prosody rate="slow">hi</prosody> <emphasis level="moderate">there</emphasis>'
    )
    style = hints_to_style(hints)
    assert "slow" in style
    assert "emphasis" in style


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


def test_parse_break_sentinels():
    text, _ = parse_ssml_lite('Hello <break time="300ms"/> world')
    segments = parse_break_sentinels(text)
    assert segments == [("text", "Hello "), ("break", "300ms"), ("text", " world")]


def test_break_to_seconds():
    assert break_to_seconds("300ms") == 0.3
    assert break_to_seconds("1s") == 1.0


def test_parse_ssml_lite_segments_nested_and_merge():
    segments = parse_ssml_lite_segments(
        'Hello <prosody rate="slow"><emphasis level="moderate">world</emphasis></prosody>!!!'
    )
    assert isinstance(segments[0], TextSegment)
    assert isinstance(segments[1], TextSegment)
    assert segments[1].rate == "slow"
    assert segments[1].emphasis == "moderate"
    assert segments[0].text.strip() == "Hello"
    assert segments[1].text.strip().startswith("world")


def test_parse_ssml_lite_segments_breaks():
    segments = parse_ssml_lite_segments('Hi<break time="300ms"/>there')
    assert isinstance(segments[0], TextSegment)
    assert isinstance(segments[1], BreakSegment)
    assert isinstance(segments[2], TextSegment)
    assert segments[1].seconds == 0.3
