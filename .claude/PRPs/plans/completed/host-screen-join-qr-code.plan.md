# Plan: Host-Screen "Join Game" QR Code (Unity WebGL, professor side)

## Summary
When a professor hosts a game, render a scannable "join game" QR code inside the Unity WebGL
canvas — right next to the existing student-link text/copy button — encoding the same public
join-landing URL students use. The professor gets two buttons: **Show/Hide** the QR, and a
**Size** button cycling Small ↔ Large. No QR library exists in the Unity project today, so this
adds a WebGL-safe pure-C# QR encoder (QRCoder core) rendered to a `Texture2D` → `RawImage`.

## User Story
As a **professor hosting a race**, I want a **QR code for the student join link shown on the
game screen, that I can hide or resize**, so that **students in the room can join instantly by
scanning the projector/screen instead of typing a URL, and I can get it out of the way when
racing**.

## Problem → Solution
Today the professor can only *copy* a text join link (`SetupScreen.StudentLinkText` +
`CopyLinkButton`) — useless for a room full of students looking at a projected screen → Show a
scannable QR of that same link on the host screen, with Show/Hide + Small/Large controls.

## Metadata
- **Complexity**: Medium
- **Source PRD**: N/A (free-form feature request)
- **PRD Phase**: N/A
- **Estimated Files**: ~10 (2 new runtime C#, 1 new test C#, 1 edit `SetupScreen.cs`, 1 edit
  `StudentLinkBuilder.cs`, 1 edit its test, vendored QRCoder core + asmdef, 1 edit
  `EDIRacing.Runtime.asmdef`, 1 edit `technical-preferences.md`)

---

## UX Design

### Before
```
┌─ Host screen (Unity WebGL, professor, GameState.Setup) ─┐
│  Room: A1B2C3                                           │
│  Students: 4                                            │
│  学生链接: https://host/survey/#/join/A1B2C3            │
│  [ Copy Link ]                                          │
└─────────────────────────────────────────────────────────┘
```

### After
```
┌─ Host screen (Unity WebGL, professor, GameState.Setup) ─┐
│  Room: A1B2C3                                           │
│  Students: 4                                            │
│  学生链接: https://host/#/join/A1B2C3                   │
│  [ Copy Link ]                                          │
│  ┌──────────────┐                                       │
│  │  ▓▓  ▓ ▓▓▓▓  │   ← RawImage, crisp (FilterMode.Point)│
│  │  ▓ QR ▓  ▓   │     size = Small(256) or Large(512)   │
│  │  ▓▓▓▓  ▓ ▓▓  │                                       │
│  └──────────────┘                                       │
│  [ 隐藏二维码 ]  [ 尺寸: 小 ]   ← two buttons           │
└─────────────────────────────────────────────────────────┘
```
When hidden: QR image + Size button are inactive; the toggle button reads `显示二维码`.

### Interaction Changes
| Touchpoint | Before | After | Notes |
|---|---|---|---|
| Room created (`OnRoomCreated`) | Link text + copy button appear | + QR renders and appears (default: visible, Small) | Same trigger point |
| Show/Hide button | — | Toggles QR image + Size button active; label flips 隐藏⇄显示 | Mirrors `RaceControlPanel.ToggleNames` |
| Size button | — | Cycles Small(256px) ↔ Large(512px); label flips 小⇄大 | Resizes `RectTransform.sizeDelta`; texture regenerated at target resolution for crispness |
| Encoded URL | `{origin}/survey/#/join/{code}` | `{origin}/#/join/{CODE}` (matches web `buildJoinLandingUrl`) | Fixes stale `/survey/` path |
| Editor / empty origin | Link UI hidden | QR UI also stays hidden | `BuildJoinLink` returns "" → skip |

---

## Mandatory Reading

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 | `Assets/Scripts/UI/SetupScreen.cs` | 16-56, 76-113, 219-253, 289-299 | The host-screen MonoBehaviour: field-declaration style, Start() wiring, `OnRoomCreated`/`ShowStudentLink`/`OnCopyStudentLink`, reconnect show/hide. This is where the QR wires in. |
| P0 | `Assets/Scripts/UI/StudentLinkBuilder.cs` | all (16) | Pure link builder — the URL to fix and to encode. |
| P0 | `Assets/Tests/EditMode/StudentLinkBuilderTests.cs` | all | Test template + the expected-URL assertions to update. |
| P1 | `Assets/Scripts/UI/RaceControlPanel.cs` | 27-28, 58-59, 94-101 | Button + label toggle pattern to mirror for Show/Hide and Size. |
| P1 | `Assets/Plugins/WebGL/WebSocketBridge.cs` | 243-250 | `GetPageOrigin()` — already used by `ShowStudentLink`; no change, just context. |
| P1 | `Assets/Scripts/Events/WeatherEffect.cs` | 255-275 | Only in-repo example of building a `Texture2D` from code (`new Texture2D(w,h,RGBA32,false)`, `SetPixel`/`SetPixels`, `Apply()`). The QR renderer mirrors this. |
| P1 | `web-app/client/src/gameLaunch.js` | 84-90 | `buildJoinLandingUrl` — the canonical URL shape the fix must match. |
| P2 | `Assets/Scripts/UI/JoinToast.cs` | 72-122 | Code-built-UI idiom (Canvas/CanvasScaler/font) if any element must self-bootstrap; and `sizeDelta` usage. |
| P2 | `web-app/client/src/components/HostRoomPanel.jsx` | 22, 51-58, 107 | Web QR (already shipped) — UX reference for size/margin (`{ width: 240, margin: 1 }`). |
| P2 | `Assets/Scripts/EDIRacing.Runtime.asmdef` | all | Add QRCoder asmdef reference here. |
| P2 | `Assets/Tests/EditMode/LeaderboardDisplayModeTests.cs` | all | Enum-cycle test pattern (for `QrSize` cycling), if you prefer enum over bool. |

