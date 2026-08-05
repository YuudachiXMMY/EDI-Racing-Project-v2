import { createHmac, timingSafeEqual } from 'crypto';

// Host token: a short-lived, HMAC-signed credential that authorizes a client to
// create a game room (become the professor/host). Minted by the authenticated
// web-app, verified locally by the WebSocket relay (Server/server.js) using the
// same shared secret — no cross-service round-trip.
//
// IMPORTANT: The wire format below — AND the checkSecretConfig boot guard — are
// mirrored byte-for-byte in Server/server.js. If you change the payload shape,
// base64url encoding, the signed bytes, or the guard's decision logic here, update
// Server/server.js in lockstep or tokens will silently fail to verify across
// processes (or the two services will disagree on whether it is safe to boot).
//
//   token      = payloadB64 + "." + sigB64
//   payloadB64 = base64url( utf8( JSON.stringify(payload) ) )   // no padding
//   payload    = { v:1, sid:<surveyId|null>, iat:<epoch ms>, exp:<epoch ms> }
//   sigB64     = base64url( HMAC_SHA256(key=INTERNAL_SECRET, msg=payloadB64) )

// The committed fallback secret. Safe only while REQUIRE_HOST_TOKEN is off — once
// the token becomes an auth boundary, anyone who knows this public value could mint
// a valid token, so checkSecretConfig refuses to boot with it under enforcement.
export const DEFAULT_INTERNAL_SECRET = 'edi-internal-default';

const INTERNAL_SECRET = process.env.INTERNAL_SECRET || DEFAULT_INTERNAL_SECRET;
const TTL_MS = parseInt(process.env.HOST_TOKEN_TTL_MS || '300000', 10); // 5 min

/**
 * Decide whether the current secret configuration is safe to boot with. Pure —
 * never reads process.env or exits; the entry point acts on the returned level.
 * Mirrored in Server/server.js — keep the decision logic in lockstep.
 * @param {{ secret: string|undefined, requireHostToken: boolean }} cfg
 * @returns {{ level: 'ok'|'warn'|'fatal', message: string }}
 */
export function checkSecretConfig({ secret, requireHostToken }) {
  const isDefault = !secret || secret === DEFAULT_INTERNAL_SECRET;
  if (!isDefault) return { level: 'ok', message: '' };
  if (requireHostToken) {
    return {
      level: 'fatal',
      message:
        'REQUIRE_HOST_TOKEN=true but INTERNAL_SECRET is unset or the public default. ' +
        'Set a strong random INTERNAL_SECRET (e.g. `openssl rand -hex 32`) before enabling ' +
        'host-token enforcement. Refusing to start.',
    };
  }
  return {
    level: 'warn',
    message:
      "INTERNAL_SECRET is the public default 'edi-internal-default'. This is acceptable only " +
      'with REQUIRE_HOST_TOKEN=false. Set a strong secret before enabling enforcement.',
  };
}

