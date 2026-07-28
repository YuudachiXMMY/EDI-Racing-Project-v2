# Plan: Unified Docker Compose — one command runs Game + WebSocket Server + Web App

## Summary
Today the three runtime pieces — the Unity **WebGL game** (static build), the **`Server/` WebSocket relay**, and the **`web-app/` survey API** — are started separately (native `npm start` in two terminals + a Caddy script, or a `docker-compose.yml` that only works if an external Traefik is already running). This plan makes **`docker compose up --build` bring up everything behind a single origin**, self-contained, matching the production self-hosting topology already committed in ADR-0004. The game container's existing nginx becomes the single-origin edge; a small env/wiring fix closes the cross-container session-archive gap; and production-only concerns (TLS, host routing) move to an optional override file.

> **This is a consolidation, not a greenfield build.** The single-origin `8080:80` nginx edge (`/`, `/survey/`, `/api`, `/ws`) was already designed and shipped in the completed plan `.claude/PRPs/plans/completed/template-migration-docker-unified-deploy.plan.md`. Subsequent commits (`bf0fdfd`/`1c0c7f3` Caddy local gateway + Traefik-based compose) **regressed the one-command local path** by making `Deploy/docker-compose.yml` depend on an external Traefik `proxy` network and splitting the stack into two host origins (`:3900`/`:3901`). This plan restores the self-contained single-origin behavior and adds the env wiring that was never completed.

## User Story
As the **professor / self-hosting operator**, I want to run **one command** to start the game, the realtime server, and the survey web app together, so that I don't have to launch three processes by hand and so my local run matches how it will run on the deployed server.

## Problem → Solution
**Current:** Game, `Server/`, and `web-app/` are launched independently (native processes + `Deploy/serve-local-https.sh` Caddy gateway), OR via `Deploy/docker-compose.yml` which **hard-requires an external Traefik `proxy` network** (`external: true`) and exposes the game and web-app as **two separate origins** (`:3900` / `:3901`) — which contradicts the browser clients' same-origin `/ws` + `/api` assumption and is not "one command."
**Desired:** `docker compose up --build` starts **all** components, self-contained (no external network required), reachable at **one origin** (e.g. `http://localhost:8080`) where `/` = game, `/ws` = realtime, `/survey` + `/api` = web app — with a documented **production override** that layers TLS/edge routing on the identical base.

## Metadata
- **Complexity**: Medium
- **Source PRD**: N/A (free-form request); related PRD `.claude/PRPs/prds/edi-survey-web-app.prd.md` Phase 5
- **PRD Phase**: N/A
- **Estimated Files**: ~8 (2 compose, 1 Dockerfile, 1 nginx.conf tweak, 1 new `.dockerignore`, 1 helper script, 1 env example, 1 ADR)

---

## UX Design

### Before
```
┌──────────────────────────────────────────────────────────┐
│ Terminal 1:  cd Server   && npm start      (WS :8080)     │
│ Terminal 2:  cd web-app  && npm start      (API :3001)    │
│ Terminal 3:  ./Deploy/serve-local-https.sh (Caddy edge)   │
│   ...or...  docker compose up  → FAILS unless external    │
│             Traefik `proxy` network already exists        │
│ Result: 3 manual steps, two separate origins, fragile     │
└──────────────────────────────────────────────────────────┘
```

### After
```
┌──────────────────────────────────────────────────────────┐
│  docker compose -f Deploy/docker-compose.yml up --build   │
│      (or: ./Deploy/up.sh)                                  │
│                                                            │
│   Browser → http://localhost:8080                         │
│     /            → Unity WebGL game (nginx static, Brotli) │
│     /ws          → WebSocket relay  (Server/server.js)     │
│     /survey /api → Survey web app   (web-app Express+SQLite)│
│                                                            │
│  Result: ONE command, ONE origin, no external deps         │
└──────────────────────────────────────────────────────────┘
```

