# Plan: Web App Foundation (Phase 1)

## Summary

Bootstrap the EDI Survey Web App as a new Node.js/Express project with SQLite database, professor authentication (simple password), and Docker containerization. This phase produces a working API skeleton that later phases build upon (survey CRUD, student answering, export).

## User Story

As a professor, I want to register and log in to the EDI Survey Web App, so that I can later create and manage survey configurations securely.

## Problem -> Solution

**Current**: No standalone web app exists. All survey functionality lives inside Unity MonoBehaviours.
**Desired**: A running Express API with auth, database schema, and Docker image — ready for Phase 2 to add survey features.

## Metadata

- **Complexity**: Medium
- **Source PRD**: `.claude/PRPs/prds/edi-survey-web-app.prd.md`
- **PRD Phase**: Phase 1 — Web App Foundation
- **Estimated Files**: ~15 new files

---

## UX Design

N/A — internal API foundation. No user-facing UI in this phase (React frontend comes in Phase 2).

---

## Mandatory Reading

| Priority | File | Lines | Why |
|----------|------|-------|-----|
| P0 | `Server/server.js` | all | Existing Node.js patterns, coding style (CommonJS, minimal deps) |
| P0 | `Server/package.json` | all | Existing dependency management approach |
| P0 | `Deploy/Dockerfile` | all | Current Docker build pattern to extend |
| P0 | `Deploy/docker-compose.yml` | all | Current compose structure to add web-app service |
| P0 | `Deploy/nginx/nginx.conf` | all | Proxy routing pattern to extend for `/api` |
| P1 | `Assets/Scripts/Data/SurveyConfig.cs` | all | Data model to mirror in DB schema |
| P1 | `Assets/Scripts/Data/SurveyQuestion.cs` | all | Question types enum to mirror |
| P1 | `Assets/Scripts/Data/AttributeMapping.cs` | all | Mapping struct to mirror |
| P1 | `Assets/Scripts/Data/SessionData.cs` | 62-100 | SavedEventRule struct to mirror |
| P1 | `Assets/Scripts/Data/CarData.cs` | all | Export target format |

## External Documentation

| Topic | Source | Key Takeaway |
|-------|--------|--------------|
| better-sqlite3 | npm docs | Synchronous API, WAL mode for concurrency, zero config |
| bcryptjs | npm docs | Pure JS bcrypt for password hashing, no native deps |
| express + cors | npm docs | Standard REST API scaffolding |
| SurveyJS | surveyjs.io | JSON schema for survey definitions — store as JSON blob in SQLite |

---

## Patterns to Mirror

### NODE_STYLE

```javascript
// SOURCE: Server/server.js:1-2
// CommonJS require, minimal dependencies, plain JS (no TypeScript in Server/)
const { WebSocketServer } = require('ws');
const PORT = parseInt(process.env.PORT || '8080', 10);
```

**Decision**: The new web-app will use **ES modules** (`import/export`) and **plain JavaScript** (no TypeScript) to stay lightweight. This diverges from the WebSocket server's CommonJS but follows modern Node.js convention. We set `"type": "module"` in package.json.

### ERROR_RESPONSE

```javascript
// SOURCE: Server/server.js:112-114
// Simple JSON error responses
sendJSON(ws, { type: 'error', message: 'Room not found' });
```

**Web App pattern** — use consistent API envelope:

```javascript
// Success
res.json({ success: true, data: { ... } });

// Error
res.status(400).json({ success: false, error: 'Validation failed' });
```

### LOGGING

```javascript
// SOURCE: Server/server.js:106,131,54
console.log(`[Room ${roomCode}] Created`);
console.log(`[Room ${code}] Student joined (${room.students.size} total)`);
```

**Web App pattern**: Same `console.log` with `[Module]` prefix. Acceptable for this project scale.

### DOCKER_BUILD

```dockerfile
# SOURCE: Deploy/Dockerfile:1-9
FROM nginx:alpine
RUN apk add --no-cache nodejs npm
WORKDIR /app/server
COPY Server/ .
RUN npm ci --omit=dev
```

---

## Files to Change

