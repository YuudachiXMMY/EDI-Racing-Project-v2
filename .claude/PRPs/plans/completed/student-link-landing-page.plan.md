# Plan: Student Link + Landing Page (Phase 4)

## Summary
When the professor hosts a race (Phase 2), surface a **shareable student link** (carrying the room code, **never** the host token) on the Unity host screen, and add a lightweight **React landing page** (`/survey/#/join/:roomCode`) where a student chooses **"进入 3D 游戏"** (Unity visual-only join) or **"2D 观战"** (existing `/live/:roomCode` spectator dashboard). Neither choice exposes any host UI. This delivers the one shareable audience entry point; the destinations are hardened in Phase 5 (3D auto-join + role lock) and Phase 6 (2D wiring polish).

## User Story
As a **student joining a live classroom race from my own browser**,
I want **to open one link the professor shares and pick how I want to watch (3D or 2D)**,
so that **I immediately see the live race with no login, no room-code typing, and no host controls**.

## Problem → Solution
Today a student either loads the full Unity build at `/` and manually types the 6-char room code (visual-only join), or hand-navigates to `/survey/#/live/:roomCode` for the 2D dashboard — and there is no single link the professor can hand out. → Phase 4 auto-builds one link from the freshly-created room code, shows it on the professor's host screen (with a copy button), and points it at a new choice page that routes to the 3D game or the 2D spectator view.