### Interaction Changes
| Touchpoint | Before | After | Notes |
|---|---|---|---|
| Start everything | 3 terminals / 3 commands | `docker compose up --build` | Matches ADR-0004 intent |
| External infra | Traefik `proxy` net must pre-exist | none for local | Prod override re-adds edge |
| Origin(s) exposed | `:3900` game + `:3901` api | single `:8080` (nginx edge) | Honors same-origin `/ws` `/api` |
| Session archive (game→web-app) | silently fails in Docker | works via `API_URL`+`INTERNAL_SECRET` | Bug fix |
| Production parity | Caddy-native vs Traefik-compose diverge | same base + thin prod override | "similar to production" |

---

## Mandatory Reading

Files that MUST be read before implementing:

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 | `Deploy/docker-compose.yml` | 1-70 | The file to rewrite; current Traefik/2-origin shape |
| P0 | `Deploy/Dockerfile` | 1-32 | Game edge image (nginx + WS server + WebGL) |
| P0 | `Deploy/nginx/nginx.conf` | 1-100 | Single-origin routing already implemented (`/ws`, `/survey`, `/api`) |
| P0 | `Deploy/start.sh` | 1-7 | Game container entrypoint (WS bg + nginx fg) |
| P0 | `Server/server.js` | 4, 42, 74-82, 531-533 | `PORT` default 8080; `API_URL`/`INTERNAL_SECRET` archive POST |
| P0 | `web-app/Dockerfile` | 1-23 | web-app 2-stage build, `API_PORT`/`DB_PATH` env |
| P0 | `web-app/src/index.js` | 16, 41-58 | `API_PORT`, static `/survey`, `/api/*` mounts |
| P1 | `web-app/src/routes/game-status.js` | 3-4, 9-30 | `WS_GAME_URL` env; HTTP form via `replace(/^ws/,'http')` |
| P1 | `Server/package.json` | 6-8 | `start` uses `--env-file=.env` (native only; Docker runs `node` directly) |
| P1 | `web-app/package.json` | 6-13 | `better-sqlite3` native dep (Node major-version sensitive) |
| P2 | `Deploy/Caddyfile` | all | The native/local HTTPS edge (kept as dev alt; informs prod override) |
| P2 | `Deploy/serve-local-https.sh` | all | Ergonomics to mirror in `up.sh` |
| P2 | `.claude/PRPs/plans/completed/template-migration-docker-unified-deploy.plan.md` | all | The completed plan that BUILT the single-origin `8080:80` nginx edge — do not re-do; consolidate on it |
| P2 | `docs/architecture/ADR-0004-docker-deployment.md` | all | Deployment decision to update |
| P2 | `.dockerignore` | all | Root ignore (applies to game build context only) |

## External Documentation

| Topic | Source | Key Takeaway |
|---|---|---|
| Compose override files | https://docs.docker.com/compose/multiple-compose-files/ | `-f base -f prod` merges; keep local self-contained, prod adds edge/TLS |
| Compose `depends_on` + healthcheck | https://docs.docker.com/compose/how-tos/startup-order/ | `condition: service_healthy` gates game on web-app readiness (already used) |
| nginx `resolver` for runtime upstream | https://nginx.org/en/docs/http/ngx_http_core_module.html#resolver | Already used so nginx starts even if `web-app` is absent (single-container smoke) |
| better-sqlite3 native builds | https://github.com/WiseLibs/better-sqlite3 | Pin Node base image; prebuilt binaries tied to Node ABI (recent Node 26 build fix) |

> No new libraries introduced — all wiring uses existing images (`nginx:alpine`, `node:20-alpine`) and env vars already read by the code.

---

## Patterns to Mirror

### SINGLE_ORIGIN_ROUTING (the edge already exists — reuse it)
```nginx
// SOURCE: Deploy/nginx/nginx.conf:28-98
location /ws { proxy_pass http://127.0.0.1:3000; ... Upgrade/Connection headers ... }
resolver 127.0.0.11 valid=30s ipv6=off;      # Docker embedded DNS
set $survey_upstream web-app;                  # runtime-resolved → nginx boots without web-app
location /survey/ { proxy_pass http://$survey_upstream:3001; ... }
location /api     { proxy_pass http://$survey_upstream:3001; ... }
location /        { try_files $uri $uri/ /index.html; }   # WebGL SPA
```
→ The browser hits nginx (:80) as the ONE origin. WebGL + React both build `ws(s)://location.host/ws` and relative `/api` — this routing satisfies both. Do **not** invent a second gateway for local.

