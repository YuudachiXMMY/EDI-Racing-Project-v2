# Implementation Report: Host-Screen "Join Game" QR Code

## Summary
Implemented an in-Unity (WebGL) join QR on the professor host screen, beside the existing student
link. QR encodes the same web-canonical join-landing URL; two buttons Show/Hide it and cycle its
size Small↔Large. Added a WebGL-safe pure-C# QR encoder (QRCoder core, vendored) rendered to a
`Texture2D`, extracted pure `QrPanelState` logic, and fixed the stale `/survey/` join URL.

## Assessment vs Reality

| Metric | Predicted (Plan) | Actual |
|---|---|---|
| Complexity | Medium | Medium-High (QRCoder vendoring was deeper than "3 files") |
| Confidence | 8/10 | Code complete; Unity compile/tests unverified (no Unity/compiler in env) |
| Files Changed | ~12 | 16 (13 vendored/new + 3 edited) |

## Tasks Completed

| # | Task | Status | Notes |
|---|---|---|---|
| 1 | Fix join URL in StudentLinkBuilder | ✅ Complete | `{origin}/#/join/{CODE}`, upper-cased (InvariantCulture) |
| 2 | Update StudentLinkBuilder tests | ✅ Complete | 4 tests; Normal case now proves uppercasing |
| 3 | Vendor QRCoder core | ✅ Complete | Deviated — see below (v1.4.3, larger closure, PayloadGenerator trimmed) |
| 4 | QRCoder asmdef + Runtime ref | ✅ Complete | `noEngineReferences:true`, `autoReferenced:false`; Runtime refs "QRCoder" |
| 5 | QrPanelState pure logic | ✅ Complete | enum QrSize + PixelSize/NextSize/labels |
| 6 | QrPanelStateTests | ✅ Complete | 8 tests |
| 7 | QrCodeRenderer (matrix→Texture2D) | ✅ Complete | FilterMode.Point, Y-flip, Color32 fast path |
| 8 | Wire QR + buttons into SetupScreen | ✅ Complete | fields, Start wiring, ShowStudentLink, handlers, OnReconnectFailed, OnDestroy |
| 9 | Manual scene wiring | ⏳ Deferred | Requires Unity Editor / UnitySkills API against this worktree — NOT done here |
| 10 | QRCoder in allowed-libraries log | ✅ Complete | technical-preferences.md |

## Validation Results

| Level | Status | Notes |
|---|---|---|
| Static Analysis | ✅ Pass | All 13 new/vendored .cs brace-balanced; 0 System.Drawing, 0 PayloadGenerator, 0 file-scoped namespaces in vendored dir; SetupScreen symbol refs resolve; Runtime asmdef refs QRCoder |
| Unit Tests | ⚠️ Not run | No C# compiler / Unity Test Runner in this environment. 12 EditMode tests written (4 StudentLinkBuilder + 8 QrPanelState) |
| Build | ⚠️ Not run | Running UnitySkills API (localhost:8090) targets the MAIN checkout, not this worktree — cannot compile the worktree headlessly |
| Integration | N/A | — |
| Edge Cases | ✅ Reasoned | Empty origin → "" → UI hidden; texture freed on regen + OnDestroy; reconnect re-renders |

## Files Changed

