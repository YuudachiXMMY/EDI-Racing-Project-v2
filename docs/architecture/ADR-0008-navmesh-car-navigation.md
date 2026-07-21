# ADR-0008: NavMesh Agent for Car Navigation

## Status

Accepted

## Date

2025-02-13

## Last Verified

2025-02-13

## Decision Makers

Project lead (professor + developer)

## Summary

Racing cars need autonomous track navigation without player input. Unity's NavMesh Agent (AI Navigation 2.0.13) was chosen over physics-based driving (unreliable) and spline-following (rigid), proven reliable in v1 with 18 releases of classroom use.

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Navigation / Physics |
| **Knowledge Risk** | MEDIUM — AI Navigation 2.0.13 is post-cutoff package version |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md` |
| **Post-Cutoff APIs Used** | AI Navigation 2.0.13 (NavMeshAgent, NavMeshSurface) |
| **Verification Required** | Cars navigate the track without getting stuck or leaving the road |

## Context

### Problem Statement

Cars in this racing game are autonomous — they drive themselves around the track. The professor triggers events that modify car speeds, but does not control steering. The navigation system must reliably follow the track geometry.

### Constraints

- Cars must navigate arbitrary track layouts (professor may change tracks)
- No player input for steering — fully autonomous
- Must work with speed modifications from event rules (ADR-0006)
- Track geometry is a 3D mesh (CartoonTracksPack1 asset)

## Decision

Use **Unity NavMesh Agent** (AI Navigation package 2.0.13) for car pathfinding. The track surface is baked as a NavMesh. Each car has a `NavMeshAgent` component that follows waypoints around the track. `CarController` manages the agent's speed based on base speed + event-applied deltas.

Key components:
- `NavMeshSurface` on track — baked navigation mesh
- `NavMeshAgent` on each car — handles pathfinding and movement
- `CarController` — sets agent speed, manages waypoints, handles lap tracking

## Alternatives Considered

### Alternative 1: Physics-Based Driving

- **Pros**: More realistic vehicle behavior; interesting drift mechanics
- **Cons**: Unreliable on complex track geometry; requires extensive tuning; cars can get stuck
- **Rejection Reason**: Reliability is paramount for classroom demonstrations; professor can't debug stuck cars mid-lecture

### Alternative 2: Spline Following

- **Pros**: Deterministic; smooth paths; easy to implement
- **Cons**: Rigid — all cars follow exact same path; no overtaking; track changes require re-authoring splines
- **Rejection Reason**: Lacks visual variety; too rigid for a racing game feel

## Consequences

### Positive

- Reliable navigation proven across 18 v1 releases
- Handles arbitrary track geometry automatically
- Built-in obstacle avoidance (cars don't overlap)
- Speed easily controlled via `NavMeshAgent.speed`

### Negative

- NavMesh must be re-baked when track changes
- Cars may take suboptimal paths on complex geometry
- Not physically realistic (cars slide, don't drift)

## Related

- [ADR-0006](ADR-0006-interpreted-rule-engine.md) — Event rules modify car speed via NavMeshAgent
- [ADR-0001](ADR-0001-render-pipeline-urp.md) — URP renders the track and cars
