# Plan: Student 2D Wiring (Phase 6)

## Summary
Finish the **2D spectator** leg of the student landing page. Phase 4 already shipped a live
`<Link to={`/live/${roomCode}`}>` for the "2D 观战" choice, so the literal route works — Phase 6
hardens that seam: centralise the `/live/:roomCode` path into a **testable, casing-normalised
helper** (symmetric with the existing 3D `buildStudentPlayUrl`), and add a **round-trip back-link**
from the 2D spectator page back to the choice page so a 2D viewer can switch to 3D. No new
subsystems — the 2D dashboard (`LiveRacePage`) and its WS spectator path already exist and expose
no host controls.

## User Story
As a **student who picked "2D 观战" on the shared classroom link**,
I want **the 2D spectator dashboard to open reliably for the right room and let me switch back to 3D**,
so that **I can watch the live race on a low-bandwidth device and change my mind without re-typing a link**.

## Problem → Solution
Phase 4's 2D button is a bare `<Link to={`/live/${roomCode}`}>` — un-normalised (a hand-typed
lowercase `/join/abc` yields `/live/abc` in the URL bar while the page displays `ABC`) and, unlike
the 3D path, has **no unit-testable helper** and **no way back** to the choice page. → Phase 6
routes the 2D choice through a `buildSpectatorPath(roomCode)` helper (uppercase-normalised,
unit-tested) and adds a "← 返回 / 切换视图" link on `LiveRacePage` back to `#/join/:roomCode`,
completing the landing ↔ 2D wiring. The spectator view itself is reused as-is.

## Metadata
- **Complexity**: Small (1 helper + 1 landing edit + 1 spectator back-link + CSS + unit tests; ~5 files, ~50 lines)
- **Source PRD**: `.claude/PRPs/prds/role-bound-game-links.prd.md`
- **PRD Phase**: Phase 6 — Student 2D wiring (depends on Phase 4 = student link + landing page, **complete**; parallel with Phase 5; feeds Phase 7 QA)
- **Estimated Files**: 5 (0 new, 5 modified)

---

## UX Design

### Before
```
Landing (/join/CODE)                      2D spectator (/live/CODE)
┌────────────────────────────┐           ┌────────────────────────────────┐
│ 进入 3D 游戏   [ 2D 观战 ]──┼──Link────▶│ Live Race   Room ABC123         │
└────────────────────────────┘           │ leaderboard · minimap · events  │
   (bare <Link to=/live/abc>,            │ (no way back to switch to 3D)   │
    lowercase leaks to URL bar)          └────────────────────────────────┘
```

### After
```
Landing (/join/CODE)                      2D spectator (/live/CODE)
┌────────────────────────────┐           ┌────────────────────────────────┐
│ 进入 3D 游戏   [ 2D 观战 ]──┼──Link────▶│ ← 返回   Live Race  Room ABC123 │
└────────────────────────────┘           │ leaderboard · minimap · events  │
   (buildSpectatorPath →                 │  "← 返回" → #/join/ABC123        │
    /live/ABC123, normalised)            └──────────────┬─────────────────┘
                                                        └──▶ back to choice → 3D
```

### Interaction Changes
| Touchpoint | Before | After | Notes |
|---|---|---|---|
| 2D route build | inline `` `/live/${roomCode}` `` in JSX | `buildSpectatorPath(roomCode)` helper | Uppercase-normalised, unit-tested, single source of truth |
| Switch 2D → 3D | none (dead end) | "← 返回" link → `#/join/:roomCode` | Round-trips to the choice page |
| Casing | lowercase can leak to URL bar | always `/live/ABC123` | URL, display, and WS `web_join_room` now agree |
| Host controls in 2D | none (already clean) | none (verified) | Defense-in-depth confirmation only — no code change |

---

