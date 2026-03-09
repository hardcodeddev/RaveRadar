import { useState, useRef, useEffect } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { register as registerApi, login as loginApi, getCities } from '../services/api';
import { useAuth } from '../services/AuthContext';

const RegisterPage = () => {
    const [email, setEmail] = useState('');
    const [password, setPassword] = useState('');
    const [location, setLocation] = useState('');
    
    // City search
    const [cityResults, setCityResults] = useState<string[]>([]);
    const [showCityDropdown, setShowCityDropdown] = useState(false);
    const cityWrapRef = useRef<HTMLDivElement>(null);
    const cityDebounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);

    const [error, setError] = useState('');
    const [loading, setLoading] = useState(false);
    const { login } = useAuth();
    const navigate = useNavigate();

    useEffect(() => {
        const handler = (e: MouseEvent) => {
            if (cityWrapRef.current && !cityWrapRef.current.contains(e.target as Node)) {
                setShowCityDropdown(false);
            }
        };
        document.addEventListener('mousedown', handler);
        return () => document.removeEventListener('mousedown', handler);
    }, []);

    const handleCityChange = (value: string) => {
        setLocation(value);
        if (cityDebounceRef.current) clearTimeout(cityDebounceRef.current);
        
        if (value.trim().length < 1) { 
            setCityResults([]); 
            setShowCityDropdown(false); 
            return; 
        }

        cityDebounceRef.current = setTimeout(async () => {
            const results = await getCities(value.trim());
            setCityResults(results);
            setShowCityDropdown(results.length > 0);
        }, 200);
    };

    const selectCity = (city: string) => {
        setLocation(city);
        setShowCityDropdown(false);
    };

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        setError('');
        setLoading(true);
        try {
            await registerApi({ email, password, location: location || undefined });
            const user = await loginApi({ email, password });
            login(user);
            navigate('/preferences');
        } catch {
            setError('Registration failed. That email may already be taken.');
        } finally {
            setLoading(false);
        }
    };

    return (
        <div className="auth-page">
            <div className="auth-card">
                <h2>Create account</h2>
                {error && <p className="error-message">{error}</p>}
                <form onSubmit={handleSubmit}>
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
                                            onMouseDown={() => selectCity(city)}
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
                        {loading ? 'Creating account...' : 'Create account'}
                    </button>
                </form>
                <p>Already have an account? <Link to="/login">Login here</Link></p>
            </div>
        </div>
    );
};

export default RegisterPage;
