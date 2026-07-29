# Plan: Professor Host Launch (Phase 2)

## Summary
Add a one-click "主持游戏 / Host Game" button on the authenticated professor Dashboard that mints a host token and opens the Unity WebGL build at the game root with `role=host`, a host token, and the survey id in the URL hash. Unity reads the hash on load, auto-calls `CreateRoom(hostToken)`, locks its role to Professor (hiding the student Join UI and skipping the manual Host button), and persists its `sessionId` so a page reload rejoins instead of creating a duplicate room.

## User Story
As a **professor running a live classroom session**,
I want **to launch the hosted racing game directly from my survey Dashboard with one click**,
so that **I never have to open the game, click "Host", and hand-copy a room code under time pressure**.

## Problem → Solution
Today the professor opens the game at `/`, clicks an in-game **Host** button (`SetupScreen.HostRoom()` → `NetworkManager.CreateRoom()` with **no** token), and any browser can do the same. → The Dashboard mints a short-lived HMAC host token, launches Unity pre-configured as host via URL hash params, and Unity auto-creates the room carrying the token (which the WS relay verifies when `REQUIRE_HOST_TOKEN=true`), with the student Join UI hidden and role locked.

## Metadata
- **Complexity**: Large (cross-cutting: React client + Node route wiring + C# + WebGL jslib; ~9 files)
- **Source PRD**: `.claude/PRPs/prds/role-bound-game-links.prd.md`
- **PRD Phase**: Phase 2 — Professor host launch (parallel with Phase 3; depends on Phase 1 = complete)
- **Estimated Files**: 9 (2 new, 7 modified)

---

## UX Design

### Before
```
Dashboard (survey list)                 Unity build at /
┌───────────────────────────┐          ┌────────────────────────────┐
│ Survey "Race A"           │          │ [ Host ]  [ Join ]         │
│  [Send to Game] [Delete]  │  ──?──▶  │ professor clicks Host,     │
│                           │          │ copies 6-char code,        │
│ (must open game manually, │          │ opens SendToGameModal,     │
│  type code, hand off)     │          │ types code to push data    │
└───────────────────────────┘          └────────────────────────────┘
```

### After
```
Dashboard (survey list)                        Unity build at /#role=host&token=…&survey=…
┌────────────────────────────────┐            ┌────────────────────────────────┐
│ Survey "Race A"  (has responses)│            │ auto-connects, auto-CreateRoom │
│  [Host Game] [Send to Game]     │  ─click─▶  │ (token verified server-side),  │
│                                 │  new tab   │ Role=Professor locked,         │
│  (button hidden if 0 responses) │            │ student Join UI hidden,        │
│                                 │            │ room code shown for sharing    │
└────────────────────────────────┘            └────────────────────────────────┘
```

### Interaction Changes
| Touchpoint | Before | After | Notes |
|---|---|---|---|
| Start hosting | Open game → click in-game Host | Dashboard "Host Game" button | Button gated on `response_count > 0` |
| Role assignment | Chosen in-app via Host/Join buttons | Bound to launch URL (`role=host`) + server token | Student JoinScreen hidden when Role=Professor |
| Room code | Professor reads it off screen, retypes into modal | Auto-created; shown on host screen for sharing (Phase 4 formalizes the student link) | Phase 2 shows the code; link generation is Phase 4 |
| Reload during host | New `create_room` → orphan room | Persisted `sessionId` → `rejoin_room` | Reuses server rejoin path (`server.js:410-467`) |

---

## Mandatory Reading

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 | `web-app/src/routes/game-status.js` | 1-17 | The `POST /api/game/host-token` endpoint **already exists** — do not re-add it |
| P0 | `web-app/client/src/pages/DashboardPage.jsx` | 8-20, 97, 124-145 | Where the button + modal-id-in-state pattern go; card-action row; response_count gate |
| P0 | `web-app/client/src/api.js` | 19-33, 118-123 | `request()` wrapper (auto-Bearer); mirror `sendToGame` for the new `hostToken` call |
| P0 | `Assets/Scripts/Network/NetworkManager.cs` | 28-31, 55-64, 93-99, 136-193, 279-327 | `CreateRoom(hostToken)`, pendingAction ready-gate, sessionId, reconnect |
| P0 | `Assets/Plugins/WebGL/WebSocketBridge.jslib` | 5-12, 75 | String-return jslib pattern to copy for a new URL-reader function |
| P0 | `Assets/Scripts/UI/RaceUI.cs` | 12, 19-23, 43-83 | The UI orchestrator; `SetRoleFromNetwork` (exists, unused); role+state gates |
| P1 | `Assets/Scripts/Network/NetworkMessages.cs` | 17-23 | `CreateRoomMessage.hostToken` field already present |
| P1 | `Assets/Scripts/UI/SetupScreen.cs` | 58-59, 134-154 | HostButton wiring + `HostRoom()` + `OnRoomCreated` (room-code display) |
| P1 | `Assets/Scripts/UI/EventPanel.cs` | 61-75 | EventPanel has no role logic; visibility owned by RaceUI |
| P1 | `Assets/Scripts/Race/RaceManager.cs` | 41, 54-63 | GameState default = Setup; existing auto-start hook |
| P2 | `web-app/src/middleware/auth.js` | 16-28 | `requireAuth` Bearer session model (context for the endpoint) |
| P2 | `Server/server.js` | 332-366, 410-467 | Server reads `msg.hostToken`; rejoin path for sessionId persistence |

## External Documentation
No external research needed — feature uses established internal patterns (Express route, React fetch wrapper, Unity `[DllImport("__Internal")]` jslib bridge, `JsonUtility` messages). All patterns exist in-repo.

---

## Patterns to Mirror

### NAMING_CONVENTION — Express route file (endpoint already exists)
```js
// SOURCE: web-app/src/routes/game-status.js:1-17
import { Router } from 'express';
import { requireAuth } from '../middleware/auth.js';
import { mintHostToken } from '../hostToken.js';
const router = Router();
router.post('/host-token', requireAuth, (req, res) => {
  const surveyId = req.body?.surveyId ?? null;
  const { token, expiresAt } = mintHostToken(surveyId);
  res.json({ success: true, data: { token, expiresAt } });
});
```
> The endpoint EXISTS at `POST /api/game/host-token`. Reuse its response envelope `{ success, data }`.

### API_WRAPPER — client fetch helper
```js
// SOURCE: web-app/client/src/api.js:118-123
export async function sendToGame(id, roomCode) {
  return request(`/surveys/${id}/send-to-game`, {
    method: 'POST',
    body: JSON.stringify({ roomCode }),
  });
}
// request() (api.js:19-33) auto-attaches Bearer token and redirects to #/login on 401.
```

### DASHBOARD_ACTION + MODAL_GATE — React card action + id-in-state modal
```jsx
// SOURCE: web-app/client/src/pages/DashboardPage.jsx:124-139
<div className="card-actions">
  {(s.response_count ?? 0) > 0 && (
    <button className="btn-primary btn-small"
      onClick={e => { e.stopPropagation(); setSendModalSurveyId(s.id); }}>
      Send to Game
    </button>
  )}
  <button className="btn-danger btn-small"
    onClick={e => { e.stopPropagation(); handleDelete(s.id, s.config_name); }}>Delete</button>
</div>
// SOURCE: DashboardPage.jsx:11, 145 — modal gated by id-in-state
// const [sendModalSurveyId, setSendModalSurveyId] = useState(null);
// {sendModalSurveyId && <SendToGameModal surveyId={sendModalSurveyId} onClose={...} />}
```
> Every card button calls `e.stopPropagation()` because the card wrapper has its own `onClick` navigate (`DashboardPage.jsx:97`).

### JSLIB_STRING_RETURN — WebGL → C# string bridge
```js
// SOURCE: Assets/Plugins/WebGL/WebSocketBridge.jslib:5-12
WebSocketBridge_GetPageWebSocketUrl: function() {
    var protocol = (window.location.protocol === 'https:') ? 'wss://' : 'ws://';
    var url = protocol + window.location.host + '/ws';
    var bufferSize = lengthBytesUTF8(url) + 1;
    var buffer = _malloc(bufferSize);
    stringToUTF8(url, buffer, bufferSize);
    return buffer;
},
```
```csharp
// SOURCE: Assets/Scripts/Network/NetworkManager.cs:28-30 — matching DllImport
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern string WebSocketBridge_GetPageWebSocketUrl();
#endif
```

### NETWORK_CREATE_ROOM — token-carrying create with ready-gate
```csharp
// SOURCE: Assets/Scripts/Network/NetworkManager.cs:136-151
public void CreateRoom(string hostToken = null)
{
    manualDisconnect = false;
    Connect();
    pendingAction = () =>
    {
        IsHost = true;
        var msg = new CreateRoomMessage { sessionId = sessionId, hostToken = hostToken };
        Send(JsonUtility.ToJson(msg));
    };
    if (bridge.IsConnected) { pendingAction(); pendingAction = null; }
}
// pendingAction fires in HandleOpen() (NetworkManager.cs:184-193) once the socket opens.
```

### UI_ROLE_ORCHESTRATION — the single place that shows/hides panels by role
```csharp
// SOURCE: Assets/Scripts/UI/RaceUI.cs:43-67
public void SetRoleFromNetwork(bool isHost)
{
    Role = isHost ? UserRole.Professor : UserRole.Student;
    ApplyRole();
    OnStateChanged(RaceManager != null ? RaceManager.CurrentState : GameState.Setup);
}
private void ApplyRole()
{
    bool isProfessor = Role == UserRole.Professor;
    if (Events != null) Events.gameObject.SetActive(isProfessor);          // line 54
    if (Setup != null) Setup.gameObject.SetActive(isProfessor);            // line 56
    if (JoinScreen != null) JoinScreen.gameObject.SetActive(!isProfessor); // line 57
    // ...camera mode...
}
// EventPanel state-gate (RaceUI.cs:77-80): shown only when Role==Professor AND GameState==Racing/Paused.
```

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `web-app/client/src/api.js` | UPDATE | Add `requestHostToken(surveyId)` wrapper for `POST /api/game/host-token` |
| `web-app/client/src/pages/DashboardPage.jsx` | UPDATE | Add "Host Game" button (gated on `response_count > 0`) + launch handler |
| `web-app/client/src/gameLaunch.js` | CREATE | Helper: build the Unity host-launch URL (game root + hash params); single source of the game-root constant |
| `Assets/Plugins/WebGL/WebSocketBridge.jslib` | UPDATE | Add `WebSocketBridge_GetPageUrl()` returning `window.location.href`; add `localStorage` get/set for sessionId persistence |
| `Assets/Scripts/Network/WebSocketBridge.cs` | UPDATE | Add `[DllImport]` + editor-fallback wrappers for the new jslib functions |
| `Assets/Scripts/Network/NetworkManager.cs` | UPDATE | Persist/restore `sessionId`; expose page-URL read; prefer rejoin when a persisted host session exists |
| `Assets/Scripts/UI/HostLaunchBootstrap.cs` | CREATE | New MonoBehaviour: parse launch params, if `role=host` → `CreateRoom(token)` + `RaceUI.SetRoleFromNetwork(true)` |
| `Assets/Scripts/UI/RaceUI.cs` | UPDATE | Wire network room-created into `SetRoleFromNetwork` (currently never called) |
| `web-app/client/.env.example` | UPDATE (if present) or note | Document `VITE_GAME_URL` override (default `/`) |

## NOT Building
- **Auto-inject survey data / auto-start race** — that is Phase 3 (parallel). Phase 2 only creates the room and locks role; the race still starts via existing means until Phase 3 lands.
- **Student link + landing page** — Phase 4. Phase 2 may display the room code on the host screen (already done by `SetupScreen.OnRoomCreated`) but does not generate the shareable student link.
- **Student-side auto-join / role hard-lock** — Phase 5.
- **Turning `REQUIRE_HOST_TOKEN` on in production config** — the boot guard (Phase 1) requires a strong `INTERNAL_SECRET` first; enabling enforcement is a deploy-config decision, not code in this phase. Phase 2 sends the token unconditionally so it works whether the flag is on or off.
- **Full EventPanel-on-launch** — EventPanel requires `GameState==Racing` (RaceUI.cs:77-80); it appears once the race starts (Phase 3 auto-start or manual). Phase 2 guarantees role-lock, not a running race.

---

## Step-by-Step Tasks

### Task 1: Add client API wrapper for host-token minting
- **ACTION**: Add an exported async function in `web-app/client/src/api.js`.
- **IMPLEMENT**:
  ```js
  export async function requestHostToken(surveyId) {
    return request('/game/host-token', {
      method: 'POST',
      body: JSON.stringify({ surveyId }),
    });
  }
  ```
- **MIRROR**: `API_WRAPPER` (`api.js:118-123`).
- **IMPORTS**: none new — `request` is module-local.
- **GOTCHA**: `request()` already prefixes `/api`, auto-attaches the Bearer token, and redirects to `#/login` on 401. Do NOT prepend `/api` here. Returns `{ success, data: { token, expiresAt } }`.
- **VALIDATE**: `grep -n "requestHostToken" web-app/client/src/api.js`; from the dashboard, network tab shows `POST /api/game/host-token` returning a token.

### Task 2: Create the game-launch URL helper
- **ACTION**: Create `web-app/client/src/gameLaunch.js`.
- **IMPLEMENT**:
  ```js
  // Single source of truth for the Unity WebGL game root. Same-origin root by default
  // (single-origin deploy: game at /, survey app at /survey/). Override with VITE_GAME_URL.
  const GAME_ROOT = import.meta.env.VITE_GAME_URL || '/';

  // Build the professor host-launch URL. Token + survey ride in the hash fragment so they
  // are never sent to the server, never logged by nginx/Traefik/Caddy, and never CDN-cached.
  export function buildHostLaunchUrl(token, surveyId) {
    const params = new URLSearchParams({ role: 'host', token, survey: String(surveyId) });
    return `${GAME_ROOT}#${params.toString()}`;
  }
  ```
- **MIRROR**: constant-module style of `web-app/client/src/constants.js`.
- **IMPORTS**: none.
- **GOTCHA**: Use the **hash** (`#`), not a query string — PRD risk row: query params may be cached/logged differently across the three edge configs and would leak the token into access logs. Hash is client-only. Note `URLSearchParams` encodes space as `+`; the C# parser (Task 7) uses `Uri.UnescapeDataString` which does NOT turn `+` into space — acceptable because tokens are base64url (no `+`), but do not pass values containing literal `+`.
- **VALIDATE**: browser/console: `buildHostLaunchUrl('t', 5)` → `/#role=host&token=t&survey=5`; covered by vitest in Task 9.

