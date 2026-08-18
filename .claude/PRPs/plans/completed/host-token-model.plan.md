# Plan: Host Token Model (PRD Phase 1)

## Summary
Introduce a signed, short-lived **host token** so that becoming the game's `professor` (the only role that can create a room and trigger events) requires a credential minted by the authenticated web-app — not just clicking "Host" in the game. The web-app mints an HMAC-signed token (keyed by the existing `INTERNAL_SECRET`); the WebSocket relay verifies it locally on `create_room`. Enforcement ships behind an env flag (`REQUIRE_HOST_TOKEN`, default off) so the live in-game Host flow keeps working until Phase 2 wires the token through the Dashboard launch.

## User Story
As a **professor running a live classroom race**, I want **host authority to be tied to a credential my authenticated dashboard issues**, so that **a student on the game link cannot create a room or trigger events even by crafting a URL or sending raw WebSocket messages**.

## Problem → Solution
**Current**: Anyone who loads the Unity build at `/` can send `{"type":"create_room"}` over `/ws` and immediately becomes `professor` with full event-trigger authority — role is claimed by message type, backed by no credential (`Server/server.js:253-279`). → **Desired**: `create_room` is accepted only when accompanied by a valid, unexpired host token that the WS server can cryptographically verify was minted by the authenticated web-app. Because `professor` role is the sole gateway to all host-only broadcasts (`event_triggered`, `race_start`, etc. are relayed only when `info.role === 'professor'`, `server.js:462-515`), gating `create_room` transitively gates every host-only message.

## Metadata
- **Complexity**: Medium
- **Source PRD**: `.claude/PRPs/prds/role-bound-game-links.prd.md`
- **PRD Phase**: Phase 1 — Host token model
- **Estimated Files**: 8 (2 create, 6 update)

---

## UX Design

Internal / security change — **no user-facing UX transformation in this phase**. The Dashboard "Host Game" button, the URL plumbing, and hiding student UI all belong to Phases 2, 4, 5. In Phase 1 the token is minted and validated but not yet surfaced in any screen; with `REQUIRE_HOST_TOKEN=false` (default) the observable behavior is unchanged.

### Interaction Changes
| Touchpoint | Before | After | Notes |
|---|---|---|---|
| `POST /api/game/host-token` | does not exist | authenticated professor receives `{ token, expiresAt }` | Behind `requireAuth`; consumed by Phase 2 |
| WS `create_room` | always accepted | accepted always if flag off; if flag on, requires valid token | Backward-compatible until flag flipped |

---

## Mandatory Reading

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 | `Server/server.js` | 1-95, 240-280, 454-522 | The WS relay: `INTERNAL_SECRET` read, `create_room` case, role-gated relay. This is where verification is added. |
| P0 | `web-app/src/middleware/auth.js` | 1-28 | `requireAuth` (Bearer) + `randomBytes` token style to mirror for the mint endpoint |
| P0 | `web-app/src/routes/game-status.js` | 1-32 | `Router` at `/api/game`; where the mint endpoint is added |
| P0 | `web-app/src/routes/results.js` | 63-67 | The canonical `INTERNAL_SECRET` validation pattern (`x-internal-secret` → 403) to mirror the shared-secret idea |
| P1 | `web-app/src/routes/auth.js` | 8-55 | Route response envelope `{ success, data }` / `{ success, error }` |
| P1 | `Assets/Scripts/Network/NetworkMessages.cs` | 17-31 | `CreateRoomMessage` shape (`JsonUtility`-serializable) — add `hostToken` field |
| P1 | `Assets/Scripts/Network/NetworkManager.cs` | 136-169 | `CreateRoom()` builds + sends the message; add optional token param |
| P1 | `web-app/__tests__/auth.test.js` | 1-40 | vitest test style (no supertest; hand-rolled stubs) |
| P2 | `web-app/__tests__/test-helpers.js` | all | `createTestDb` / `createTestUser` helpers (fixture convention) |
| P2 | `Deploy/docker-compose.yml` | 17-69 | Where `INTERNAL_SECRET` env is injected into both services |

## External Documentation