## Metadata
- **Complexity**: Medium (React landing page + link helper + Unity host-screen surface; ~10 files, 2 new C# + 1 new JSX)
- **Source PRD**: `.claude/PRPs/prds/role-bound-game-links.prd.md`
- **PRD Phase**: Phase 4 — Student link + landing page (depends on Phase 2 = professor host launch; blocks Phases 5 & 6)
- **Estimated Files**: 10 (3 new, 7 modified)

---

## UX Design

### Before
```
Professor (Unity host screen)              Student (no shareable link)
┌────────────────────────────┐            ┌────────────────────────────────┐
│ Room: A1B2C3               │            │ loads / , types "A1B2C3" to join│
│ 2 student(s) connected     │  ──tell──▶ │   ── OR ──                      │
│ (reads code aloud)         │   verbally │ hand-types /survey/#/live/A1B2C3│
└────────────────────────────┘            └────────────────────────────────┘
```

### After
```
Professor (Unity host screen)              Student (one shared link)
┌────────────────────────────────────┐    ┌──────────────────────────────────┐
│ Room: A1B2C3                       │    │  加入直播赛事   房间号 A1B2C3      │
│ 学生链接: https://…/survey/#/join/ │    │  ┌────────────┐  ┌────────────┐  │
│           A1B2C3        [复制]      │──▶ │  │ 进入 3D 游戏│  │  2D 观战    │  │
│ 2 student(s) connected             │    │  └────────────┘  └────────────┘  │
└────────────────────────────────────┘    │  (no Host / EventPanel anywhere)  │
                                           └──────────────────────────────────┘
```

### Interaction Changes
| Touchpoint | Before | After | Notes |
|---|---|---|---|
| Get the audience in | Professor reads room code aloud; students type it | One link shown on host screen (copy button), shared once | Link carries room code only, **no token** |
| Student entry | Full Unity build + manual code, or hand-typed `/live/CODE` | Landing page with a 3D vs 2D choice | New route `/join/:roomCode` |
| 3D path | Load `/`, click Join, type code | Click "进入 3D 游戏" → opens game root with `#room=CODE&role=play` | Unity **auto-join** + role-lock lands in **Phase 5**; Phase 4 only builds the link |
| 2D path | Hand-navigate to `/live/CODE` | Click "2D 观战" → in-app `Link` to `/live/CODE` | `/live/:roomCode` (`LiveRacePage`) already works |

---

## Mandatory Reading

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 | `web-app/client/src/gameLaunch.js` | 1-11 | The helper module to EXTEND; mirror `buildHostLaunchUrl`'s `URLSearchParams` + hash style exactly |
| P0 | `web-app/client/src/App.jsx` | 1-28 | HashRouter route table; add `/join/:roomCode` next to `/live/:roomCode` (line 23) |
| P0 | `web-app/client/src/pages/LiveRacePage.jsx` | 1-70 | The 2D spectator the "2D 观战" button targets; also the closest page-component style to mirror |
| P0 | `Assets/Scripts/UI/SetupScreen.cs` | 19-24, 45-86, 142-154, 177-198 | Host-screen UI fields + `Start()` init/hide + `OnRoomCreated` (room-code display) + `OnNetworkReconnected` re-show — where the student-link surface hooks in |
| P0 | `Assets/Plugins/WebGL/WebSocketBridge.cs` | 173-233 | Existing DllImport + static-wrapper block (`GetPageUrl`/`StorageGet`/`ClearUrlHash`/`HostAutoInject`) with `#if UNITY_WEBGL && !UNITY_EDITOR` editor fallbacks — mirror for the two new functions |
| P0 | `Assets/Plugins/WebGL/WebSocketBridge.jslib` | 72-122 | jslib `mergeInto` string-return + `UTF8ToString` idiom; **HostAutoInject is currently the last entry before `};`** — add a comma when appending |
| P1 | `Assets/Scripts/UI/HostLaunchParams.cs` | 1-31 | Model for a pure, UnityEngine-free, EditMode-testable static helper (mirror for `StudentLinkBuilder`) |
| P1 | `Assets/Tests/EditMode/HostLaunchParamsTests.cs` | all | EditMode NUnit test pattern to mirror for `StudentLinkBuilderTests` |
| P1 | `web-app/__tests__/game-launch.test.js` | 1-27 | vitest pattern to EXTEND for `buildStudentPlayUrl` |
| P2 | `web-app/client/src/index.css` | 30-32, 220-229 | `.btn-primary` / `.live-race-page` / `.live-message` classes to reuse; where to add `.join-landing` |
| P2 | `Assets/Scripts/Network/NetworkManager.cs` | 34, 42 | `public string RoomCode { get; }` and `event Action<string> OnRoomCreated` — the room code source |
| P2 | `web-app/client/vite.config.js` | 5 | `base: '/survey/'` — why the shareable link path is `/survey/#/join/...` |

## External Documentation
No external research needed — feature uses established internal patterns (React Router `useParams`/`Link`, `URLSearchParams` hash building, Unity UGUI `Text`/`Button`, `[DllImport("__Internal")]` jslib bridge, pure static C# helper + NUnit EditMode test). All patterns exist in-repo.

---

## Patterns to Mirror

### URL_HELPER — hash-fragment link builder (extend this module)
```js
// SOURCE: web-app/client/src/gameLaunch.js:1-11
const GAME_ROOT = import.meta.env?.VITE_GAME_URL || '/';

export function buildHostLaunchUrl(token, surveyId) {
  const params = new URLSearchParams({ role: 'host', token, survey: String(surveyId) });
  return `${GAME_ROOT}#${params.toString()}`;
}
// New student helpers follow the SAME GAME_ROOT + '#' + URLSearchParams shape.
```

### ROUTE_TABLE — HashRouter route registration
```jsx
// SOURCE: web-app/client/src/App.jsx:22-24
<Route path="/s/:shareCode" element={<StudentSurveyPage />} />
<Route path="/live/:roomCode" element={<LiveRacePage />} />
// Add: <Route path="/join/:roomCode" element={<JoinLandingPage />} /> (public, no ProtectedRoute)
```

### PAGE_COMPONENT — useParams page + in-app Link
```jsx
// SOURCE: web-app/client/src/pages/LiveRacePage.jsx:1, 17-18, 35
import { useParams } from 'react-router-dom';
export default function LiveRacePage() {
  const { roomCode } = useParams();
  // ...
  <span className="live-room-code">Room {roomCode?.toUpperCase()}</span>
}
```

### JSLIB_STRING_RETURN + JSLIB_STRING_ARG — WebGL bridge idioms
```js
// SOURCE: Assets/Plugins/WebGL/WebSocketBridge.jslib:72-78 (string return)
WebSocketBridge_GetPageUrl: function() {
    var url = window.location.href;
    var bufferSize = lengthBytesUTF8(url) + 1;
    var buffer = _malloc(bufferSize);
    stringToUTF8(url, buffer, bufferSize);
    return buffer;
},
// SOURCE: WebSocketBridge.jslib:89-91 (string arg, void return)
WebSocketBridge_LocalStorageSet: function(keyPtr, valPtr) {
    window.localStorage.setItem(UTF8ToString(keyPtr), UTF8ToString(valPtr));
},
```

### DLLIMPORT_WRAPPER — C# bridge with editor fallback
```csharp
// SOURCE: Assets/Plugins/WebGL/WebSocketBridge.cs:175, 183-190
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern string WebSocketBridge_GetPageUrl();
#endif
public static string GetPageUrl()
{
#if UNITY_WEBGL && !UNITY_EDITOR
    return WebSocketBridge_GetPageUrl();
#else
    return ""; // editor has no page URL
#endif
}
```

### PURE_HELPER — UnityEngine-free, EditMode-testable static class
```csharp
// SOURCE: Assets/Scripts/UI/HostLaunchParams.cs:9-31
using System;
using System.Collections.Generic;

public static class HostLaunchParams
{
    public static Dictionary<string, string> ParseHash(string url)
    {
        var dict = new Dictionary<string, string>();
        if (string.IsNullOrEmpty(url)) return dict;
        // ...never throws; empty/unknown input → empty result...
        return dict;
    }
}
```

### HOST_SCREEN_DISPLAY — show/hide a room-derived field on room-created
```csharp
// SOURCE: Assets/Scripts/UI/SetupScreen.cs:142-154
private void OnRoomCreated(string roomCode)
{
    if (RoomCodeText != null)
    {
        RoomCodeText.gameObject.SetActive(true);
        RoomCodeText.text = $"Room: {roomCode}";
    }
    if (InfoText != null) InfoText.text = "Room created. Start when ready.";
    // ...
}
// Fields declared/hidden the same way as RoomCodeText (SetupScreen.cs:22, 72).
```

### EDITMODE_TEST — NUnit EditMode assertion style
```csharp
// SOURCE: Assets/Tests/EditMode/HostLaunchParamsTests.cs (mirror file + placement)
using NUnit.Framework;
[TestFixture]
public class HostLaunchParamsTests
{
    [Test]
    public void ParseHash_Normal_ReturnsAllKeys() { /* Assert.AreEqual(...) */ }
}
```

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `web-app/client/src/gameLaunch.js` | UPDATE | Add `buildStudentPlayUrl(roomCode)` (game-root `#room=…&role=play`) |
| `web-app/client/src/pages/JoinLandingPage.jsx` | CREATE | The student choice page (3D vs 2D) |
| `web-app/client/src/App.jsx` | UPDATE | Register public route `/join/:roomCode` |
| `web-app/client/src/index.css` | UPDATE | Minimal `.join-landing` / `.join-choices` styles (reuse `.btn-primary`) |
| `web-app/__tests__/game-launch.test.js` | UPDATE | Unit tests for `buildStudentPlayUrl` |
| `Assets/Plugins/WebGL/WebSocketBridge.jslib` | UPDATE | Add `WebSocketBridge_GetPageOrigin` + `WebSocketBridge_CopyToClipboard` |
| `Assets/Plugins/WebGL/WebSocketBridge.cs` | UPDATE | Add DllImport + static wrappers `GetPageOrigin()` / `CopyToClipboard()` with editor fallbacks |
| `Assets/Scripts/UI/StudentLinkBuilder.cs` | CREATE | Pure builder `BuildJoinLink(origin, roomCode)` → `{origin}/survey/#/join/{roomCode}` |
| `Assets/Scripts/UI/SetupScreen.cs` | UPDATE | New `StudentLinkText` + `CopyLinkButton` fields; build/show link on room-created & reconnect; wire copy |
| `Assets/Tests/EditMode/StudentLinkBuilderTests.cs` | CREATE | EditMode tests for `StudentLinkBuilder` |

## NOT Building
- **Unity student-side auto-join + role hard-lock (3D)** — Phase 5. Phase 4's "进入 3D 游戏" button only navigates to `/#room=CODE&role=play`; Unity does not yet auto-`JoinRoom` from that hash or hide Host UI. Until Phase 5, a student clicking 3D still sees the manual Join screen. Do **not** add Unity URL-join parsing here.
- **2D wiring polish / telemetry** — Phase 6. Phase 4 wires the "2D 观战" button to the existing `/live/:roomCode` (already functional); any status-check or join telemetry on the landing page is Phase 6.
- **QR code affordance** — deferred "Could"; requires a QR library (new dependency). Phase 4 ships a text link + copy button only.
- **Room "is-live" validation on the landing page** — the landing page is stateless; it builds links from the `roomCode` param. If the room is dead, the 2D view already shows "Room Not Found" (`LiveRacePage` `Error` phase). No new liveness API.
- **React component-test infra (jsdom / Testing Library)** — not set up in this repo (vitest `globals:true`, Node env only). Do not add it; validate the landing page via build + manual, unit-test only the pure `buildStudentPlayUrl` helper.
- **Changing where the professor launches** — the host screen (Unity) stays the surface for the link; do not move link generation to the Dashboard (which does not know the server-assigned room code).

---

## Step-by-Step Tasks

### Task 1: Add `buildStudentPlayUrl` to the game-launch helper
- **ACTION**: In `web-app/client/src/gameLaunch.js`, add an exported function below `buildHostLaunchUrl`.
- **IMPLEMENT**:
  ```js
  // Build the student 3D-join URL. Carries the room code and role=play only — NEVER a host
  // token — so opening it can join/watch but cannot create a room or trigger events. The hash
  // (client-only) keeps it out of server/CDN logs, consistent with the host-launch URL.
  export function buildStudentPlayUrl(roomCode) {
    const params = new URLSearchParams({ room: String(roomCode), role: 'play' });
    return `${GAME_ROOT}#${params.toString()}`;
  }
  ```
- **MIRROR**: `URL_HELPER` (`gameLaunch.js:7-10`) — same `GAME_ROOT` + `#` + `URLSearchParams` shape.
- **IMPORTS**: none — `GAME_ROOT` is module-local.
- **GOTCHA**: Use `role=play` (not `host`) and include **no** `token` key. `URLSearchParams` encodes uppercase room codes safely (alphanumeric untouched). Keep the `?.` optional-chaining default (`import.meta.env?.VITE_GAME_URL`) already in the file — vitest leaves `VITE_GAME_URL` unset so `GAME_ROOT` falls back to `/`.
- **VALIDATE**: `grep -n "buildStudentPlayUrl" web-app/client/src/gameLaunch.js`; unit test in Task 5.

