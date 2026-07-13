import { Router } from 'express';
import { randomBytes } from 'crypto';
import { getDb } from '../db.js';
import { requireAuth } from '../middleware/auth.js';

const router = Router();

function generateShareCode() {
  return randomBytes(4).toString('hex').toUpperCase(); // 8-char code
}

// GET /api/surveys — list professor's surveys
router.get('/', requireAuth, (req, res) => {
  const db = getDb();
  const surveys = db.prepare(
    'SELECT id, config_name, description, share_code, is_active, created_at, updated_at FROM surveys WHERE user_id = ? ORDER BY updated_at DESC'
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
