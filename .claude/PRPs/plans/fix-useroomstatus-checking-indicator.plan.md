# Plan: Fix `useRoomStatus` stuck `checking` indicator

## Summary
The `useRoomStatus` hook can leave its `checking` flag stuck at `true` when the
user shortens the room code below the minimum length while a status fetch is
in-flight. This plan adds a single `setChecking(false)` reset in the
debounce/poll effect so the "checking" spinner never sticks. It is the follow-up
to Suggestion 1 from the `/code-review` of PR #51.

## User Story
As a professor entering a room code in the Send-to-Game / Send-Config modal,
I want the "checking…" indicator to clear when I delete characters back below a
valid length, so that the UI never shows a phantom in-progress check.

## Problem → Solution
**Current state**: When `roomCode` changes to a value shorter than
`MIN_CODE_LENGTH` (4) while a `getRoomStatus` request is in-flight, the effect
re-runs, aborts the in-flight fetch (`abortRef.current = true`), resets
`roomStatus` to `null`, then hits `if (trimmed.length < MIN_CODE_LENGTH) return;`
**without** resetting `checking`. The aborted fetch returns early after its
`await` and also never calls `setChecking(false)`. Result: `checking` stays
`true` until the next valid fetch, so `RoomStatusBadge` shows a stuck spinner.

**Desired state**: Every effect run that clears the pending fetch also resets
`checking` to `false`, so the spinner reflects only a genuinely in-flight check.

## Metadata
- **Complexity**: Small
- **Source PRD**: N/A (standalone — code-review follow-up)
- **PRD Phase**: N/A
- **Estimated Files**: 1

---

## UX Design

### Before
```
Room Code: [ABCD]      -> "Checking..." spinner shows (fetch in-flight)
User deletes a char
Room Code: [ABC]       -> status badge cleared, BUT "Checking..." spinner STUCK ON
```

### After
```
Room Code: [ABCD]      -> "Checking..." spinner shows (fetch in-flight)
User deletes a char
Room Code: [ABC]       -> status badge cleared AND spinner cleared (idle)
```

### Interaction Changes
| Touchpoint | Before | After | Notes |
|---|---|---|---|
| `RoomStatusBadge` while typing | Spinner can stick after shortening code mid-fetch | Spinner clears with the status | `checking` prop drives the spinner |
| `canSend` gate in both modals | Could stay disabled via `!checking` while spinner stuck | Re-enables correctly | `canSend = ... && !checking && ...` |

---

## Mandatory Reading

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 (critical) | `web-app/client/src/hooks/useRoomStatus.js` | 1-85 | The only file changed; contains the effect and `checking` state |
| P1 (important) | `web-app/client/src/components/SendToGameModal.jsx` | all | Consumer with `poll: true` + `canSend` using `!checking` |
| P1 (important) | `web-app/client/src/components/SendConfigModal.jsx` | all | Consumer with `poll: false` + `canSend` using `!checking` |
| P2 (reference) | `web-app/client/src/components/RoomStatusBadge.jsx` | all | Reads `checking` to render the spinner |

