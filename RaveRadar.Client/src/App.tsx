import { BrowserRouter as Router, Route, Routes, Link } from 'react-router-dom';
import Dashboard from './pages/Dashboard';
import LoginPage from './pages/LoginPage';
import RegisterPage from './pages/RegisterPage';
import PreferencesPage from './pages/PreferencesPage';
import { AuthProvider, useAuth } from './services/AuthContext';
import './App.css';

const NavLinks = () => {
    const { user, logout } = useAuth();
    return (
        <div className="nav-links">
            <Link to="/">Dashboard</Link>
            {user ? (
                <>
                    <span className="user-email">{user.email}</span>
                    <button onClick={logout} className="logout-btn">Logout</button>
                </>
            ) : (
                <>
                    <Link to="/login">Login</Link>
                    <Link to="/register">Register</Link>
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
            </Routes>
          </div>
        </div>
      </Router>
    </AuthProvider>
  );
}

export default App;
