import re
from dataclasses import dataclass
from typing import Iterable, List, Optional, Tuple, Union

import numpy as np


@dataclass
class SsmlHint:
    kind: str
    value: str


@dataclass
class TextSegment:
    text: str
    rate: Optional[str]
    emphasis: Optional[str]


@dataclass
class BreakSegment:
    seconds: float


Segment = Union[TextSegment, BreakSegment]


BREAK_SENTINEL_PREFIX = "[[BREAK:"
BREAK_SENTINEL_SUFFIX = "]]"


def parse_ssml_lite(text: str) -> Tuple[str, List[SsmlHint]]:
    hints: List[SsmlHint] = []

    def _break(match: re.Match) -> str:
        time_value = match.group(1)
        hints.append(SsmlHint(kind="break", value=time_value))
        return f" {BREAK_SENTINEL_PREFIX}{time_value}{BREAK_SENTINEL_SUFFIX} "

    def _prosody(match: re.Match) -> str:
        rate = match.group(1)
        content = match.group(2)
        hints.append(SsmlHint(kind="prosody", value=rate))
        return content

    def _emphasis(match: re.Match) -> str:
        level = match.group(1)
        content = match.group(2)
        hints.append(SsmlHint(kind="emphasis", value=level))
        return content

    text = re.sub(r"<break\s+time=\"(.*?)\"\s*/>", _break, text)
    text = re.sub(
        r"<prosody\s+rate=\"(slow|fast)\">(.*?)</prosody>",
        _prosody,
        text,
        flags=re.DOTALL,
    )
    text = re.sub(
        r"<emphasis\s+level=\"(strong|moderate)\">(.*?)</emphasis>",
        _emphasis,
        text,
        flags=re.DOTALL,
    )
    text = re.sub(r"<[^>]+>", "", text)
    return text.strip(), hints


def parse_ssml_lite_segments(text: str) -> List[Segment]:
    tokens = re.split(r"(<[^>]+>)", text)
    rate_stack: List[Optional[str]] = []
    emphasis_stack: List[Optional[str]] = []
    segments: List[Segment] = []

    def current_rate() -> Optional[str]:
        return rate_stack[-1] if rate_stack else None

    def current_emphasis() -> Optional[str]:
        return emphasis_stack[-1] if emphasis_stack else None

    for token in tokens:
        if not token:
            continue
        if token.startswith("<") and token.endswith(">"):
            tag = token.strip()
            lower = tag.lower()
            break_match = re.match(r"^<\s*break\s+time\s*=\s*\"(.*?)\"\s*/\s*>$", lower)
            if break_match:
                seconds = break_to_seconds(break_match.group(1))
                segments.append(BreakSegment(seconds=seconds))
                continue
            prosody_open = re.match(r"^<\s*prosody\s+rate\s*=\s*\"(slow|fast)\"\s*>$", lower)
            if prosody_open:
                rate_stack.append(prosody_open.group(1))
                continue
            if re.match(r"^<\s*/\s*prosody\s*>$", lower):
                if rate_stack:
                    rate_stack.pop()
                continue
            emphasis_open = re.match(
                r"^<\s*emphasis\s+level\s*=\s*\"(moderate|strong)\"\s*>$",
                lower,
            )
            if emphasis_open:
                emphasis_stack.append(emphasis_open.group(1))
                continue
            if re.match(r"^<\s*/\s*emphasis\s*>$", lower):
                if emphasis_stack:
                    emphasis_stack.pop()
                continue
            continue
        segments.append(TextSegment(text=token, rate=current_rate(), emphasis=current_emphasis()))

    merged: List[Segment] = []
    for segment in segments:
        if (
            merged
            and isinstance(segment, TextSegment)
            and isinstance(merged[-1], TextSegment)
            and merged[-1].rate == segment.rate
            and merged[-1].emphasis == segment.emphasis
        ):
            merged[-1].text += segment.text
        else:
            merged.append(segment)
    return merged


def parse_break_sentinels(text: str) -> List[Tuple[str, str]]:
    pattern = re.compile(r"\[\[BREAK:(.*?)\]\]")
    results: List[Tuple[str, str]] = []
    last_end = 0
    for match in pattern.finditer(text):
        if match.start() > last_end:
            results.append(("text", text[last_end : match.start()]))
        results.append(("break", match.group(1)))
        last_end = match.end()
    if last_end < len(text):
        results.append(("text", text[last_end:]))
    return results


def break_to_seconds(value: str) -> float:
    value = value.strip().lower()
    if value.endswith("ms"):
        return float(value[:-2]) / 1000.0
    if value.endswith("s"):
        return float(value[:-1])
    raise ValueError(f"Unsupported break time format: {value}")


def insert_silence(sample_rate: int, seconds: float) -> np.ndarray:
    if seconds <= 0:
        return np.array([], dtype=np.float32)
    samples = int(round(sample_rate * seconds))
    return np.zeros(samples, dtype=np.float32)


def hints_to_style(hints: Iterable[SsmlHint]) -> str:
    parts: List[str] = []
    for hint in hints:
        if hint.kind == "prosody":
            if hint.value == "slow":
                parts.append("slow pace")
            elif hint.value == "fast":
                parts.append("fast pace")
        elif hint.kind == "emphasis":
            if hint.value == "strong":
                parts.append("strong emphasis")
            elif hint.value == "moderate":
                parts.append("moderate emphasis")
        elif hint.kind == "break":
            parts.append(f"pause {hint.value}")
    return ", ".join(parts)


def apply_pronunciation(text: str, entries: Iterable[Tuple[str, str]]) -> str:
    for source, target in entries:
        pattern = r"\b" + re.escape(source) + r"\b"
        text = re.sub(pattern, target, text, flags=re.IGNORECASE)
    return text


def chunk_text(text: str, max_chars: int = 400) -> List[str]:
    sentences = re.split(r"(?<=[.!?])\s+", text.strip())
    chunks: List[str] = []
    current = ""
    for sentence in filter(None, sentences):
        if len(current) + len(sentence) + 1 > max_chars and current:
            chunks.append(current.strip())
            current = sentence
        else:
            current = f"{current} {sentence}".strip()
    if current:
        chunks.append(current.strip())
    return chunks


def stitch_audio(chunks: List[np.ndarray], sample_rate: int, crossfade_ms: int = 50) -> np.ndarray:
    if not chunks:
        return np.array([], dtype=np.float32)
    if len(chunks) == 1:
        return chunks[0]
    fade_samples = int(sample_rate * crossfade_ms / 1000)
    output = chunks[0].astype(np.float32)
    for chunk in chunks[1:]:
        chunk = chunk.astype(np.float32)
        if fade_samples == 0:
            output = np.concatenate([output, chunk])
            continue
        head = chunk[:fade_samples]
        tail = output[-fade_samples:]
        if len(head) < fade_samples or len(tail) < fade_samples:
            output = np.concatenate([output, chunk])
            continue
        fade_out = np.linspace(1.0, 0.0, fade_samples)
        fade_in = np.linspace(0.0, 1.0, fade_samples)
        blended = tail * fade_out + head * fade_in
        output = np.concatenate([output[:-fade_samples], blended, chunk[fade_samples:]])
    return output