### Task 3: Add the "Host Game" button to the Dashboard
- **ACTION**: In `web-app/client/src/pages/DashboardPage.jsx`, add a button inside the `.card-actions` row (before "Send to Game"), gated on responses, plus an async launch handler.
- **IMPLEMENT** (handler near the top of the component):
  ```jsx
  async function handleHostGame(surveyId) {
    try {
      const res = await requestHostToken(surveyId);
      const { token } = res.data;
      window.open(buildHostLaunchUrl(token, surveyId), '_blank', 'noopener');
    } catch (err) {
      // match existing dashboard error handling (see handleDelete)
      alert('Failed to start host session: ' + err.message);
    }
  }
  ```
  Button (mirror the gated "Send to Game" button):
  ```jsx
  {(s.response_count ?? 0) > 0 && (
    <button className="btn-primary btn-small"
      onClick={e => { e.stopPropagation(); handleHostGame(s.id); }}>
      主持游戏
    </button>
  )}
  ```
- **MIRROR**: `DASHBOARD_ACTION` (`DashboardPage.jsx:124-139`), same `response_count` gate and `e.stopPropagation()`.
- **IMPORTS**: add `requestHostToken` to the existing `../api.js` import; `import { buildHostLaunchUrl } from '../gameLaunch.js';`.
- **GOTCHA**: The card wrapper navigates on click (`DashboardPage.jsx:97`) — the `e.stopPropagation()` is mandatory or clicking Host Game also navigates. Inspect `handleDelete`'s error style and match it (do not introduce a new toast system).
- **VALIDATE**: Dashboard shows "主持游戏" only on surveys with responses; clicking opens a new tab at `/#role=host&token=…&survey=…`.