| File | Action | Justification |
|------|--------|---------------|
| `web-app/package.json` | CREATE | Project manifest, dependencies, scripts |
| `web-app/src/index.js` | CREATE | Express app entry point |
| `web-app/src/db.js` | CREATE | SQLite initialization + schema migration |
| `web-app/src/schema.sql` | CREATE | Database schema DDL |
| `web-app/src/routes/auth.js` | CREATE | Register/login endpoints |
| `web-app/src/routes/surveys.js` | CREATE | Survey CRUD endpoints (skeleton) |
| `web-app/src/routes/export.js` | CREATE | Export endpoint (skeleton) |
| `web-app/src/middleware/auth.js` | CREATE | Simple session/token auth middleware |
| `web-app/Dockerfile` | CREATE | Multi-stage build for web-app |
| `web-app/.gitignore` | CREATE | node_modules, *.db |
| `Deploy/docker-compose.yml` | UPDATE | Add web-app service |
| `Deploy/nginx/nginx.conf` | UPDATE | Add `/api` proxy to web-app |

## NOT Building

- React frontend (Phase 2)
- SurveyJS integration (Phase 2)
- Student answering endpoints (Phase 3)
- JSON export logic (Phase 4)
- Survey templates seed data (Phase 5)

---

## Step-by-Step Tasks

### Task 1: Initialize web-app project

- **ACTION**: Create `web-app/` directory at project root with package.json
- **IMPLEMENT**:
  ```json
  {
    "name": "edi-survey-web-app",
    "version": "1.0.0",
    "type": "module",
    "description": "Survey management web app for EDI Racing Game",
    "main": "src/index.js",
    "scripts": {
      "start": "node src/index.js",
      "dev": "node --watch src/index.js"
    },
    "dependencies": {
      "express": "^4.21.0",
      "cors": "^2.8.5",
      "better-sqlite3": "^11.0.0",
      "bcryptjs": "^2.4.3",
      "crypto": "builtin"
    }
  }
  ```
- **MIRROR**: Server/package.json — minimal deps, simple scripts
- **GOTCHA**: Use `better-sqlite3` (synchronous, C binding) not `sqlite3` (async callback hell). Use `bcryptjs` (pure JS) not `bcrypt` (native compile issues in Alpine Docker).
- **VALIDATE**: `cd web-app && npm install` succeeds

### Task 2: Create SQLite schema

- **ACTION**: Create `web-app/src/schema.sql` with all tables
- **IMPLEMENT**:
  ```sql
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
  ```
- **MIRROR**: Unity's SurveyConfig stores Questions/Mappings/Rules as arrays — we store as JSON columns in SQLite. This preserves the exact same structure for export.
- **GOTCHA**: SQLite JSON columns are just TEXT. Validation happens in the API layer, not the DB.
- **VALIDATE**: Schema loads without error in db.js init

### Task 3: Create database initialization module

- **ACTION**: Create `web-app/src/db.js`
- **IMPLEMENT**:
  ```javascript
  import Database from 'better-sqlite3';
  import { readFileSync } from 'fs';
  import { join, dirname } from 'path';
  import { fileURLToPath } from 'url';

  const __dirname = dirname(fileURLToPath(import.meta.url));
  const DB_PATH = process.env.DB_PATH || join(__dirname, '..', 'data', 'edi-survey.db');

  let db;

  export function getDb() {
    if (!db) {
      // Ensure data directory exists
      const { mkdirSync } = await import('fs');
      mkdirSync(dirname(DB_PATH), { recursive: true });

      db = new Database(DB_PATH);
      db.pragma('journal_mode = WAL');
      db.pragma('foreign_keys = ON');

      // Run schema migration
      const schema = readFileSync(join(__dirname, 'schema.sql'), 'utf-8');
      db.exec(schema);

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
  ```
- **MIRROR**: SurveyConfigManager.cs stores to `Application.persistentDataPath/SurveyConfigs/` — same concept, different path
- **GOTCHA**: `better-sqlite3` is synchronous — no async/await needed for queries. WAL mode enables concurrent reads (50 students).
- **VALIDATE**: Importing module creates DB file and tables

### Task 4: Create auth middleware