### Task 2: Create the student landing page component
- **ACTION**: Create `web-app/client/src/pages/JoinLandingPage.jsx`.
- **IMPLEMENT**:
  ```jsx
  import { useParams, Link } from 'react-router-dom';
  import { buildStudentPlayUrl } from '../gameLaunch.js';

  // Public landing page (no auth) reached via the professor-shared student link
  // /survey/#/join/:roomCode. Offers the two audience paths; neither exposes host controls.
  export default function JoinLandingPage() {
    const { roomCode } = useParams();
    return (
      <div className="join-landing">
        <h1>加入直播赛事</h1>
        <p className="join-room-code">房间号 {roomCode?.toUpperCase()}</p>
        <div className="join-choices">
          {/* 3D: leaves the survey app for the Unity game root. Phase 5 makes Unity
              auto-join from this hash and hide Host UI; for now it opens the game. */}
          <a className="btn-primary btn-choice" href={buildStudentPlayUrl(roomCode)}>
            <span className="join-choice-title">进入 3D 游戏</span>
            <span className="join-choice-sub">在浏览器中观看你队伍的赛车</span>
          </a>
          {/* 2D: stays inside the survey app (HashRouter) — existing spectator view. */}
          <Link className="btn-primary btn-choice" to={`/live/${roomCode}`}>
            <span className="join-choice-title">2D 观战</span>
            <span className="join-choice-sub">排行榜 · 小地图 · 事件流</span>
          </Link>
        </div>
      </div>
    );
  }
  ```
