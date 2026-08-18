# Plan: Professor Builder UI

## Summary
Build runtime UI panels enabling professors to create/edit survey questions, attribute mappings, and event rules entirely within the game — without touching JSON files. Integrates with the existing SetupScreen and SurveyConfigManager from Phase 3.

## User Story
As a professor, I want to create custom survey questions, define how responses map to car attributes, and configure race events using an in-game visual editor, so that I can set up EDI demonstrations without any technical knowledge or file editing.

## Problem -> Solution
Professor must manually write JSON config files or use pre-built templates -> Professor uses visual in-game UI with dropdowns, input fields, and buttons to build complete survey configs in under 15 minutes.

## Metadata
- **Complexity**: Large
- **Source PRD**: `.claude/PRPs/prds/flexible-survey-and-mapping.prd.md`
- **PRD Phase**: Phase 4 — Professor Builder UI
- **Estimated Files**: 7 new + 2 modified = 9

---

## UX Design

### Before
```
+----------------------------------+
| Setup Screen                     |
|                                  |
| [Start with Default CSV]         |
| [Load Session]                   |
| [Host Room]                      |
|                                  |
| Info: Ready to start race.       |
+----------------------------------+
(Professor must manually create JSON config files outside the game)
```

### After
```
+------------------------------------------------------------------+
| Setup Screen                                                      |
|                                                                   |
| [New Survey]  [Load Config]  [Templates v]                       |
|                                                                   |
| Active: "Accessibility Survey" (3 questions, 3 mappings, 3 rules)|
|                                                                   |
| [Start with CSV]  [Start with Survey]  [Load Session]  [Host]    |
+------------------------------------------------------------------+

  |-- [New Survey] opens:
  +--------------------------------------------------------------+
  | SURVEY BUILDER                              [Save] [Back]     |
  | Config Name: [_______________]                                |
  |                                                               |
  | Tab: [Questions] [Mappings] [Rules]                          |
  |                                                               |
  | == Questions Tab ======================================       |
  | [+ Add Question]                                              |
  |                                                               |
  | Q1: "What is your primary language?"  [Text v]  [x]          |
  |     Required: [x]                                             |
  |                                                               |
  | Q2: "Do you have a disability?"  [MultipleChoice v]  [x]     |
  |     Options: No | Yes - Physical | Yes - Cognitive [+]        |
  |     Required: [x]                                             |
  |                                                               |
  | Q3: "Rate accommodation ease"  [Numeric v]  [x]              |
  |     Range: [1] to [10]   Required: [x]                       |
  +--------------------------------------------------------------+

  | == Mappings Tab ========================================      |
  | [+ Add Mapping]                                               |
  |                                                               |
  | Q: [primary_language v]  ->  Attr: [language]                 |
  |    Transform: [direct v]  Default: [English]                  |
  |                                                               |
  | Q: [disability v]  ->  Attr: [disability]                     |
  |    Transform: [lookup v]  Default: [none]                     |
  |    Lookup: No -> none | Yes - Physical -> physical [+]        |
  +--------------------------------------------------------------+

  | == Rules Tab ==========================================       |
  | [+ Add Rule]                                                  |
  |                                                               |
  | "Language Barrier"                                             |
  |  If [language v] [NotEquals v] [English]                      |
  |  Then speed [-10] for [8]s  Weather: [None v]                 |
  |  Repeat: [ ]                                     [x]          |
  +--------------------------------------------------------------+
```

### Interaction Changes
| Touchpoint | Before | After | Notes |
|---|---|---|---|
| Setup Screen | Only Start/Load/Host buttons | Adds New Survey, Load Config, Templates buttons | Professor now has config management |
| Config creation | Edit JSON files externally | Visual tabbed editor inside the game | Zero code/file knowledge needed |
| Template usage | Call SurveyConfigManager API | Select from dropdown on Setup Screen | One-click template loading |
| Active config | No visual indicator | Status line shows config name + counts | Professor knows what config is active |

---

## Mandatory Reading