### Task 4: Add jslib functions — read page URL + localStorage persistence
- **ACTION**: In `Assets/Plugins/WebGL/WebSocketBridge.jslib`, add three functions inside the `mergeInto` object (before the closing at line 75).
- **IMPLEMENT**:
  ```js
  WebSocketBridge_GetPageUrl: function() {
      var url = window.location.href;
      var bufferSize = lengthBytesUTF8(url) + 1;
      var buffer = _malloc(bufferSize);
      stringToUTF8(url, buffer, bufferSize);
      return buffer;
  },
  WebSocketBridge_LocalStorageGet: function(keyPtr) {
      var key = UTF8ToString(keyPtr);
      var val = window.localStorage.getItem(key) || '';
      var bufferSize = lengthBytesUTF8(val) + 1;
      var buffer = _malloc(bufferSize);
      stringToUTF8(val, buffer, bufferSize);
      return buffer;
  },
  WebSocketBridge_LocalStorageSet: function(keyPtr, valPtr) {
      window.localStorage.setItem(UTF8ToString(keyPtr), UTF8ToString(valPtr));
  },
  ```
- **MIRROR**: `JSLIB_STRING_RETURN` (`jslib:5-12`) — same `_malloc`/`stringToUTF8`/return-buffer idiom; `UTF8ToString` is already used by `WebSocketBridge_Connect` (jslib:28+).
- **IMPORTS**: n/a (jslib).
- **GOTCHA**: Keep every function comma-separated inside `mergeInto` — a missing comma silently breaks the whole library at build. Return an **empty string** (not null) when a key is absent, so the C# marshaler gets a valid pointer.
- **VALIDATE**: `grep -n "WebSocketBridge_GetPageUrl\|LocalStorageGet\|LocalStorageSet" Assets/Plugins/WebGL/WebSocketBridge.jslib` shows all three; a WebGL build compiles without emscripten errors.