- **MIRROR**: `PAGE_COMPONENT` (`LiveRacePage.jsx:1,17-18,35`) for `useParams` + `roomCode?.toUpperCase()`.
- **IMPORTS**: `useParams`, `Link` from `react-router-dom`; `buildStudentPlayUrl` from `../gameLaunch.js`.
- **GOTCHA**: The 3D choice MUST be an `<a href>` (a full navigation to the game root `/`), NOT a router `<Link>` — `Link` would try to resolve inside the survey HashRouter. The 2D choice MUST be a router `<Link to={/live/...}>` so it stays in the survey app. Do not `window.open` a new tab — a student commits to one view; same-tab navigation is expected.
- **VALIDATE**: `npm run build` compiles; manual: open `/survey/#/join/TESTCODE` → heading, room code, two buttons render.

### Task 3: Register the `/join/:roomCode` route
- **ACTION**: In `web-app/client/src/App.jsx`, import the page and add a public route beside `/live/:roomCode`.
- **IMPLEMENT**:
  ```jsx
  import JoinLandingPage from './pages/JoinLandingPage.jsx';
  // ...inside <Routes>, next to the /live route:
  <Route path="/join/:roomCode" element={<JoinLandingPage />} />
  ```
- **MIRROR**: `ROUTE_TABLE` (`App.jsx:22-24`).
- **IMPORTS**: add the `JoinLandingPage` import beside the other page imports (lines 3-8).
- **GOTCHA**: Do **NOT** wrap it in `<ProtectedRoute>` — students are unauthenticated (like `/s/:shareCode` and `/live/:roomCode`). Place it before the catch-all `<Route path="*" .../>` (line 24) or it will be shadowed.
- **VALIDATE**: `grep -n "join/:roomCode\|JoinLandingPage" web-app/client/src/App.jsx`; navigating to `#/join/X` renders the page, not the login redirect.

### Task 4: Add minimal landing-page styles
- **ACTION**: In `web-app/client/src/index.css`, append landing-page classes (reuse `.btn-primary`).
- **IMPLEMENT**:
  ```css
  .join-landing { display: flex; flex-direction: column; align-items: center; justify-content: center;
    min-height: 100vh; gap: 20px; background: var(--bg); text-align: center; padding: 24px; }
  .join-room-code { font-size: 18px; font-weight: 600; letter-spacing: 2px; color: var(--accent); }
  .join-choices { display: flex; gap: 20px; flex-wrap: wrap; justify-content: center; }
  .btn-choice { display: flex; flex-direction: column; gap: 6px; padding: 24px 32px; min-width: 200px;
    border: none; border-radius: 10px; text-decoration: none; cursor: pointer; }
  .join-choice-title { font-size: 20px; font-weight: 700; }
  .join-choice-sub { font-size: 13px; opacity: 0.85; }
  ```
- **MIRROR**: existing `.live-race-page`/`.live-message` layout style (`index.css:220-229`); `.btn-primary` background is inherited by adding `btn-choice` alongside it in the class list.
- **IMPORTS**: n/a.
- **GOTCHA**: Use the existing CSS variables (`--bg`, `--accent`) — they are defined at the top of `index.css`. Do not introduce a new color system.
- **VALIDATE**: Build succeeds; landing page is centered with two readable buttons in light/dark.

