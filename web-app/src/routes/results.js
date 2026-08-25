import { Router } from 'express';
import { timingSafeEqual } from 'crypto';
import { getDb } from '../db.js';
import { requireAuth } from '../middleware/auth.js';
import { loadOwnedSurvey } from '../middleware/loadOwnedSurvey.js';
import { DEFAULT_INTERNAL_SECRET } from '../hostToken.js';

const router = Router();

// POST /api/surveys/:id/results — store race results
router.post('/surveys/:id/results', requireAuth, loadOwnedSurvey, (req, res) => {
  const db = getDb();
  const survey = req.survey;

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
router.get('/surveys/:id/results', requireAuth, loadOwnedSurvey, (req, res) => {
  const db = getDb();
  const survey = req.survey;

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

// POST /api/sessions/archive — store archived session (called by WS server, shared-secret auth).
// This is an auth boundary INDEPENDENT of REQUIRE_HOST_TOKEN: it can write session records
// against any professor account, so the public default secret must never be a valid credential.
const INTERNAL_SECRET = process.env.INTERNAL_SECRET || DEFAULT_INTERNAL_SECRET;

/**
 * Whether the configured secret is usable to authorize session archiving. Pure —
 * never reads process.env; the caller passes the raw value. The public default (or an
 * unset/empty secret) is NOT usable, so archiving fails closed rather than accepting a
 * credential anyone can read from the repo.
 * @param {string|undefined} secret - raw process.env.INTERNAL_SECRET
 * @returns {boolean}
 */
export function archiveSecretUsable(secret) {
  return typeof secret === 'string' && secret.length > 0 && secret !== DEFAULT_INTERNAL_SECRET;
}

const ARCHIVE_ENABLED = archiveSecretUsable(process.env.INTERNAL_SECRET);

router.post('/sessions/archive', (req, res) => {
  // Fail closed: with a default/unset secret the endpoint is disabled entirely, so the
  // public value 'edi-internal-default' can never authorize an archive write.
  if (!ARCHIVE_ENABLED) {
    return res.status(503).json({
      success: false,
      error: 'Session archiving is disabled: set a strong INTERNAL_SECRET.',
    });
  }
  // Constant-time comparison so a caller cannot probe the secret byte-by-byte via timing.
  const provided = Buffer.from(req.headers['x-internal-secret'] || '');
  const expected = Buffer.from(INTERNAL_SECRET);
  if (provided.length !== expected.length || !timingSafeEqual(provided, expected)) {
    return res.status(403).json({ success: false, error: 'Forbidden' });
  }
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
      game_phase, race_started, rankings_json, event_log_json, total_race_time, started_at)
     VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`
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
    totalRaceTime || 0,
    req.body.startedAt || new Date().toISOString()
  );

  res.json({ success: true, data: { id: Number(result.lastInsertRowid) } });
});

// POST /api/internal/race-results — WS-server write path that populates the survey Results tab.
// Shared-secret auth, INDEPENDENT of the user JWT (the WS relay has no professor token) and
// fail-closed like /sessions/archive. Looks up the survey by linked room code and inserts one
// race_results row. Called by Server/server.js on every `race_results` message (End/Save/Export).
router.post('/internal/race-results', (req, res) => {
  if (!ARCHIVE_ENABLED) {
    return res.status(503).json({
      success: false,
      error: 'Results write is disabled: set a strong INTERNAL_SECRET.',
    });
  }
  const provided = Buffer.from(req.headers['x-internal-secret'] || '');
  const expected = Buffer.from(INTERNAL_SECRET);
  if (provided.length !== expected.length || !timingSafeEqual(provided, expected)) {
    return res.status(403).json({ success: false, error: 'Forbidden' });
  }

  const { roomCode, configName, rankings, eventLog, totalRaceTime } = req.body;
  if (!roomCode) {
    return res.status(400).json({ success: false, error: 'roomCode is required' });
  }
  if (!rankings || !Array.isArray(rankings)) {
    return res.status(400).json({ success: false, error: 'rankings array is required' });
  }

  const db = getDb();
  const linked = db.prepare(
    'SELECT id FROM surveys WHERE linked_room_code = ? COLLATE NOCASE'
  ).get(roomCode);

  // Unlinked room (e.g. in-game host without a web survey): skip quietly rather than error.
  if (!linked) {
    return res.json({ success: true, skipped: true });
  }

  const result = db.prepare(
    `INSERT INTO race_results (survey_id, room_code, config_name, rankings_json, event_log_json, total_race_time)
     VALUES (?, ?, ?, ?, ?, ?)`
  ).run(
    linked.id,
    roomCode,
    configName || '',
    JSON.stringify(rankings),
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