Files that MUST be read before implementing:

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 | `Assets/Scripts/Data/SurveyConfig.cs` | all | Data model being edited |
| P0 | `Assets/Scripts/Data/SurveyQuestion.cs` | all | Question struct fields |
| P0 | `Assets/Scripts/Data/AttributeMapping.cs` | all | Mapping struct fields |
| P0 | `Assets/Scripts/Data/SurveyConfigManager.cs` | all | Save/Load/Template API |
| P0 | `Assets/Scripts/Events/EventRule.cs` | all | Rule struct to create |
| P0 | `Assets/Scripts/Data/SessionData.cs` | 65-108 | SavedEventRule serialization |
| P0 | `Assets/Scripts/Events/ComparisonOperator.cs` | all | Dropdown options for rules |
| P0 | `Assets/Scripts/Events/WeatherType.cs` | all | Dropdown options for weather |
| P1 | `Assets/Scripts/UI/SetupScreen.cs` | all | Integration point |
| P1 | `Assets/Scripts/UI/EventPanel.cs` | all | Dynamic row instantiation pattern |
| P1 | `Assets/Scripts/RuntimeSetup.cs` | 386-462 | UI factory helpers to mirror |
| P1 | `Assets/Scripts/UI/RaceUI.cs` | all | Panel visibility/state management |
| P2 | `Assets/Scripts/UI/RaceControlPanel.cs` | all | Status feedback pattern |
| P2 | `Assets/Scripts/Data/SurveyTemplates.cs` | all | Template names/content |

## External Documentation

| Topic | Source | Key Takeaway |
|---|---|---|
| Unity UGUI ScrollView | Unity Manual | Use ScrollRect + Content with VerticalLayoutGroup for scrollable dynamic lists |
| Unity InputField | Unity API | Legacy InputField (not TMP) for text entry; set contentType for numeric |
| Unity Dropdown | Unity API | Legacy UI.Dropdown; populate via AddOptions(List<string>) |

---

## Patterns to Mirror

### NAMING_CONVENTION
```csharp
// SOURCE: Assets/Scripts/UI/EventPanel.cs:1-10
// Class: PascalCase MonoBehaviour, single responsibility per file
// Fields: PascalCase public with [Header] groups and [Tooltip]
// Private methods: PascalCase, no prefix
public class EventPanel : MonoBehaviour
{
    [Header("References")]
    public EventManager EventManager;

    [Header("UI Elements")]
    public Transform ContentParent;
    public GameObject EventRowPrefab;
}
```

### DYNAMIC_UI_CONSTRUCTION
```csharp
// SOURCE: Assets/Scripts/UI/EventPanel.cs:41-68
// Pattern: Instantiate prefab into ContentParent, get child components, bind click with captured index
private void BuildEventRows()
{
    var events = EventManager.Schedule.Events;
    for (int i = 0; i < events.Length; i++)
    {
        GameObject row = Instantiate(EventRowPrefab, ContentParent);
        var button = row.GetComponentInChildren<Button>();
        var text = row.GetComponentInChildren<Text>();
        if (text != null) text.text = $"[{i + 1}] {events[i].DisplayName}";
        if (button != null)
        {
            int index = i; // capture for closure
            button.onClick.AddListener(() => TriggerEvent(index));
        }
    }
}
```

### RUNTIME_UI_FACTORY
```csharp
// SOURCE: Assets/Scripts/RuntimeSetup.cs:386-462
// Pattern: Programmatic UI creation with anchor-based positioning
private GameObject CreatePanel(Transform parent, string name,
    Vector2 anchorMin, Vector2 anchorMax,
    Vector2 offsetMin, Vector2 offsetMax)
{
    GameObject obj = new GameObject(name);
    obj.transform.SetParent(parent, false);
    Image bg = obj.AddComponent<Image>();
    bg.color = new Color(0, 0, 0, 0.6f);
    RectTransform rt = obj.GetComponent<RectTransform>();
    rt.anchorMin = anchorMin;
    rt.anchorMax = anchorMax;
    rt.pivot = anchorMin;
    rt.offsetMin = offsetMin;
    rt.offsetMax = offsetMax;
    return obj;
}
```

### PANEL_VISIBILITY
```csharp
// SOURCE: Assets/Scripts/UI/RaceUI.cs:69-83
// Pattern: SetActive based on GameState, subscribe in Start, unsubscribe in OnDestroy
private void OnStateChanged(GameState state)
{
    bool isSetup = state == GameState.Setup;
    if (Setup != null) Setup.gameObject.SetActive(isSetup);
}
```

