from __future__ import annotations
from typing import Optional
from pydantic import BaseModel


class TrackInput(BaseModel):
    artist_name: str
    song_name: str
    genres: list[str] = []
    vibes: list[str] = []
    added_at: Optional[str] = None  # ISO-8601 datetime string


class CandidateArtist(BaseModel):
    id: int
    name: str
    genres: list[str] = []
    vibes: list[str] = []
    top_tracks: list[str] = []
    popularity: int = 0


class ScoredArtist(BaseModel):
    id: int
    score: float
    reason: str


class RecommendRequest(BaseModel):
    user_id: int
    saved_tracks: list[TrackInput] = []
    favorite_artist_names: list[str] = []
    candidate_artists: list[CandidateArtist] = []


class RecommendResponse(BaseModel):
    artists: list[ScoredArtist]
    source: str = "ml"


class AudioFeatureRequest(BaseModel):
    artist_name: str
    song_name: str


class AudioFeatureDto(BaseModel):
    bpm_value: Optional[float] = None
    energy_score: Optional[float] = None
    danceability_score: Optional[float] = None
    valence_score: Optional[float] = None
    darkness_score: Optional[float] = None
