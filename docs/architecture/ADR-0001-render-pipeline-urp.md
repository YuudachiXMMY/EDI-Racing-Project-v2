# ADR-0001: Render Pipeline — Universal Render Pipeline (URP)

## Status

Accepted

## Date

2025-02-13

## Last Verified

2025-02-13

## Decision Makers

Project lead (professor + developer)

## Summary

The project needed a rendering pipeline that delivers acceptable visual quality while supporting WebGL builds with strict memory and performance constraints. URP 17.3.0 was chosen over Standard RP (deprecated) and HDRP (too heavy for WebGL).

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Rendering |
| **Knowledge Risk** | LOW — URP is well-documented and stable |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md` |
| **Post-Cutoff APIs Used** | URP 17.3.0 (minor API changes from earlier versions) |
| **Verification Required** | Verify WebGL build produces correct lighting and materials |

## Context

### Problem Statement

Unity offers three render pipelines: Built-in (Standard RP), URP, and HDRP. The project targets WebGL as primary platform with a 2048 MB memory ceiling and 60 FPS target. A pipeline must be chosen that balances visual quality with WebGL constraints.

### Current State

Project was started with URP already configured. V1 prototype used Standard RP but migrated during v2 setup.

### Constraints

- WebGL build target limits GPU features (no compute shaders, limited texture formats)
- Memory ceiling: 2048 MB
- Frame budget: 16.67 ms (60 FPS)
- Car racing visuals need basic lighting, shadows, and materials — not photorealism

## Decision

Use **Universal Render Pipeline (URP) 17.3.0** for all rendering.

URP provides SRP Batcher for efficient draw call batching, adequate shadow quality for a racing game, and full WebGL compatibility. The project uses asset store materials (CartoonTracksPack1, CarsAssetPack) that are URP-compatible.

## Alternatives Considered

### Alternative 1: Standard RP (Built-in)

- **Description**: Unity's legacy rendering pipeline
- **Pros**: Widest asset compatibility, most documentation
- **Cons**: Deprecated in Unity 6; no SRP Batcher; fewer optimization controls
- **Rejection Reason**: Deprecated — no future support; missing modern batching optimizations needed for WebGL

### Alternative 2: HDRP

- **Description**: High Definition Render Pipeline for high-fidelity visuals
- **Pros**: Best visual quality; advanced lighting features
- **Cons**: Does not support WebGL; high memory/GPU requirements
- **Rejection Reason**: Incompatible with WebGL build target

## Consequences

### Positive

- Full WebGL support with SRP Batcher optimization
- Adequate visual quality for educational racing game
- Active Unity support and updates

### Negative

- Some advanced rendering features unavailable (volumetric fog, ray tracing)
- Asset store materials must be URP-compatible (or converted)

## Related

- [ADR-0002](ADR-0002-webgl-build-target.md) — WebGL build target constrains pipeline choice