### Task 5: Unit-test the student play-URL helper
- **ACTION**: In `web-app/__tests__/game-launch.test.js`, add a `describe('buildStudentPlayUrl', ...)` block.
- **IMPLEMENT**:
  ```js
  import { buildHostLaunchUrl, buildStudentPlayUrl } from '../client/src/gameLaunch.js';

  describe('buildStudentPlayUrl', () => {
    it('puts room and role=play in the hash at the game root, with no token', () => {
      expect(buildStudentPlayUrl('A1B2C3')).toBe('/#room=A1B2C3&role=play');
    });
    it('never begins a query string (room code stays out of server logs)', () => {
      const url = buildStudentPlayUrl('XYZ');
      expect(url).not.toContain('?');
      expect(url).toContain('#room=XYZ');
    });
    it('carries no host token key', () => {
      expect(buildStudentPlayUrl('R1')).not.toContain('token');
    });
  });
  ```
- **MIRROR**: existing `buildHostLaunchUrl` tests (`game-launch.test.js:6-27`).
- **IMPORTS**: extend the existing top import to include `buildStudentPlayUrl`.
- **GOTCHA**: `import.meta.env.VITE_GAME_URL` is undefined under vitest → `GAME_ROOT` = `/`; assert against `/#...`. Deterministic, no clock/env.
- **VALIDATE**: `cd web-app && npx vitest run` — all green.

### Task 6: Add jslib `GetPageOrigin` + `CopyToClipboard`
- **ACTION**: In `Assets/Plugins/WebGL/WebSocketBridge.jslib`, append two functions to the `mergeInto` object after `WebSocketBridge_HostAutoInject`.
- **IMPLEMENT**:
  ```js
  // NOTE: add a comma after HostAutoInject's closing brace — it is currently the last entry.
  WebSocketBridge_GetPageOrigin: function() {
      var origin = window.location.origin;
      var bufferSize = lengthBytesUTF8(origin) + 1;
      var buffer = _malloc(bufferSize);
      stringToUTF8(origin, buffer, bufferSize);
      return buffer;
  },
  WebSocketBridge_CopyToClipboard: function(textPtr) {
      var text = UTF8ToString(textPtr);
      if (navigator.clipboard && navigator.clipboard.writeText) {
          navigator.clipboard.writeText(text).catch(function(e) {
              console.warn('[Clipboard] copy failed', e);
          });
      }
  },
  ```
- **MIRROR**: `JSLIB_STRING_RETURN` (`jslib:72-78`) for origin; `JSLIB_STRING_ARG` (`jslib:89-91`) for the clipboard arg.
- **IMPORTS**: n/a (jslib).
- **GOTCHA**: Every entry in `mergeInto` MUST be comma-separated — `HostAutoInject` is presently the final entry before the closing `};` (jslib:104-122), so you MUST add a trailing comma to its closing `}` when appending. A missing comma silently breaks the whole library at build. `navigator.clipboard` requires a secure context (HTTPS/localhost) — the production edge is HTTPS (see `webgl-https-native-brotli` plan), so this is fine; the `if`-guard degrades gracefully otherwise.
- **VALIDATE**: `grep -n "WebSocketBridge_GetPageOrigin\|WebSocketBridge_CopyToClipboard" Assets/Plugins/WebGL/WebSocketBridge.jslib`; a WebGL build compiles without emscripten errors.

### Task 7: Add C# wrappers `GetPageOrigin()` + `CopyToClipboard()`
- **ACTION**: In `Assets/Plugins/WebGL/WebSocketBridge.cs`, add DllImports (inside the existing `#if UNITY_WEBGL && !UNITY_EDITOR` extern block near line 175-179) and two static wrappers (after `HostAutoInject`, ~line 233).
- **IMPLEMENT**:
  ```csharp
  #if UNITY_WEBGL && !UNITY_EDITOR
      [DllImport("__Internal")] private static extern string WebSocketBridge_GetPageOrigin();
      [DllImport("__Internal")] private static extern void WebSocketBridge_CopyToClipboard(string text);
  #endif

  public static string GetPageOrigin()
  {
  #if UNITY_WEBGL && !UNITY_EDITOR
      return WebSocketBridge_GetPageOrigin();
  #else
      return ""; // editor has no page origin; SetupScreen hides the student-link UI
  #endif
  }
  public static void CopyToClipboard(string text)
  {
  #if UNITY_WEBGL && !UNITY_EDITOR
      WebSocketBridge_CopyToClipboard(text);
  #else
      UnityEngine.GUIUtility.systemCopyBuffer = text; // editor fallback
  #endif
  }
  ```
- **MIRROR**: `DLLIMPORT_WRAPPER` (`WebSocketBridge.cs:175,183-190`) — identical `#if` structure and empty-string editor fallback.
- **IMPORTS**: `System.Runtime.InteropServices` already imported (used by existing DllImports).
- **GOTCHA**: Keep the extern declarations INSIDE the existing `#if UNITY_WEBGL && !UNITY_EDITOR` block or the editor build fails to find `__Internal`. `GetPageOrigin()` returning `""` in editor is intentional — SetupScreen treats empty as "no link surface," preserving in-editor testing.
- **VALIDATE**: Editor compiles with no errors; `WebSocketBridge.GetPageOrigin()` returns `""` in editor.

