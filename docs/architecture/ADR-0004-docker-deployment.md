# ADR-0004: Docker Compose Deployment

## Status

Accepted

## Date

2025-02-13

## Last Verified

2025-02-13

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

Use **Docker Compose** to orchestrate all services. The `Deploy/docker-compose.yml` defines:
- **nginx** (:80) — serves the Unity WebGL build as static files
- **Node.js** (:8080) — WebSocket server for real-time sync
- **Web App** (:3001) — Survey creation, response collection, export

SQLite database files persist via Docker volumes. The entire system starts with `docker compose up`.

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

- Single command deployment (`docker compose up`)
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
