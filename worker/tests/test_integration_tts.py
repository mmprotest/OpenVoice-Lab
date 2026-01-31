import os

import pytest
from app import app, model_manager
from fastapi.testclient import TestClient


@pytest.mark.integration
def test_tts_if_model_present():
    model_id = model_manager.resolve_model_id("custom_voice", "0.6b")
    if not model_manager.is_downloaded(model_id):
        pytest.skip("Model not downloaded")
    client = TestClient(app)
    payload = {
        "voice_id": "preset::female-1",
        "text": "Hello world",
        "language": "Auto",
        "style": None,
        "model_size": "0.6b",
        "backend": "cpu",
        "sample_rate": 24000,
        "enable_ssml_lite": True,
        "pronunciation_profile_id": None,
        "project_id": None,
    }
    response = client.post("/tts", json=payload)
    assert response.status_code == 200
    data = response.json()
    assert "output_path" in data
    assert os.path.exists(data["output_path"])


@pytest.mark.integration
def test_tts_streaming_chunks_if_model_present():
    model_id = model_manager.resolve_model_id("custom_voice", "0.6b")
    if not model_manager.is_downloaded(model_id):
        pytest.skip("Model not downloaded")
    client = TestClient(app)
    payload = {
        "voice_id": "preset::female-1",
        "text": 'Hello <break time="300ms"/> world',
        "language": "Auto",
        "style": None,
        "model_size": "0.6b",
        "backend": "cpu",
        "sample_rate": 24000,
        "enable_ssml_lite": True,
        "pronunciation_profile_id": None,
        "project_id": None,
    }
    with client.stream("POST", "/tts/stream", json=payload) as response:
        assert response.status_code == 200
        assert response.headers.get("X-Sample-Rate") == "24000"
        chunks = list(response.iter_bytes())
    assert len(chunks) > 1
