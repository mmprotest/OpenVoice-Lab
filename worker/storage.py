from __future__ import annotations

import json
import os
import sqlite3
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Dict, Iterable, List, Optional


@dataclass
class StoragePaths:
    root: Path
    models: Path
    voices: Path
    outputs: Path
    logs: Path
    pronunciation: Path
    history: Path
    projects: Path
    db: Path


def resolve_root() -> Path:
    local_appdata = os.environ.get("LOCALAPPDATA")
    if local_appdata:
        root = Path(local_appdata) / "OpenVoiceLab"
    else:
        root = Path.home() / ".local" / "share" / "OpenVoiceLab"
    root.mkdir(parents=True, exist_ok=True)
    return root


def get_paths() -> StoragePaths:
    root = resolve_root()
    models = root / "models"
    voices = root / "voices"
    outputs = root / "outputs"
    logs = root / "logs"
    pronunciation = root / "pronunciation"
    history = root / "history"
    projects = root / "projects"
    db = root / "history.db"
    for path in [models, voices, outputs, logs, pronunciation, history, projects]:
        path.mkdir(parents=True, exist_ok=True)
    (voices / "user").mkdir(parents=True, exist_ok=True)
    return StoragePaths(
        root=root,
        models=models,
        voices=voices,
        outputs=outputs,
        logs=logs,
        pronunciation=pronunciation,
        history=history,
        projects=projects,
        db=db,
    )


def read_json(path: Path) -> Dict[str, Any]:
    if not path.exists():
        return {}
    return json.loads(path.read_text(encoding="utf-8"))


def write_json(path: Path, data: Dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, indent=2), encoding="utf-8")


class Database:
    def __init__(self, path: Path) -> None:
        self.path = path
        self._init_schema()

    def _connect(self) -> sqlite3.Connection:
        conn = sqlite3.connect(self.path)
        conn.row_factory = sqlite3.Row
        return conn

    def _init_schema(self) -> None:
        with self._connect() as conn:
            conn.execute(
                """
                CREATE TABLE IF NOT EXISTS projects (
                    project_id TEXT PRIMARY KEY,
                    name TEXT NOT NULL,
                    created_at TEXT NOT NULL
                )
                """
            )
            conn.execute(
                """
                CREATE TABLE IF NOT EXISTS history (
                    job_id TEXT PRIMARY KEY,
                    text TEXT NOT NULL,
                    voice_id TEXT NOT NULL,
                    output_path TEXT NOT NULL,
                    created_at TEXT NOT NULL,
                    project_id TEXT
                )
                """
            )
            conn.execute(
                """
                CREATE TABLE IF NOT EXISTS pronunciation_profiles (
                    profile_id TEXT PRIMARY KEY,
                    name TEXT NOT NULL,
                    created_at TEXT NOT NULL
                )
                """
            )
            conn.execute(
                """
                CREATE TABLE IF NOT EXISTS pronunciation_entries (
                    profile_id TEXT NOT NULL,
                    source TEXT NOT NULL,
                    target TEXT NOT NULL,
                    FOREIGN KEY(profile_id) REFERENCES pronunciation_profiles(profile_id)
                )
                """
            )
            conn.commit()

    def list_projects(self) -> List[Dict[str, Any]]:
        with self._connect() as conn:
            rows = conn.execute("SELECT project_id, name, created_at FROM projects ORDER BY created_at DESC").fetchall()
        return [dict(row) for row in rows]

    def create_project(self, project_id: str, name: str, created_at: str) -> None:
        with self._connect() as conn:
            conn.execute(
                "INSERT INTO projects (project_id, name, created_at) VALUES (?, ?, ?)",
                (project_id, name, created_at),
            )
            conn.commit()

    def add_history(self, entry: Dict[str, Any]) -> None:
        with self._connect() as conn:
            conn.execute(
                """
                INSERT OR REPLACE INTO history
                    (job_id, text, voice_id, output_path, created_at, project_id)
                VALUES (?, ?, ?, ?, ?, ?)
                """,
                (
                    entry["job_id"],
                    entry["text"],
                    entry["voice_id"],
                    entry["output_path"],
                    entry["created_at"],
                    entry.get("project_id"),
                ),
            )
            conn.commit()

    def list_history(self, limit: int, project_id: Optional[str], query: Optional[str]) -> List[Dict[str, Any]]:
        sql = "SELECT job_id, text, voice_id, output_path, created_at, project_id FROM history"
        params: List[Any] = []
        clauses = []
        if project_id:
            clauses.append("project_id = ?")
            params.append(project_id)
        if query:
            clauses.append("LOWER(text) LIKE ?")
            params.append(f"%{query.lower()}%")
        if clauses:
            sql += " WHERE " + " AND ".join(clauses)
        sql += " ORDER BY created_at DESC LIMIT ?"
        params.append(limit)
        with self._connect() as conn:
            rows = conn.execute(sql, params).fetchall()
        return [dict(row) for row in rows]

    def get_history(self, job_id: str) -> Optional[Dict[str, Any]]:
        with self._connect() as conn:
            row = conn.execute(
                "SELECT job_id, text, voice_id, output_path, created_at, project_id FROM history WHERE job_id = ?",
                (job_id,),
            ).fetchone()
        return dict(row) if row else None

    def list_pronunciation_profiles(self) -> List[Dict[str, Any]]:
        with self._connect() as conn:
            profiles = conn.execute(
                "SELECT profile_id, name, created_at FROM pronunciation_profiles ORDER BY created_at DESC"
            ).fetchall()
            entries = conn.execute(
                "SELECT profile_id, source, target FROM pronunciation_entries"
            ).fetchall()
        profile_map: Dict[str, Dict[str, Any]] = {row["profile_id"]: dict(row) for row in profiles}
        for profile in profile_map.values():
            profile["entries"] = []
        for entry in entries:
            if entry["profile_id"] in profile_map:
                profile_map[entry["profile_id"]]["entries"].append(
                    {"from": entry["source"], "to": entry["target"]}
                )
        return list(profile_map.values())

    def create_pronunciation_profile(self, profile_id: str, name: str, created_at: str) -> None:
        with self._connect() as conn:
            conn.execute(
                "INSERT INTO pronunciation_profiles (profile_id, name, created_at) VALUES (?, ?, ?)",
                (profile_id, name, created_at),
            )
            conn.commit()

    def update_pronunciation_entries(self, profile_id: str, entries: Iterable[Dict[str, str]]) -> None:
        with self._connect() as conn:
            conn.execute("DELETE FROM pronunciation_entries WHERE profile_id = ?", (profile_id,))
            conn.executemany(
                "INSERT INTO pronunciation_entries (profile_id, source, target) VALUES (?, ?, ?)",
                [(profile_id, entry["from"], entry["to"]) for entry in entries],
            )
            conn.commit()

    def delete_all(self) -> None:
        with self._connect() as conn:
            conn.execute("DELETE FROM history")
            conn.execute("DELETE FROM projects")
            conn.execute("DELETE FROM pronunciation_entries")
            conn.execute("DELETE FROM pronunciation_profiles")
            conn.commit()
