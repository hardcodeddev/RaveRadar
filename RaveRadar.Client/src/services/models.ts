export interface Artist {
    id: number;
    name: string;
    spotifyId?: string;
    imageUrl?: string;
    genres: string[];
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
}

export interface User {
    id: number;
    email: string;
    location?: string;
    favoriteArtists: Artist[];
    favoriteGenres: Genre[];
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
}
