import { BrowserRouter as Router, Route, Routes, Link, useLocation } from 'react-router-dom';
import Dashboard from './pages/Dashboard';
import LoginPage from './pages/LoginPage';
import RegisterPage from './pages/RegisterPage';
import PreferencesPage from './pages/PreferencesPage';
import DiscoverPage from './pages/DiscoverPage';
import { AuthProvider, useAuth } from './services/AuthContext';
import './App.css';

const NavLinks = () => {
    const { user, logout } = useAuth();
    const { pathname } = useLocation();

    const isActive = (path: string) =>
        pathname === path ? { color: '#fff' } : {};

    return (
        <div className="nav-links">
            <Link to="/" style={isActive('/')}>Dashboard</Link>
            <Link to="/discover" style={isActive('/discover')}>Discover</Link>
            {user ? (
                <>
                    <Link to="/preferences" className="account-nav-link">
                        {user.location ? `📍 ${user.location}` : '⚙ Account'}
                    </Link>
                    <button onClick={logout} className="logout-btn">Logout</button>
                </>
            ) : (
                <>
                    <Link to="/login" style={isActive('/login')}>Login</Link>
                    <Link to="/register" style={isActive('/register')}>Register</Link>
                </>
            )}
        </div>
    );
};

function App() {
    return (
        <AuthProvider>
            <Router>
                <div className="app-container">
                    <nav className="navbar">
                        <Link to="/" className="logo-link">
                            <svg className="logo-svg" viewBox="0 0 512 512" fill="none" xmlns="http://www.w3.org/2000/svg">
                                <rect width="512" height="512" rx="100" fill="#0D0D12" fillOpacity="0.6" />
                                <circle className="logo-orbit-1" cx="256" cy="256" r="180" stroke="#2a1a50" strokeWidth="1.5" strokeDasharray="10 10" />
                                <circle className="logo-orbit-2" cx="256" cy="256" r="120" stroke="#2a1a50" strokeWidth="1.5" strokeDasharray="8 8" />
                                <path
                                    className="logo-arc"
                                    d="M256 60C364.248 60 452 147.752 452 256"
                                    stroke="url(#logo-grad-arc)"
                                    strokeWidth="22"
                                    strokeLinecap="round"
                                />
                                <path
                                    className="logo-wave"
                                    d="M140 256H180L200 180L240 330L280 120L320 380L350 256H380"
                                    stroke="url(#logo-grad-wave)"
                                    strokeWidth="14"
                                    strokeLinecap="round"
                                    strokeLinejoin="round"
                                    fill="none"
                                />
                                <circle className="logo-dot" cx="350" cy="256" r="9" fill="#00F2FF" />
                                <defs>
                                    <linearGradient id="logo-grad-arc" x1="452" y1="60" x2="452" y2="256" gradientUnits="userSpaceOnUse">
                                        <stop stopColor="#7c3aed" />
                                        <stop offset="1" stopColor="#00ffaa" />
                                    </linearGradient>
                                    <linearGradient id="logo-grad-wave" x1="140" y1="250" x2="380" y2="250" gradientUnits="userSpaceOnUse">
                                        <stop stopColor="#7c3aed" />
                                        <stop offset="0.5" stopColor="#00ffaa" />
                                        <stop offset="1" stopColor="#c026d3" />
                                    </linearGradient>
                                </defs>
                            </svg>
                            <span className="logo-text">RaveRadar</span>
                        </Link>
                        <NavLinks />
                    </nav>
                    <div className="content">
                        <Routes>
                            <Route path="/" element={<Dashboard />} />
                            <Route path="/login" element={<LoginPage />} />
                            <Route path="/register" element={<RegisterPage />} />
                            <Route path="/preferences" element={<PreferencesPage />} />
                            <Route path="/discover" element={<DiscoverPage />} />
                        </Routes>
                    </div>
                </div>
            </Router>
        </AuthProvider>
    );
}

export default App;
