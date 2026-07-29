import { describe, it, expect } from 'vitest';
import { buildHostLaunchUrl } from '../client/src/gameLaunch.js';

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