### Task 5: Add C# DllImport wrappers + editor fallbacks
- **ACTION**: In `Assets/Scripts/Network/WebSocketBridge.cs`, add DllImports (WebGL) and editor-mode fallbacks so play-mode-in-editor does not crash.
- **IMPLEMENT**:
  ```csharp
  #if UNITY_WEBGL && !UNITY_EDITOR
      [DllImport("__Internal")] private static extern string WebSocketBridge_GetPageUrl();
      [DllImport("__Internal")] private static extern string WebSocketBridge_LocalStorageGet(string key);
      [DllImport("__Internal")] private static extern void WebSocketBridge_LocalStorageSet(string key, string val);
  #endif

  public static string GetPageUrl()
  {
  #if UNITY_WEBGL && !UNITY_EDITOR
      return WebSocketBridge_GetPageUrl();
  #else
      return ""; // editor has no page URL; HostLaunchBootstrap treats empty as "no host params"
  #endif
  }
  public static string StorageGet(string key)
  {
  #if UNITY_WEBGL && !UNITY_EDITOR
      return WebSocketBridge_LocalStorageGet(key);
  #else
      return UnityEngine.PlayerPrefs.GetString(key, "");
  #endif
  }
  public static void StorageSet(string key, string val)
  {
  #if UNITY_WEBGL && !UNITY_EDITOR
      WebSocketBridge_LocalStorageSet(key, val);
  #else
      UnityEngine.PlayerPrefs.SetString(key, val);
  #endif
  }
  ```
