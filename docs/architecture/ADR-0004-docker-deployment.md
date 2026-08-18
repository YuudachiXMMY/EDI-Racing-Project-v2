# ADR-0004: Docker Compose Deployment

## Status

Accepted

## Date

2025-02-13

## Last Verified

2026-07-28

## Decision Makers

Project lead (professor + developer)

## Summary

The professor needs to self-host the entire system (game + web app + server) on campus. Docker Compose was chosen to bundle nginx (serving WebGL build), Node.js (WebSocket + web app), and SQLite into a single reproducible deployment that works offline.

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Core (deployment) |
| **Knowledge Risk** | LOW — Docker is engine-independent |
| **References Consulted** | N/A |
| **Post-Cutoff APIs Used** | None |
| **Verification Required** | `docker compose up` starts all services and game is playable |

## Context

### Problem Statement

The system has three components: Unity WebGL game, Node.js WebSocket server, and the Survey Web App (React + Express + SQLite). The professor must deploy this on a campus machine with minimal DevOps knowledge. The system should work offline (no cloud dependencies).

### Constraints

- Professor is not a DevOps engineer — deployment must be simple
- Campus network may not have internet access during class
- Single machine deployment (no Kubernetes, no cloud)
- Must serve WebGL build + WebSocket server + Web App simultaneously

## Decision

Use **Docker Compose** to orchestrate all services behind a **single origin**. The
game container's nginx (:80) is the edge and routes everything:
- **`/`** — Unity WebGL build (static files, native Brotli)
- **`/ws`** — Node.js WebSocket relay (`Server/server.js`, in-container port **3000**, matched by `nginx.conf` and pinned via `ENV PORT=3000`)
- **`/survey`, `/api`** — Survey Web App (React + Express + SQLite, `web-app` service on :3001)

The **base** `Deploy/docker-compose.yml` is self-contained: `docker compose up --build`
(or `./Deploy/up.sh`) starts every component with **no external network** and exposes
one host port (`${GAME_PORT:-3900}`). Cross-container wiring is via env: the game
service sets `API_URL=http://web-app:3001` and a shared `INTERNAL_SECRET` so
`Server/server.js` can archive finished sessions into the web app; the web app sets
`WS_GAME_URL=ws://edi-racing-game:3000` to reach the game server. SQLite persists via
the `survey-data` Docker volume.

Production concerns (TLS termination, host-based routing) are layered on the identical
base via an optional overlay `Deploy/docker-compose.prod.yml` (Traefik + external
`proxy` network): `docker compose -f Deploy/docker-compose.yml -f Deploy/docker-compose.prod.yml up -d`.

## Alternatives Considered

### Alternative 1: GitHub Pages + Cloud Hosting

- **Pros**: No self-hosting burden; automatic HTTPS
- **Cons**: Requires internet; hosting costs; CORS complexity for WebSocket
- **Rejection Reason**: Campus may lack reliable internet; professor wants full control

### Alternative 2: Bare Metal Install

- **Pros**: No Docker dependency; potentially simpler
- **Cons**: Node.js version management; nginx config; OS-specific issues; harder to reproduce
- **Rejection Reason**: Reproducibility and portability more important than avoiding Docker

## Consequences

### Positive

- Single command deployment (`docker compose up --build` / `./Deploy/up.sh`) — no external infra
- Single origin: game, realtime, and survey app share one host/port (honors the clients' same-origin `/ws` + `/api`)
- Same base for local and production; production adds only a thin Traefik/TLS overlay
- Works offline once images are built
- Reproducible across machines
- Easy backup (copy Docker volumes)

### Negative

- Docker must be installed on the professor's machine
- WebGL build must be pre-built and placed in the deploy directory
- Container images can be large (but only built once)

## Related

- [ADR-0002](ADR-0002-webgl-build-target.md) — nginx serves the WebGL build
- [ADR-0003](ADR-0003-websocket-multi-client-sync.md) — Node.js WS server runs in Docker
- [ADR-0007](ADR-0007-web-app-stack-react-express-sqlite.md) — Web App also deployed via Docker