## Mandatory Reading

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 | `web-app/client/src/gameLaunch.js` | 1-18 | The helper module to EXTEND; mirror the exported-function + comment style. NOTE the 2D helper returns a **router path**, not a `${GAME_ROOT}#…` hash URL (see GOTCHA) |
| P0 | `web-app/client/src/pages/JoinLandingPage.jsx` | 1-27 | The landing page; line 20 is the bare 2D `<Link>` to replace with the helper |
| P0 | `web-app/client/src/pages/LiveRacePage.jsx` | 1-47 | The 2D spectator; `useParams` `roomCode`, `.live-header` layout — where the back-link is inserted |
| P0 | `web-app/__tests__/game-launch.test.js` | 1-43 | vitest pattern to EXTEND with a `buildSpectatorPath` `describe` block (mirror `buildStudentPlayUrl` at 29-43) |
| P1 | `web-app/client/src/App.jsx` | 15-29 | Route table — confirms `/join/:roomCode` (line 25) and `/live/:roomCode` (line 24) are both public (no `ProtectedRoute`); the back-link target must be a HashRouter `Link`, not `<a>` |
| P1 | `web-app/client/src/index.css` | 220-254 | `.live-header` / `.live-race-page` (220-222) and the Phase-4 `.join-*` block (247-254) — where `.live-back` is appended, reusing existing tokens |
| P2 | `Server/server.js` | 469-485 | `web_join_room` handler: uppercases `roomCode`, joins `room.webapps` as role `webapp`, replays cached state. Confirms the 2D viewer is a pure spectator (no host branch) and that casing is server-normalised too |
| P2 | `web-app/client/src/hooks/useRaceWebSocket.js` | 24-28, 84-92 | Sends `web_join_room` with `roomCode.toUpperCase()` on open; re-connects on `roomCode` change — why display/URL casing is cosmetic but worth normalising |
| P2 | `web-app/client/src/components/SendToGameModal.jsx` | 141 | The OTHER existing `#/live/${roomCode.trim().toUpperCase()}` builder — DO NOT refactor it in Phase 6 (different surface: professor dashboard). Listed so the implementer knows it exists and leaves it alone |

## External Documentation
No external research needed — feature uses established internal patterns (React Router `useParams`/`Link`,
a pure exported path helper, vitest unit test, existing CSS tokens). All patterns exist in-repo.

---

## Patterns to Mirror

### URL_HELPER — exported path builder (extend this module)
```js
// SOURCE: web-app/client/src/gameLaunch.js:12-18
// Build the student 3D-join URL. Carries the room code and role=play only — NEVER a host
// token — so opening it can join/watch but cannot create a room or trigger events.
export function buildStudentPlayUrl(roomCode) {
  const params = new URLSearchParams({ room: String(roomCode), role: 'play' });
  return `${GAME_ROOT}#${params.toString()}`;
}
// The new 2D helper mirrors the EXPORT + doc-comment shape, but returns a HashRouter-relative
// path ("/live/CODE"), NOT a GAME_ROOT hash URL — the 2D view stays inside the survey app.
```

### PAGE_COMPONENT — useParams + in-app Link (mirror for the back-link)
```jsx
// SOURCE: web-app/client/src/pages/JoinLandingPage.jsx:1,7,20
import { useParams, Link } from 'react-router-dom';
const { roomCode } = useParams();
<Link className="btn-primary btn-choice" to={`/live/${roomCode}`}> … </Link>
// The LiveRacePage back-link uses the SAME react-router Link, to={`/join/${roomCode}`}.
```

### SPECTATOR_HEADER — the 2D header the back-link joins
```jsx
// SOURCE: web-app/client/src/pages/LiveRacePage.jsx:32-40
<header className="live-header">
  <h1>Live Race</h1>
  <div className="live-room-info">
    <span className="live-room-code">Room {roomCode?.toUpperCase()}</span>
    …