### COMPOSE_SINGLE_ORIGIN_PUBLISH (the shape the completed plan shipped — restore it)
```yaml
# SOURCE: .claude/PRPs/plans/completed/template-migration-docker-unified-deploy.plan.md:141-152
services:
  edi-racing:
    build: { context: .., dockerfile: Deploy/Dockerfile }
    ports: ["8080:80"]              # ONE origin published
    depends_on: { web-app: { condition: service_healthy } }
    restart: unless-stopped
```
→ This is the target base shape. The current file replaced it with Traefik labels + external network; the rewrite brings it back (parameterized `${GAME_PORT:-8080}`).

### IN_CONTAINER_WS_PORT (Docker convention = 3000, native = 8080)
```js
// SOURCE: Server/server.js:4
const PORT = parseInt(process.env.PORT || '8080', 10);
```
```nginx
// SOURCE: Deploy/nginx/nginx.conf:29  → proxy_pass http://127.0.0.1:3000;
```
→ nginx expects the WS server on **3000**, so the game container MUST run `server.js` with `PORT=3000`. Set it explicitly (compose env + Dockerfile `ENV` default) so the two never drift.

### SERVER_TO_WEBAPP_ARCHIVE (the gap to close)
```js
// SOURCE: Server/server.js:42,74-82
const API_URL = process.env.API_URL || 'http://localhost:3001';
const INTERNAL_SECRET = process.env.INTERNAL_SECRET || 'edi-internal-default';
fetch(`${API_URL}/api/sessions/archive`, {
  method: 'POST',
  headers: { 'Content-Type': 'application/json', 'x-internal-secret': INTERNAL_SECRET },
  body: JSON.stringify(archivePayload),
}).catch(() => {});
```
→ In Docker the web-app is a **separate container**, so `API_URL` must be `http://web-app:3001` and `INTERNAL_SECRET` must **match** the web-app's value. Currently unset on the game service → archive silently drops.

### WEBAPP_TO_SERVER_LINK (already env-driven, keep consistent)
```js
// SOURCE: web-app/src/routes/game-status.js:3-4
const WS_GAME_URL = process.env.WS_GAME_URL || 'ws://localhost:8080';
const GAME_HTTP_URL = WS_GAME_URL.replace(/^ws/, 'http'); // same host:port for WS + HTTP
```
→ Set `WS_GAME_URL=ws://edi-racing-game:3000`; the derived HTTP `http://edi-racing-game:3000` hits `server.js` HTTP endpoints on the **same** port (server.js serves WS+HTTP on one port). Consistent with `PORT=3000`.

### COMPOSE_HEALTHCHECK_GATE
```yaml
// SOURCE: Deploy/docker-compose.yml:17-25,48-53
healthcheck: { test: ["CMD","wget","-q","--spider","http://localhost:3001/api/health"], interval: 10s, ... }
depends_on: { web-app: { condition: service_healthy } }
```
→ Reuse verbatim. web-app `/api/health` (`index.js:37`) and game `:80` are the probes. Note: `node:20-alpine` has `wget`, not `curl`.

