import { describe, it, expect, beforeAll, afterAll, vi } from 'vitest';
import express from 'express';
import { createTestDb, createTestUser } from './test-helpers.js';

// The internal race-results endpoint is gated by a module-load-time ARCHIVE_ENABLED flag derived
// from INTERNAL_SECRET. Set a strong secret BEFORE the dynamic import below so the route is
// enabled (a default/unset secret fails closed — covered by results-archive.test.js).
process.env.INTERNAL_SECRET = 'strong-internal-secret-0123456789abcdef';
const SECRET = process.env.INTERNAL_SECRET;

let testDb;
vi.mock('../src/db.js', async (importOriginal) => {
  const actual = await importOriginal();
  return { ...actual, getDb: () => testDb };
});

// Dynamic import (runs after the env assignment above) so results.js binds the strong secret.
const { default: resultsRoutes } = await import('../src/routes/results.js');

let server, baseUrl, surveyId;

// A single Unity CarResult row with the millisecond time fields.
const RANKINGS = [
  { Rank: 1, TeamName: 'Team A', Attributes: [{ Key: 'colorIndex', Value: '0' }],
    LapsCompleted: 9, CheckpointsPassed: 54, TotalTime: 1.5,
    ElapsedTime: 120.001, BestLapTime: 12.750, AverageLapTime: 13.333 },
];

beforeAll(async () => {
  testDb = createTestDb();
  const { userId } = createTestUser(testDb);

  // Survey linked to room ROOM01, plus an unlinked room has no survey.
  const info = testDb.prepare(
    'INSERT INTO surveys (user_id, config_name, questions_json, mappings_json, rules_json, share_code, linked_room_code) VALUES (?,?,?,?,?,?,?)'
  ).run(userId, 'Linked Survey', '[]', '[]', '[]', 'SHARE001', 'ROOM01');
  surveyId = info.lastInsertRowid;

  const app = express();
  app.use(express.json());
  app.use('/api', resultsRoutes);
  await new Promise(resolve => { server = app.listen(0, resolve); });
  baseUrl = `http://127.0.0.1:${server.address().port}`;
});

afterAll(() => {
  server?.close();
  testDb?.close();
});

function post(body, headers = {}) {
  return fetch(`${baseUrl}/api/internal/race-results`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', ...headers },
    body: JSON.stringify(body),
  });
}

describe('POST /api/internal/race-results', () => {
  it('rejects a request without the shared secret (403)', async () => {
    const res = await post({ roomCode: 'ROOM01', rankings: RANKINGS });
    expect(res.status).toBe(403);
  });

  it('rejects a request with a wrong secret (403)', async () => {
    const res = await post({ roomCode: 'ROOM01', rankings: RANKINGS }, { 'x-internal-secret': 'nope' });
    expect(res.status).toBe(403);
  });

  it('400s when rankings is missing', async () => {
    const res = await post({ roomCode: 'ROOM01' }, { 'x-internal-secret': SECRET });
    expect(res.status).toBe(400);
  });

  it('writes a race_results row for a linked room', async () => {
    const res = await post(
      { roomCode: 'ROOM01', configName: 'Linked Survey', rankings: RANKINGS, eventLog: [], totalRaceTime: 120.001 },
      { 'x-internal-secret': SECRET }
    );
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body.success).toBe(true);
    expect(body.data.id).toBeGreaterThan(0);

    const row = testDb.prepare('SELECT * FROM race_results WHERE survey_id = ?').get(surveyId);
    expect(row).toBeTruthy();
    expect(row.room_code).toBe('ROOM01');
    const stored = JSON.parse(row.rankings_json);
    expect(stored[0].BestLapTime).toBe(12.75);
  });

  it('skips (no row) for an unlinked room without erroring', async () => {
    const before = testDb.prepare('SELECT COUNT(*) AS n FROM race_results').get().n;
    const res = await post(
      { roomCode: 'NOSUCH', rankings: RANKINGS },
      { 'x-internal-secret': SECRET }
    );
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body.success).toBe(true);
    expect(body.skipped).toBe(true);
    const after = testDb.prepare('SELECT COUNT(*) AS n FROM race_results').get().n;
    expect(after).toBe(before);
  });
});
