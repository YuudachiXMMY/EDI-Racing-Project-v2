# Plan: Car Name Labels Always Face the Active Camera

## Summary
World-space car name labels must always face whichever camera is currently rendering the
race (the "player camera"), from **any** angle — including overhead / high fixed cameras.
Today the billboard flattens the look direction on the Y axis and caches `Camera.main` once
at spawn, so labels turn edge-on from elevated cameras and never re-target when the active
camera changes (Free ↔ Spectator ↔ Fixed F1–F9 ↔ AutoCam). Fix the billboard math in
`CarLabel` to face the live active camera every visible frame, and unit-test the pure facing
math.

## User Story
As a **viewer of the race (professor on the classroom screen or a student in the browser)**,
I want **each car's name label to squarely face the camera I'm currently watching through**,
so that **I can always read every car's name regardless of which camera mode is active or
what angle it looks from**.

## Problem → Solution
- **Current:** `CarLabel.LateUpdate` billboards with `lookDir.y = 0f` (Y-locked) toward a
  `Camera.main` transform cached at `Initialize`, refreshed only when it becomes `null`, and
  only every 4th frame. → From an elevated/overhead camera the label turns nearly edge-on
  (unreadable); after a camera-mode switch the label may keep facing the previous camera;
  during AutoCam cuts orientation lags up to 3 frames.
- **Desired:** Every visible frame, each label faces the **currently active camera** using a
  full 3D look rotation (no Y flattening), re-resolving the active camera whenever the cached
  one is gone or disabled. Facing math extracted to a pure, unit-tested static method.

## Metadata
- **Complexity**: Small
- **Source PRD**: N/A (free-form feature request)
- **PRD Phase**: N/A
- **Estimated Files**: 2 (1 runtime edit, 1 new test file)

---

## UX Design

### Before
```
Overhead / high fixed camera looking down at the track:

        [ car ]          ← label turned EDGE-ON (Y-locked billboard),
          |                reads as a thin vertical sliver or nothing
          '  (unreadable)

After switching Free → Fixed F3:
   label still oriented toward the OLD camera for a beat / until cache clears
```

### After
```
Any camera, any angle (chase / free / overhead fixed / auto-cut):

       ┌──────────┐
       │  BAMA12  │      ← label squarely faces the active camera,
       └──────────┘        upright and readable, updates the instant
          [ car ]          the active camera or its angle changes
```

### Interaction Changes
| Touchpoint | Before | After | Notes |
|---|---|---|---|
| Overhead/high fixed camera (e.g. AutoAllCams broadcast, F-cams) | Label edge-on, unreadable | Label faces camera, readable | Y-lock removal is the key fix |
| Camera mode switch (Free/Spectator/Fixed/AutoCam) | Faces previously cached camera until it goes null | Re-resolves active camera, faces it | Cache refresh on `!isActiveAndEnabled` |
| Fast AutoCam cut | Up to 3 frames of stale orientation | Correct orientation same frame | Per-visible-frame billboard |
| Chase / near-horizontal camera (already worked) | Faces camera (Y-locked) | Faces camera (full 3D) | No visible regression |

---

## Mandatory Reading

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 (critical) | `Assets/Scripts/UI/CarLabel.cs` | 1-56 | The only runtime file changed — current billboard + culling + camera caching |
| P0 (critical) | `Assets/Tests/EditMode/CarLabelSpawnerToggleTests.cs` | all | Exact EditMode test pattern to mirror (fixture/naming/teardown) |
| P1 (important) | `Assets/Scripts/UI/CarLabelSpawner.cs` | 82-140 | How each label is created and `Initialize(car.transform)` is called (spawn context) |
| P1 (important) | `Assets/Scripts/Camera/CameraManager.cs` | 133-176 | Camera-mode model: one persistent `Camera.main`, moved/enabled by controller components → confirms `Camera.main` is the active player camera |
| P2 (reference) | `Assets/Scripts/Camera/SpectatorCamera.cs` | 97-139 | Confirms Spectator/AutoCam drive the same camera transform via `LateUpdate` (why single-camera assumption holds) |
| P2 (reference) | `Assets/Tests/EditMode/Tests.asmdef` | all | Test asmdef references `EDIRacing.Runtime`; new test file needs no asmdef change |