- **MIRROR**: existing `#if UNITY_WEBGL && !UNITY_EDITOR` DllImport block in `WebSocketBridge.cs:28-31`.
- **IMPORTS**: `using System.Runtime.InteropServices;` (confirm it is already present in WebSocketBridge.cs — it is used by existing DllImports).
- **GOTCHA**: The existing page-URL DllImport currently lives in `NetworkManager.cs:28-30`; put the new three in `WebSocketBridge.cs` for cohesion and expose static wrappers. Editor fallback via `PlayerPrefs` keeps editor play mode working (and gives Task 6 a working persistence store in-editor).
- **VALIDATE**: Editor compiles with no errors; `WebSocketBridge.GetPageUrl()` returns `""` in editor.

### Task 6: Persist and restore sessionId in NetworkManager
- **ACTION**: In `Assets/Scripts/Network/NetworkManager.cs`, replace the always-fresh `sessionId` generation (`:64`) with load-or-create, and persist `lastRoomCode`/`wasHost` for reload-rejoin.
- **IMPLEMENT** (in `Awake`, replacing line 64):
  ```csharp
  sessionId = WebSocketBridge.StorageGet("edi-session-id");
  if (string.IsNullOrEmpty(sessionId))
  {
      sessionId = Guid.NewGuid().ToString("N").Substring(0, 12);
      WebSocketBridge.StorageSet("edi-session-id", sessionId);
  }
  ```
  On room created / joined, persist the room + host flag (in the existing `room_created` handling and `JoinRoom`):
  ```csharp
  WebSocketBridge.StorageSet("edi-last-room", roomCode);
  WebSocketBridge.StorageSet("edi-was-host", IsHost ? "1" : "0");
  ```
  Add public helpers for the bootstrap to consult on reload:
  ```csharp
  public bool HasPersistedHostSession()
      => WebSocketBridge.StorageGet("edi-was-host") == "1"
      && !string.IsNullOrEmpty(WebSocketBridge.StorageGet("edi-last-room"));
  public string PersistedRoom() => WebSocketBridge.StorageGet("edi-last-room");
  ```
