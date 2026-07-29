# Plan: Student Unity Auto-Join + Role Lock (Phase 5)

## Summary
When a student opens the 3D student link (`/#room=<CODE>&role=play`, no host token), the Unity WebGL build must read the room code from the URL hash, auto-`JoinRoom` as a passive spectator, and hard-lock the client to the non-host role — hiding the `EventPanel`, race controls, and Host/Setup UI so no host controls exist and `IsHost` can never flip to `true`. This is the exact mirror of Phase 2's `HostLaunchBootstrap`, reusing the hash parser, `NetworkManager.JoinRoom` (which already sets `IsHost=false`), and `RaceUI.ApplyRole` — the new work is a student bootstrap, a pure decision helper, and a role-lock guard.

## User Story
As a **student in a live classroom**, I want to **open the shared 3D link and immediately watch the live race**, so that I **join with zero typing and cannot accidentally (or deliberately) trigger game events or hijack the host role**.

## Problem → Solution
**Current state**: The 3D student link (`buildStudentPlayUrl` → `/#room=<CODE>&role=play`) just loads the Unity build at `/`; Unity ignores the hash, so the student sees the manual Host/Join chooser and must type the 6-char room code. Nothing stops them from clicking Host. → **Desired state**: Unity reads `role=play` + `room` from the hash, auto-joins the room as a spectator, and locks the UI to Student — EventPanel/Controls/Setup hidden, JoinScreen bypassed, `IsHost=false` and unchangeable from the UI.

## Metadata
- **Complexity**: Medium
- **Source PRD**: `.claude/PRPs/prds/role-bound-game-links.prd.md`
- **PRD Phase**: Phase 5 — Student Unity auto-join + role lock
- **Estimated Files**: 6 changed (3 new `.cs` + 3 `.cs.meta`, 2 updated `.cs`, 1 scene wiring) — Unity-only; **no web-app changes** (the URL + landing page shipped in Phase 4)

---

## UX Design

### Before
```
Student clicks "进入 3D 游戏" on landing page
        ↓
Unity WebGL loads at /#room=ABC123&role=play
        ↓
┌─────────────────────────────────────────┐
│  Unity ignores the hash.                 │
│  Student sees:                           │
│   [ Host Room ]   ← student CAN click    │
│   Room code: [______]  ← must type       │
│   Team name: [______]                    │
│   [ Join ]                               │
└─────────────────────────────────────────┘
```

### After
```
Student clicks "进入 3D 游戏" on landing page
        ↓
Unity WebGL loads at /#room=ABC123&role=play
        ↓
┌─────────────────────────────────────────┐
│  StudentJoinBootstrap reads the hash.    │
│  Auto-JoinRoom("ABC123"), IsHost=false.  │
│  Role locked → Student.                  │
│   • EventPanel        → HIDDEN           │
│   • Race controls     → HIDDEN           │
│   • Host/Setup screen → HIDDEN           │
│   • JoinScreen        → auto-dismissed   │
│  → Live 3D race, spectator camera.       │
│  No host controls exist anywhere.        │
└─────────────────────────────────────────┘
```

### Interaction Changes
| Touchpoint | Before | After | Notes |
|---|---|---|---|
| 3D link opens Unity | Manual Host/Join chooser shown | Auto-joins from URL hash | Zero typing |
| Room code entry | Student types 6 chars | Read from `room` hash param | `buildStudentPlayUrl` already emits it |
| Host button | Visible & clickable by student | Setup screen hidden by role lock; server rejects `create_room` without token (Phase 1) | UI hide = defense-in-depth; server token = authority |
| EventPanel | Not role-gated on this entry | Hidden via `RaceUI.ApplyRole(Student)` | Core success signal |
| Page reload | Loses room, back to manual entry | Hash retained → auto-rejoins same room | Deliberate: no token to protect, so we keep the hash (see Task 2 GOTCHA) |

---

