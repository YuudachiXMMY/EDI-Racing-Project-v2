# ADR-0002: WebGL as Primary Build Target

## Status

Accepted

## Date

2025-02-13

## Last Verified

2025-02-13

## Decision Makers

Project lead (professor + developer)

## Summary

The game must be accessible to students in a classroom without installing software. WebGL was chosen as the primary build target so the game runs directly in browsers, with Editor as the development target.

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Core |
| **Knowledge Risk** | LOW — WebGL builds well-established in Unity |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md` |
| **Post-Cutoff APIs Used** | WebGPU support (new in Unity 6, not used yet) |
| **Verification Required** | WebGL build loads and runs in Chrome/Firefox/Safari |

## Context

### Problem Statement

Students need to view a live racing simulation during class. Any friction (installing apps, creating accounts) reduces participation. The professor hosts the game on a local server.

### Constraints

- Zero-install requirement for students (browser only)
- Professor self-hosts on campus network
- Memory limit: 2048 MB (webGLMaximumMemorySize in ProjectSettings)
- No UDP networking in browsers (WebSocket only)

## Decision

**WebGL** is the primary build target. **Editor** is used for development. No desktop standalone or mobile builds.

Custom WebGL template (`EDIRacing`) handles the game container. `BuildScript.cs` automates the WebGL build process. Docker serves the built files via nginx.

## Alternatives Considered

### Alternative 1: Desktop Standalone

- **Pros**: Better performance; full Unity feature access
- **Cons**: Requires installation on every student machine; IT department approval needed
- **Rejection Reason**: Install friction contradicts core requirement of zero-barrier access

### Alternative 2: Mobile (iOS/Android)

- **Pros**: Students have phones readily available
- **Cons**: Requires app store distribution; platform fragmentation; not suitable for classroom projection
- **Rejection Reason**: Not applicable to classroom viewing scenario

## Consequences

### Positive

- Zero-install: students join via URL
- Professor controls deployment independently
- Works on any device with a modern browser

### Negative

- Memory ceiling (2048 MB) limits asset complexity
- No compute shaders or advanced GPU features
- Networking limited to WebSocket (no UDP)
- Some Unity features unavailable (threading, file system access)

## Related

- [ADR-0001](ADR-0001-render-pipeline-urp.md) — URP chosen because HDRP doesn't support WebGL
- [ADR-0003](ADR-0003-websocket-multi-client-sync.md) — WebSocket chosen because WebGL lacks UDP
- [ADR-0004](ADR-0004-docker-deployment.md) — Docker serves WebGL build
