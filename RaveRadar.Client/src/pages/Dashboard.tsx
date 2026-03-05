import { useEffect, useState } from 'react';
import { getArtists, getEvents } from '../services/api';
import { useAuth } from '../services/AuthContext';
import type { Artist, Event } from '../services/models';

const Dashboard = () => {
  const [artists, setArtists] = useState<Artist[]>([]);
  const [events, setEvents] = useState<Event[]>([]);
  const { user, loading: authLoading } = useAuth();
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const fetchData = async () => {
      if (authLoading) return;
      try {
        setLoading(true);
        const fetchedArtists = await getArtists();
        setArtists(fetchedArtists);
        
        // Use user?.id if available for personalized events
        const fetchedEvents = await getEvents(user?.location, user?.id);
        setEvents(fetchedEvents);
      } catch (error) {
        console.error('Failed to fetch data:', error);
      } finally {
        setLoading(false);
      }
    };

    fetchData();
  }, [user, authLoading]);

  return (
    <div className="dashboard">
      <header className="dashboard-header">
        <div className="welcome-section">
          <h1>{user ? `Welcome back, ${user.email.split('@')[0]}!` : 'Discover your next rave'}</h1>
          <p>{user?.location ? `Showing events in ${user.location}` : 'Personalize your experience to see recommended events.'}</p>
        </div>
        {user && user.favoriteArtists.length > 0 && (
            <div className="user-preferences-summary">
                <span>FAVORITES: </span>
                {user.favoriteArtists.map(a => <span key={a.id} className="tag artist-tag">{a.name}</span>)}
            </div>
        )}
      </header>

      {loading ? (
        <div className="loading">Loading the radar...</div>
      ) : (
        <>
          <section>
            <h2>{user ? 'Recommended Artists' : 'Featured Artists'}</h2>
            <div className="grid">
              {artists.slice(0, 4).map(artist => (
                <div key={artist.id} className="card">
                  <img src={artist.imageUrl || 'https://via.placeholder.com/300x300?text=Artist'} alt={artist.name} className="card-img" />
                  <div className="card-content">
                    <h3>{artist.name}</h3>
                    <p className="genres">{artist.genres?.join(', ') || 'No genres listed'}</p>
                    <div className="links">
                      <a href={`https://open.spotify.com/search/${encodeURIComponent(artist.name)}`} target="_blank" rel="noreferrer" className="link spotify">Spotify</a>
                      <a href={`https://soundcloud.com/search?q=${encodeURIComponent(artist.name)}`} target="_blank" rel="noreferrer" className="link soundcloud">SoundCloud</a>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          </section>

          <section>
            <h2>{user ? 'Recommended Events' : 'Upcoming Events'}</h2>
            <div className="grid">
              {events.map(event => (
                <div key={event.id} className="card">
                  <img src={event.imageUrl || 'https://via.placeholder.com/300x200?text=Event'} alt={event.name} className="card-img" />
                  <div className="card-content">
                    <div className="card-badge">{new Date(event.date).toLocaleDateString(undefined, { month: 'short', day: 'numeric' })}</div>
                    <h3>{event.name}</h3>
                    <p className="event-meta">
                        <span className="venue">{event.venue}</span> • <span className="city">{event.city}</span>
                    </p>
                    <div className="event-tags">
                        {event.artistNames.slice(0, 2).map(name => <span key={name} className="tag artist-tag">{name}</span>)}
                        {event.genreNames.slice(0, 2).map(name => <span key={name} className="tag genre-tag">{name}</span>)}
                    </div>
                    <a href={event.ticketUrl || '#'} className="btn" target="_blank" rel="noreferrer">Get Tickets</a>
                  </div>
                </div>
              ))}
            </div>
          </section>
        </>
      )}
    </div>
  );
};

export default Dashboard;
