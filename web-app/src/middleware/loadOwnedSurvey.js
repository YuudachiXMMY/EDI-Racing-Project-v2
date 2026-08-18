import { getDb } from '../db.js';

/**
 * Load the survey named by `:id` and verify it belongs to the authenticated user.
 * On success attaches the full row to `req.survey` and calls `next()`.
 * On miss (not found OR not owned) responds 404 with the standard envelope.
 *
 * MUST be mounted AFTER `requireAuth` — depends on `req.user.userId`.
 *
 * @param {import('express').Request} req
 * @param {import('express').Response} res
 * @param {import('express').NextFunction} next
 */
export function loadOwnedSurvey(req, res, next) {
  const db = getDb();
  const survey = db.prepare('SELECT * FROM surveys WHERE id = ? AND user_id = ?')
    .get(req.params.id, req.user.userId);
  if (!survey) {
    return res.status(404).json({ success: false, error: 'Survey not found' });
  }
  req.survey = survey;
  next();
}
