# ADR-0007: Web App Stack — React + Express + SQLite + SurveyJS

## Status

Accepted

## Date

2025-04-01

## Last Verified

2025-04-01

## Decision Makers

Project lead (professor + developer)

## Summary

A web-based survey tool was needed to replace the external MS Forms + DataTool.py workflow. React (frontend) + Express (backend) + SQLite (database) + SurveyJS (survey rendering) were chosen to unify the tech stack with the existing Node.js WebSocket server and provide zero-config database deployment.

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Core (external tooling) |
| **Knowledge Risk** | LOW — web stack is engine-independent |
| **References Consulted** | N/A |
| **Post-Cutoff APIs Used** | None |
| **Verification Required** | Web app creates surveys, collects responses, exports to Unity format |

## Context

### Problem Statement

Professors used MS Forms externally, then ran DataTool.py to transform data, then manually imported CSV into Unity. This 3-tool workflow is error-prone and requires technical knowledge. A unified web app should handle survey creation, student response collection, and data export in Unity-compatible format.

### Constraints

- Must integrate with existing Node.js WebSocket server (same tech stack preferred)
- SQLite for zero-config deployment in Docker
- Professor authentication needed; students access without login
- Survey question format must match Unity's `SurveyConfig` JSON structure
- SurveyJS chosen for mature survey rendering with JSON-native question definitions (MIT license)

## Decision

Build the **EDI Survey Web App** with:
- **Frontend**: React + SurveyJS Creator (professor) + SurveyJS (student)
- **Backend**: Express.js REST API
- **Database**: SQLite (better-sqlite3) — zero-config, file-based
- **Auth**: JWT for professor; anonymous access for students via share link
- **Data Format**: JSON matching Unity's SurveyConfig structure 1:1

The app produces JSON exports that Unity's `JsonImporter` can consume directly. The `SurveyResponseMapper` logic (originally C# in Unity) is replicated in JavaScript on the server for the "Send to Game" feature.

## Alternatives Considered

### Alternative 1: Vue.js / Angular

- **Pros**: Alternative frontend frameworks with good ecosystems
- **Cons**: Less SurveyJS ecosystem support; team unfamiliar
- **Rejection Reason**: SurveyJS has mature React components; React is more widely known

### Alternative 2: Python FastAPI

- **Pros**: Strong data processing (pandas for DataTool.py logic)
- **Cons**: Different tech stack from existing Node.js server; two runtimes in Docker
- **Rejection Reason**: Unified Node.js stack is simpler to deploy and maintain

### Alternative 3: MongoDB / MySQL

- **Pros**: More scalable; better concurrent access
- **Cons**: Requires separate server process; configuration overhead
- **Rejection Reason**: SQLite is zero-config; project serves one professor at a time; no scalability needs

## Consequences

### Positive

- Unified Node.js stack (WebSocket server + Web App)
- Zero-config database (SQLite file in Docker volume)
- SurveyJS handles complex survey UIs (multi-select, validation, theming)
- JSON data format 1:1 with Unity's SurveyConfig

### Negative

- SQLite limits concurrent writes (acceptable for single-professor use)
- SurveyJS community edition has fewer features than paid version
- JavaScript reimplementation of C# mapping logic must be kept in sync

## Related

- [ADR-0004](ADR-0004-docker-deployment.md) — Web app deployed via Docker Compose
- [ADR-0005](ADR-0005-dynamic-attribute-model.md) — JSON export matches dynamic attribute model
