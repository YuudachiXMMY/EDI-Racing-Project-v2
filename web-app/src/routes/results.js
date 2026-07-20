import { Router } from 'express';
import { getDb } from '../db.js';
import { requireAuth } from '../middleware/auth.js';

const router = Router();

// POST /api/surveys/:id/results — store race results
router.post('/:id/results', requireAuth, (req, res) => {
  const db = getDb();
  const survey = db.prepare('SELECT * FROM surveys WHERE id = ? AND user_id = ?')
    .get(req.params.id, req.user.userId);
  if (!survey) {
    return res.status(404).json({ success: false, error: 'Survey not found' });
  }

  const { roomCode, configName, rankings, eventLog, totalRaceTime } = req.body;
  if (!rankings || !Array.isArray(rankings)) {
    return res.status(400).json({ success: false, error: 'rankings array is required' });
  }

  const result = db.prepare(
    `INSERT INTO race_results (survey_id, room_code, config_name, rankings_json, event_log_json, total_race_time)
     VALUES (?, ?, ?, ?, ?, ?)`
  ).run(
    survey.id,
    roomCode || '',
    configName || '',
    JSON.stringify(rankings),
    JSON.stringify(eventLog || []),
    totalRaceTime || 0
  );

  res.json({ success: true, data: { id: Number(result.lastInsertRowid) } });
});

// GET /api/surveys/:id/results — fetch all race results for a survey
router.get('/:id/results', requireAuth, (req, res) => {
  const db = getDb();
  const survey = db.prepare('SELECT * FROM surveys WHERE id = ? AND user_id = ?')
    .get(req.params.id, req.user.userId);
  if (!survey) {
    return res.status(404).json({ success: false, error: 'Survey not found' });
  }

  const results = db.prepare(
    'SELECT * FROM race_results WHERE survey_id = ? ORDER BY received_at DESC'
  ).all(survey.id);

  const parsed = results.map(r => ({
    id: r.id,
    roomCode: r.room_code,
    configName: r.config_name,
    rankings: JSON.parse(r.rankings_json),
    eventLog: JSON.parse(r.event_log_json),
    totalRaceTime: r.total_race_time,
    receivedAt: r.received_at,
  }));

  res.json({ success: true, data: parsed });
});

export default router;
