import { describe, it, expect, beforeEach } from 'vitest';
import { createTestDb, createTestUser } from './test-helpers.js';
import { seedTemplates, refreshTemplateContent } from '../src/seed-templates.js';

describe('Database Schema', () => {
  let db;

  beforeEach(() => {
    db = createTestDb();
  });

  it('creates all required tables', () => {
    const tables = db.prepare(
      "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name"
    ).all().map(r => r.name);

    expect(tables).toContain('users');
    expect(tables).toContain('surveys');
    expect(tables).toContain('responses');
    expect(tables).toContain('race_results');
    expect(tables).toContain('templates');
    expect(tables).toContain('game_sessions');
  });

  it('enforces unique email on users', () => {
    createTestUser(db, 'a@test.com');
    expect(() => createTestUser(db, 'a@test.com')).toThrow();
  });

  it('enforces foreign key on surveys.user_id', () => {
    expect(() => {
      db.prepare(
        "INSERT INTO surveys (user_id, config_name) VALUES (999, 'Bad')"
      ).run();
    }).toThrow();
  });

  it('enforces unique response per email per survey', () => {
    const user = createTestUser(db);
    const survey = db.prepare(
      "INSERT INTO surveys (user_id, config_name) VALUES (?, 'Test')"
    ).run(user.userId);

    db.prepare(
      "INSERT INTO responses (survey_id, email, team_name) VALUES (?, 'a@b.com', 'Team1')"
    ).run(survey.lastInsertRowid);

    expect(() => {
      db.prepare(
        "INSERT INTO responses (survey_id, email, team_name) VALUES (?, 'a@b.com', 'Team2')"
      ).run(survey.lastInsertRowid);
    }).toThrow();
  });

  it('allows a response with no email or team name (both optional)', () => {
    const user = createTestUser(db);
    const survey = db.prepare(
      "INSERT INTO surveys (user_id, config_name) VALUES (?, 'Test')"
    ).run(user.userId);

    expect(() => {
      db.prepare(
        "INSERT INTO responses (survey_id, answers_json) VALUES (?, '{}')"
      ).run(survey.lastInsertRowid);
    }).not.toThrow();

    const row = db.prepare(
      'SELECT email, team_name FROM responses WHERE survey_id = ?'
    ).get(survey.lastInsertRowid);
    expect(row.email).toBe('');
    expect(row.team_name).toBe('');
  });

  it('does not treat empty emails as colliding (partial unique index)', () => {
    const user = createTestUser(db);
    const survey = db.prepare(
      "INSERT INTO surveys (user_id, config_name) VALUES (?, 'Test')"
    ).run(user.userId);

    db.prepare(
      "INSERT INTO responses (survey_id, email, team_name) VALUES (?, '', 'Team1')"
    ).run(survey.lastInsertRowid);

    // A second empty-email response must be allowed — uniqueness only applies to
    // non-empty emails.
    expect(() => {
      db.prepare(
        "INSERT INTO responses (survey_id, email, team_name) VALUES (?, '', 'Team2')"
      ).run(survey.lastInsertRowid);
    }).not.toThrow();
  });
});

