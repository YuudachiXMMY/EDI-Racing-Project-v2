# Role-Bound Game Links (Professor Host Link + Student Join/Watch Link)

## Problem Statement

In a live classroom session, the professor runs a survey and then hosts the racing game, while students watch/participate from their own browsers. Today the Unity WebGL build is served to **everyone at the same root URL `/`**, and whether a browser becomes the host (with event-trigger authority) or a student is decided **inside the running game by clicking "Host" vs "Join"** — not by the link. This means any student can click "Host," create a room, and trigger events, and the professor must manually create a room and hand-copy a 6-char code to push survey data into the game. The workflow does not match how a professor actually teaches: one authoritative host entry, one shareable audience entry.

## Evidence

- Unity build is served to all clients at the origin root; there is no game-side URL role distinction — role is chosen in-app via Host/Join buttons. (`Deploy/nginx/nginx.conf:96-98`; `SetupScreen.cs:134-140`; `JoinScreen.cs:84`)
- The server **already** enforces that only the room's single `professor` socket is relayed outward and that `event_triggered` only flows from the host — so the authorization backbone exists, but role is claimed at connect time by message type, not bound to a URL or a credential. (`Server/server.js:462-521`; `NetworkSync.cs:172-183`)
- Survey→game handoff is manual: the professor opens `SendToGameModal` and types the 6-char room code to push data. (`web-app/client/src/components/SendToGameModal.jsx`; `Server/server.js:437-452`)
- Assumption to validate: professors find the current "click Host, copy room code, tell students to type it" flow error-prone under live-classroom time pressure. Needs a short professor walkthrough to confirm.

## Proposed Solution

Bind game role to the **link** and back it with **server-side token enforcement**, and collapse the survey→host handoff into a single Dashboard action:

1. **Professor host link** — a "主持游戏 / Host Game" button on the authenticated survey Dashboard launches the Unity build already in host mode, carrying a short-lived **host token** and the selected survey id. The game auto-creates the room, auto-injects the survey responses (no manual "Send to Game"), and shows the professor-only `EventPanel`.
2. **Student join/watch link** — auto-generated when hosting begins, embedding the room code (and **no** host token). It opens a lightweight landing page where the student picks **"进入 3D 游戏" (playable/visual-only join)** or **"2D 观战" (spectator dashboard)**. Neither path exposes Host UI or event triggers.
3. **Server-side enforcement** — the WS relay rejects `create_room` and `event_triggered` (and other host-only messages) unless the socket presented a valid host token. URL role is thus not merely cosmetic: a hand-crafted host URL without a valid token cannot trigger events.

This reuses the existing role-based relay, the visual-only student join path, the 2D spectator view, and the jslib WebSocket bridge — the change is primarily (a) URL/entry plumbing, (b) a token check, and (c) auto-inject wiring.

## Key Hypothesis

We believe **link-bound roles with a one-click host launch and auto-injected survey data** will **eliminate role confusion and manual room-code handoff** for **professors running live classroom sessions**.
We'll know we're right when a professor can go from "survey complete" to "students watching a live race" **without ever typing a room code or clicking an in-game Host button**, and when a student on the student link **cannot** trigger a game event even by manipulating the URL.

## What We're NOT Building

