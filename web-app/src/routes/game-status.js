import { Router } from 'express';

const WS_GAME_URL = process.env.WS_GAME_URL || 'ws://localhost:8080';
const GAME_HTTP_URL = WS_GAME_URL.replace(/^ws/, 'http');

const router = Router();

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