### Task 8: Create the pure `StudentLinkBuilder`
- **ACTION**: Create `Assets/Scripts/UI/StudentLinkBuilder.cs` (UnityEngine-free, EditMode-testable).
- **IMPLEMENT**:
  ```csharp
  /// <summary>
  /// Pure builder for the shareable student join link surfaced on the professor host screen.
  /// Composes "{origin}/survey/#/join/{roomCode}" — the survey app's landing route (Phase 4).
  /// The link carries only the room code — NEVER the host token — so a student who opens it
  /// cannot create a room or trigger events. Kept free of UnityEngine so it is EditMode-testable.
  /// Returns "" for empty origin (e.g. Editor) or empty room code, so callers can hide the UI.
  /// </summary>
  public static class StudentLinkBuilder
  {
      public static string BuildJoinLink(string origin, string roomCode)
      {
          if (string.IsNullOrEmpty(origin) || string.IsNullOrEmpty(roomCode)) return "";
          string trimmed = origin.TrimEnd('/');
          return $"{trimmed}/survey/#/join/{roomCode}";
      }
  }
  ```
- **MIRROR**: `PURE_HELPER` (`HostLaunchParams.cs:9-31`) — same "no UnityEngine, never throws, empty-in→empty-out" contract; lives in `Assets/Scripts/UI/` (runtime asmdef).
- **IMPORTS**: none.
- **GOTCHA**: The `/survey/` segment is hardcoded here because the survey app's deploy base is fixed (`vite.config.js:5 base:'/survey/'`; `nginx.conf` `location /survey/`), matching how `HostAutoInject` hardcodes `/api/...`. `TrimEnd('/')` prevents a double slash if `origin` ever ends with `/`. Do not upper/lower-case `roomCode` — pass the server-assigned code through verbatim (the 2D `LiveRacePage` uppercases only for display).
- **VALIDATE**: Compiles; covered by Task 10 EditMode tests.

### Task 9: Surface the student link on the host screen
- **ACTION**: In `Assets/Scripts/UI/SetupScreen.cs`, add two Inspector fields, hide them in `Start()`, build/show the link in `OnRoomCreated` and `OnNetworkReconnected`, and wire the copy button.
- **IMPLEMENT**:
  - New fields (in the `[Header("Network (Optional)")]` group, near line 22):
    ```csharp
    public Text StudentLinkText;
    public Button CopyLinkButton;
    private string currentStudentLink = "";
    ```
  - In `Start()` (mirror the RoomCodeText hide at line 72, and button-wire at 58-59):
    ```csharp
    if (StudentLinkText != null) StudentLinkText.gameObject.SetActive(false);
    if (CopyLinkButton != null)
    {
        CopyLinkButton.gameObject.SetActive(false);
        CopyLinkButton.onClick.AddListener(OnCopyStudentLink);
    }
    ```
  - A helper + copy handler:
    ```csharp
    private void ShowStudentLink(string roomCode)
    {
        currentStudentLink = StudentLinkBuilder.BuildJoinLink(WebSocketBridge.GetPageOrigin(), roomCode);
        if (string.IsNullOrEmpty(currentStudentLink)) return; // editor / no origin → keep hidden
        if (StudentLinkText != null)
        {
            StudentLinkText.gameObject.SetActive(true);
            StudentLinkText.text = $"学生链接: {currentStudentLink}";
        }
        if (CopyLinkButton != null) CopyLinkButton.gameObject.SetActive(true);
    }

    private void OnCopyStudentLink()
    {
        if (!string.IsNullOrEmpty(currentStudentLink))
            WebSocketBridge.CopyToClipboard(currentStudentLink);
    }
    ```
  - Call `ShowStudentLink(roomCode)` at the end of `OnRoomCreated` (after line 149) and `ShowStudentLink(NetworkManager.RoomCode)` in `OnNetworkReconnected` (after the RoomCodeText re-show at line 184).
- **MIRROR**: `HOST_SCREEN_DISPLAY` (`SetupScreen.cs:142-154`) and the button-wire/hide idiom (`SetupScreen.cs:58-59, 71-73`).
- **IMPORTS**: none new (`UnityEngine.UI` already imported for `Text`/`Button`).
- **GOTCHA**: `GetPageOrigin()` returns `""` in editor, so `ShowStudentLink` no-ops there — the link surface only appears in a real WebGL build (consistent with the existing network-UI editor behavior). New serialized fields `StudentLinkText`/`CopyLinkButton` must be assigned in the scene — **scene wiring is runtime-QA-pending** (see Notes; prefer the UnitySkills REST API `http://localhost:8090`). Unsubscribe is not required (button lifetime == SetupScreen lifetime, matching `HostButton`).
- **VALIDATE**: Editor compiles; in a WebGL build the host screen shows "学生链接: https://…/survey/#/join/CODE" after room creation, and Copy places it on the clipboard.

