import { Router, urlencoded } from 'express';
import { requireAuth } from '../middleware/auth.js';
import { mintHostToken, verifyHostToken, mintGameAccess, verifyGameAccess } from '../hostToken.js';
import { getDb } from '../db.js';
import { GAME_HTTP_URL, normalizeRoomCode } from '../config.js';
import { DEFAULT_INTERNAL_SECRET } from '../hostToken.js';

const router = Router();

// Shared secret proving this proxy call originates from the trusted web-app backend, not an
// arbitrary caller on the game server's network. The game server constant-time compares it before
// disclosing a survey→room mapping (Server/server.js GET /api/survey-room). Same resolution rule
// as everywhere else: unset falls back to the public default (fine while no strong secret is set).
const INTERNAL_SECRET = process.env.INTERNAL_SECRET || DEFAULT_INTERNAL_SECRET;

// The gated Unity build lives here (nginx serves /usr/share/nginx/html/game/ behind an
// auth_request to /gate). /enter redirects into it with role/token/room in the hash.
const GAME_PATH = '/game/';
const ACCESS_COOKIE = 'game_access';

// Read a single cookie value from a raw Cookie header without pulling in cookie-parser.
function readCookie(header, name) {
  if (typeof header !== 'string' || header.length === 0) return '';
  for (const part of header.split(';')) {
    const eq = part.indexOf('=');
    if (eq <= 0) continue;
    if (part.slice(0, eq).trim() === name) return part.slice(eq + 1).trim();
  }
  return '';
}

// Set the HttpOnly game-access cookie, scoped to /game/ so it only rides requests nginx
// actually gates. Secure is added behind TLS (X-Forwarded-Proto from the edge) so local
// HTTP smoke tests still work. SameSite=Lax: the /enter -> /game/ hop is a top-level nav.
function setAccessCookie(req, res, value, expiresAt) {
  const maxAge = Math.max(0, Math.floor((expiresAt - Date.now()) / 1000));
  const https = req.secure || req.headers['x-forwarded-proto'] === 'https';
  const attrs = [
    `${ACCESS_COOKIE}=${value}`,
    `Path=${GAME_PATH}`,
    `Max-Age=${maxAge}`,
    'HttpOnly',
    'SameSite=Lax',
  ];
  if (https) attrs.push('Secure');
  res.setHeader('Set-Cookie', attrs.join('; '));
}

// POST /api/game/host-token — issue a short-lived host credential to an
// authenticated professor. Consumed by the Dashboard "Host Game" launch (Phase 2)
// and verified by the WS relay on create_room.
router.post('/host-token', requireAuth, (req, res) => {
  // Coerce surveyId to a positive integer or null. The token's `sid` claim must never carry an
  // arbitrary/oversized value — mintHostToken's contract is {number|null} (see hostToken.js).
  const raw = req.body?.surveyId;
  let surveyId = null;
  if (raw !== undefined && raw !== null) {
    const n = Number(raw);
    if (!Number.isInteger(n) || n <= 0) {
      return res.status(400).json({ success: false, error: 'surveyId must be a positive integer' });
    }
    surveyId = n;
  }
  // Ownership: a professor may only mint a host token for their own survey. Prevents an
  // authenticated user from embedding another professor's surveyId in the signed `sid` claim.
  if (surveyId !== null) {
    const owned = getDb()
      .prepare('SELECT id FROM surveys WHERE id = ? AND user_id = ?')
      .get(surveyId, req.user.userId);
    if (!owned) {
      return res.status(404).json({ success: false, error: 'Survey not found' });
    }
  }
  const { token, expiresAt } = mintHostToken(surveyId);
  res.json({ success: true, data: { token, expiresAt } });
});

// GET /api/game/room-status/:code — proxy room status from WS server
router.get('/room-status/:code', async (req, res) => {
  const code = req.params.code.toUpperCase();
  try {
    const response = await fetch(`${GAME_HTTP_URL}/api/room-status/${code}`);
    const data = await response.json();
    res.json({ success: true, data });
  } catch {
    res.json({ success: true, data: { exists: false, error: 'Game server unreachable' } });
  }
});

// GET /api/game/room-results/:code — proxy race results from WS server
router.get('/room-results/:code', async (req, res) => {
  const code = req.params.code.toUpperCase();
  try {
    const response = await fetch(`${GAME_HTTP_URL}/api/room-results/${code}`);
    const data = await response.json();
    res.json({ success: true, data });
  } catch {
    res.json({ success: true, data: { exists: false, error: 'Game server unreachable' } });
  }
});

