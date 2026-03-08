import { useState, useEffect, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import { getArtists, updatePreferences, searchSongs, removeSavedTrack } from '../services/api';
import { useAuth } from '../services/AuthContext';
import type { Artist, SongResult } from '../services/models';

const PreferencesPage = () => {
    const { user, login, updateUser } = useAuth();
    const navigate = useNavigate();

    const [search, setSearch] = useState('');
    const [artists, setArtists] = useState<Artist[]>([]);
    const [location, setLocation] = useState(user?.location || '');
    const [selectedArtistIds, setSelectedArtistIds] = useState<number[]>(
        user?.favoriteArtists.map(a => a.id) || []
    );
    const [favoriteSongs, setFavoriteSongs] = useState<string[]>(user?.favoriteSongs || []);
    const [saving, setSaving] = useState(false);

    // Song search
    const [songQuery, setSongQuery] = useState('');
    const [songResults, setSongResults] = useState<SongResult[]>([]);
    const [showDropdown, setShowDropdown] = useState(false);
    const songWrapRef = useRef<HTMLDivElement>(null);
    const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);

    useEffect(() => {
        getArtists().then(setArtists).catch(console.error);
    }, []);

    useEffect(() => {
        const handler = (e: MouseEvent) => {
            if (songWrapRef.current && !songWrapRef.current.contains(e.target as Node)) {
                setShowDropdown(false);
            }
        };
        document.addEventListener('mousedown', handler);
        return () => document.removeEventListener('mousedown', handler);
    }, []);

    const handleArtistSearch = async (e: React.FormEvent) => {
        e.preventDefault();
        const data = await getArtists(search);
        setArtists(data);
    };

    const toggleArtist = (id: number) => {
        setSelectedArtistIds(prev =>
            prev.includes(id) ? prev.filter(i => i !== id) : [...prev, id]
        );
    };

    const handleSongQueryChange = (value: string) => {
        setSongQuery(value);
        if (debounceRef.current) clearTimeout(debounceRef.current);
        if (value.trim().length < 2) { setSongResults([]); setShowDropdown(false); return; }
        debounceRef.current = setTimeout(async () => {
            const results = await searchSongs(value.trim());
            setSongResults(results);
            setShowDropdown(results.length > 0);
        }, 280);
    };

    const addSongFromResult = (song: SongResult) => {
        const str = `${song.artistName} - ${song.songName}`;
        if (!favoriteSongs.includes(str)) setFavoriteSongs(prev => [...prev, str]);
        setSongQuery(''); setSongResults([]); setShowDropdown(false);
    };

    const addSongManual = (e: React.FormEvent) => {
        e.preventDefault();
        const str = songQuery.trim();
        if (str && !favoriteSongs.includes(str)) {
            setFavoriteSongs(prev => [...prev, str]);
            setSongQuery(''); setSongResults([]); setShowDropdown(false);
        }
    };

    const handleSave = async () => {
        if (!user) return;
        setSaving(true);
        try {
            const updated = await updatePreferences(user.id, {
                location: location || undefined,
                artistIds: selectedArtistIds,
                favoriteSongs,
            });
            login(updated);
            navigate('/');
        } catch {
            alert('Something went wrong saving your preferences.');
        } finally {
            setSaving(false);
        }
    };

    return (
        <div className="preferences-page">
            <header className="page-header">
                <h1>Account & Preferences</h1>
                <p>Set your city to filter events near you. Pick artists to personalize recommendations.</p>
            </header>

            {/* City */}
            <section className="pref-section city-section">
                <h2>Your City</h2>
                <p className="pref-section-desc">Dashboard events filter to this city by default.</p>
                <input
                    type="text"
                    className="pref-input"
                    placeholder="e.g. Las Vegas, Miami, Los Angeles"
                    value={location}
                    onChange={e => setLocation(e.target.value)}
                />
                <small>Toggle "Show all cities" on the dashboard anytime.</small>
            </section>

            {/* Favorite Artists */}
            <section className="pref-section">
                <h2>Favorite Artists</h2>
                <p className="pref-section-desc">
                    {selectedArtistIds.length > 0
                        ? `${selectedArtistIds.length} selected`
                        : 'Select artists to power your recommendations'}
                </p>
                <form onSubmit={handleArtistSearch} className="search-bar">
                    <input
                        type="text"
                        placeholder="Search artists..."
                        value={search}
                        onChange={e => setSearch(e.target.value)}
                    />
                    <button type="submit" className="btn-secondary">Search</button>
                </form>
                <div className="artist-selection-grid">
                    {artists.map(artist => (
                        <div
                            key={artist.id}
                            className={`artist-select-card ${selectedArtistIds.includes(artist.id) ? 'selected' : ''}`}
                            onClick={() => toggleArtist(artist.id)}
                        >
                            <img
                                src={artist.imageUrl || `https://ui-avatars.com/api/?name=${encodeURIComponent(artist.name)}&background=1c1c1c&color=888&size=200`}
                                alt={artist.name}
                            />
                            <div className="info">{artist.name}</div>
                        </div>
                    ))}
                </div>
            </section>

            {/* On Repeat */}
            <section className="pref-section">
                <h2>On Repeat</h2>
                <p className="pref-section-desc">Songs you're vibing to — used to tune your recommendations.</p>
                <div className="song-search-wrapper" ref={songWrapRef}>
                    <form onSubmit={addSongManual} className="song-input-group">
                        <input
                            type="text"
                            placeholder="Search song or artist..."
                            value={songQuery}
                            onChange={e => handleSongQueryChange(e.target.value)}
                            onFocus={() => songResults.length > 0 && setShowDropdown(true)}
                            autoComplete="off"
                        />
                        <button type="submit" className="demo-btn">Add</button>
                    </form>
                    {showDropdown && (
                        <div className="song-dropdown">
                            {songResults.map((song, i) => (
                                <div
                                    key={i}
                                    className="song-dropdown-item"
                                    onMouseDown={() => addSongFromResult(song)}
                                >
                                    <img
                                        src={song.imageUrl || `https://ui-avatars.com/api/?name=${encodeURIComponent(song.artistName)}&background=1c1c1c&color=888&size=80`}
                                        alt={song.artistName}
                                        className="song-dropdown-img"
                                    />
                                    <div className="song-dropdown-info">
                                        <span className="song-dropdown-title">{song.songName}</span>
                                        <span className="song-dropdown-artist">{song.artistName}</span>
                                    </div>
                                    {song.source === 'Spotify' && (
                                        <span className="song-dropdown-source">SPOTIFY</span>
                                    )}
                                </div>
                            ))}
                        </div>
                    )}
                </div>
                {favoriteSongs.length > 0 && (
                    <div className="song-tags">
                        {favoriteSongs.map(song => (
                            <span key={song} className="song-tag">
                                {song}
                                <button onClick={() => setFavoriteSongs(prev => prev.filter(s => s !== song))} className="remove-tag">×</button>
                            </span>
                        ))}
                    </div>
                )}
            </section>

            {/* Saved Tracks */}
            {user?.savedTracks && user.savedTracks.length > 0 && (
                <section className="pref-section">
                    <h2>Saved Tracks</h2>
                    <p className="pref-section-desc">{user.savedTracks.length} track{user.savedTracks.length !== 1 ? 's' : ''} saved from Discover</p>
                    <div className="saved-tracks-list">
                        {user.savedTracks.map(track => (
                            <div key={track.id} className="saved-track-card">
                                <img
                                    src={track.imageUrl || `https://ui-avatars.com/api/?name=${encodeURIComponent(track.artistName)}&background=1c1c1c&color=888&size=80`}
                                    alt={track.artistName}
                                    className="saved-track-img"
                                />
                                <div className="saved-track-info">
                                    <span className="saved-track-title">{track.songName}</span>
                                    <span className="saved-track-artist">{track.artistName}</span>
                                    <div className="saved-track-pills">
                                        {track.genres.slice(0, 2).map(g => (
                                            <span key={g} className="pill genre-pill">{g}</span>
                                        ))}
                                        {track.vibes.map(v => (
                                            <span key={v} className="pill vibe-pill">{v}</span>
                                        ))}
                                    </div>
                                </div>
                                <div className="saved-track-actions">
                                    {track.previewUrl && (
                                        <a href={track.externalUrl || '#'} target="_blank" rel="noreferrer" className="link spotify" style={{ fontSize: '0.75rem' }}>
                                            Spotify
                                        </a>
                                    )}
                                    <button
                                        className="remove-saved-btn"
                                        onClick={async () => {
                                            const updated = await removeSavedTrack(user.id, track.id);
                                            updateUser({ savedTracks: updated });
                                        }}
                                        title="Remove"
                                    >
                                        ×
                                    </button>
                                </div>
                            </div>
                        ))}
                    </div>
                </section>
            )}

            <div className="sticky-footer">
                <button onClick={handleSave} className="btn-primary" disabled={saving}>
                    {saving ? 'Saving...' : 'Save & Explore'}
                </button>
            </div>
        </div>
    );
};

export default PreferencesPage;
