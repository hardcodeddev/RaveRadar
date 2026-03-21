# Tag → numeric axis mapping
# Axes: energy, danceability, valence, bpm_tier, darkness
# Values are additive contributions (clamped to [-1, 1] at vector build time)

VIBE_TAGS: dict[str, dict[str, float]] = {
    "energetic":    {"energy": 0.9,  "danceability": 0.5},
    "dark":         {"valence": -0.6, "darkness": 0.7},
    "euphoric":     {"valence": 0.9,  "energy": 0.5},
    "dance":        {"danceability": 0.9, "energy": 0.3},
    "ambient":      {"energy": -0.8, "danceability": -0.5},
    "uplifting":    {"valence": 0.8,  "energy": 0.4},
    "aggressive":   {"energy": 0.7,  "darkness": 0.5, "danceability": 0.3},
    "groovy":       {"danceability": 0.8, "valence": 0.4},
    "melodic":      {"valence": 0.6,  "danceability": 0.3},
    "heavy":        {"energy": 0.6,  "darkness": 0.4, "danceability": 0.4},
    "chill":        {"energy": -0.7, "valence": 0.3,  "danceability": -0.3},
    "underground":  {"darkness": 0.4, "energy": 0.3},
    "trippy":       {"valence": 0.5,  "danceability": 0.4, "darkness": 0.3},
    "psychedelic":  {"valence": 0.4,  "energy": 0.5,  "darkness": 0.3},
    "hard":         {"energy": 0.8,  "danceability": 0.5, "darkness": 0.3},
    "minimal":      {"energy": -0.2, "danceability": 0.2},
    "peak time":    {"energy": 0.8,  "danceability": 0.7},
    "driving":      {"energy": 0.6,  "danceability": 0.6, "bpm_tier": 0.4},
    "fast":         {"bpm_tier": 0.8, "energy": 0.5},
    "slow":         {"bpm_tier": -0.5, "energy": -0.3},
    "bass":         {"energy": 0.5,  "danceability": 0.6, "darkness": 0.2},
    "industrial":   {"darkness": 0.8, "energy": 0.6,  "danceability": 0.2},
    "happy":        {"valence": 0.9,  "energy": 0.5,  "danceability": 0.5},
    "sad":          {"valence": -0.7, "energy": -0.3, "darkness": 0.3},
    "romantic":     {"valence": 0.7,  "energy": -0.2, "danceability": 0.3},
}