## Mandatory Reading

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 (critical) | `Assets/Scripts/UI/HostLaunchBootstrap.cs` | 1-65 | **The exact template to mirror.** Copy its structure for `StudentJoinBootstrap`; also add the role-guard fix here (Task 4) |
| P0 (critical) | `Assets/Scripts/UI/HostAutoInjectDecision.cs` | 1-16 | Template for the pure `StudentJoinDecision` |
| P0 (critical) | `Assets/Scripts/UI/HostLaunchParams.cs` | 9-31 | **Reused as-is** — `ParseHash` handles the student hash too |
| P0 (critical) | `Assets/Scripts/UI/RaceUI.cs` | 9-93 | `SetRoleFromNetwork`/`ApplyRole`/`OnStateChanged` — the role-lock enforcement point (Task 3 edits here) |
| P0 (critical) | `Assets/Scripts/Network/NetworkManager.cs` | 33, 148-192, 290-303 | `IsHost`, `JoinRoom` (sets `IsHost=false`), `OnRoomJoined`, `PersistSession` |
| P1 (important) | `web-app/client/src/gameLaunch.js` | 12-18 | Authoritative student URL format: `#room=<CODE>&role=play` — **`role` value is `play`, not `student`** |
| P1 (important) | `Assets/Scripts/UI/JoinScreen.cs` | 22-91 | Student screen; `OnRoomJoined` self-hides on join (line 87-91). Confirms auto-join dismisses it |
| P1 (important) | `Assets/Plugins/WebGL/WebSocketBridge.cs` | 185-192, 218-223 | `GetPageUrl()` (returns `""` in Editor), `ClearUrlHash()` wrappers |
| P1 (important) | `Assets/Scripts/Network/NetworkSync.cs` | 76-84, 172-183, 287-335 | Existing `IsHost` broadcast guards + visual-only student path — already enforce student passivity |
| P2 (reference) | `Assets/Tests/EditMode/HostAutoInjectDecisionTests.cs` | 1-56 | Test template for `StudentJoinDecisionTests` |
| P2 (reference) | `Assets/Tests/EditMode/Tests.asmdef` | all | Test assembly config (`EDIRacing.Runtime` ref, Editor-only) |
| P2 (reference) | `Server/server.js` | 368-408, 541-608 | `join_room` (role `student`, no token) + host-only relay gating — the server backstop |
| P2 (reference) | `web-app/client/src/pages/JoinLandingPage.jsx` | 1-27 | The landing page that emits the 3D link (Phase 4 — unchanged here) |

## External Documentation

No external research needed — feature uses established internal patterns (mirrors Phase 2 `HostLaunchBootstrap`, reuses existing `NetworkManager.JoinRoom` and `RaceUI.ApplyRole`, and the server `join_room` path already exists and is role-gated).

---

## Patterns to Mirror

### NAMING_CONVENTION
```csharp
// SOURCE: Assets/Scripts/UI/HostAutoInjectDecision.cs:5-16
// Pure, MonoBehaviour-free, EditMode-testable helper. Namespace-free static class.
public static class HostAutoInjectDecision
{
    public static bool ShouldAutoInject(string role, string surveyId)
    {
        return role == "host" && !string.IsNullOrWhiteSpace(surveyId);
    }
}
```

### BOOTSTRAP_PATTERN (auto-role on launch)
```csharp
// SOURCE: Assets/Scripts/UI/HostLaunchBootstrap.cs:19-64
private void Start()
{
    if (NetworkManager == null || RaceUI == null) { /* log + return */ return; }

    var p = HostLaunchParams.ParseHash(WebSocketBridge.GetPageUrl());   // reads window.location.href hash

    if (p.TryGetValue("role", out var role) && role == "host")
    {
        p.TryGetValue("token", out var token);
        p.TryGetValue("survey", out var surveyId);
        // ... subscribe OnRoomCreated before CreateRoom ...
        NetworkManager.CreateRoom(token);
        WebSocketBridge.ClearUrlHash();          // host: strip so reload doesn't re-mint with stale token
        RaceUI.SetRoleFromNetwork(true);         // lock UI to Professor
        return;
    }

    if (NetworkManager.HasPersistedHostSession()) // reload-resume fallback
    {
        NetworkManager.ResumeHostSession();
        RaceUI.SetRoleFromNetwork(true);
    }
}
```

