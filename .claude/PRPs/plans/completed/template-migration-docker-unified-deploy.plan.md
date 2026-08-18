# Plan: Template Migration + Docker Unified Deploy

## Summary
Make the EDI Survey Web App fully accessible through the unified Docker Compose deployment (single port 8080 entry point), and ensure the 3 survey templates are properly seeded as database records for extensibility. Currently the web app SPA is only accessible internally within the Docker network — this phase exposes it via nginx at `/survey/` path alongside the Unity WebGL game.

## User Story
As a professor, I want to run `docker compose up` once and have both the survey web app and Unity racing game accessible from the same host, so that I can configure surveys and run races without managing multiple services.

## Problem → Solution
- **Current**: Survey Web App port not exposed in docker-compose; SPA unreachable from outside Docker network; templates hardcoded in JS (not database-backed)
- **Desired**: Single `docker compose up` → nginx serves Unity WebGL at `/`, Survey Web App at `/survey/`, API at `/api/`, WebSocket at `/ws` — all on port 8080. Templates stored in DB for extensibility.

## Metadata
- **Complexity**: Medium
- **Source PRD**: `.claude/PRPs/prds/edi-survey-web-app.prd.md`
- **PRD Phase**: Phase 5 — 模板迁移 + Docker 统一部署
- **Estimated Files**: 7

---

## UX Design

### Before
```
┌──────────────────────────────────────────────┐
│  http://host:8080                            │
│  ├─ /     → Unity WebGL game                 │
│  ├─ /api  → Survey API (backend only)        │
│  ├─ /ws   → WebSocket relay                  │
│  └─ Survey SPA: NOT ACCESSIBLE!              │
│                                              │
│  Students cannot reach survey pages          │
│  Professor must manage services separately   │
└──────────────────────────────────────────────┘
```

### After
```
┌──────────────────────────────────────────────┐
│  http://host:8080                            │
│  ├─ /        → Unity WebGL game              │
│  ├─ /survey/ → Survey Web App (React SPA)    │
│  ├─ /api/    → Survey API (REST)             │
│  └─ /ws      → WebSocket relay               │
│                                              │
│  Student link: http://host:8080/survey/#/s/ABC123  │
│  Professor:    http://host:8080/survey/#/dashboard │
│  Game:         http://host:8080/                    │
└──────────────────────────────────────────────┘
```

### Interaction Changes
| Touchpoint | Before | After | Notes |
|---|---|---|---|
| Student survey link | Not accessible in Docker | `http://host:8080/survey/#/s/SHARECODE` | Works on mobile |
| Professor dashboard | Not accessible in Docker | `http://host:8080/survey/#/dashboard` | Same host as game |
| Unity game | `http://host:8080/` | `http://host:8080/` | Unchanged |
| API calls from SPA | `/api/*` → works internally | `/api/*` → works via nginx | Unchanged (absolute paths) |

---

## Mandatory Reading

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 (critical) | `Deploy/docker-compose.yml` | all | Current service definitions |
| P0 (critical) | `Deploy/nginx/nginx.conf` | all | Current routing rules |
| P0 (critical) | `web-app/client/vite.config.js` | all | Need to add `base` setting |
| P1 (important) | `web-app/Dockerfile` | all | Multi-stage build for SPA |
| P1 (important) | `web-app/src/routes/templates.js` | all | Current template implementation |
| P1 (important) | `web-app/src/db.js` | all | DB init pattern for seed |
| P2 (reference) | `web-app/src/schema.sql` | all | Current schema to extend |
| P2 (reference) | `Deploy/Dockerfile` | all | edi-racing container build |

## External Documentation

| Topic | Source | Key Takeaway |
|---|---|---|
| Vite base option | Vite docs | Set `base: '/survey/'` to prefix all asset URLs |
| nginx proxy_pass with trailing slash | nginx docs | `proxy_pass http://host/;` strips the matched location prefix |
| better-sqlite3 WAL mode | npm docs | Already configured; safe for concurrent reads |

---

## Patterns to Mirror

Code patterns discovered in the codebase. Follow these exactly.

### NAMING_CONVENTION
```js
// SOURCE: web-app/src/routes/surveys.js:1-5
import { Router } from 'express';
import { getDb } from '../db.js';
import { requireAuth } from '../middleware/auth.js';

const router = Router();
```

### ERROR_HANDLING
```js
// SOURCE: web-app/src/routes/responses.js:55-63
try {
  const result = db.prepare(
    'INSERT INTO responses ...'
  ).run(...);
  res.status(201).json({ success: true, data: { id: result.lastInsertRowid } });
} catch (err) {
  if (err.message.includes('UNIQUE constraint failed')) {
    return res.status(409).json({ success: false, error: 'Already exists' });
  }
  throw err;
}
```

