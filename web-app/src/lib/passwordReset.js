import { randomBytes, createHash } from 'crypto';

const RESET_TTL_MS = parseInt(process.env.RESET_TOKEN_TTL_MS || '3600000', 10); // 1 hour

export function hashToken(rawToken) {
  return createHash('sha256').update(rawToken).digest('hex');
}

/** Create a single-use reset record for a user. Returns the RAW token (emailed, never stored). */
export function createResetToken(db, userId, now = Date.now()) {
  const raw = randomBytes(32).toString('hex');
  // Invalidate any outstanding tokens for this user (only the newest link should work).
  db.prepare('UPDATE password_resets SET used_at = ? WHERE user_id = ? AND used_at IS NULL')
    .run(now, userId);
  db.prepare('INSERT INTO password_resets (user_id, token_hash, expires_at) VALUES (?, ?, ?)')
    .run(userId, hashToken(raw), now + RESET_TTL_MS);
  return raw;
}

/** Verify + atomically consume a token. Returns { valid, userId? , error? }. Never throws. */
export function consumeResetToken(db, rawToken, now = Date.now()) {
  if (typeof rawToken !== 'string' || rawToken.length === 0) {
    return { valid: false, error: 'missing token' };
  }
  const row = db.prepare('SELECT * FROM password_resets WHERE token_hash = ?').get(hashToken(rawToken));
  if (!row) return { valid: false, error: 'invalid token' };
  if (row.used_at !== null) return { valid: false, error: 'token already used' };
  if (row.expires_at <= now) return { valid: false, error: 'token expired' };
  db.prepare('UPDATE password_resets SET used_at = ? WHERE id = ?').run(now, row.id);
  return { valid: true, userId: row.user_id };
}
