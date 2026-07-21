import Database from 'better-sqlite3';
import { readFileSync } from 'fs';
import { join, dirname } from 'path';
import { fileURLToPath } from 'url';
import { seedTemplates } from '../src/seed-templates.js';

const __dirname = dirname(fileURLToPath(import.meta.url));
const schemaPath = join(__dirname, '..', 'src', 'schema.sql');

/**
 * Creates an isolated in-memory SQLite database with the full schema applied.
 * Each test gets its own database — no shared state.
 */
export function createTestDb() {
  const db = new Database(':memory:');
  db.pragma('journal_mode = WAL');
  db.pragma('foreign_keys = ON');

  const schema = readFileSync(schemaPath, 'utf-8');
  db.exec(schema);

  // Apply same migrations as db.js
  try { db.exec("ALTER TABLE surveys ADD COLUMN linked_room_code TEXT DEFAULT NULL"); } catch {}
  try { db.exec("ALTER TABLE surveys ADD COLUMN post_processing_json TEXT NOT NULL DEFAULT '[]'"); } catch {}
  try { db.exec("ALTER TABLE templates ADD COLUMN post_processing_json TEXT NOT NULL DEFAULT '[]'"); } catch {}

  db.exec(`CREATE TABLE IF NOT EXISTS game_sessions (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id INTEGER REFERENCES users(id),
    survey_id INTEGER REFERENCES surveys(id),
    room_code TEXT NOT NULL DEFAULT '',
    config_name TEXT NOT NULL DEFAULT '',
    student_count INTEGER NOT NULL DEFAULT 0,
    student_names_json TEXT NOT NULL DEFAULT '[]',
    game_phase TEXT NOT NULL DEFAULT 'Setup',
    race_started INTEGER NOT NULL DEFAULT 0,
    rankings_json TEXT NOT NULL DEFAULT '[]',
    event_log_json TEXT NOT NULL DEFAULT '[]',
    total_race_time REAL NOT NULL DEFAULT 0,
    started_at TEXT NOT NULL DEFAULT (datetime('now')),
    ended_at TEXT NOT NULL DEFAULT (datetime('now'))
  )`);

  return db;
}

/**
 * Creates a test user and returns { userId, email }.
 */
export function createTestUser(db, email = 'test@example.com', passwordHash = '$2a$10$abcdefghijklmnopqrstuuABCDEFGHIJKLMNOPQRSTUVWX') {
  const result = db.prepare(
    'INSERT INTO users (email, password_hash, display_name) VALUES (?, ?, ?)'
  ).run(email, passwordHash, 'Test User');
  return { userId: result.lastInsertRowid, email };
}