### TWO_STAGE_NODE_BUILD (web-app image — do not change stack)
```dockerfile
// SOURCE: web-app/Dockerfile:1-23
FROM node:20-alpine AS client-build ... RUN npm run build
FROM node:20-alpine ... COPY --from=client-build /app/client/dist ./client/dist
ENV API_PORT=3001 ; ENV DB_PATH=/app/data/edi-survey.db ; CMD ["node","src/index.js"]
```
→ Keep `node:20-alpine` (pinned for `better-sqlite3` ABI). SQLite persists to `/app/data` volume.

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `Deploy/docker-compose.yml` | UPDATE | Make self-contained: publish nginx edge as single origin (`${GAME_PORT:-8080}:80`); drop hard external-Traefik dep; wire `API_URL`/`INTERNAL_SECRET`/`PORT`/`WS_GAME_URL` env |
| `Deploy/docker-compose.prod.yml` | CREATE | Optional override adding TLS/edge (Traefik labels + external `proxy` net) — "similar to production" without breaking local |
| `Deploy/Dockerfile` | UPDATE | Add `ENV PORT=3000` default so nginx↔WS agree even without compose env |
| `Deploy/nginx/nginx.conf` | REVIEW | Confirm `/ws`→3000 and `/survey`/`/api`→`web-app:3001` unaffected; no behavioral change expected |
| `web-app/.dockerignore` | CREATE | Exclude `node_modules/`, `data/` (live SQLite db!), `.env`, `.DS_Store` from build context |
| `Deploy/up.sh` | CREATE | One-command wrapper: pre-check `webgl-build/`, then `docker compose up --build` (mirror `serve-local-https.sh` ergonomics) |
| `Deploy/.env.example` | UPDATE | Document required keys: `GAME_PORT`, `API_PORT`, `INTERNAL_SECRET`, `GAME_DOMAIN`/`API_DOMAIN` (prod) |
| `docs/architecture/ADR-0004-docker-deployment.md` | UPDATE | Record unified one-command topology + prod override; note single-origin nginx edge |

## NOT Building

- **A brand-new reverse proxy (Caddy/Traefik) as the default local edge** — the game container's nginx is already a complete single-origin edge; adding another is redundant. Traefik is reserved for the **prod override only**.
- **A literal single-process container running all 3** (nginx + WS + Express + SQLite in one image). Rejected: mixes `better-sqlite3` native build into `nginx:alpine`, runs 3 unmanaged processes, and diverges from ADR-0004's compose model. (Documented as an alternative below, not implemented.)
- **Changing the WebSocket/API protocols, room logic, DB schema, or client code** — clients are already same-origin-correct; no code changes to `server.js`, `web-app/src/**`, or Unity.
- **Building the Unity WebGL output** — `Deploy/webgl-build/` is a precondition (Dockerfile fails fast if missing); this plan does not run Unity.
- **Retiring the native Caddy dev loop** (`serve-local-https.sh`) — kept as an HTTPS dev alternative and as the basis for the prod TLS override.
- **Template DB seeding / SPA base-path work** — already delivered by the completed template-migration plan; not repeated here.
- **CI/CD pipeline changes** — out of scope; local + server-deploy parity only.

---

## Step-by-Step Tasks

### Task 1: Pin the in-container WS port so nginx and server.js never drift
- **ACTION**: Add a default `PORT=3000` to the game image.
- **IMPLEMENT**: In `Deploy/Dockerfile`, after the server copy and before `CMD`, add `ENV PORT=3000`. This guarantees `server.js` listens on 3000 to match `nginx.conf:29` even if compose env is absent.
- **MIRROR**: `IN_CONTAINER_WS_PORT`.
- **IMPORTS**: none.
- **GOTCHA**: `server.js` default is 8080; nginx proxies to 3000. Without this env the single-container smoke test (nginx up, web-app absent) still serves static but `/ws` 502s. Setting `ENV PORT=3000` fixes the default.
- **VALIDATE**: `docker build -f Deploy/Dockerfile -t edi-game .` succeeds; `docker run --rm edi-game env | grep PORT=3000`.