## External Documentation

| Topic | Source | Key Takeaway |
|---|---|---|
| QRCoder (pure C#, MIT) | https://github.com/codebude/QRCoder | Core classes `QRCodeGenerator` + `QRCodeData` produce a module matrix (`QRCodeData.ModuleMatrix`, a `List<BitArray>`) with **no `System.Drawing` dependency**. Only the `QRCode`/`Bitmap`/`Base64QRCode` renderer classes use `System.Drawing` — DO NOT vendor those (they break WebGL/IL2CPP). Vendor only `QRCodeGenerator.cs`, `QRCodeData.cs`, `AbstractQRCode.cs`. |
| QRCode ECC level | QRCoder API | `QRCodeGenerator.CreateQrCode(payload, ECCLevel.Q)` — level Q (~25% recovery) is a good projector-scan default. |
| Unity Texture2D for pixel art | Unity 6.3 docs | `FilterMode.Point` + `wrapMode = Clamp` keeps QR modules crisp (no bilinear blur that breaks scanning). Regenerate the texture at the display resolution rather than upscaling a small texture. |

> RESEARCH NOTE: The only genuinely external piece is QRCoder. Everything else uses established
> internal patterns. QRCoder core is ~1200 lines of self-contained C#; vendor the three files
> above under `Assets/ThirdParty/QRCoder/` with their MIT `LICENSE.txt`.

---

## Patterns to Mirror

### NAMING_CONVENTION — public serialized fields, [Header] groups, (Optional) suffix, no namespace
```csharp
// SOURCE: Assets/Scripts/UI/SetupScreen.cs:25-31
[Header("Network (Optional)")]
public NetworkManager NetworkManager;
public Button HostButton;
public Text RoomCodeText;
public Text StudentCountText;
public Text StudentLinkText;
public Button CopyLinkButton;
private string currentStudentLink = "";
```

### BUTTON_TOGGLE_WITH_LABEL — the exact interaction to mirror for Show/Hide + Size
```csharp
// SOURCE: Assets/Scripts/UI/RaceControlPanel.cs:58-59, 94-101
if (ToggleNamesButton != null)
    ToggleNamesButton.onClick.AddListener(ToggleNames);
...
private void ToggleNames()
{
    if (CarLabelSpawner == null) return;
    CarLabelSpawner.ToggleLabels();
    bool on = CarLabelSpawner.LabelsVisible;
    if (ToggleNamesLabel != null) ToggleNamesLabel.text = on ? "Names: On" : "Names: Off";
    ShowStatus(on ? "Car names shown" : "Car names hidden");
}
```

### SHOW_HIDE — SetActive per element, wired + hidden in Start()
```csharp
// SOURCE: Assets/Scripts/UI/SetupScreen.cs:97-102
if (StudentLinkText != null) StudentLinkText.gameObject.SetActive(false);
if (CopyLinkButton != null)
{
    CopyLinkButton.gameObject.SetActive(false);
    CopyLinkButton.onClick.AddListener(OnCopyStudentLink);
}
```

### SHOW_ON_ROOM_CREATED — where the link becomes available; QR hooks in here
```csharp
// SOURCE: Assets/Scripts/UI/SetupScreen.cs:235-247
private void ShowStudentLink(string roomCode)
{
    currentStudentLink = StudentLinkBuilder.BuildJoinLink(WebSocketBridge.GetPageOrigin(), roomCode);
    if (string.IsNullOrEmpty(currentStudentLink)) return;
    if (StudentLinkText != null)
    {
        StudentLinkText.gameObject.SetActive(true);
        StudentLinkText.text = $"学生链接: {currentStudentLink}";
    }
    if (CopyLinkButton != null) CopyLinkButton.gameObject.SetActive(true);
}
```

### TEXTURE2D_FROM_CODE — the renderer mirrors this construction/Apply cycle
```csharp
// SOURCE: Assets/Scripts/Events/WeatherEffect.cs:263-274 (procedural texture idiom)
Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
// ... SetPixel / SetPixels over the grid ...
tex.Apply();
```

### PURE_TESTABLE_HELPER — extract UnityEngine-free logic into a static class
```csharp
// SOURCE: Assets/Scripts/UI/StudentLinkBuilder.cs:8-16
public static class StudentLinkBuilder
{
    public static string BuildJoinLink(string origin, string roomCode)
    {
        if (string.IsNullOrEmpty(origin) || string.IsNullOrEmpty(roomCode)) return "";
        string trimmed = origin.TrimEnd('/');
        return $"{trimmed}/survey/#/join/{roomCode}";   // ← the line to fix
    }
}
```

### TEST_STRUCTURE — NUnit, PascalCase Method_Scenario_Expected, Assert.AreEqual
```csharp
// SOURCE: Assets/Tests/EditMode/StudentLinkBuilderTests.cs:1-12
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
}
```

### CANONICAL_WEB_URL — the shape BuildJoinLink must match after the fix
```javascript
// SOURCE: web-app/client/src/gameLaunch.js:88-90
export function buildJoinLandingUrl(roomCode) {
  return `${window.location.origin}/#/join/${String(roomCode).toUpperCase()}`;
}
```

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `Assets/ThirdParty/QRCoder/QRCodeGenerator.cs` | CREATE (vendor) | Pure-C# QR encoder core (no System.Drawing). |
| `Assets/ThirdParty/QRCoder/QRCodeData.cs` | CREATE (vendor) | Module-matrix container (`ModuleMatrix`). |
| `Assets/ThirdParty/QRCoder/AbstractQRCode.cs` | CREATE (vendor) | Base class the above reference. |
| `Assets/ThirdParty/QRCoder/LICENSE.txt` | CREATE | MIT license text (attribution requirement). |
| `Assets/ThirdParty/QRCoder/QRCoder.asmdef` | CREATE | Isolate vendored code in its own assembly so `EDIRacing.Runtime` can reference it. |
| `Assets/Scripts/UI/QrCodeRenderer.cs` | CREATE | UnityEngine glue: matrix → `Texture2D` (FilterMode.Point). Thin, not unit-tested (needs Texture2D). |
| `Assets/Scripts/UI/QrPanelState.cs` | CREATE | Pure, UnityEngine-free size/visibility logic (enum + cycle + pixel-size map). EditMode-testable. |
| `Assets/Scripts/UI/StudentLinkBuilder.cs` | UPDATE | Fix URL to `{origin}/#/join/{CODE}` (drop `/survey/`, uppercase). |
| `Assets/Scripts/UI/SetupScreen.cs` | UPDATE | Add SerializeFields (RawImage + 2 buttons + labels), wire in Start(), render QR in ShowStudentLink, add handlers, hide on reconnect-fail. |
| `Assets/Scripts/EDIRacing.Runtime.asmdef` | UPDATE | Add `"QRCoder"` to `references`. |
| `Assets/Tests/EditMode/StudentLinkBuilderTests.cs` | UPDATE | Update expected URLs to the fixed shape. |
| `Assets/Tests/EditMode/QrPanelStateTests.cs` | CREATE | Cover size cycle + pixel-size map + visibility toggle. |
| `.claude/docs/technical-preferences.md` | UPDATE | Add QRCoder to "Allowed Libraries / Addons" with approval note. |

