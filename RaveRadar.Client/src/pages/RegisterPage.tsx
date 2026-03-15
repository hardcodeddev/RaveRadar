import { useState, useRef, useEffect } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import {
    register as registerApi,
    login as loginApi,
    getCities,
    getArtists,
    searchSongs,
    updatePreferences,
} from '../services/api';
import { useAuth } from '../services/AuthContext';
import type { Artist, SongResult, User } from '../services/models';

const avatarFallback = (name: string) =>
    `https://ui-avatars.com/api/?name=${encodeURIComponent(name)}&background=1c1c1c&color=888&size=200`;

const RegisterPage = () => {
    const [step, setStep] = useState(1);

    // Step 1 — account
    const [email, setEmail] = useState('');
    const [password, setPassword] = useState('');
    const [location, setLocation] = useState('');
    const [cityResults, setCityResults] = useState<string[]>([]);
    const [showCityDropdown, setShowCityDropdown] = useState(false);
    const cityWrapRef = useRef<HTMLDivElement>(null);
    const cityDebounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);
    const [error, setError] = useState('');
    const [loading, setLoading] = useState(false);

    // Step 2 — artists
    const [artistSearch, setArtistSearch] = useState('');
    const [artists, setArtists] = useState<Artist[]>([]);
    const [selectedArtistIds, setSelectedArtistIds] = useState<number[]>([]);

    // Step 3 — songs
    const [songQuery, setSongQuery] = useState('');
    const [songResults, setSongResults] = useState<SongResult[]>([]);
    const [showSongDropdown, setShowSongDropdown] = useState(false);
    const [favoriteSongs, setFavoriteSongs] = useState<string[]>([]);
    const songWrapRef = useRef<HTMLDivElement>(null);
    const songDebounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);

    const [loggedInUser, setLoggedInUser] = useState<User | null>(null);

    const { login } = useAuth();
    const navigate = useNavigate();

    useEffect(() => {
        if (step === 2) {
            getArtists().then(setArtists).catch(console.error);
        }
    }, [step]);

    useEffect(() => {
        const handler = (e: MouseEvent) => {
            if (cityWrapRef.current && !cityWrapRef.current.contains(e.target as Node))
                setShowCityDropdown(false);
            if (songWrapRef.current && !songWrapRef.current.contains(e.target as Node))
                setShowSongDropdown(false);
        };
        document.addEventListener('mousedown', handler);
        return () => document.removeEventListener('mousedown', handler);
    }, []);

    const handleCityChange = (value: string) => {
        setLocation(value);
        if (cityDebounceRef.current) clearTimeout(cityDebounceRef.current);
        if (value.trim().length < 1) { setCityResults([]); setShowCityDropdown(false); return; }
        cityDebounceRef.current = setTimeout(async () => {
            const results = await getCities(value.trim());
            setCityResults(results);
            setShowCityDropdown(results.length > 0);
        }, 200);
    };

    const handleSubmitAccount = async (e: React.FormEvent) => {
        e.preventDefault();
        setError('');
        setLoading(true);
        try {
            await registerApi({ email, password, location: location || undefined });
            const user = await loginApi({ email, password });
            login(user);
            setLoggedInUser(user);
            setStep(2);
        } catch {
            setError('Registration failed. That email may already be taken.');
        } finally {
            setLoading(false);
        }
    };

    const handleArtistSearch = async (e: React.FormEvent) => {
        e.preventDefault();
        const data = await getArtists(artistSearch);
        setArtists(data);
    };

    const toggleArtist = (id: number) => {
        setSelectedArtistIds(prev =>
            prev.includes(id) ? prev.filter(i => i !== id) : [...prev, id]
        );
    };

    const handleSongQueryChange = (value: string) => {
        setSongQuery(value);
        if (songDebounceRef.current) clearTimeout(songDebounceRef.current);
        if (value.trim().length < 2) { setSongResults([]); setShowSongDropdown(false); return; }
        songDebounceRef.current = setTimeout(async () => {
            const results = await searchSongs(value.trim());
            setSongResults(results);
            setShowSongDropdown(results.length > 0);
        }, 280);
    };

    const addSongFromResult = (song: SongResult) => {
        const str = `${song.artistName} - ${song.songName}`;
        if (!favoriteSongs.includes(str)) setFavoriteSongs(prev => [...prev, str]);
        setSongQuery(''); setSongResults([]); setShowSongDropdown(false);
    };

    const addSongManual = (e: React.FormEvent) => {
        e.preventDefault();
        const str = songQuery.trim();
        if (str && !favoriteSongs.includes(str)) {
            setFavoriteSongs(prev => [...prev, str]);
            setSongQuery(''); setSongResults([]); setShowSongDropdown(false);
        }
    };

    const handleFinish = async () => {
        if (!loggedInUser) { navigate('/'); return; }
        setLoading(true);
        try {
            await updatePreferences(loggedInUser.id, {
                location: location || undefined,
                artistIds: selectedArtistIds,
                favoriteSongs,
            });
        } catch (err) {
            console.error('Could not save preferences', err);
        } finally {
            setLoading(false);
            navigate('/');
        }
    };

    const STEPS = ['Account', 'Artists', 'Songs'];
    const isWide = step > 1;

    return (
        <div className={`auth-page${isWide ? ' register-wizard' : ''}`}>
            <div className={`auth-card${isWide ? ' auth-card-wide' : ''}`}>

                {/* Step progress */}
                <div className="wizard-steps">
                    <div className="wizard-steps-line" />
                    {STEPS.map((label, i) => (
                        <div
                            key={label}
                            className={`wizard-step${i + 1 === step ? ' active' : ''}${i + 1 < step ? ' done' : ''}`}
                        >
                            <span className="wizard-step-num">{i + 1 < step ? '✓' : i + 1}</span>
                            <span className="wizard-step-label">{label}</span>
                        </div>
                    ))}
                </div>

                {/* ── Step 1: Account ── */}
                {step === 1 && (
                    <>
                        <h2>Create account</h2>
                        {error && <p className="error-message">{error}</p>}
                        <form onSubmit={handleSubmitAccount}>
                            <div className="form-group">
                                <label>Email</label>
                                <input
                                    type="email"
                                    value={email}
                                    onChange={e => setEmail(e.target.value)}
                                    placeholder="you@example.com"
                                    required
                                    autoFocus
                                />
                            </div>
                            <div className="form-group">
                                <label>Password</label>
                                <input
                                    type="password"
                                    value={password}
                                    onChange={e => setPassword(e.target.value)}
                                    placeholder="••••••••"
                                    required
                                />
                            </div>
                            <div className="form-group">
                                <label>City <span style={{ color: '#555', fontWeight: 400 }}>(optional)</span></label>
                                <div className="song-search-wrapper" ref={cityWrapRef}>
                                    <input
                                        type="text"
                                        value={location}
                                        onChange={e => handleCityChange(e.target.value)}
                                        placeholder="e.g. Los Angeles"
                                        onFocus={() => cityResults.length > 0 && setShowCityDropdown(true)}
                                        autoComplete="off"
                                    />
                                    {showCityDropdown && (
                                        <div className="song-dropdown">
                                            {cityResults.map((city, i) => (
                                                <div
                                                    key={i}
                                                    className="song-dropdown-item"
                                                    onMouseDown={() => { setLocation(city); setShowCityDropdown(false); }}
                                                >
                                                    <div className="song-dropdown-info">
                                                        <span className="song-dropdown-title">{city}</span>
                                                    </div>
                                                </div>
                                            ))}
                                        </div>
                                    )}
                                </div>
                            </div>
                            <button type="submit" className="btn-primary" disabled={loading}>
                                {loading ? 'Creating account...' : 'Next: Pick Artists →'}
                            </button>
                        </form>
                        <p>Already have an account? <Link to="/login">Login here</Link></p>
                    </>
                )}

                {/* ── Step 2: Artists ── */}
                {step === 2 && (
                    <>
                        <h2>Favorite Artists</h2>
                        <p className="wizard-desc">
                            Pick artists you love — we'll use these to personalize events and recommendations.
                            {selectedArtistIds.length > 0 && <strong style={{ color: 'var(--cyan)', marginLeft: 6 }}>{selectedArtistIds.length} selected</strong>}
                        </p>
                        <form onSubmit={handleArtistSearch} className="search-bar">
                            <input
                                type="text"
                                placeholder="Search artists..."
                                value={artistSearch}
                                onChange={e => setArtistSearch(e.target.value)}
                                autoFocus
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
                                        src={artist.imageUrl || avatarFallback(artist.name)}
                                        alt={artist.name}
                                        onError={e => { (e.target as HTMLImageElement).src = avatarFallback(artist.name); }}
                                    />
                                    <div className="info">{artist.name}</div>
                                </div>
                            ))}
                        </div>
                        <div className="wizard-footer">
                            <span className="wizard-skip" onClick={() => setStep(3)}>Skip</span>
                            <button className="btn-primary wizard-next-btn" onClick={() => setStep(3)}>
                                Next: Songs on Repeat →
                            </button>
                        </div>
                    </>
                )}

                {/* ── Step 3: Songs ── */}
                {step === 3 && (
                    <>
                        <h2>Songs on Repeat</h2>
                        <p className="wizard-desc">
                            Add tracks you've been vibing to — we'll tune your recommendations around your taste.
                        </p>
                        <div className="song-search-wrapper" ref={songWrapRef}>
                            <form onSubmit={addSongManual} className="song-input-group">
                                <input
                                    type="text"
                                    placeholder="Search song or artist..."
                                    value={songQuery}
                                    onChange={e => handleSongQueryChange(e.target.value)}
                                    onFocus={() => songResults.length > 0 && setShowSongDropdown(true)}
                                    autoComplete="off"
                                    autoFocus
                                />
                                <button type="submit" className="demo-btn">Add</button>
                            </form>
                            {showSongDropdown && (
                                <div className="song-dropdown">
                                    {songResults.map((song, i) => (
                                        <div
                                            key={i}
                                            className="song-dropdown-item"
                                            onMouseDown={() => addSongFromResult(song)}
                                        >
                                            <img
                                                src={song.imageUrl || avatarFallback(song.artistName)}
                                                alt={song.artistName}
                                                className="song-dropdown-img"
                                                onError={e => { (e.target as HTMLImageElement).src = avatarFallback(song.artistName); }}
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
                            <div className="song-tags" style={{ marginBottom: 16 }}>
                                {favoriteSongs.map(song => (
                                    <span key={song} className="song-tag">
                                        {song}
                                        <button
                                            onClick={() => setFavoriteSongs(prev => prev.filter(s => s !== song))}
                                            className="remove-tag"
                                        >×</button>
                                    </span>
                                ))}
                            </div>
                        )}
                        <div className="wizard-footer">
                            <span className="wizard-skip" onClick={handleFinish}>Skip</span>
                            <button
                                className="btn-primary wizard-next-btn"
                                onClick={handleFinish}
                                disabled={loading}
                            >
                                {loading ? 'Saving...' : 'Done & Explore →'}
                            </button>
                        </div>
                    </>
                )}
            </div>
        </div>
    );
};

export default RegisterPage;