### Task 2: Rewrite `Deploy/docker-compose.yml` to be self-contained and single-origin
- **ACTION**: Make `docker compose up --build` work with zero external prerequisites and expose ONE origin.
- **IMPLEMENT**:
  - Keep two services: `edi-racing` (game edge) and `web-app`.
  - `edi-racing`: `build.context: ..`, `dockerfile: Deploy/Dockerfile`; publish the nginx edge — `ports: ["${GAME_PORT:-8080}:80"]`; env `PORT=3000`, `API_URL=http://web-app:3001`, `INTERNAL_SECRET=${INTERNAL_SECRET:-edi-internal-default}`; `depends_on: web-app: {condition: service_healthy}`; keep the `:80` healthcheck; `restart: unless-stopped`.
  - `web-app`: `build.context: ../web-app`; env `API_PORT=3001`, `DB_PATH=/app/data/edi-survey.db`, `WS_GAME_URL=ws://edi-racing-game:3000`, `INTERNAL_SECRET=${INTERNAL_SECRET:-edi-internal-default}`; `volumes: [survey-data:/app/data]`; keep `/api/health` healthcheck. **Do not publish** web-app to the host by default (reached via nginx `/survey` `/api`) — or publish only behind a commented debug line.
  - **Remove** the `proxy` external network and all `traefik.*` labels from this base file. Keep one internal `edi-racing-net` bridge so `edi-racing` can resolve `web-app` by name (nginx `resolver 127.0.0.11` uses Docker DNS).
- **MIRROR**: `COMPOSE_SINGLE_ORIGIN_PUBLISH`, `SINGLE_ORIGIN_ROUTING`, `SERVER_TO_WEBAPP_ARCHIVE`, `WEBAPP_TO_SERVER_LINK`, `COMPOSE_HEALTHCHECK_GATE`.
- **IMPORTS**: none.
- **GOTCHA**: `nginx.conf:77` hardcodes upstream host `web-app`. Compose resolves a **service** named `web-app` on the shared network — the `container_name: edi-racing-api` does NOT change the DNS service name, so `web-app` still resolves. Keep the service key literally `web-app`. Also: `WS_GAME_URL` host `edi-racing-game` must resolve on the shared net — the service key is `edi-racing` but `container_name: edi-racing-game`; add a network alias `edi-racing-game` to the `edi-racing` service (or set `WS_GAME_URL=ws://edi-racing:3000`). Prefer the alias to avoid churn.
- **VALIDATE**: `docker compose -f Deploy/docker-compose.yml config` prints merged config with no `external: true`; `docker compose up --build` reaches `healthy` on both without any pre-created network.

### Task 3: Create `web-app/.dockerignore`
- **ACTION**: Prevent the live SQLite DB, `node_modules`, and secrets from entering the web-app build context.
- **IMPLEMENT**: New `web-app/.dockerignore` with: `node_modules/`, `client/node_modules/`, `data/`, `*.db`, `.env`, `.DS_Store`, `__tests__/`, `client/dist/` (rebuilt in stage 1).
- **MIRROR**: root `.dockerignore` style (`.dockerignore:1-26`).
- **IMPORTS**: none.
- **GOTCHA**: web-app build context is `../web-app`; the **root** `.dockerignore` does NOT apply to it. Without this file, `data/edi-survey.db` (a live DB) is shipped into the build context — bloating builds and risking a stale DB baked into a layer if a future `COPY . .` is added.
- **VALIDATE**: `docker compose build web-app` context transfer size drops; runtime `/app/data` shows only the volume-mounted DB.

### Task 4: Create the production override `Deploy/docker-compose.prod.yml`
- **ACTION**: Re-add the external edge (Traefik + TLS + host routing) as an **opt-in overlay** so the base stays one-command-local.
- **IMPLEMENT**: An override that adds to `edi-racing` the `traefik.*` labels (host rule `${GAME_DOMAIN:-edi-racing.localhost}` → port 80) and attaches the external `proxy` network; declare `networks: proxy: {external: true}`. Optionally drop the host `ports:` publish in prod (edge handles ingress). Usage: `docker compose -f Deploy/docker-compose.yml -f Deploy/docker-compose.prod.yml up -d`.
- **MIRROR**: the Traefik labels currently in `Deploy/docker-compose.yml:27-33,54-57` (move them here).
- **IMPORTS**: none.
- **GOTCHA**: Because clients are same-origin, the **game host** (`edi-racing.localhost`) must serve `/ws` `/api` `/survey` too — Traefik routes the ONE host to the nginx edge, which fans out internally. Do NOT expect the browser to talk to `api.edi-racing.localhost` directly (React uses relative `/api`). The separate `api.*` host is optional/debug-only.
- **VALIDATE**: With an external `proxy` network + Traefik running, `docker compose -f ... -f prod up -d` registers the router; `curl -H 'Host: edi-racing.localhost' http://<edge>/api/health` returns ok.