### EVENT_SUBSCRIPTION
```csharp
// SOURCE: Assets/Scripts/UI/SetupScreen.cs:53-68
// Pattern: Subscribe in OnEnable, unsubscribe in OnDisable
private void OnEnable()
{
    if (NetworkManager != null)
    {
        NetworkManager.OnRoomCreated += OnRoomCreated;
        NetworkManager.OnStudentCountChanged += OnStudentCountChanged;
    }
}
private void OnDisable()
{
    if (NetworkManager != null)
    {
        NetworkManager.OnRoomCreated -= OnRoomCreated;
        NetworkManager.OnStudentCountChanged -= OnStudentCountChanged;
    }
}
```

### STATUS_FEEDBACK
```csharp
// SOURCE: Assets/Scripts/UI/RaceControlPanel.cs:69-93
// Pattern: Show message text, fade after delay via coroutine
private void ShowStatus(string message)
{
    if (StatusText == null) return;
    StatusText.text = message;
    StatusText.color = Color.white;
    if (statusFadeCoroutine != null) StopCoroutine(statusFadeCoroutine);
    statusFadeCoroutine = StartCoroutine(FadeStatus());
}
```

### DATA_SERIALIZATION
```csharp
// SOURCE: Assets/Scripts/Data/SessionData.cs:77-108
// Pattern: SavedEventRule as serializable copy without runtime state
// Use (int) cast for enums, string for all values, paired From/To methods
public static SavedEventRule FromRule(EventRule rule) { ... }
public EventRule ToRule(Key triggerKey) { ... }
```

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `Assets/Scripts/UI/SurveyBuilderPanel.cs` | CREATE | Main builder panel with tabs for questions/mappings/rules |
| `Assets/Scripts/UI/QuestionEditorRow.cs` | CREATE | Individual question row within survey builder |
| `Assets/Scripts/UI/MappingEditorRow.cs` | CREATE | Individual mapping row within mappings tab |
| `Assets/Scripts/UI/RuleEditorRow.cs` | CREATE | Individual rule row within rules tab |
| `Assets/Scripts/UI/ConfigManagerPanel.cs` | CREATE | Save/Load/Template selection panel |
| `Assets/Scripts/UI/BuilderUIFactory.cs` | CREATE | Static factory methods for creating UI elements (mirrors RuntimeSetup helpers) |
| `Assets/Scripts/UI/TabButton.cs` | CREATE | Simple tab selection component |
| `Assets/Scripts/UI/SetupScreen.cs` | UPDATE | Add config management buttons and builder integration |
| `Assets/Scripts/RuntimeSetup.cs` | UPDATE | Wire SurveyBuilderPanel at runtime |

## NOT Building

- Drag-and-drop reordering of questions (use up/down buttons instead)
- Undo/redo system for edits (professor can reload config)
- Live preview of how cars would look with current config
- Import/export to other professors (config is JSON file — manual sharing is fine for now)
- Compound rule conditions (AND/OR) — single condition per rule only
- Keyboard shortcuts within the builder
- Dark/light theme toggle
- Mobile-optimized layout (desktop browser only for professors)

---

## Step-by-Step Tasks

### Task 1: BuilderUIFactory (Static helpers)
- **ACTION**: Create `Assets/Scripts/UI/BuilderUIFactory.cs` — static class with factory methods for all UI primitives needed by builder panels
- **IMPLEMENT**: 
  - `CreatePanel(parent, name, anchorMin, anchorMax, offsetMin, offsetMax, bgColor)` — returns GameObject with Image bg and RectTransform
  - `CreateText(parent, name, content, fontSize, alignment, anchorMin, anchorMax, offsetMin, offsetMax)` — returns Text component
  - `CreateButton(parent, name, label, anchorMin, anchorMax, offsetMin, offsetMax)` — returns Button with label
  - `CreateInputField(parent, name, placeholder, anchorMin, anchorMax, offsetMin, offsetMax)` — returns InputField with placeholder Text
  - `CreateDropdown(parent, name, options, anchorMin, anchorMax, offsetMin, offsetMax)` — returns Dropdown with options
  - `CreateToggle(parent, name, label, anchorMin, anchorMax, offsetMin, offsetMax)` — returns Toggle with label
  - `CreateScrollView(parent, name, anchorMin, anchorMax, offsetMin, offsetMax)` — returns ScrollRect with Content (VerticalLayoutGroup + ContentSizeFitter)
