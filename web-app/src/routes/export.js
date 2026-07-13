import { Router } from 'express';
import { getDb } from '../db.js';
import { requireAuth } from '../middleware/auth.js';

const router = Router();

// GET /api/surveys/:id/export — export survey data for Unity
router.get('/:id/export', requireAuth, (req, res) => {
  const db = getDb();
  const survey = db.prepare('SELECT * FROM surveys WHERE id = ? AND user_id = ?')
    .get(req.params.id, req.user.userId);
  if (!survey) {
    return res.status(404).json({ success: false, error: 'Survey not found' });
  }

  // Phase 4 will add response->CarData mapping here
  // For now, return structure with empty carData
  const exportData = {
    configName: survey.config_name,
    carData: [],
    eventRules: JSON.parse(survey.rules_json),
  };

  res.json({ success: true, data: exportData });
});

export default router;
