import React, { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { register as registerApi, login as loginApi } from '../services/api';
import { useAuth } from '../services/AuthContext';

const RegisterPage = () => {
    const [email, setEmail] = useState('');
    const [password, setPassword] = useState('');
    const [location, setLocation] = useState('');
    const [error, setError] = useState('');
    const { login } = useAuth();
    const navigate = useNavigate();

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        try {
            await registerApi({ email, password, location });
            const user = await loginApi({ email, password });
            login(user);
            navigate('/preferences');
        } catch (err) {
            setError('Registration failed. Email may be taken.');
        }
    };

    return (
        <div className="auth-page">
            <div className="auth-card">
                <h2>Create Account</h2>
                {error && <p className="error-message">{error}</p>}
                <form onSubmit={handleSubmit}>
                    <div className="form-group">
                        <label>Email</label>
                        <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} required />
                    </div>
                    <div className="form-group">
                        <label>Password</label>
                        <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} required />
                    </div>
                    <div className="form-group">
                        <label>City (Optional)</label>
                        <input type="text" value={location} onChange={(e) => setLocation(e.target.value)} placeholder="e.g. Los Angeles" />
                    </div>
                    <button type="submit" className="btn btn-primary">Register</button>
                </form>
                <p>Already have an account? <Link to="/login">Login here</Link></p>
            </div>
        </div>
    );
};

export default RegisterPage;