### Task 5: Add `Deploy/up.sh` one-command wrapper
- **ACTION**: Provide an ergonomic single entrypoint mirroring `serve-local-https.sh`.
- **IMPLEMENT**: `Deploy/up.sh` (chmod +x): `set -e`; resolve `SCRIPT_DIR`; verify `./webgl-build/Build` exists (else print "run Unity WebGL build first" and exit 1, matching `serve-local-https.sh:16-18`); then `exec docker compose -f "$SCRIPT_DIR/docker-compose.yml" up --build "$@"`. Echo the single URL `http://localhost:${GAME_PORT:-8080}`.
- **MIRROR**: `Deploy/serve-local-https.sh:9-18,35-36`.
- **IMPORTS**: none.
- **GOTCHA**: `webgl-build/` is deploy-provided; the Dockerfile already fails fast (`Deploy/Dockerfile:18-23`) but a friendly pre-check avoids a confusing build error.
- **VALIDATE**: `./Deploy/up.sh` from a clean checkout (with `webgl-build/` present, no external network) brings the stack up; `Ctrl+C` stops it.

### Task 6: Update `Deploy/.env.example` (and document `.env`)
- **ACTION**: Document every knob the compose files read.
- **IMPLEMENT**: Ensure `Deploy/.env.example` lists: `GAME_PORT=8080`, `API_PORT=3901` (debug publish, optional), `INTERNAL_SECRET=<change-me>`, `GAME_DOMAIN=edi-racing.localhost`, `API_DOMAIN=api.edi-racing.localhost` (prod override). Add a comment that `INTERNAL_SECRET` must be identical for game + web-app (archive auth).
- **MIRROR**: existing `Deploy/.env.example`.
- **IMPORTS**: none.
- **GOTCHA**: `Deploy/.env` is gitignored and blocked by permission settings — do NOT read/commit it; only maintain `.env.example` and instruct the operator to `cp .env.example .env`.
- **VALIDATE**: `docker compose --env-file Deploy/.env config` resolves all `${...}` without warnings.

### Task 7: Update ADR-0004 to record the unified topology
- **ACTION**: Reflect the one-command, single-origin, base+prod-override decision.
- **IMPLEMENT**: In `docs/architecture/ADR-0004-docker-deployment.md`, update **Decision** and **Consequences**: nginx edge = single origin (`/`, `/ws`, `/survey`, `/api`); base compose is self-contained (no external network); production layered via `docker-compose.prod.yml` (Traefik/TLS). Bump **Last Verified**. Note the `API_URL`/`INTERNAL_SECRET` wiring requirement.
- **MIRROR**: ADR section structure already in the file (`ADR-0004:47-84`).
- **IMPORTS**: none.
- **GOTCHA**: Per `docs/CLAUDE.md`, keep Status `Accepted`; do not renumber. Reference ADR-0002/0003/0007 as already listed.
- **VALIDATE**: Optionally run `/architecture-review`; at minimum the ADR reads consistently with the new compose files.

---

## Testing Strategy

> This is infrastructure/config work — validation is behavioral (bring the stack up and exercise the chain), not unit tests. Existing `web-app/__tests__/*` (vitest) and `Assets/Tests` remain untouched and must still pass.

### Behavioral checks

