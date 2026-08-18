# Plan: Host-Token Secret Guard (MEDIUM-1 remediation)

## Summary
When `REQUIRE_HOST_TOKEN=true` becomes the auth boundary (Phase 2+), a default/unset
`INTERNAL_SECRET` (`'edi-internal-default'`, committed in the repo) lets anyone mint a
valid host token and bypass the gate. This plan adds a **boot-time config guard** to both
services: fail-fast (exit 1) when enforcement is on with a weak secret, and emit a loud
warning when the flag is off but the secret is still default — plus documents that a
strong random `INTERNAL_SECRET` is mandatory once enforcement is enabled.

## User Story
As the operator (professor/admin) deploying the EDI-Racing stack,
I want the servers to refuse to start (or loudly warn) when the host-token gate is
enabled but still using the public default secret,
So that a misconfiguration can never silently ship an auth boundary that anyone can bypass.

## Problem → Solution
**Current:** Both `web-app/src/hostToken.js:18` and `Server/server.js:10` fall back to
`process.env.INTERNAL_SECRET || 'edi-internal-default'`. With `REQUIRE_HOST_TOKEN=true`
and no override, the token gate is fully bypassable and nothing warns the operator.
**Desired:** A shared, pure `checkSecretConfig({ secret, requireHostToken })` decision
function, mirrored in both services (ESM + CJS), invoked at boot. `fatal` → log + `exit(1)`;
`warn` → loud startup warning; `ok` → silent. `.env.example` files document the requirement.

