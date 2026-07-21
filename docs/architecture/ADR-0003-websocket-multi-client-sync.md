# ADR-0003: WebSocket-Based Multi-Client Synchronization

## Status

Accepted

## Date

2025-02-13

## Last Verified

2025-02-13

## Decision Makers

Project lead (professor + developer)

## Summary

Multiple students need to observe the same race simultaneously in their browsers. WebSocket with deterministic simulation was chosen over Unity Netcode (requires relay) and full state streaming (too bandwidth-heavy), syncing only events rather than full game state.

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Networking |
| **Knowledge Risk** | LOW — WebSocket is a web standard, not engine-specific |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md` |
| **Post-Cutoff APIs Used** | None — uses native browser WebSocket, not Unity Netcode |
| **Verification Required** | Multiple browser clients receive synchronized race state |

## Context

### Problem Statement

The professor runs a race on the main screen while students watch on their devices. Students may also submit survey responses during setup. The synchronization system must work within WebGL's networking constraints (no UDP, no P2P).

### Constraints

- WebGL only supports WebSocket (no raw TCP/UDP)
- Campus network may have firewalls blocking non-standard ports
- Lightweight — students view on phones/laptops with varying bandwidth
- Server runs on professor's machine (not cloud-hosted)

## Decision

Use **WebSocket** for all client-server communication with **deterministic simulation**. The server (Node.js) manages sessions, relays events, and broadcasts state. Only events (race start, event triggers, results) are synchronized — not per-frame game state.

Architecture:
```
Professor (Unity WebGL) ←── WebSocket ──→ Node.js Server ←── WebSocket ──→ Student Browsers
                                          (Session mgmt)
                                          (State broadcast)
                                          (Event relay)
```

Key design: the professor's Unity client is authoritative. Students receive event broadcasts and render locally or view a simplified race viewer.

## Alternatives Considered

### Alternative 1: Unity Netcode for GameObjects

- **Pros**: Built-in state replication; lobby system
- **Cons**: Requires Unity Relay service (cloud dependency); complex setup for view-only clients; overkill for one-way broadcast
- **Rejection Reason**: Cloud dependency contradicts self-hosted requirement; too complex for event-only sync

### Alternative 2: Full State Streaming

- **Pros**: Perfect visual sync across all clients
- **Cons**: High bandwidth (car positions × 60 FPS × N clients); latency-sensitive; complex interpolation
- **Rejection Reason**: Bandwidth-heavy; unnecessary when events + deterministic sim suffice

## Consequences

### Positive

- Lightweight — only events transmitted, not per-frame state
- Works within WebGL constraints (WebSocket is browser-native)
- Self-hosted — no cloud dependencies
- Simple server (Node.js, single file)

### Negative

- Deterministic simulation required for visual consistency
- No true multiplayer (students observe, don't control cars)
- WebSocket reconnection handling needed for dropped connections

## Related

- [ADR-0002](ADR-0002-webgl-build-target.md) — WebGL constraint drives WebSocket choice
- [ADR-0004](ADR-0004-docker-deployment.md) — Node.js WS server runs in Docker
