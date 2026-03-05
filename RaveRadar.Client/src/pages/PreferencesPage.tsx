import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { getArtists, updatePreferences } from '../services/api';
import { useAuth } from '../services/AuthContext';
import type { Artist } from '../services/models';

const PreferencesPage = () => {
    const [search, setSearch] = useState('');
    const [artists, setArtists] = useState<Artist[]>([]);
    const [selectedArtistIds, setSelectedArtistIds] = useState<number[]>([]);
    const { user, login } = useAuth();
    const navigate = useNavigate();

    useEffect(() => {
        const fetchInitial = async () => {
            const results = await getArtists();
            setArtists(results);
        };
        fetchInitial();
    }, []);

    const handleSearch = async (e: React.FormEvent) => {
        e.preventDefault();
        try {
            const results = await getArtists(search);
            setArtists(results);
        } catch (error) {
            console.error('Failed to search artists:', error);
        }
    };

    const toggleArtist = (id: number) => {
        setSelectedArtistIds(prev => 
            prev.includes(id) ? prev.filter(i => i !== id) : [...prev, id]
        );
    };

    const handleSave = async () => {
        if (!user) return;
        try {
            const updatedUser = await updatePreferences(user.id, { 
                artistIds: selectedArtistIds 
            });
            login(updatedUser);
            navigate('/');
        } catch (error) {
            console.error('Failed to save preferences:', error);
            alert('Something went wrong saving your favorites.');
        }
    };

    return (
        <div className="preferences-page">
            <header className="page-header">
                <h1>Who do you follow?</h1>
                <p>Select artists you love to get personalized event recommendations.</p>
            </header>

            <form onSubmit={handleSearch} className="search-bar">
                <input 
                    type="text" 
                    placeholder="Search artists..." 
                    value={search} 
                    onChange={(e) => setSearch(e.target.value)}
                />
                <button type="submit" className="btn btn-secondary">Search</button>
            </form>

            <div className="artist-selection-grid">
                {artists.map(artist => (
                    <div 
                        key={artist.id} 
                        className={`artist-select-card ${selectedArtistIds.includes(artist.id) ? 'selected' : ''}`}
                        onClick={() => toggleArtist(artist.id)}
                    >
                        <img src={artist.imageUrl || 'https://via.placeholder.com/150'} alt={artist.name} />
                        <div className="info">
                            <span>{artist.name}</span>
                        </div>
                    </div>
                ))}
            </div>

            <div className="sticky-footer">
                <button 
                    onClick={handleSave} 
                    className="btn btn-primary"
                    disabled={selectedArtistIds.length === 0}
                >
                    Save & Explore ({selectedArtistIds.length})
                </button>
            </div>
        </div>
    );
};

export default PreferencesPage;