- **ACTION**: Create `web-app/src/middleware/auth.js`
- **IMPLEMENT**: Simple token-based auth using random hex tokens stored in a `sessions` table or in-memory Map. No JWT complexity.
  ```javascript
  import { randomBytes } from 'crypto';

  // In-memory session store (simple for MVP; survives within process lifetime)
  const sessions = new Map(); // token -> { userId, email }

  export function createSession(userId, email) {
    const token = randomBytes(32).toString('hex');
    sessions.set(token, { userId, email });
    return token;
  }

  export function destroySession(token) {
    sessions.delete(token);
  }

  export function requireAuth(req, res, next) {
    const header = req.headers.authorization;
    if (!header || !header.startsWith('Bearer ')) {
      return res.status(401).json({ success: false, error: 'Authentication required' });
    }
    const token = header.slice(7);
    const session = sessions.get(token);
    if (!session) {
      return res.status(401).json({ success: false, error: 'Invalid or expired session' });
    }
    req.user = session;
    next();
  }
  ```
- **MIRROR**: PRD says "简单密码保护" — this is deliberately simple, no OAuth/JWT complexity
- **GOTCHA**: In-memory sessions reset on server restart. Acceptable for classroom use. Can add SQLite persistence later if needed.
- **VALIDATE**: Protected routes return 401 without token, 200 with valid token

### Task 5: Create auth routes

- **ACTION**: Create `web-app/src/routes/auth.js`
- **IMPLEMENT**:
  ```javascript
  import { Router } from 'express';
  import bcrypt from 'bcryptjs';
  import { getDb } from '../db.js';
  import { createSession, destroySession } from '../middleware/auth.js';

  const router = Router();

  // POST /api/auth/register
  router.post('/register', (req, res) => {
    const { email, password, displayName } = req.body;

    if (!email || !password) {
      return res.status(400).json({ success: false, error: 'Email and password required' });
    }
    if (password.length < 6) {
      return res.status(400).json({ success: false, error: 'Password must be at least 6 characters' });
    }

    const db = getDb();
    const existing = db.prepare('SELECT id FROM users WHERE email = ?').get(email);
    if (existing) {
      return res.status(409).json({ success: false, error: 'Email already registered' });
    }

    const hash = bcrypt.hashSync(password, 10);
    const result = db.prepare(
      'INSERT INTO users (email, password_hash, display_name) VALUES (?, ?, ?)'
    ).run(email, hash, displayName || '');

    const token = createSession(result.lastInsertRowid, email);
    res.status(201).json({
      success: true,
      data: { token, user: { id: result.lastInsertRowid, email, displayName: displayName || '' } }
    });
  });

  // POST /api/auth/login
  router.post('/login', (req, res) => {
    const { email, password } = req.body;
    if (!email || !password) {
      return res.status(400).json({ success: false, error: 'Email and password required' });
    }

    const db = getDb();
    const user = db.prepare('SELECT * FROM users WHERE email = ?').get(email);
    if (!user || !bcrypt.compareSync(password, user.password_hash)) {
      return res.status(401).json({ success: false, error: 'Invalid credentials' });
    }

    const token = createSession(user.id, user.email);
    res.json({
      success: true,
      data: { token, user: { id: user.id, email: user.email, displayName: user.display_name } }
    });
  });

  // POST /api/auth/logout
  router.post('/logout', (req, res) => {
    const header = req.headers.authorization;
    if (header && header.startsWith('Bearer ')) {
      destroySession(header.slice(7));
    }
    res.json({ success: true });
  });

  export default router;
  ```
- **MIRROR**: API response envelope `{ success, data, error }` from TypeScript patterns
- **GOTCHA**: `bcrypt.hashSync` is blocking but acceptable — professor registration is infrequent. Use sync to match better-sqlite3's sync API.
- **VALIDATE**: Register → login → access protected route → logout → 401

### Task 6: Create survey routes skeleton

