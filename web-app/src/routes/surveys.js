import { Router } from 'express';
import { randomBytes } from 'crypto';
import { getDb } from '../db.js';
import { requireAuth } from '../middleware/auth.js';

const router = Router();

function generateShareCode() {
  return randomBytes(4).toString('hex').toUpperCase(); // 8-char code
}

// GET /api/surveys — list professor's surveys (includes response count)
router.get('/', requireAuth, (req, res) => {
  const db = getDb();
  const surveys = db.prepare(
    `SELECT s.id, s.config_name, s.description, s.share_code, s.is_active, s.created_at, s.updated_at,
       (SELECT COUNT(*) FROM responses r WHERE r.survey_id = s.id) AS response_count
     FROM surveys s WHERE s.user_id = ? ORDER BY s.updated_at DESC`
  ).all(req.user.userId);
  res.json({ success: true, data: surveys });
});

// GET /api/surveys/:id/responses/count
router.get('/:id/responses/count', requireAuth, (req, res) => {
  const db = getDb();
  const survey = db.prepare('SELECT id FROM surveys WHERE id = ? AND user_id = ?')
    .get(req.params.id, req.user.userId);
  if (!survey) {
    return res.status(404).json({ success: false, error: 'Survey not found' });
  }
  const row = db.prepare('SELECT COUNT(*) as count FROM responses WHERE survey_id = ?')
    .get(req.params.id);
  res.json({ success: true, data: { count: row.count } });
});

// GET /api/surveys/:id — get full survey config
router.get('/:id', requireAuth, (req, res) => {
  const db = getDb();
  const survey = db.prepare('SELECT * FROM surveys WHERE id = ? AND user_id = ?')
    .get(req.params.id, req.user.userId);
  if (!survey) {
    return res.status(404).json({ success: false, error: 'Survey not found' });
  }
  res.json({
    success: true,
    data: {
      ...survey,
      questions: JSON.parse(survey.questions_json),
      mappings: JSON.parse(survey.mappings_json),
      rules: JSON.parse(survey.rules_json),
    }
  });
});

// POST /api/surveys — create new survey
router.post('/', requireAuth, (req, res) => {
  const { configName, description, questions, mappings, rules } = req.body;
  if (!configName) {
    return res.status(400).json({ success: false, error: 'configName is required' });
  }

  const db = getDb();
  const shareCode = generateShareCode();
  const result = db.prepare(
    `INSERT INTO surveys (user_id, config_name, description, questions_json, mappings_json, rules_json, share_code)
     VALUES (?, ?, ?, ?, ?, ?, ?)`
  ).run(
    req.user.userId,
    configName,
    description || '',
    JSON.stringify(questions || []),
    JSON.stringify(mappings || []),
    JSON.stringify(rules || []),
    shareCode
  );

  res.status(201).json({
    success: true,
    data: { id: result.lastInsertRowid, shareCode }
  });
});

// PUT /api/surveys/:id — update survey
router.put('/:id', requireAuth, (req, res) => {
  const { configName, description, questions, mappings, rules } = req.body;
  const db = getDb();

  const existing = db.prepare('SELECT id FROM surveys WHERE id = ? AND user_id = ?')
    .get(req.params.id, req.user.userId);
  if (!existing) {
    return res.status(404).json({ success: false, error: 'Survey not found' });
  }

  db.prepare(
    `UPDATE surveys SET config_name = ?, description = ?, questions_json = ?, mappings_json = ?, rules_json = ?, updated_at = datetime('now')
     WHERE id = ? AND user_id = ?`
  ).run(
    configName,
    description || '',
    JSON.stringify(questions || []),
    JSON.stringify(mappings || []),
    JSON.stringify(rules || []),
    req.params.id,
    req.user.userId
  );

  res.json({ success: true });
});

// PATCH /api/surveys/:id/active — toggle survey active/inactive
router.patch('/:id/active', requireAuth, (req, res) => {
  const { isActive } = req.body;
  if (typeof isActive !== 'boolean') {
    return res.status(400).json({ success: false, error: 'isActive (boolean) is required' });
  }

  const db = getDb();
  const existing = db.prepare('SELECT id FROM surveys WHERE id = ? AND user_id = ?')
    .get(req.params.id, req.user.userId);
  if (!existing) {
    return res.status(404).json({ success: false, error: 'Survey not found' });
  }

  db.prepare(
    "UPDATE surveys SET is_active = ?, updated_at = datetime('now') WHERE id = ? AND user_id = ?"
  ).run(isActive ? 1 : 0, req.params.id, req.user.userId);

  res.json({ success: true });
});

// PATCH /api/surveys/:id/link-room — link survey to a game room for real-time notifications
router.patch('/:id/link-room', requireAuth, (req, res) => {
  const { roomCode } = req.body;
  if (!roomCode || !roomCode.trim()) {
    return res.status(400).json({ success: false, error: 'roomCode is required' });
  }

  const db = getDb();
  const result = db.prepare(
    "UPDATE surveys SET linked_room_code = ?, updated_at = datetime('now') WHERE id = ? AND user_id = ?"
  ).run(roomCode.trim().toUpperCase(), req.params.id, req.user.userId);

  if (result.changes === 0) {
    return res.status(404).json({ success: false, error: 'Survey not found' });
  }
  res.json({ success: true, data: { linkedRoomCode: roomCode.trim().toUpperCase() } });
});

// DELETE /api/surveys/:id/link-room — unlink survey from game room
router.delete('/:id/link-room', requireAuth, (req, res) => {
  const db = getDb();
  const result = db.prepare(
    "UPDATE surveys SET linked_room_code = NULL, updated_at = datetime('now') WHERE id = ? AND user_id = ?"
  ).run(req.params.id, req.user.userId);

  if (result.changes === 0) {
    return res.status(404).json({ success: false, error: 'Survey not found' });
  }
  res.json({ success: true });
});

// DELETE /api/surveys/:id
router.delete('/:id', requireAuth, (req, res) => {
  const db = getDb();
  const result = db.prepare('DELETE FROM surveys WHERE id = ? AND user_id = ?')
    .run(req.params.id, req.user.userId);
  if (result.changes === 0) {
    return res.status(404).json({ success: false, error: 'Survey not found' });
  }
  res.json({ success: true });
});

export default router;