- **MIRROR**: RUNTIME_UI_FACTORY from RuntimeSetup.cs:386-462
- **IMPORTS**: `UnityEngine; UnityEngine.UI;`
- **GOTCHA**: Use `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")` — same as RuntimeSetup; InputField needs both Text and child Placeholder Text; ScrollRect needs Mask + Image on viewport, VerticalLayoutGroup + ContentSizeFitter on Content child
- **VALIDATE**: Helper produces visible, interactable UI elements when called from any MonoBehaviour; no compile errors

### Task 2: TabButton component
- **ACTION**: Create `Assets/Scripts/UI/TabButton.cs` — simple reusable tab switching component
- **IMPLEMENT**:
  - Fields: `Button TabBtn`, `GameObject TabContent`, `Text Label`
  - Method `SetSelected(bool)` — highlight button color, show/hide TabContent
  - Static helper: `SelectTab(TabButton[] tabs, int index)` — deselects all except index
- **MIRROR**: NAMING_CONVENTION
- **IMPORTS**: `UnityEngine; UnityEngine.UI;`
- **GOTCHA**: Button colors via `btn.colors` ColorBlock, not direct color change
- **VALIDATE**: Clicking tab shows one content panel, hides others

### Task 3: QuestionEditorRow
- **ACTION**: Create `Assets/Scripts/UI/QuestionEditorRow.cs` — MonoBehaviour for a single question edit row, built programmatically
- **IMPLEMENT**:
  - Method `Build(Transform parent, SurveyQuestion data, int index, System.Action<int> onDelete, System.Action<int> onMoveUp, System.Action<int> onMoveDown)`
  - UI contains: index label, question text InputField, QuestionType dropdown (Text/MultipleChoice/Numeric), Required toggle, Delete button, Up/Down buttons
  - Conditional section: if MultipleChoice → show options list with Add/Remove; if Numeric → show Min/Max fields
  - Method `ToQuestion()` → returns populated SurveyQuestion struct (auto-generates Id from index if empty)
  - Method `Refresh(SurveyQuestion data, int index)` → updates UI from data
  - Type dropdown `onValueChanged` toggles conditional sections
- **MIRROR**: DYNAMIC_UI_CONSTRUCTION, RUNTIME_UI_FACTORY
- **IMPORTS**: `System; System.Collections.Generic; UnityEngine; UnityEngine.UI;`
- **GOTCHA**: When QuestionType changes, must destroy/rebuild conditional section; options stored as comma-separated in a single InputField or as dynamic sub-rows; use unique Id = $"q_{index}" if Id is empty
- **VALIDATE**: Can create a row, change type, set text, toggle required, get back valid SurveyQuestion

### Task 4: MappingEditorRow
- **ACTION**: Create `Assets/Scripts/UI/MappingEditorRow.cs` — MonoBehaviour for a single attribute mapping edit row
- **IMPLEMENT**:
  - Method `Build(Transform parent, AttributeMapping data, int index, string[] questionIds, System.Action<int> onDelete)`
  - UI contains: QuestionId dropdown (populated from available questions), AttributeName InputField, TransformType dropdown (direct/lookup/numeric), DefaultValue InputField, Delete button
  - Conditional: if TransformType == "lookup" → show lookup entries sub-list (key InputField → value InputField) with Add/Remove
  - Method `ToMapping()` → returns populated AttributeMapping struct
  - Method `Refresh(AttributeMapping data, int index, string[] questionIds)` → updates UI
- **MIRROR**: DYNAMIC_UI_CONSTRUCTION
- **IMPORTS**: `System; System.Collections.Generic; UnityEngine; UnityEngine.UI;`
- **GOTCHA**: QuestionId dropdown must refresh when questions tab changes; LookupEntries use AttributeEntry[] (Key/Value pairs); TransformType change triggers conditional rebuild
- **VALIDATE**: Can create mapping row, select question, set attribute name, switch to lookup and add entries, get back valid AttributeMapping

