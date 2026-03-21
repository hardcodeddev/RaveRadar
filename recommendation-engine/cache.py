import json
import os
import time
from typing import Any

_CACHE_FILE = os.getenv("ML_CACHE_FILE", "/tmp/raveradar_ml_cache.json")


class TTLCache:
    """In-memory TTL cache that persists to a JSON file so entries survive
    container restarts and Render deploys."""

    def __init__(self, path: str = _CACHE_FILE):
        self._path = path
        self._store: dict[str, tuple[Any, float]] = {}
        self._load()

    # ── persistence ──────────────────────────────────────────────────────────

    def _load(self) -> None:
        try:
            with open(self._path, "r") as f:
                raw = json.load(f)
            now = time.time()
            self._store = {
                k: (v, exp)
                for k, (v, exp) in raw.items()
                if exp > now           # drop already-expired entries
            }
        except (FileNotFoundError, json.JSONDecodeError, Exception):
            self._store = {}

    def _save(self) -> None:
        try:
            with open(self._path, "w") as f:
                json.dump(self._store, f, separators=(",", ":"))
        except Exception:
            pass  # non-fatal — in-memory still works

    # ── public API ────────────────────────────────────────────────────────────

    def get(self, key: str) -> Any | None:
        entry = self._store.get(key)
        if entry is None:
            return None
        value, expires_at = entry
        if time.time() > expires_at:
            del self._store[key]
            return None
        return value

    def set(self, key: str, value: Any, ttl_seconds: int) -> None:
        self._store[key] = (value, time.time() + ttl_seconds)
        self._save()


cache = TTLCache()

TTL_DEEZER       = 24 * 3600   # 24 h
TTL_MUSICBRAINZ  = 12 * 3600   # 12 h
