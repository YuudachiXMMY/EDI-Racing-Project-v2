import { Router } from 'express';
import { requireAuth } from '../middleware/auth.js';
import { mintHostToken, verifyHostToken, mintGameAccess, verifyGameAccess } from '../hostToken.js';
import { getDb } from '../db.js';
import { GAME_HTTP_URL, normalizeRoomCode } from '../config.js';

const router = Router();

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

// GET /api/game/enter — the access gateway for the gated Unity build. It proves the caller
// may load the build, mints the HttpOnly game-access cookie nginx checks, then 302-redirects
// into /game/ with role/token/room in the hash (where Unity reads them). Two roles:
//   role=host — requires a valid host token (the same token Unity uses for create_room). The
//               token still travels in the final hash so Unity can create the room.
//   role=play — audience/students. Requires a *live* room (checked against the WS server);
//               carries NO host token, so it can watch but never create a room or trigger.
router.get('/enter', async (req, res) => {
  const role = req.query.role === 'host' ? 'host' : 'play';

  if (role === 'host') {
    const token = typeof req.query.token === 'string' ? req.query.token : '';
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
  }

  // role=play: only admit spectators to a room that actually exists on the WS server, so a
  // guessed/stale code cannot mint an access cookie.
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