| Check | Command / Action | Expected |
|---|---|---|
| One-command up | `./Deploy/up.sh` (or `docker compose -f Deploy/docker-compose.yml up --build`) | Both services reach `healthy`; no external network error |
| Single origin — game | open `http://localhost:8080/` | Unity WebGL loads (Brotli assets, correct MIME) |
| Single origin — survey | open `http://localhost:8080/survey/` | React survey app loads |
| Same-origin API | `curl http://localhost:8080/api/health` | `{"success":true,...}` (proxied to web-app) |
| Same-origin WS | devtools: WebGL/React opens `ws://localhost:8080/ws` | 101 Switching Protocols; room create/join works |
| Cross-service room status | `curl http://localhost:8080/api/game/room-status/<code>` | web-app→game proxy returns room state (not "unreachable") |
| Session archive (the fix) | create room in game, finish/destroy room, then query web-app history/results | archived session appears (proves `API_URL`+`INTERNAL_SECRET` wired) |
| DB persistence | `docker compose down` then `up`; check survey/history data | data survives (volume `survey-data`) |
| Self-contained | run on a machine with NO `proxy` network | still comes up (base file has no `external: true`) |
| Prod override | `docker compose -f docker-compose.yml -f docker-compose.prod.yml config` | merges; Traefik labels + external net present |

### Edge Cases Checklist
- [ ] `webgl-build/` missing → `up.sh` pre-check (and Dockerfile) fail fast with a clear message
- [ ] `web-app` slow to start → game `depends_on service_healthy` waits; nginx `resolver` keeps nginx up regardless
- [ ] `web-app` container absent (single-container smoke) → `/` still serves; `/survey` `/api` 502 gracefully (not a boot failure)
- [ ] `INTERNAL_SECRET` mismatch → archive rejected (document the shared-secret requirement)
- [ ] Port 8080 already in use → override `GAME_PORT` in `.env`
- [ ] `better-sqlite3` ABI vs Node base image → keep `node:20-alpine` pin; rebuild on Node bump
- [ ] Repeated `up`/`down` → volume `survey-data` persists; no data loss

---

## Validation Commands

### Static Analysis
```bash
# Validate compose syntax + variable resolution (base and merged prod)
docker compose -f Deploy/docker-compose.yml config
docker compose -f Deploy/docker-compose.yml -f Deploy/docker-compose.prod.yml config
```
EXPECT: Valid merged config; base has NO `external: true`; prod adds it.

### Build
```bash
docker compose -f Deploy/docker-compose.yml build
```
EXPECT: Both images build; web-app context excludes `data/`/`node_modules` (via new `.dockerignore`).

### Full Stack Up (behavioral)
```bash
./Deploy/up.sh            # or: docker compose -f Deploy/docker-compose.yml up --build -d
docker compose -f Deploy/docker-compose.yml ps   # both 'healthy'
curl -fsS http://localhost:8080/api/health       # {"success":true,...}
curl -fsSI http://localhost:8080/                 # 200, serves index.html
```
EXPECT: Both healthy; health + root reachable at the single origin.

### Existing test suites (no regression)
```bash
cd web-app && npm test        # vitest — auth/db tests unaffected
```
EXPECT: All pass (config-only change; no source edits to web-app).

### Manual Validation
- [ ] `http://localhost:8080/` → game plays; create room; note code
- [ ] `http://localhost:8080/survey/` → build a survey; submit a response; verify game gets `new_web_response`
- [ ] Finish/destroy the room → survey web app shows the archived session (archive path works)
- [ ] `docker compose down && ./Deploy/up.sh` → survey/history data persists

---

## Acceptance Criteria
- [ ] `docker compose -f Deploy/docker-compose.yml up --build` starts game + WS server + web-app with **no external network / no pre-created infra**
- [ ] All three are reachable at a **single origin** (`/`, `/ws`, `/survey`, `/api`)
- [ ] Cross-container **session archive works** (game→web-app via `API_URL` + matching `INTERNAL_SECRET`)
- [ ] web-app→game **room-status proxy works** (`WS_GAME_URL` set)
- [ ] SQLite data **persists** across `down`/`up`
- [ ] A documented **prod override** re-adds Traefik/TLS without editing the base
- [ ] `web-app/.dockerignore` prevents the live DB from entering the build context
- [ ] ADR-0004 updated; `docker compose config` clean

