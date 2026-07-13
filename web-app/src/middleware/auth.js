import { randomBytes } from 'crypto';

// In-memory session store (simple for MVP; survives within process lifetime)
const sessions = new Map(); // token -> { userId, email }

export function createSession(userId, email) {
  const token = randomBytes(32).toString('hex');
  sessions.set(token, { userId, email });
  return token;
}

export function destroySession(token) {
  sessions.delete(token);
}

export function requireAuth(req, res, next) {
  const header = req.headers.authorization;
  if (!header || !header.startsWith('Bearer ')) {
    return res.status(401).json({ success: false, error: 'Authentication required' });
  }
  const token = header.slice(7);
  const session = sessions.get(token);
  if (!session) {
    return res.status(401).json({ success: false, error: 'Invalid or expired session' });
  }
  req.user = session;
  next();
}