### API_RESPONSE_FORMAT
```js
// SOURCE: web-app/src/routes/templates.js:70-72
router.get('/', (req, res) => {
  res.json({ success: true, data: templates });
});
```

### DB_INIT_PATTERN
```js
// SOURCE: web-app/src/db.js:11-23
export function getDb() {
  if (!db) {
    mkdirSync(dirname(DB_PATH), { recursive: true });
    db = new Database(DB_PATH);
    db.pragma('journal_mode = WAL');
    db.pragma('foreign_keys = ON');
    const schema = readFileSync(join(__dirname, 'schema.sql'), 'utf-8');
    db.exec(schema);
  }
  return db;
}
```

### DOCKER_COMPOSE_PATTERN
```yaml
# SOURCE: Deploy/docker-compose.yml:1-12
services:
  edi-racing:
    build:
      context: ..
      dockerfile: Deploy/Dockerfile
    ports:
      - "8080:80"
    depends_on:
      - web-app
    restart: unless-stopped
```

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `web-app/client/vite.config.js` | UPDATE | Add `base: '/survey/'` for correct asset paths when served under nginx sub-path |
| `Deploy/nginx/nginx.conf` | UPDATE | Add `location /survey/` proxy to web-app container |
| `web-app/src/schema.sql` | UPDATE | Add `templates` table for DB-backed templates |
| `web-app/src/db.js` | UPDATE | Add seed logic to insert default templates on first init |
| `web-app/src/routes/templates.js` | UPDATE | Read templates from DB instead of hardcoded array |
| `Deploy/docker-compose.yml` | UPDATE | Add healthcheck for web-app; ensure proper depends_on |
| `web-app/src/index.js` | UPDATE | Serve SPA with correct base path handling |

## NOT Building

- Admin UI for managing templates (templates are seeded via code; future feature)
- HTTPS/TLS termination (handled by external reverse proxy in production)
- Custom domain routing (out of scope; single host deployment)
- Student authentication (by design: email-only identification)
- Multiple server instances / load balancing (single machine target)

---

## Step-by-Step Tasks

### Task 1: Add templates table to schema
- **ACTION**: Add `templates` table to `schema.sql`
- **IMPLEMENT**: 
  ```sql
  CREATE TABLE IF NOT EXISTS templates (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT UNIQUE NOT NULL,
    description TEXT NOT NULL DEFAULT '',
    questions_json TEXT NOT NULL DEFAULT '[]',
    mappings_json TEXT NOT NULL DEFAULT '[]',
    rules_json TEXT NOT NULL DEFAULT '[]',
    created_at TEXT NOT NULL DEFAULT (datetime('now'))
  );
  ```
- **MIRROR**: DB_INIT_PATTERN — schema auto-applied on startup via `db.exec(schema)`
- **IMPORTS**: None (SQL file)
- **GOTCHA**: Table must use `IF NOT EXISTS` to be idempotent (same as other tables)
- **VALIDATE**: Start web-app, confirm no SQL errors, table exists

### Task 2: Add template seed logic to db.js
- **ACTION**: After schema init, seed default templates if table is empty
- **IMPLEMENT**: 
  ```js
  import { seedTemplates } from './seed-templates.js';
  // After db.exec(schema):
  const count = db.prepare('SELECT COUNT(*) as c FROM templates').get().c;
  if (count === 0) {
    seedTemplates(db);
    console.log('[DB] Seeded default templates');
  }
  ```
  Create `web-app/src/seed-templates.js` containing the 3 template definitions (moved from routes/templates.js) and a `seedTemplates(db)` function that inserts them.
- **MIRROR**: DB_INIT_PATTERN
- **IMPORTS**: `import { seedTemplates } from './seed-templates.js';`
- **GOTCHA**: Seed only when table is empty — prevents duplicates on restart
- **VALIDATE**: Delete `edi-survey.db`, restart → templates appear in DB

### Task 3: Update templates route to read from DB
- **ACTION**: Rewrite `routes/templates.js` to query DB instead of hardcoded array
- **IMPLEMENT**:
  ```js
  router.get('/', (req, res) => {
    const db = getDb();
    const rows = db.prepare('SELECT * FROM templates ORDER BY id ASC').all();
    const templates = rows.map(r => ({
      name: r.name,
      description: r.description,
      config: {
        questions: JSON.parse(r.questions_json),
        mappings: JSON.parse(r.mappings_json),
        rules: JSON.parse(r.rules_json),
      }
    }));
    res.json({ success: true, data: templates });
  });
  ```
