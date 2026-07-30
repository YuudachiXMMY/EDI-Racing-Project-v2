import { describe, it, expect } from 'vitest';
import { normalizeRoomCode, generateShareCode, WS_GAME_URL, GAME_HTTP_URL } from '../src/config.js';

describe('normalizeRoomCode', () => {
  it('trims and upper-cases a valid code', () => {
    expect(normalizeRoomCode(' ab ')).toEqual({ ok: true, code: 'AB' });
  });

  it('upper-cases mixed-case codes', () => {
    expect(normalizeRoomCode('a1b2c3')).toEqual({ ok: true, code: 'A1B2C3' });
  });

  it('rejects an empty string with the standard envelope', () => {
    expect(normalizeRoomCode('')).toEqual({ ok: false, error: 'roomCode is required' });
  });

  it('rejects null', () => {
    expect(normalizeRoomCode(null)).toEqual({ ok: false, error: 'roomCode is required' });
  });

  it('rejects undefined', () => {
    expect(normalizeRoomCode(undefined)).toEqual({ ok: false, error: 'roomCode is required' });
  });

  it('rejects a whitespace-only string', () => {
    expect(normalizeRoomCode('   ')).toEqual({ ok: false, error: 'roomCode is required' });
  });
});

describe('generateShareCode', () => {
  it('returns an 8-character code', () => {
    expect(generateShareCode()).toHaveLength(8);
  });

  it('returns uppercase hex characters only', () => {
    expect(generateShareCode()).toMatch(/^[0-9A-F]{8}$/);
  });

  it('returns different codes on repeat calls (random)', () => {
    expect(generateShareCode()).not.toBe(generateShareCode());
  });
});

describe('game URL config', () => {
  it('defaults WS_GAME_URL to the local relay', () => {
    expect(WS_GAME_URL).toBe('ws://localhost:8080');
  });

  it('derives GAME_HTTP_URL from WS_GAME_URL (ws -> http)', () => {
    expect(GAME_HTTP_URL).toBe('http://localhost:8080');
  });
});
