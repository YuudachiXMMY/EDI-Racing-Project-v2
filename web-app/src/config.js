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

// Stalwart SMTP settings for password-recovery email (see infrastructure/mail/README.md).
// The password is env-configured; on the deploy server it arrives via apps/ediracing/.env.extra.
export const mailConfig = {
  // Empty default (not 'stalwart') so mailConfigured() is false until SMTP_HOST is set
  // explicitly. In production the compose file injects SMTP_HOST=stalwart; locally, an
  // unset host means the boot guard correctly warns that reset emails are disabled.
  host: process.env.SMTP_HOST || '',
  port: parseInt(process.env.SMTP_PORT || '587', 10),
  secure: (process.env.SMTP_SECURE || 'false').toLowerCase() === 'true', // true=465, false=587/STARTTLS
  user: process.env.SMTP_USER || '',
  pass: process.env.SMTP_PASS || '',
  from: process.env.MAIL_FROM || 'noreply@localhost',
};

// Public origin used to build reset links. The SPA is served at the site root with
// HashRouter, so links are `${APP_BASE_URL}/#/reset-password?token=...`. No trailing slash.
export const APP_BASE_URL = (process.env.APP_BASE_URL || 'http://localhost:3001').replace(/\/$/, '');

/**
 * Whether outgoing mail is usable: a host and from-address are required, and if a
 * username is set (authenticated submission) a password must accompany it.
 * @returns {boolean}
 */
export function mailConfigured() {
  return Boolean(mailConfig.host && mailConfig.from && (!mailConfig.user || mailConfig.pass));
}
