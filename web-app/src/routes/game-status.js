import { Router } from 'express';
import { requireAuth } from '../middleware/auth.js';
import { mintHostToken } from '../hostToken.js';
import { getDb } from '../db.js';

const WS_GAME_URL = process.env.WS_GAME_URL || 'ws://localhost:8080';
const GAME_HTTP_URL = WS_GAME_URL.replace(/^ws/, 'http');

const router = Router();

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

export default router;
