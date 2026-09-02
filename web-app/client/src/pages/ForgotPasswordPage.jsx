import { useState } from 'react';
import { requestPasswordReset } from '../api.js';

export default function ForgotPasswordPage() {
  const [email, setEmail] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const [sent, setSent] = useState(false);

  async function handleSubmit(e) {
    e.preventDefault();
    setError('');
    setLoading(true);

    const result = await requestPasswordReset(email);

    setLoading(false);

    if (result.success) {
      // Generic message regardless of whether the email is registered (no enumeration).
      setSent(true);
    } else {
      setError(result.error || 'Something went wrong');
    }
  }

  return (
    <div className="login-page">
      <div className="login-card">
        <h1>Survey</h1>
        <p className="subtitle">Forgot Password</p>

        {sent ? (
          <p className="subtitle">
            If that email is registered, a reset link has been sent. Check your inbox.
          </p>
        ) : (
          <form onSubmit={handleSubmit}>
            <input
              type="email"
              placeholder="Email"
              value={email}
              onChange={e => setEmail(e.target.value)}
              required
            />
            {error && <p className="error">{error}</p>}
            <button type="submit" disabled={loading}>
              {loading ? 'Please wait...' : 'Send Reset Link'}
            </button>
          </form>
        )}

        <p className="toggle-link">
          Remembered your password?{' '}
          <a href="#/login">Back to login</a>
        </p>
      </div>
    </div>
  );
}
