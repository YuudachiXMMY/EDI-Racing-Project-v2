import { Router } from 'express';
import { requireAuth } from '../middleware/auth.js';
import { mintHostToken } from '../hostToken.js';

const WS_GAME_URL = process.env.WS_GAME_URL || 'ws://localhost:8080';
const GAME_HTTP_URL = WS_GAME_URL.replace(/^ws/, 'http');

const router = Router();

// POST /api/game/host-token — issue a short-lived host credential to an
// authenticated professor. Consumed by the Dashboard "Host Game" launch (Phase 2)
// and verified by the WS relay on create_room.
router.post('/host-token', requireAuth, (req, res) => {
  const surveyId = req.body?.surveyId ?? null;
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
