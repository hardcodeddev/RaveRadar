import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { getRecommendations, addFavoriteArtist, removeFavoriteArtist, saveTrack, removeSavedTrack } from '../services/api';
import { useAuth } from '../services/AuthContext';
import type { Artist, SongResult } from '../services/models';

const DiscoverPage = () => {
    const { user, updateUser } = useAuth();
    const [loading, setLoading] = useState(true);
    const [tab, setTab] = useState<'artists' | 'songs'>('artists');
    const [artists, setArtists] = useState<Artist[]>([]);
    const [songs, setSongs] = useState<SongResult[]>([]);
    const [playingKey, setPlayingKey] = useState<string | null>(null);
    const audioRef = useRef<HTMLAudioElement | null>(null);

    useEffect(() => {
        if (!user) { setLoading(false); return; }
        getRecommendations(user.id)
            .then(data => { setArtists(data.artists); setSongs(data.songs); })
            .catch(console.error)
            .finally(() => setLoading(false));
    }, [user?.id]);

    // Clean up audio on unmount
    useEffect(() => () => { audioRef.current?.pause(); }, []);

    const toggleFavArtist = async (artist: Artist) => {
        if (!user) return;
        const isFav = user.favoriteArtists.some(a => a.id === artist.id);
        const updated = isFav
            ? await removeFavoriteArtist(user.id, artist.id)
            : await addFavoriteArtist(user.id, artist.id);
        updateUser({ favoriteArtists: updated });
    };

    const toggleSave = async (song: SongResult) => {
        if (!user) return;
        const existing = (user.savedTracks ?? []).find(t =>
            (song.spotifyTrackId && t.spotifyTrackId === song.spotifyTrackId) ||
            (t.artistName === song.artistName && t.songName === song.songName)
        );
        if (existing) {
            const updated = await removeSavedTrack(user.id, existing.id);
            updateUser({ savedTracks: updated });
        } else {
            const updated = await saveTrack(user.id, song);
            updateUser({ savedTracks: updated });
        }
    };

    const togglePreview = (previewUrl: string, key: string) => {
        if (playingKey === key) {
            audioRef.current?.pause();
            setPlayingKey(null);
        } else {
            audioRef.current?.pause();
            audioRef.current = new Audio(previewUrl);
            audioRef.current.volume = 0.6;
            audioRef.current.play().catch(() => {});
            audioRef.current.onended = () => setPlayingKey(null);
            setPlayingKey(key);
        }
    };

    if (!user) {
        return (
            <div className="discover-page">
                <div className="auth-prompt">
                    <h2>Discover new music</h2>
                    <p>Sign in to get personalized artist and song recommendations.</p>
                    <Link to="/login" className="btn">Login</Link>
                </div>
            </div>
        );
    }

    const hasPref = user.favoriteArtists.length > 0 || user.favoriteGenres.length > 0;
    const seedLabel = user.favoriteArtists.length > 0
        ? user.favoriteArtists.slice(0, 2).map(a => a.name).join(', ') + (user.favoriteArtists.length > 2 ? ' & more' : '')
        : null;

    return (
        <div className="discover-page">
            <div className="discover-header">
                <div className="page-header" style={{ marginBottom: 0 }}>
                    <h1>Discover</h1>
                    <p>{hasPref && seedLabel ? `Based on ${seedLabel}` : 'Top picks right now'}</p>
                </div>
                {!hasPref && (
                    <Link to="/preferences" className="demo-btn" style={{ textDecoration: 'none', flexShrink: 0 }}>
                        Set preferences →
                    </Link>
                )}
            </div>

            <div className="discover-tabs">
                <button className={`tab-btn ${tab === 'artists' ? 'active' : ''}`} onClick={() => setTab('artists')}>
                    Artists
                    {artists.length > 0 && <span className="tab-count">{artists.length}</span>}
                </button>
                <button className={`tab-btn ${tab === 'songs' ? 'active' : ''}`} onClick={() => setTab('songs')}>
                    Songs
                    {songs.length > 0 && <span className="tab-count">{songs.length}</span>}
                </button>
            </div>

            {loading ? (
                <div className="loading">Scanning the radar...</div>
            ) : tab === 'artists' ? (
                <div className="grid">
                    {artists.length > 0 ? artists.map(artist => {
                        const isFav = user.favoriteArtists.some(a => a.id === artist.id);
                        return (
                            <div key={artist.id} className="card">
                                <img
                                    src={artist.imageUrl || `https://ui-avatars.com/api/?name=${encodeURIComponent(artist.name)}&background=141414&color=00ffcc&size=300`}
                                    alt={artist.name}
                                    className="card-img"
                                />
                                <div className="card-content">
                                    {artist.reason && (
                                        <div className="rec-reason">
                                            {artist.fromMl && <span className="ml-badge">ML</span>}
                                            {artist.reason}
                                        </div>
                                    )}
                                    <div className="card-header-flex">
                                        <h3>{artist.name}</h3>
                                        <button
                                            className={`fav-btn ${isFav ? 'active' : ''}`}
                                            onClick={() => toggleFavArtist(artist)}
                                            title={isFav ? 'Remove from favorites' : 'Add to favorites'}
                                        >
                                            {isFav ? '★' : '☆'}
                                        </button>
                                    </div>
                                    <p className="genres">{artist.genres?.join(', ') || 'Electronic'}</p>
                                    {artist.bio && <p className="artist-bio">{artist.bio}</p>}
                                    <div className="links">
                                        <a href={`https://open.spotify.com/search/${encodeURIComponent(artist.name)}`} target="_blank" rel="noreferrer" className="link spotify">Spotify</a>
                                        <a href={`https://soundcloud.com/search?q=${encodeURIComponent(artist.name)}`} target="_blank" rel="noreferrer" className="link soundcloud">SoundCloud</a>
                                    </div>
                                </div>
                            </div>
                        );
                    }) : (
                        <div className="no-results">
                            No recommendations yet.{' '}
                            <Link to="/preferences" className="inline-link">Add favorite artists</Link> to personalize your feed.
                        </div>
                    )}
                </div>
            ) : (
                songs.length > 0 ? (
                    <div className="songs-list">
                        {songs.map((song, i) => {
                            const key = `${song.artistName}-${song.songName}`;
                            const songStr = `${song.artistName} - ${song.songName}`;
                            const savedTracks = user.savedTracks ?? [];
                            const isSaved = savedTracks.some(t =>
                                (song.spotifyTrackId && t.spotifyTrackId === song.spotifyTrackId) ||
                                (t.artistName === song.artistName && t.songName === song.songName)
                            );
                            const isPlaying = playingKey === key;

                            return (
                                <div key={i} className="song-row">
                                    <span className="song-index">{i + 1}</span>
                                    <img
                                        src={song.imageUrl || `https://ui-avatars.com/api/?name=${encodeURIComponent(song.artistName)}&background=141414&color=ff00ff&size=100`}
                                        alt={song.artistName}
                                        className="song-thumb"
                                    />
                                    <div className="song-info">
                                        <span className="song-title">{song.songName}</span>
                                        <span className="song-artist">{song.artistName}</span>
                                        {song.reason && <span className="song-reason">{song.reason}</span>}
                                        {song.bpmValue != null && (
                                            <div className="audio-pills">
                                                <span className="audio-pill">{`BPM: ${Math.round(song.bpmValue)}`}</span>
                                                {song.energyScore != null && (
                                                    <span className="audio-pill">
                                                        {`Energy ${'●'.repeat(Math.round(Math.max(0, Math.min(1, song.energyScore)) * 5))}${'○'.repeat(5 - Math.round(Math.max(0, Math.min(1, song.energyScore)) * 5))}`}
                                                    </span>
                                                )}
                                                {song.danceabilityScore != null && song.danceabilityScore > 0.5 && (
                                                    <span className="audio-pill">Dance</span>
                                                )}
                                            </div>
                                        )}
                                    </div>
                                    <div className="song-actions">
                                        {song.previewUrl && (
                                            <button
                                                className={`preview-btn ${isPlaying ? 'playing' : ''}`}
                                                onClick={() => togglePreview(song.previewUrl!, key)}
                                                title={isPlaying ? 'Pause preview' : 'Play 30s preview'}
                                            >
                                                {isPlaying ? '■' : '▶'}
                                            </button>
                                        )}
                                        <a
                                            href={song.externalUrl || `https://open.spotify.com/search/${encodeURIComponent(songStr)}`}
                                            target="_blank"
                                            rel="noreferrer"
                                            className="link spotify"
                                        >
                                            Spotify
                                        </a>
                                        <a
                                            href={`https://soundcloud.com/search?q=${encodeURIComponent(songStr)}`}
                                            target="_blank"
                                            rel="noreferrer"
                                            className="link soundcloud"
                                        >
                                            SC
                                        </a>
                                        <button
                                            className={`save-song-btn ${isSaved ? 'saved' : ''}`}
                                            onClick={() => toggleSave(song)}
                                            title={isSaved ? 'Remove from saved' : 'Save track'}
                                        >
                                            {isSaved ? '♥' : '♡'}
                                        </button>
                                    </div>
                                </div>
                            );
                        })}
                    </div>
                ) : (
                    <div className="no-results" style={{ display: 'block' }}>
                        No song recommendations yet.{' '}
                        <Link to="/preferences" className="inline-link">Add favorite artists</Link> to unlock picks.
                    </div>
                )
            )}
        </div>
    );
};

export default DiscoverPage;