- **MIRROR**: API_RESPONSE_FORMAT
- **IMPORTS**: `import { getDb } from '../db.js';`
- **GOTCHA**: Response shape must remain identical (`{ name, description, config: { questions, mappings, rules } }`) — frontend depends on this
- **VALIDATE**: `curl /api/templates` returns same structure as before

### Task 4: Set Vite base path to `/survey/`
- **ACTION**: Add `base: '/survey/'` to `vite.config.js`
- **IMPLEMENT**:
  ```js
  export default defineConfig({
    base: '/survey/',
    plugins: [react()],
    server: {
      port: 5173,
      proxy: {
        '/api': {
          target: 'http://localhost:3001',
          changeOrigin: true
        }
      }
    }
  })
  ```
- **MIRROR**: N/A (Vite config)
- **IMPORTS**: None
- **GOTCHA**: Dev mode (`npm run dev`) will now serve at `/survey/` too; dev proxy still works for `/api`
- **VALIDATE**: `npm run build` → check `dist/index.html` has `src="/survey/assets/..."` paths

### Task 5: Update Express SPA serving for base path
- **ACTION**: Modify `index.js` to serve SPA correctly under the `/survey/` base path when accessed directly (not through nginx)
- **IMPLEMENT**: No changes needed — Express serves `client/dist` at root, and Vite builds with `base: '/survey/'` which means the HTML references `/survey/assets/*`. When nginx proxies `/survey/` → `http://web-app:3001/`, Express serves index.html for any non-API path. The HTML then requests `/survey/assets/*` from the browser, which nginx proxies back correctly.
  
  However, the Express static middleware needs to serve assets at the `/survey/` prefix too for when nginx proxies `/survey/assets/foo.js` → `web-app:3001/assets/foo.js`. Since Express serves `client/dist` at root and dist contains `assets/` directory, this works automatically.
  
  **Actually needed**: Update the static serving to mount at root (already correct) — no change needed here.
- **MIRROR**: N/A
- **IMPORTS**: None
- **GOTCHA**: The SPA's `fetch('/api/...')` calls use absolute paths — these bypass the base path and go directly to `/api` which nginx handles. No change needed.
- **VALIDATE**: Access `http://web-app:3001/` → returns index.html; verify asset paths load

### Task 6: Add nginx location for Survey Web App
- **ACTION**: Add `location /survey/` block to `Deploy/nginx/nginx.conf`
- **IMPLEMENT**:
  ```nginx
  # Survey Web App (React SPA)
  location /survey/ {
      proxy_pass http://web-app:3001/;
      proxy_set_header Host $host;
      proxy_set_header X-Real-IP $remote_addr;
      proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
  }
  ```
  The trailing `/` on `proxy_pass` strips the `/survey/` prefix when forwarding to web-app. So `/survey/assets/main.js` → `http://web-app:3001/assets/main.js`.
- **MIRROR**: Existing `/api` location block pattern in nginx.conf
- **IMPORTS**: None
- **GOTCHA**: Must place BEFORE the SPA fallback `location /` block (nginx matches most specific first for prefix locations, so order matters less, but for clarity put it above). Also ensure the trailing slash is present on both the location and proxy_pass.
- **VALIDATE**: `docker compose up` → `curl http://localhost:8080/survey/` returns HTML with correct asset paths

### Task 7: Add healthcheck and finalize docker-compose
- **ACTION**: Add healthcheck to web-app service and ensure depends_on uses condition
- **IMPLEMENT**:
  ```yaml
  web-app:
    build:
      context: ../web-app
      dockerfile: Dockerfile
    volumes:
      - survey-data:/app/data
    environment:
      - API_PORT=3001
      - DB_PATH=/app/data/edi-survey.db
    healthcheck:
      test: ["CMD", "wget", "-q", "--spider", "http://localhost:3001/api/health"]
      interval: 10s
      timeout: 5s
      retries: 3
    restart: unless-stopped

  edi-racing:
    depends_on:
      web-app:
        condition: service_healthy
  ```
- **MIRROR**: DOCKER_COMPOSE_PATTERN
- **IMPORTS**: None
- **GOTCHA**: `node:20-alpine` doesn't have `curl` by default; use `wget` which is available in alpine
- **VALIDATE**: `docker compose up` → edi-racing waits for web-app health → all services start correctly

---

## Testing Strategy

### Integration Tests