// GET /api/game/survey-room/:surveyId — resolve the live room a professor's survey is currently
// hosting. The web-app polls this right after "Host Game" to discover the room code (the WS
// server owns code generation; the prebuilt Unity client never reports it back), then renders the
// student join link + QR. Auth + ownership: a professor may only look up their own survey's room.
router.get('/survey-room/:surveyId', requireAuth, async (req, res) => {
  const n = Number(req.params.surveyId);
  if (!Number.isInteger(n) || n <= 0) {
    return res.status(400).json({ success: false, error: 'surveyId must be a positive integer' });
  }
  const owned = getDb()
    .prepare('SELECT id FROM surveys WHERE id = ? AND user_id = ?')
    .get(n, req.user.userId);
  if (!owned) {
    return res.status(404).json({ success: false, error: 'Survey not found' });
  }
  try {
    const response = await fetch(`${GAME_HTTP_URL}/api/survey-room/${n}`, {
      headers: { 'x-internal-secret': INTERNAL_SECRET },
    });
    const data = await response.json();
    res.json({ success: true, data });
  } catch {
    res.json({ success: true, data: { exists: false, error: 'Game server unreachable' } });
  }
});

// The access gateway for the gated Unity build. It proves the caller may load the build, mints
// the HttpOnly game-access cookie nginx checks, then 302-redirects into /game/ with
// role/token/room in the URL *hash* (never sent to any server; Unity reads it). Two roles, two
// methods:
//   POST /enter (role=host) — the host token is a create_room credential, so it travels in the
//     POST body, NOT a query string: a GET query would be captured by nginx/edge access logs and
//     browser history. The token still rides the final /game/#hash so Unity can create the room.
//   GET /enter (role=play) — audience/students. Carries only a room code (not a secret) and
//     requires a *live* room (checked against the WS server), so GET is fine here.

// POST /api/game/enter — host launch. Token in the body (see above). urlencoded so a plain
// <form method="POST"> submit (from the web-app client) parses without a JSON content-type.
router.post('/enter', urlencoded({ extended: false }), (req, res) => {
  const token = typeof req.body?.token === 'string' ? req.body.token : '';
  const result = verifyHostToken(token);
  if (!result.valid) {
    return res.status(403).send('Invalid or expired host token');
  }
  const surveyId = result.surveyId ?? null;
  const { token: cookie, expiresAt } = mintGameAccess({ role: 'host', surveyId });
  setAccessCookie(req, res, cookie, expiresAt);
  const params = new URLSearchParams({ role: 'host', token });
  if (surveyId !== null) params.set('survey', String(surveyId));
  return res.redirect(302, `${GAME_PATH}#${params.toString()}`);
});

// GET /api/game/enter — spectator/student launch. role=host is rejected here: the host token
// must never ride a GET query string (405 steers the client to POST).
router.get('/enter', async (req, res) => {
  if (req.query.role === 'host') {
    return res.status(405).send('host launch must use POST');
  }
  // Only admit spectators to a room that actually exists on the WS server, so a guessed/stale
  // code cannot mint an access cookie.
  const norm = normalizeRoomCode(typeof req.query.room === 'string' ? req.query.room : '');
  if (!norm.ok) {
    return res.status(400).send('room is required');
  }
  const code = norm.code;
  let exists = false;
  try {
    const response = await fetch(`${GAME_HTTP_URL}/api/room-status/${code}`);
    const data = await response.json();
    exists = !!data?.exists;
  } catch {
    return res.status(503).send('Game server unreachable');
  }
  if (!exists) {
    return res.status(404).send('Room not found');
  }
  const { token: cookie, expiresAt } = mintGameAccess({ role: 'play', room: code });
  setAccessCookie(req, res, cookie, expiresAt);
  const params = new URLSearchParams({ role: 'play', room: code });
  return res.redirect(302, `${GAME_PATH}#${params.toString()}`);
});

// GET /api/game/gate — internal endpoint driven by nginx's `auth_request` on /game/. Returns
// 204 when the game-access cookie is valid, 401 otherwise (nginx then bounces the client to
// the survey app root). Reads only the cookie; never mints one.
router.get('/gate', (req, res) => {
  const cookie = readCookie(req.headers.cookie, ACCESS_COOKIE);
  const result = verifyGameAccess(cookie);
  if (!result.valid) {
    return res.status(401).end();
  }
  return res.status(204).end();
});

export default router;
