import { describe, it, expect, beforeEach, vi } from 'vitest';
import Database from 'better-sqlite3';
import { readFileSync } from 'fs';
import { join, dirname } from 'path';
import { fileURLToPath } from 'url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const schemaPath = join(__dirname, '..', 'src', 'schema.sql');

// The middleware calls getDb() internally, so mock the db module to return the
// per-test in-memory database instead of the production file DB.
let testDb;
vi.mock('../src/db.js', () => ({
  getDb: () => testDb,
}));

const { loadOwnedSurvey } = await import('../src/middleware/loadOwnedSurvey.js');

function makeRes() {
  const res = {};
  res.status = (code) => {
    res._code = code;
    return { json: (body) => { res._body = body; } };
  };
  return res;
}

describe('loadOwnedSurvey middleware', () => {
  let ownerId, otherId, surveyId;

  beforeEach(() => {
    testDb = new Database(':memory:');
    testDb.pragma('foreign_keys = ON');
    testDb.exec(readFileSync(schemaPath, 'utf-8'));

    ownerId = testDb.prepare(
      "INSERT INTO users (email, password_hash) VALUES ('owner@test.com', 'x')"
    ).run().lastInsertRowid;
    otherId = testDb.prepare(
      "INSERT INTO users (email, password_hash) VALUES ('other@test.com', 'x')"
    ).run().lastInsertRowid;
    surveyId = testDb.prepare(
      "INSERT INTO surveys (user_id, config_name) VALUES (?, 'Mine')"
    ).run(ownerId).lastInsertRowid;
  });

  it('returns 404 with the standard envelope when the survey belongs to another user', () => {
    const req = { params: { id: surveyId }, user: { userId: otherId } };
    const res = makeRes();
    let nextCalled = false;
    loadOwnedSurvey(req, res, () => { nextCalled = true; });

    expect(res._code).toBe(404);
    expect(res._body).toEqual({ success: false, error: 'Survey not found' });
    expect(nextCalled).toBe(false);
    expect(req.survey).toBeUndefined();
  });

  it('returns 404 when the survey id does not exist', () => {
    const req = { params: { id: 99999 }, user: { userId: ownerId } };
    const res = makeRes();
    let nextCalled = false;
    loadOwnedSurvey(req, res, () => { nextCalled = true; });

    expect(res._code).toBe(404);
    expect(nextCalled).toBe(false);
  });

  it('attaches req.survey and calls next() when the caller owns the survey', () => {
    const req = { params: { id: surveyId }, user: { userId: ownerId } };
    const res = makeRes();
    let nextCalled = false;
    loadOwnedSurvey(req, res, () => { nextCalled = true; });

    expect(nextCalled).toBe(true);
    expect(res._code).toBeUndefined();
    expect(req.survey).toBeDefined();
    expect(req.survey.id).toBe(surveyId);
    expect(req.survey.config_name).toBe('Mine');
  });
});
