export interface Artist {
    id: number;
    name: string;
    spotifyId?: string;
    imageUrl?: string;
    genres: string[];
    popularity: number;
    bio?: string;
    topTracks: string[];
    reason?: string;
    fromMl?: boolean;
}

export interface Genre {
    id: number;
    name: string;
}

export interface Event {
    id: string;
    name: string;
    date: string;
    venue?: string;
    city?: string;
    ticketUrl?: string;
    imageUrl?: string;
    latitude: number;
    longitude: number;
    artistNames: string[];
    genreNames: string[];
    reason?: string;
}

export interface User {
    id: number;
    email: string;
    location?: string;
    favoriteArtists: Artist[];
    favoriteGenres: Genre[];
    favoriteSongs: string[];
    savedTracks: SavedTrack[];
}

export interface RegisterDto {
    email: string;
    password: string;
    location?: string;
}

export interface LoginDto {
    email: string;
    password: string;
}

export interface PreferencesDto {
    location?: string;
    artistIds?: number[];
    genreIds?: number[];
    favoriteSongs?: string[];
}

export interface SavedTrack {
    id: number;
    spotifyTrackId?: string;
    songName: string;
    artistName: string;
    artistSpotifyId?: string;
    imageUrl?: string;
    previewUrl?: string;
    externalUrl?: string;
    genres: string[];
    vibes: string[];
    addedAt: string;
}

export interface SongResult {
    artistId: number;
    artistName: string;
    songName: string;
    artistSpotifyId?: string;
    spotifyTrackId?: string;
    imageUrl?: string;
    previewUrl?: string;
    externalUrl?: string;
    source?: string;
    reason?: string;
    bpmValue?: number;
    energyScore?: number;
    danceabilityScore?: number;
}

export interface RecommendationsResult {
    artists: Artist[];
    songs: SongResult[];
}