No external research needed — the token uses Node's built-in `crypto` (HMAC-SHA256, `timingSafeEqual`), already available in both runtimes. No new dependency (the codebase deliberately avoids `jsonwebtoken` and `dotenv`).

| Topic | Source | Key Takeaway |
|---|---|---|
| Node HMAC | `crypto.createHmac('sha256', secret)` (built-in) | Same API in CJS (`require('crypto')`) and ESM (`import`) |
| Constant-time compare | `crypto.timingSafeEqual(a, b)` | Requires equal-length Buffers; wrap in try/catch and length-check first |

---

## Patterns to Mirror

### NAMING_CONVENTION (env + default, read identically in both processes)
```js
// SOURCE: Server/server.js:74  &  web-app/src/routes/results.js:63
const INTERNAL_SECRET = process.env.INTERNAL_SECRET || 'edi-internal-default';
```

### SHARED_SECRET_VALIDATION (consumer side → 403)
```js
// SOURCE: web-app/src/routes/results.js:63-67
const INTERNAL_SECRET = process.env.INTERNAL_SECRET || 'edi-internal-default';
router.post('/sessions/archive', (req, res) => {
  if (req.headers['x-internal-secret'] !== INTERNAL_SECRET) {
    return res.status(403).json({ success: false, error: 'Forbidden' });
  }
  // ...
});
```

### AUTH_MIDDLEWARE (Bearer, opaque token)
```js
// SOURCE: web-app/src/middleware/auth.js:16-28
export function requireAuth(req, res, next) {
  const header = req.headers.authorization;
  if (!header || !header.startsWith('Bearer ')) {
    return res.status(401).json({ success: false, error: 'Authentication required' });
  }
  const token = header.slice(7);
  const session = sessions.get(token);
  if (!session) {
    return res.status(401).json({ success: false, error: 'Invalid or expired session' });
  }
  req.user = session;
  next();
}
```

### ROUTE_RESPONSE_ENVELOPE
```js
// SOURCE: web-app/src/routes/auth.js:31-34, 50-54
res.status(201).json({ success: true, data: { token, user: {...} } });
// error:
return res.status(400).json({ success: false, error: 'Email and password required' });
```

### WS_ERROR_TO_CLIENT
```js
// SOURCE: Server/server.js:285, 440  (sendJSON guards readyState===1, :25-29)
sendJSON(ws, { type: 'error', message: 'Room not found' });
sendJSON(ws, { type: 'error', message: 'Not authorized' });
```

### WS_CREATE_ROOM_CASE (where the gate goes)
```js
// SOURCE: Server/server.js:253-279
case 'create_room': {
  const roomCode = generateRoomCode();
  rooms.set(roomCode, { professor: ws, students: new Set(), /* ... */ });
  const clientInfo = { roomCode, role: 'professor', sessionId: msg.sessionId || null };
  clientRooms.set(ws, clientInfo);
  if (msg.sessionId) sessions.set(msg.sessionId, { roomCode, role: 'professor' });
  sendJSON(ws, { type: 'room_created', roomCode });
  console.log(`[Room ${roomCode}] Created${msg.sessionId ? ` (session: ${msg.sessionId})` : ''}`);
  break;
}
```

### UNITY_MESSAGE_CLASS (JsonUtility-serializable; concrete fields only)
```csharp
// SOURCE: Assets/Scripts/Network/NetworkMessages.cs:17-22
[Serializable]
public class CreateRoomMessage
{
    public string type = "create_room";
    public string sessionId;
}
```

### UNITY_SEND_CREATE_ROOM
```csharp
// SOURCE: Assets/Scripts/Network/NetworkManager.cs:143-144
var msg = new CreateRoomMessage { sessionId = sessionId };
Send(JsonUtility.ToJson(msg));
```

### TEST_STRUCTURE (vitest, no supertest)
```js
// SOURCE: web-app/__tests__/auth.test.js:1-20
import { describe, it, expect } from 'vitest';
import { createSession } from '../src/middleware/auth.js';

describe('Auth Middleware', () => {
  it('createSession returns a hex token', () => {
    const token = createSession(1, 'test@test.com');
    expect(typeof token).toBe('string');
    expect(token.length).toBe(64);
  });
});
```

---

