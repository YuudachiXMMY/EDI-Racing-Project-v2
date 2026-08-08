import { describe, it, expect } from 'vitest';
import { createHmac } from 'crypto';
import {
  mintHostToken,
  verifyHostToken,
  checkSecretConfig,
  DEFAULT_INTERNAL_SECRET,
} from '../src/hostToken.js';

// Deterministic clock values — never read the real clock (coding-standards: no
// time-dependent assertions). TTL default is 300000 ms.
const T0 = 1_750_000_000_000;
const TTL = 300_000;

describe('hostToken', () => {
  it('round-trips a valid token and recovers the surveyId', () => {
    const { token, expiresAt } = mintHostToken(42, T0);
    expect(expiresAt).toBe(T0 + TTL);
    expect(verifyHostToken(token, T0)).toEqual({ valid: true, surveyId: 42 });
  });

  it('preserves a null surveyId by default', () => {
    const { token } = mintHostToken(undefined, T0);
    expect(verifyHostToken(token, T0)).toEqual({ valid: true, surveyId: null });
  });

  it('rejects a tampered payload', () => {
    const { token } = mintHostToken(7, T0);
    const [payloadB64, sigB64] = token.split('.');
    // Flip the first payload char to something else (base64url-safe).
    const flipped = (payloadB64[0] === 'A' ? 'B' : 'A') + payloadB64.slice(1);
    const bad = `${flipped}.${sigB64}`;
    expect(verifyHostToken(bad, T0).valid).toBe(false);
  });

  it('rejects a tampered signature', () => {
    const { token } = mintHostToken(7, T0);
    const bad = token.slice(0, -2) + (token.endsWith('xx') ? 'yy' : 'xx');
    expect(verifyHostToken(bad, T0).valid).toBe(false);
  });

  it('rejects an expired token', () => {
    const { token } = mintHostToken(1, T0);
    expect(verifyHostToken(token, T0 + TTL + 1)).toEqual({ valid: false, error: 'expired' });
  });

  it('accepts a token one ms before expiry', () => {
    const { token } = mintHostToken(1, T0);
    expect(verifyHostToken(token, T0 + TTL - 1).valid).toBe(true);
  });

  it('rejects malformed input without throwing', () => {
    for (const bad of ['', null, undefined, 'abc', '.', 'a.', '.b', 42, {}]) {
      expect(verifyHostToken(bad, T0).valid).toBe(false);
    }
  });

  it('rejects an unsupported payload version even when correctly signed', () => {
    // Hand-build a token whose payload is v:2 but correctly signed, to prove the
    // version check (not just the signature) gates acceptance.
    const b64url = (buf) =>
      buf.toString('base64').replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
    const secret = process.env.INTERNAL_SECRET || 'edi-internal-default';
    const payloadB64 = b64url(Buffer.from(JSON.stringify({ v: 2, sid: 1, iat: T0, exp: T0 + TTL })));
    const sig = b64url(createHmac('sha256', secret).update(payloadB64).digest());
    expect(verifyHostToken(`${payloadB64}.${sig}`, T0)).toEqual({
      valid: false,
      error: 'unsupported version',
    });
  });
});

describe('checkSecretConfig', () => {
  // Pure decision function — inputs passed explicitly, never via process.env, so the
  // matrix stays deterministic (coding-standards: no shared mutable global state).
  it('is fatal when enforcement is on and secret is the default', () => {
    expect(
      checkSecretConfig({ secret: DEFAULT_INTERNAL_SECRET, requireHostToken: true }).level
    ).toBe('fatal');
  });

  it('is fatal when enforcement is on and secret is unset', () => {
    expect(checkSecretConfig({ secret: undefined, requireHostToken: true }).level).toBe('fatal');
  });

  it('is fatal when enforcement is on and secret is an empty string', () => {
    expect(checkSecretConfig({ secret: '', requireHostToken: true }).level).toBe('fatal');
  });

  it('warns when enforcement is off but secret is still the default', () => {
    expect(
      checkSecretConfig({ secret: DEFAULT_INTERNAL_SECRET, requireHostToken: false }).level
    ).toBe('warn');
  });

  it('is fatal on the default when the game-access gate is active, even with enforcement off', () => {
    // The web-app always serves /api/game/gate, so the public default would let anyone forge a
    // game_access cookie — never merely a warning for that process.
    expect(
      checkSecretConfig({ secret: DEFAULT_INTERNAL_SECRET, requireHostToken: false, gameAccessGate: true }).level
    ).toBe('fatal');
    expect(
      checkSecretConfig({ secret: undefined, requireHostToken: false, gameAccessGate: true }).level
    ).toBe('fatal');
  });

  it('is ok with a strong secret regardless of the game-access gate', () => {
    expect(
      checkSecretConfig({ secret: 's3cr3t-random', requireHostToken: false, gameAccessGate: true }).level
    ).toBe('ok');
  });

  it('is ok when a strong secret is set, regardless of enforcement', () => {
    expect(checkSecretConfig({ secret: 's3cr3t-random', requireHostToken: true }).level).toBe('ok');
    expect(checkSecretConfig({ secret: 's3cr3t-random', requireHostToken: false }).level).toBe('ok');
  });
});