- **MIRROR**: existing sessionId usage (`NetworkManager.cs:55, 64`) and reconnect's `RejoinRoomMessage` (`:308`).
- **IMPORTS**: none new.
- **GOTCHA**: Keep the 12-char `Guid` format identical — the server persists sessions keyed by this exact string (`server.js:360-362`). Persisting `sessionId` is what lets a reload send `rejoin_room` (`server.js:410-467`) instead of a duplicate `create_room`. Do NOT persist the host **token** (short-lived, single-purpose) — only sessionId/room. Confirm the exact variable holding the created room code in the `room_created` handler before inserting the persist call.
- **VALIDATE**: In a WebGL build, reload during an active host session → console shows a rejoin attempt with the same sessionId, not a new room code.

### Task 7: Create HostLaunchBootstrap — parse URL, auto-host or auto-rejoin
- **ACTION**: Create `Assets/Scripts/UI/HostLaunchBootstrap.cs`, a MonoBehaviour placed on the scene alongside `RaceUI`/`NetworkManager`, running on `Start()` after they exist.
- **IMPLEMENT**:
  ```csharp
  using UnityEngine;

  // Reads professor host-launch params from the page URL hash on load. If launched as
  // host (Dashboard "Host Game"), auto-creates the room carrying the host token and locks
  // the UI to Professor. On a plain reload with a persisted host session, prefers rejoin.
  public class HostLaunchBootstrap : MonoBehaviour
  {
      public NetworkManager NetworkManager;
      public RaceUI RaceUI;

      private void Start()
      {
          string url = WebSocketBridge.GetPageUrl();
          var p = HostLaunchParams.ParseHash(url); // { role, token, survey }

          if (p.TryGetValue("role", out var role) && role == "host")
          {
              p.TryGetValue("token", out var token);
              NetworkManager.CreateRoom(token);        // IsHost=true, sends hostToken
              RaceUI.SetRoleFromNetwork(true);         // hide JoinScreen, Role=Professor
              return;
          }
          if (NetworkManager.HasPersistedHostSession()) // reload of an active host
          {
              RaceUI.SetRoleFromNetwork(true);
              // existing reconnect path resumes the room using the persisted sessionId
          }
      }
  }
  ```
  And a small testable static parser (same file or `HostLaunchParams.cs`):
  ```csharp
  using System;
  using System.Collections.Generic;

  public static class HostLaunchParams
  {
      // Parse "…/#role=host&token=…&survey=…" (also tolerates a leading '?').
      public static Dictionary<string, string> ParseHash(string url)
      {
          var dict = new Dictionary<string, string>();
          if (string.IsNullOrEmpty(url)) return dict;
          int hash = url.IndexOf('#');
          string frag = hash >= 0 ? url.Substring(hash + 1) : "";
          if (frag.StartsWith("?")) frag = frag.Substring(1);
          foreach (var pair in frag.Split('&'))
          {
              if (pair.Length == 0) continue;
              int eq = pair.IndexOf('=');
              if (eq <= 0) continue;
              dict[Uri.UnescapeDataString(pair.Substring(0, eq))]
                  = Uri.UnescapeDataString(pair.Substring(eq + 1));
          }
          return dict;
      }
  }
  ```
- **MIRROR**: `NETWORK_CREATE_ROOM` (pendingAction ready-gate means no manual wait) and `UI_ROLE_ORCHESTRATION` (`RaceUI.SetRoleFromNetwork`).
- **IMPORTS**: as shown.
- **GOTCHA**: `CreateRoom` internally calls `Connect()` and defers the actual `create_room` send via `pendingAction` until the socket opens (`NetworkManager.cs:184-193`) — so calling it in `Start()` is safe before the WS connects. In the editor `GetPageUrl()` returns `""`, so nothing auto-hosts (preserves manual editor testing). Assign `NetworkManager`/`RaceUI` via Inspector (prefer the UnitySkills API for scene wiring — see Validation).
- **VALIDATE**: Load the build at `/#role=host&token=T&survey=5` → console logs `create_room` with the token; JoinScreen inactive; Role=Professor.

