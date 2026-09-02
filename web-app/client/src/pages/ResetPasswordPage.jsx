import { useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { resetPassword } from '../api.js';

export default function ResetPasswordPage() {
  const navigate = useNavigate();
  // With HashRouter, useSearchParams reads the query AFTER the '#' correctly.
  const [searchParams] = useSearchParams();
  const token = searchParams.get('token') || '';

  const [password, setPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  async function handleSubmit(e) {
    e.preventDefault();
    setError('');

    if (password.length < 6) {
      setError('Password must be at least 6 characters');
      return;
    }
    if (password !== confirm) {
      setError('Passwords do not match');
      return;
    }

    setLoading(true);
    const result = await resetPassword(token, password);
    setLoading(false);

    if (result.success) {
      navigate('/login');
    } else {
      setError(result.error || 'Something went wrong');
    }
  }

  return (
    <div className="login-page">
      <div className="login-card">
        <h1>Survey</h1>
        <p className="subtitle">Reset Your Password</p>

        {!token ? (
          <p className="error">This reset link is missing its token. Request a new link.</p>
        ) : (
          <form onSubmit={handleSubmit}>
            <input
              type="password"
              placeholder="New Password (min 6 chars)"
              value={password}
              onChange={e => setPassword(e.target.value)}
              required
              minLength={6}
            />
            <input
              type="password"
              placeholder="Confirm Password"
              value={confirm}
              onChange={e => setConfirm(e.target.value)}
              required
              minLength={6}
            />
            {error && <p className="error">{error}</p>}
            <button type="submit" disabled={loading}>
              {loading ? 'Please wait...' : 'Set New Password'}
            </button>
          </form>
        )}

        <p className="toggle-link">
          <a href="#/login">Back to login</a>
        </p>
      </div>
    </div>
  );
}
