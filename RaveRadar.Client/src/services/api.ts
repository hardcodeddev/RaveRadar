import axios from 'axios';
import type { Artist, Genre, Event, User, RegisterDto, LoginDto, PreferencesDto, SongResult, RecommendationsResult, SavedTrack } from './models';

const api = axios.create({
    baseURL: '/api',
});

export const getArtists = async (search?: string, genre?: string) => {
    const params: Record<string, any> = {};
    if (search) params.search = search;
    if (genre) params.genre = genre;
    return (await api.get<Artist[]>('/Artists', { params })).data;
};
export const getTopArtists = async (count: number = 5) => {
    return (await api.get<Artist[]>('/Artists/top', { params: { count } })).data;
};
export const getArtistsByGenre = async (genre: string) => {
    return (await api.get<Artist[]>(`/Artists/genre/${genre}`)).data;
};
export const getGenres = async (search?: string) => {
    const params: Record<string, any> = {};
    if (search) params.search = search;
    return (await api.get<Genre[]>('/Genres', { params })).data;
};
export const getEvents = async (city?: string, userId?: number, allCities?: boolean) => {
    const params: Record<string, any> = {};
    if (city) params.city = city;
    if (userId) params.userId = userId;
    if (allCities) params.allCities = true;
    return (await api.get<Event[]>('/Events', { params })).data;
};

export const register = async (dto: RegisterDto) => (await api.post<Partial<User>>('/Users/register', dto)).data;
export const login = async (dto: LoginDto) => (await api.post<User>('/Users/login', dto)).data;
export const updatePreferences = async (userId: number, dto: PreferencesDto) => 
    (await api.post<User>(`/Users/${userId}/preferences`, dto)).data;

export const addFavoriteArtist = async (userId: number, artistId: number) => 
    (await api.post<Artist[]>(`/Users/${userId}/favorites/artists/${artistId}`)).data;

export const removeFavoriteArtist = async (userId: number, artistId: number) => 
    (await api.delete<Artist[]>(`/Users/${userId}/favorites/artists/${artistId}`)).data;

export const toggleFavoriteSong = async (userId: number, songName: string) =>
    (await api.post<string[]>(`/Users/${userId}/favorites/songs`, songName, {
        headers: { 'Content-Type': 'application/json' }
    })).data;

export const searchSongs = async (q: string) =>
    (await api.get<SongResult[]>('/Artists/songs/search', { params: { q } })).data;

export const getRecommendations = async (userId: number) =>
    (await api.get<RecommendationsResult>(`/Users/${userId}/recommendations`)).data;

export const saveTrack = async (userId: number, song: SongResult) =>
    (await api.post<SavedTrack[]>(`/Users/${userId}/saved-tracks`, {
        spotifyTrackId: song.spotifyTrackId,
        songName: song.songName,
        artistName: song.artistName,
        artistSpotifyId: song.artistSpotifyId,
        imageUrl: song.imageUrl,
        previewUrl: song.previewUrl,
        externalUrl: song.externalUrl,
    })).data;

export const removeSavedTrack = async (userId: number, trackId: number) =>
    (await api.delete<SavedTrack[]>(`/Users/${userId}/saved-tracks/${trackId}`)).data;
