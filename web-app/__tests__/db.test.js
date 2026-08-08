import { describe, it, expect, beforeEach } from 'vitest';
import { createTestDb, createTestUser } from './test-helpers.js';
import { seedTemplates } from '../src/seed-templates.js';

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
