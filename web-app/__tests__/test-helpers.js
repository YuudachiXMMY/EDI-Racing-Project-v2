import Database from 'better-sqlite3';
import { readFileSync } from 'fs';
import { join, dirname } from 'path';
import { fileURLToPath } from 'url';
import { seedTemplates } from '../src/seed-templates.js';
import { applyMigrations } from '../src/db.js';

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

  // Apply the same migrations as production init (single source of truth in db.js).
  applyMigrations(db);

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