- **ACTION**: Create `web-app/src/routes/surveys.js` with CRUD endpoints (minimal implementation for now)
- **IMPLEMENT**: Full CRUD for surveys — this is needed so Phase 2 (React frontend) can immediately consume these endpoints.
  ```javascript
  import { Router } from 'express';
  import { randomBytes } from 'crypto';
  import { getDb } from '../db.js';
  import { requireAuth } from '../middleware/auth.js';

  const router = Router();

  function generateShareCode() {
    return randomBytes(4).toString('hex').toUpperCase(); // 8-char code
  }

  // GET /api/surveys — list professor's surveys
  router.get('/', requireAuth, (req, res) => {
    const db = getDb();
    const surveys = db.prepare(
      'SELECT id, config_name, description, share_code, is_active, created_at, updated_at FROM surveys WHERE user_id = ? ORDER BY updated_at DESC'
    ).all(req.user.userId);
    res.json({ success: true, data: surveys });
  });

  // GET /api/surveys/:id — get full survey config
  router.get('/:id', requireAuth, (req, res) => {
    const db = getDb();
    const survey = db.prepare('SELECT * FROM surveys WHERE id = ? AND user_id = ?')
      .get(req.params.id, req.user.userId);
    if (!survey) {
      return res.status(404).json({ success: false, error: 'Survey not found' });
    }
    res.json({
      success: true,
      data: {
        ...survey,
        questions: JSON.parse(survey.questions_json),
        mappings: JSON.parse(survey.mappings_json),
        rules: JSON.parse(survey.rules_json),
      }
    });
  });

  // POST /api/surveys — create new survey
  router.post('/', requireAuth, (req, res) => {
    const { configName, description, questions, mappings, rules } = req.body;
    if (!configName) {
      return res.status(400).json({ success: false, error: 'configName is required' });
    }

    const db = getDb();
    const shareCode = generateShareCode();
    const result = db.prepare(
      `INSERT INTO surveys (user_id, config_name, description, questions_json, mappings_json, rules_json, share_code)
       VALUES (?, ?, ?, ?, ?, ?, ?)`
    ).run(
      req.user.userId,
      configName,
      description || '',
      JSON.stringify(questions || []),
      JSON.stringify(mappings || []),
      JSON.stringify(rules || []),
      shareCode
    );

    res.status(201).json({
      success: true,
      data: { id: result.lastInsertRowid, shareCode }
    });
  });

  // PUT /api/surveys/:id — update survey
  router.put('/:id', requireAuth, (req, res) => {
    const { configName, description, questions, mappings, rules } = req.body;
    const db = getDb();

    const existing = db.prepare('SELECT id FROM surveys WHERE id = ? AND user_id = ?')
      .get(req.params.id, req.user.userId);
    if (!existing) {
      return res.status(404).json({ success: false, error: 'Survey not found' });
    }

    db.prepare(
      `UPDATE surveys SET config_name = ?, description = ?, questions_json = ?, mappings_json = ?, rules_json = ?, updated_at = datetime('now')
       WHERE id = ? AND user_id = ?`
    ).run(
      configName,
      description || '',
      JSON.stringify(questions || []),
      JSON.stringify(mappings || []),
      JSON.stringify(rules || []),
      req.params.id,
      req.user.userId
    );

    res.json({ success: true });
  });

  // DELETE /api/surveys/:id
  router.delete('/:id', requireAuth, (req, res) => {
    const db = getDb();
    const result = db.prepare('DELETE FROM surveys WHERE id = ? AND user_id = ?')
      .run(req.params.id, req.user.userId);
    if (result.changes === 0) {
      return res.status(404).json({ success: false, error: 'Survey not found' });
    }
    res.json({ success: true });
  });

  export default router;
  ```
- **MIRROR**: Repository pattern with direct SQL; API envelope `{ success, data }`
- **GOTCHA**: Always filter by `user_id` — professors can only see their own surveys
- **VALIDATE**: CRUD cycle via curl: create → list → get → update → delete

### Task 7: Create export route skeleton

- **ACTION**: Create `web-app/src/routes/export.js`
- **IMPLEMENT**: Skeleton endpoint that returns the JSON format Unity expects. Full response-to-CarData mapping logic comes in Phase 4.
  ```javascript
  import { Router } from 'express';
  import { getDb } from '../db.js';
  import { requireAuth } from '../middleware/auth.js';

  const router = Router();

  // GET /api/surveys/:id/export — export survey data for Unity
  router.get('/:id/export', requireAuth, (req, res) => {
    const db = getDb();
    const survey = db.prepare('SELECT * FROM surveys WHERE id = ? AND user_id = ?')
      .get(req.params.id, req.user.userId);
    if (!survey) {
      return res.status(404).json({ success: false, error: 'Survey not found' });
    }

    // Phase 4 will add response→CarData mapping here
    // For now, return structure with empty carData
    const exportData = {
      configName: survey.config_name,
      carData: [],
      eventRules: JSON.parse(survey.rules_json),
    };

    res.json({ success: true, data: exportData });
  });

  export default router;
  ```