## Token Wire Format (authoritative spec — both implementations MUST match byte-for-byte)

```
token   = payloadB64 + "." + sigB64
payloadB64 = base64url( utf8( JSON.stringify(payload) ) )      // no padding
payload = { "v": 1, "sid": <surveyId:int|null>, "iat": <epoch ms:int>, "exp": <epoch ms:int> }
sigB64  = base64url( HMAC_SHA256( key = INTERNAL_SECRET, msg = payloadB64 ) )   // no padding
```

- `base64url` = standard base64 with `+`→`-`, `/`→`_`, trailing `=` stripped.
- The HMAC is computed over the **payloadB64 string** (not the raw JSON), so both sides sign/verify the exact same bytes without re-serialization ambiguity.
- Verification: split on the **first** `.`; recompute `sigB64` over `payloadB64`; `timingSafeEqual` the two signature buffers (length-check first); base64url-decode + `JSON.parse` the payload; reject if `payload.v !== 1` or `payload.exp <= Date.now()`.
- TTL default 300000 ms (5 min), overridable via `HOST_TOKEN_TTL_MS`.

Example (illustrative, secret = `edi-internal-default`):
```
payload    = {"v":1,"sid":42,"iat":1750000000000,"exp":1750000300000}
token      = eyJ2IjoxLCJzaWQiOjQyLCJpYXQiOjE3NTAwMDAwMDAwMDAsImV4cCI6MTc1MDAwMDMwMDAwMH0.<sig>
```

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `web-app/src/hostToken.js` | CREATE | ESM `mintHostToken()` + `verifyHostToken()` — the web-app source of truth, fully unit-tested |
| `web-app/src/routes/game-status.js` | UPDATE | Add `POST /host-token` behind `requireAuth` to mint tokens |
| `web-app/__tests__/host-token.test.js` | CREATE | vitest coverage for mint/verify: valid, tampered, expired, wrong version |
| `Server/server.js` | UPDATE | Add CJS `verifyHostToken()` (mirror of the spec) + gate `create_room` behind `REQUIRE_HOST_TOKEN` |
| `Assets/Scripts/Network/NetworkMessages.cs` | UPDATE | Add `public string hostToken;` to `CreateRoomMessage` |
| `Assets/Scripts/Network/NetworkManager.cs` | UPDATE | `CreateRoom(string hostToken = null)` includes token in the message |
| `Server/.env.example` | UPDATE | Document `REQUIRE_HOST_TOKEN`, `HOST_TOKEN_TTL_MS` |
| `web-app/.env.example` | UPDATE | Document `HOST_TOKEN_TTL_MS` |
| `Deploy/docker-compose.yml` | UPDATE | Inject `REQUIRE_HOST_TOKEN` (default `false`) into `edi-racing` service |

## NOT Building

- The Dashboard "Host Game" button and any URL that carries the token to Unity — **Phase 2**.
- Passing the token from the page URL into the jslib WS URL / reading `window.location.search` — **Phase 2**.
- Auto-injecting survey data — **Phase 3**.
- Student link, landing page, role-locking student clients — **Phases 4-5**.
- Flipping `REQUIRE_HOST_TOKEN` to `true` in production compose — deferred until Phase 2 lands (this plan only introduces the flag, default off).
- Protecting the unauthenticated HTTP endpoints (`/api/room-status`, `/api/notify-response`) — out of scope; those are read/notify, not host-authority.
- Token revocation / rotation lists — tokens are short-lived and stateless by design.

---

## Step-by-Step Tasks

