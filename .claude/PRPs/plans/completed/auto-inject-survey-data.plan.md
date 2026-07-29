# Plan: Auto-Inject Survey Data on Host Launch (Phase 3)

## Summary
When a professor launches the hosted game from the Dashboard (Phase 2), the selected survey's
responses are pushed into the game **automatically** the instant the room is created — no manual
"Send to Game" modal, no typed room code. Phase 3 is the last plumbing piece that completes the
"survey done → race live" flow.

## User Story
As a **professor running a live classroom race**, I want the **survey data to load into the game
automatically when I click "Host Game"**, so that **I never have to open the Send-to-Game modal or
type a room code in front of the class**.

## Problem → Solution
**Current:** Professor hosts in Unity → copies the 6-char room code → opens `SendToGameModal` →
types the code → clicks Send. Two apps, one hand-copied code, error-prone under time pressure.
**Desired:** Professor clicks "Host Game" (Phase 2). Unity auto-creates the room; the moment
`room_created` fires, the game itself calls the existing `send-to-game` API with the freshly-minted
room code and the survey id carried in the launch URL. Data lands in the game with zero manual steps.

## Metadata
- **Complexity**: Medium
- **Source PRD**: `.claude/PRPs/prds/role-bound-game-links.prd.md`
- **PRD Phase**: Phase 3 — Auto-inject survey data (runs parallel with Phase 2; depends on Phase 1)
- **Estimated Files**: 5 (2 new / 3 modified)

---

## UX Design

### Before
```
┌────────────────────────────────────────────────────────────┐
│ Professor (2 apps, manual code handoff):                    │
│                                                             │
│  Dashboard ──"Host Game"──▶ Unity opens, room = "ABCDEF"    │
│                                    │                        │
│         reads code off screen ◀────┘                        │
│                                    │                        │
│  Dashboard ▶ "Send to Game" modal ▶ types "ABCDEF" ▶ Send   │
│                                    │                        │
│                          survey data ──▶ game               │
└────────────────────────────────────────────────────────────┘
```

### After
```
┌────────────────────────────────────────────────────────────┐
│ Professor (1 click, zero codes):                            │
│                                                             │
│  Dashboard ──"Host Game"──▶ Unity opens                     │
│                                    │                        │
│                       room_created("ABCDEF")                │
│                                    │ (game fires internally)│
│              POST /api/surveys/{id}/send-to-game {ABCDEF}   │
│                                    │                        │
│                          survey data ──▶ game (automatic)   │
└────────────────────────────────────────────────────────────┘
```

### Interaction Changes
| Touchpoint | Before | After | Notes |
|---|---|---|---|
| Load survey data into game | Open modal, type room code, click Send | Nothing — happens on host launch | `SendToGameModal` remains as a manual fallback (re-send after edits) |
| Room code visibility to professor | Must read + retype | Not needed for injection | Still shown for the student link (Phase 4) |
| Steps "survey done → data in game" | ~4 manual actions | 0 | Meets PRD metric "0 typed room codes" |

---

## Mandatory Reading

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 (critical) | `Assets/Scripts/UI/HostLaunchBootstrap.cs` | 1-47 | The launch entry point Phase 3 extends; already parses the hash and calls `CreateRoom` |
| P0 (critical) | `Assets/Scripts/Network/NetworkManager.cs` | 42, 148-166, 283-289 | `OnRoomCreated` event + `CreateRoom` + `room_created` handler — the hook |
| P0 (critical) | `web-app/src/routes/export.js` | 253-335 | The existing `send-to-game` endpoint we reuse verbatim (web_join_room → survey_import) |
| P0 (critical) | `Server/server.js` | 524-539 | `survey_import` relay — server side, unchanged, confirms the data path |
| P1 (important) | `Assets/Plugins/WebGL/WebSocketBridge.jslib` | 5-102 | jslib `mergeInto` pattern; add the fetch helper here |
| P1 (important) | `Assets/Plugins/WebGL/WebSocketBridge.cs` | 28-31, 175-220 | DllImport + editor-guard pattern to mirror for the new bridge call |
| P1 (important) | `web-app/client/src/api.js` | 1-22, 118-124 | Auth = `Bearer` token in localStorage key `edi-survey-token`; `sendToGame(id, roomCode)` shape |
| P2 (reference) | `Assets/Scripts/UI/HostLaunchParams.cs` | 1-31 | Pure, testable parser; the `survey` key is already parsed out of the hash |
| P2 (reference) | `Assets/Tests/EditMode/HostLaunchParamsTests.cs` | all | Test style to mirror for the new pure decision helper |
| P2 (reference) | `web-app/client/src/components/SendToGameModal.jsx` | 92-113 | The manual flow being automated (kept, not removed) |

