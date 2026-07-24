# Weather System

> **Status**: Accepted — reverse-engineered from codebase (v2)
> **Last Updated**: 2026-07-23
> **Source**: `WeatherEffect.cs`, `WeatherType.cs`

---

## 1. Overview

The weather system provides visual reinforcement for event effects. It manages
skybox transitions, directional light color/intensity, ambient lighting, and
particle effects (snow). It supports an automatic day/sunset cycle that runs
during the race, plus event-triggered overrides (snow, night, sunset) that
temporarily pause the cycle.

---

## 2. Player Fantasy

"When I trigger a 'Language Barrier' event with snow, the sky goes overcast,
snowflakes fill the screen, and the affected cars visibly slow down. The visual
atmosphere change makes the penalty feel real and dramatic — students remember
it."

---

## 3. Detailed Rules

### Weather Types

| Type | Visual | Speed Penalty (typical) |
|------|--------|------------------------|
| None | No change | — |
| Snow | Overcast skybox + 2000-particle blizzard + dimmed light | -8 m/s |
| Night | Dark skybox + very low light intensity | -5 m/s |
| Sunset | Warm skybox + orange directional light | -3 m/s |

### Automatic Day/Sunset Cycle

```
if DayCycleEnabled and race is running:
    phase = (elapsed % DayCycleDuration) / DayCycleDuration
    if phase >= DayFraction → transition to Sunset
    if phase < DayFraction → transition to Day
```

Default: 90s cycle, 60% day (54s), 40% sunset (36s).

### Event Override

When an event triggers weather (snow/night/sunset):
1. `eventOverrideActive = true` — pauses the auto cycle
2. Weather activates for `rule.Duration` seconds
3. After duration expires, `EndEventOverride()` resumes the cycle
4. Cycle resumes at the correct phase (not reset)

### Override Priority

If multiple event weathers overlap, each manages its own timer. The override
only ends when ALL event weathers have expired (checked via `IsSnowActive`,
`IsNightActive`, `IsSunsetActive` flags).

### Transition Blending

All state changes use `SkyTransition` coroutine:
```
over TransitionTime seconds:
    skybox = target (immediate swap)
    light.intensity = Lerp(start, target, SmoothStep(t))
    light.color = Lerp(start, target, SmoothStep(t))
    ambientLight = Lerp(start, target, SmoothStep(t))
```

If a new transition starts mid-blend, the previous coroutine is stopped and the
new one starts from current values.

### Snow Particle System

- Created programmatically in `Awake()`
- 2000 max particles, emission rate 500/s
- Follows main camera position (50m above, updated in LateUpdate)
- Box shape 100×1×100 meters
- Start speed 8 m/s, lifetime 5s, gravity 0.3
- URP Particles/Unlit shader (with fallbacks)

---

## 4. Formulas

### Cycle Phase

```
elapsed = Time.time - cycleStartTime
phase = (elapsed % DayCycleDuration) / DayCycleDuration
isSunset = phase >= DayFraction
```

### Transition Interpolation

```
t = SmoothStep(0, 1, clamp01(elapsed / TransitionTime))
// SmoothStep provides ease-in/ease-out for natural-feeling transitions
```

### Snow Light Dimming

```
snow_intensity = originalLightIntensity × 0.7  // 30% dimming
```

---

## 5. Edge Cases

| Scenario | Handling |
|----------|----------|
| No directional light in scene | Light-related transitions skipped; ambient-only |
| DayCycleDuration = 0 | Cycle update returns early (no division by zero) |
| Multiple weather events overlap | Each tracks its own flag; override ends only when all clear |
| Race reset during active weather | `ResetAll()` stops all coroutines, restores originals |
| No skybox materials assigned | `RenderSettings.skybox` set to null (renders solid color) |
| Transition interrupted mid-blend | Previous coroutine stopped; new one starts from current state |
| DayCycleEnabled = false | No auto-cycling; only event-triggered weather works |

---

## 6. Dependencies

| Dependency | Role |
|-----------|------|
| EventManager | Calls `ActivateSnow/Night/Sunset(duration)` |
| Camera.main | Snow particles follow camera |
| Directional Light | Intensity/color transitions |
| RenderSettings | Skybox + ambient light |
| Skybox Materials (×4) | Day, Night, Snow, Sunset (Customizable Skybox shader) |

---

## 7. Tuning Knobs

| Parameter | Default | Range | Effect |
|-----------|---------|-------|--------|
| DayCycleEnabled | true | bool | Enable/disable auto day/sunset cycle |
| DayCycleDuration | 90 s | > 0 | Full cycle period |
| DayFraction | 0.6 | 0.2–0.8 | % of cycle in daytime |
| TransitionTime | 1.5 s | > 0 | Skybox/light blend duration |
| NightLightIntensity | 0.15 | 0–1 | Directional light during night |
| SunsetLightIntensity | 0.6 | 0–1 | Directional light during sunset |
| DayAmbientColor | (0.53, 0.53, 0.53) | Color | Ambient during day |
| NightAmbientColor | (0.05, 0.05, 0.15) | Color | Ambient during night |
| SunsetAmbientColor | (0.45, 0.25, 0.15) | Color | Ambient during sunset |
| SunsetLightColor | (1, 0.55, 0.2) | Color | Directional light color at sunset |
| SnowAmbientColor | (0.6, 0.65, 0.7) | Color | Ambient during snow |
| Snow MaxParticles | 2000 | int (const) | Particle budget |
| Snow EmissionRate | 500/s | float (const) | Snowfall density |

---

## 8. Acceptance Criteria

- [ ] Day/sunset auto-cycle runs during race with correct timing
- [ ] Snow event: skybox changes, particles appear, light dims 30%
- [ ] Night event: dark skybox, very low light intensity
- [ ] Sunset event: warm skybox + orange directional light
- [ ] Transitions use smooth interpolation (no pop-in)
- [ ] Event weather pauses auto-cycle; resumes at correct phase after
- [ ] Snow particles follow main camera
- [ ] ResetAll restores original skybox, light, and ambient
- [ ] Multiple overlapping weather events don't conflict