### Task 1: Create the token module (web-app, ESM)
- **ACTION**: Create `web-app/src/hostToken.js`.
- **IMPLEMENT**:
  - `import { createHmac, timingSafeEqual } from 'crypto';`
  - `const INTERNAL_SECRET = process.env.INTERNAL_SECRET || 'edi-internal-default';` (mirror the exact default).
  - `const TTL_MS = parseInt(process.env.HOST_TOKEN_TTL_MS || '300000', 10);`
  - Helper `b64url(buf)` → `buf.toString('base64').replace(/\+/g,'-').replace(/\//g,'_').replace(/=+$/,'')`; and `b64urlDecode(str)` → pad and `Buffer.from(..., 'base64')`.
  - `sign(payloadB64)` → `b64url(createHmac('sha256', INTERNAL_SECRET).update(payloadB64).digest())`.
  - `export function mintHostToken(surveyId = null, now = Date.now())` → build payload `{ v:1, sid: surveyId, iat: now, exp: now + TTL_MS }`, `payloadB64 = b64url(Buffer.from(JSON.stringify(payload)))`, return `{ token: payloadB64 + '.' + sign(payloadB64), expiresAt: payload.exp }`.
  - `export function verifyHostToken(token, now = Date.now())` → returns `{ valid: boolean, surveyId?, error? }`. Steps: guard non-string/empty; split on first `.` (`const i = token.indexOf('.')`); recompute sig; `timingSafeEqual` inside try/catch with length guard; parse payload; check `v===1` and `exp > now`.
- **MIRROR**: `NAMING_CONVENTION` (env default), `TEST_STRUCTURE` for exports.
- **IMPORTS**: `crypto` (built-in).
- **GOTCHA**: `timingSafeEqual` throws if buffers differ in length — compare lengths first and return `{valid:false}` rather than letting it throw. Pass `now` as a param so tests are deterministic (no real clock; matches coding-standards "no time-dependent assertions").
- **VALIDATE**: `node --input-type=module -e "import('./web-app/src/hostToken.js').then(m=>{const t=m.mintHostToken(42);console.log(m.verifyHostToken(t.token))})"` → prints `{ valid: true, surveyId: 42 }`.

### Task 2: Add the mint endpoint (web-app)
- **ACTION**: Update `web-app/src/routes/game-status.js`.
- **IMPLEMENT**: `import { requireAuth } from '../middleware/auth.js';` and `import { mintHostToken } from '../hostToken.js';`. Add:
  ```js
  // POST /api/game/host-token — issue a short-lived host credential (authenticated professor)
  router.post('/host-token', requireAuth, (req, res) => {
    const surveyId = req.body?.surveyId ?? null;
    const { token, expiresAt } = mintHostToken(surveyId);
    res.json({ success: true, data: { token, expiresAt } });
  });
  ```
- **MIRROR**: `AUTH_MIDDLEWARE`, `ROUTE_RESPONSE_ENVELOPE`.
- **IMPORTS**: as above (both ESM, relative paths with `.js`).
- **GOTCHA**: The route is mounted at `/api/game` in `index.js:34`, so the full path is `POST /api/game/host-token`. `express.json()` is already applied globally (`index.js:20`), so `req.body` is parsed. `surveyId` is optional — don't 400 if absent.
- **VALIDATE**: after `npm start`, `curl -s -XPOST localhost:3001/api/game/host-token` → `401 {"success":false,"error":"Authentication required"}`; with a valid `Authorization: Bearer <login token>` → `{"success":true,"data":{"token":"...","expiresAt":...}}`.

### Task 3: Verify + gate `create_room` on the WS server (CJS mirror)
- **ACTION**: Update `Server/server.js`.
- **IMPLEMENT**:
  - Near the top add `const crypto = require('crypto');` and constants: `const INTERNAL_SECRET = process.env.INTERNAL_SECRET || 'edi-internal-default';` (note: `INTERNAL_SECRET` is currently declared *inside* `destroyRoom` at `:74` — hoist a module-level const and reuse it there to avoid duplication) and `const REQUIRE_HOST_TOKEN = (process.env.REQUIRE_HOST_TOKEN || 'false').toLowerCase() === 'true';`.
  - Add a `verifyHostToken(token, now = Date.now())` function that is a **byte-for-byte mirror** of the spec in Task 1 (same `b64url`, same HMAC over `payloadB64`, same `timingSafeEqual` + length guard, same `v===1` + `exp>now` checks). Return `{ valid, surveyId, error }`.
  - In `case 'create_room':`, **before** generating the room, add:
    ```js
    if (REQUIRE_HOST_TOKEN) {
      const result = verifyHostToken(msg.hostToken);
      if (!result.valid) {
        sendJSON(ws, { type: 'error', message: 'Host authorization required' });
        console.log(`[Auth] Rejected create_room (${result.error || 'no token'})`);
        return;
      }
    }
    ```
