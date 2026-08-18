# Implementation Report: Unified Docker Compose (one command runs Game + Server + Web App)

## Summary
Consolidated the three runtime pieces (Unity WebGL game, `Server/` WebSocket relay, `web-app/` survey API) behind the game container's existing single-origin nginx edge so that **`docker compose up --build` (or `./Deploy/up.sh`) starts everything self-contained**, with no external Traefik network required. Closed the cross-container session-archive env gap and demoted Traefik/TLS to an optional production overlay. **No runtime source code changed — configuration only.**

## Assessment vs Reality

| Metric | Predicted (Plan) | Actual |
|---|---|---|
| Complexity | Medium | Medium (as predicted) |
| Confidence | 8/10 | 8/10 — held; one env-example write blocked by permissions |
| Files Changed | ~8 | 6 code/config + 1 report + plan archived |

## Tasks Completed

| # | Task | Status | Notes |
|---|---|---|---|
| 1 | Pin `ENV PORT=3000` in game Dockerfile | ✅ Complete | nginx upstream (3000) ↔ `server.js` never drift |
| 2 | Rewrite `docker-compose.yml` self-contained/single-origin | ✅ Complete | `${GAME_PORT:-8080}:80`; Traefik + external net removed; `API_URL`/`INTERNAL_SECRET` wired; `edi-racing-game` network alias added |
| 3 | Create `web-app/.dockerignore` | ✅ Complete | Excludes live SQLite DB, node_modules, secrets |
| 4 | Create `docker-compose.prod.yml` overlay | ✅ Complete | Traefik labels + external `proxy` net; merge validated |
| 5 | Create `Deploy/up.sh` wrapper | ✅ Complete | `chmod +x`; webgl-build precheck; bash syntax OK |
| 6 | Update `Deploy/.env.example` | ⚠️ Deviated | **WRITE DENIED by permission settings** — env now documented in compose comments + `up.sh` header + ADR instead |
| 7 | Update ADR-0004 | ✅ Complete | Decision rewritten (single-origin edge, base+prod overlay); Last Verified → 2026-07-28; Positive consequences expanded |

## Validation Results

| Level | Status | Notes |
|---|---|---|
| Static Analysis | ✅ Pass | `docker compose -f Deploy/docker-compose.yml config` → **BASE_CONFIG_OK** |
| Prod overlay merge | ✅ Pass | `-f base -f prod config` → **PROD_MERGE_OK** |
| Script syntax | ✅ Pass | `bash -n Deploy/up.sh` → **UPSH_SYNTAX_OK** |
| Build (game image) | ⚠️ N/A | `Deploy/webgl-build/Build` is git-untracked and absent in this fresh worktree — a documented precondition; the game image's fail-fast artifact check cannot pass without a Unity WebGL build |
| Build (web-app image) | ⏭️ Skipped | web-app `Dockerfile` unchanged; new `.dockerignore` only excludes files the Dockerfile never `COPY`s — near-zero risk |
| Integration / runtime `up` | ⏭️ Not run | Requires `webgl-build/` present + Docker daemon build; run on a machine with the WebGL build (see Manual Validation in the plan) |
| Unit tests (`web-app`) | ⏭️ Untouched | No `web-app/src/**` changes; existing vitest suite unaffected |

## Files Changed

| File | Action | Notes |
|---|---|---|
| `Deploy/Dockerfile` | UPDATED | +5 (`ENV PORT=3000` + comment) |
| `Deploy/docker-compose.yml` | REWRITTEN | self-contained single-origin (78 lines) |
| `Deploy/docker-compose.prod.yml` | CREATED | +34 (Traefik/TLS overlay) |
| `Deploy/up.sh` | CREATED | +20, executable |
| `web-app/.dockerignore` | CREATED | +13 |
| `docs/architecture/ADR-0004-docker-deployment.md` | UPDATED | Decision + Consequences + date |

## Deviations from Plan
- **Task 6 — `Deploy/.env.example` not updated (WRITE DENIED by permission settings).** The `.env*` path is blocked for both Read and Write in this environment (not the GateGuard hook). Mitigation: every var the compose files read has an inline `${VAR:-default}` fallback, so a missing/stale `.env` still runs with sane defaults (`GAME_PORT=8080`, `INTERNAL_SECRET=edi-internal-default`, etc.), and the variables are documented in `docker-compose.yml` comments, the `up.sh` header, and ADR-0004.
  - **Action for the operator:** if a `Deploy/.env` already exists with the old `GAME_PORT=3900`, update it to `8080` (or delete it to use the default). Recommended real keys: `GAME_PORT`, `API_PORT`, `INTERNAL_SECRET`, `GAME_DOMAIN`, `API_DOMAIN`.

## Issues Encountered
- **Fresh worktree lacks `webgl-build/`** (git-untracked build artifact) → full `docker compose up --build` of the game image can't be exercised here. Config-level validation used as the strongest available gate; runtime smoke test deferred to a machine with the Unity WebGL build.
- **GateGuard fact-force hook** fired on the direct Dockerfile edit and report write; satisfied with the 4 required facts and retried successfully.

## Tests Written
None — infrastructure/config change with no new runtime logic. Validation is behavioral (compose config + runtime `up`), per the plan's Testing Strategy.

## Next Steps
- [ ] On a machine with `Deploy/webgl-build/` present: `./Deploy/up.sh` then verify `http://localhost:8080/` (game), `/survey/`, `curl /api/health`, and that a finished race archives into the web app (proves `API_URL`+`INTERNAL_SECRET`).
- [ ] Reconcile any existing `Deploy/.env` with the new defaults (`GAME_PORT=8080`).
- [ ] Optional: `/code-review`.
