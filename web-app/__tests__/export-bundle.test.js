import { describe, it, expect, beforeAll, afterAll, vi } from 'vitest';
import express from 'express';
import { createTestDb, createTestUser } from './test-helpers.js';
import { createSession } from '../src/middleware/auth.js';

// Route handlers call getDb() at request time; point it at an isolated in-memory
// database. importOriginal keeps applyMigrations/etc real (test-helpers needs them).
let testDb;
vi.mock('../src/db.js', async (importOriginal) => {
  const actual = await importOriginal();
  return { ...actual, getDb: () => testDb };
});

// Imported after the mock is declared (vi.mock is hoisted) so the router binds the
// mocked getDb.
const { default: exportRoutes } = await import('../src/routes/export.js');

// Minimal in-memory reader for STORE-method zips (mirrors zip.test.js) — extracts
// each entry's bytes by walking the central directory + local headers.
function readZip(buf) {
  const eocd = buf.length - 22;
  expect(buf.readUInt32LE(eocd)).toBe(0x06054b50);
  const count = buf.readUInt16LE(eocd + 10);
  let ptr = buf.readUInt32LE(eocd + 16);
  const files = {};
  for (let i = 0; i < count; i++) {
    const size = buf.readUInt32LE(ptr + 24);
    const nameLen = buf.readUInt16LE(ptr + 28);
    const extraLen = buf.readUInt16LE(ptr + 30);
    const commentLen = buf.readUInt16LE(ptr + 32);
    const localOffset = buf.readUInt32LE(ptr + 42);
    const name = buf.toString('utf8', ptr + 46, ptr + 46 + nameLen);
    const localNameLen = buf.readUInt16LE(localOffset + 26);
    const localExtraLen = buf.readUInt16LE(localOffset + 28);
    const dataStart = localOffset + 30 + localNameLen + localExtraLen;
    files[name] = buf.subarray(dataStart, dataStart + size);
    ptr += 46 + nameLen + extraLen + commentLen;
  }
  return files;
}

const QUESTIONS = [
  { Id: 'color', Text: 'Car colour', Type: 1, Options: ['Blue', 'Red'] },
  { Id: 'count', Text: 'Members', Type: 2, Options: [] },
];
// Map the colour answer to a Unity colorIndex so the vehicle CSV has real content.
const MAPPINGS = [
  {
    QuestionId: 'color',
    AttributeName: 'colorIndex',
    TransformType: 'lookup',
    LookupEntries: [{ Key: 'Blue', Value: '0' }, { Key: 'Red', Value: '1' }],
    DefaultValue: '0',
  },
];

let server, baseUrl, token, surveyId;

beforeAll(async () => {
  testDb = createTestDb();
  const { userId } = createTestUser(testDb);
  token = createSession(userId, 'test@example.com');

  const info = testDb.prepare(
    'INSERT INTO surveys (user_id, config_name, questions_json, mappings_json, rules_json, share_code) VALUES (?,?,?,?,?,?)'
  ).run(userId, 'Demo Survey', JSON.stringify(QUESTIONS), JSON.stringify(MAPPINGS), '[]', 'ABC12345');
  surveyId = info.lastInsertRowid;

  const insertResp = testDb.prepare(
    'INSERT INTO responses (survey_id, email, team_name, answers_json) VALUES (?,?,?,?)'
  );
  insertResp.run(surveyId, 'a@example.com', 'Team A', JSON.stringify({ color: 'Blue', count: 3 }));
  insertResp.run(surveyId, 'b@example.com', 'Team B', JSON.stringify({ color: 'Red', count: 5 }));

  const app = express();
  app.use(express.json());
  app.use('/api/surveys', exportRoutes);
  await new Promise(resolve => { server = app.listen(0, resolve); });
  baseUrl = `http://127.0.0.1:${server.address().port}`;
});

afterAll(() => {
  server?.close();
  testDb?.close();
});