## Metadata
- **Complexity**: Small
- **Source PRD**: `.claude/PRPs/reviews/host-token-model-review.md` (finding MEDIUM-1)
- **PRD Phase**: Pre-Phase-2 hardening ("Required Before Enforcement" #1)
- **Estimated Files**: 6 (2 source, 2 boot entry points, 1 test, 3 `.env.example` — env edits permission-gated)

---

## UX Design

Internal / operator-facing change — no player UX. The only surface is server stdout/stderr
at boot and process exit code.

### Before
```
$ REQUIRE_HOST_TOKEN=true node server.js     # no INTERNAL_SECRET set
WebSocket + HTTP server listening on port 8080   ← starts happily, gate is bypassable
```

### After
```
$ REQUIRE_HOST_TOKEN=true node server.js     # no INTERNAL_SECRET set
[Auth] FATAL: REQUIRE_HOST_TOKEN=true but INTERNAL_SECRET is unset or the public
       default. Set a strong random INTERNAL_SECRET (e.g. `openssl rand -hex 32`)
       before enabling host-token enforcement. Refusing to start.
$ echo $?
1

$ node server.js                             # flag off, secret still default
[Auth] WARNING: INTERNAL_SECRET is the public default 'edi-internal-default'.
       This is acceptable only with REQUIRE_HOST_TOKEN=false. Set a strong secret
       before enabling enforcement.
WebSocket + HTTP server listening on port 8080   ← starts, but operator is warned
```

### Interaction Changes
| Touchpoint | Before | After | Notes |
|---|---|---|---|
| Server boot (enforcement on, weak secret) | Starts silently | Logs `[Auth] FATAL`, exits 1 | Blocks unsafe deploy |
| Server boot (enforcement off, weak secret) | Starts silently | Logs `[Auth] WARNING`, starts | Advisory only |
| Server boot (strong secret) | Starts silently | Starts silently | No behavior change |
| Web-app boot | Same, symmetric | Same guard mirrored | Minter must match verifier |

---

## Mandatory Reading

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 | `web-app/src/hostToken.js` | 1-44 | ESM source; where `DEFAULT`/guard export goes; mirror-warning header |
| P0 | `Server/server.js` | 1-62, 594-597 | CJS mirror site; env reads; boot `server.listen` callback |
| P0 | `web-app/src/index.js` | 1-59 | Web-app entry point; where boot guard is invoked before `app.listen` |
| P1 | `web-app/__tests__/host-token.test.js` | all | Test harness, vitest style, deterministic-input convention to mirror |
| P2 | `web-app/src/routes/game-status.js` | 1-17 | Minting caller; confirms web-app depends on the same secret |
| P2 | `.claude/PRPs/reviews/host-token-model-review.md` | 18-22, 66-69 | Exact finding + acceptance context |

## External Documentation
No external research needed — feature uses established internal patterns (env-default reads,
`process.exit` on fatal config, `console.warn`/`[Auth]` log prefix, vitest pure-function tests).

---

## Patterns to Mirror

### ENV_DEFAULT_READ
```js
// SOURCE: Server/server.js:10-13
const INTERNAL_SECRET = process.env.INTERNAL_SECRET || 'edi-internal-default';
const REQUIRE_HOST_TOKEN = (process.env.REQUIRE_HOST_TOKEN || 'false').toLowerCase() === 'true';
```

### PURE_INJECTABLE_FUNCTION  (deterministic, testable — same posture as injectable `now`)
```js
// SOURCE: web-app/src/hostToken.js:52  (verifyHostToken takes `now` for deterministic tests)
export function verifyHostToken(token, now = Date.now()) { ... }
// → new guard follows this: pure inputs in, decision object out, no process.exit inside.
```

### LOCKSTEP_DUPLICATION  (ESM ↔ CJS mirror with warning header)
```js
// SOURCE: Server/server.js:15-18
// Host-token verification — MUST match web-app/src/hostToken.js byte-for-byte.
// If you change the format here, update web-app/src/hostToken.js in lockstep.
```

### AUTH_LOG_PREFIX
```js
// SOURCE: Server/server.js:313
console.log(`[Auth] Rejected create_room (${result.error || 'no token'})`);
```

### FATAL_EXIT / BOOT_CALLBACK
```js
// SOURCE: web-app/src/index.js:61-64  (process lifecycle handling already present)
process.on('SIGTERM', () => { closeDb(); process.exit(0); });
// SOURCE: Server/server.js:594-596  &  web-app/src/index.js:57-59  (boot callbacks)
server.listen(PORT, () => { console.log(`WebSocket + HTTP server listening on port ${PORT}`); });
```

### TEST_STRUCTURE
```js
// SOURCE: web-app/__tests__/host-token.test.js:1-19
import { describe, it, expect } from 'vitest';
import { mintHostToken, verifyHostToken } from '../src/hostToken.js';
const T0 = 1_750_000_000_000;
describe('hostToken', () => {
  it('round-trips a valid token and recovers the surveyId', () => { ... });
});
```

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `web-app/src/hostToken.js` | UPDATE | Export `DEFAULT_INTERNAL_SECRET` + pure `checkSecretConfig()` |
| `web-app/src/index.js` | UPDATE | Invoke guard at boot before `app.listen`; exit 1 on fatal |
| `Server/server.js` | UPDATE | Mirror `checkSecretConfig()` (CJS) + invoke at boot before `server.listen` |
| `web-app/__tests__/host-token.test.js` | UPDATE | Add unit tests for `checkSecretConfig()` decision matrix |
| `Server/.env.example` | UPDATE | Add `REQUIRE_HOST_TOKEN`, `HOST_TOKEN_TTL_MS`, strong-secret note (⚠ permission-gated) |
| `web-app/.env.example` | UPDATE | Same doc note (⚠ permission-gated) |
| `Deploy/.env.example` | UPDATE | Add `INTERNAL_SECRET` / `REQUIRE_HOST_TOKEN` guidance (⚠ permission-gated) |

## NOT Building
- Session/room binding or single-use tokens (LOW-2 — deferred to Phase 2).
- A server-side native unit test harness (LOW-4 — `Server/` has no test runner; out of scope).
- Any change to token wire format, HMAC, or `verifyHostToken` logic.
- Secret rotation, KMS, or secret-manager integration.
- Enabling `REQUIRE_HOST_TOKEN=true` by default (stays off; this plan only guards the *transition*).
- Removing the `'edi-internal-default'` fallback for the archive path (pre-existing, non-regression).

---

## Step-by-Step Tasks

### Task 1: Add pure guard + exported default to `web-app/src/hostToken.js`
- **ACTION**: Add an exported constant for the default secret and a pure decision function.
- **IMPLEMENT**:
  ```js
  export const DEFAULT_INTERNAL_SECRET = 'edi-internal-default';

  /**
   * Decide whether the current secret configuration is safe to boot with.
   * Pure — never reads env or exits; caller (entry point) acts on the result.
   * @param {{ secret: string|undefined, requireHostToken: boolean }} cfg
   * @returns {{ level: 'ok'|'warn'|'fatal', message: string }}
   */
  export function checkSecretConfig({ secret, requireHostToken }) {
    const isDefault = !secret || secret === DEFAULT_INTERNAL_SECRET;
    if (!isDefault) return { level: 'ok', message: '' };
    if (requireHostToken) {
      return { level: 'fatal', message:
        'REQUIRE_HOST_TOKEN=true but INTERNAL_SECRET is unset or the public default. ' +
        'Set a strong random INTERNAL_SECRET (e.g. `openssl rand -hex 32`) before enabling ' +
        'host-token enforcement. Refusing to start.' };
    }
    return { level: 'warn', message:
      "INTERNAL_SECRET is the public default 'edi-internal-default'. This is acceptable only " +
      'with REQUIRE_HOST_TOKEN=false. Set a strong secret before enabling enforcement.' };
  }
  ```
- **MIRROR**: PURE_INJECTABLE_FUNCTION (inputs in, decision out, no side effects), ENV_DEFAULT_READ semantics for `isDefault`.
- **IMPORTS**: none new.
- **GOTCHA**: Keep `checkSecretConfig` free of `process.env` and `process.exit` so it stays unit-testable and deterministic (coding-standards: DI over globals). Update the top-of-file lockstep warning header to note the guard is also mirrored in `Server/server.js`.
- **VALIDATE**: `node --check web-app/src/hostToken.js`.

### Task 2: Invoke guard at web-app boot in `web-app/src/index.js`
- **ACTION**: Read enforcement flag, run guard before `app.listen`, act on level.
- **IMPLEMENT**:
  ```js
  import { checkSecretConfig } from './hostToken.js';
  // ...after PORT is defined, before app.listen:
  const REQUIRE_HOST_TOKEN = (process.env.REQUIRE_HOST_TOKEN || 'false').toLowerCase() === 'true';
  const secretCheck = checkSecretConfig({ secret: process.env.INTERNAL_SECRET, requireHostToken: REQUIRE_HOST_TOKEN });
  if (secretCheck.level === 'fatal') { console.error(`[Auth] FATAL: ${secretCheck.message}`); process.exit(1); }
  if (secretCheck.level === 'warn') { console.warn(`[Auth] WARNING: ${secretCheck.message}`); }
  ```
- **MIRROR**: AUTH_LOG_PREFIX (`[Auth]`), FATAL_EXIT (`process.exit`), ENV_DEFAULT_READ (flag parse).
- **IMPORTS**: `import { checkSecretConfig } from './hostToken.js';`
- **GOTCHA**: Run the guard *before* `app.listen(...)` so a fatal config never binds the port. `import` must sit with the other top imports (ESM hoists, but keep it tidy).
- **VALIDATE**: `node --check web-app/src/index.js`; manual boot cases in Validation Commands.

### Task 3: Mirror the guard (CJS) into `Server/server.js`
- **ACTION**: Add a CJS copy of `DEFAULT_INTERNAL_SECRET` + `checkSecretConfig`, invoke before `server.listen`.
- **IMPLEMENT**: Add near the env reads (after line 13):
  ```js
  const DEFAULT_INTERNAL_SECRET = 'edi-internal-default';
  // Boot guard — MUST match web-app/src/hostToken.js checkSecretConfig in lockstep.
  function checkSecretConfig({ secret, requireHostToken }) {
    const isDefault = !secret || secret === DEFAULT_INTERNAL_SECRET;
    if (!isDefault) return { level: 'ok', message: '' };
    if (requireHostToken) {
      return { level: 'fatal', message:
        'REQUIRE_HOST_TOKEN=true but INTERNAL_SECRET is unset or the public default. ' +
        'Set a strong random INTERNAL_SECRET (e.g. `openssl rand -hex 32`) before enabling ' +
        'host-token enforcement. Refusing to start.' };
    }
    return { level: 'warn', message:
      "INTERNAL_SECRET is the public default 'edi-internal-default'. This is acceptable only " +
      'with REQUIRE_HOST_TOKEN=false. Set a strong secret before enabling enforcement.' };
  }
  ```
  Then immediately before `server.listen(PORT, ...)` (line 594):
  ```js
  const secretCheck = checkSecretConfig({ secret: process.env.INTERNAL_SECRET, requireHostToken: REQUIRE_HOST_TOKEN });
  if (secretCheck.level === 'fatal') { console.error(`[Auth] FATAL: ${secretCheck.message}`); process.exit(1); }
  if (secretCheck.level === 'warn') { console.warn(`[Auth] WARNING: ${secretCheck.message}`); }
  ```
- **MIRROR**: LOCKSTEP_DUPLICATION (add a "MUST match … in lockstep" comment like verifyHostToken), AUTH_LOG_PREFIX, FATAL_EXIT.
- **IMPORTS**: none (`REQUIRE_HOST_TOKEN` already defined at line 13; uses `process.env` directly to catch unset).
- **GOTCHA**: `Server/server.js` reads `INTERNAL_SECRET` at line 10 with the `|| default` fallback — do NOT reuse that resolved constant for the guard (it already collapsed unset→default). Pass raw `process.env.INTERNAL_SECRET` so "unset" is distinguishable and still flagged. Guard MUST run before `server.listen`.
- **VALIDATE**: `node --check Server/server.js`.

### Task 4: Unit-test the decision matrix in `web-app/__tests__/host-token.test.js`
- **ACTION**: Add a `describe('checkSecretConfig')` block covering all four cases.
- **IMPLEMENT**:
  ```js
  import { checkSecretConfig, DEFAULT_INTERNAL_SECRET } from '../src/hostToken.js';

  describe('checkSecretConfig', () => {
    it('is fatal when enforcement is on and secret is the default', () => {
      expect(checkSecretConfig({ secret: DEFAULT_INTERNAL_SECRET, requireHostToken: true }).level).toBe('fatal');
    });
    it('is fatal when enforcement is on and secret is unset', () => {
      expect(checkSecretConfig({ secret: undefined, requireHostToken: true }).level).toBe('fatal');
    });
    it('is fatal when enforcement is on and secret is empty string', () => {
      expect(checkSecretConfig({ secret: '', requireHostToken: true }).level).toBe('fatal');
    });
    it('warns when enforcement is off but secret is default', () => {
      expect(checkSecretConfig({ secret: DEFAULT_INTERNAL_SECRET, requireHostToken: false }).level).toBe('warn');
    });
    it('is ok when a strong secret is set, regardless of enforcement', () => {
      expect(checkSecretConfig({ secret: 's3cr3t-random', requireHostToken: true }).level).toBe('ok');
      expect(checkSecretConfig({ secret: 's3cr3t-random', requireHostToken: false }).level).toBe('ok');
    });
  });
  ```
- **MIRROR**: TEST_STRUCTURE (vitest `describe`/`it`, pure inputs — no env, no clock).
- **IMPORTS**: extend the existing import from `../src/hostToken.js`.
- **GOTCHA**: Do NOT set `process.env` in tests (coding-standards: no shared mutable global state, determinism). Pass `secret`/`requireHostToken` explicitly — that is exactly why the function is pure.
- **VALIDATE**: `cd web-app && npx vitest run` — expect existing 8 + new 5 = 13 hostToken assertions green, 0 regressions.

### Task 5: Document the strong-secret requirement in `.env.example` files
- **ACTION**: Add `REQUIRE_HOST_TOKEN`, `HOST_TOKEN_TTL_MS`, and a strong-secret warning comment to `Server/.env.example` and `web-app/.env.example`; add secret guidance to `Deploy/.env.example`.
- **IMPLEMENT** (append/adjust; keep existing keys):
  ```dotenv
  # SECURITY: When REQUIRE_HOST_TOKEN=true, INTERNAL_SECRET MUST be a strong random
  # value shared by web-app and Server. Servers refuse to boot with the default here.
  # Generate with: openssl rand -hex 32
  INTERNAL_SECRET=change-me-to-a-random-secret
  # Host-token enforcement gate for create_room. Leave false until Phase 2.
  REQUIRE_HOST_TOKEN=false
  # Host-token lifetime in ms (default 300000 = 5 min).
  HOST_TOKEN_TTL_MS=300000
  ```
- **MIRROR**: existing `.env.example` comment style (Deploy uses `#` section headers).
- **IMPORTS**: n/a.
- **GOTCHA**: ⚠ These files are under a **permission-denied directory** for the editor (Read/Edit blocked; the prior review recorded this exact block). If Edit/Write is refused, surface it and either (a) ask the user to grant access or run the edit via `!`, or (b) hand the user the exact diff to paste. Do NOT silently skip — the `.env.example` doc is part of MEDIUM-1's fix ("document that a strong random INTERNAL_SECRET is mandatory").
- **VALIDATE**: `cat Server/.env.example web-app/.env.example Deploy/.env.example` shows the new keys/comments.

---

## Testing Strategy

### Unit Tests
| Test | Input | Expected Output | Edge Case? |
|---|---|---|---|
| fatal on default + enforce | `{secret:'edi-internal-default', requireHostToken:true}` | `level:'fatal'` | yes |
| fatal on unset + enforce | `{secret:undefined, requireHostToken:true}` | `level:'fatal'` | yes (unset vs default) |
| fatal on empty-string + enforce | `{secret:'', requireHostToken:true}` | `level:'fatal'` | yes (falsy edge) |
| warn on default + no enforce | `{secret:'edi-internal-default', requireHostToken:false}` | `level:'warn'` | yes |
| ok on strong + enforce | `{secret:'s3cr3t-random', requireHostToken:true}` | `level:'ok'` | no |
| ok on strong + no enforce | `{secret:'s3cr3t-random', requireHostToken:false}` | `level:'ok'` | no |

### Edge Cases Checklist
- [x] Empty/unset secret (`undefined`) treated as default → fatal when enforcing
- [x] Exact default string match → fatal when enforcing
- [x] Strong secret → always ok
- [x] Empty-string secret `''` → falsy, treated as unset (fatal when enforcing) — asserted explicitly
- [x] Flag off + weak secret → warn, not fatal (in-game Host flow must still boot)
- [x] Concurrent access — n/a (pure function, boot-time only)

---

## Validation Commands

### Static Analysis
```bash
node --check web-app/src/hostToken.js
node --check web-app/src/index.js
node --check Server/server.js
```
EXPECT: Zero syntax errors (3×).

### Unit Tests
```bash
cd web-app && npx vitest run
```
EXPECT: All pass, ≥13 hostToken assertions, no regressions vs the prior 8.

### Boot Behavior (manual, from repo root)
```bash
# 1. Fatal: enforcement on, default secret
REQUIRE_HOST_TOKEN=true node Server/server.js ; echo "exit=$?"
# EXPECT: [Auth] FATAL... ; exit=1 ; port NOT bound

# 2. Warn: flag off, default secret
node Server/server.js
# EXPECT: [Auth] WARNING... then "listening on port 8080" (Ctrl-C to stop)

# 3. Clean: strong secret + enforcement
REQUIRE_HOST_TOKEN=true INTERNAL_SECRET=$(openssl rand -hex 32) node Server/server.js
# EXPECT: no [Auth] FATAL/WARNING, listens normally (Ctrl-C to stop)

# 4. Web-app symmetric check
REQUIRE_HOST_TOKEN=true node web-app/src/index.js ; echo "exit=$?"
# EXPECT: [Auth] FATAL... ; exit=1
```

### Compose Render (config unchanged, sanity only)
```bash
docker compose -f Deploy/docker-compose.yml config >/dev/null && echo OK
```
EXPECT: `OK` (compose still valid).

### Manual Validation
- [ ] Case 1 exits 1 and never prints "listening"
- [ ] Case 2 warns but still listens
- [ ] Case 3 is silent (no `[Auth]` lines) and listens
- [ ] Web-app (Case 4) mirrors the server's fatal behavior
- [ ] `.env.example` files carry the strong-secret warning (or the block is surfaced to the user)

---

## Acceptance Criteria
- [ ] `checkSecretConfig` exists in `hostToken.js` (ESM) and is mirrored in `Server/server.js` (CJS)
- [ ] Both entry points invoke the guard **before** binding their port
- [ ] `REQUIRE_HOST_TOKEN=true` + default/unset secret → `[Auth] FATAL` + `process.exit(1)` on both services
- [ ] Flag off + default secret → `[Auth] WARNING`, server still boots (no regression to in-game Host flow)
- [ ] Strong secret → silent, normal boot
- [ ] Unit tests cover the full decision matrix and pass
- [ ] `.env.example` documents the mandatory strong secret (or the permission block is surfaced)
- [ ] `node --check` clean on all three files; no wire-format / verify logic touched

## Completion Checklist
- [ ] Code follows discovered patterns (ENV_DEFAULT_READ, AUTH_LOG_PREFIX, PURE_INJECTABLE_FUNCTION)
- [ ] Lockstep comment added to `Server/server.js` guard mirroring the verify-lockstep style
- [ ] Logging uses `[Auth]` prefix and `console.error`/`console.warn` appropriately
- [ ] Tests follow vitest pure-input convention (no `process.env`, no real clock)
- [ ] No hardcoded values beyond the intentional default-secret sentinel
- [ ] `.env.example` documentation updated (or block reported)
- [ ] No scope additions (no session-binding, no format changes)
- [ ] Self-contained — no further codebase searching needed to implement

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| `.env.example` edits blocked by permission-denied dir | High | Low | Surface the block; provide exact diff for user to paste / run via `!` |
| ESM/CJS guard copies drift over time | Medium | Medium | Add lockstep comment (mirrors existing verifyHostToken duplication warning) |
| `Server/server.js` guard reuses collapsed `INTERNAL_SECRET` const (unset→default already lost) | Medium | High | Pass **raw** `process.env.INTERNAL_SECRET` to the guard, not the line-10 constant |
| Fatal exit breaks an existing deploy that set `REQUIRE_HOST_TOKEN=true` with default secret | Low | Medium | Flag defaults off today; document in release notes that enabling now requires a real secret |
| Empty-string `INTERNAL_SECRET=''` slips through | Low | Medium | `!secret` treats `''` as default; covered by an explicit test |

## Notes
- The guard is intentionally a **pure decision function** returning `{level,message}` rather than
  calling `process.exit` itself — this keeps it unit-testable (no env, no clock, no process control),
  matching the codebase's injectable-`now` posture in `verifyHostToken`.
- This does not remove the `'edi-internal-default'` fallback (used by the pre-existing
  `/api/sessions/archive` path); it only prevents that fallback from silently becoming an
  auth boundary. Removing the fallback entirely is a larger, separate decision.
- `Server/` has no test runner (LOW-4), so the CJS copy is covered by lockstep review + the
  ESM unit tests + manual boot cases rather than a native server-side test.
