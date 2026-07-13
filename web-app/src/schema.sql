-- Professor accounts
CREATE TABLE IF NOT EXISTS users (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  email TEXT UNIQUE NOT NULL,
  password_hash TEXT NOT NULL,
  display_name TEXT NOT NULL DEFAULT '',
  created_at TEXT NOT NULL DEFAULT (datetime('now'))
);

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
  -- Share code for student access
  share_code TEXT UNIQUE,
  is_active INTEGER NOT NULL DEFAULT 1,
  created_at TEXT NOT NULL DEFAULT (datetime('now')),
  updated_at TEXT NOT NULL DEFAULT (datetime('now'))
);

-- Student survey responses
CREATE TABLE IF NOT EXISTS responses (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  survey_id INTEGER NOT NULL REFERENCES surveys(id),
  email TEXT NOT NULL,
  team_name TEXT NOT NULL,
  -- Raw answers as JSON key-value pairs
  answers_json TEXT NOT NULL DEFAULT '{}',
  submitted_at TEXT NOT NULL DEFAULT (datetime('now'))
);

-- Unique constraint: one response per email per survey
CREATE UNIQUE INDEX IF NOT EXISTS idx_responses_unique
  ON responses(survey_id, email);

-- Built-in survey templates (seeded on first startup)
CREATE TABLE IF NOT EXISTS templates (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  name TEXT UNIQUE NOT NULL,
  description TEXT NOT NULL DEFAULT '',
  questions_json TEXT NOT NULL DEFAULT '[]',
  mappings_json TEXT NOT NULL DEFAULT '[]',
  rules_json TEXT NOT NULL DEFAULT '[]',
  created_at TEXT NOT NULL DEFAULT (datetime('now'))
);
