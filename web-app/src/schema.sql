-- Professor accounts
CREATE TABLE IF NOT EXISTS users (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  email TEXT UNIQUE NOT NULL,
  password_hash TEXT NOT NULL,
  display_name TEXT NOT NULL DEFAULT '',
  created_at TEXT NOT NULL DEFAULT (datetime('now'))
);

-- Password reset tokens (single-use, short-lived). Only the SHA-256 hash of the
-- token is stored; the raw token lives only in the emailed reset link.
CREATE TABLE IF NOT EXISTS password_resets (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  user_id INTEGER NOT NULL REFERENCES users(id),
  token_hash TEXT NOT NULL UNIQUE,
  expires_at INTEGER NOT NULL,          -- epoch ms
  used_at INTEGER DEFAULT NULL,         -- epoch ms; set once the token is consumed
  created_at TEXT NOT NULL DEFAULT (datetime('now'))
);
CREATE INDEX IF NOT EXISTS idx_password_resets_token ON password_resets(token_hash);

-- Survey configurations (mirrors Unity SurveyConfig)
CREATE TABLE IF NOT EXISTS surveys (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  user_id INTEGER NOT NULL REFERENCES users(id),
  config_name TEXT NOT NULL,
  description TEXT NOT NULL DEFAULT '',
  -- Full SurveyJS question schema stored as JSON
  questions_json TEXT NOT NULL DEFAULT '[]',
  -- AttributeMapping[] stored as JSON
  mappings_json TEXT NOT NULL DEFAULT '[]',
  -- SavedEventRule[] stored as JSON
  rules_json TEXT NOT NULL DEFAULT '[]',
  -- Post-processing rules (aggregate transforms) stored as JSON
  post_processing_json TEXT NOT NULL DEFAULT '[]',
  -- Share code for student access
  share_code TEXT UNIQUE,
  is_active INTEGER NOT NULL DEFAULT 1,
  -- Soft-delete flag: archived surveys are hidden from the main list but can be restored.
  is_archived INTEGER NOT NULL DEFAULT 0,
  -- Room code linked for real-time response notifications
  linked_room_code TEXT DEFAULT NULL,
  created_at TEXT NOT NULL DEFAULT (datetime('now')),
  updated_at TEXT NOT NULL DEFAULT (datetime('now'))
);

-- Student survey responses
CREATE TABLE IF NOT EXISTS responses (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  survey_id INTEGER NOT NULL REFERENCES surveys(id),
  -- Email and team name are optional; stored as '' when the student omits them.
  email TEXT NOT NULL DEFAULT '',
  team_name TEXT NOT NULL DEFAULT '',
  -- Raw answers as JSON key-value pairs
  answers_json TEXT NOT NULL DEFAULT '{}',
  submitted_at TEXT NOT NULL DEFAULT (datetime('now'))
);

-- Unique constraint: one response per email per survey.
-- Partial so that optional (empty) emails never collide with each other.
CREATE UNIQUE INDEX IF NOT EXISTS idx_responses_unique
  ON responses(survey_id, email) WHERE email != '';

-- Race results sent from Unity game
CREATE TABLE IF NOT EXISTS race_results (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  survey_id INTEGER NOT NULL REFERENCES surveys(id),
  room_code TEXT NOT NULL DEFAULT '',
  config_name TEXT NOT NULL DEFAULT '',
  rankings_json TEXT NOT NULL DEFAULT '[]',
  event_log_json TEXT NOT NULL DEFAULT '[]',
  total_race_time REAL NOT NULL DEFAULT 0,
  received_at TEXT NOT NULL DEFAULT (datetime('now'))
);

-- Game session history (archived when room closes)
CREATE TABLE IF NOT EXISTS game_sessions (
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
);

-- Built-in survey templates (seeded on first startup)
CREATE TABLE IF NOT EXISTS templates (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  name TEXT UNIQUE NOT NULL,
  description TEXT NOT NULL DEFAULT '',
  questions_json TEXT NOT NULL DEFAULT '[]',
  mappings_json TEXT NOT NULL DEFAULT '[]',
  rules_json TEXT NOT NULL DEFAULT '[]',
  post_processing_json TEXT NOT NULL DEFAULT '[]',
  created_at TEXT NOT NULL DEFAULT (datetime('now'))
);