- **Persistent accounts / matchmaking for students** — student links are ephemeral per-session; no student login. The classroom is a trust context with one authenticated professor.
- **Multiple simultaneous hosts / co-hosts** — one professor host per room remains the model (matches current `professor` grace-period design, `Server/server.js:6,104-116`).
- **Rewriting the 2D spectator or the visual-only join gameplay** — both are reused as-is; we only change how they're reached and gated.
- **Real student-side car control / input** — "join" stays visual-only (watch own team's car); students do not drive. (Confirm in Open Questions.)

## Success Metrics

| Metric | Target | How Measured |
|--------|--------|--------------|
| Professor steps from "survey done" → "race live for students" | ≤ 2 clicks, 0 typed room codes | Professor walkthrough / session recording |
| Unauthorized event trigger from student link | 0 | Attempt `event_triggered` and `create_room` from a student-link socket; server must reject |
| Student self-service (no professor help to join) | 100% of students reach a view via the shared link alone | Classroom observation / join telemetry (existing WS `student_count`) |
| Survey data reaching the game without manual send | 100% of host launches | Verify `survey_import` fires automatically on host launch |

## Open Questions

- [ ] Should students ever actively drive a car, or is "join" strictly visual-only (current behavior)?
- [x] Host token lifetime & scope: single-use per launch, or reusable while the professor session is valid? What happens on professor refresh/reconnect (there is a 60s grace period today — does the token survive it)? **RESOLVED (2026-07-28)**: create-scoped, surveyId-bound, 5-min TTL, stateless (reusable within TTL — LOW-2 accepted for the single-professor classroom trust model since the token never enters the student link). The token authorizes `create_room` only; professor refresh/reconnect uses the existing `sessionId` path (`Server/server.js:412-443`), so the token does **not** need to survive the 60s grace period. Phase 2 must persist `sessionId` client-side and prefer `reconnect` over a fresh `create_room` on reload.
- [ ] Should the student landing page default to 3D or 2D, and should low-bandwidth/mobile auto-fall back to 2D?
- [ ] Is the student link tied to a specific survey/room only, or a stable per-class link reused across sessions?
- [ ] Does the professor still need a manual "re-send data" path if the survey is edited after the race starts?

---

## Users & Context

**Primary User — The Professor (session host)**
- **Who**: An instructor running a live in-class activity; keyboard-operated on a projected screen; authenticated in the web app.
- **Current behavior**: Logs into Dashboard → builds/opens a survey → shares `/survey/#/s/<code>` → collects responses → opens the game at `/` → clicks Host → copies the room code → opens `SendToGameModal` and types the code → triggers events via `EventPanel`.
- **Trigger**: Survey responses are in; it's time to run the race in front of the class.
- **Success state**: One click launches the hosted game with data already loaded; a student link is ready to share; only the professor can trigger events.

**Secondary User — The Student (audience/participant)**
- **Who**: A student on their own laptop/phone browser, no login.
- **Current behavior**: Either loads the full Unity build at `/` and manually types the room code to join (visual-only), or opens `/survey/#/live/:roomCode` for the 2D dashboard.
- **Trigger**: Professor shares the student link.
- **Success state**: Opens the link, picks 3D or 2D, and immediately sees the live race — with no host controls.

**Job to Be Done**
When **my survey is complete and I want to run the race live in class**, I want to **launch the hosted game and give students a watch link in one step**, so I can **teach without fumbling room codes or worrying a student will hijack the host role**.

**Non-Users**
Remote/asynchronous players, unauthenticated hosts, and anyone needing student accounts or persistent progression — out of scope for this classroom tool.

---

## Solution Detail

### Core Capabilities (MoSCoW)

| Priority | Capability | Rationale |
|----------|------------|-----------|
| Must | Dashboard "Host Game" action that launches Unity in host mode with a host token + survey id | The core one-click professor entry |
| Must | Server-side host-token validation gating `create_room` + all host-only/event messages | Makes role binding real, not cosmetic (decision: server-side token enforcement) |
| Must | Auto-inject selected survey responses into the game on host launch | Removes manual Send-to-Game step (decision: auto-inject) |
| Must | Auto-generated student link embedding room code, no host token | The shareable audience entry |
| Must | Student landing page with "3D 游戏" vs "2D 观战" choice; both hide Host/EventPanel | Decision: both, student chooses |
| Should | Student-link Unity entry auto-joins via URL room code (skip manual code entry) | Removes student-side friction |
| Should | Host client hides Join UI; student client hard-locks non-host role | Defense-in-depth beyond server token |
| Could | Copy/QR affordance for the student link on the professor screen | Faster classroom sharing |
| Won't | Student car control / driving input | Explicitly deferred; join stays visual-only |
| Won't | Multi-host / co-host, student accounts | Out of scope |

### MVP Scope

Minimum to validate the hypothesis:
1. Dashboard "Host Game" → launches Unity host with token + auto-injected survey data.
2. Server rejects host-only messages without a valid token.
3. Auto-generated student link → landing page → at least the **3D visual-only** path auto-joining by URL room code (2D can follow immediately after, since it already exists).

### User Flow (critical path)

**Professor**: Dashboard → (survey selected) → click **Host Game** → Unity opens as host, room auto-created, survey data auto-loaded, `EventPanel` visible, student link shown to copy → trigger events.

**Student**: Open student link → landing page → choose **3D** (auto-join visual-only, own team car highlighted) or **2D** (leaderboard/minimap/event feed) → watch live; no host controls anywhere.

---

## Technical Approach

**Feasibility**: HIGH — the authorization model, visual-only join, 2D spectator, and jslib WS bridge all already exist; the work is entry plumbing + a token check + auto-inject wiring.

**Architecture Notes**
- **Single origin preserved**: game at `/`, survey/API at `/survey`+`/api`, WS at `/ws` (`Deploy/nginx/nginx.conf:28-98`). New links are just parameterized entries into these, not new services.
- **Host token issuance**: mint a short-lived token from the authenticated web-app (professor is already logged in, bcrypt users, `web-app/src/routes/auth.js`) and pass it to the Unity host launch; the WS server validates it on `create_room` and host-only branches (extend `Server/server.js:253-278, 454-521`). Shared secret path already exists (`INTERNAL_SECRET` / `API_URL` between services, `Deploy/docker-compose.yml`).
- **Auto-inject**: on host launch, trigger the existing `survey_import` path (`Server/server.js:437-452`) automatically using the survey id carried in the launch URL, instead of the manual `SendToGameModal` flow.
- **URL role for Unity**: introduce distinct entries, e.g. host launch carrying `?role=host&token=…&survey=…` and student `?room=CODE&role=play|watch`; Unity reads them via the existing jslib page-URL/WS-URL bridge (`Assets/Plugins/WebGL/WebSocketBridge.jslib:5-12`; `NetworkManager.cs:101-112`) and auto-calls `CreateRoom`/`JoinRoom` while hiding the opposite UI.
- **Student landing page**: a new lightweight route in the existing React client (alongside `LiveRacePage`) offering the 3D-vs-2D choice.

**Technical Risks**

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Token leaks via URL/history if professor shares the wrong link | M | Short-lived, single-session token; never embed token in the student link; make the student link visually/structurally distinct |
| Unity auto-role reads URL before WS connects, races with reconnect/grace-period logic (`NetworkManager.cs:279-327`) | M | Gate auto-`CreateRoom`/`JoinRoom` on connection-ready; reuse existing reconnect backoff; token must survive the 60s professor grace period |
| WebGL query-param/token access differs across nginx/Traefik/Caddy edges | L | Verify jslib `GetPageWebSocketUrl`/URL access on all three edge configs (`Deploy/`); prefer hash or WS-handshake token over query string if caching interferes |
| Auto-inject fires before survey responses are final | L | Only enable Host Game when the survey has responses; allow professor to re-launch |

---

## Implementation Phases

<!--
  STATUS: pending | in-progress | complete
  PARALLEL: phases that can run concurrently
  DEPENDS: phases that must complete first
  PRP: link to generated plan file once created
-->

| # | Phase | Description | Status | Parallel | Depends | PRP Plan |
|---|-------|-------------|--------|----------|---------|----------|
| 1 | Host token model | Issue + validate a host token; server rejects host-only messages without it | complete | - | - | `.claude/PRPs/plans/completed/host-token-model.plan.md` (report: `.claude/PRPs/reports/host-token-model-report.md`) |
| 2 | Professor host launch | Dashboard "Host Game" launches Unity in host mode carrying token + survey id; Unity auto-creates room, hides Join UI | in-progress (code landed, compiles + web-app tests green; **scene wiring + runtime QA pending**) | with 3 | 1 | `.claude/PRPs/plans/completed/professor-host-launch.plan.md` (report: `.claude/PRPs/reports/professor-host-launch-report.md`) |
| 3 | Auto-inject survey data | On host launch, auto-run `survey_import` for the selected survey (drop manual Send-to-Game) | in-progress | with 2 | 1 | `.claude/PRPs/plans/auto-inject-survey-data.plan.md` |
| 4 | Student link + landing page | Auto-generate student link (room code, no token); landing page with 3D/2D choice | pending | - | 2 | - |
| 5 | Student Unity auto-join + role lock | 3D path auto-joins via URL room code, hides Host/EventPanel, hard-locks non-host role | pending | with 6 | 4 | - |
| 6 | Student 2D wiring | Route the 2D spectator (`/live/:roomCode`) from the landing page | pending | with 5 | 4 | - |
| 7 | End-to-end + adversarial QA | Full professor→student flow; verify a student link cannot trigger events even via crafted URL | pending | - | 3, 5, 6 | - |

### Phase Details

**Phase 1: Host token model**
- **Goal**: Make role enforcement real at the server.
- **Scope**: Token issuance from authenticated web-app; WS server validates on `create_room` and host-only/`event_triggered` branches; reject otherwise.
- **Success signal**: A socket without a valid token cannot create a room or trigger an event; a valid token can.

**Phase 2: Professor host launch**
- **Goal**: One-click hosted launch from the Dashboard.
- **Scope**: "Host Game" button; launch URL with token + survey id; Unity auto-`CreateRoom`, hide Join UI, show `EventPanel`.
- **Success signal**: Clicking Host Game yields a hosted room with the professor as host and no manual in-game steps.

**Phase 3: Auto-inject survey data**
- **Goal**: Remove the manual room-code handoff.
- **Scope**: Fire the existing `survey_import` automatically for the launched survey.
- **Success signal**: Survey responses appear in the game on host launch with zero manual sending.

**Phase 4: Student link + landing page**
- **Goal**: One shareable audience entry.
- **Scope**: Generate student link (room code, no token); landing page offering 3D vs 2D.
- **Success signal**: Opening the link shows the choice page tied to the live room.

**Phase 5: Student Unity auto-join + role lock**
- **Goal**: Frictionless, safe 3D watching.
- **Scope**: Auto-`JoinRoom` from URL; hide Host button + `EventPanel`; hard-lock non-host client role.
- **Success signal**: Student lands in visual-only 3D with no host controls; `IsHost` is false and unchangeable from the UI.

**Phase 6: Student 2D wiring**
- **Goal**: Offer the lightweight path.
- **Scope**: Route landing-page "2D 观战" to the existing `/live/:roomCode`.
- **Success signal**: 2D dashboard shows the live race for the chosen room.

**Phase 7: End-to-end + adversarial QA**
- **Goal**: Prove the workflow and the security boundary.
- **Scope**: Full flow test + attempts to trigger events/create rooms from a student-link socket and a crafted host URL without a token.
- **Success signal**: Professor flow works in ≤2 clicks/0 codes; all unauthorized host actions are rejected.

### Parallelism Notes

Phases 2 and 3 both depend only on Phase 1's token and touch different surfaces (launch plumbing vs data injection), so they run concurrently. Phases 5 and 6 are the two student paths off the same landing page (Phase 4) and are independent of each other. Phase 7 is the final barrier gathering all paths.

---

## Decisions Log

| Decision | Choice | Alternatives | Rationale |
|----------|--------|--------------|-----------|
| Professor game entry | Dashboard one-click host launch (auto-host + auto-data) | Game-root auto-detect; unified single page | Fewest steps under live-classroom time pressure; professor already authenticated on Dashboard |
| Student view | Both 3D playable-view and 2D spectator, student chooses on a landing page | 3D only; 2D only | Serves varied device/bandwidth; both paths already exist to reuse |
| Role security | Server-side host-token enforcement | URL + hidden UI only | Prevents a crafted host URL from triggering events; UI hiding alone is bypassable |
| Survey→game data | Auto-inject on host launch | Keep manual Send-to-Game | Eliminates the error-prone room-code handoff |
| Student driving | Deferred (visual-only join) | Let students drive | Keeps scope tight; matches current visual-only design (confirm in Open Questions) |
| Host token scope | create-scoped, surveyId-bound, 5-min TTL, stateless (reusable within TTL); reconnect via `sessionId` | Single-use `jti` tracking; session-lifetime token | Lowest complexity, reuses existing sessionId-based reconnect; token never in student link so replay exposure is low (LOW-2 accepted for classroom trust) |

---

## Research Summary

**Market Context**
Classroom live-participation tools (Kahoot, Gimkit, Quizizz, Blooket) share the same pattern this PRD adopts: one authoritative host/presenter session behind login, plus a low-friction join code/link for the audience that carries no host authority. The consistent lesson is that host authority must be server-enforced (audience clients are untrusted, even in a "trusted classroom") and that audience friction (typing codes) should be minimized via direct links/QR. This validates both the server-token decision and the auto-join student link.

**Technical Context**
The heavy lifting already exists in-repo: server-enforced role relay and host-only `event_triggered` (`Server/server.js:462-521`; `NetworkSync.cs:172-183`), a working visual-only student join (`NetworkSync.cs:287-335`), a 2D spectator view (`web-app/client/src/pages/LiveRacePage.jsx`), the survey→game `survey_import` path (`Server/server.js:437-452`), an authenticated professor context with a shared inter-service secret (`web-app/src/routes/auth.js`; `INTERNAL_SECRET`/`API_URL` in `Deploy/docker-compose.yml`), and a jslib bridge exposing page/WS URLs to Unity (`Assets/Plugins/WebGL/WebSocketBridge.jslib:5-12`; `NetworkManager.cs:101-112`). The gap is entry/URL plumbing + a token check + auto-inject wiring — not new subsystems. Feasibility HIGH.

---

*Generated: 2026-07-28*
*Status: DRAFT - needs validation*
