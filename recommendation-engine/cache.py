import time
from typing import Any

class TTLCache:
    def __init__(self):
        self._store: dict[str, tuple[Any, float]] = {}

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


cache = TTLCache()

TTL_DEEZER = 24 * 3600       # 24 h
TTL_LASTFM_TAGS = 6 * 3600   # 6 h
TTL_LASTFM_SIMILAR = 12 * 3600  # 12 h