### Task 5: RuleEditorRow
- **ACTION**: Create `Assets/Scripts/UI/RuleEditorRow.cs` — MonoBehaviour for a single event rule edit row
- **IMPLEMENT**:
  - Method `Build(Transform parent, SavedEventRule data, int index, System.Action<int> onDelete)`
  - UI contains: DisplayName InputField, AttributeName InputField, Operator dropdown (from ComparisonOperator enum names), CompareValue InputField, SpeedDelta InputField (numeric), Duration InputField (numeric), Weather dropdown (from WeatherType enum names), AllowRepeat toggle, Delete button
  - Method `ToRule()` → returns populated SavedEventRule struct
  - Method `Refresh(SavedEventRule data, int index)` → updates UI
  - Operator dropdown: hide AttributeName/CompareValue when "All" is selected
- **MIRROR**: DYNAMIC_UI_CONSTRUCTION, DATA_SERIALIZATION
- **IMPORTS**: `System; UnityEngine; UnityEngine.UI;`
- **GOTCHA**: SpeedDelta can be negative (penalty) or positive (boost) — use contentType=Standard not IntegerNumber; Operator stored as int in SavedEventRule; max 9 rules (keyboard keys 1-9)
- **VALIDATE**: Can create rule row, fill all fields, get back valid SavedEventRule matching original data

### Task 6: SurveyBuilderPanel (main tabbed editor)
- **ACTION**: Create `Assets/Scripts/UI/SurveyBuilderPanel.cs` — MonoBehaviour managing the 3-tab builder interface
- **IMPLEMENT**:
  - Fields: `SurveyConfigManager ConfigManager`, `SetupScreen SetupScreen`
  - Private state: `SurveyConfig editingConfig`, lists of row components
  - `Show(SurveyConfig config)` — opens panel, populates from config (null = new blank config)
  - `Hide()` — closes panel, returns to SetupScreen
  - Three tabs: Questions, Mappings, Rules — use TabButton[]
  - Questions tab: ScrollView with QuestionEditorRow instances, [+ Add Question] button at bottom
  - Mappings tab: ScrollView with MappingEditorRow instances, [+ Add Mapping] button
  - Rules tab: ScrollView with RuleEditorRow instances, [+ Add Rule] button
  - Top bar: ConfigName InputField, [Save] button, [Back] button
  - `Save()` — collects all row data into SurveyConfig, calls ConfigManager.SaveConfig(), sets as active
  - `CollectConfig()` → builds SurveyConfig from all rows
  - Question add/delete/reorder updates mapping dropdown options
  - Maximum limits: 20 questions, 20 mappings, 9 rules (with warning text)
- **MIRROR**: PANEL_VISIBILITY, DYNAMIC_UI_CONSTRUCTION, STATUS_FEEDBACK
- **IMPORTS**: `System; System.Collections.Generic; UnityEngine; UnityEngine.UI;`
- **GOTCHA**: When questions change, must refresh mapping QuestionId dropdowns; destroying rows must update indices; save must call `ConfigManager.SetActiveConfig(config)` after save; entire panel built programmatically in `Awake()` or `Build()`
- **VALIDATE**: Can open builder, add questions of each type, add mappings pointing to questions, add rules, save config, re-open and see all data intact

### Task 7: ConfigManagerPanel
- **ACTION**: Create `Assets/Scripts/UI/ConfigManagerPanel.cs` — small overlay for load/template selection
- **IMPLEMENT**:
  - Fields: `SurveyConfigManager ConfigManager`, `SurveyBuilderPanel BuilderPanel`
  - Method `ShowLoadPanel()` — lists saved configs (from GetSavedConfigPaths()), each as a button
  - Method `ShowTemplatePanel()` — lists templates (from GetTemplateNames()), each as a button
  - On select: loads config, calls `ConfigManager.SetActiveConfig()`, updates SetupScreen info text
  - Optional: "Edit" button that opens selected config in BuilderPanel
  - Dismiss: click outside or [X] button