- **MIRROR**: `WS_ERROR_TO_CLIENT`, `WS_CREATE_ROOM_CASE`, the token spec.
- **IMPORTS**: `crypto` (built-in, CommonJS `require`).
- **GOTCHA**: Server is **CommonJS** (`require`, not `import`) — do not copy the ESM import lines from Task 1. `Date.now()`/`Math.random()` are fine in the running server (the no-`Date.now()` rule applies only to Workflow scripts, not to app runtime code). Keep the flag default **off** so existing in-game Host and current deployments are unaffected until Phase 2. The relay `default:` branch needs **no** change — `event_triggered` is already gated by `role === 'professor'`, and role is only granted by a successful `create_room`.
- **VALIDATE**: Start server with `REQUIRE_HOST_TOKEN=true`. Send `{"type":"create_room"}` (no token) via `wscat`/a small ws script → receive `{"type":"error","message":"Host authorization required"}`, no `room_created`. Send `{"type":"create_room","hostToken":"<minted>"}` → receive `{"type":"room_created","roomCode":"..."}`. With flag unset, both are accepted (legacy).

### Task 4: Add `hostToken` to the Unity create-room message
- **ACTION**: Update `Assets/Scripts/Network/NetworkMessages.cs`.
- **IMPLEMENT**: add a field to `CreateRoomMessage`:
  ```csharp
  [Serializable]
  public class CreateRoomMessage
  {
      public string type = "create_room";
      public string sessionId;
      public string hostToken;   // set when launched from the professor Dashboard (Phase 2)
  }
  ```
- **MIRROR**: `UNITY_MESSAGE_CLASS`.
- **IMPORTS**: none.
- **GOTCHA**: `JsonUtility` serializes an empty/`null` string field as `"hostToken":""` — harmless: the server only checks it when `REQUIRE_HOST_TOKEN` is on, and Phase 2 supplies a real value. Do not use a nullable or property; `JsonUtility` only serializes public concrete fields.
- **VALIDATE**: EditMode compile passes; `JsonUtility.ToJson(new CreateRoomMessage{sessionId="abc"})` contains `"type":"create_room"` and a `hostToken` key.

### Task 5: Plumb an optional token through `CreateRoom()`
- **ACTION**: Update `Assets/Scripts/Network/NetworkManager.cs`.
- **IMPLEMENT**: change signature to `public void CreateRoom(string hostToken = null)` and include it:
  ```csharp
  pendingAction = () =>
  {
      IsHost = true;
      var msg = new CreateRoomMessage { sessionId = sessionId, hostToken = hostToken };
      Send(JsonUtility.ToJson(msg));
  };
  ```
- **MIRROR**: `UNITY_SEND_CREATE_ROOM`.
- **IMPORTS**: none.
- **GOTCHA**: Keep the parameter **optional** so the existing caller `SetupScreen.HostRoom()` (`SetupScreen.cs:134-140`) still compiles unchanged — it passes no token, which is fine while the flag is off. Phase 2 will change that caller to pass a token read from the URL. Do NOT touch the reconnect path (`rejoin_room` uses `sessionId`, not the token — professor rejoin is already transitively protected because a professor session only exists after a token-gated `create_room`).
- **VALIDATE**: EditMode compile passes; existing `SetupScreen` still builds.

### Task 6: Env + compose documentation
- **ACTION**: Update `Server/.env.example`, `web-app/.env.example`, `Deploy/docker-compose.yml`.
- **IMPLEMENT**:
  - `Server/.env.example`: add `REQUIRE_HOST_TOKEN=false` and `HOST_TOKEN_TTL_MS=300000` with a comment.
  - `web-app/.env.example`: add `HOST_TOKEN_TTL_MS=300000` (mint side reads TTL).
  - `Deploy/docker-compose.yml`: under the `edi-racing` service env block (near `INTERNAL_SECRET`, `:26-29`) add `REQUIRE_HOST_TOKEN=${REQUIRE_HOST_TOKEN:-false}`.
