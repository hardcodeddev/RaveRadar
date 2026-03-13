import React, { createContext, useContext, useState, useEffect } from 'react';
import axios from 'axios';
import type { User } from '../services/models';

const apiBase = (import.meta.env.VITE_API_BASE_URL ?? '') + '/api';

interface AuthContextType {
    user: User | null;
    login: (user: User) => void;
    logout: () => void;
    updateUser: (userData: Partial<User>) => void;
    loading: boolean;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export const AuthProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
    const [user, setUser] = useState<User | null>(null);
    const [loading, setLoading] = useState(true);

    useEffect(() => {
        const storedUser = localStorage.getItem('raveradar_user');
        if (!storedUser) { setLoading(false); return; }

        const parsed: User = JSON.parse(storedUser);
        // Validate the session is still valid (DB may have been wiped on redeploy)
        axios.get(`${apiBase}/Users/${parsed.id}`)
            .then(res => {
                setUser(res.data);
                localStorage.setItem('raveradar_user', JSON.stringify(res.data));
            })
            .catch(() => {
                // User no longer exists — clear stale session silently
                localStorage.removeItem('raveradar_user');
                setUser(null);
            })
            .finally(() => setLoading(false));
    }, []);

    const login = (userData: User) => {
        setUser(userData);
        localStorage.setItem('raveradar_user', JSON.stringify(userData));
    };

    const logout = () => {
        setUser(null);
        localStorage.removeItem('raveradar_user');
    };

    const updateUser = (updates: Partial<User>) => {
        setUser(prev => {
            if (!prev) return null;
            const updated = { ...prev, ...updates };
            localStorage.setItem('raveradar_user', JSON.stringify(updated));
            return updated;
        });
    };

    return (
        <AuthContext.Provider value={{ user, login, logout, updateUser, loading }}>
            {children}
        </AuthContext.Provider>
    );
};

export const useAuth = () => {
    const context = useContext(AuthContext);
    if (context === undefined) {
        throw new Error('useAuth must be used within an AuthProvider');
    }
    return context;
};