### Task 10: EditMode tests for `StudentLinkBuilder`
- **ACTION**: Create `Assets/Tests/EditMode/StudentLinkBuilderTests.cs`.
- **IMPLEMENT**:
  ```csharp
  using NUnit.Framework;

  [TestFixture]
  public class StudentLinkBuilderTests
  {
      [Test]
      public void BuildJoinLink_Normal_ComposesSurveyJoinRoute()
      {
          Assert.AreEqual("https://host.example/survey/#/join/A1B2C3",
              StudentLinkBuilder.BuildJoinLink("https://host.example", "A1B2C3"));
      }

      [Test]
      public void BuildJoinLink_TrailingSlashOrigin_NoDoubleSlash()
      {
          Assert.AreEqual("https://host.example/survey/#/join/R1",
              StudentLinkBuilder.BuildJoinLink("https://host.example/", "R1"));
      }

      [Test]
      public void BuildJoinLink_EmptyOrigin_ReturnsEmpty()
      {
          Assert.AreEqual("", StudentLinkBuilder.BuildJoinLink("", "R1"));
      }

      [Test]
      public void BuildJoinLink_EmptyRoom_ReturnsEmpty()
      {
          Assert.AreEqual("", StudentLinkBuilder.BuildJoinLink("https://host.example", ""));
      }
  }
  ```
- **MIRROR**: `EDITMODE_TEST` (`HostLaunchParamsTests.cs`) — same `[TestFixture]`/`[Test]`/`Assert.AreEqual` style, same `Assets/Tests/EditMode/` folder (covered by `Tests.asmdef`).
- **IMPORTS**: `NUnit.Framework`.
- **GOTCHA**: `Tests.asmdef` already references the runtime assembly (`EDIRacing.Runtime`) so `StudentLinkBuilder` is visible (existing tests use `HostLaunchParams`). No new asmdef reference needed. Let Unity import the new file to generate its `.cs.meta`.
- **VALIDATE**: Unity EditMode run (UnitySkills API or Test Runner) — all four pass.

---

## Testing Strategy

### Unit Tests
| Test | Input | Expected Output | Edge Case? |
|---|---|---|---|
| `buildStudentPlayUrl` basic | `'A1B2C3'` | `/#room=A1B2C3&role=play` | No |
| `buildStudentPlayUrl` no query | `'XYZ'` | contains `#room=XYZ`, no `?` | Yes |
| `buildStudentPlayUrl` no token | `'R1'` | does not contain `token` | Yes |
| `BuildJoinLink` normal | `("https://h", "A1B2C3")` | `https://h/survey/#/join/A1B2C3` | No |
| `BuildJoinLink` trailing slash | `("https://h/", "R1")` | `https://h/survey/#/join/R1` | Yes |
| `BuildJoinLink` empty origin | `("", "R1")` | `""` | Yes |
| `BuildJoinLink` empty room | `("https://h", "")` | `""` | Yes |

### Edge Cases Checklist
- [x] Empty origin (editor) → `BuildJoinLink` returns `""`; SetupScreen keeps the link UI hidden.
- [x] Empty/undefined roomCode on landing page → `roomCode?.toUpperCase()` renders blank; buttons still build (defensive; the professor-shared link always has a code).
- [x] Token never present → student play URL and join link both omit `token`; assert in tests.
- [x] Trailing-slash origin → `TrimEnd('/')` prevents `//survey`.
- [x] Dead/expired room → 2D path shows `LiveRacePage` `Error` phase ("Room not found"); 3D path is Phase 5's concern. No crash.
- [ ] Clipboard blocked (non-secure context) → `if (navigator.clipboard...)` guard no-ops; link text remains readable/selectable. (Manual, WebGL only.)

---

## Validation Commands

### Static Analysis / Build (web-app client)
```bash
cd web-app/client && npm run build
```
EXPECT: Vite build succeeds; no undefined imports (`buildStudentPlayUrl`, `JoinLandingPage`).

### Unit Tests (web-app)
```bash
cd web-app && npx vitest run
```
EXPECT: All tests pass, including the new `buildStudentPlayUrl` block and existing `buildHostLaunchUrl` / `host-token` tests.

### Unity Compile + EditMode tests
```bash
# Prefer UnitySkills API (http://localhost:8090) for compile + EditMode test run; fallback:
# Editor > Test Runner (EditMode), or game-ci/unity-test-runner locally.
```
EXPECT: C# compiles (jslib DllImports resolve under WebGL define); `StudentLinkBuilderTests` (4) + existing EditMode suite pass.

### Browser Validation (landing page)
```bash
cd web-app/client && npm run dev
# open http://localhost:5173/survey/#/join/TESTCODE (or the dev base)
```
EXPECT: Heading "加入直播赛事", room code, and two buttons render; "2D 观战" navigates to `#/live/TESTCODE`; "进入 3D 游戏" navigates to `/#room=TESTCODE&role=play`.