describe('Seed Templates', () => {
  let db;

  beforeEach(() => {
    db = createTestDb();
  });

  it('seeds 1 default template', () => {
    seedTemplates(db);

    const count = db.prepare('SELECT COUNT(*) as c FROM templates').get().c;
    expect(count).toBe(1);
  });

  it('seeds are idempotent (INSERT OR IGNORE)', () => {
    seedTemplates(db);
    seedTemplates(db);

    const count = db.prepare('SELECT COUNT(*) as c FROM templates').get().c;
    expect(count).toBe(1);
  });

  it('template names match expected set', () => {
    seedTemplates(db);

    const names = db.prepare('SELECT name FROM templates ORDER BY name').all().map(r => r.name);
    expect(names).toEqual(['ENGG*1100 Survey']);
  });

  it('templates have valid JSON in rules_json', () => {
    seedTemplates(db);

    const templates = db.prepare('SELECT name, rules_json FROM templates').all();
    for (const t of templates) {
      const rules = JSON.parse(t.rules_json);
      expect(Array.isArray(rules)).toBe(true);
      expect(rules.length).toBeGreaterThan(0);
    }
  });

  it('ENGG*1100 seeds exactly the two weather rules', () => {
    seedTemplates(db);

    const row = db.prepare("SELECT rules_json FROM templates WHERE name = 'ENGG*1100 Survey'").get();
    const names = JSON.parse(row.rules_json).map(r => r.DisplayName);
    expect(names).toEqual(['Snow Weather', 'Night Weather']);
  });

  it('refreshTemplateContent rewrites a stale ENGG*1100 row down to the two weather rules', () => {
    // Simulate a DB seeded before the rule reduction: a full 7-rule rules_json already
    // present on the built-in ENGG*1100 row. seedTemplates()'s INSERT OR IGNORE would
    // skip this existing row, so refreshTemplateContent() is the ONLY path that brings
    // it current — this is the migration that rewrites live production DBs.
    const staleRules = [
      { DisplayName: 'Name Length Penalty', AttributeName: 'teamName', Operator: 6, CompareValue: '10', SpeedDelta: -10, Duration: 8, Weather: 0, AllowRepeat: false },
      { DisplayName: 'Color Boost (Blue)', AttributeName: 'colorIndex', Operator: 0, CompareValue: '3', SpeedDelta: 15, Duration: 6, Weather: 0, AllowRepeat: false },
      { DisplayName: 'Color Penalty (Red)', AttributeName: 'colorIndex', Operator: 0, CompareValue: '2', SpeedDelta: -12, Duration: 8, Weather: 0, AllowRepeat: false },
      { DisplayName: 'Function Boost (Password)', AttributeName: 'functions', Operator: 2, CompareValue: 'password', SpeedDelta: 10, Duration: 6, Weather: 0, AllowRepeat: false },
      { DisplayName: 'Function Penalty (Face Recog)', AttributeName: 'functions', Operator: 2, CompareValue: 'facerecog', SpeedDelta: -10, Duration: 8, Weather: 0, AllowRepeat: false },
      { DisplayName: 'Snow Weather', AttributeName: '', Operator: 8, CompareValue: '', SpeedDelta: -8, Duration: 12, Weather: 1, AllowRepeat: true },
      { DisplayName: 'Night Weather', AttributeName: '', Operator: 8, CompareValue: '', SpeedDelta: -5, Duration: 15, Weather: 2, AllowRepeat: true },
    ];
    db.prepare(
      "INSERT INTO templates (name, rules_json) VALUES ('ENGG*1100 Survey', ?)"
    ).run(JSON.stringify(staleRules));

    refreshTemplateContent(db);

    const row = db.prepare("SELECT rules_json FROM templates WHERE name = 'ENGG*1100 Survey'").get();
    const names = JSON.parse(row.rules_json).map(r => r.DisplayName);
    expect(names).toEqual(['Snow Weather', 'Night Weather']);

    // Updates the existing row in place — no duplicate row created.
    const count = db.prepare("SELECT COUNT(*) as c FROM templates WHERE name = 'ENGG*1100 Survey'").get().c;
    expect(count).toBe(1);
  });
});

describe('Survey CRUD', () => {
  let db, user;

  beforeEach(() => {
    db = createTestDb();
    user = createTestUser(db);
  });

  it('creates and reads a survey', () => {
    const result = db.prepare(
      "INSERT INTO surveys (user_id, config_name, description) VALUES (?, 'My Survey', 'desc')"
    ).run(user.userId);

    const survey = db.prepare('SELECT * FROM surveys WHERE id = ?').get(result.lastInsertRowid);
    expect(survey.config_name).toBe('My Survey');
    expect(survey.description).toBe('desc');
    expect(survey.user_id).toBe(user.userId);
  });

  it('updates a survey', () => {
    const result = db.prepare(
      "INSERT INTO surveys (user_id, config_name) VALUES (?, 'Old')"
    ).run(user.userId);

    db.prepare("UPDATE surveys SET config_name = 'New' WHERE id = ?").run(result.lastInsertRowid);

    const survey = db.prepare('SELECT config_name FROM surveys WHERE id = ?').get(result.lastInsertRowid);
    expect(survey.config_name).toBe('New');
  });

  it('deletes a survey', () => {
    const result = db.prepare(
      "INSERT INTO surveys (user_id, config_name) VALUES (?, 'ToDelete')"
    ).run(user.userId);

    db.prepare('DELETE FROM surveys WHERE id = ?').run(result.lastInsertRowid);

    const survey = db.prepare('SELECT * FROM surveys WHERE id = ?').get(result.lastInsertRowid);
    expect(survey).toBeUndefined();
  });
});