## External Documentation
No external research needed — feature reuses established internal patterns only (existing REST
endpoint, existing WS `survey_import` relay, existing jslib bridge, existing localStorage auth).

---

## Patterns to Mirror

### NAMING_CONVENTION — jslib bridge function
```javascript
// SOURCE: Assets/Plugins/WebGL/WebSocketBridge.jslib:72-101
  WebSocketBridge_GetPageUrl: function() {
    var url = window.location.href;
    var buffer = _malloc(lengthBytesUTF8(url) + 1);
    stringToUTF8(url, buffer, lengthBytesUTF8(url) + 1);
    return buffer;
  },
  WebSocketBridge_LocalStorageGet: function(keyPtr) {
    var key = UTF8ToString(keyPtr);
    var val = window.localStorage.getItem(key) || "";
    // ...marshal back to C#
  },
  WebSocketBridge_ClearUrlHash: function() {
    history.replaceState(null, "", window.location.pathname + window.location.search);
  },
// mergeInto(LibraryManager.library, WebSocketBridgeLib);  <-- append new fn inside this object
```

### DLLIMPORT_WRAPPER — C# bridge with editor no-op guard
```csharp
// SOURCE: Assets/Plugins/WebGL/WebSocketBridge.cs:176-220
    [DllImport("__Internal")] private static extern string WebSocketBridge_LocalStorageGet(string key);
    [DllImport("__Internal")] private static extern void   WebSocketBridge_ClearUrlHash();

    public static string StorageGet(string key)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return WebSocketBridge_LocalStorageGet(key);
#else
        return "";   // editor/standalone fallback — no browser
#endif
    }
```

### EVENT_HOOK — subscribing to room creation
```csharp
// SOURCE: Assets/Scripts/Network/NetworkManager.cs:42, 283-289
    public event Action<string> OnRoomCreated;
    // ...
            case "room_created":
                var rc = JsonUtility.FromJson<RoomCreatedMessage>(raw);
                RoomCode = rc.roomCode;
                lastRoomCode = rc.roomCode;
                PersistSession(rc.roomCode, true);
                OnRoomCreated?.Invoke(RoomCode);   // <-- Phase 3 subscribes here
```

### LAUNCH_BOOTSTRAP — hash parse + role gate (the file to extend)
```csharp
// SOURCE: Assets/Scripts/UI/HostLaunchBootstrap.cs:27-38
        var p = HostLaunchParams.ParseHash(WebSocketBridge.GetPageUrl());
        if (p.TryGetValue("role", out var role) && role == "host")
        {
            p.TryGetValue("token", out var token);
            NetworkManager.CreateRoom(token);
            WebSocketBridge.ClearUrlHash();
            RaceUI.SetRoleFromNetwork(true);
            return;
        }
```

### PURE_TESTABLE_HELPER — split logic out of MonoBehaviour for EditMode tests
```csharp
// SOURCE: Assets/Scripts/UI/HostLaunchParams.cs:9-30 (pattern to mirror for the new decision helper)
public static class HostLaunchParams
{
    public static Dictionary<string, string> ParseHash(string url) { /* never throws */ }
}
```

### SERVER_RELAY — survey_import is untouched (confirms end-to-end path)
```javascript
// SOURCE: Server/server.js:524-539  (NO CHANGE — reference only)
      case 'survey_import': {
        const webInfo = clientRooms.get(ws);
        if (!webInfo || webInfo.role !== 'webapp') { /* reject */ }
        const importRoom = rooms.get(webInfo.roomCode);
        if (!importRoom || !importRoom.professor || importRoom.professor.readyState !== 1) {
          sendJSON(ws, { type: 'survey_import_ack', success: false, error: 'Professor not connected' });
          return;
        }
        importRoom.professor.send(data.toString());   // relayed to the Unity host
        sendJSON(ws, { type: 'survey_import_ack', success: true });
      }
```