// Insert the back-link as the FIRST child of <header>, before <h1>, styled .live-back.
```

### VITEST_HELPER_TEST — unit test block to mirror
```js
// SOURCE: web-app/__tests__/game-launch.test.js:29-43
describe('buildStudentPlayUrl', () => {
  it('puts room and role=play in the hash at the game root, with no token', () => {
    expect(buildStudentPlayUrl('A1B2C3')).toBe('/#room=A1B2C3&role=play');
  });
  it('carries no host token key', () => {
    expect(buildStudentPlayUrl('R1')).not.toContain('token');
  });
});
```

### CSS_TOKEN_REUSE — existing header + join styles
```css
/* SOURCE: web-app/client/src/index.css:220-222, 247-254 */
.live-header { display: flex; align-items: center; gap: 16px; padding: 12px 20px; border-bottom: 1px solid var(--border); }
.join-choice-sub { font-size: 13px; opacity: 0.85; }   /* var(--accent), var(--border) etc. already defined at top of file */
```

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `web-app/client/src/gameLaunch.js` | UPDATE | Add `buildSpectatorPath(roomCode)` → `/live/<UPPER>` (router path, no token, no host authority) |
| `web-app/client/src/pages/JoinLandingPage.jsx` | UPDATE | Use `buildSpectatorPath` in the 2D `<Link to=…>` (normalise casing, single source of truth) |
| `web-app/client/src/pages/LiveRacePage.jsx` | UPDATE | Add "← 返回" `Link` to `#/join/:roomCode` so a 2D viewer can switch to 3D |
| `web-app/client/src/index.css` | UPDATE | Minimal `.live-back` style (reuse existing tokens) |
| `web-app/__tests__/game-launch.test.js` | UPDATE | Unit tests for `buildSpectatorPath` (normal, casing, no-token) |

