import { describe, it, expect } from 'vitest';
import { buildHostLaunchUrl, buildStudentPlayUrl, buildSpectatorPath } from '../client/src/gameLaunch.js';

// The Unity build is gated at /game/; launch links go through the same-origin access
// gateway /api/game/enter (which sets the game-access cookie and redirects into /game/).
describe('buildHostLaunchUrl', () => {
  it('routes through the access gateway with role, token, and survey', () => {
    expect(buildHostLaunchUrl('tok', 5)).toBe('/api/game/enter?role=host&token=tok&survey=5');
  });

  it('coerces a numeric surveyId to a string', () => {
    expect(buildHostLaunchUrl('t', 42)).toContain('survey=42');
  });

  it('URL-encodes special characters in the token', () => {
    // A space encodes as '+' via URLSearchParams; '/' and '=' are percent-encoded safely.
    const url = buildHostLaunchUrl('a b/c', 1);
    expect(url).toContain('token=a+b%2Fc');
    expect(url.startsWith('/api/game/enter?role=host')).toBe(true);
  });

  it('is a same-origin gateway path (relative, no origin/host leaked)', () => {
    const url = buildHostLaunchUrl('secret', 9);
    expect(url.startsWith('/api/game/enter?')).toBe(true);
    expect(url).not.toContain('://');
  });
});

describe('buildStudentPlayUrl', () => {
  it('routes through the gateway with role=play and room, no token', () => {
    expect(buildStudentPlayUrl('A1B2C3')).toBe('/api/game/enter?role=play&room=A1B2C3');
  });

  it('carries no host token key (audience can watch but never host)', () => {
    expect(buildStudentPlayUrl('R1')).not.toContain('token');
  });

  it('is a same-origin gateway path', () => {
    const url = buildStudentPlayUrl('XYZ');
    expect(url.startsWith('/api/game/enter?')).toBe(true);
    expect(url).toContain('room=XYZ');
    expect(url).not.toContain('://');
  });
});

describe('buildSpectatorPath', () => {
  it('builds the in-app 2D spectator route for the room', () => {
    expect(buildSpectatorPath('A1B2C3')).toBe('/live/A1B2C3');
  });

  it('upper-cases the room code so URL, display, and WS all agree', () => {
    expect(buildSpectatorPath('abc123')).toBe('/live/ABC123');
  });

  it('is a router path, not a game hash URL, and carries no token', () => {
    const p = buildSpectatorPath('R1');
    expect(p.startsWith('/live/')).toBe(true);
    expect(p).not.toContain('#');
    expect(p).not.toContain('token');
  });
});