| File | Action | Notes |
|---|---|---|
| `Assets/Scripts/UI/StudentLinkBuilder.cs` | UPDATED | URL fix + uppercasing |
| `Assets/Tests/EditMode/StudentLinkBuilderTests.cs` | UPDATED | expected URLs |
| `Assets/Scripts/UI/QrPanelState.cs` | CREATED | pure size/visibility logic |
| `Assets/Tests/EditMode/QrPanelStateTests.cs` | CREATED | 8 tests |
| `Assets/Scripts/UI/QrCodeRenderer.cs` | CREATED | matrix → Texture2D |
| `Assets/Scripts/UI/SetupScreen.cs` | UPDATED | QR fields, wiring, handlers, teardown |
| `Assets/Scripts/EDIRacing.Runtime.asmdef` | UPDATED | + "QRCoder" reference |
| `Assets/ThirdParty/QRCoder/QRCodeGenerator.cs` | CREATED (vendored) | v1.4.3, Payload overloads removed |
| `Assets/ThirdParty/QRCoder/QRCodeData.cs` | CREATED (vendored) | verbatim v1.4.3 |
| `Assets/ThirdParty/QRCoder/AbstractQRCode.cs` | CREATED (vendored) | verbatim |
| `Assets/ThirdParty/QRCoder/Framework4.0Methods/Stream4Methods.cs` | CREATED (vendored) | polyfill (used by QRCodeData) |
| `Assets/ThirdParty/QRCoder/Framework4.0Methods/String4Methods.cs` | CREATED (vendored) | polyfill (self-contained) |
| `Assets/ThirdParty/QRCoder/Exceptions/DataTooLongException.cs` | CREATED (vendored) | thrown by generator |
| `Assets/ThirdParty/QRCoder/Extensions/StringValueAttribute.cs` | CREATED (vendored) | self-contained |
| `Assets/ThirdParty/QRCoder/LICENSE.txt` | CREATED | MIT |
| `Assets/ThirdParty/QRCoder/QRCoder.asmdef` | CREATED | isolates vendored assembly |
| `.claude/docs/technical-preferences.md` | UPDATED | QRCoder approval entry |

## Deviations from Plan

1. **QRCoder vendoring scope (Task 3)** — WHAT: plan said "vendor 3 files from master"; actual is
   ~8 files from tag **v1.4.3**. WHY: (a) master uses C# 10 file-scoped namespaces which Unity's
   C# 9 compiler rejects; v1.4.3 is block-scoped. (b) The real compile closure needs the
   Framework4.0Methods polyfill (`Stream4Methods`, used unconditionally by `QRCodeData`),
   `Exceptions/DataTooLongException`, and `Extensions/StringValueAttribute`. (c) `PayloadGenerator`
   (2600 lines) was dropped entirely by removing the 4 `CreateQrCode/GenerateQrCode(Payload…)`
   overloads from the vendored `QRCodeGenerator.cs`, because it uses SDK-style `*_OR_GREATER`
   conditional-compilation symbols that Unity does not define (would activate old-framework polyfill
   branches). Only the `CreateQrCode(string, ECCLevel)` path I use is kept.

2. **Task numbering** — the plan's Task 10 (allowed-libraries) done; Task 9 (scene `.unity`/`.prefab`
   wiring) deferred to a Unity-Editor step.

## Issues Encountered
- No C# compiler (dotnet/mono/csc) and no Unity access to the worktree in this environment, so
  compile + EditMode tests could not be executed here. Mitigated with exhaustive static analysis
  and dependency-closure verification of the vendored subset.
- Repeated GateGuard fact-forcing prompts on each edit — complied per-file rather than disabling.

## Tests Written

| Test File | Tests | Coverage |
|---|---|---|
| `Assets/Tests/EditMode/StudentLinkBuilderTests.cs` | 4 | URL composition, uppercasing, trailing slash, empty inputs |
| `Assets/Tests/EditMode/QrPanelStateTests.cs` | 8 | PixelSize, NextSize cycle, visibility + size labels |

## Next Steps
- [ ] **Open this worktree in Unity** (or point UnitySkills API at it) → let Unity generate `.meta`
      files, compile, and run the EditMode suite. Fix any compile error surfaced by the real compiler.
- [ ] **Scene wiring (Task 9)**: add a RawImage + two Buttons(+Text) under the host Canvas and
      assign the 5 new SerializeFields; commit with the `.unity`/`.prefab` (see memory
      `scene-wiring-lags-merged-scripts`).
- [ ] **Play-mode verify**: host a room, scan the QR with a phone → lands on JoinLandingPage for the
      room; exercise Show/Hide + Small/Large.
- [ ] Code review via `/code-review`; PR #43 already tracks this branch.
