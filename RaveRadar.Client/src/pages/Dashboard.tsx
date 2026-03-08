import { useEffect, useState, useCallback } from 'react';
import { Link } from 'react-router-dom';
import { getArtists, getEvents, addFavoriteArtist, removeFavoriteArtist } from '../services/api';
import { useAuth } from '../services/AuthContext';
import type { Artist, Event } from '../services/models';

const Dashboard = () => {
    const { user, loading: authLoading, updateUser } = useAuth();
    const [artists, setArtists] = useState<Artist[]>([]);
    const [events, setEvents] = useState<Event[]>([]);
    const [loading, setLoading] = useState(true);
    const [searchQuery, setSearchQuery] = useState('');
    const [showAllCities, setShowAllCities] = useState(false);

    const fetchArtists = useCallback(async (query = '') => {
        const data = await getArtists(query);
        setArtists(data);
    }, []);

    const fetchEvents = useCallback(async (allCities: boolean) => {
        const data = await getEvents(undefined, user?.id, allCities);
        setEvents(data);
    }, [user?.id]);

    useEffect(() => {
        if (authLoading) return;
        let cancelled = false;
        (async () => {
            try {
                setLoading(true);
                await Promise.all([fetchArtists(), fetchEvents(showAllCities)]);
            } catch (e) {
                console.error(e);
            } finally {
                if (!cancelled) setLoading(false);
            }
        })();
        return () => { cancelled = true; };
    }, [user?.id, user?.location, authLoading, fetchArtists, fetchEvents, showAllCities]);

    const handleSearch = (e: React.FormEvent) => {
        e.preventDefault();
        fetchArtists(searchQuery);
    };

    const toggleFavorite = async (artist: Artist) => {
        if (!user) return;
        const isFav = user.favoriteArtists.some(a => a.id === artist.id);
        try {
            const updated = isFav
                ? await removeFavoriteArtist(user.id, artist.id)
                : await addFavoriteArtist(user.id, artist.id);
            updateUser({ favoriteArtists: updated });
        } catch (e) {
            console.error(e);
        }
    };

    return (
        <div>
            {/* Header */}
            <header className="dashboard-header">
                <div className="welcome-section">
                    <h1>
                        {user ? `Hey, ${user.email.split('@')[0]}` : 'Discover your next rave'}
                    </h1>
                    {user?.location ? (
                        <p>
                            {showAllCities ? 'All cities' : `Events in ${user.location}`}
                            {' · '}
                            <Link to="/preferences" className="inline-link">Change city</Link>
                        </p>
                    ) : (
                        <p>
                            {user
                                ? <><Link to="/preferences" className="inline-link">Set your city</Link> to filter events near you</>
                                : 'Log in to personalize your feed'
                            }
                        </p>
                    )}
                </div>
                <form className="dashboard-search" onSubmit={handleSearch}>
                    <input
                        type="text"
                        placeholder="Search artists..."
                        value={searchQuery}
                        onChange={e => setSearchQuery(e.target.value)}
                    />
                    <button type="submit" className="demo-btn">Search</button>
                </form>
            </header>

            {loading ? (
                <div className="loading">Loading the radar...</div>
            ) : (
                <>
                    {/* Artists */}
                    <section>
                        <h2>{searchQuery ? `Results for "${searchQuery}"` : 'Trending Artists'}</h2>
                        <div className="grid">
                            {artists.length > 0 ? artists.slice(0, 8).map(artist => {
                                const isFav = user?.favoriteArtists.some(a => a.id === artist.id);
                                return (
                                    <div key={artist.id} className="card">
                                        <img
                                            src={artist.imageUrl || `https://ui-avatars.com/api/?name=${encodeURIComponent(artist.name)}&background=141414&color=00ffcc&size=300`}
                                            alt={artist.name}
                                            className="card-img"
                                        />
                                        <div className="card-content">
                                            <div className="card-header-flex">
                                                <h3>{artist.name}</h3>
                                                {user && (
                                                    <button
                                                        className={`fav-btn ${isFav ? 'active' : ''}`}
                                                        onClick={() => toggleFavorite(artist)}
                                                        title={isFav ? 'Remove from favorites' : 'Add to favorites'}
                                                    >
                                                        {isFav ? '★' : '☆'}
                                                    </button>
                                                )}
                                            </div>
                                            <p className="genres">{artist.genres?.join(', ') || 'Electronic'}</p>
                                            <div className="links">
                                                <a href={`https://open.spotify.com/search/${encodeURIComponent(artist.name)}`} target="_blank" rel="noreferrer" className="link spotify">Spotify</a>
                                                <a href={`https://soundcloud.com/search?q=${encodeURIComponent(artist.name)}`} target="_blank" rel="noreferrer" className="link soundcloud">SoundCloud</a>
                                            </div>
                                        </div>
                                    </div>
                                );
                            }) : (
                                <div className="no-results">No artists found for "{searchQuery}"</div>
                            )}
                        </div>
                    </section>

                    {/* Events */}
                    <section>
                        <div className="section-header">
                            <h2>
                                {user?.location && !showAllCities
                                    ? `Events in ${user.location}`
                                    : 'Upcoming Events'}
                            </h2>
                            {user?.location && (
                                <button
                                    className="city-toggle-btn"
                                    onClick={() => setShowAllCities(p => !p)}
                                >
                                    {showAllCities ? `Filter to ${user.location}` : 'Show all cities'}
                                </button>
                            )}
                        </div>
                        <div className="grid">
                            {events.length > 0 ? events.map(event => (
                                <div key={event.id} className="card">
                                    <img
                                        src={event.imageUrl || 'https://images.unsplash.com/photo-1470225620780-dba8ba36b745?auto=format&fit=crop&q=80&w=600'}
                                        alt={event.name}
                                        className="card-img"
                                    />
                                    <div className="card-content">
                                        <div className="card-badge">
                                            {new Date(event.date).toLocaleDateString(undefined, { month: 'short', day: 'numeric' })}
                                        </div>
                                        {event.reason && <div className="rec-reason">{event.reason}</div>}
                                        <h3>{event.name}</h3>
                                        <p className="event-meta">
                                            {event.venue && <span>{event.venue}</span>}
                                            {event.venue && event.city && ' · '}
                                            {event.city && <span>{event.city}</span>}
                                        </p>
                                        <div className="event-tags">
                                            {event.artistNames.slice(0, 2).map(n => (
                                                <span key={n} className="tag artist-tag">{n}</span>
                                            ))}
                                            {event.genreNames.slice(0, 1).map(n => (
                                                <span key={n} className="tag genre-tag">{n}</span>
                                            ))}
                                        </div>
                                        <a href={event.ticketUrl || '#'} className="btn" target="_blank" rel="noreferrer">
                                            Get Tickets
                                        </a>
                                    </div>
                                </div>
                            )) : (
                                <div className="no-results">
                                    No events found{user?.location && !showAllCities ? ` in ${user.location}` : ''}.
                                    {user?.location && !showAllCities && (
                                        <><br /><button className="city-toggle-btn" style={{ marginTop: 12 }} onClick={() => setShowAllCities(true)}>Show all cities</button></>
                                    )}
                                </div>
                            )}
                        </div>
                    </section>
                </>
            )}
        </div>
    );
};

export default Dashboard;
