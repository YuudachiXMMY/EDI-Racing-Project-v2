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

// POST /api/sessions/archive — store archived session (called by WS server, no auth)
router.post('/sessions/archive', (req, res) => {
  const { roomCode, configName, studentCount, studentNames, gamePhase,
          raceStarted, rankings, eventLog, totalRaceTime } = req.body;

  if (!roomCode) {
    return res.status(400).json({ success: false, error: 'roomCode is required' });
  }

  const db = getDb();

  // Try to find the survey linked to this room
  const linked = db.prepare(
    'SELECT id, user_id FROM surveys WHERE linked_room_code = ? COLLATE NOCASE'
  ).get(roomCode);

  const result = db.prepare(
    `INSERT INTO game_sessions
     (user_id, survey_id, room_code, config_name, student_count, student_names_json,
      game_phase, race_started, rankings_json, event_log_json, total_race_time)
     VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`
  ).run(
    linked ? linked.user_id : null,
    linked ? linked.id : null,
    roomCode,
    configName || '',
    studentCount || 0,
    JSON.stringify(studentNames || []),
    gamePhase || 'Setup',
    raceStarted ? 1 : 0,
    JSON.stringify(rankings || []),
    JSON.stringify(eventLog || []),
    totalRaceTime || 0
  );

  res.json({ success: true, data: { id: Number(result.lastInsertRowid) } });
});

// GET /api/sessions — list all game sessions for the logged-in professor
router.get('/sessions', requireAuth, (req, res) => {
  const db = getDb();
  const sessions = db.prepare(
    `SELECT gs.*, s.config_name as survey_config_name
     FROM game_sessions gs
     LEFT JOIN surveys s ON gs.survey_id = s.id
     WHERE gs.user_id = ?
     ORDER BY gs.ended_at DESC
     LIMIT 100`
  ).all(req.user.userId);

  const parsed = sessions.map(s => ({
    id: s.id,
    surveyId: s.survey_id,
    roomCode: s.room_code,
    configName: s.config_name || s.survey_config_name || '',
    studentCount: s.student_count,
    studentNames: JSON.parse(s.student_names_json),
    gamePhase: s.game_phase,
    raceStarted: !!s.race_started,
    rankings: JSON.parse(s.rankings_json),
    eventLog: JSON.parse(s.event_log_json),
    totalRaceTime: s.total_race_time,
    startedAt: s.started_at,
    endedAt: s.ended_at,
  }));

  res.json({ success: true, data: parsed });
});

export default router;