## NOT Building
- **No web-app changes.** The React host QR (`HostRoomPanel.jsx`) already exists and is out of scope; this is purely the in-Unity-canvas QR.
- **No new networking / server changes.** Room code + origin already reach Unity via existing events and `WebSocketBridge`.
- **No animated tween** for resize/fade. Direct `SetActive` + `sizeDelta`, matching the codebase (no tween helper exists).
- **No QR persistence, download, or "save image" affordance.** Scan-only.
- **No scene-file (.unity/.prefab) authoring in this plan's code tasks.** Wiring the new
  SerializeFields onto the host Canvas is a manual Unity-Editor step (see Task 9), preferably
  via the UnitySkills API per project convention.
- **No touch/gamepad handling.** Keyboard/mouse only (teacher-operated), per technical-preferences.
- **Not vendoring QRCoder's `System.Drawing`-based renderers** (`QRCode.cs`, `Base64QRCode.cs`, etc.) — they break WebGL/IL2CPP.

---

## Step-by-Step Tasks

### Task 1: Fix the canonical join URL in StudentLinkBuilder
- **ACTION**: Change `BuildJoinLink` to produce the web-canonical URL.
- **IMPLEMENT**:
  ```csharp
  public static string BuildJoinLink(string origin, string roomCode)
  {
      if (string.IsNullOrEmpty(origin) || string.IsNullOrEmpty(roomCode)) return "";
      string trimmed = origin.TrimEnd('/');
      return $"{trimmed}/#/join/{roomCode.ToUpper(System.Globalization.CultureInfo.InvariantCulture)}";
  }
  ```
  Also update the class `<summary>` doc: the route is now `"{origin}/#/join/{ROOMCODE}"` (survey
  app is the site root; the `/survey/` prefix was stale — see `web-app/client/src/App.jsx:29`
  route `/join/:roomCode`, and `buildJoinLandingUrl` at `gameLaunch.js:88-90`).