---

## Architecture Decision

**Approach — Design A: client-triggered reuse of the existing `send-to-game` endpoint.**
On `NetworkManager.OnRoomCreated`, the Unity WebGL host calls a new jslib fetch helper that POSTs to
`/api/surveys/{surveyId}/send-to-game` with `{ roomCode }` and the professor's `Bearer` token (read
from localStorage `edi-survey-token`). The web-app endpoint runs the **existing** `web_join_room` →
`survey_import` sequence; the game server relays the survey data to the Unity host exactly as the
manual modal does today. **No server-side (`Server/server.js`) or web-app route changes.**

Why this is safe & minimal:
- The endpoint is `requireAuth` → only the authenticated professor (whose token is in this browser)
  can trigger it; a student browser has no token. Injection authority rides the existing auth.
- Unity **already** handles the inbound `survey_import` message (the manual flow ships today —
  `NetworkMessages.cs:227` `SurveyImportMessage`, handled in `NetworkSync.cs`). No new inbound Unity code.
- Same-origin in production (game `/`, api `/api` behind nginx), so a **relative** `/api/...` fetch
  needs no base URL. In Editor/Standalone `GetPageUrl()` returns `""` so nothing auto-hosts —
  auto-inject never runs off-browser.

**Alternatives Considered**
- **Design B — server-side auto-inject at `create_room`.** The host token payload already carries
  `sid` (surveyId; `Server/server.js:41-85`), so the game server could fetch the export from the
  web-app via `INTERNAL_SECRET` and set `room.surveyData` directly. *Rejected for Phase 3*: adds a new
  web-app internal route + an outbound HTTP client in the game server, and does **not** reuse the
  `survey_import` path the PRD names. Its one advantage — surveyId bound to the *verified* token
  rather than a client-supplied hash param — is noted below as optional future hardening.
- **Dashboard-side polling / postMessage to learn the room code.** Fragile cross-tab signalling;
  the game already knows its own room code at `room_created`, so let the game make the call.

**Scope (WILL build)**
- Read the `survey` id from the launch hash in `HostLaunchBootstrap` (already parsed, currently unused).
- One-shot subscribe to `OnRoomCreated`; on fire, call the new bridge → POST `send-to-game`.
- New jslib `WebSocketBridge_HostAutoInject` (fire-and-forget fetch with console logging) + C# wrapper.
- A pure `HostAutoInjectDecision` helper (should-inject: role==host && non-empty survey) + EditMode test.

**NOT Building**
- Any change to `Server/server.js` or the `survey_import` relay.
- Any new/changed web-app REST route (we call the existing `send-to-game`).
- Removing or altering `SendToGameModal.jsx` — it stays as the manual re-send fallback (PRD open
  question: re-send after survey edited mid-race).
- **Auto-starting the race / showing `EventPanel` on launch** — Phase 3 only makes the *data* present;
  the professor still starts the race. EventPanel-on-Racing is a Phase 2/7 concern.
- Cross-checking the hash `survey` against the token's `sid` (accepted residual; see Risks).
- Re-injecting on a host *reload* (resume path does not fire `OnRoomCreated`; see Gotcha in Task 3).

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `Assets/Scripts/UI/HostAutoInjectDecision.cs` | CREATE | Pure, testable "should auto-inject?" logic (mirrors `HostLaunchParams` split) |
| `Assets/Tests/EditMode/HostAutoInjectDecisionTests.cs` | CREATE | EditMode tests for the decision helper |
| `Assets/Scripts/UI/HostLaunchBootstrap.cs` | UPDATE | Read `survey` from hash; subscribe `OnRoomCreated` → call bridge |
| `Assets/Plugins/WebGL/WebSocketBridge.jslib` | UPDATE | Add `WebSocketBridge_HostAutoInject` fetch helper |
| `Assets/Plugins/WebGL/WebSocketBridge.cs` | UPDATE | DllImport + `HostAutoInject(surveyId, roomCode)` wrapper with editor no-op |

---

## Step-by-Step Tasks