### Manual Validation
- [ ] Professor host launch (Phase 2) → host screen shows "学生链接: …/survey/#/join/CODE" once the room is created.
- [ ] Copy button places the link on the clipboard (WebGL/HTTPS build).
- [ ] Opening the copied link → landing page with the two choices, room code matching.
- [ ] "2D 观战" → live spectator dashboard for that room (no host controls).
- [ ] "进入 3D 游戏" → opens the Unity game at the game root carrying `#room=CODE&role=play` (auto-join lands in Phase 5).
- [ ] The student link contains **no** `token` anywhere.

---

## Acceptance Criteria
- [ ] `buildStudentPlayUrl(roomCode)` returns `/#room=CODE&role=play` (no token), unit-tested.
- [ ] `/join/:roomCode` public route renders the choice page with room code + 3D/2D buttons.
- [ ] "2D 观战" routes to the existing `/live/:roomCode`; "进入 3D 游戏" navigates to the game root with the room-code hash.
- [ ] `StudentLinkBuilder.BuildJoinLink` composes `{origin}/survey/#/join/{roomCode}`, unit-tested (incl. empty/trailing-slash edges).
- [ ] The professor host screen displays the shareable student link (and a copy button) once the room is created and after reconnect.
- [ ] The student link carries the room code only — never the host token.
- [ ] Client build + Unity compile clean; vitest + EditMode green.

## Completion Checklist
- [ ] Follows discovered patterns (`URL_HELPER`, `ROUTE_TABLE`, `PAGE_COMPONENT`, `JSLIB_STRING_RETURN/ARG`, `DLLIMPORT_WRAPPER`, `PURE_HELPER`, `HOST_SCREEN_DISPLAY`, `EDITMODE_TEST`).
- [ ] No `<ProtectedRoute>` on the student route; 3D uses `<a href>`, 2D uses `<Link>`.
- [ ] jslib entries comma-separated; `navigator.clipboard` guarded.
- [ ] `/survey/` hardcode justified (fixed deploy base), matching `HostAutoInject`.
- [ ] Editor fallbacks present (`GetPageOrigin`→`""`, clipboard→`systemCopyBuffer`); link UI hidden in editor.
- [ ] Tests follow vitest + Unity EditMode patterns; deterministic.
- [ ] No scope creep into Phase 5 (Unity auto-join/role-lock) or Phase 6 (2D polish); no QR; no jsdom infra.
- [ ] Self-contained — every file:line and snippet captured above.

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| 3D button opens Unity but student still sees manual Join UI (Phase 5 not done) | H | L | Documented Phase 5 coupling; Phase 4 delivers the choice page + link, not the auto-join — set expectation, do not force Unity URL-join here |
| New SetupScreen fields unassigned in scene → link never shows | M | M | Scene wiring via UnitySkills API is a required runtime-QA step (mirror Phase 2/3 "scene wiring pending"); `null`-guards keep it from throwing |
| jslib comma/marshaling error silently breaks WebGL build | M | H | Copy the exact `_malloc`/`stringToUTF8`/`UTF8ToString` idiom; add the trailing comma after `HostAutoInject`; grep-verify; build immediately after Task 6 |
| Clipboard fails in a non-secure context | L | L | `if (navigator.clipboard...)` guard; production edge is HTTPS; link text stays selectable |
| `/survey/` base changes in a future deploy | L | M | Single hardcode in `StudentLinkBuilder` + jslib note; mirrors existing `/api` hardcode; would break `HostAutoInject` too, so caught together |

## Notes
- **Phase boundary**: Phase 4 = the shareable link + the choice page. The 3D destination is completed by **Phase 5** (Unity auto-`JoinRoom` from `#room=…&role=play`, hide Host/EventPanel, hard-lock non-host role); the 2D destination already works and **Phase 6** is thin polish/wiring confirmation. Phase 4 intentionally makes both buttons live so nothing is dead, without pulling Phase 5/6 work forward.
- **Where the link lives**: the room code is assigned by the server after Unity sends `create_room` (Phase 2), so the only place that knows it at hosting time is the Unity host screen — hence the link is surfaced there, not on the Dashboard (which never learns the code).
- **Token safety**: the student link (`/survey/#/join/CODE`) and the 3D play URL (`/#room=CODE&role=play`) both carry the room code only. Combined with Phase 1 server-side host-token enforcement, a student opening either link cannot `create_room` or `event_triggered`. This is the PRD's core security property; Phase 7 adversarially verifies it.
- **Scene wiring** (attaching `StudentLinkText`/`CopyLinkButton` to the `SetupScreen` in `complete_track_demo.unity`): prefer the UnitySkills REST API at `http://localhost:8090` per project technical-preferences; fall back to direct scene edits only if unsupported. This (plus runtime QA of the copy button on an HTTPS WebGL build) is the expected post-implementation QA step, consistent with Phase 2/3 status.
- **Open Question touched**: OQ "Should the student landing page default to 3D or 2D…" — Phase 4 presents an explicit equal choice (no auto-default, no bandwidth fallback). Auto-fallback to 2D on mobile/low-bandwidth is a future enhancement, out of scope here.
