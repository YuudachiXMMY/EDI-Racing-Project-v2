import Database from 'better-sqlite3';
import { readFileSync, mkdirSync } from 'fs';
import { join, dirname } from 'path';
import { fileURLToPath } from 'url';
import { seedTemplates } from './seed-templates.js';

const __dirname = dirname(fileURLToPath(import.meta.url));
const DB_PATH = process.env.DB_PATH || join(__dirname, '..', 'data', 'edi-survey.db');

let db;

export function getDb() {
  if (!db) {
    mkdirSync(dirname(DB_PATH), { recursive: true });

    db = new Database(DB_PATH);
    db.pragma('journal_mode = WAL');
    db.pragma('foreign_keys = ON');

    const schema = readFileSync(join(__dirname, 'schema.sql'), 'utf-8');
    db.exec(schema);

    // Migration: add linked_room_code to existing surveys table
    try {
      db.exec('ALTER TABLE surveys ADD COLUMN linked_room_code TEXT DEFAULT NULL');
    } catch {
      // Column already exists — ignore
    }

    // Seed default templates if table is empty
    const count = db.prepare('SELECT COUNT(*) as c FROM templates').get().c;
    if (count === 0) {
      seedTemplates(db);
      console.log('[DB] Seeded default templates');
    }

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