### HASH_PARSER (reused as-is)
```csharp
// SOURCE: Assets/Scripts/UI/HostLaunchParams.cs:11-30
public static Dictionary<string, string> ParseHash(string url)
{
    var dict = new Dictionary<string, string>();
    if (string.IsNullOrEmpty(url)) return dict;
    int hash = url.IndexOf('#');
    string frag = hash >= 0 ? url.Substring(hash + 1) : "";
    if (frag.StartsWith("?")) frag = frag.Substring(1);
    foreach (var pair in frag.Split('&')) { /* split on first '=', percent-decode, dict[key]=val */ }
    return dict;
}
```

### JOIN_ROOM (student entry — already sets IsHost=false)
```csharp
// SOURCE: Assets/Scripts/Network/NetworkManager.cs:165-181
public void JoinRoom(string code, string teamName = "")
{
    manualDisconnect = false;
    Connect();
    pendingAction = () => {
        IsHost = false;                                   // <-- non-host role at the network layer
        SendMessage(new JoinRoomMessage {
            roomCode = code.ToUpper(), sessionId = sessionId, teamName = teamName
        });
    };
    if (IsConnected) { pendingAction(); pendingAction = null; }
}
```

### ROLE_LOCK (visibility enforcement)
```csharp
// SOURCE: Assets/Scripts/UI/RaceUI.cs:53-77
public void SetRoleFromNetwork(bool isHost)
{
    Role = isHost ? UserRole.Professor : UserRole.Student;
    ApplyRole();
    OnStateChanged(CurrentState);
}

private void ApplyRole()
{
    bool isProfessor = Role == UserRole.Professor;
    if (Events != null)     Events.gameObject.SetActive(isProfessor);      // EventPanel — host only
    if (Controls != null)   Controls.gameObject.SetActive(isProfessor);
    if (Setup != null)      Setup.gameObject.SetActive(isProfessor);       // Host/Setup screen — host only
    if (JoinScreen != null) JoinScreen.gameObject.SetActive(!isProfessor); // student only
    // camera: Free for professor, Spectator for student
}
```

### TEST_STRUCTURE
```csharp
// SOURCE: Assets/Tests/EditMode/HostAutoInjectDecisionTests.cs:1-20
using NUnit.Framework;

[TestFixture]
public class HostAutoInjectDecisionTests
{
    [Test]
    public void ShouldAutoInject_HostWithSurvey_ReturnsTrue()
    {
        // Arrange / Act
        bool result = HostAutoInjectDecision.ShouldAutoInject("host", "42");
        // Assert
        Assert.IsTrue(result);
    }
    // ...empty/null/whitespace survey → false; student role → false; empty role → false
}
```

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `Assets/Scripts/UI/StudentJoinDecision.cs` | CREATE | Pure `ShouldAutoJoin(role, room)` — mirrors `HostAutoInjectDecision`, EditMode-testable |
| `Assets/Scripts/UI/StudentJoinDecision.cs.meta` | CREATE | Unity import meta (hand-generated GUID, as in Phase 4) |
| `Assets/Scripts/UI/StudentJoinBootstrap.cs` | CREATE | MonoBehaviour: parse hash → `JoinRoom` → lock role. Mirror of `HostLaunchBootstrap` |
| `Assets/Scripts/UI/StudentJoinBootstrap.cs.meta` | CREATE | Unity import meta |
| `Assets/Scripts/UI/RaceUI.cs` | UPDATE | Add `roleLocked` guard + `LockAsStudent()` so a URL-launched student can never flip to Professor |
| `Assets/Scripts/UI/HostLaunchBootstrap.cs` | UPDATE | Guard the persisted-host-resume fallback so a `role=play` launch in a previously-host browser is **not** hijacked into resuming the old host session |
| `Assets/Tests/EditMode/StudentJoinDecisionTests.cs` | CREATE | Mirror `HostAutoInjectDecisionTests` |
| `Assets/Tests/EditMode/StudentJoinDecisionTests.cs.meta` | CREATE | Unity import meta |
| `Assets/Scenes/complete_track_demo.unity` | UPDATE (runtime QA) | Attach `StudentJoinBootstrap` to the launch GameObject; assign `NetworkManager` + `RaceUI` refs. Prefer UnitySkills REST API `http://localhost:8090` |

