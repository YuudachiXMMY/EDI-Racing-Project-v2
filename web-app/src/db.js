import Database from 'better-sqlite3';
import { readFileSync, mkdirSync } from 'fs';
import { join, dirname } from 'path';
import { fileURLToPath } from 'url';
import { seedTemplates, refreshTemplateContent } from './seed-templates.js';

const __dirname = dirname(fileURLToPath(import.meta.url));
const DB_PATH = process.env.DB_PATH || join(__dirname, '..', 'data', 'edi-survey.db');

let db;

/**
 * Apply idempotent migrations to an already-schema'd database. Single source of truth
 * shared by production init (getDb) and the test harness (test-helpers.createTestDb),
 * so the three former copies of the game_sessions definition can no longer drift.
 *
 * - The three ALTER TABLE blocks backfill columns onto pre-existing surveys/templates
 *   tables in older databases; they are no-ops (caught) on fresh databases.
 * - The game_sessions CREATE is `IF NOT EXISTS`: redundant on a fresh DB (schema.sql
 *   already creates it) and a no-op on an old DB that already has it. Kept here so an
 *   old DB predating the table still gets it.
 *
 * @param {import('better-sqlite3').Database} db
 */
export function applyMigrations(db) {
  // Migration: add linked_room_code to existing surveys table
  try {
    db.exec('ALTER TABLE surveys ADD COLUMN linked_room_code TEXT DEFAULT NULL');
  } catch {
    // Column already exists — ignore
  }

  // Migration: add post_processing_json to existing surveys table
  try {
    db.exec("ALTER TABLE surveys ADD COLUMN post_processing_json TEXT NOT NULL DEFAULT '[]'");
  } catch {
    // Column already exists — ignore
  }

  // Migration: add is_archived to existing surveys table (soft-delete / archive support)
  try {
    db.exec('ALTER TABLE surveys ADD COLUMN is_archived INTEGER NOT NULL DEFAULT 0');
  } catch {
    // Column already exists — ignore
  }

  // Migration: add post_processing_json to existing templates table
  try {
    db.exec("ALTER TABLE templates ADD COLUMN post_processing_json TEXT NOT NULL DEFAULT '[]'");
  } catch {
    // Column already exists — ignore
  }

  // Migration: remove deprecated built-in templates from older DBs.
  // The seed set was reduced to just 'ENGG*1100 Survey', but these three legacy
  // templates persist because seedTemplates() uses INSERT OR IGNORE (never deletes).
  // Idempotent: a no-op once the rows are gone. Nothing references templates(id),
  // so there are no dependent surveys/foreign keys to cascade.
  db.prepare(
    "DELETE FROM templates WHERE name IN ('V1 Parity', 'Accessibility', 'Diversity')"
  ).run();

  // Migration: refresh mappings + post-processing on already-seeded template rows so DBs
  // seeded before the strict-gt / difference-based male rules pick up the new config.
  // No-op on a fresh DB (templates table is empty until seedTemplates runs below).
  try {
    refreshTemplateContent(db);
  } catch {
    // templates table not present yet — first-run seed handles content
  }

  // Migration: make the per-email response uniqueness partial. Email is now optional,
  // so a full UNIQUE(survey_id, email) index (present in older DBs) would reject a second
  // empty-email response for the same survey. Recreate it to only constrain non-empty
  // emails. Wrapped in try/catch: a no-op on old-shape DBs that have no responses table.
  try {
    db.exec('DROP INDEX IF EXISTS idx_responses_unique');
    db.exec(
      "CREATE UNIQUE INDEX IF NOT EXISTS idx_responses_unique ON responses(survey_id, email) WHERE email != ''"
    );
  } catch {
    // responses table does not exist yet — ignore
  }

  // Migration: create password_resets table for existing DBs (predating password recovery).
  // Idempotent: a no-op once the table exists (schema.sql creates it on fresh DBs).
  db.exec(`CREATE TABLE IF NOT EXISTS password_resets (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id INTEGER NOT NULL REFERENCES users(id),
    token_hash TEXT NOT NULL UNIQUE,
    expires_at INTEGER NOT NULL,
    used_at INTEGER DEFAULT NULL,
    created_at TEXT NOT NULL DEFAULT (datetime('now'))
  )`);
  db.exec('CREATE INDEX IF NOT EXISTS idx_password_resets_token ON password_resets(token_hash)');

  // Migration: create game_sessions table for existing DBs
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
}

export function getDb() {
  if (!db) {
    mkdirSync(dirname(DB_PATH), { recursive: true });

    db = new Database(DB_PATH);
    db.pragma('journal_mode = WAL');
    db.pragma('foreign_keys = ON');

    const schema = readFileSync(join(__dirname, 'schema.sql'), 'utf-8');
    db.exec(schema);

    applyMigrations(db);

    // Seed default templates (inserts missing ones via INSERT OR IGNORE)
    seedTemplates(db);

    console.log('[DB] Initialized:', DB_PATH);
  }
  return db;
}

export function closeDb() {
  if (db) {
    db.close();
    db = null;
  }
}