> **Branch note**: `useRoomStatus.js` exists **only on the PR #51 branch**
> `worktree-prp-plan-dead-code-cleanup` — it is not yet on `main`. Apply this fix
> on top of that branch (add a commit to PR #51), or on `main` after PR #51
> merges. Do NOT try to implement against `main` before the merge.

## External Documentation

No external research needed — feature uses established internal React hook
patterns already present in `useRoomStatus.js`.

---

## Patterns to Mirror

### STATE_RESET_AT_EFFECT_TOP
```js
// SOURCE: web-app/client/src/hooks/useRoomStatus.js:31-36 (PR #51 branch)
useEffect(() => {
    abortRef.current = false;
    clearTimeout(debounceRef.current);
    clearInterval(pollRef.current);
    setRoomStatus(null);            // <-- existing reset; add setChecking(false) here
    // ...
```
The effect already centralizes teardown/reset at its top (`clearTimeout`,
`clearInterval`, `setRoomStatus(null)`). The `checking` reset belongs in the same
block, mirroring `setRoomStatus(null)`.

### CHECKING_LIFECYCLE
```js
// SOURCE: web-app/client/src/hooks/useRoomStatus.js:44-52 (PR #51 branch)
setChecking(true);
const result = await getRoomStatus(trimmedCode);
if (abortRef.current) return;      // early-out leaves checking as-is
const data = result.success ? result.data : { exists: false, error: 'Failed to check' };
setRoomStatus(data);
setChecking(false);
```
`checking` is set `true` only inside `fetchStatus` right before the network call
and cleared on the success path. The abort path and the sub-min-length early
return are the two gaps this fix closes by resetting at the effect top.

### NO_CLIENT_TEST_HARNESS
```jsonc
// SOURCE: web-app/client/package.json (PR #51 branch)
"scripts": { "dev": "vite", "build": "vite build", "lint": "oxlint", "preview": "vite preview" }
// no vitest / jest / @testing-library / jsdom present
```
The client has **no** unit-test tooling. Validation for client changes is
`npm run lint` (oxlint) + `npm run build` (vite). Do not invent a test file;
setting up a hook-test harness is out of scope for a one-line fix.

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `web-app/client/src/hooks/useRoomStatus.js` | UPDATE | Add one `setChecking(false)` at the effect top so aborted / sub-min-length runs clear the spinner |

## NOT Building

- No changes to `SendToGameModal.jsx`, `SendConfigModal.jsx`, or
  `RoomCodeModal.jsx` — they consume `checking` unchanged.
- No new client test tooling (vitest / testing-library) — out of scope.
- No change to debounce/poll timing, abort logic, or the `onFinished` flow.
- No change to `RoomStatusBadge.jsx`.

---

## Step-by-Step Tasks

### Task 1: Reset `checking` at the top of the debounce/poll effect
- **ACTION**: In `web-app/client/src/hooks/useRoomStatus.js`, add `setChecking(false);`
  immediately after the existing `setRoomStatus(null);` line inside the main
  `useEffect(() => { ... }, [roomCode, poll])`.
- **IMPLEMENT**:
  ```js
  useEffect(() => {
      abortRef.current = false;
      clearTimeout(debounceRef.current);
      clearInterval(pollRef.current);
      setRoomStatus(null);
      setChecking(false);   // <-- ADD: clear any stale in-flight spinner; fetchStatus re-sets it when a real check starts
      // ... unchanged below
  ```
- **MIRROR**: `STATE_RESET_AT_EFFECT_TOP` — sits alongside `setRoomStatus(null)`.
- **IMPORTS**: None (`setChecking` already exists from `useState`).
- **GOTCHA**: Place it at the effect **top**, not before the early `return`. Top
  placement covers *both* the sub-min-length early-return path **and** the
  normal path (where `fetchStatus` sets `checking` back to `true` after the
  800 ms debounce). Do not add it inside `fetchStatus` — that would not fix the
  early-return gap. During the debounce window `checking` will read `false`,
  which is correct (no fetch is in flight yet).
- **VALIDATE**: `cd web-app/client && npm run lint && npm run build` — both pass;
  oxlint reports no new warnings for `useRoomStatus.js`.

---

## Testing Strategy

### Unit Tests
No client unit-test harness exists (see `NO_CLIENT_TEST_HARNESS`). Validation is
static (oxlint) + build (vite) + manual walkthrough.

| Test | Input | Expected Output | Edge Case? |
|---|---|---|---|
| Manual: shorten code mid-fetch | Type `ABCD` (spinner shows), then delete to `ABC` | Spinner clears, status badge cleared | Yes |
| Manual: normal check still works | Type valid `ABCDEF` | Spinner shows during fetch, resolves to status | No |
| Manual: poll path (SendToGame) | Valid code, wait for poll tick | Spinner behaves normally on each poll | Yes |

### Edge Cases Checklist
- [ ] Shorten to sub-min-length while a fetch is in-flight -> spinner clears
- [ ] Clear the field entirely while checking -> spinner clears
- [ ] Rapidly retype a valid code -> spinner reflects only the live fetch
- [ ] `poll: true` (SendToGameModal) still polls after the reset
- [ ] `canSend` re-enables once the spinner clears

---

## Validation Commands

### Static Analysis
```bash
cd web-app/client && npm run lint
```
EXPECT: No new oxlint warnings for `useRoomStatus.js` (baseline warnings only).

### Build
```bash
cd web-app/client && npm run build
```
EXPECT: Vite build passes (~59 modules), no errors.

### Manual Validation
- [ ] Start client dev server (`cd web-app/client && npm run dev`) with the backend running
- [ ] Open the Send-to-Game modal, type a 4+ char code, observe the "checking" spinner
- [ ] Delete a character to drop below 4 chars **while the spinner is showing**
- [ ] Confirm the spinner and status badge both clear (no stuck spinner)
- [ ] Retype a valid code and confirm a fresh check runs normally

---

## Acceptance Criteria
- [ ] `setChecking(false)` added at the top of the `[roomCode, poll]` effect
- [ ] `npm run lint` passes (no new warnings)
- [ ] `npm run build` passes
- [ ] Manual walkthrough shows the spinner clears when shortening code mid-fetch
- [ ] No behavioral change to normal check, polling, or `onFinished` auto-save

## Completion Checklist
- [ ] Code follows discovered patterns (reset-at-effect-top)
- [ ] No new client test tooling introduced
- [ ] Change is one line; no scope additions
- [ ] Applied on the correct branch (`worktree-prp-plan-dead-code-cleanup` / PR #51, or `main` post-merge)
- [ ] Self-contained — no questions needed during implementation

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Applying to `main` where the file doesn't exist yet | Medium | Implementation fails to find file | Apply on PR #51 branch, or wait for merge (documented in Mandatory Reading) |
| Spinner briefly reads `false` during the 800 ms debounce | Low | Cosmetic; correct behavior (no fetch in flight) | Intended — spinner should reflect only live fetches |

## Notes
- This is the direct implementation of Suggestion 1 from the PR #51 code review.
- No behavioral regression: `fetchStatus` still owns setting `checking = true`
  before each network call and `false` on success; this fix only closes the two
  paths (abort + sub-min-length early return) that previously left it stuck.
- Because the file lives on PR #51's branch, the most natural landing is an
  additional commit on `worktree-prp-plan-dead-code-cleanup` (that PR already
  carries plan + report artifacts, so this fits its established pattern).
