import { Router } from 'express';
import { getDb } from '../db.js';
import { requireAuth } from '../middleware/auth.js';
import { loadOwnedSurvey } from '../middleware/loadOwnedSurvey.js';
import { GAME_HTTP_URL } from '../config.js';

const router = Router();

// GET /api/s/:shareCode — get survey for student (no auth)
router.get('/s/:shareCode', (req, res) => {
  const db = getDb();
  const survey = db.prepare(
    'SELECT id, config_name, description, questions_json, is_active FROM surveys WHERE share_code = ? COLLATE NOCASE'
  ).get(req.params.shareCode);

  if (!survey) {
    return res.status(404).json({ success: false, error: 'Survey not found' });
  }
  if (!survey.is_active) {
    return res.status(403).json({ success: false, error: 'This survey is no longer accepting responses' });
  }

  res.json({
    success: true,
    data: {
      id: survey.id,
      configName: survey.config_name,
      description: survey.description,
      questions: JSON.parse(survey.questions_json),
    }
  });
});

// POST /api/s/:shareCode/respond — submit student response (no auth)
router.post('/s/:shareCode/respond', (req, res) => {
  const { email, teamName, answers } = req.body;

  if (!email || !email.trim()) {
    return res.status(400).json({ success: false, error: 'Email is required' });
  }
  if (!teamName || !teamName.trim()) {
    return res.status(400).json({ success: false, error: 'Team name is required' });
  }

  const db = getDb();
  const survey = db.prepare(
    'SELECT id, is_active FROM surveys WHERE share_code = ? COLLATE NOCASE'
  ).get(req.params.shareCode);

  if (!survey) {
    return res.status(404).json({ success: false, error: 'Survey not found' });
  }
  if (!survey.is_active) {
    return res.status(403).json({ success: false, error: 'This survey is no longer accepting responses' });
  }

  try {
    const result = db.prepare(
      'INSERT INTO responses (survey_id, email, team_name, answers_json) VALUES (?, ?, ?, ?)'
    ).run(survey.id, email.trim(), teamName.trim(), JSON.stringify(answers || {}));

    // Notify WS server if survey is linked to a room (fire-and-forget)
    const linked = db.prepare('SELECT linked_room_code FROM surveys WHERE id = ?').get(survey.id);
    if (linked && linked.linked_room_code) {
      const count = db.prepare('SELECT COUNT(*) as c FROM responses WHERE survey_id = ?').get(survey.id).c;
      fetch(`${GAME_HTTP_URL}/api/notify-response`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          roomCode: linked.linked_room_code,
          responseCount: count,
          teamName: teamName.trim(),
          surveyId: survey.id,
        }),
      }).catch(() => {}); // Silently ignore if WS server unreachable
    }

    res.status(201).json({ success: true, data: { id: result.lastInsertRowid } });
  } catch (err) {
    if (err.message.includes('UNIQUE constraint failed')) {
      return res.status(409).json({ success: false, error: 'You have already submitted a response for this survey' });
    }
    throw err;
  }
});

// GET /api/surveys/:id/responses — list responses (professor, requires auth)
router.get('/surveys/:id/responses', requireAuth, loadOwnedSurvey, (req, res) => {
  const db = getDb();
  const responses = db.prepare(
    'SELECT id, email, team_name, answers_json, submitted_at FROM responses WHERE survey_id = ? ORDER BY submitted_at DESC'
  ).all(req.params.id);

  const parsed = responses.map(r => ({
    ...r,
    answers: JSON.parse(r.answers_json),
  }));

  res.json({ success: true, data: parsed });
});

export default router;