### Task 1: Pure decision helper `HostAutoInjectDecision`
- **ACTION**: Create `Assets/Scripts/UI/HostAutoInjectDecision.cs`.
- **IMPLEMENT**: `public static class HostAutoInjectDecision` with
  `public static bool ShouldAutoInject(string role, string surveyId)` returning
  `role == "host" && !string.IsNullOrWhiteSpace(surveyId)`. No `UnityEngine` dependency.
- **MIRROR**: PURE_TESTABLE_HELPER (`HostLaunchParams.cs`).
- **IMPORTS**: none (plain C#; keep it in the same asmdef as `HostLaunchParams`).
- **GOTCHA**: Do not throw on null — return `false`. This keeps parity with the never-throw parser.
- **VALIDATE**: Covered by Task 5 tests; compiles under the UI asmdef.

### Task 2: jslib fetch helper `WebSocketBridge_HostAutoInject`
- **ACTION**: Add a function to the `WebSocketBridgeLib` object in
  `Assets/Plugins/WebGL/WebSocketBridge.jslib` (inside the `mergeInto` object, before line 102).
- **IMPLEMENT**:
  ```javascript
  WebSocketBridge_HostAutoInject: function(surveyIdPtr, roomCodePtr) {
    var surveyId = UTF8ToString(surveyIdPtr);
    var roomCode = UTF8ToString(roomCodePtr);
    var token = window.localStorage.getItem("edi-survey-token") || "";
    var headers = { "Content-Type": "application/json" };
    if (token) headers["Authorization"] = "Bearer " + token;
    fetch("/api/surveys/" + encodeURIComponent(surveyId) + "/send-to-game", {
      method: "POST",
      headers: headers,
      body: JSON.stringify({ roomCode: roomCode })
    }).then(function(r) {
      if (!r.ok) console.warn("[HostAutoInject] send-to-game failed: HTTP " + r.status);
    }).catch(function(e) {
      console.warn("[HostAutoInject] send-to-game error", e);
    });
  },
  ```
- **MIRROR**: NAMING_CONVENTION (jslib) — same `UTF8ToString` marshalling and `mergeInto` placement.
- **IMPORTS**: none (browser globals `fetch`, `window.localStorage`).
- **GOTCHA**: Relative URL `/api/...` only works same-origin — correct in production (nginx serves
  game + api on one origin, `Deploy/nginx/nginx.conf`). Do **not** hardcode `localhost`. Fire-and-forget:
  do not try to marshal the response back to C# (WebGL async → C# is awkward and unneeded here).
- **VALIDATE**: `asset_refresh` via UnitySkills → zero console errors; jslib only compiles into WebGL builds.

### Task 3: Wire the trigger in `HostLaunchBootstrap`
- **ACTION**: Extend `Assets/Scripts/UI/HostLaunchBootstrap.cs` `Start()`.
- **IMPLEMENT**:
  - After parsing `p`, read `p.TryGetValue("survey", out var surveyId);`.
  - In the `role == "host"` branch, **before** `NetworkManager.CreateRoom(token)`, if
    `HostAutoInjectDecision.ShouldAutoInject(role, surveyId)` then subscribe a **one-shot** handler:
    ```csharp
    void OnCreated(string roomCode)
    {
        NetworkManager.OnRoomCreated -= OnCreated;      // one-shot: never re-inject
        Debug.Log($"[HostLaunchBootstrap] Auto-injecting survey {surveyId} into room {roomCode}.");
        WebSocketBridge.HostAutoInject(surveyId, roomCode);
    }
    NetworkManager.OnRoomCreated += OnCreated;
    ```
  - Keep the existing `CreateRoom(token)` → `ClearUrlHash()` → `SetRoleFromNetwork(true)` sequence.
- **MIRROR**: EVENT_HOOK + LAUNCH_BOOTSTRAP.
- **IMPORTS**: none new (same assembly as `NetworkManager`, `WebSocketBridge`).
- **GOTCHA**: Subscribe **before** `CreateRoom` so the handler is attached when `room_created` returns.
  The one-shot unsubscribe means the **resume-on-reload** path (`ResumeHostSession`, which sends
  `rejoin_room` and yields `reconnect_state`, *not* `room_created`) will **not** re-inject — this is
  intended: the game server has already cached `room.surveyData`. Reads `survey` from the hash **before**
  `ClearUrlHash()` runs.
- **VALIDATE**: `asset_refresh` → zero errors; logic path covered by Task 5 decision tests.

### Task 4: C# bridge wrapper `HostAutoInject`
- **ACTION**: Add DllImport + wrapper in `Assets/Plugins/WebGL/WebSocketBridge.cs`.
- **IMPLEMENT**:
  ```csharp
  [DllImport("__Internal")] private static extern void WebSocketBridge_HostAutoInject(string surveyId, string roomCode);

  public static void HostAutoInject(string surveyId, string roomCode)
  {
  #if UNITY_WEBGL && !UNITY_EDITOR
      WebSocketBridge_HostAutoInject(surveyId, roomCode);
  #else
      Debug.Log($"[WebSocketBridge] HostAutoInject noop (editor): survey={surveyId}, room={roomCode}");
  #endif
  }
  ```
- **MIRROR**: DLLIMPORT_WRAPPER (`StorageGet`/`ClearUrlHash`).
- **IMPORTS**: existing (`System.Runtime.InteropServices`, `UnityEngine`).
- **GOTCHA**: Editor/Standalone must be a no-op (the `__Internal` symbol only exists in WebGL). Keep
  the `#if UNITY_WEBGL && !UNITY_EDITOR` guard identical to the existing wrappers.
- **VALIDATE**: `asset_refresh` → zero errors across all 3 asmdefs.

### Task 5: EditMode tests for the decision helper
- **ACTION**: Create `Assets/Tests/EditMode/HostAutoInjectDecisionTests.cs`.
- **IMPLEMENT**: NUnit `[Test]` cases:
  - `ShouldAutoInject("host", "42")` → true
  - `ShouldAutoInject("host", "")` → false (no survey)
  - `ShouldAutoInject("host", null)` → false
  - `ShouldAutoInject("host", "   ")` → false (whitespace)
  - `ShouldAutoInject("play", "42")` → false (student role)
  - `ShouldAutoInject("", "42")` → false
- **MIRROR**: TEST_STRUCTURE (`HostLaunchParamsTests.cs`) — same asmdef, `[TestFixture]`/`[Test]` style,
  `test_[scenario]_[expected]` naming per coding standards.
- **IMPORTS**: `NUnit.Framework`.
- **GOTCHA**: Keep tests pure (no Play mode) so they run in the EditMode runner and stay deterministic
  (project rule: no time/random). This avoids the `test_run` Play-mode block noted in the Phase 2 report.
- **VALIDATE**: Run via Editor Test Runner (or `test_run` in Bypass mode) — all green.

---

## Testing Strategy

### Unit Tests
| Test | Input | Expected Output | Edge Case? |
|---|---|---|---|
| host + survey → inject | `("host","42")` | `true` | No |
| host + empty survey | `("host","")` | `false` | Yes |
| host + null survey | `("host",null)` | `false` | Yes |
| host + whitespace survey | `("host","  ")` | `false` | Yes |
| student role | `("play","42")` | `false` | Yes |
| empty role | `("","42")` | `false` | Yes |

### Edge Cases Checklist
- [x] Empty / null / whitespace survey id → no inject (decision helper)
- [x] Non-host role → no inject
- [x] Reload/resume path → no double inject (one-shot unsubscribe; resume yields `reconnect_state`)
- [x] Editor/Standalone → bridge no-op, no auto-host at all
- [ ] No survey responses → endpoint returns 400; fire-and-forget logs a warning, professor re-launches
      (Dashboard already gates "Host Game" on `response_count > 0`, so this is rare)
- [ ] Game server / web-app unreachable → `catch` logs; game still hosts (manual modal remains available)

### Integration (folds into Phase 7 adversarial QA)
- Full flow: Dashboard "Host Game" → room auto-created → survey data appears in game with **no** modal.
- Verify a **student**-link browser (no `edi-survey-token`) cannot trigger `send-to-game` (401 from `requireAuth`).

---

## Validation Commands

### Static Analysis — Unity compile (UnitySkills API preferred)
```bash
# Preferred: POST asset_refresh + console_get_logs via UnitySkills REST API (http://localhost:8090)
# per .claude/docs/technical-preferences.md — do NOT hand-edit if the API is available.
```
EXPECT: Zero console errors across all 3 asmdefs after `asset_refresh`.

### Static Analysis — web-app client lint (only if JS touched; Phase 3 touches none by default)
```bash
cd web-app/client && npx oxlint src/
```
EXPECT: No new warnings.

### Unit Tests — Unity EditMode
```bash
# Editor Test Runner (Window > General > Test Runner > EditMode > Run All)
# or UnitySkills test_run in Bypass mode (auto mode blocks Play-mode-capable runs — see Phase 2 report)
```
EXPECT: `HostAutoInjectDecisionTests` + existing `HostLaunchParamsTests` all pass.

### Unit Tests — web-app (regression; no new web-app code expected)
```bash
cd web-app && npm test
```
EXPECT: 34/34 still green (no web-app source changed).

### Manual / Runtime Validation (Phase 7)
- [ ] Scene has `HostLaunchBootstrap` wired (Phase 2 remaining item) with `NetworkManager`/`RaceUI` refs.
- [ ] `POST /api/game/host-token` with a survey that has responses → open the built game URL → confirm
      room auto-creates AND survey cars/rules load with no modal.
- [ ] Reload the host tab → resume, no duplicate room, no second injection.
- [ ] From a plain browser (no token) hit `/api/surveys/{id}/send-to-game` → 401.

---

## Acceptance Criteria
- [ ] `HostAutoInjectDecision.ShouldAutoInject` implemented and unit-tested (6 cases green).
- [ ] `HostLaunchBootstrap` reads `survey` and, for `role==host`, injects on `OnRoomCreated` (one-shot).
- [ ] jslib + C# bridge `HostAutoInject` added; editor path is a no-op; WebGL compiles clean.
- [ ] No changes to `Server/server.js`, the `survey_import` relay, or any web-app REST route.
- [ ] End-to-end (Phase 7): host launch loads survey data with **0 typed room codes / 0 modals**.
- [ ] `SendToGameModal.jsx` still works as the manual fallback.

## Completion Checklist
- [ ] Code follows discovered patterns (jslib `mergeInto`, `#if UNITY_WEBGL` guard, pure-helper split).
- [ ] Error handling matches codebase style (fire-and-forget `console.warn`, never blocks hosting).
- [ ] Logging follows conventions (`[HostLaunchBootstrap]` / `[HostAutoInject]` prefixes, `Debug.Log`).
- [ ] Tests follow EditMode pattern; deterministic; no Play mode.
- [ ] No hardcoded room codes or origins (relative `/api/...`, localStorage key `edi-survey-token`).
- [ ] No unnecessary scope (no auto-start, no server edits, modal untouched).
- [ ] Self-contained — no codebase search needed to implement.

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Same-origin assumption breaks if game/api are split across origins | L | Inject fails silently | Prod serves both on one origin (nginx); `console.warn` on non-ok; manual modal fallback remains |
| Hash `survey` id is client-supplied, not bound to token `sid` | L | A crafted host URL could name another survey | Endpoint is `requireAuth` (professor-only); classroom trust model (mirrors accepted LOW-2). Future: Design B binds `sid` server-side |
| `room_created` arrives before subscription | L | Missed injection | Subscribe **before** `CreateRoom`; handler attaches synchronously in `Start` |
| Auto-inject fires before survey responses final | L | Stale/empty data | Dashboard gates "Host Game" on `response_count > 0`; professor can re-launch or use manual modal |
| Reload double-injects | L | Duplicate data push | One-shot unsubscribe; resume path yields `reconnect_state`, not `room_created` |

## Notes
- Phase 3 runs **parallel with Phase 2** (both depend only on Phase 1's token) and touches a different
  surface — Phase 2 = launch plumbing, Phase 3 = data injection. The only shared file is
  `HostLaunchBootstrap.cs`; coordinate the edit if both are in flight.
- The host token's `sid` binding (`Server/server.js:41-85`) is *not* used by Design A — kept as the
  hook for a future server-side hardening (Design B) if the classroom trust assumption is tightened.
- UnitySkills API (`http://localhost:8090`) is the required path for the `.jslib`/`.cs` edits and
  `asset_refresh`/test runs per `.claude/docs/technical-preferences.md`; fall back to file edits only
  if the API is unavailable.
