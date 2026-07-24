# Car Movement & Navigation

> **Status**: Accepted — reverse-engineered from codebase (v2)
> **Last Updated**: 2026-07-23
> **Source**: `CarController.cs`, `CarSpawner.cs`, `WaypointPath.cs`,
> `CarIdentity.cs`, `RaceConfig.cs`

---

## 1. Overview

Cars are fully autonomous — no player controls steering. Each car uses a Unity
`NavMeshAgent` to navigate a baked NavMesh track, targeting waypoints with
look-ahead smoothing. Speed is dynamically adjusted by curvature analysis,
forward collision detection, trigger-based inelastic collisions, and event
modifiers. Per-car lateral offsets create varied racing lines so cars don't bunch
into a single-file train.

---

## 2. Player Fantasy

"The cars feel like they're actually racing — they slow down in corners, avoid
each other, take slightly different lines, and occasionally bump and recover.
When an event hits, affected cars visibly slow down while others pull ahead."

---

## 3. Detailed Rules

### Spawning (CarSpawner)

- Prefabs selected by `colorIndex` (0=green, 1=black, 2=red, 3=blue, 4=white)
- Position: `StartingGridPositions` if available, otherwise random offset around `SpawnPoint`
- Spawn position snapped to NavMesh via `NavMesh.SamplePosition()`
- Components added at runtime:
  - `Rigidbody` (mass=100, isKinematic=true)
  - `NavMeshAgent` (radius from config)
  - `BoxCollider` (isTrigger=true, for inelastic collision)
  - `TrailRenderer` (color matched to car)
  - `CarController` (AI driving)
  - `CarIdentity` (team name, attributes)

### Waypoint Targeting (Look-Ahead)

1. Find a target point ~`LookAheadDistance` meters ahead along the waypoint path
2. Walk forward from current waypoint, consuming distance segment by segment
3. Interpolate within the final segment when look-ahead distance falls mid-segment
4. Apply persistent lateral offset + small random variation per waypoint

```
destination = ComputeLookAheadTarget()  // ~15m ahead on path
offset = persistentLateralOffset + Random.Range(-0.3, 0.3) * lateralOffsetRange
destination += perpendicular * offset   // offset validated on NavMesh
agent.SetDestination(destination)
```

### Waypoint Advance

Car advances to next waypoint when:
- `agent.remainingDistance < 2m` (close enough), OR
- `HasPassedWaypoint()` returns true (dot product of track direction > 0)

### Speed Control Layers

Four independent multipliers compose the final speed:

```
finalSpeed = baseSpeed × collisionMult × curvatureMult × forwardMult
```

This composite is **suspended** while event speed modifiers are active.

### Frame Staggering

Expensive calculations are staggered across frames to distribute CPU load:
- Curvature update: every 5th frame (offset by instance ID)
- Forward detection: every 3rd frame (offset by instance ID)
- Collision speed: every frame (cheap)

---

## 4. Formulas

### Curvature-Based Speed (updated every 5 frames)

```
angle = Vector3.Angle(segment1, segment2)    // 0° = straight, 180° = hairpin
curvature = angle / 180                       // normalized to [0, 1]
target = 1 - CurveSlowdownFactor × curvature²
target = clamp(target, 0.3, 1.0)
curvatureMult = MoveTowards(curvatureMult, target, deltaTime × 2)
```

Squared curvature means gentle curves barely affect speed; sharp turns cause
significant braking.

### Forward Collision Detection (updated every 3 frames)

```
SphereCast(origin, radius=agent.radius×0.8, direction=velocity, range=ForwardDetectionRange)
if hit another CarController:
    proximity = 1 - (hit.distance / ForwardDetectionRange)
    target = Lerp(1.0, ForwardSlowdownFactor, proximity)
forwardMult = MoveTowards(forwardMult, target, deltaTime × 3)
```

Closer car ahead = more slowdown. Smooth transition prevents jitter.

### Inelastic Collision (trigger-based)

```
OnTriggerEnter: collisionCount++
OnTriggerExit:  collisionCount = max(0, collisionCount - 1)

target = collisionCount > 0 ? CollisionSpeedFactor : 1.0
rate = collisionCount > 0 ? 0.15 : CollisionRecoveryTime
collisionMult = MoveTowards(collisionMult, target, deltaTime / rate)
```

Impact is fast (rate=0.15); recovery is gradual (rate=CollisionRecoveryTime).

### Event Speed Modifier

```
ApplySpeedModifier(delta, duration):
    activeModifierCount++
    agent.speed += delta
    wait(duration)
    agent.speed -= delta
    activeModifierCount--
```

