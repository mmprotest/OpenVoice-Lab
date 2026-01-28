import re
from dataclasses import dataclass
from typing import Iterable, List, Tuple

import numpy as np


@dataclass
class SsmlHint:
    kind: str
    value: str


def parse_ssml_lite(text: str) -> Tuple[str, List[SsmlHint]]:
    hints: List[SsmlHint] = []

    def _break(match: re.Match) -> str:
        time_value = match.group(1)
        hints.append(SsmlHint(kind="break", value=time_value))
        return " "

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
    text = re.sub(r"<prosody\s+rate=\"(slow|fast)\">(.*?)</prosody>", _prosody, text, flags=re.DOTALL)
    text = re.sub(
        r"<emphasis\s+level=\"(strong|moderate)\">(.*?)</emphasis>",
        _emphasis,
        text,
        flags=re.DOTALL,
    )
    text = re.sub(r"<[^>]+>", "", text)
    return text.strip(), hints


def apply_pronunciation(text: str, entries: Iterable[Tuple[str, str]]) -> str:
    for source, target in entries:
        pattern = r"\\b" + re.escape(source) + r"\\b"
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
