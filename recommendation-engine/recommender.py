import numpy as np
from datetime import datetime, timezone
from feature_extractor import GENRE_LIST, VIBE_LIST

AXIS_NAMES = ["energy", "danceability", "valence", "bpm_tier", "darkness"]
AXIS_PHRASES = {
    "energy":       ("high-energy",  "low-energy"),
    "danceability": ("dancefloor",   "ambient"),
    "valence":      ("euphoric",     "dark"),
    "bpm_tier":     ("fast-paced",   "downtempo"),
    "darkness":     ("dark",         "bright"),
}


def build_user_profile(
    track_vectors: list[np.ndarray],
    added_at_list: list[datetime],
) -> np.ndarray | None:
    """Recency-weighted centroid of saved-track vectors."""
    if not track_vectors:
        return None

    now = datetime.now(timezone.utc)
    weights = []
    for added_at in added_at_list:
        if added_at.tzinfo is None:
            added_at = added_at.replace(tzinfo=timezone.utc)
        age_days = (now - added_at).total_seconds() / 86400
        weights.append(float(np.exp(-0.02 * age_days)))

    weights_arr = np.array(weights)
    weights_arr = weights_arr / weights_arr.sum()

    matrix = np.stack(track_vectors)
    profile = (matrix * weights_arr[:, np.newaxis]).sum(axis=0)

    norm = np.linalg.norm(profile)
    return profile / norm if norm > 0 else profile


def _make_reason(user_profile: np.ndarray, candidate_vector: np.ndarray, score: float = 0.0) -> str:
    parts = []

    # 1. Top genre match — most human-readable signal, always collect
    if len(user_profile) >= 20:
        genre_contrib = user_profile[5:20] * candidate_vector[5:20]
        top_g = int(np.argmax(genre_contrib))
        if genre_contrib[top_g] > 0.001:
            parts.append(GENRE_LIST[top_g])

    # 2. Top vibe match — always collect independently of genre
    if len(user_profile) >= 28:
        vibe_contrib = user_profile[20:28] * candidate_vector[20:28]
        top_v = int(np.argmax(vibe_contrib))
        if vibe_contrib[top_v] > 0.001:
            vibe_name = VIBE_LIST[top_v].lower()
            if vibe_name not in " ".join(parts):   # skip if already implied by genre label
                parts.append(vibe_name)

    # 3. Best audio axis — supplement when genre/vibe alone is thin
    if len(parts) < 2:
        audio_contrib = user_profile[:5] * candidate_vector[:5]
        for idx in np.argsort(np.abs(audio_contrib))[::-1]:
            val = float(audio_contrib[idx])
            if abs(val) >= 0.01:
                pos, neg = AXIS_PHRASES[AXIS_NAMES[idx]]
                parts.append(pos if val > 0 else neg)
                break

    if not parts:
        return "strong match" if score >= 0.6 else "matches your taste"

    qualifier = "strong " if score >= 0.65 else "good " if score >= 0.45 else ""
    return f"{qualifier}{' · '.join(parts)} match"


def score_candidates(
    user_profile: np.ndarray,
    candidate_data: list[dict],
) -> list[dict]:
    """Score candidate artists against the user profile.

    Each item in candidate_data: {id, vector, bpm, bpm_std}
    Returns list sorted by score descending.
    """
    user_energy = float(user_profile[0]) if len(user_profile) > 0 else 0.0
    user_bpm_tier = float(user_profile[3]) if len(user_profile) > 3 else 0.0
    user_vibe_vec = user_profile[20:] if len(user_profile) >= 28 else np.zeros(8)

    results = []
    for item in candidate_data:
        vec = item["vector"]

        # Base: cosine similarity (both are L2-normalised)
        score = float(np.dot(user_profile, vec))

        # BPM bonus: ±10 BPM of reconstructed user BPM
        candidate_bpm = item.get("bpm", 0) or 0
        user_bpm = user_bpm_tier * 120 + 80 if user_bpm_tier > 0 else 0
        if candidate_bpm > 0 and user_bpm > 0 and abs(candidate_bpm - user_bpm) <= 10:
            score += 0.05

        # Energy proximity bonus
        candidate_energy = float(vec[0]) if len(vec) > 0 else 0.0
        if abs(candidate_energy - user_energy) <= 0.15:
            score += 0.03

        # Vibe overlap bonus
        candidate_vibe_vec = vec[20:] if len(vec) >= 28 else np.zeros(8)
        if np.dot(user_vibe_vec, candidate_vibe_vec) > 0:
            score += 0.03

        # Incoherent-artist penalty
        if (item.get("bpm_std") or 0) > 30:
            score -= 0.05

        results.append({"id": item["id"], "score": score, "reason": _make_reason(user_profile, vec, score)})

    results.sort(key=lambda x: x["score"], reverse=True)
    return results
