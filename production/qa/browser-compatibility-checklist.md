# Cross-Browser Compatibility Checklist

## Test Matrix

| Browser | Version | WebGL2 | Status | Notes |
|---------|---------|--------|--------|-------|
| Chrome | latest | Yes | [ ] | Primary target |
| Firefox | latest | Yes | [ ] | AudioContext autoplay policy |
| Safari | latest | Partial | [ ] | Stricter GPU memory limits |
| Edge | latest | Yes | [ ] | Chromium-based, similar to Chrome |

## Performance Tests

Run each scenario and record FPS (visible via FpsCounter in Development builds) and memory (browser DevTools > Memory).

| Scenario | Chrome FPS | Firefox FPS | Safari FPS | Edge FPS | Notes |
|----------|-----------|-------------|-----------|---------|-------|
| 5 cars | | | | | Baseline |
| 20 cars | | | | | Medium load |
| 50 cars | | | | | Target ceiling |
| 50 cars + snow event | | | | | Weather + max cars |
| 50 cars + night event | | | | | Lighting + max cars |

## Feature Tests (per browser)

| Feature | Chrome | Firefox | Safari | Edge |
|---------|--------|---------|--------|------|
| WebGL build loads | [ ] | [ ] | [ ] | [ ] |
| WebSocket connects | [ ] | [ ] | [ ] | [ ] |
| Room hosting works | [ ] | [ ] | [ ] | [ ] |
| Room joining works | [ ] | [ ] | [ ] | [ ] |
| Keyboard shortcuts (1-7, F1-F9) | [ ] | [ ] | [ ] | [ ] |
| Free camera (WASD + mouse) | [ ] | [ ] | [ ] | [ ] |
| Spectator camera follows leader | [ ] | [ ] | [ ] | [ ] |
| Events trigger correctly | [ ] | [ ] | [ ] | [ ] |
| Weather VFX (snow particles) | [ ] | [ ] | [ ] | [ ] |
| Skybox transitions (day/sunset) | [ ] | [ ] | [ ] | [ ] |
| Night mode lighting | [ ] | [ ] | [ ] | [ ] |
| Car labels visible and billboard | [ ] | [ ] | [ ] | [ ] |
| Label distance culling works | [ ] | [ ] | [ ] | [ ] |
| Trail renderers visible | [ ] | [ ] | [ ] | [ ] |
| Save session (P key) | [ ] | [ ] | [ ] | [ ] |
| Load session (L key) | [ ] | [ ] | [ ] | [ ] |
| Export results CSV (X key) | [ ] | [ ] | [ ] | [ ] |
| Leaderboard updates | [ ] | [ ] | [ ] | [ ] |
| Race finish detection | [ ] | [ ] | [ ] | [ ] |

## Known Issues

- **Safari**: WebGL2 support is partial; may fall back to WebGL1 for some features. Stricter memory limits may cause issues with 50+ cars.
- **Firefox**: AudioContext requires user gesture before playing. May affect future audio features.
- **All browsers**: WebGL performance varies significantly with GPU drivers. Integrated GPUs will perform worse than dedicated.

## How to Test

1. Build WebGL: `./scripts/build-webgl.sh`
2. Fix and serve: `./Deploy/fix-webgl.sh --serve`
3. Open `http://localhost:8080` in each browser
4. Load a 50-car CSV and start the race
5. Record FPS from the on-screen counter (Development build only)
6. Check browser DevTools > Memory for heap size
7. Test each feature from the checklist above