- **MIRROR**: DYNAMIC_UI_CONSTRUCTION, EVENT_SUBSCRIPTION
- **IMPORTS**: `System; UnityEngine; UnityEngine.UI;`
- **GOTCHA**: GetSavedConfigPaths() returns full paths — display only filename; template configs have no file path (they're in-memory); rebuild list each time panel opens (configs may have been saved in between)
- **VALIDATE**: Can see saved configs and templates, select one, it becomes active config

### Task 8: Update SetupScreen
- **ACTION**: Modify `Assets/Scripts/UI/SetupScreen.cs` to integrate config management
- **IMPLEMENT**:
  - Add fields: `SurveyConfigManager SurveyConfigManager`, `SurveyBuilderPanel BuilderPanel`, `ConfigManagerPanel ConfigPanel`
  - Add UI elements: `Button NewSurveyButton`, `Button LoadConfigButton`, `Button TemplateButton`, `Text ActiveConfigText`, `Button StartWithSurveyButton`
  - `NewSurveyButton.onClick` → opens BuilderPanel with null config
  - `LoadConfigButton.onClick` → opens ConfigPanel in load mode
  - `TemplateButton.onClick` → opens ConfigPanel in template mode  
  - `StartWithSurveyButton.onClick` → starts race using active config's CSV or survey data
  - Update `InfoText` to show active config summary when one is set
  - Add method `RefreshActiveConfigDisplay()` — shows "Active: {name} ({n} questions, {m} rules)"
  - OnEnable: subscribe to ConfigManager state if needed
- **MIRROR**: EVENT_SUBSCRIPTION, NAMING_CONVENTION
- **IMPORTS**: `UnityEngine; UnityEngine.UI;`
- **GOTCHA**: All new fields are optional (null-guarded) to not break existing scene setups without the builder; StartWithSurvey needs active config with at least one question or one rule to be meaningful
- **VALIDATE**: Setup screen shows new buttons; clicking New Survey opens builder; loading a config updates status text; starting race with config applies rules to EventSchedule

### Task 9: Update RuntimeSetup
- **ACTION**: Modify `Assets/Scripts/RuntimeSetup.cs` to auto-wire the builder panels when no Inspector setup exists
- **IMPLEMENT**:
  - In `SetupUI()`, after building existing panels, check if `SurveyBuilderPanel` exists; if not, create and wire:
    - Create BuilderPanel GameObject with SurveyBuilderPanel component
    - Create ConfigManagerPanel GameObject with ConfigManagerPanel component
    - Wire references: ConfigManager from RaceManager.SurveyConfigManager, SetupScreen from existing
    - Wire SetupScreen references to new panels
  - Builder panel starts hidden (SetActive false)
  - ConfigPanel starts hidden
- **MIRROR**: RUNTIME_UI_FACTORY — follows existing RuntimeSetup pattern of auto-creating missing UI
- **IMPORTS**: existing imports sufficient
- **GOTCHA**: Must check `raceManager.SurveyConfigManager != null` before creating builder (graceful skip if no ConfigManager); RuntimeSetup already has auto-find pattern; builder panel is large — use near-fullscreen anchors (0.05, 0.05) to (0.95, 0.95)
- **VALIDATE**: Enter Play Mode in complete_track_demo scene → Setup Screen shows builder buttons without any Inspector configuration; clicking "New Survey" shows the builder panel

---

## Testing Strategy

### Unit Tests

This is a Unity project with no automated test pipeline. Validation is manual in-Editor.

| Test | Input | Expected Output | Edge Case? |
|---|---|---|---|
| Create blank config | Click "New Survey" | Builder opens with empty tabs, no rows | No |
| Add question (Text) | Click "+ Add Question", select Text type | Row appears with text input, required toggle | No |
| Add question (MultipleChoice) | Select MultipleChoice type | Options field appears with Add button | No |
| Add question (Numeric) | Select Numeric type | Min/Max fields appear | No |
| Delete question | Click [x] on question row | Row removed, indices re-numbered | No |
| Add mapping | Click "+ Add Mapping" | Row appears with question dropdown populated | No |
| Mapping question sync | Add question, then check mapping dropdown | New question appears in dropdown options | No |
| Add rule | Click "+ Add Rule" | Row appears with all fields | No |
| Rule "All" operator | Select "All" in operator dropdown | AttributeName and CompareValue fields hidden | Yes |
| Save config | Fill name, click Save | Config file created, status shows "Saved" | No |
| Load config | Click Load, select config | Config loads into builder with all data | No |
| Load template | Click Templates, select "Accessibility" | Template loads with 3 questions, 3 mappings, 3 rules | No |
| Max rules limit | Add 10th rule | Warning shown "Maximum 9 rules" | Yes |
| Empty config name | Try to save with empty name | Warning shown "Please enter a config name" | Yes |
| Start with config | Set active config, click Start | Race uses config's rules in EventSchedule | No |

### Edge Cases Checklist
- [x] Empty config (no questions, no mappings, no rules) — valid, acts like V1
- [x] Maximum 9 rules (keyboard key limit)
- [x] Maximum 20 questions/mappings (UI scrolling)
- [x] Question deleted while mapping references it — mapping dropdown shows "(deleted)"
- [x] Numeric fields with non-numeric input — fallback to 0
- [x] Very long question text — horizontal overflow, no line break needed
- [x] Config name with special characters — SurveyConfigManager.SanitizeFileName handles this
- [x] No SurveyConfigManager in scene — builder buttons don't appear (null guards)

---

## Validation Commands

### Static Analysis
```bash
# Unity project — no CLI type checker
# Validation: Open Unity Editor, check Console for compile errors
```
EXPECT: Zero C# compiler errors in Console

### Build Verification
```bash
# Enter Play Mode in complete_track_demo scene
# Check no errors in Console on startup
```
EXPECT: Game enters Setup state, Setup Screen shows builder buttons

### Manual Validation
- [ ] Click "New Survey" — builder panel appears fullscreen-ish with 3 tabs
- [ ] Add 3 questions (one of each type) — all render correctly
- [ ] Switch to Mappings tab — dropdown shows the 3 questions
- [ ] Add a mapping with "lookup" transform — lookup entries sub-list appears
- [ ] Switch to Rules tab — add a rule with NotEquals operator
- [ ] Enter config name, click Save — status shows "Saved", file exists in persistentDataPath/SurveyConfigs/
- [ ] Click Back — returns to Setup Screen, active config line shows name
- [ ] Click "Load Config" — saved config appears in list
- [ ] Select it — config loads back into builder with all data intact
- [ ] Click "Templates" — 3 templates listed
- [ ] Select "Accessibility" — loads with 3 questions, 3 mappings, 3 rules
- [ ] Start race with active config — EventSchedule updated with config's rules
- [ ] During race, trigger event via keyboard or EventPanel — rule applies correctly

---

## Acceptance Criteria
- [ ] All 9 tasks completed
- [ ] Zero C# compile errors
- [ ] Professor can create a complete config (questions + mappings + rules) using only UI
- [ ] Config saves to JSON and loads back with all data intact
- [ ] Templates load correctly and can be edited/re-saved
- [ ] Starting race with active config applies rules to EventSchedule
- [ ] All new fields are null-guarded (no errors if components missing in scene)
- [ ] Under 15 minutes for unfamiliar user to create a 3-question survey with rules

## Completion Checklist
- [ ] All code in global namespace (no `namespace` declarations)
- [ ] Uses `UnityEngine.UI` (legacy), not TextMeshPro
- [ ] PascalCase naming for classes, fields, methods
- [ ] [Header] and [Tooltip] on public serialized fields
- [ ] `SetActive` for panel show/hide
- [ ] Event subscription in OnEnable, unsubscription in OnDisable
- [ ] Null guards on all optional references
- [ ] No hardcoded strings for enum values (use `Enum.GetNames()`)
- [ ] Builder panel hidden by default (shown only on button click)
- [ ] RuntimeSetup auto-wires when Inspector not configured

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| UI layout breaks at different resolutions | MEDIUM | LOW | Use anchor-based positioning (percentage of screen), test at 1920x1080 and 1280x720 |
| ScrollView performance with many rows | LOW | LOW | Max 20 questions + 20 mappings + 9 rules = 49 rows max, no pooling needed |
| Builder panel occludes important setup info | LOW | MEDIUM | Use near-fullscreen overlay with semi-transparent bg; Back button always visible |
| Legacy UI.Dropdown limited styling | MEDIUM | LOW | Acceptable for professor tool — function over form |
| RuntimeSetup conflicts with Inspector-configured panels | LOW | HIGH | All auto-creation guarded by `FindFirstObjectByType<T>() == null` checks |

## Notes
- The builder UI is built entirely at runtime via code (no prefabs needed). This matches the RuntimeSetup pattern and ensures it works in any scene without additional asset setup.
- Future Phase 5 (Student Survey) will reuse `SurveyQuestion` rendering logic from QuestionEditorRow — consider extracting a shared `QuestionRenderer` later.
- Config files are shared via the filesystem. A future "Export/Import" feature could add copy-to-clipboard for JSON sharing.
- The 9-rule keyboard limit comes from `EventSchedule` using Digit1-Digit9 keys. The UI should show this limit clearly.