function b64url(buf) {
  return buf.toString('base64').replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

function b64urlDecode(str) {
  const pad = str.length % 4 === 0 ? '' : '='.repeat(4 - (str.length % 4));
  return Buffer.from(str.replace(/-/g, '+').replace(/_/g, '/') + pad, 'base64');
}

function sign(payloadB64) {
  return b64url(createHmac('sha256', INTERNAL_SECRET).update(payloadB64).digest());
}

/**
 * Mint a host token for the given survey.
 * @param {number|null} surveyId - survey this host session is tied to (optional).
 * @param {number} now - epoch ms; injectable for deterministic tests.
 * @returns {{ token: string, expiresAt: number }}
 */
export function mintHostToken(surveyId = null, now = Date.now()) {
  const payload = { v: 1, sid: surveyId, iat: now, exp: now + TTL_MS };
  const payloadB64 = b64url(Buffer.from(JSON.stringify(payload)));
  return { token: payloadB64 + '.' + sign(payloadB64), expiresAt: payload.exp };
}

/**
 * Verify the signature, version, and expiry of a token in the shared wire format and
 * return its decoded payload. Never throws. Used by both verifyHostToken and
 * verifyGameAccess so the two credentials share one hardened parse/verify path.
 * @param {string} token
 * @param {number} now - epoch ms.
 * @returns {{ valid: true, payload: object } | { valid: false, error: string }}
 */
function verifySignedToken(token, now) {
  if (typeof token !== 'string' || token.length === 0) {
    return { valid: false, error: 'missing token' };
  }
  const dot = token.indexOf('.');
  if (dot <= 0 || dot === token.length - 1) {
    return { valid: false, error: 'malformed token' };
  }
  const payloadB64 = token.slice(0, dot);
  const sigB64 = token.slice(dot + 1);

  const expected = sign(payloadB64);
  const a = Buffer.from(sigB64);
  const b = Buffer.from(expected);
  if (a.length !== b.length) {
    return { valid: false, error: 'bad signature' };
  }
  try {
    if (!timingSafeEqual(a, b)) {
      return { valid: false, error: 'bad signature' };
    }
  } catch {
    return { valid: false, error: 'bad signature' };
  }

  let payload;
  try {
    payload = JSON.parse(b64urlDecode(payloadB64).toString('utf8'));
  } catch {
    return { valid: false, error: 'bad payload' };
  }
  if (!payload || payload.v !== 1) {
    return { valid: false, error: 'unsupported version' };
  }
  if (typeof payload.exp !== 'number' || payload.exp <= now) {
    return { valid: false, error: 'expired' };
  }
  return { valid: true, payload };
}

/**
 * Verify a host token. Never throws.
 * @param {string} token
 * @param {number} now - epoch ms; injectable for deterministic tests.
 * @returns {{ valid: boolean, surveyId?: number|null, error?: string }}
 */
export function verifyHostToken(token, now = Date.now()) {
  const sig = verifySignedToken(token, now);
  if (!sig.valid) return sig;
  return { valid: true, surveyId: sig.payload.sid ?? null };
}

// ── Game-access cookie ─────────────────────────────────────────────────────────
// A DIFFERENT credential from the host token above. The host token authorizes creating a
// WS room (checked by Server/server.js). The game-access token authorizes *loading the
// Unity build at all*: /api/game/enter mints it once the caller proves they may enter
// (valid host token, or a live room), it rides the HttpOnly `game_access` cookie, and
// nginx's auth_request checks it via /api/game/gate before serving any /game/ asset.
// Longer-lived than the host token because it must outlast a whole class session, not just
// the create_room handshake. Same b64url+HMAC wire format, but NOT mirrored in
// Server/server.js — only the web-app mints it and (behind nginx) verifies it.
//   payload = { v:1, role:'host'|'play', room:<code|null>, sid:<surveyId|null>, iat, exp }
const ACCESS_TTL_MS = parseInt(process.env.GAME_ACCESS_TTL_MS || '14400000', 10); // 4h

/**
 * Mint a game-access token for the /game/ build gate.
 * @param {{ role: 'host'|'play', room?: string|null, surveyId?: number|null }} claims
 * @param {number} now - epoch ms; injectable for deterministic tests.
 * @returns {{ token: string, expiresAt: number }}
 */
export function mintGameAccess({ role, room = null, surveyId = null }, now = Date.now()) {
  const payload = { v: 1, role, room, sid: surveyId, iat: now, exp: now + ACCESS_TTL_MS };
  const payloadB64 = b64url(Buffer.from(JSON.stringify(payload)));
  return { token: payloadB64 + '.' + sign(payloadB64), expiresAt: payload.exp };
}

/**
 * Verify a game-access token. Never throws.
 * @param {string} token
 * @param {number} now - epoch ms; injectable for deterministic tests.
 * @returns {{ valid: boolean, role?: string, room?: string|null, surveyId?: number|null, error?: string }}
 */
export function verifyGameAccess(token, now = Date.now()) {
  const sig = verifySignedToken(token, now);
  if (!sig.valid) return sig;
  const { payload } = sig;
  if (payload.role !== 'host' && payload.role !== 'play') {
    return { valid: false, error: 'bad role' };
  }
  return { valid: true, role: payload.role, room: payload.room ?? null, surveyId: payload.sid ?? null };
}