| Test | Input | Expected Output | Edge Case? |
|---|---|---|---|
| Templates API from DB | `GET /api/templates` | 3 templates with same structure as before | No |
| Template seed idempotent | Restart server twice | Still exactly 3 templates (no duplicates) | Yes |
| SPA served at /survey/ | `GET http://host:8080/survey/` | HTML with `/survey/assets/` prefixed scripts | No |
| API via nginx | `GET http://host:8080/api/health` | `{ success: true }` | No |
| Student survey via nginx | `GET http://host:8080/survey/#/s/CODE` | SPA loads, renders survey | No |
| Unity WebGL at root | `GET http://host:8080/` | Unity loader HTML | No |
| WebSocket still works | Connect to `ws://host:8080/ws` | Connection accepted | No |

### Edge Cases Checklist
- [ ] Fresh deploy (no existing DB) → templates seeded correctly
- [ ] Existing DB (from Phase 1-4) → migration adds templates table, seeds templates
- [ ] Concurrent student submissions (SQLite WAL handles this)
- [ ] Large survey (50 responses) export still works
- [ ] Asset caching (nginx cache headers don't conflict with survey SPA)
- [ ] Browser back/forward with HashRouter at `/survey/` base

---

## Validation Commands

### Build Web App Client
```bash
cd web-app/client && npm run build
```
EXPECT: Build succeeds; `dist/index.html` contains `src="/survey/assets/..."` paths

### Lint Check
```bash
cd web-app/client && npm run lint
```
EXPECT: No errors

### Docker Build
```bash
cd Deploy && docker compose build
```
EXPECT: Both images build successfully

### Docker Start
```bash
cd Deploy && docker compose up -d
```
EXPECT: Both services healthy within 30s

### End-to-End Smoke Test
```bash
# Unity game
curl -s http://localhost:8080/ | grep -q "UnityLoader\|unity" && echo "PASS: Unity"

# Survey SPA
curl -s http://localhost:8080/survey/ | grep -q "EDI Survey" && echo "PASS: SPA"

# API health
curl -s http://localhost:8080/api/health | grep -q "ok" && echo "PASS: API"

# Templates endpoint
curl -s http://localhost:8080/api/templates | grep -q "V1 Parity" && echo "PASS: Templates"
```
EXPECT: All 4 PASS

### Manual Validation
- [ ] Open `http://localhost:8080/survey/#/login` → login page renders
- [ ] Register a new professor account → redirected to dashboard
- [ ] "Start from template" → "Accessibility" → survey created with pre-filled questions/mappings/rules
- [ ] Copy share code → open `http://localhost:8080/survey/#/s/SHARECODE` in incognito → student form loads
- [ ] Submit student response → professor dashboard shows response count
- [ ] Export JSON → verify `carData` and `eventRules` fields present
- [ ] Open `http://localhost:8080/` → Unity WebGL loads normally
- [ ] WebSocket connection in Unity still works for multiplayer

---

## Acceptance Criteria
- [ ] All tasks completed
- [ ] `docker compose up` starts all services successfully on fresh machine
- [ ] Survey SPA accessible at `/survey/` via nginx
- [ ] Templates loaded from database (seeded on first start)
- [ ] Unity WebGL still accessible at root `/`
- [ ] API endpoints work through nginx at `/api/`
- [ ] WebSocket relay still works at `/ws`
- [ ] Student can access survey via share link at `/survey/#/s/CODE`
- [ ] Professor full flow: login → create from template → share → export

## Completion Checklist
- [ ] Code follows discovered patterns (API response format, error handling)
- [ ] DB seed is idempotent (no duplicates on restart)
- [ ] nginx config handles all paths correctly
- [ ] Vite base path set so assets load through nginx sub-path
- [ ] No hardcoded URLs (all relative or config-driven)
- [ ] Docker compose starts cleanly with proper health dependencies
- [ ] No unnecessary scope additions

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| nginx proxy_pass path stripping incorrect | Low | High — SPA assets 404 | Test locally with `docker compose up` before merging |
| Vite base path breaks dev mode | Low | Medium — DX friction | Dev proxy still works for `/api`; SPA runs at `/survey/` in dev too |
| SQLite schema migration on existing DB | Low | Medium — first-run issue | `CREATE TABLE IF NOT EXISTS` is idempotent |
| Asset cache collision between Unity and SPA | Low | Low — different paths | Unity at `/`, SPA at `/survey/` — no overlap |

## Notes
- The templates are already mirrored from Unity's `SurveyTemplates.cs` to `web-app/src/routes/templates.js` (done in Phase 2). This phase moves them from hardcoded JS to database records.
- HashRouter in the React SPA means all client routing is hash-based (`#/login`, `#/s/CODE`). The browser only requests the base HTML page from the server — no server-side routing needed for SPA paths.
- The web-app container does NOT need its own exposed port since nginx proxies everything.
- Future improvement: add a `/survey` redirect (without trailing slash) → `/survey/` for convenience.