### Task 8: Ensure RaceUI reflects network-created host without manual role
- **ACTION**: In `Assets/Scripts/UI/RaceUI.cs`, keep `SetRoleFromNetwork` as the single role entry; if `NetworkManager` exposes a room-created event, subscribe so an in-game Host click also drives role (keeps both paths consistent).
- **IMPLEMENT** (only if such an event exists — verify the exact member first):
  ```csharp
  if (NetworkManager != null)
      NetworkManager.OnRoomCreated += _ => SetRoleFromNetwork(true);
  // unsubscribe in OnDisable
  ```
  If no public room-created event exists, do NOT invent one — rely solely on the bootstrap's `SetRoleFromNetwork(true)` call and leave a note.
- **MIRROR**: `UI_ROLE_ORCHESTRATION` (`RaceUI.cs:43-67`).
- **IMPORTS**: none.
- **GOTCHA**: `RaceUI.Role` (UI truth) and `NetworkManager.IsHost` (network truth) are **decoupled today** — the only bridge is `SetRoleFromNetwork`, previously never called. Do not read `IsHost` directly in UI gates; always go through `SetRoleFromNetwork`. Unsubscribe in `OnDisable` if you subscribe.
- **VALIDATE**: Auto-host launch AND manual in-game Host both end with JoinScreen hidden and Role=Professor; no double-subscription warnings.

### Task 9: Tests
- **ACTION**: Add unit tests for the pure client helper and the C# hash parser.
- **IMPLEMENT**:
  - `web-app/client` (vitest): test `buildHostLaunchUrl('tok', 5)` → `'/#role=host&token=tok&survey=5'`; encoded token round-trips.
  - C# EditMode test (`tests/` per coding-standards): `HostLaunchParams.ParseHash` for: normal `#role=host&token=t&survey=5`, empty string, missing `#`, value containing `=` (`#token=ab=cd` → `ab=cd`).
- **MIRROR**: existing vitest layout `web-app/__tests__/host-token.test.js`; deterministic inputs, no clock/env.
- **IMPORTS**: `import { describe, it, expect } from 'vitest';`.
- **GOTCHA**: The parser is a standalone `public static` class (`HostLaunchParams`) precisely so it is testable without a MonoBehaviour. Match the project's EditMode harness (Unity Test Framework 1.6.0 + NUnit). `import.meta.env.VITE_GAME_URL` is undefined under vitest → default `/` applies; assert against `/`.
- **VALIDATE**: `cd web-app && npx vitest run` all green; Unity EditMode run passes.

---

## Testing Strategy

### Unit Tests
| Test | Input | Expected Output | Edge Case? |
|---|---|---|---|
| `buildHostLaunchUrl` basic | `('tok', 5)` | `/#role=host&token=tok&survey=5` | No |
| `buildHostLaunchUrl` encoding | token with `%`/space | value percent/`+`-encoded in hash | Yes |
| `ParseHash` normal | `x/#role=host&token=t&survey=5` | `{role:host, token:t, survey:5}` | No |
| `ParseHash` no hash | `https://x/` | `{}` (empty) | Yes |
| `ParseHash` empty | `""` | `{}` | Yes |
| `ParseHash` value with `=` | `#token=ab=cd` | `{token: "ab=cd"}` | Yes |
| sessionId persistence | reload with stored id | same 12-char sessionId reused | Yes |

### Edge Cases Checklist
- [x] Empty input → `GetPageUrl()==""` in editor; parser returns `{}`; no auto-host.
- [x] Missing/blank token in URL → `CreateRoom(null)`; server rejects only if `REQUIRE_HOST_TOKEN=true` (correct — surfaces misconfig).
- [x] Invalid types → surveyId coerced via `String(surveyId)`; server accepts `sid` or null.
- [x] Concurrent access → single professor per room (unchanged model).
- [x] Network failure → existing reconnect/backoff (`NetworkManager.cs:279-327`) applies unchanged.
- [x] Reload mid-host → persisted sessionId → rejoin, not duplicate room.
- [ ] Permission denied → n/a (no new server auth surface; endpoint already `requireAuth`).

---

## Validation Commands

### Static Analysis / Build (web-app client)
```bash
cd web-app/client && npm run build
```
EXPECT: Vite build succeeds; no undefined imports (`requestHostToken`, `buildHostLaunchUrl`).

### Unit Tests (web-app)
```bash
cd web-app && npx vitest run
```
EXPECT: All tests pass, including new `gameLaunch` tests and existing `host-token.test.js`.

### Unity Build (WebGL) + EditMode tests
```bash
# Prefer UnitySkills API (http://localhost:8090) for compile + EditMode test run; fallback:
# Editor > Test Runner (EditMode), or game-ci/unity-test-runner locally.
```
EXPECT: C# compiles (jslib DllImports resolve under WebGL define); EditMode parser tests pass.