## Completion Checklist
- [ ] Reuses the existing nginx single-origin edge (no redundant proxy) — matches `nginx.conf` patterns
- [ ] Env wiring uses the **exact** var names the code reads (`PORT`, `API_URL`, `INTERNAL_SECRET`, `WS_GAME_URL`, `API_PORT`, `DB_PATH`)
- [ ] No source/protocol/schema changes to `server.js`, `web-app/src/**`, or Unity
- [ ] `node:20-alpine` pin preserved for `better-sqlite3`
- [ ] Helper script mirrors `serve-local-https.sh` ergonomics
- [ ] No secrets committed; only `.env.example` maintained
- [ ] Self-contained — implementable from this plan without further codebase search

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| nginx upstream name (`web-app`) / game alias (`edi-racing-game`) mismatch on the compose network | Medium | Stack up but `/api`/archive fail | Keep service key `web-app`; add network alias `edi-racing-game` (or set `WS_GAME_URL=ws://edi-racing:3000`); verify with `docker compose config` + curl |
| `INTERNAL_SECRET` differs between services | Medium | Archive silently rejected | Single `${INTERNAL_SECRET}` var applied to both services; documented in `.env.example` |
| `better-sqlite3` native build breaks on base-image bump | Low | web-app build fails | Pin `node:20-alpine`; recent Node-26 build fix shows sensitivity — do not bump casually |
| Operator expects literally one container | Low | Confusion vs request wording | Plan delivers one **command**; ADR-0004 already commits to compose; single-container variant documented as rejected alternative |
| Brotli MIME/encoding regressions when served over plain HTTP | Low | WebGL fails to load | nginx already sets `Content-Encoding: br` + MIME per extension (`nginx.conf:40-58`); Brotli works over HTTP |
| Removing Traefik labels breaks an existing prod deploy | Low | Prod ingress lost | Labels preserved in `docker-compose.prod.yml`; deploy docs updated to use base+prod |

## Notes
- **Why compose, not one container:** ADR-0004 already commits to Docker Compose for this self-hosted, offline-capable, single-machine deployment. Two services cleanly isolate the `better-sqlite3` native build and the SQLite volume from the `nginx:alpine` edge, and mirror how the campus server runs. The user's real need — "don't start them separately, like production" — is satisfied by **one command**.
- **This reverses a regression, not a new design:** the completed plan `completed/template-migration-docker-unified-deploy.plan.md` already established `ports: "8080:80"` + single-origin nginx routing. Later commits (`bf0fdfd`, `1c0c7f3`) introduced the Caddy local gateway and a Traefik-based compose that require external infra — good for a production Traefik host, but they broke `docker compose up` for the plain local case. This plan re-establishes the self-contained base and demotes Traefik to an override.
- **Single origin is mandatory, not cosmetic:** both browser clients build `ws(s)://location.host/ws` and relative `/api` (Unity `Assets/Plugins/WebGL/WebSocketBridge.jslib:5-12`; React `web-app/client/src/hooks/useRaceWebSocket.js:18-21`, `web-app/client/src/api.js:24`). The current 2-origin compose (`:3900`/`:3901`) only works if a host-routing edge collapses them back to one host — so the nginx edge (or Traefik→nginx in prod) is the correct shape.
- **Port convention:** the Docker path standardizes the WS server on **3000** (nginx upstream); the native/Caddy dev path uses **8080**. This plan makes `PORT=3000` explicit in the game image so the two never silently diverge. The native 8080 path (`serve-local-https.sh`, `Caddyfile`, `WS_GAME_URL` default) is left intact for HTTPS dev.
- **HTTPS locally (optional, out of scope):** WSS/native-Brotli-over-HTTPS is already handled by the Caddy dev loop; if desired inside compose, add Caddy `local_certs` (or Traefik TLS) in the prod override — plain HTTP + WS is sufficient for the one-command local goal.
- Permission-restricted files not read (defaults sourced from code fallbacks): `Deploy/.env`, `web-app/.env(.example)`, `Server/.env.example`.