- **VALIDATE**: Returns valid JSON with correct structure

### Task 8: Create Express app entry point

- **ACTION**: Create `web-app/src/index.js`
- **IMPLEMENT**:
  ```javascript
  import express from 'express';
  import cors from 'cors';
  import { getDb, closeDb } from './db.js';
  import authRoutes from './routes/auth.js';
  import surveyRoutes from './routes/surveys.js';
  import exportRoutes from './routes/export.js';

  const PORT = parseInt(process.env.API_PORT || '3001', 10);

  const app = express();
  app.use(cors());
  app.use(express.json({ limit: '1mb' }));

  // Initialize database on startup
  getDb();

  // Routes
  app.use('/api/auth', authRoutes);
  app.use('/api/surveys', surveyRoutes);
  app.use('/api/surveys', exportRoutes);

  // Health check
  app.get('/api/health', (req, res) => {
    res.json({ success: true, data: { status: 'ok' } });
  });

  // Global error handler
  app.use((err, req, res, _next) => {
    console.error('[API] Error:', err.message);
    res.status(500).json({ success: false, error: 'Internal server error' });
  });

  app.listen(PORT, () => {
    console.log(`[API] EDI Survey Web App listening on port ${PORT}`);
  });

  process.on('SIGTERM', () => {
    closeDb();
    process.exit(0);
  });
  ```
- **MIRROR**: Server/server.js console.log style with `[Module]` prefix
- **GOTCHA**: Port 3001 to avoid conflict with WebSocket server on 3000. Configure via `API_PORT` env var.
- **VALIDATE**: `node src/index.js` starts without error, `/api/health` returns OK

### Task 9: Create .gitignore

- **ACTION**: Create `web-app/.gitignore`
- **IMPLEMENT**:
  ```
  node_modules/
  data/
  *.db
  *.db-wal
  *.db-shm
  ```
- **VALIDATE**: `git status` doesn't show node_modules or db files

### Task 10: Create web-app Dockerfile

- **ACTION**: Create `web-app/Dockerfile`
- **IMPLEMENT**:
  ```dockerfile
  FROM node:20-alpine

  WORKDIR /app

  COPY package.json package-lock.json* ./
  RUN npm ci --omit=dev

  COPY src/ ./src/

  # Data directory for SQLite (mount as volume for persistence)
  RUN mkdir -p /app/data

  ENV API_PORT=3001
  ENV DB_PATH=/app/data/edi-survey.db

  EXPOSE 3001

  CMD ["node", "src/index.js"]
  ```
- **MIRROR**: Deploy/Dockerfile — alpine base, npm ci --omit=dev
- **GOTCHA**: SQLite DB file must be in a Docker volume for persistence across container restarts
- **VALIDATE**: `docker build -t edi-survey-web-app ./web-app` succeeds

### Task 11: Update docker-compose.yml

- **ACTION**: Update `Deploy/docker-compose.yml` to add web-app service
- **IMPLEMENT**:
  ```yaml
  services:
    edi-racing:
      build:
        context: ..
        dockerfile: Deploy/Dockerfile
      ports:
        - "8080:80"
      environment:
        - PORT=3000
      depends_on:
        - web-app
      restart: unless-stopped

    web-app:
      build:
        context: ../web-app
        dockerfile: Dockerfile
      volumes:
        - survey-data:/app/data
      environment:
        - API_PORT=3001
        - DB_PATH=/app/data/edi-survey.db
      restart: unless-stopped

  volumes:
    survey-data:
  ```
- **MIRROR**: Existing docker-compose.yml structure
- **GOTCHA**: web-app does NOT expose ports directly — nginx proxies `/api` to it. Use Docker internal networking.
- **VALIDATE**: `docker compose config` validates without error

### Task 12: Update nginx.conf for API proxy

- **ACTION**: Add `/api` location block to `Deploy/nginx/nginx.conf`
- **IMPLEMENT**: Add before the SPA fallback `location /`:
  ```nginx
  # Survey Web App API proxy
  location /api {
      proxy_pass http://web-app:3001;
      proxy_set_header Host $host;
      proxy_set_header X-Real-IP $remote_addr;
      proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
  }
  ```
