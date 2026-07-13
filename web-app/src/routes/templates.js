import { Router } from 'express';
import { getDb } from '../db.js';

const router = Router();

// GET /api/templates — list available survey templates
router.get('/', (req, res) => {
  const db = getDb();
  const rows = db.prepare('SELECT * FROM templates ORDER BY id ASC').all();
  const templates = rows.map(r => ({
    name: r.name,
    description: r.description,
    config: {
      questions: JSON.parse(r.questions_json),
      mappings: JSON.parse(r.mappings_json),
      rules: JSON.parse(r.rules_json),
    }
  }));
  res.json({ success: true, data: templates });
});

export default router;
