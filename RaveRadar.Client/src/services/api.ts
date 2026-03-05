import axios from 'axios';
import type { Artist, Genre, Event, User, RegisterDto, LoginDto, PreferencesDto } from './models';

const api = axios.create({
    baseURL: 'http://localhost:5057/api',
});

export const getArtists = async (search?: string) => {
    const params: Record<string, any> = {};
    if (search) params.search = search;
    return (await api.get<Artist[]>('/Artists', { params })).data;
};
export const getGenres = async (search?: string) => {
    const params: Record<string, any> = {};
    if (search) params.search = search;
    return (await api.get<Genre[]>('/Genres', { params })).data;
};
export const getEvents = async (city?: string, userId?: number) => {
    const params: Record<string, any> = {};
    if (city) params.city = city;
    if (userId) params.userId = userId;
    return (await api.get<Event[]>('/Events', { params })).data;
};

export const register = async (dto: RegisterDto) => (await api.post<Partial<User>>('/Users/register', dto)).data;
export const login = async (dto: LoginDto) => (await api.post<User>('/Users/login', dto)).data;
export const updatePreferences = async (userId: number, dto: PreferencesDto) => 
    (await api.post<User>(`/Users/${userId}/preferences`, dto)).data;
