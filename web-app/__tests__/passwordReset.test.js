import { describe, it, expect } from 'vitest';
import { createTestDb, createTestUser } from './test-helpers.js';
import {
  hashToken,
  createResetToken,
  consumeResetToken,
} from '../src/lib/passwordReset.js';
import { createSession, destroySessionsForUser, requireAuth } from '../src/middleware/auth.js';

// Deterministic clock — never read the real clock (coding-standards: no time-dependent
// assertions). Default TTL is 3_600_000 ms (1 hour).
const T0 = 1_750_000_000_000;
const TTL = 3_600_000;

describe('passwordReset tokens', () => {
  it('hashes a token deterministically and never returns the raw value', () => {
    const h1 = hashToken('abc');
    const h2 = hashToken('abc');
    expect(h1).toBe(h2);
    expect(h1).not.toBe('abc');
    expect(h1).toMatch(/^[0-9a-f]{64}$/); // sha256 hex
  });

  it('consumes a valid token exactly once and returns the userId', () => {
    const db = createTestDb();
    const { userId } = createTestUser(db);
    const raw = createResetToken(db, userId, T0);

    expect(consumeResetToken(db, raw, T0 + 1000)).toEqual({ valid: true, userId });
  });

  it('rejects a second consume of the same token as already used', () => {
    const db = createTestDb();
    const { userId } = createTestUser(db);
    const raw = createResetToken(db, userId, T0);

    expect(consumeResetToken(db, raw, T0 + 1000).valid).toBe(true);
    expect(consumeResetToken(db, raw, T0 + 2000)).toEqual({
      valid: false,
      error: 'token already used',
    });
  });

  it('rejects an expired token', () => {
    const db = createTestDb();
    const { userId } = createTestUser(db);
    const raw = createResetToken(db, userId, T0);

    // now == expires_at is expired (expires_at <= now); one ms past is definitely expired.
    expect(consumeResetToken(db, raw, T0 + TTL + 1)).toEqual({
      valid: false,
      error: 'token expired',
    });
  });

  it('accepts a token one ms before expiry', () => {
    const db = createTestDb();
    const { userId } = createTestUser(db);
    const raw = createResetToken(db, userId, T0);

    expect(consumeResetToken(db, raw, T0 + TTL - 1).valid).toBe(true);
  });

  it('rejects an unknown / garbage token', () => {
    const db = createTestDb();
    createTestUser(db);
    expect(consumeResetToken(db, 'not-a-real-token', T0)).toEqual({
      valid: false,
      error: 'invalid token',
    });
    // A very long string is treated as invalid, not a crash.
    expect(consumeResetToken(db, 'x'.repeat(10000), T0).valid).toBe(false);
  });

  it('rejects empty / missing / non-string tokens', () => {
    const db = createTestDb();
    for (const bad of ['', undefined, null, 42, {}]) {
      expect(consumeResetToken(db, bad, T0)).toEqual({ valid: false, error: 'missing token' });
    }
  });

  it('invalidates the previous token when a newer one is created (only newest works)', () => {
    const db = createTestDb();
    const { userId } = createTestUser(db);
    const first = createResetToken(db, userId, T0);
    const second = createResetToken(db, userId, T0 + 1000);

    expect(consumeResetToken(db, first, T0 + 2000)).toEqual({
      valid: false,
      error: 'token already used',
    });
    expect(consumeResetToken(db, second, T0 + 2000)).toEqual({ valid: true, userId });
  });
});

describe('destroySessionsForUser', () => {
  // Helper: does requireAuth accept this bearer token? (session still present)
  function sessionIsValid(token) {
    let ok = false;
    const req = { headers: { authorization: `Bearer ${token}` } };
    const res = { status: () => ({ json: () => {} }) };
    requireAuth(req, res, () => { ok = true; });
    return ok;
  }

  it("removes a user's active session after a reset", () => {
    const userId = 987654321; // unique to avoid collision with other suites' sessions
    const token = createSession(userId, 'purge@example.com');
    expect(sessionIsValid(token)).toBe(true);

    destroySessionsForUser(userId);
    expect(sessionIsValid(token)).toBe(false);
  });

  it('leaves other users\' sessions intact', () => {
    const keep = createSession(111222333, 'keep@example.com');
    const drop = createSession(444555666, 'drop@example.com');

    destroySessionsForUser(444555666);
    expect(sessionIsValid(keep)).toBe(true);
    expect(sessionIsValid(drop)).toBe(false);
  });
});