## External Documentation
| Topic | Source | Key Takeaway |
|---|---|---|
| World-space UI billboard | Unity manual — Canvas `RenderMode.WorldSpace` | A world-space Canvas renders its readable face toward its local **+Z** (`transform.forward`). To be readable, `transform.forward` must point **toward** the camera → `LookRotation(camPos - labelPos)`. |
| `Quaternion.LookRotation(forward, upwards)` | Unity ScriptReference | Second arg orients "up"; passing `cam.up` keeps text upright under camera pitch/roll and stays valid when the camera looks straight down (overhead), where a Y-locked look direction degenerates. |

> No third-party libraries involved — established internal Unity patterns only.

---

## Patterns to Mirror

### NAMING_CONVENTION
```csharp
// SOURCE: Assets/Scripts/UI/CarLabel.cs:10-20
[Tooltip("Vertical offset above the car pivot")]
public float HeightOffset = 4f;        // PascalCase public serialized field
private Transform target;              // camelCase private field
```

### DOC_COMMENT (public API)
```csharp
// SOURCE: Assets/Scripts/UI/CarLabel.cs:22
/// <summary>...</summary>
public void Initialize(Transform carTransform) { ... }
```

### BILLBOARD_REFERENCE (existing, to be corrected)
```csharp
// SOURCE: Assets/Scripts/UI/CarLabel.cs:49-52
Vector3 lookDir = cam.position - transform.position;
lookDir.y = 0f;                                   // <-- DEFECT: Y-lock, fails overhead
if (lookDir.sqrMagnitude > 0.001f)
    transform.rotation = Quaternion.LookRotation(lookDir);
```

### DISTANCE_CULLING (keep unchanged)
```csharp
// SOURCE: Assets/Scripts/UI/CarLabel.cs:39-43
float sqrDist = (cam.position - transform.position).sqrMagnitude;
bool visible = sqrDist < MaxVisibleDistance * MaxVisibleDistance;
if (canvas != null && canvas.enabled != visible)
    canvas.enabled = visible;
```

### TEST_STRUCTURE
```csharp
// SOURCE: Assets/Tests/EditMode/CarLabelSpawnerToggleTests.cs:8-30
[TestFixture]
public class CarLabelSpawnerToggleTests
{
    private CarLabelSpawner spawner;

    [SetUp]  public void SetUp()  { /* new GameObject + AddComponent */ }
    [TearDown] public void TearDown() { if (spawner != null) Object.DestroyImmediate(spawner.gameObject); }

    [Test]
    public void LabelsVisible_DefaultsToTrue() { Assert.IsTrue(spawner.LabelsVisible); }
}
```
Test method naming observed: `Method_Condition_ExpectedResult`.

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `Assets/Scripts/UI/CarLabel.cs` | UPDATE | Fix billboard to full-3D facing of the live active camera every visible frame; extract pure facing math |
| `Assets/Tests/EditMode/CarLabelBillboardTests.cs` | CREATE | EditMode unit tests for the pure `ComputeFacingRotation` math (BLOCKING logic gate) |

## NOT Building
- **No new `CameraManager.ActiveCamera` API / dependency injection.** The scene uses one
  persistent `Camera.main` (moved/enabled by controller components), so `Camera.main` already
  *is* the active player camera. Injecting a camera reference is deferred (see Risks) —
  keep scope to `CarLabel`.