While `activeModifierCount > 0`, composite speed formula is bypassed — the
agent's speed is managed directly by modifier coroutines. Multiple events stack
additively.

### Composite Speed (when no event modifiers active)

```
agent.speed = baseSpeed × collisionMult × curvatureMult × forwardMult
```

### Stuck Detection

```
every frame:
    distMoved = Distance(position, lastCheckedPosition)
    if distMoved < StuckDistanceThreshold:
        stuckTimer += deltaTime
        if stuckTimer >= StuckTimeThreshold:
            consecutiveStuckCount++
            if consecutiveStuckCount >= MaxRecoveryAttempts (3):
                warp to next waypoint
            else:
                attemptRecovery()
    else:
        stuckTimer = 0
        consecutiveStuckCount = 0
```

### Recovery Escalation

```
sideSign = (count % 2 == 0) ? +1 : -1     // alternate sides
escalation = 1 + count × 0.5               // 1x → 1.5x → 2x
recoveryTarget = position + lateral × offset × escalation + forward × offset
snap to NavMesh
```

After 3 failed recoveries: warp to next waypoint, clear trail.

---

## 5. Edge Cases

| Scenario | Handling |
|----------|----------|
| Car stuck in geometry | Progressive recovery: offset sideways (alternating), then warp |
| Multiple simultaneous collisions | `collisionCount` tracks overlapping triggers; slowdown compounds |
| Look-ahead exceeds path length | Returns farthest available waypoint |
| NavMesh sample fails during recovery | Falls back to warp-to-next-waypoint |
| Agent path pending | Skips waypoint advance check |
| Zero-length waypoint segment | Curvature multiplier stays at 1.0 |
| Event modifier expires during collision | Composite speed resumes with current collision multiplier |

---

## 6. Dependencies

| Dependency | Role |
|-----------|------|
| NavMeshAgent (Unity) | Pathfinding and movement |
| NavMeshSurface (baked) | Track navigation mesh |
| WaypointPath | Ordered waypoint sequence |
| CarIdentity | Team name, attributes for event matching |
| RaceConfig (SO) | All tunable parameters |
| EventManager | Calls `ApplySpeedModifier()` |
| LapTracker | Notified via CheckpointTrigger |

---

## 7. Tuning Knobs

| Parameter | Default | Range | Effect |
|-----------|---------|-------|--------|
| DefaultSpeed | 40 m/s | > 0 | Base cruising speed |
| AngularSpeed | 800 °/s | > 0 | Rotation speed |
| Acceleration | 60 m/s² | > 0 | Speed-up rate |
| AgentRadius | 2.5 m | > 0 | Inter-car spacing (half car width) |
| CarScale | 2.5x | > 0 | Visual model scale |
| WaypointLateralOffset | 3 m | ≥ 0 | Max random lane offset |
| LookAheadDistance | 15 m | > 0 | Target distance along path |
| CurveSlowdownFactor | 0.5 | 0–1 | Cornering slowdown strength |
| ForwardDetectionRange | 12 m | ≥ 0 | SphereCast range for cars ahead |
| ForwardSlowdownFactor | 0.6 | 0–1 | Speed reduction when car detected ahead |
| CollisionSpeedFactor | 0.4 | 0–1 | Immediate speed on impact |
| CollisionRecoveryTime | 1 s | > 0 | Seconds to recover from collision |
| StuckTimeThreshold | 2 s | > 0 | Seconds before deemed stuck |
| StuckDistanceThreshold | 0.5 m | > 0 | Min movement to not be stuck |
| StuckRecoveryOffset | 5 m | > 0 | Lateral offset for recovery attempt |
| MaxRecoveryAttempts | 3 | const | Warp after N failed recoveries |
| TrailDuration | 0.5 s | ≥ 0 | Trail persistence time |
| TrailStartWidth | 0.8 m | ≥ 0 | Trail width at car |
| TrailEndWidth | 0.1 m | ≥ 0 | Trail width at tail |

---

## 8. Acceptance Criteria

- [ ] Cars follow waypoint path smoothly without stopping at each waypoint
- [ ] Different cars take visibly different racing lines (lateral offset)
- [ ] Cars slow down in curves proportional to curvature severity
- [ ] Cars brake when approaching another car ahead
- [ ] Trigger-based collisions cause immediate slowdown with gradual recovery
- [ ] Stuck cars recover within 3 attempts or warp to next waypoint
- [ ] Event speed modifiers stack additively and expire after duration
- [ ] Composite speed resumes correctly after all event modifiers expire
- [ ] Trail renderer follows car with correct team color
- [ ] Frame-staggered updates distribute CPU load across cars
