import { randomBytes } from 'crypto';

// Centralized runtime configuration and shared helpers for the survey backend.
// Single source of truth for the game-server URL, room-code normalization, and
// share-code generation (previously duplicated across route modules).

// WebSocket URL of the Unity game relay server.
export const WS_GAME_URL = process.env.WS_GAME_URL || 'ws://localhost:8080';

// HTTP base derived from the WS URL (ws:// -> http://, wss:// -> https://).
export const GAME_HTTP_URL = WS_GAME_URL.replace(/^ws/, 'http');

/**
 * Validate and normalize a room code.
 * @param {string} roomCode
 * @returns {{ ok: true, code: string } | { ok: false, error: string }}
 */
export function normalizeRoomCode(roomCode) {
  if (!roomCode || !roomCode.trim()) return { ok: false, error: 'roomCode is required' };
  return { ok: true, code: roomCode.trim().toUpperCase() };
}

/**
 * Generate an 8-character uppercase hex share code.
 * Uppercase is significant: student lookups use `WHERE share_code = ? COLLATE NOCASE`.
 * @returns {string}
 */
export function generateShareCode() {
  return randomBytes(4).toString('hex').toUpperCase(); // 8-char code
}
