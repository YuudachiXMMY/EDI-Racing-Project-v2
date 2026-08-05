import { describe, it, expect } from 'vitest';
import { mintGameAccess, verifyGameAccess, mintHostToken } from '../src/hostToken.js';

// Deterministic clock — never read the real clock (coding-standards: no time-dependent
// assertions). GAME_ACCESS_TTL_MS default is 4h.
const T0 = 1_750_000_000_000;
const TTL = 14_400_000;

describe('gameAccess token', () => {
  it('round-trips a host access token (role + surveyId, room null)', () => {
    const { token, expiresAt } = mintGameAccess({ role: 'host', surveyId: 42 }, T0);
    expect(expiresAt).toBe(T0 + TTL);
    expect(verifyGameAccess(token, T0)).toEqual({
      valid: true,
      role: 'host',
      room: null,
      surveyId: 42,
    });
  });

  it('round-trips a play access token (role + room, surveyId null)', () => {
    const { token } = mintGameAccess({ role: 'play', room: 'A1B2C3' }, T0);
    expect(verifyGameAccess(token, T0)).toEqual({
      valid: true,
      role: 'play',
      room: 'A1B2C3',
      surveyId: null,
    });
  });

  it('rejects an expired token', () => {
    const { token } = mintGameAccess({ role: 'play', room: 'R1' }, T0);
    // One ms past expiry.
    expect(verifyGameAccess(token, T0 + TTL + 1)).toEqual({ valid: false, error: 'expired' });
  });

  it('rejects a tampered payload', () => {
    const { token } = mintGameAccess({ role: 'host', surveyId: 7 }, T0);
    const [payloadB64, sigB64] = token.split('.');
    const flipped = (payloadB64[0] === 'e' ? 'f' : 'e') + payloadB64.slice(1);
    expect(verifyGameAccess(`${flipped}.${sigB64}`, T0).valid).toBe(false);
  });

  it('rejects missing/malformed tokens without throwing', () => {
    expect(verifyGameAccess('', T0)).toEqual({ valid: false, error: 'missing token' });
    expect(verifyGameAccess('no-dot', T0)).toEqual({ valid: false, error: 'malformed token' });
  });

  it('does NOT accept a host token as a game-access token (no role claim)', () => {
    // A host token is a different credential: it authorizes create_room, not build access.
    // It shares the wire format but carries no `role`, so the gate must reject it.
    const { token } = mintHostToken(5, T0);
    expect(verifyGameAccess(token, T0)).toEqual({ valid: false, error: 'bad role' });
  });
});
