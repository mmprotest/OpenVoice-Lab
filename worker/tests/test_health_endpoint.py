import pytest
from fastapi.testclient import TestClient

from app import app


def test_health_endpoint_returns_ok_and_version():
    client = TestClient(app)
    resp = client.get("/health")
    assert resp.status_code == 200
    data = resp.json()
    # API contract: ok + version
    assert "ok" in data
    assert data["ok"] is True
    assert "version" in data
    assert isinstance(data["version"], str)
    assert len(data["version"]) > 0
    assert pytest is not None
