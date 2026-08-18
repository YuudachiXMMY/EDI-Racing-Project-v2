# Implementation Report: Auto-Inject Survey Data (Phase 3)

## Summary
Implemented Phase 3 of role-bound-game-links: on a Dashboard "Host Game" launch, the game
auto-injects the selected survey's responses the instant the room is created — no manual
Send-to-Game modal, no typed room code. Chose **Design A** (client-triggered reuse): once
`NetworkManager.OnRoomCreated` fires, `HostLaunchBootstrap` calls a new jslib bridge
(`WebSocketBridge.HostAutoInject`) that POSTs to the **existing** authenticated
`POST /api/surveys/:id/send-to-game` endpoint with the fresh room code and the professor's
Bearer token from localStorage. **No `Server/server.js` or web-app route changes** — the
`survey_import` relay and Unity's inbound handling are reused as-is.

## Assessment vs Reality

| Metric | Predicted (Plan) | Actual |
|---|---|---|
| Complexity | Medium | Medium — held |
| Confidence | 8/10 | Held — no design surprises |
| Files Changed | 5 (2 new / 3 modified) | 7 tracked (4 new incl. 2 `.meta` / 3 modified) |

## Tasks Completed

| # | Task | Status | Notes |
|---|---|---|---|
| 1 | `HostAutoInjectDecision` pure helper | ✅ Complete | `role=="host" && !IsNullOrWhiteSpace(surveyId)` |
| 2 | jslib `WebSocketBridge_HostAutoInject` | ✅ Complete | fire-and-forget fetch; JS syntax verified |
| 3 | `HostLaunchBootstrap` one-shot wiring | ✅ Complete | subscribes before `CreateRoom`; captures surveyId pre-`ClearUrlHash` |
| 4 | C# `HostAutoInject` bridge wrapper | ✅ Complete | `#if UNITY_WEBGL` guard; editor no-op logs |
| 5 | `HostAutoInjectDecisionTests` (6 cases) | ✅ Written | EditMode run deferred (see Validation) |

## Validation Results

| Level | Status | Notes |
|---|---|---|
| Static — C# API contracts | ✅ Pass | Verified `OnRoomCreated : event Action<string>`, `CreateRoom(string=null)`, `RaceUI.SetRoleFromNetwork(bool)`, wrapper signature — all match call sites |
| Static — jslib JS syntax | ✅ Pass | `node --check` clean (whole file + isolated inject block) |
| Static — asmdef wiring | ✅ Pass | `HostAutoInjectDecision` in `EDIRacing.Runtime`; `Tests.EditMode` references it |
| Unity Compile | ⏸ Deferred | Live UnitySkills instance targets the **main checkout**, not this worktree; run `asset_refresh` when the branch is checked out there |
| Unity EditMode tests | ⏸ Deferred | Run `HostAutoInjectDecisionTests` via Editor Test Runner on branch checkout |
| Runtime / Integration | ⏸ Deferred | Folds into Phase 7 adversarial QA (full host→data flow; student-link 401) |
| web-app tests | N/A | Zero web-app source files changed — reuses existing `send-to-game` endpoint |

## Files Changed

| File | Action | Lines |
|---|---|---|
| `Assets/Scripts/UI/HostAutoInjectDecision.cs` | CREATE | +16 |
| `Assets/Scripts/UI/HostAutoInjectDecision.cs.meta` | CREATE | +2 |
| `Assets/Tests/EditMode/HostAutoInjectDecisionTests.cs` | CREATE | +58 |
| `Assets/Tests/EditMode/HostAutoInjectDecisionTests.cs.meta` | CREATE | +2 |
| `Assets/Scripts/UI/HostLaunchBootstrap.cs` | UPDATE | +18 |
| `Assets/Plugins/WebGL/WebSocketBridge.jslib` | UPDATE | +21 |
| `Assets/Plugins/WebGL/WebSocketBridge.cs` | UPDATE | +15 |

## Deviations from Plan

1. **Worktree was branched off `origin/main`, which lacks Phase 2.** The `EnterWorktree`
   default (`fresh` → `origin/main`) branched before the Phase 2 `HostLaunchBootstrap` /
   WebSocketBridge-bridge work (that lives unmerged on `feat/professor-host-launch`). Rebased the
   branch **onto `feat/professor-host-launch`** so the Phase 2 files this plan builds on are
   present. Consequence: the PR must target `feat/professor-host-launch`, not `main`.
2. **Test naming uses the codebase's PascalCase NUnit convention** (`Method_Scenario_Expected`,
   mirroring `HostLaunchParamsTests`) rather than the `test_[system]_[scenario]_[expected]` in
   `.claude/rules/test-standards.md` (that rule's examples are GDScript). Consistency with the
   actual C# test suite was chosen.
3. **Unity compile/EditMode validation deferred**, not run inline — the live Unity instance is
   bound to the main checkout, and driving it would write into the shared checkout the user's
   session is on, breaking worktree isolation. Same constraint Phase 2 documented.

## Issues Encountered
- **GateGuard fact-force gate** fired on each first file touch — answered inline each time;
  no impact on output.
- **Rebase conflict** in the PRD phase table (Phase 2 row differed between main and the feat
  branch) — resolved by keeping the feat branch's richer Phase 2 row and applying the Phase 3 update.

## Tests Written

| Test File | Tests | Coverage |
|---|---|---|
| `Assets/Tests/EditMode/HostAutoInjectDecisionTests.cs` | 6 | `ShouldAutoInject` — host+survey, empty/null/whitespace survey, student role, empty role |

## Next Steps
- [ ] Run Unity `asset_refresh` (zero errors) + EditMode `HostAutoInjectDecisionTests` on branch checkout
- [ ] `/code-review` the changeset
- [ ] Phase 7 runtime QA: host launch loads survey data with 0 codes/0 modals; student link → 401 on `send-to-game`
- [ ] Merge order: land Phase 2 (`feat/professor-host-launch`) → this Phase 3 branch → main