- **MIRROR**: PURE_TESTABLE_HELPER.
- **IMPORTS**: `System.Globalization` (fully-qualified inline as above, or add `using`).
- **GOTCHA**: There is NO `ToUpperCase()` in C#; use `ToUpper(...)`. `buildJoinLandingUrl`
  upper-cases the code and the server `web_join_room` also upper-cases (see `gameLaunch.js:78-82`).
  Match that exactly or a scanned lowercase code may mismatch the on-page "Room …" label.
- **VALIDATE**: Update + run `StudentLinkBuilderTests` (Task 2) — all green.

### Task 2: Update StudentLinkBuilder tests to the fixed URL
- **ACTION**: Rewrite the 4 assertions' expected strings; rename the "SurveyJoinRoute" test; the
  Normal case doubles as the uppercasing proof.
- **IMPLEMENT**:
  ```csharp
  [Test] public void BuildJoinLink_Normal_ComposesJoinLandingRoute()
  {
      Assert.AreEqual("https://host.example/#/join/A1B2C3",
          StudentLinkBuilder.BuildJoinLink("https://host.example", "a1b2c3"));  // lowercase input → uppercase output
  }
  [Test] public void BuildJoinLink_TrailingSlashOrigin_NoDoubleSlash()
  {
      Assert.AreEqual("https://host.example/#/join/R1",
          StudentLinkBuilder.BuildJoinLink("https://host.example/", "r1"));
  }
  [Test] public void BuildJoinLink_EmptyOrigin_ReturnsEmpty() { Assert.AreEqual("", StudentLinkBuilder.BuildJoinLink("", "R1")); }
  [Test] public void BuildJoinLink_EmptyRoom_ReturnsEmpty()   { Assert.AreEqual("", StudentLinkBuilder.BuildJoinLink("https://host.example", "")); }
  ```
- **MIRROR**: TEST_STRUCTURE.
- **IMPORTS**: `using NUnit.Framework;`
- **GOTCHA**: Keep determinism (no time/random).
- **VALIDATE**: EditMode run passes.