- **MIRROR**: existing `${INTERNAL_SECRET:-edi-internal-default}` substitution style.
- **IMPORTS**: none.
- **GOTCHA**: Both services must resolve the **same** `INTERNAL_SECRET` for HMAC to verify across processes — this already holds (both read `INTERNAL_SECRET` with the same default, compose injects the same value into both services). Do not introduce a separate signing secret.
- **VALIDATE**: `docker compose config` renders without error and shows `REQUIRE_HOST_TOKEN` on the game service.

### Task 7: Unit tests (web-app, vitest)
- **ACTION**: Create `web-app/__tests__/host-token.test.js`.
- **IMPLEMENT**: `import { describe, it, expect } from 'vitest';` and `import { mintHostToken, verifyHostToken } from '../src/hostToken.js';`. Cases:
  1. round-trip: `mintHostToken(42)` → `verifyHostToken(token)` = `{ valid:true, surveyId:42 }`.
  2. `surveyId` null default preserved.
  3. tampered payload (flip a char in `payloadB64`) → `valid:false`.
  4. tampered signature → `valid:false`.
  5. expired: `mintHostToken(1, 1000)` then `verifyHostToken(token, 1000 + 300000 + 1)` → `valid:false`.
  6. malformed inputs: `''`, `null`, `'abc'` (no dot), `'a.b.c'` → `valid:false`, no throw.
  7. wrong version: hand-build a token with payload `v:2` re-signed → `valid:false`.
- **MIRROR**: `TEST_STRUCTURE`.
- **IMPORTS**: as above.
- **GOTCHA**: Pass explicit `now`/`iat` values (params from Task 1) so tests are deterministic — no real-clock reads (coding-standards: deterministic, no time-dependent assertions).
- **VALIDATE**: `cd web-app && npm test` → the new file's cases pass, no regressions in `auth.test.js`/`db.test.js`.

---

## Testing Strategy

### Unit Tests
| Test | Input | Expected Output | Edge Case? |
|---|---|---|---|
| round-trip valid | `mint(42)` → `verify(token, iatWindow)` | `{valid:true, surveyId:42}` | No |
| null surveyId | `mint()` | `verify` → `{valid:true, surveyId:null}` | No |
| tampered payload | flip char in payloadB64 | `{valid:false}` | Yes |
| tampered signature | flip char in sigB64 | `{valid:false}` | Yes |
| expired | `verify(token, exp+1)` | `{valid:false}` | Yes |
| no dot / empty / null | `'abc'`, `''`, `null` | `{valid:false}`, no throw | Yes |
| wrong version | payload `v:2` re-signed | `{valid:false}` | Yes |

### Edge Cases Checklist
- [x] Empty input (`''`, `null`) → `{valid:false}`, no throw
- [x] Malformed token (no `.`, extra `.`) → handled via `indexOf('.')` split
- [x] Expired token → `exp <= now` rejected
- [x] Tampered payload/signature → HMAC mismatch rejected
- [x] Cross-process compatibility → both sides key off the same `INTERNAL_SECRET`
- [ ] Concurrent access → N/A (stateless, pure function)
- [x] Permission denied → `create_room` without token (flag on) → `{type:'error'}`, `POST /host-token` without Bearer → 401

---

## Validation Commands

### Static Analysis
```bash
# web-app has no linter/tsc; syntax check the new/edited files:
node --check web-app/src/hostToken.js
node --check web-app/src/routes/game-status.js
node --check Server/server.js
```
EXPECT: no output (syntax OK)

### Unit Tests
```bash
cd web-app && npm test
```
EXPECT: all `host-token.test.js` cases pass; `auth.test.js` + `db.test.js` still green

### WS enforcement smoke (manual, flag on)
```bash
# terminal A
REQUIRE_HOST_TOKEN=true PORT=8080 node Server/server.js
# terminal B — mint a token via node, then connect with/without it (small ws script or wscat)
node --input-type=module -e "import('./web-app/src/hostToken.js').then(m=>console.log(m.mintHostToken(1).token))"
```
EXPECT: `create_room` with no/invalid token → `{"type":"error","message":"Host authorization required"}`; with the minted token → `{"type":"room_created",...}`

