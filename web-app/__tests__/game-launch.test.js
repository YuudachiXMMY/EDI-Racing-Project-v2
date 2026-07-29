import { describe, it, expect } from 'vitest';
import { buildHostLaunchUrl, buildStudentPlayUrl, buildSpectatorPath } from '../client/src/gameLaunch.js';

// VITE_GAME_URL is unset under vitest, so GAME_ROOT falls back to '/'. Assertions below
// assume that default (single-origin deploy: game at /, survey app at /survey/).
describe('buildHostLaunchUrl', () => {
  it('puts role, token, and survey in the hash fragment at the game root', () => {
    expect(buildHostLaunchUrl('tok', 5)).toBe('/#role=host&token=tok&survey=5');
  });

  it('coerces a numeric surveyId to a string', () => {
    expect(buildHostLaunchUrl('t', 42)).toContain('survey=42');
  });

  it('URL-encodes special characters in the token', () => {
    // A space encodes as '+' via URLSearchParams; '/' and '=' are percent/encoded safely.
    const url = buildHostLaunchUrl('a b/c', 1);
    expect(url).toContain('token=a+b%2Fc');
    expect(url.startsWith('/#role=host')).toBe(true);
  });

  it('always begins with the hash fragment (token never in the query string)', () => {
    const url = buildHostLaunchUrl('secret', 9);
    expect(url).not.toContain('?');
    expect(url).toContain('#');
  });
});

describe('buildStudentPlayUrl', () => {
  it('puts room and role=play in the hash at the game root, with no token', () => {
    expect(buildStudentPlayUrl('A1B2C3')).toBe('/#room=A1B2C3&role=play');
  });

  it('never begins a query string (room code stays out of server logs)', () => {
    const url = buildStudentPlayUrl('XYZ');
    expect(url).not.toContain('?');
    expect(url).toContain('#room=XYZ');
  });

  it('carries no host token key', () => {
    expect(buildStudentPlayUrl('R1')).not.toContain('token');
  });
});

describe('buildSpectatorPath', () => {
  it('builds the in-app 2D spectator route for the room', () => {
    expect(buildSpectatorPath('A1B2C3')).toBe('/live/A1B2C3');
  });

  it('upper-cases the room code so URL, display, and WS all agree', () => {
    expect(buildSpectatorPath('abc123')).toBe('/live/ABC123');
  });

  it('is a router path, not a game-root hash URL, and carries no token', () => {
    const p = buildSpectatorPath('R1');
    expect(p.startsWith('/live/')).toBe(true);
    expect(p).not.toContain('#');
    expect(p).not.toContain('token');
  });
});
