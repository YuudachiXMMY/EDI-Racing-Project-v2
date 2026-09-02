import { describe, it, expect } from 'vitest';
import Database from 'better-sqlite3';
import { readFileSync } from 'fs';
import { join, dirname } from 'path';
import { fileURLToPath } from 'url';
import { applyMigrations } from '../src/db.js';

const __dirname = dirname(fileURLToPath(import.meta.url));
const schemaPath = join(__dirname, '..', 'src', 'schema.sql');

const GAME_SESSIONS_COLUMNS = [
  'id', 'user_id', 'survey_id', 'room_code', 'config_name', 'student_count',
  'student_names_json', 'game_phase', 'race_started', 'rankings_json',
  'event_log_json', 'total_race_time', 'started_at', 'ended_at',
];

function freshSchemaDb() {
  const db = new Database(':memory:');
  db.pragma('foreign_keys = ON');
  db.exec(readFileSync(schemaPath, 'utf-8'));
  return db;
}

describe('applyMigrations', () => {
  it('leaves a fresh schema DB with a complete game_sessions table', () => {
    const db = freshSchemaDb();
    applyMigrations(db);

    const cols = db.prepare('PRAGMA table_info(game_sessions)').all().map(c => c.name);
    for (const col of GAME_SESSIONS_COLUMNS) {
      expect(cols).toContain(col);
    }
    expect(cols).toHaveLength(GAME_SESSIONS_COLUMNS.length);
  });

  it('is idempotent — replaying does not throw', () => {
    const db = freshSchemaDb();
    expect(() => {
      applyMigrations(db);
      applyMigrations(db);
      applyMigrations(db);
    }).not.toThrow();
  });

  it('backfills missing columns and creates game_sessions on an old-shape DB', () => {
    // Simulate a pre-migration database: surveys/templates without the added columns
    // and no game_sessions table at all.
    const db = new Database(':memory:');
    db.pragma('foreign_keys = ON');
    db.exec("CREATE TABLE surveys (id INTEGER PRIMARY KEY, user_id INTEGER, config_name TEXT)");
    db.exec("CREATE TABLE templates (id INTEGER PRIMARY KEY, name TEXT)");

    applyMigrations(db);

    const surveyCols = db.prepare('PRAGMA table_info(surveys)').all().map(c => c.name);
    expect(surveyCols).toContain('linked_room_code');
    expect(surveyCols).toContain('post_processing_json');
    expect(surveyCols).toContain('is_archived');

    const templateCols = db.prepare('PRAGMA table_info(templates)').all().map(c => c.name);
    expect(templateCols).toContain('post_processing_json');

    const gameSessions = db.prepare(
      "SELECT name FROM sqlite_master WHERE type='table' AND name='game_sessions'"
    ).get();
    expect(gameSessions).toBeDefined();
  });
});