## NOT Building
- **Refactoring `SendToGameModal.jsx:141`** — it builds `#/live/${roomCode.trim().toUpperCase()}` on the *professor dashboard*, a different surface with its own trim/host context. Consolidating it into `buildSpectatorPath` is tempting but out of scope for the student-wiring phase (touches professor UI + its tests). Leave it exactly as-is.
- **2D spectator join telemetry / counting** — `web_join_room` adds the viewer to `room.webapps` and does **not** bump `student_count` (that counts Unity 3D students only, `Server/server.js:462`). Making 2D viewers count toward a metric is a server change and belongs to Phase 7's measurement/QA, not this wiring phase.
- **Rewriting the 2D dashboard** — PRD "What We're NOT Building": the 2D spectator is reused as-is. Do not touch `LiveLeaderboard`/`TrackMinimap`/`LiveEventFeed`/`useRaceWebSocket` logic beyond adding the header back-link.
- **Room-liveness pre-check on the landing page** — the 2D view already handles a dead/unknown room via its `Error` phase ("Room not found", `LiveRacePage.jsx:49-53`). No new liveness API (matches Phase 4's stateless-landing decision).
- **3D auto-join / role-lock** — Phase 5. Do not touch the 3D `<a href>` path or any Unity code.
- **React component-test infra (jsdom / Testing Library)** — not set up in this repo (vitest `globals:true`, Node env). Unit-test only the pure `buildSpectatorPath` helper; validate the pages via build + manual (same constraint Phase 4 documented).
- **Default-to-2D / low-bandwidth auto-fallback** — Open Question deferred; the landing keeps an explicit equal 3D/2D choice.

---

## Step-by-Step Tasks

### Task 1: Add `buildSpectatorPath` to the game-launch helper
- **ACTION**: In `web-app/client/src/gameLaunch.js`, add an exported function below `buildStudentPlayUrl` (end of file).
- **IMPLEMENT**:
  ```js
  // Build the in-app path to the 2D spectator dashboard for a room. Returns a HashRouter-relative
  // path ("/live/CODE") — NOT a game-root hash URL — because the 2D view lives inside the survey
  // app (see App.jsx route /live/:roomCode). Upper-cases the code so the URL bar, the on-page
  // "Room …" label, and the server's web_join_room (which upper-cases too) all agree. Carries no
  // token and grants no host authority: a spectator can watch but never create a room or trigger.
  export function buildSpectatorPath(roomCode) {
    return `/live/${String(roomCode).toUpperCase()}`;
  }
  ```
- **MIRROR**: `URL_HELPER` (`gameLaunch.js:12-18`) — same export + doc-comment shape.
- **IMPORTS**: none — no `GAME_ROOT`, no `URLSearchParams` (this is a router path, not a game-root URL).
- **GOTCHA**: Do **NOT** prefix `GAME_ROOT` or add a `#` — the other two helpers return `${GAME_ROOT}#…` for `<a href>` navigations that *leave* the survey app; this one returns a plain `/live/CODE` string for a react-router `<Link to>` that *stays inside* the HashRouter. `String(roomCode)` guards a non-string/undefined param (renders `/live/UNDEFINED` harmlessly rather than throwing). The professor-shared link always carries a real code.
- **VALIDATE**: `grep -n "buildSpectatorPath" web-app/client/src/gameLaunch.js`; unit tested in Task 4.

### Task 2: Route the landing 2D choice through the helper
- **ACTION**: In `web-app/client/src/pages/JoinLandingPage.jsx`, import `buildSpectatorPath` and use it in the 2D `<Link>`.
- **IMPLEMENT**:
  - Extend the import at line 2:
    ```jsx
    import { buildStudentPlayUrl, buildSpectatorPath } from '../gameLaunch.js';
    ```
  - Replace the 2D `<Link>` (line 20) `to`:
    ```jsx
    <Link className="btn-primary btn-choice" to={buildSpectatorPath(roomCode)}>
    ```
- **MIRROR**: `PAGE_COMPONENT` (`JoinLandingPage.jsx:20`) — keep the same `className` and children; only the `to` value changes.
- **IMPORTS**: add `buildSpectatorPath` to the existing `../gameLaunch.js` import.
- **GOTCHA**: Keep it a react-router `<Link>` (in-app HashRouter navigation), NOT an `<a href>` — the 2D view stays inside the survey app, unlike the 3D `<a>`. `buildSpectatorPath` returns `/live/CODE`, exactly the shape `<Link to>` expects.
- **VALIDATE**: `grep -n "buildSpectatorPath" web-app/client/src/pages/JoinLandingPage.jsx`; `npm run build` compiles; navigating `#/join/abc` → 2D button targets `#/live/ABC`.

### Task 3: Add a back-link on the 2D spectator page
- **ACTION**: In `web-app/client/src/pages/LiveRacePage.jsx`, import `Link` and add a "← 返回" link as the first child of `<header className="live-header">`.
- **IMPLEMENT**:
  - Extend the import at line 1:
    ```jsx
    import { useParams, Link } from 'react-router-dom';
    ```
  - Insert as the first child of `<header className="live-header">` (before `<h1>Live Race</h1>`, line 33):
    ```jsx
    <Link className="live-back" to={`/join/${roomCode}`} aria-label="返回视图选择">← 返回</Link>
    ```
- **MIRROR**: `PAGE_COMPONENT` (`JoinLandingPage.jsx:1,20`) for the `Link` import + usage; `SPECTATOR_HEADER` (`LiveRacePage.jsx:32-40`) for placement.
- **IMPORTS**: add `Link` to the existing `react-router-dom` import (already importing `useParams`).
- **GOTCHA**: Target `/join/${roomCode}` (raw param) — react-router `Link` normalises nothing, and the landing page tolerates any casing (`roomCode?.toUpperCase()` for display). Use a `<Link>`, not `<a href>` — this navigation stays in the HashRouter; an `<a>` would trigger a full reload. Do not add a host/EventPanel affordance of any kind — the 2D page must stay spectator-only (PRD security property).
- **VALIDATE**: `grep -n "live-back" web-app/client/src/pages/LiveRacePage.jsx`; build compiles; the 2D page shows "← 返回" that navigates to `#/join/CODE`.

### Task 4: Unit-test `buildSpectatorPath`
- **ACTION**: In `web-app/__tests__/game-launch.test.js`, extend the top import and add a `describe('buildSpectatorPath', …)` block.
- **IMPLEMENT**:
  ```js
  import { buildHostLaunchUrl, buildStudentPlayUrl, buildSpectatorPath } from '../client/src/gameLaunch.js';

  describe('buildSpectatorPath', () => {
    it('builds the in-app 2D spectator route for the room', () => {
      expect(buildSpectatorPath('A1B2C3')).toBe('/live/A1B2C3');
    });
    it('upper-cases the room code so URL, display, and WS all agree', () => {
      expect(buildSpectatorPath('abc123')).toBe('/live/ABC123');
    });
    it('is a router path, not a game-root hash URL, and carries no token', () => {
      const p = buildSpectatorPath('R1');
      expect(p.startsWith('/live/')).toBe(true);
      expect(p).not.toContain('#');
      expect(p).not.toContain('token');
    });
  });
  ```
- **MIRROR**: `VITEST_HELPER_TEST` (`game-launch.test.js:29-43`) — same `describe`/`it`/`expect` style; extend the existing top import (do not add a second import line).
- **IMPORTS**: add `buildSpectatorPath` to the existing `../client/src/gameLaunch.js` import.
- **GOTCHA**: Deterministic — no clock/env/random. `VITE_GAME_URL` is irrelevant here (the helper ignores `GAME_ROOT`), so assertions are stable regardless of env.
- **VALIDATE**: `cd web-app && npx vitest run` — all green, including the new block and existing `buildHostLaunchUrl` / `buildStudentPlayUrl` / `host-token` suites.

### Task 5: Add the `.live-back` style
- **ACTION**: In `web-app/client/src/index.css`, append a `.live-back` rule near the `.live-header` block (after line 222).
- **IMPLEMENT**:
  ```css
  .live-back { color: var(--accent); text-decoration: none; font-size: 14px; font-weight: 600;
    padding: 4px 10px; border: 1px solid var(--border); border-radius: 6px; white-space: nowrap; }
  .live-back:hover { background: var(--border); }
  ```
- **MIRROR**: `CSS_TOKEN_REUSE` (`index.css:220-222`) — reuse `--accent` / `--border` already defined at the top of the file; match the header's compact scale.
- **IMPORTS**: n/a.
- **GOTCHA**: Use existing CSS variables only (`--accent`, `--border`) — do not introduce a new color. The `.live-header` is a flexbox with `gap: 16px`, so the back-link sits inline before the title with no extra layout work.
- **VALIDATE**: Build succeeds; "← 返回" renders as a compact bordered link at the header start in light/dark.

---

## Testing Strategy

### Unit Tests
| Test | Input | Expected Output | Edge Case? |
|---|---|---|---|
| `buildSpectatorPath` normal | `'A1B2C3'` | `/live/A1B2C3` | No |
| `buildSpectatorPath` casing | `'abc123'` | `/live/ABC123` | Yes |
| `buildSpectatorPath` is router path, no token | `'R1'` | starts `/live/`, no `#`, no `token` | Yes |

### Edge Cases Checklist
- [x] Lowercase / mixed-case room in URL → `buildSpectatorPath` upper-cases; URL bar, display, and WS `web_join_room` (which also upper-cases) all agree.
- [x] Undefined/blank `roomCode` on the landing → `String(roomCode).toUpperCase()` yields `/live/UNDEFINED` (harmless; professor link always has a code) — no throw.
- [x] Dead/unknown room via the 2D path → `LiveRacePage` `Error` phase ("Room not found"), no crash (reused, unchanged).
- [x] 2D page exposes no host controls → confirmed by reading `LiveRacePage` (leaderboard/minimap/event-feed only) and `web_join_room` (role `webapp`, no host branch). Defense-in-depth for the PRD security property; verified, no code change.
- [x] Back-link stays in-app → react-router `<Link>` (HashRouter), not `<a href>`; no full reload.

---

## Validation Commands

### Static Analysis / Build (web-app client)
```bash
cd web-app/client && npm run build
```
EXPECT: Vite build succeeds; no undefined imports (`buildSpectatorPath`, `Link`).

### Unit Tests (web-app)
```bash
cd web-app && npx vitest run
```
EXPECT: All tests pass, including the new `buildSpectatorPath` block and existing `game-launch` / `host-token` / `auth` / `db` suites.

### Browser Validation (landing → 2D → back)
```bash
cd web-app/client && npm run dev
# open http://localhost:5173/survey/#/join/testcode
```
EXPECT:
- "2D 观战" navigates to `#/live/TESTCODE` (upper-cased even from a lowercase `/join/testcode`).
- The 2D page header shows "← 返回"; clicking it returns to `#/join/testcode`.
- No Host button / EventPanel / event-trigger control anywhere on the 2D page.

### Manual Validation
- [ ] Professor host launch (Phase 2) → open the shared student link → "2D 观战" → live spectator dashboard for that room.
- [ ] With a live race running: leaderboard, minimap, and event feed update in the 2D view.
- [ ] "← 返回" round-trips to the choice page; "进入 3D 游戏" from there still opens the game root.
- [ ] The 2D URL is `/survey/#/live/<UPPERCASE-CODE>` regardless of the casing in the join link.
- [ ] No host controls reachable from the 2D page.

---

## Acceptance Criteria
- [ ] `buildSpectatorPath(roomCode)` returns the upper-cased `/live/CODE` router path (no `#`, no token), unit-tested.
- [ ] The landing "2D 观战" `<Link>` uses `buildSpectatorPath` (single source of truth, casing normalised).
- [ ] `LiveRacePage` shows a "← 返回" `Link` to `#/join/:roomCode` and remains spectator-only.
- [ ] Client build + vitest green; no new dependencies, no jsdom infra.
- [ ] `SendToGameModal.jsx:141` left untouched.

## Completion Checklist
- [ ] Follows discovered patterns (`URL_HELPER`, `PAGE_COMPONENT`, `SPECTATOR_HEADER`, `VITEST_HELPER_TEST`, `CSS_TOKEN_REUSE`).
- [ ] 2D path is an in-app `<Link>`; only the 3D path is an `<a href>`.
- [ ] Casing normalised in one place (`buildSpectatorPath`); server already upper-cases too.
- [ ] No host UI on the 2D page (verified); no telemetry/server change; no 2D-dashboard rewrite.
- [ ] Tests deterministic; no scope creep into Phase 5 (3D/Unity) or Phase 7 (adversarial QA/metrics).
- [ ] Self-contained — every file:line and snippet captured above.

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Implementer prefixes `GAME_ROOT`/adds `#` to `buildSpectatorPath`, breaking the in-app `<Link>` | M | M | GOTCHA + comment state the router-path (not hash-URL) contract explicitly; unit test asserts no `#` |
| Back-link accidentally added as `<a href>` → full page reload out of the HashRouter | L | L | Task 3 GOTCHA mandates react-router `<Link>`; build/manual check |
| Someone consolidates `SendToGameModal:141` into the helper and breaks professor-dashboard tests | L | M | Explicit NOT-building entry; leave that line alone |
| Phase 6 perceived as "already done in Phase 4" → PR adds no value | L | L | Phase 6 delivers the testable helper + casing normalisation + round-trip back-link that Phase 4 explicitly deferred as "thin polish/wiring confirmation" (Phase 4 plan Notes) |

## Notes
- **Phase boundary**: Phase 4 made both landing buttons *live* (nothing dead) but flagged the 2D leg as
  "thin polish/wiring confirmation" for Phase 6 (Phase 4 plan, Notes + NOT-building). Phase 6 is exactly
  that polish: a single-source-of-truth, unit-tested, casing-normalised route helper plus a round-trip
  back-link — no new subsystem, matching the PRD's "reuse the 2D spectator as-is" constraint.
- **Security posture**: the 2D path carries only a room code, joins as role `webapp` (`Server/server.js:477`),
  and has no host branch — a 2D viewer can watch but never `create_room` or `event_triggered`. Phase 6
  changes nothing here; it only *confirms* the property (Phase 7 adversarially verifies it end-to-end).
- **Why a router path, not a hash URL**: the 3D leg *leaves* the survey app for the Unity game root
  (`<a href>` + `GAME_ROOT#…`), so it uses `buildStudentPlayUrl`. The 2D leg *stays* in the survey app's
  HashRouter, so `buildSpectatorPath` returns a plain `/live/CODE` for `<Link to>`. Keeping both in
  `gameLaunch.js` centralises all student-entry route building in one tested module.
- **Casing is cosmetic-but-correct**: `useRaceWebSocket` and the server both `toUpperCase()` the code,
  so a lowercase `/live/abc` already *works*; normalising in `buildSpectatorPath` just keeps the URL bar
  and the on-page "Room …" label consistent with what's sent over the wire.