## NOT Building

- **Web-app / landing-page changes** — the 3D link (`buildStudentPlayUrl`) and `JoinLandingPage` shipped in Phase 4. Phase 5 only makes Unity *consume* the existing URL.
- **2D spectator wiring** — that is Phase 6 (`/live/:roomCode` from the landing page). Out of scope.
- **Server changes** — `join_room` (role `student`, no token) and host-only relay gating already exist (`Server/server.js:368-408, 541-608`). No new endpoints or token logic.
- **Student car driving / input** — join stays strictly visual-only (PRD "Won't"). The student watches; they do not drive.
- **Team-based own-car highlighting for URL-joined students** — the 3D link carries no team, so auto-join is an anonymous spectator (empty `teamName`); the existing `ownCarIndex` emissive highlight (`NetworkSync.cs:314-331`) only fires when a team matches. Team selection for URL joiners is deferred (see Open Questions in the PRD).
- **A dedicated student reload-resume/persistence path in `NetworkManager`** — not needed: we keep the URL hash so reload re-runs the bootstrap (see Task 2). No `HasPersistedStudentSession` mirror.

---

## Step-by-Step Tasks

### Task 1: Pure decision helper `StudentJoinDecision`
- **ACTION**: Create `Assets/Scripts/UI/StudentJoinDecision.cs`.
- **IMPLEMENT**:
  ```csharp
  public static class StudentJoinDecision
  {
      // Auto-join only when the URL declares the student-play role AND carries a room code.
      public static bool ShouldAutoJoin(string role, string room)
      {
          return role == "play" && !string.IsNullOrWhiteSpace(room);
      }
  }
  ```
- **MIRROR**: `HostAutoInjectDecision` (NAMING_CONVENTION pattern) — no namespace, `public static`, single pure method.
- **IMPORTS**: none.
- **GOTCHA**: The role value is **`play`** (from `gameLaunch.js buildStudentPlayUrl`), NOT `student`. Match the URL the web-app actually emits. Do not add a `role=="student"` alias unless Phase 4's builder changes.
- **VALIDATE**: Compiles under `EDIRacing.Runtime`; covered by Task 5 tests.