- **MIRROR**: Existing `/ws` proxy block pattern
- **GOTCHA**: `web-app` hostname resolved by Docker Compose internal DNS. Must match service name in docker-compose.yml.
- **VALIDATE**: After `docker compose up`, `curl http://localhost:8080/api/health` returns OK

---

## Testing Strategy

### Manual API Tests (curl)

| Test | Command | Expected |
|------|---------|----------|
| Health check | `curl localhost:3001/api/health` | `{"success":true,"data":{"status":"ok"}}` |
| Register | `curl -X POST localhost:3001/api/auth/register -H 'Content-Type: application/json' -d '{"email":"prof@test.com","password":"test123"}'` | 201 with token |
| Login | `curl -X POST localhost:3001/api/auth/login -H 'Content-Type: application/json' -d '{"email":"prof@test.com","password":"test123"}'` | 200 with token |
| Unauthed | `curl localhost:3001/api/surveys` | 401 |
| Create survey | `curl -X POST localhost:3001/api/surveys -H 'Authorization: Bearer TOKEN' -H 'Content-Type: application/json' -d '{"configName":"Test"}'` | 201 |
| List surveys | `curl -H 'Authorization: Bearer TOKEN' localhost:3001/api/surveys` | 200 with array |

### Edge Cases Checklist

- [ ] Register with duplicate email returns 409
- [ ] Login with wrong password returns 401
- [ ] Short password (< 6 chars) returns 400
- [ ] Missing required fields return 400
- [ ] Access other professor's survey returns 404
- [ ] Delete non-existent survey returns 404
- [ ] Large JSON body (questions array) handles correctly
- [ ] Concurrent student submissions don't corrupt SQLite (WAL mode)

---

## Validation Commands

### Install

```bash
cd web-app && npm install
```

EXPECT: No errors, node_modules created

### Start Server

```bash
cd web-app && node src/index.js
```

EXPECT: `[API] EDI Survey Web App listening on port 3001`

### Docker Build

```bash
docker build -t edi-survey-web-app ./web-app
```

EXPECT: Image builds successfully

### Docker Compose

```bash
cd Deploy && docker compose config
```

EXPECT: Valid compose configuration

### Manual Validation

- [ ] Register a professor account
- [ ] Login with that account
- [ ] Create a survey with configName
- [ ] List surveys returns the created survey
- [ ] Get survey by ID returns full config
- [ ] Update survey config
- [ ] Delete survey
- [ ] Export endpoint returns correct JSON structure
- [ ] Docker build succeeds
- [ ] Docker compose up starts both services
- [ ] `/api/health` accessible through nginx proxy

---

## Acceptance Criteria

- [ ] Express API starts and serves `/api/health`
- [ ] SQLite database initializes with correct schema
- [ ] Professor can register and login
- [ ] Survey CRUD endpoints work with auth
- [ ] Export skeleton returns correct JSON structure
- [ ] Docker image builds
- [ ] docker-compose.yml includes web-app service
- [ ] nginx proxies `/api` to web-app

## Completion Checklist

- [ ] Code uses ES modules (`import/export`)
- [ ] API responses use `{ success, data, error }` envelope
- [ ] Logging uses `[Module]` prefix pattern
- [ ] No hardcoded secrets (passwords hashed, token generated)
- [ ] Database enforces foreign keys and unique constraints
- [ ] SQLite uses WAL mode for concurrency
- [ ] .gitignore excludes node_modules and .db files

## Risks

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| better-sqlite3 native compilation fails in Docker | Low | High | Use `node:20-alpine` + apk add build-base if needed |
| In-memory sessions lost on restart | Medium | Low | Acceptable for classroom use; can add DB sessions later |
| Port conflicts with existing services | Low | Low | Configurable via env vars |

## Notes

- The `web-app/` directory is a **separate project** at the repo root, alongside `Server/` and `Assets/`. It has its own package.json, Dockerfile, and dependency tree.
- Phase 2 will add a React frontend (likely via Vite) inside `web-app/` or as a separate `web-app/client/` directory.
- The `db.js` module uses synchronous `better-sqlite3` calls intentionally — no callback/promise complexity for simple CRUD operations.
- Share codes (8-char hex) are generated per survey for student access links. Phase 3 will add a public `GET /api/public/surveys/:shareCode` endpoint that doesn't require auth.