### Task 3: Vendor QRCoder core (WebGL-safe subset)
- **ACTION**: Copy `QRCodeGenerator.cs`, `QRCodeData.cs`, `AbstractQRCode.cs`, and `LICENSE.txt`
  from QRCoder (https://github.com/codebude/QRCoder, `QRCoder/` folder) into
  `Assets/ThirdParty/QRCoder/`. Do **not** copy any file that has `using System.Drawing;`.
- **IMPLEMENT**: After copying, grep the vendored folder for `System.Drawing` and
  `System.Windows` — expect ZERO hits. `QRCodeData.cs` may reference `System.IO.Compression`
  (fine — present in Unity). Remove any `[assembly: ...]` attributes if the compiler complains.
- **MIRROR**: N/A (third-party).
- **IMPORTS**: N/A.
- **GOTCHA**: QRCoder's namespace is `QRCoder`; keep it. No reflection-heavy paths in the core →
  WebGL/IL2CPP safe. If IL2CPP strips something at runtime, add a `link.xml` preserving `QRCoder`.
- **VALIDATE**: Unity compiles with no errors after Task 4's asmdef exists.

### Task 4: Add QRCoder asmdef and reference it from Runtime
- **ACTION**: Create `Assets/ThirdParty/QRCoder/QRCoder.asmdef`; add its name to Runtime refs.
- **IMPLEMENT**:
  ```json
  // Assets/ThirdParty/QRCoder/QRCoder.asmdef
  { "name": "QRCoder", "rootNamespace": "QRCoder", "references": [], "includePlatforms": [], "allowUnsafeCode": false, "autoReferenced": false }
  ```
  Then in `Assets/Scripts/EDIRacing.Runtime.asmdef`, add `"QRCoder"` to the `"references"` array
  (alongside `Unity.InputSystem`, `Unity.AI.Navigation`, `EDIRacing.WebGL`).
- **MIRROR**: existing asmdef structure (`EDIRacing.Runtime.asmdef`).
- **GOTCHA**: `autoReferenced:false` means ONLY assemblies that explicitly list `QRCoder` can use
  it — so the Runtime reference is mandatory or `QrCodeRenderer` won't compile.
- **VALIDATE**: Unity recompiles clean; `QrCodeRenderer` can `using QRCoder;`.

### Task 5: Create QrPanelState (pure size/visibility logic)
- **ACTION**: New `Assets/Scripts/UI/QrPanelState.cs` — UnityEngine-free.
- **IMPLEMENT**:
  ```csharp
  /// <summary>
  /// Pure size/visibility state for the host-screen join-QR panel. UnityEngine-free so the
  /// size-cycle and pixel-size mapping are EditMode-testable without a live scene. The
  /// MonoBehaviour (SetupScreen) holds the current values and applies the results to UGUI.
  /// </summary>
  public static class QrPanelState
  {
      public enum QrSize { Small, Large }

      // Pixel edge length rendered into the Texture2D AND used as the RawImage sizeDelta.
      // Rendering at the display size (not upscaling a small texture) keeps modules crisp.
      public static int PixelSize(QrSize size) => size == QrSize.Large ? 512 : 256;

      // Size button cycles Small <-> Large.
      public static QrSize NextSize(QrSize size) => size == QrSize.Small ? QrSize.Large : QrSize.Small;

      public static string SizeLabel(QrSize size) => size == QrSize.Large ? "尺寸: 大" : "尺寸: 小";
      public static string VisibilityLabel(bool visible) => visible ? "隐藏二维码" : "显示二维码";
  }
  ```
- **MIRROR**: PURE_TESTABLE_HELPER; enum idiom from `LeaderboardPanel.DisplayMode`.
- **IMPORTS**: none (no `UnityEngine` — that's the point).
- **GOTCHA**: Keep it in `EDIRacing.Runtime` (default for `Assets/Scripts/`) so the test asmdef
  (references `EDIRacing.Runtime`) can reach it.
- **VALIDATE**: Covered by Task 6 tests.

### Task 6: Create QrPanelStateTests
- **ACTION**: New `Assets/Tests/EditMode/QrPanelStateTests.cs`.
- **IMPLEMENT**:
  ```csharp
  using NUnit.Framework;

  [TestFixture]
  public class QrPanelStateTests
  {
      [Test] public void PixelSize_Small_Returns256() => Assert.AreEqual(256, QrPanelState.PixelSize(QrPanelState.QrSize.Small));
      [Test] public void PixelSize_Large_Returns512() => Assert.AreEqual(512, QrPanelState.PixelSize(QrPanelState.QrSize.Large));
      [Test] public void NextSize_Small_ReturnsLarge() => Assert.AreEqual(QrPanelState.QrSize.Large, QrPanelState.NextSize(QrPanelState.QrSize.Small));
      [Test] public void NextSize_Large_ReturnsSmall() => Assert.AreEqual(QrPanelState.QrSize.Small, QrPanelState.NextSize(QrPanelState.QrSize.Large));
      [Test] public void VisibilityLabel_Visible_SaysHide() => Assert.AreEqual("隐藏二维码", QrPanelState.VisibilityLabel(true));
      [Test] public void VisibilityLabel_Hidden_SaysShow() => Assert.AreEqual("显示二维码", QrPanelState.VisibilityLabel(false));
  }
  ```
- **MIRROR**: TEST_STRUCTURE; `LeaderboardDisplayModeTests` enum assertions.
- **IMPORTS**: `using NUnit.Framework;`
- **GOTCHA**: `Tests.EditMode` asmdef already references `EDIRacing.Runtime`; no new ref needed.
  Non-ASCII string literals in tests are fine (files are UTF-8).
- **VALIDATE**: EditMode run — 6 green.

### Task 7: Create QrCodeRenderer (matrix → Texture2D)
- **ACTION**: New `Assets/Scripts/UI/QrCodeRenderer.cs` — the only UnityEngine glue for QR pixels.
- **IMPLEMENT**:
  ```csharp
  using UnityEngine;
  using QRCoder;

  /// <summary>
  /// Renders a payload string into a crisp black-on-white QR Texture2D for display in a UGUI
  /// RawImage. WebGL-safe: uses only QRCoder's generator core (no System.Drawing) and Unity's
  /// Texture2D. FilterMode.Point keeps modules sharp when the RawImage is scaled.
  /// </summary>
  public static class QrCodeRenderer
  {
      // pixelSize = target texture edge length (e.g. QrPanelState.PixelSize(size)).
      public static Texture2D Render(string payload, int pixelSize)
      {
          if (string.IsNullOrEmpty(payload) || pixelSize <= 0) return null;

          using (var generator = new QRCodeGenerator())
          {
              QRCodeData data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
              var matrix = data.ModuleMatrix;        // List<BitArray>, includes the quiet-zone border
              int modules = matrix.Count;

              var tex = new Texture2D(pixelSize, pixelSize, TextureFormat.RGBA32, false)
              {
                  filterMode = FilterMode.Point,
                  wrapMode = TextureWrapMode.Clamp,
              };
              var pixels = new Color32[pixelSize * pixelSize];
              Color32 black = new Color32(0, 0, 0, 255);
              Color32 white = new Color32(255, 255, 255, 255);

              for (int y = 0; y < pixelSize; y++)
              {
                  // Texture2D origin is bottom-left; flip Y so the QR isn't mirrored vertically.
                  int my = modules - 1 - (y * modules / pixelSize);
                  for (int x = 0; x < pixelSize; x++)
                  {
                      int mx = x * modules / pixelSize;
                      bool dark = matrix[my][mx];
                      pixels[y * pixelSize + x] = dark ? black : white;
                  }
              }
              tex.SetPixels32(pixels);
              tex.Apply(false, false);
              return tex;
          }
      }
  }
  ```
- **MIRROR**: TEXTURE2D_FROM_CODE (`WeatherEffect.CreateSnowflakeTexture`).
- **IMPORTS**: `UnityEngine`, `QRCoder`.
- **GOTCHA**:
  - QRCoder's `ModuleMatrix` **already includes the 4-module quiet zone** — do not add another
    border. Verify by generating once and eyeballing the white margin.
  - Nearest mapping (`x*modules/pixelSize`) is acceptable at 256/512 for typical URL lengths
    (~30-40 modules). Rendering at display resolution (Task 8 regenerates on resize) avoids
    upscale blur.
  - IL2CPP: `Color32[]` + `SetPixels32` is the fast path; avoid per-pixel `SetPixel`.
  - The OLD texture is freed by SetupScreen (Task 8), not here, to avoid a WebGL memory leak.
- **VALIDATE**: Play-mode smoke (Task 9 manual) — QR scans to the join URL on a phone.

### Task 8: Wire the QR + two buttons into SetupScreen
- **ACTION**: Add fields, Start() wiring/hide, render in ShowStudentLink, handlers, reconnect
  cleanup, and texture lifecycle.
- **IMPLEMENT** (add near the `Network (Optional)` group, ~line 31):
  ```csharp
  [Header("Join QR (Optional)")]
  public RawImage StudentQrImage;          // displays the generated QR texture
  public Button ToggleQrButton;            // Show/Hide
  public Text ToggleQrLabel;               // label on ToggleQrButton
  public Button QrSizeButton;              // Small <-> Large
  public Text QrSizeLabel;                 // label on QrSizeButton
  private QrPanelState.QrSize qrSize = QrPanelState.QrSize.Small;
  private bool qrVisible = true;
  private Texture2D qrTexture;             // owned; destroyed on regen/teardown
  ```
  In `Start()` (after the CopyLinkButton block ~line 102) — hide + wire:
  ```csharp
  if (StudentQrImage != null) StudentQrImage.gameObject.SetActive(false);
  if (ToggleQrButton != null)
  {
      ToggleQrButton.gameObject.SetActive(false);
      ToggleQrButton.onClick.AddListener(OnToggleQr);
  }
  if (QrSizeButton != null)
  {
      QrSizeButton.gameObject.SetActive(false);
      QrSizeButton.onClick.AddListener(OnCycleQrSize);
  }
  ```
  Extend `ShowStudentLink` (after the CopyLinkButton line ~247):
  ```csharp
  RenderStudentQr();
  if (ToggleQrButton != null) ToggleQrButton.gameObject.SetActive(true);
  ApplyQrVisibility();   // sets image + size button active per qrVisible, updates labels
  ```
  New methods:
  ```csharp
  private void RenderStudentQr()
  {
      if (StudentQrImage == null || string.IsNullOrEmpty(currentStudentLink)) return;
      if (qrTexture != null) Destroy(qrTexture);         // free previous WebGL texture
      int px = QrPanelState.PixelSize(qrSize);
      qrTexture = QrCodeRenderer.Render(currentStudentLink, px);
      StudentQrImage.texture = qrTexture;
      StudentQrImage.rectTransform.sizeDelta = new Vector2(px, px);  // sizeDelta pattern: JoinToast.cs:95
  }

  private void OnToggleQr()
  {
      qrVisible = !qrVisible;
      ApplyQrVisibility();
  }

  private void ApplyQrVisibility()
  {
      if (StudentQrImage != null) StudentQrImage.gameObject.SetActive(qrVisible);
      if (QrSizeButton != null)   QrSizeButton.gameObject.SetActive(qrVisible);
      if (ToggleQrLabel != null)  ToggleQrLabel.text = QrPanelState.VisibilityLabel(qrVisible);
      if (QrSizeLabel != null)    QrSizeLabel.text = QrPanelState.SizeLabel(qrSize);
  }

  private void OnCycleQrSize()
  {
      qrSize = QrPanelState.NextSize(qrSize);
      RenderStudentQr();                                  // regen at new resolution for crispness
      if (QrSizeLabel != null) QrSizeLabel.text = QrPanelState.SizeLabel(qrSize);
  }
  ```
  In `OnReconnectFailed()` (near line 298-299 where StudentLinkText/CopyLinkButton hide), also
  hide the QR controls:
  ```csharp
  if (StudentQrImage != null) StudentQrImage.gameObject.SetActive(false);
  if (ToggleQrButton != null) ToggleQrButton.gameObject.SetActive(false);
  if (QrSizeButton != null)   QrSizeButton.gameObject.SetActive(false);
  ```
  Add teardown so the WebGL texture is freed (merge into an existing `OnDestroy`/`OnDisable` if
  present — check first):
  ```csharp
  private void OnDestroy()
  {
      if (qrTexture != null) Destroy(qrTexture);
  }
  ```
- **MIRROR**: NAMING_CONVENTION, SHOW_HIDE, BUTTON_TOGGLE_WITH_LABEL, SHOW_ON_ROOM_CREATED.
- **IMPORTS**: Confirm `using UnityEngine.UI;` is at the top of SetupScreen.cs (it uses
  `Button`/`Text`; `RawImage` is the same namespace). No new using needed.
- **GOTCHA**:
  - `ConfigureSingleButtonMenu()` (line 127-158) hides many buttons at runtime via `HideButton`.
    Do NOT add the QR buttons to that collapse — the QR controls are only shown from
    `ShowStudentLink` (post room-create). Confirm `ConfigureSingleButtonMenu` doesn't blanket-hide
    all panel children.
  - `OnNetworkReconnected` (line 289) re-calls `ShowStudentLink` → QR re-renders on reconnect; the
    `Destroy(qrTexture)` guard prevents a leak.
  - Defaults `qrVisible=true`, `qrSize=Small` → QR appears immediately at 256px on host.
  - Use `RawImage` (not `Image`) so no `Sprite` is needed; assign `.texture` directly.
- **VALIDATE**: Play-mode (Task 9 manual) — labels flip, image shows/hides, size toggles,
  reconnect re-renders.

### Task 9: Manual scene wiring (Unity Editor / UnitySkills API)
- **ACTION**: On the host-screen Canvas carrying `SetupScreen`, add a `RawImage` + two `Button`s
  (each with a child `Text`) beneath the existing student-link text, and drag them into the new
  `StudentQrImage` / `ToggleQrButton` / `ToggleQrLabel` / `QrSizeButton` / `QrSizeLabel` fields.
- **IMPLEMENT**: Prefer the **UnitySkills REST API** (`http://localhost:8090`, per
  `.claude/docs/technical-preferences.md`) to create GameObjects and set serialized references;
  fall back to manual Editor wiring only if unsupported. Match uGUI + `Arial`/`LegacyRuntime`
  font conventions (`JoinToast.cs:118-122`).
- **MIRROR**: existing SetupScreen elements' layout (RoomCodeText/StudentLinkText siblings).
- **GOTCHA**: Per memory `[[scene-wiring-lags-merged-scripts]]` — merged PRs often ship `.cs` but
  NOT the `.unity`/`.prefab` wiring. This wiring MUST be committed with the scene; otherwise the
  fields stay null and (by the `!= null` guards) the QR silently never appears. Verify serialized
  refs in the committed scene.
- **VALIDATE**: Enter Play mode as host; QR block renders. Fields non-null in Inspector.

### Task 10: Add QRCoder to the allowed-libraries log
- **ACTION**: Append to `.claude/docs/technical-preferences.md` → "Allowed Libraries / Addons":
  ```
  - **QRCoder** (Unity runtime, vendored core only) — pure-C# QR generation for the host-screen
    join QR. Only QRCodeGenerator.cs/QRCodeData.cs/AbstractQRCode.cs vendored (no System.Drawing;
    WebGL/IL2CPP-safe). MIT license. Approved <YYYY-MM-DD>.
  ```
- **MIRROR**: the existing `nodemailer` entry format.
- **VALIDATE**: Doc renders; entry present.

---

## Testing Strategy

### Unit Tests (EditMode, automated — BLOCKING per coding-standards Logic gate)
| Test | Input | Expected Output | Edge Case? |
|---|---|---|---|
| `BuildJoinLink_Normal_ComposesJoinLandingRoute` | `("https://host.example","a1b2c3")` | `"https://host.example/#/join/A1B2C3"` | uppercasing |
| `BuildJoinLink_TrailingSlashOrigin_NoDoubleSlash` | `("https://host.example/","r1")` | `"https://host.example/#/join/R1"` | trailing slash |
| `BuildJoinLink_EmptyOrigin_ReturnsEmpty` | `("","R1")` | `""` | empty (Editor) |
| `BuildJoinLink_EmptyRoom_ReturnsEmpty` | `("https://host.example","")` | `""` | empty room |
| `PixelSize_Small_Returns256` / `_Large_Returns512` | enum | 256 / 512 | boundary |
| `NextSize_Small_ReturnsLarge` / `_Large_ReturnsSmall` | enum | cycled enum | cycle wrap |
| `VisibilityLabel_Visible_SaysHide` / `_Hidden_SaysShow` | bool | 隐藏/显示 label | — |

### Edge Cases Checklist
- [x] Empty input → `BuildJoinLink` returns "" (Editor/no origin); `QrCodeRenderer.Render` returns null → guards keep UI hidden.
- [x] Maximum size input → long URLs increase module count; nearest-neighbor mapping still scans at 256/512.
- [ ] Invalid types → N/A (typed C#).
- [x] Concurrent access → N/A (single-threaded Unity main thread).
- [x] Network failure → reconnect re-renders QR; reconnect-fail hides it.
- [x] Permission denied → N/A.
- [x] Texture leak → `Destroy(qrTexture)` on regen + `OnDestroy`.

### What is NOT automated (per coding-standards "What NOT to Automate")
- Actual QR **scannability** and visual crispness → manual phone-scan in Play mode / WebGL build.
- Texture pixel output → visual, not asserted.

---

## Validation Commands

### Static Analysis / Compile
```bash
# Unity compiles on domain reload; no standalone C# type-check CLI in this project.
# Trigger a compile/refresh + surface errors via UnitySkills API (preferred per technical-preferences).
# See .claude/skills/unity-skills/SKILL.md for the exact endpoint.
```
EXPECT: Zero compile errors (esp. QRCoder core: no `System.Drawing`).

### Unit Tests (EditMode)
```bash
# Preferred: run the EditMode suite via UnitySkills API test runner.
# CI uses game-ci/unity-test-runner@v4 (EditMode). Locally, Unity Test Runner window → EditMode.
```
EXPECT: `StudentLinkBuilderTests` (updated) + `QrPanelStateTests` (new) pass; no regressions.

### Browser / WebGL Validation
```bash
# Build WebGL (BuildScript.cs), serve the gated /game/ page, host a room as professor.
# Or verify on the running deployment per memory [[ediracing-deploy-pinned-compose]].
```
EXPECT: QR appears on host after room-create; scanning a phone opens `{origin}/#/join/{CODE}`
(JoinLandingPage) for that room.

### Manual Validation
- [ ] Host a game as professor → QR renders (Small/256px, visible) beside the student link.
- [ ] Scan QR with a phone → lands on JoinLandingPage for the correct room; can join 3D / spectate 2D.
- [ ] Click 隐藏二维码 → QR + size button hide, label flips to 显示二维码; click again → reappears.
- [ ] Click 尺寸 → QR grows to 512px and stays crisp; label flips 小⇄大.
- [ ] Copied link (`Copy Link`) and QR encode the **same** URL.
- [ ] Editor play (no origin) → no QR, no errors.
- [ ] Trigger a reconnect → QR re-renders without visual glitch or memory growth.

---

## Acceptance Criteria
- [ ] QR of the join-landing URL shows on the professor host screen after a room is created.
- [ ] Show/Hide button toggles QR visibility with a flipping label.
- [ ] Size button cycles the QR Small↔Large, regenerating a crisp texture.
- [ ] QR and the copy-link button encode the identical, web-canonical URL `{origin}/#/join/{CODE}`.
- [ ] All EditMode tests pass; no regressions in the suite.
- [ ] No Unity compile errors; WebGL/IL2CPP build succeeds (no `System.Drawing`).
- [ ] Scene wiring committed with the `.unity`/`.prefab` file (fields non-null).

## Completion Checklist
- [ ] Code follows discovered patterns (public fields + [Header], SetActive, button+label toggle).
- [ ] Pure logic (`QrPanelState`, `StudentLinkBuilder`) is UnityEngine-free and tested.
- [ ] Texture lifecycle managed (no WebGL leak).
- [ ] Tests follow NUnit `Method_Scenario_Expected` convention.
- [ ] No hardcoded values that should be tunable (sizes live in `QrPanelState`).
- [ ] `technical-preferences.md` updated with QRCoder approval.
- [ ] No web-app changes; no networking changes.
- [ ] Self-contained — no further codebase search needed.

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Vendored QRCoder pulls a `System.Drawing` file → WebGL build fails | Medium | High | Vendor ONLY the 3 core files; grep the folder for `System.Drawing` = 0 hits (Task 3). |
| Scene wiring not committed → fields null, QR never shows (silent) | Medium | High | Task 9 + memory `[[scene-wiring-lags-merged-scripts]]`; verify serialized refs in committed scene; prefer UnitySkills API. |
| Fixing `BuildJoinLink` changes the copied link shape | Low | Low | The old `/survey/` route is already stale (App.jsx route is `/join/:roomCode`); new shape matches the web QR and live route. |
| Non-integer module→pixel scaling blurs QR at some URL lengths | Low | Medium | Regenerate at display resolution (256/512) + `FilterMode.Point`; if a URL fails to scan, round `pixelSize` up to a multiple of `modules`. |
| IL2CPP strips QRCoder types | Low | Medium | Add `link.xml` preserving `QRCoder` if a runtime `TypeLoadException` appears. |
| WebGL texture leak on repeated reconnects | Low | Medium | `Destroy(qrTexture)` before regen and in `OnDestroy`. |

## Notes
- **Two host QRs now exist**: the React `HostRoomPanel.jsx` (web dashboard) and this new in-Unity
  one. After the `BuildJoinLink` fix both encode the same `{origin}/#/join/{CODE}` — intentional
  parity. Whether the web dashboard host modal becomes redundant once the in-game QR ships is a
  separate product decision, out of scope here.
- The pure/glue split (`StudentLinkBuilder`, `QrPanelState` pure & tested; `QrCodeRenderer`,
  `SetupScreen` glue) matches the project's established `*Builder`/`*Formatter`/`*Decision`
  testability idiom.
- Verification-driven per coding-standards: pure logic has BLOCKING unit tests; the visual QR is
  validated by phone-scan screenshots (ADVISORY UI gate). See project memory
  `[[unity-playmode-verification]]` for the UnitySkills play-mode screenshot flow.