- **No change to distance culling, height offset, spawn logic, toggle/hotkey, or label text**
  (those are covered by prior PRs #14/#15/#16).
- **No screen-space / UI-Toolkit rewrite** of labels — stays world-space UGUI Canvas.
- **No per-frame `Camera.main` scan removal via a global singleton** — re-resolve only when the
  cached camera is null or disabled.

---

## Step-by-Step Tasks

### Task 1: Extract a pure, testable facing function in `CarLabel`
- **ACTION**: Add a `public static Quaternion ComputeFacingRotation(Vector3 labelPos, Vector3 camPos, Vector3 camUp)` to `CarLabel`.
- **IMPLEMENT**:
  ```csharp
  /// <summary>
  /// Rotation that makes a world-space label's readable face (+Z) point at the camera from
  /// any angle. Pure and deterministic so the billboard math is unit-testable without a live
  /// camera. Falls back to identity when the label sits on top of the camera (degenerate dir).
  /// </summary>
  public static Quaternion ComputeFacingRotation(Vector3 labelPos, Vector3 camPos, Vector3 camUp)
  {
      Vector3 lookDir = camPos - labelPos;               // +Z toward camera → text readable
      if (lookDir.sqrMagnitude < 0.0001f) return Quaternion.identity;
      return Quaternion.LookRotation(lookDir, camUp);    // full 3D, no Y flattening
  }
  ```
- **MIRROR**: DOC_COMMENT + NAMING_CONVENTION patterns above.
- **IMPORTS**: none new (`UnityEngine` already imported).
- **GOTCHA**: Do NOT zero `lookDir.y` — that is the exact defect. Passing `camUp` (not `Vector3.up`) keeps text upright and gives a valid basis when the camera looks straight down, where `Vector3.up` would be parallel to `lookDir` and `LookRotation` would warn/degenerate.
- **VALIDATE**: `ComputeFacingRotation(Vector3.zero, Vector3.up*5, Vector3.forward) * Vector3.forward` ≈ `Vector3.up` (label +Z points at an overhead camera).

### Task 2: Rewrite `LateUpdate` to face the active camera every visible frame
- **ACTION**: Replace the Y-locked, 4-frame-throttled billboard block with a per-visible-frame call to `ComputeFacingRotation`, keeping distance culling intact.
- **IMPLEMENT**:
  ```csharp
  private void LateUpdate()
  {
      if (target == null) return;

      transform.position = target.position + Vector3.up * HeightOffset;

      Transform activeCam = ResolveActiveCamera();
      if (activeCam == null) return;

      // Distance culling — disable Canvas to eliminate draw calls when far away.
      float sqrDist = (activeCam.position - transform.position).sqrMagnitude;
      bool visible = sqrDist < MaxVisibleDistance * MaxVisibleDistance;
      if (canvas != null && canvas.enabled != visible)
          canvas.enabled = visible;

      // Face the active camera from any angle, every visible frame, so a mode switch or a
      // fast AutoCam cut never leaves a label edge-on or aimed at the previous camera.
      if (visible)
          transform.rotation = ComputeFacingRotation(transform.position, activeCam.position, activeCam.up);
  }
  ```
- **MIRROR**: DISTANCE_CULLING pattern (unchanged semantics).
- **IMPORTS**: none new.
- **GOTCHA**: Remove the now-unused `frameCounter` and `staggerOffset` fields (and the `staggerOffset = GetInstanceID() % 4;` line in `Initialize`). Leaving them causes an unused-field warning; the codebase keeps warnings clean. Do not remove `cam`/`canvas`/`target` fields.
- **VALIDATE**: EditMode + a compile pass; play-mode smoke (Task 5).

### Task 3: Make the active-camera reference self-heal
- **ACTION**: Add `private Transform ResolveActiveCamera()` that reuses the cached camera while it is active-and-enabled, otherwise re-resolves `Camera.main`.
- **IMPLEMENT**:
  ```csharp
  // Reuse the cached camera while it is the live active one; re-resolve when it has been
  // disabled or destroyed (camera-mode switch, or a future second camera taking the tag).
  private Transform ResolveActiveCamera()
  {
      if (cam != null && cam.gameObject.activeInHierarchy)
      {
          var c = cam.GetComponent<Camera>();
          if (c == null || c.isActiveAndEnabled) return cam;
      }
      cam = Camera.main != null ? Camera.main.transform : null;
      return cam;
  }
  ```
- **MIRROR**: existing lazy-resolve intent at `CarLabel.cs:36`, hardened.
- **IMPORTS**: none new.
- **GOTCHA**: In this scene one persistent `Camera.main` is moved by controller components, so `Camera.main` is stable and this mostly guards the destroyed/disabled edge; keep it anyway so a future student/professor split (two tagged cameras) still resolves the enabled one. `Camera.main` is internally cached by Unity — calling it on re-resolve only is cheap.
- **VALIDATE**: Manual — switch camera modes in play mode (Task 5); labels keep facing the live camera.

### Task 4: Unit-test the pure facing math (BLOCKING logic gate)
- **ACTION**: Create `Assets/Tests/EditMode/CarLabelBillboardTests.cs`.
- **IMPLEMENT** (mirror TEST_STRUCTURE; pure math, no GameObject needed):
  ```csharp
  using NUnit.Framework;
  using UnityEngine;

  /// <summary>
  /// Pins <see cref="CarLabel.ComputeFacingRotation"/>: a world-space label's +Z (readable
  /// face) must point at the camera from any angle, including straight overhead where the old
  /// Y-locked billboard failed. Deterministic — no live camera or play mode.
  /// </summary>
  [TestFixture]
  public class CarLabelBillboardTests
  {
      private const float Tol = 0.01f;

      [Test]
      public void ComputeFacingRotation_HorizontalCamera_FacesCamera()
      {
          var rot = CarLabel.ComputeFacingRotation(Vector3.zero, new Vector3(0f, 0f, 10f), Vector3.up);
          Assert.Less(Vector3.Angle(rot * Vector3.forward, Vector3.forward), 1f);
      }

      [Test]
      public void ComputeFacingRotation_OverheadCamera_LabelForwardPointsUp()
      {
          // Camera straight above — the exact case the Y-locked billboard turned edge-on.
          var rot = CarLabel.ComputeFacingRotation(Vector3.zero, new Vector3(0f, 10f, 0f), Vector3.forward);
          Assert.Less(Vector3.Angle(rot * Vector3.forward, Vector3.up), 1f);
      }

      [Test]
      public void ComputeFacingRotation_DegenerateSamePosition_ReturnsIdentity()
      {
          var rot = CarLabel.ComputeFacingRotation(Vector3.one, Vector3.one, Vector3.up);
          Assert.That(Quaternion.Angle(rot, Quaternion.identity), Is.LessThan(Tol));
      }

      [Test]
      public void ComputeFacingRotation_FacePointsFromLabelTowardCamera()
      {
          Vector3 labelPos = new Vector3(3f, 1f, -2f);
          Vector3 camPos = new Vector3(-4f, 6f, 5f);
          var rot = CarLabel.ComputeFacingRotation(labelPos, camPos, Vector3.up);
          Vector3 expected = (camPos - labelPos).normalized;
          Assert.Less(Vector3.Angle(rot * Vector3.forward, expected), 1f);
      }
  }
  ```
- **MIRROR**: `CarLabelSpawnerToggleTests` fixture/naming; no asmdef edit (Tests.asmdef already references `EDIRacing.Runtime`).
- **IMPORTS**: `NUnit.Framework`, `UnityEngine`.
- **GOTCHA**: Determinism — assert on angles/tolerance, never exact float equality on quaternion components.
- **VALIDATE**: Unity Test Runner (EditMode) all green.

### Task 5: Play-mode verification (UnitySkills)
- **ACTION**: Verify live that labels face each camera mode. **Editor operates on the root
  checkout**, so this runs after the change is synced to root (or the two-line edit applied
  there); prefer `camera_screenshot` (explicit `Camera.Render`) because game-view frames freeze
  while the editor is unfocused.
- **IMPLEMENT**: Enter play mode, start a race, then for each of Free / a Fixed F-cam / AutoAllCams (overhead broadcast): `camera_screenshot` and confirm names are readable and upright, especially from the overhead broadcast cut.
- **MIRROR**: memory `[[unity-playmode-verification]]` (runInBackground pause, `camera_screenshot` over `scene_screenshot`).
- **GOTCHA**: Frozen-frame artifact — unfocused editor doesn't advance frames, so `LateUpdate` (hence billboarding) won't run under `scene_screenshot`; use `camera_screenshot`. Note this limitation in the PR if a full moving-race pass can't complete.
- **VALIDATE**: Screenshot per mode shows readable, camera-facing labels.

---

## Testing Strategy

### Unit Tests
| Test | Input | Expected Output | Edge Case? |
|---|---|---|---|
| `ComputeFacingRotation_HorizontalCamera_FacesCamera` | label (0,0,0), cam (0,0,10) | label +Z ≈ +Z (toward cam) | No |
| `ComputeFacingRotation_OverheadCamera_LabelForwardPointsUp` | label (0,0,0), cam (0,10,0), up=fwd | label +Z ≈ +up (faces down-looking cam) | **Yes** (the fixed defect) |
| `ComputeFacingRotation_DegenerateSamePosition_ReturnsIdentity` | label == cam | `Quaternion.identity` | **Yes** |
| `ComputeFacingRotation_FacePointsFromLabelTowardCamera` | arbitrary offset label/cam | +Z ≈ normalized(cam−label) | No |

### Edge Cases Checklist
- [x] Camera directly overhead (Y-locked degenerate) → covered by overhead test
- [x] Label coincident with camera (zero look dir) → identity, no `LookRotation` warning
- [x] Camera below the label (negative Y) → general test covers arbitrary vertical offset
- [ ] Active camera destroyed/disabled at runtime → `ResolveActiveCamera` re-resolves (manual, Task 5)
- [ ] No `MainCamera`-tagged camera present → `ResolveActiveCamera` returns null, `LateUpdate` early-returns (no crash)

---

## Validation Commands

### Static Analysis / Compile
```
UnitySkills: script_get_compile_feedback   (or GET /skill/script_get_compile_feedback)
```
EXPECT: Zero compile errors/warnings (including no unused-field warnings after removing `frameCounter`/`staggerOffset`).

### Unit Tests (EditMode)
```
UnitySkills: test_run (EditMode filter "CarLabelBillboardTests") → poll test_get_result(jobId)
# CI equivalent: game-ci/unity-test-runner@v4 (EditMode)
```
EXPECT: 4/4 CarLabelBillboardTests pass; existing CarLabelSpawnerToggleTests still pass.

### Full Test Suite
```
UnitySkills: test_run (EditMode, no filter) → test_get_result(jobId)
```
EXPECT: No regressions across the EditMode suite.

### Browser / Play-mode Validation
```
UnitySkills: editor_enter_playmode → camera_screenshot (savePath Assets/Screenshots/label_face_<mode>.png) per camera mode
```
EXPECT: Names readable and upright from chase, fixed, and overhead broadcast cameras.

### Manual Validation
- [ ] Start a race; cycle Free → F1..F9 → AutoTopCars → AutoAllCams; every label stays squarely readable.
- [ ] From the overhead AutoAllCams broadcast cut, names are readable (not edge-on).
- [ ] Toggle labels off/on (N key / button) still works (no regression to PR #14/#16 behavior).

---

## Acceptance Criteria
- [ ] Labels face the currently active camera from any angle, including overhead/high fixed cameras.
- [ ] Orientation updates the same frame the active camera or its angle changes (no stale/lagged facing).
- [ ] `ComputeFacingRotation` is pure, documented, and covered by passing EditMode tests.
- [ ] No compile errors/warnings; existing tests unaffected.
- [ ] Distance culling, toggle/hotkey, height offset, and label text behavior unchanged.

## Completion Checklist
- [ ] Code follows discovered patterns (PascalCase publics, doc comments, EditMode fixture style)
- [ ] Error/degenerate handling matches codebase style (early return, identity fallback)
- [ ] Tests follow `CarLabelSpawnerToggleTests` structure and `Method_Condition_Expected` naming
- [ ] No hardcoded magic values beyond documented tolerances/thresholds
- [ ] Unused `frameCounter`/`staggerOffset` removed
- [ ] No unnecessary scope additions (no CameraManager API, no UI rewrite)
- [ ] Self-contained — implementable from this plan without further searching

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Multiple `MainCamera`-tagged cameras (future student/professor split) make `Camera.main` ambiguous | Low | Med | `ResolveActiveCamera` returns whichever is active-and-enabled; if ambiguity becomes real, promote to injected `CameraManager.ActiveCamera` (noted in NOT Building) |
| Per-frame billboard cost with many labels | Very Low | Low | One quaternion per visible label per frame; ≤~30 labels ≪ 16.67 ms budget; culled labels skip it |
| Text reads mirrored if look direction sign is wrong | Low | Med | `LookRotation(camPos − labelPos)` points +Z **toward** camera (Canvas readable face) — pinned by `..._FacePointsFromLabelTowardCamera` test |
| Change won't show in the user's editor until synced to root checkout | High | Low | Editor runs on root; apply the edit to root or merge+pull (same caveat as PR #16) — called out in Task 5 |

## Notes
- The scene's camera model (verified in `CameraManager`/`SpectatorCamera`): a single persistent
  `Camera.main` whose transform is driven/enabled by `FreeCameraController` / `SpectatorCamera`
  controllers and repositioned for `Fixed` mode. Therefore `Camera.main` **is** the active
  player camera today; the fix targets the *facing math* and *cache freshness*, not camera
  discovery.
- This is orthogonal to the label-visibility fixes already shipped: PR #15 restored
  `RaceConfig.LabelVisibleDistance` (distance culling) and PR #16 disabled `Text` truncation
  (labels render at all). This plan makes the rendered labels *orient correctly*. Related
  memory: `[[scene-wiring-lags-merged-scripts]]`, `[[unity-playmode-verification]]`.
- **Confidence: 9/10** for single-pass implementation — the change is localized to one method
  plus a pure helper and its tests, with the exact defect and patterns captured above.
