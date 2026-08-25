import { Router } from 'express';
import bcrypt from 'bcryptjs';
import { getDb } from '../db.js';
import { createSession, destroySession, destroySessionsForUser } from '../middleware/auth.js';
import { createResetToken, consumeResetToken } from '../lib/passwordReset.js';
import { sendPasswordResetEmail } from '../lib/mailer.js';
import { APP_BASE_URL, mailConfigured } from '../config.js';

const router = Router();

// POST /api/auth/register
router.post('/register', (req, res) => {
  const { email, password, displayName } = req.body;

  if (!email || !password) {
    return res.status(400).json({ success: false, error: 'Email and password required' });
  }
  if (password.length < 6) {
    return res.status(400).json({ success: false, error: 'Password must be at least 6 characters' });
  }

  const db = getDb();
  const existing = db.prepare('SELECT id FROM users WHERE email = ?').get(email);
  if (existing) {
    return res.status(409).json({ success: false, error: 'Email already registered' });
  }

  const hash = bcrypt.hashSync(password, 10);
  const result = db.prepare(
    'INSERT INTO users (email, password_hash, display_name) VALUES (?, ?, ?)'
  ).run(email, hash, displayName || '');

  const token = createSession(result.lastInsertRowid, email);
  res.status(201).json({
    success: true,
    data: { token, user: { id: result.lastInsertRowid, email, displayName: displayName || '' } }
  });
});

// POST /api/auth/login
router.post('/login', (req, res) => {
  const { email, password } = req.body;
  if (!email || !password) {
    return res.status(400).json({ success: false, error: 'Email and password required' });
  }

  const db = getDb();
  const user = db.prepare('SELECT * FROM users WHERE email = ?').get(email);
  if (!user || !bcrypt.compareSync(password, user.password_hash)) {
    return res.status(401).json({ success: false, error: 'Invalid credentials' });
  }

  const token = createSession(user.id, user.email);
  res.json({
    success: true,
    data: { token, user: { id: user.id, email: user.email, displayName: user.display_name } }
  });
});

// POST /api/auth/logout
router.post('/logout', (req, res) => {
  const header = req.headers.authorization;
  if (header && header.startsWith('Bearer ')) {
    destroySession(header.slice(7));
  }
  res.json({ success: true });
});

// Per-email cooldown to blunt reset-email bombing. In-memory and process-scoped, matching
// the session store; not a substitute for edge rate limiting, just a cheap first line.
const RESET_COOLDOWN_MS = parseInt(process.env.RESET_COOLDOWN_MS || '60000', 10); // 1 min
const lastResetRequest = new Map(); // email -> epoch ms

// POST /api/auth/forgot-password  — always generic success (no user enumeration).
// Responds BEFORE doing any per-user work so registered and unregistered emails are
// indistinguishable by response timing, not just by body. All work is best-effort and
// its errors are swallowed (the client has already received the generic response).
router.post('/forgot-password', (req, res) => {
  const { email } = req.body;
  const generic = { success: true, data: { message: 'If that email is registered, a reset link has been sent.' } };
  if (!email) return res.status(400).json({ success: false, error: 'Email required' });

  res.json(generic);

  try {
    const now = Date.now();
    const last = lastResetRequest.get(email);
    if (last && now - last < RESET_COOLDOWN_MS) return; // within cooldown — skip silently
    lastResetRequest.set(email, now);

    const db = getDb();
    const user = db.prepare('SELECT id, email FROM users WHERE email = ?').get(email);
    if (!user) return;
    if (!mailConfigured()) {
      console.warn('[Auth] forgot-password requested but mail is not configured — no email sent.');
      return;
    }
    const raw = createResetToken(db, user.id);
    const resetUrl = `${APP_BASE_URL}/#/reset-password?token=${raw}`;
    sendPasswordResetEmail(user.email, resetUrl).catch((err) => {
      console.error('[Auth] Failed to send reset email:', err.message); // swallow — already responded
    });
  } catch (err) {
    console.error('[Auth] forgot-password processing error:', err.message); // already responded generically
  }
});

// POST /api/auth/reset-password  — { token, password }
router.post('/reset-password', (req, res) => {
  const { token, password } = req.body;
  if (!token || !password) {
    return res.status(400).json({ success: false, error: 'Token and password required' });
  }
  if (password.length < 6) {
    return res.status(400).json({ success: false, error: 'Password must be at least 6 characters' });
  }
  const db = getDb();
  const result = consumeResetToken(db, token);
  if (!result.valid) {
    return res.status(400).json({ success: false, error: 'Invalid or expired reset link' });
  }
  const hash = bcrypt.hashSync(password, 10);
  const info = db.prepare('UPDATE users SET password_hash = ? WHERE id = ?').run(hash, result.userId);
  if (info.changes === 0) {
    // The account was removed after the token was minted (token is now consumed/dead anyway).
    return res.status(400).json({ success: false, error: 'Invalid or expired reset link' });
  }
  destroySessionsForUser(result.userId); // force re-login everywhere
  res.json({ success: true, data: { message: 'Password updated. You can now log in.' } });
});

export default router;
