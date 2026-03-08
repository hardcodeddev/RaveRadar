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
                        <div className="logo">RaveRadar</div>
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