### Unity compile
```bash
# via UnitySkills API (http://localhost:8090) if available, else open the project; EditMode tests:
# Assets/Tests/EditMode compiles; existing NetworkMessagesTests pass
```
EXPECT: no compile errors; existing tests unaffected

### Docker config
```bash
cd Deploy && docker compose config >/dev/null && echo OK
```
EXPECT: `OK`, `REQUIRE_HOST_TOKEN` present on `edi-racing`

### Manual Validation
- [ ] With `REQUIRE_HOST_TOKEN` unset, the current in-game Host button still creates a room (no regression).
- [ ] With `REQUIRE_HOST_TOKEN=true`, raw `create_room` without a token is rejected; a token minted by `/api/game/host-token` (authenticated) is accepted.
- [ ] A token older than TTL is rejected.

---

## Acceptance Criteria
- [ ] `POST /api/game/host-token` requires auth and returns a `{ token, expiresAt }` envelope.
- [ ] `verifyHostToken` in both web-app (ESM) and `Server/server.js` (CJS) accept the same token and agree on valid/invalid for every test vector.
- [ ] With `REQUIRE_HOST_TOKEN=true`, `create_room` without a valid token is rejected with `{type:'error'}` and no room is created; with a valid token it succeeds.
- [ ] With the flag off (default), behavior is unchanged (in-game Host still works).
- [ ] `event_triggered`/host broadcasts remain reachable only to a socket that became `professor` via a gated `create_room` (verified: no new relay path added).
- [ ] All validation commands pass; new tests green; no regressions.

## Completion Checklist
- [ ] Code follows discovered patterns (env default, `{success,data}` envelope, `sendJSON({type:'error'})`, snake_case WS types)
- [ ] Error handling matches codebase (`console.log('[Auth] ...')`, 401/403 JSON envelopes)
- [ ] Logging uses bracketed prefix (`[Auth]`)
- [ ] Tests follow vitest style (no supertest, deterministic `now` params)
- [ ] No hardcoded secret — reuses `INTERNAL_SECRET`
- [ ] Server CJS vs web-app ESM kept distinct (no cross-import)
- [ ] No scope creep into Phases 2-5
- [ ] Self-contained — token wire format fully specified for both implementations

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| The two `verifyHostToken` copies (CJS + ESM) drift over time | M | H (tokens silently reject) | Authoritative byte-level spec in this plan; identical test vectors; a comment in each pointing to the other |
| Flipping the flag on before Phase 2 breaks live in-game Host | M | H | Flag defaults **off**; this plan does not enable it in prod compose |
| `INTERNAL_SECRET` differs between the two services in some deploy | L | H | Compose injects the same value to both; `.env.example` documents it; smoke test verifies cross-process accept |
| `timingSafeEqual` throws on length mismatch | L | M | Length-guard before compare; wrap in try/catch |
| Token leaks in URL/history (relevant once Phase 2 puts it in a URL) | M | M | Short TTL (5 min); flagged as a Phase 2 concern in the PRD risk table |

## Notes
- **Why gate only `create_room`**: `professor` is the sole role that relays host-only messages (`server.js:462-515`), and role is assigned only on a successful `create_room` (or `rejoin_room` for an already-professor session, which itself can only exist post-`create_room`). Gating the room-creation entry point therefore transitively enforces "only the professor can trigger events" without touching the hot relay path.
- **Why HMAC-local verify instead of a web-app introspection call**: avoids a per-`create_room` network round-trip and any new coupling/endpoint on the WS server's HTTP surface; mirrors the existing shared-secret trust model (`INTERNAL_SECRET`) already used for `/api/sessions/archive`. Stateless — survives WS-server restarts (unlike the in-memory user `sessions` Map).
- **Why a feature flag**: lets Phase 1 land and be tested in isolation without breaking the deployed in-game Host flow; Phase 2 supplies the token end-to-end and Phase 5/7 flips `REQUIRE_HOST_TOKEN=true` in the production overlay.
- **Follow-ups for later phases** (not this plan): Phase 2 reads the token from the launch URL (extend `WebSocketBridge.jslib` to expose `window.location.search`, pass to `CreateRoom`), adds the Dashboard "Host Game" button that calls `POST /api/game/host-token`, and flips the enforcement flag.
