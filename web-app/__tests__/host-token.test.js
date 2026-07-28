import { describe, it, expect } from 'vitest';
import { createHmac } from 'crypto';
import { mintHostToken, verifyHostToken } from '../src/hostToken.js';

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