### Task 2: Bootstrap `StudentJoinBootstrap`
- **ACTION**: Create `Assets/Scripts/UI/StudentJoinBootstrap.cs` — a MonoBehaviour with serialized `NetworkManager` and `RaceUI` fields (mirror `HostLaunchBootstrap`'s fields at lines 16-17).
- **IMPLEMENT** (in `Start()`):
  ```csharp
  private void Start()
  {
      if (NetworkManager == null || RaceUI == null) { Debug.LogWarning("StudentJoinBootstrap: missing refs"); return; }

      var p = HostLaunchParams.ParseHash(WebSocketBridge.GetPageUrl());
      p.TryGetValue("role", out var role);
      p.TryGetValue("room", out var room);

      if (!StudentJoinDecision.ShouldAutoJoin(role, room)) return;   // not a student launch → leave manual UI intact

      NetworkManager.JoinRoom(room, "");        // empty teamName = anonymous spectator; server accepts
      RaceUI.LockAsStudent();                   // hide EventPanel/Controls/Setup, show JoinScreen, lock role
      // NOTE: deliberately do NOT ClearUrlHash — see GOTCHA
  }
  ```
- **MIRROR**: BOOTSTRAP_PATTERN (`HostLaunchBootstrap.cs:19-64`), but call `JoinRoom` instead of `CreateRoom`, and `LockAsStudent()` instead of `SetRoleFromNetwork(true)`.
- **IMPORTS**: `using UnityEngine;` (reference `HostLaunchParams`, `WebSocketBridge`, `StudentJoinDecision`, `NetworkManager`, `RaceUI` — all in `EDIRacing.Runtime`, no extra `using`).
- **GOTCHA (keep the hash)**: Unlike the host branch, do **not** call `WebSocketBridge.ClearUrlHash()`. The student hash carries no secret (no token), and retaining `#room=<CODE>&role=play` lets a full page reload re-run this bootstrap and auto-rejoin the same room — cheaper and more robust than a persisted-session path. `JoinRoom` uppercases and re-sends idempotently; if the room has since closed, the server replies `error`/`Room not found` and the existing `JoinScreen` status surfaces it.
- **GOTCHA (Editor no-op)**: `WebSocketBridge.GetPageUrl()` returns `""` in the Editor (WebSocketBridge.cs:185-192), so `ParseHash("")` → empty dict → `ShouldAutoJoin(null, null)` → false. The bootstrap is inert in-Editor and in Play mode without a hash — nothing auto-fires. Good.
- **GOTCHA (mutual exclusivity with HostLaunchBootstrap)**: Both bootstraps may live on the same GameObject. A URL is either `role=host` or `role=play`, never both, so only the matching branch acts. Ordering of the two `Start()` calls is irrelevant because the non-matching one no-ops — **provided** Task 4's guard is applied (otherwise a stale-host localStorage flag could hijack a student launch).
- **VALIDATE**: `asset_refresh` → zero compile errors across asmdefs; in-Editor Play mode with no hash does nothing.

### Task 3: Role-lock hardening in `RaceUI`
- **ACTION**: Update `Assets/Scripts/UI/RaceUI.cs` — add a `roleLocked` flag and a `LockAsStudent()` method; guard `SetRoleFromNetwork` so a lock cannot be overridden.
- **IMPLEMENT**:
  ```csharp
  private bool roleLocked;

  public void SetRoleFromNetwork(bool isHost)
  {
      if (roleLocked) return;                 // once locked to student, ignore any later flip
      Role = isHost ? UserRole.Professor : UserRole.Student;
      ApplyRole();
      OnStateChanged(CurrentState);
  }

  public void LockAsStudent()
  {
      Role = UserRole.Student;
      ApplyRole();
      OnStateChanged(CurrentState);
      roleLocked = true;                      // block HandleRoomCreated / manual Host from flipping back
  }
  ```
- **MIRROR**: ROLE_LOCK pattern — reuse existing `ApplyRole()`/`OnStateChanged()` verbatim; only add the guard + lock setter.
- **IMPORTS**: none (same file).
- **GOTCHA**: `HandleRoomCreated` (RaceUI.cs:48) currently calls `SetRoleFromNetwork(true)` on any `OnRoomCreated`. A student never creates a room, but the `roleLocked` guard makes this defense-in-depth: even if something fired it, the student stays Student. Note the *authoritative* block on host actions is the Phase-1 server token check — UI hiding is layered defense, not the sole guard (state this in QA).
- **GOTCHA**: `OnStateChanged` (RaceUI.cs:79-93) already re-gates `Events`/`Controls` behind `Role == Professor` each state transition, so the EventPanel stays hidden across `Waiting → Racing → Finished`. No per-state re-show for a locked student.
- **VALIDATE**: Compiles; manual/runtime QA confirms EventPanel/Controls/Setup stay hidden through a full race and after any `room_created` message.

### Task 4: Prevent host-resume hijack of a student launch (`HostLaunchBootstrap`)
- **ACTION**: Update `Assets/Scripts/UI/HostLaunchBootstrap.cs` — guard the `HasPersistedHostSession()` fallback so it does not fire when the URL declares a student role.
- **IMPLEMENT**: capture `role` from the parsed hash and gate the fallback:
  ```csharp
  var p = HostLaunchParams.ParseHash(WebSocketBridge.GetPageUrl());
  p.TryGetValue("role", out var role);

  if (role == "host") { /* existing host branch unchanged */ return; }

  // Only resume a persisted host session for a *plain* reload (no student role in URL).
  if (role != "play" && NetworkManager.HasPersistedHostSession())
  {
      NetworkManager.ResumeHostSession();
      RaceUI.SetRoleFromNetwork(true);
  }
  ```
- **MIRROR**: existing `HostLaunchBootstrap.Start()` structure — minimal, additive guard.
- **IMPORTS**: none.
- **GOTCHA**: Without this guard, a student who opens the 3D link in a browser that *previously* hosted (localStorage `edi-was-host=="1"`) would have `HasPersistedHostSession()` return `true` and get resumed as **host** — a real role-escalation/UX bug. The student join later self-heals persistence (`PersistSession(code,false)` on `room_joined`, NetworkManager.cs:296), but the first launch must not hijack. This guard is the fix.
- **VALIDATE**: Compiles; runtime QA: set `edi-was-host="1"` in localStorage, open `#room=X&role=play`, confirm the client joins as student (not resumes as host).

### Task 5: EditMode tests for `StudentJoinDecision`
- **ACTION**: Create `Assets/Tests/EditMode/StudentJoinDecisionTests.cs`.
- **IMPLEMENT**: `[TestFixture]` with `[Test]` methods, `Method_Scenario_Expected` naming:
  - `ShouldAutoJoin_PlayRoleWithRoom_ReturnsTrue` → `("play","ABC123")` → true
  - `ShouldAutoJoin_EmptyRoom_ReturnsFalse` → `("play","")` → false
  - `ShouldAutoJoin_WhitespaceRoom_ReturnsFalse` → `("play","   ")` → false
  - `ShouldAutoJoin_NullRoom_ReturnsFalse` → `("play",null)` → false
  - `ShouldAutoJoin_HostRole_ReturnsFalse` → `("host","ABC123")` → false
  - `ShouldAutoJoin_EmptyRole_ReturnsFalse` → `("","ABC123")` → false
  - `ShouldAutoJoin_StudentAliasRole_ReturnsFalse` → `("student","ABC123")` → false (documents that only `play` is accepted)
- **MIRROR**: TEST_STRUCTURE (`HostAutoInjectDecisionTests.cs`) — pure static-function asserts, no `[SetUp]`/`[TearDown]`.
- **IMPORTS**: `using NUnit.Framework;`.
- **GOTCHA**: Assembly `Tests.asmdef` references `EDIRacing.Runtime` and is Editor-only with `defineConstraints: ["UNITY_INCLUDE_TESTS"]` — `StudentJoinDecision` must live in `EDIRacing.Runtime` (i.e. under `Assets/Scripts/`) to be referenceable. Do not place it under a test-only folder.
- **VALIDATE**: `test_run` EditMode (may be blocked in UnitySkills `auto` panel mode → run via Editor Test Runner or Bypass, matching Phase 2/3/4 precedent). File must at minimum compile via `asset_refresh`.

### Task 6: Scene wiring (runtime QA)
- **ACTION**: Attach `StudentJoinBootstrap` to the launch GameObject in `Assets/Scenes/complete_track_demo.unity` (the same GameObject that carries `HostLaunchBootstrap`), and assign its `NetworkManager` + `RaceUI` references.
- **IMPLEMENT**: Prefer the UnitySkills REST API (`http://localhost:8090`) per project technical preferences; `Assets/Editor/SceneWiring.cs` is the existing helper that wires the analogous `HostLaunchBootstrap` — extend or mirror it.
- **MIRROR**: however `HostLaunchBootstrap` is wired in the scene (see Phase 2 remaining-work note — that wiring is itself runtime-QA-pending).
- **IMPORTS**: n/a (Editor/scene operation).
- **GOTCHA**: The feature is inert until wired. Unity editor is bound to the **main checkout**, not this worktree (per Phase 2/3/4 reports), so scene wiring + the EditMode run are runtime-QA-pending and fold into Phase 7, not this changeset.
- **VALIDATE**: In a running build, opening `#room=<live>&role=play` joins the room, JoinScreen dismisses, EventPanel/Controls/Setup are absent.

---

## Testing Strategy

### Unit Tests

| Test | Input | Expected Output | Edge Case? |
|---|---|---|---|
| `ShouldAutoJoin_PlayRoleWithRoom_ReturnsTrue` | `("play","ABC123")` | `true` | No |
| `ShouldAutoJoin_EmptyRoom_ReturnsFalse` | `("play","")` | `false` | Yes |
| `ShouldAutoJoin_WhitespaceRoom_ReturnsFalse` | `("play","   ")` | `false` | Yes |
| `ShouldAutoJoin_NullRoom_ReturnsFalse` | `("play",null)` | `false` | Yes |
| `ShouldAutoJoin_HostRole_ReturnsFalse` | `("host","ABC123")` | `false` | Yes |
| `ShouldAutoJoin_EmptyRole_ReturnsFalse` | `("","ABC123")` | `false` | Yes |
| `ShouldAutoJoin_StudentAliasRole_ReturnsFalse` | `("student","ABC123")` | `false` | Yes (guards against wrong role value) |

> `HostLaunchParams.ParseHash` is already covered by `HostLaunchParamsTests` (8 tests) and is reused unchanged. `buildStudentPlayUrl` is already covered by web-app vitest (Phase 4). `RaceUI.LockAsStudent`/`roleLocked` is a MonoBehaviour with serialized fields — verified by runtime QA (consistent with the project's pure-class-only unit-test convention; Phase 2's `RaceUI` change was likewise not unit-tested).

### Edge Cases Checklist
- [x] Empty / null / whitespace room code → `ShouldAutoJoin` false (no auto-join, manual UI intact)
- [x] Wrong role value (`host`, `student`, empty) → no student auto-join
- [x] In-Editor / no-hash launch → `GetPageUrl()==""` → inert
- [x] Page reload → hash retained → idempotent re-join
- [x] Dead/closed room on (re)join → server `error` surfaced by existing JoinScreen status
- [x] Stale `edi-was-host` localStorage on a student launch → Task 4 guard prevents host-resume hijack
- [x] `room_created` message reaching a locked student → `roleLocked` guard keeps Student role
- [ ] Network drop mid-watch → handled by existing `NetworkManager` reconnect coroutine (not new here)

---

## Validation Commands

### Static Analysis / Unity Compile
```bash
# Preferred: UnitySkills REST API triggers a domain reload + reports console errors.
curl -s -X POST http://localhost:8090/asset_refresh
curl -s http://localhost:8090/console_get_logs   # EXPECT: zero compile errors across all 3 asmdefs
```
EXPECT: Zero C# compile errors. (If UnitySkills is unavailable, open the project in the Editor and confirm a clean console.)

### Unit Tests (Unity EditMode)
```bash
# Via UnitySkills (may be gated in `auto` panel mode — then run in Editor Test Runner or Bypass mode)
curl -s -X POST http://localhost:8090/test_run -d '{"mode":"EditMode","filter":"StudentJoinDecisionTests"}'
```
EXPECT: 7/7 `StudentJoinDecisionTests` pass; no regression in `HostLaunchParamsTests`/`HostAutoInjectDecisionTests`/`StudentLinkBuilderTests`.

### Web-app regression (should be untouched)
```bash
cd web-app && npx vitest run
```
EXPECT: All existing tests green (37/37 as of Phase 4) — Phase 5 changes no web-app files.

### Manual / Runtime Validation (folds into Phase 7)
- [ ] Wire `StudentJoinBootstrap` in `complete_track_demo.unity`; assign `NetworkManager` + `RaceUI`.
- [ ] Professor hosts a room (Phase 2 flow); copy the student link from the host screen.
- [ ] Open the "进入 3D 游戏" link → Unity loads `#room=<CODE>&role=play` → **auto-joins**, JoinScreen dismisses, live 3D race visible.
- [ ] Confirm **no** EventPanel, race controls, or Host/Setup UI anywhere; camera is Spectator.
- [ ] Reload the student tab → auto-rejoins the same room (hash retained).
- [ ] Adversarial: with `REQUIRE_HOST_TOKEN=true`, attempt `create_room`/`event_triggered` from the student socket → server rejects (Phase 1 backstop). `IsHost` stays `false`.
- [ ] Adversarial: seed `edi-was-host="1"` in localStorage, open the student link → joins as student, NOT resumed as host (Task 4).

---

## Acceptance Criteria
- [ ] Opening `/#room=<CODE>&role=play` auto-joins the room with no typed code and no Host/Join chooser.
- [ ] `IsHost` is `false` for the student client and cannot be flipped from the UI (`roleLocked`).
- [ ] `EventPanel`, race controls, and Host/Setup screen are hidden for the student across all game states.
- [ ] A student launch in a previously-host browser joins as student (no host-session hijack).
- [ ] `StudentJoinDecisionTests` (7) pass; no regression in existing EditMode/web-app tests.
- [ ] No web-app or server files changed.

## Completion Checklist
- [ ] Code follows discovered patterns (`HostLaunchBootstrap`/`HostAutoInjectDecision` mirror)
- [ ] `role=="play"` used (not `student`); matches `gameLaunch.js`
- [ ] Hash intentionally retained for students (documented)
- [ ] Role lock guards against flip-back + stale-host hijack
- [ ] `.cs.meta` files hand-generated with fresh GUIDs (Unity not bound to worktree)
- [ ] Tests mirror existing EditMode structure
- [ ] No hardcoded room codes / no web-app or server scope additions
- [ ] Self-contained — no questions needed during implementation

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Scene wiring pending (feature inert until wired) | H | M | Explicit Task 6 + Phase 7 runtime QA; same posture as Phase 2/3/4 |
| Stale `edi-was-host` hijacks student launch | M | H | Task 4 guard (role-gated host-resume fallback) |
| UI-hide bypassed by a determined user (re-activating hidden Host GameObject) | L | M | Server-side host-token enforcement (Phase 1) is the authoritative backstop; UI lock is defense-in-depth |
| `.cs.meta` GUID collision / Unity re-import churn | L | L | Generate fresh GUIDs matching the project's 2-line `.cs.meta` format (Phase 4 precedent) |
| Retained hash confuses back/forward nav in WebGL | L | L | Single-page WebGL build; hash is the only state carrier; acceptable and reversible |
| EditMode `test_run` blocked in UnitySkills `auto` mode | M | L | Run via Editor Test Runner or Bypass; compilation still verified via `asset_refresh` |

## Notes
- **Why a separate `StudentJoinBootstrap` (not a branch inside `HostLaunchBootstrap`)**: mirrors the project's file-per-concern layout (`HostLaunchBootstrap` + `HostAutoInjectDecision` + `HostLaunchParams`), keeps the pure decision independently testable, and the two bootstraps are mutually exclusive by role value so they coexist safely on one GameObject. Task 4's guard is what makes that coexistence airtight.
- **Role value discrepancy caught during exploration**: the PRD prose and one explorer sketch said `role=student`, but Phase 4's shipped `buildStudentPlayUrl` emits `role=play`. This plan is authoritative on `play`; a `role=="student"` alias is explicitly rejected (Task 5 test locks this in).
- **No `ClearUrlHash` for students** is a deliberate deviation from the host pattern — see Task 2 GOTCHA. If a future requirement wants the address bar cleaned, add a persisted-student-session resume instead (out of scope here).
- **Server + NetworkSync already enforce passivity**: `join_room` registers the socket as `student`, and every host broadcast in `NetworkSync.cs` is `IsHost`-guarded, so a student is structurally receive-only even before the UI lock. Phase 5 adds the *entry* automation and the *UI* lock; it does not weaken any existing guard.