describe('GET /api/surveys/:id/export-bundle', () => {
  it('rejects an unauthenticated request', async () => {
    const res = await fetch(`${baseUrl}/api/surveys/${surveyId}/export-bundle`);
    expect(res.status).toBe(401);
  });

  it('returns a zip bundling responses xlsx, Unity CSV, and analysis CSV', async () => {
    const res = await fetch(`${baseUrl}/api/surveys/${surveyId}/export-bundle`, {
      headers: { Authorization: `Bearer ${token}` },
    });
    expect(res.status).toBe(200);
    expect(res.headers.get('content-type')).toBe('application/zip');
    expect(res.headers.get('content-disposition')).toContain('Demo_Survey-data.zip');

    const buf = Buffer.from(await res.arrayBuffer());
    const files = readZip(buf);

    expect(Object.keys(files).sort()).toEqual([
      'Demo_Survey-analysis.csv',
      'Demo_Survey-responses.xlsx',
      'vehicleGroupData.csv',
    ]);

    // Unity CSV: one row per response as teamName,colorIndex,functions.
    const vehicle = files['vehicleGroupData.csv'].toString('utf8');
    expect(vehicle).toContain('Team A,0,');
    expect(vehicle).toContain('Team B,1,');

    // Analysis CSV: tidy long-format header + a numeric metric row.
    const analysis = files['Demo_Survey-analysis.csv'].toString('utf8');
    expect(analysis.split('\n')[0]).toBe('Question,Type,Answered,Metric,Value');
    expect(analysis).toContain('Members,Numeric,2,Mean,4');

    // Responses workbook: a real xlsx is a zip whose first bytes are "PK\x03\x04".
    expect(files['Demo_Survey-responses.xlsx'].readUInt32LE(0)).toBe(0x04034b50);
  });

  it('404s for a survey the user does not own', async () => {
    const res = await fetch(`${baseUrl}/api/surveys/999999/export-bundle`, {
      headers: { Authorization: `Bearer ${token}` },
    });
    expect(res.status).toBe(404);
  });

  it('adds leaderboard.csv once the survey has a race_results row', async () => {
    // Newest race_results row for this survey — Unity CarResult shape (PascalCase), incl. the
    // millisecond time fields (ElapsedTime/BestLapTime/AverageLapTime).
    const rankings = [
      { Rank: 1, TeamName: 'Team A', Attributes: [{ Key: 'colorIndex', Value: '0' }],
        LapsCompleted: 7, CheckpointsPassed: 42, TotalTime: 3.1,
        ElapsedTime: 92.437, BestLapTime: 12.004, AverageLapTime: 13.205 },
      { Rank: 2, TeamName: 'Team B', Attributes: [{ Key: 'colorIndex', Value: '1' }],
        LapsCompleted: 6, CheckpointsPassed: 40, TotalTime: 2.0,
        ElapsedTime: 92.437, BestLapTime: 12.500, AverageLapTime: 14.100 },
    ];
    testDb.prepare(
      `INSERT INTO race_results (survey_id, room_code, config_name, rankings_json, event_log_json, total_race_time)
       VALUES (?,?,?,?,?,?)`
    ).run(surveyId, 'ROOM01', 'Demo Survey', JSON.stringify(rankings), '[]', 92.437);

    const res = await fetch(`${baseUrl}/api/surveys/${surveyId}/export-bundle`, {
      headers: { Authorization: `Bearer ${token}` },
    });
    expect(res.status).toBe(200);
    const files = readZip(Buffer.from(await res.arrayBuffer()));

    expect(Object.keys(files)).toContain('leaderboard.csv');
    const csv = files['leaderboard.csv'].toString('utf8');
    const [header, rowA] = csv.split('\n');
    expect(header).toBe('Rank,TeamName,colorIndex,LapsCompleted,CheckpointsPassed,TotalTime,BestLap,AvgLap');
    // Times rendered at millisecond (F3) precision; ElapsedTime surfaces as TotalTime.
    expect(rowA).toBe('1,Team A,0,7,42,92.437,12.004,13.205');
  });
});