### Manual Validation
- [ ] Dashboard: "主持游戏" appears only on surveys with `response_count > 0`.
- [ ] Click it → new tab opens at `/#role=host&token=…&survey=…`.
- [ ] Unity auto-connects, creates a room (console `create_room` carries `hostToken`), JoinScreen hidden, Role=Professor.
- [ ] With `REQUIRE_HOST_TOKEN=true` + strong `INTERNAL_SECRET`: valid token → room created; tampered/removed token → server rejects `create_room`.
- [ ] Reload the host tab → same sessionId, rejoin (no orphan room).
- [ ] EventPanel appears once the race enters Racing state (via Phase 3 or manual start) — documented dependency, not a Phase 2 blocker.

---

## Acceptance Criteria
- [ ] Dashboard "Host Game" button present, gated on responses, launches Unity with token+survey in the hash.
- [ ] Unity auto-creates the room as host with no manual in-game clicks; student Join UI hidden; Role locked to Professor.
- [ ] Host token flows end-to-end and is verified server-side when enforcement is on.
- [ ] `sessionId` persists; reload rejoins rather than creating a duplicate room.
- [ ] Unit tests (client URL helper + C# hash parser) written and passing.
- [ ] No new type/lint errors; client build + Unity compile clean.

## Completion Checklist
- [ ] Follows discovered patterns (Express envelope, `request()` wrapper, jslib string-return, pendingAction, `SetRoleFromNetwork`).
- [ ] Error handling matches Dashboard style (no new toast framework).
- [ ] Logging matches existing `[NetworkManager]`/`console.log` style.
- [ ] Tests follow vitest + Unity EditMode patterns.
- [ ] No hardcoded game URL — `VITE_GAME_URL` override with `/` default; token never in query string or logs.
- [ ] Token NOT persisted (only sessionId/room); token stays short-lived.
- [ ] No scope creep into Phase 3/4/5.
- [ ] Self-contained — every file:line and snippet captured above.

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| EventPanel not visible on launch (needs Racing state) | H | M | Documented as Phase 3 coupling; Phase 2 delivers role-lock, not a running race — set expectation, don't force-start with empty data |
| Token in URL leaks via history/logs | M | M | Use hash fragment (never sent to server), short 5-min TTL, never in the student link |
| jslib comma/marshaling error silently breaks WebGL build | M | H | Copy the exact `_malloc`/`stringToUTF8` idiom; grep-verify commas; build immediately after Task 4 |
| `RaceUI.Role` vs `NetworkManager.IsHost` desync | M | M | Route ALL role changes through `SetRoleFromNetwork`; never read `IsHost` in UI gates |
| Editor play mode auto-hosts unexpectedly | L | L | `GetPageUrl()` returns `""` in editor → bootstrap no-ops; manual testing preserved |
| Reload creates orphan rooms | M | M | Persist sessionId + prefer `rejoin_room` (server path exists) |

## Notes
- **Phase 1 already shipped the hard part**: `POST /api/game/host-token` (`game-status.js:13-17`), `CreateRoom(hostToken)` (`NetworkManager.cs:136`), `CreateRoomMessage.hostToken` (`NetworkMessages.cs:22`), and server-side `verifyHostToken(msg.hostToken)` (`server.js:332-340`). Phase 2 is mostly wiring the ends together.
- **Two genuine gaps filled here**: (1) no client-side game-root URL constant → `gameLaunch.js` + `VITE_GAME_URL`; (2) no jslib URL/localStorage reader → new `WebSocketBridge_GetPageUrl` + storage functions.
- **Token scope decision (PRD OQ#2, resolved 2026-07-28)**: create-scoped, surveyId-bound, 5-min TTL, stateless; reconnect via `sessionId`. This plan honors it — token authorizes `create_room` only, is never persisted, and reload uses the sessionId rejoin path.
- **Scene wiring** (attaching `HostLaunchBootstrap`, assigning Inspector refs): prefer the UnitySkills REST API at `http://localhost:8090` per project technical-preferences; fall back to direct scene/prefab edits only if unsupported.
- **Parallel with Phase 3**: Phase 3 (auto-inject + auto-start) is what makes the launched room immediately show a running race + EventPanel. Sequence Phase 3 right after (or alongside) to realize the full "survey done → race live" flow.
