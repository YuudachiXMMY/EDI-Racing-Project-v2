# Plan: ENGG*1100 Survey Default Template

## Summary
Create a new default survey template "ENGG*1100 Survey" that replicates the original MS Forms questionnaire used in ENGG*1100. The template includes 14 questions, a mapping pipeline with average-threshold aggregate logic (porting DataTool.py algorithm), V1-compatible event rules, and dual export capability (Excel for professor analysis, CSV/JSON for Unity game).

## User Story
As a professor, I want a pre-built survey template that exactly mirrors the original MS Forms questionnaire, so that I can collect student team data through the web-app and export it in the same formats (Excel + game data) without needing external tools like DataTool.py.

## Problem -> Solution
**Current state**: Professor uses external MS Forms + DataTool.py + manual CSV import. The V1 Parity template has no questions/mappings (CSV import only).
**Desired state**: A single web-app template with all 14 questions, automatic average-threshold processing, Excel export matching `input.xlsx`, and game data export matching `vehicleGroupData.csv`.

## Metadata
- **Complexity**: Large
- **Source PRD**: N/A (user request)
- **PRD Phase**: N/A
- **Estimated Files**: 12-15

---

## UX Design

### Before
```
Professor flow (external tools):
  MS Forms -> input.xlsx -> DataTool.py -> vehicleGroupData.csv -> Unity CSV import
  (3 separate tools, manual file shuffling)
```

### After
```
Professor flow (all-in-one):
  Web-App Survey (ENGG*1100 template) -> Export Excel (for analysis)
                                       -> Send to Game (auto-processed)
  (Single tool, automatic processing)
```

### Interaction Changes
| Touchpoint | Before | After | Notes |
|---|---|---|---|
| Survey creation | Use MS Forms externally | Click "ENGG*1100 Survey" template button | Dashboard template picker |
| Data collection | Students fill MS Form | Students fill web-app survey at share link | Same questions, same order |
| Excel export | Download from MS Forms | Click "Export Excel" in editor | Same column structure as input.xlsx |
| Game data | Run DataTool.py manually | Click "Send to Game" or "Export for Unity" | Average-threshold logic runs server-side |
| Event rules | Manually configure in Unity | Pre-loaded from template, keyboard 1-7 | Same V1 Parity rules |

---

## Mandatory Reading

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 | `web-app/src/seed-templates.js` | all | Template structure, seeding pattern |
| P0 | `web-app/src/routes/export.js` | 1-115 | Export pipeline: applyTransform + mapResponsesToCarData |
| P0 | `web-app/src/db.js` | all | DB init, migration pattern, template seeding |
| P0 | `web-app/src/schema.sql` | all | DB schema: surveys, responses, templates tables |
| P1 | `web-app/client/src/surveyjs-config.js` | all | Unity <-> SurveyJS question conversion |
| P1 | `web-app/client/src/constants.js` | all | Enum parity (QuestionType, ComparisonOperator, etc.) |
| P1 | `web-app/client/src/pages/EditorPage.jsx` | all | Editor UI — add export buttons here |
| P1 | `web-app/client/src/components/QuestionsTab.jsx` | all | SurveyJS Creator config — need checkbox type |
| P1 | `web-app/src/routes/responses.js` | all | Student response submission flow |
| P2 | `Assets/Scripts/Data/SurveyTemplates.cs` | all | Unity-side template definitions (keep in sync) |
| P2 | `Assets/Scripts/Data/SessionData.cs` | 62-109 | SavedEventRule structure |
| P2 | `Assets/Scripts/Events/EventSchedule.cs` | all | Default event rules with TriggerKey bindings |
| P2 | `Assets/Scripts/Data/CarData.cs` | all | CarData attribute system |
| P2 | `Assets/Scripts/Events/ComparisonOperator.cs` | all | Operator enum values |

---

## Patterns to Mirror

### TEMPLATE_STRUCTURE
```js
// SOURCE: web-app/src/seed-templates.js:6-61
// Each template has: name, description, questions[], mappings[], rules[]
// Questions use: { Id, Text, Type, Options, MinValue, MaxValue, Required }
// Mappings use: { QuestionId, AttributeName, DefaultValue, TransformType, LookupEntries }
// Rules use: { DisplayName, AttributeName, Operator, CompareValue, SpeedDelta, Duration, Weather, AllowRepeat }
```

### EXPORT_TRANSFORM
```js
// SOURCE: web-app/src/routes/export.js:15-37
// applyTransform handles: 'lookup' (case-insensitive key match), 'numeric' (parseFloat), 'direct' (pass-through)
function applyTransform(responseValue, mapping) {
  const transformType = (mapping.TransformType || 'direct').toLowerCase();
  switch (transformType) {
    case 'lookup': { /* match LookupEntries by Key */ }
    case 'numeric': { /* parseFloat validation */ }
    case 'direct': default: return responseValue;
  }
}
```

### MAP_RESPONSES_TO_CARDATA
```js
// SOURCE: web-app/src/routes/export.js:48-82
// Iterates mappings, looks up answer by QuestionId, applies transform, produces { teamName, attributes[] }
function mapResponsesToCarData(teamName, answers, mappings) {
  // ...per-response attribute generation
}
```

### DB_MIGRATION_PATTERN
```js
// SOURCE: web-app/src/db.js:24-28
// Wrap ALTER TABLE in try/catch to handle "column already exists"
try {
  db.exec('ALTER TABLE surveys ADD COLUMN linked_room_code TEXT DEFAULT NULL');
} catch { /* Column already exists */ }
```

### SURVEYJS_QUESTION_TYPES
```js
// SOURCE: web-app/client/src/surveyjs-config.js:19-26
// MultipleChoice -> 'radiogroup', Numeric -> 'text' with inputType:'number', Text -> 'text'
// Also handles 'checkbox' in reverse conversion (line 54)
```

### SEEDING_GUARD
```js
// SOURCE: web-app/src/db.js:49-52
const count = db.prepare('SELECT COUNT(*) as c FROM templates').get().c;
if (count === 0) { seedTemplates(db); }
```

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `web-app/src/seed-templates.js` | UPDATE | Add ENGG*1100 Survey template with 14 questions, mappings, post-processing config, and V1 rules |
| `web-app/src/routes/export.js` | UPDATE | Add `average_threshold`/`fixed_threshold` aggregate processing in export pipeline; add Excel & CSV export endpoints |
| `web-app/src/db.js` | UPDATE | Add `post_processing_json` column migration for surveys & templates tables; re-seed logic for new template |
| `web-app/src/schema.sql` | UPDATE | Add `post_processing_json` column to templates table definition |
| `web-app/client/src/surveyjs-config.js` | UPDATE | Handle `checkbox` type in Unity->SurveyJS conversion (for Q14 multi-select) |
| `web-app/client/src/components/QuestionsTab.jsx` | UPDATE | Add 'checkbox' to allowed SurveyJS question types |
| `web-app/client/src/constants.js` | UPDATE | Add TransformTypes for new transforms; add MultiSelect QuestionType |
| `web-app/client/src/pages/EditorPage.jsx` | UPDATE | Add "Export Excel" and "Export CSV" buttons |
| `web-app/client/src/api.js` | UPDATE | Add API functions for Excel/CSV export |
| `web-app/src/routes/templates.js` | UPDATE | Include post_processing in template response |
| `Assets/Scripts/Data/SurveyTemplates.cs` | UPDATE | Add ENGG*1100 Survey template matching web-app version |
| `Assets/Scripts/Data/SurveyQuestion.cs` | UPDATE | Add `MultiSelect = 3` to QuestionType enum |
| `web-app/package.json` | UPDATE | Add `xlsx` (SheetJS) dependency for Excel export |

## NOT Building

- Custom DataTool.py replacement CLI — the algorithm moves into the web-app export pipeline
- Modifications to EventManager.cs or RuleEngine.cs — existing rule engine already handles the V1 rules
- New SurveyJS custom question widgets — `checkbox` type is already built into SurveyJS
- Real-time aggregate recalculation — averages computed only at export/send-to-game time
- Column 8 (member count), 9 (member names), 16-19 — stored in responses but NOT mapped to game attributes (professor analysis only)

---

## Step-by-Step Tasks

### Task 1: Add `post_processing_json` column to DB schema

- **ACTION**: Add migration for `post_processing_json` TEXT column to `surveys` and `templates` tables
- **IMPLEMENT**:
  1. In `schema.sql`: add `post_processing_json TEXT NOT NULL DEFAULT '[]'` to `templates` table
  2. In `db.js`: add try/catch migration for both `surveys` and `templates` tables (same pattern as `linked_room_code`)
  3. In `db.js`: add re-seed logic — if template count is less than expected (3 → 4), call seedTemplates again for the new one
- **MIRROR**: DB_MIGRATION_PATTERN from `db.js:24-28`
- **IMPORTS**: None new
- **GOTCHA**: Existing DBs already have 3 templates. Use `INSERT OR IGNORE` (already in seedTemplates). But since template names are UNIQUE, adding a 4th template will just work. However, we need to handle the case where the DB already exists with 3 templates — detect by checking if "ENGG*1100 Survey" exists.
- **VALIDATE**: Start the web-app, verify the new column exists in both tables. Check that the new template appears in `GET /api/templates`.

### Task 2: Define the ENGG*1100 Survey template in seed-templates.js

- **ACTION**: Add the full template definition with 14 questions, per-response mappings, post-processing rules, and V1 event rules
- **IMPLEMENT**:
  Questions (14 total):
  ```js
  { Id: 'team_name', Text: 'Name your autonomous vehicle team.\n[Letters and numbers only, NO space] (e.g. Apollo3)', Type: 0 /* Text */, Options: [], MinValue: 0, MaxValue: 0, Required: true },
  { Id: 'color', Text: 'Choose the colour for your autonomous vehicle.', Type: 1 /* MC */, Options: ['Blue', 'Red', 'Black', 'White', 'Green'], MinValue: 0, MaxValue: 0, Required: true },
  { Id: 'member_count', Text: 'How many members in the team?', Type: 2 /* Numeric */, Options: [], MinValue: 1, MaxValue: 20, Required: true },
  { Id: 'member_names', Text: 'Please enter the first names of the members of your group, separated by commas. (e.g. Steve, Albert, John). This information will only be used to distribute prizes after. If you do not want the prize, enter NA.', Type: 0, Options: [], MinValue: 0, MaxValue: 0, Required: true },
  { Id: 'facial_count', Text: 'How many members in your team use a facial recognition function on their phones/PCs?', Type: 2, Options: [], MinValue: 0, MaxValue: 20, Required: true },
  { Id: 'glasses_count', Text: 'How many members in your team wear glasses or contact lenses?', Type: 2, Options: [], MinValue: 0, MaxValue: 20, Required: true },
  { Id: 'language_count', Text: 'How many different languages overall are spoken in your team?', Type: 2, Options: [], MinValue: 0, MaxValue: 50, Required: true },
  { Id: 'male_count', Text: 'How many members in the group identify themselves as male?', Type: 2, Options: [], MinValue: 0, MaxValue: 20, Required: true },
  { Id: 'pwd_count', Text: 'How many members in the team has their password with 5 characters or more?', Type: 2, Options: [], MinValue: 0, MaxValue: 20, Required: true },
  { Id: 'distance_km', Text: 'Consider all members, whose hometown (attended their high school) is the furthest from the University of Guelph? Enter the distance in kilometers below.', Type: 2, Options: [], MinValue: 0, MaxValue: 99999, Required: true },
  { Id: 'vehicle_type', Text: 'What type of vehicle would your team prefer to ride in?', Type: 1, Options: ['Convertible', 'Hatchback', 'Pickup truck', 'Sedan', 'SUV', 'Van'], MinValue: 0, MaxValue: 0, Required: false },
  { Id: 'entertainment', Text: 'What type of entertainment system do you utilize the most?', Type: 1, Options: ['Bluetooth', 'CD player', 'AUX connected devices', 'Apple CarPlay', 'Android AutoCar'], MinValue: 0, MaxValue: 0, Required: false },
  { Id: 'driving_experience', Text: 'What is the cumulative driving experience of your team (in years)?', Type: 2, Options: [], MinValue: 0, MaxValue: 200, Required: false },
  { Id: 'car_features', Text: 'Rank the following advanced in-car features in terms of importance to your team. (Select up to 3)', Type: 3 /* MultiSelect */, Options: ['Heads-up display', 'Automatic High Beams', 'Electronic Door Handles', 'Do-It-All Touchscreens', 'Camera Vision', 'Lane-Keep Assist', 'Full-self driving'], MinValue: 0, MaxValue: 3, Required: false },
  ```

  Per-response mappings (only game-relevant fields):
  ```js
  // Team name: direct pass-through (note: team_name question answer is used, but web-app already captures teamName separately from the form header)
  // Actually, team_name is ALSO a survey question — we need to handle this
  // The web-app asks for teamName in the header form. With this template, the team_name question IS the same data.
  // Solution: map team_name question to "teamName" attribute for completeness, but the web-app response.team_name field is the primary source.

  { QuestionId: 'color', AttributeName: 'colorIndex', DefaultValue: '0', TransformType: 'lookup',
    LookupEntries: [
      { Key: 'Green', Value: '0' }, { Key: 'Black', Value: '1' },
      { Key: 'Red', Value: '2' }, { Key: 'Blue', Value: '3' }, { Key: 'White', Value: '4' }
    ] },
  { QuestionId: 'facial_count', AttributeName: 'facial_count', DefaultValue: '0', TransformType: 'numeric', LookupEntries: [] },
  { QuestionId: 'glasses_count', AttributeName: 'glasses_count', DefaultValue: '0', TransformType: 'numeric', LookupEntries: [] },
  { QuestionId: 'language_count', AttributeName: 'language_count', DefaultValue: '0', TransformType: 'numeric', LookupEntries: [] },
  { QuestionId: 'male_count', AttributeName: 'male_count', DefaultValue: '0', TransformType: 'numeric', LookupEntries: [] },
  { QuestionId: 'pwd_count', AttributeName: 'pwd_count', DefaultValue: '0', TransformType: 'numeric', LookupEntries: [] },
  { QuestionId: 'distance_km', AttributeName: 'distance_km', DefaultValue: '0', TransformType: 'numeric', LookupEntries: [] },
  ```

  Post-processing rules (aggregate transforms — NEW concept):
  ```js
  [
    { type: 'average_threshold', sourceAttribute: 'facial_count', direction: 'gte', tagName: 'facerecog', targetAttribute: 'functions' },
    { type: 'average_threshold', sourceAttribute: 'glasses_count', direction: 'gte', tagName: 'glasses', targetAttribute: 'functions' },
    { type: 'average_threshold', sourceAttribute: 'language_count', direction: 'gte', tagName: 'language', targetAttribute: 'functions' },
    { type: 'average_threshold', sourceAttribute: 'pwd_count', direction: 'lte', tagName: 'password', targetAttribute: 'functions' },
    { type: 'average_threshold', sourceAttribute: 'distance_km', direction: 'gte', tagName: 'distance', targetAttribute: 'functions' },
    { type: 'fixed_threshold', sourceAttribute: 'male_count', threshold: 2, direction: 'gt', tagName: 'male', targetAttribute: 'functions' },
  ]
  ```

  Rules (identical to V1 Parity):
  ```js
  [
    { DisplayName: 'Name Length Penalty', AttributeName: 'teamName', Operator: 6, CompareValue: '10', SpeedDelta: -15, Duration: 8, Weather: 0, AllowRepeat: true },
    { DisplayName: 'Color Boost (Blue)', AttributeName: 'colorIndex', Operator: 0, CompareValue: '3', SpeedDelta: 20, Duration: 8, Weather: 0, AllowRepeat: true },
    { DisplayName: 'Color Penalty (Red)', AttributeName: 'colorIndex', Operator: 0, CompareValue: '2', SpeedDelta: -15, Duration: 8, Weather: 0, AllowRepeat: true },
    { DisplayName: 'Function Boost (Password)', AttributeName: 'functions', Operator: 2, CompareValue: 'password', SpeedDelta: 15, Duration: 6, Weather: 0, AllowRepeat: true },
    { DisplayName: 'Function Penalty (Face Recog)', AttributeName: 'functions', Operator: 2, CompareValue: 'facerecog', SpeedDelta: -15, Duration: 8, Weather: 0, AllowRepeat: true },
    { DisplayName: 'Snow Weather', AttributeName: '', Operator: 8, CompareValue: '', SpeedDelta: -8, Duration: 12, Weather: 1, AllowRepeat: true },
    { DisplayName: 'Night Weather', AttributeName: '', Operator: 8, CompareValue: '', SpeedDelta: -5, Duration: 15, Weather: 2, AllowRepeat: true },
  ]
  ```
- **MIRROR**: TEMPLATE_STRUCTURE
- **IMPORTS**: None new
- **GOTCHA**: The `team_name` question duplicates the web-app's built-in teamName form field. Students type team name in the header AND answer Q1. Solution: use `team_name` question answer to override the header team name at export time (or keep them synced via the student survey page). Simplest: just don't duplicate — remove `team_name` from survey questions and rely on the header field. BUT the professor wants the export to match input.xlsx which has this column. Decision: Keep Q1 in the survey for completeness; at export time, use the response's team_name field (from header) as the team identity, and store Q1's answer as a raw response for Excel export.
- **VALIDATE**: `GET /api/templates` returns 4 templates including "ENGG*1100 Survey" with all 14 questions.

### Task 3: Update templates route to include post_processing

- **ACTION**: Include `post_processing` in the template API response
- **IMPLEMENT**: In `web-app/src/routes/templates.js`, add `postProcessing: JSON.parse(r.post_processing_json || '[]')` to the config object
- **MIRROR**: Existing pattern in templates.js:11-15
- **IMPORTS**: None
- **GOTCHA**: Old template rows may not have `post_processing_json` column yet — handle with `|| '[]'` fallback
- **VALIDATE**: `GET /api/templates` includes `postProcessing` array in each template's config

### Task 4: Update survey create/read/update to handle post_processing

- **ACTION**: Thread `postProcessing` through create, read, and update endpoints
- **IMPLEMENT**:
  1. `surveys.js POST /`: accept `postProcessing` in body, store as `post_processing_json`
  2. `surveys.js GET /:id`: include `postProcessing: JSON.parse(survey.post_processing_json || '[]')` in response
  3. `surveys.js PUT /:id`: update `post_processing_json`
  4. `export.js POST /import-config`: handle `PostProcessing` from Unity config JSON
- **MIRROR**: Existing questions_json / mappings_json / rules_json handling throughout
- **IMPORTS**: None
- **GOTCHA**: Must also update `DashboardPage.jsx handleCreateFromTemplate` to pass `postProcessing` when creating from template
- **VALIDATE**: Create survey from ENGG*1100 template, verify postProcessing is stored and returned

### Task 5: Implement aggregate post-processing in export pipeline

- **ACTION**: Add server-side logic to compute average-threshold tags during export
- **IMPLEMENT**: In `web-app/src/routes/export.js`, add new function `applyPostProcessing(carDataArray, postProcessing)`:
  ```js
  function applyPostProcessing(carDataArray, postProcessing) {
    if (!postProcessing || postProcessing.length === 0) return carDataArray;

    // Step 1: Compute averages for each sourceAttribute across all cars
    const averages = {};
    for (const rule of postProcessing) {
      if (rule.type !== 'average_threshold') continue;
      const values = carDataArray.map(car => {
        const attr = car.attributes.find(a => a.key === rule.sourceAttribute);
        return attr ? parseFloat(attr.value) : 0;
      }).filter(v => !isNaN(v));
      averages[rule.sourceAttribute] = values.length > 0
        ? values.reduce((a, b) => a + b, 0) / values.length
        : 0;
    }

    // Step 2: Apply threshold rules to each car
    return carDataArray.map(car => {
      const tags = {}; // targetAttribute -> tag[]

      for (const rule of postProcessing) {
        const attr = car.attributes.find(a => a.key === rule.sourceAttribute);
        const value = attr ? parseFloat(attr.value) : 0;
        let passes = false;

        if (rule.type === 'average_threshold') {
          const avg = averages[rule.sourceAttribute] || 0;
          passes = rule.direction === 'gte' ? value >= avg
                 : rule.direction === 'lte' ? value <= avg
                 : false;
        } else if (rule.type === 'fixed_threshold') {
          const threshold = parseFloat(rule.threshold) || 0;
          passes = rule.direction === 'gt'  ? value > threshold
                 : rule.direction === 'gte' ? value >= threshold
                 : rule.direction === 'lt'  ? value < threshold
                 : rule.direction === 'lte' ? value <= threshold
                 : false;
        }

        if (passes) {
          if (!tags[rule.targetAttribute]) tags[rule.targetAttribute] = [];
          tags[rule.targetAttribute].push(rule.tagName);
        }
      }

      // Merge tag arrays into attributes (e.g., "facerecog/glasses/male")
      const newAttributes = [...car.attributes];
      for (const [attrName, tagArray] of Object.entries(tags)) {
        newAttributes.push({ key: attrName, value: tagArray.join('/') });
      }

      return { ...car, attributes: newAttributes };
    });
  }
  ```
  Then update both `GET /:id/export` and `POST /:id/send-to-game` to call this after mapping:
  ```js
  const postProcessing = JSON.parse(survey.post_processing_json || '[]');
  let carData = responses.map(r => { ... });
  carData = applyPostProcessing(carData, postProcessing);
  ```
- **MIRROR**: EXPORT_TRANSFORM, MAP_RESPONSES_TO_CARDATA
- **IMPORTS**: None
- **GOTCHA**: `pwd_count` uses `lte` direction (reversed from others — lower password strength = gets the tag). This matches DataTool.py line 148: `input_df["pwd_tag"] = input_df["avg_pwd"] >= input_df.iloc[:,14]` which means pwd_tag is True when avg >= individual value, i.e., individual <= avg.
- **VALIDATE**: With test data matching input.xlsx, verify that the exported `functions` string for each team matches `vehicleGroupData.csv`.

### Task 6: Add Excel export endpoint

- **ACTION**: New endpoint `GET /api/surveys/:id/export-excel` that generates xlsx matching input.xlsx column structure
- **IMPLEMENT**:
  1. Add `xlsx` (SheetJS) dependency: `npm install xlsx` in web-app/
  2. New endpoint in `export.js`:
  ```js
  router.get('/:id/export-excel', requireAuth, (req, res) => {
    // Fetch survey + responses
    // Build worksheet with columns matching input.xlsx:
    // ID, Start time, Completion time, Email, Name, Last modified time,
    // [Q1 through Q14 full question text as headers]
    // Each row = one response
    // Send as .xlsx download
  });
  ```
  3. Column mapping:
     - Col 0: ID (response row number)
     - Col 1: Start time (submitted_at)
     - Col 2: Completion time (submitted_at)
     - Col 3: Email
     - Col 4: Name (blank, matches MS Forms)
     - Col 5: Last modified time (blank)
     - Col 6-19: Each question's full text as header, answer as value
     - For Q14 (multi-select): join selected values with semicolons (matching MS Forms format)
- **MIRROR**: Existing export endpoint pattern
- **IMPORTS**: `import XLSX from 'xlsx'`
- **GOTCHA**: Must set proper Content-Type and Content-Disposition headers for xlsx download. SheetJS `write` returns a Buffer in 'buffer' mode.
- **VALIDATE**: Export Excel, open in Excel/Numbers, verify column structure matches input.xlsx.

### Task 7: Add CSV export endpoint (vehicleGroupData.csv format)

- **ACTION**: New endpoint `GET /api/surveys/:id/export-csv` that generates CSV in vehicleGroupData format
- **IMPLEMENT**: In `export.js`, add:
  ```js
  router.get('/:id/export-csv', requireAuth, (req, res) => {
    // Same pipeline as JSON export (mappings + post-processing)
    // Output: teamName,colorIndex,functions (no header, matches vehicleGroupData.csv)
    const csv = carData.map(car => {
      const colorIndex = car.attributes.find(a => a.key === 'colorIndex')?.value || '0';
      const functions = car.attributes.find(a => a.key === 'functions')?.value || '';
      return `${car.teamName},${colorIndex},${functions}`;
    }).join('\n');
    res.setHeader('Content-Type', 'text/csv');
    res.setHeader('Content-Disposition', `attachment; filename="vehicleGroupData.csv"`);
    res.send(csv);
  });
  ```
- **MIRROR**: Existing export endpoint
- **IMPORTS**: None
- **GOTCHA**: CSV has no header row — matches original vehicleGroupData.csv format exactly
- **VALIDATE**: Compare output against reference `vehicleGroupData.csv` using same input data.

### Task 8: Add MultiSelect question type support

- **ACTION**: Support `Type: 3` (MultiSelect) in both frontend and backend
- **IMPLEMENT**:
  1. `web-app/client/src/constants.js`: Add `MultiSelect: 3` to QuestionType
  2. `web-app/client/src/surveyjs-config.js`:
     - In `unityQuestionsToSurveyJS`: add case for `QuestionType.MultiSelect` → SurveyJS `checkbox` type with `maxSelectedChoices` from `MaxValue`
     - `surveyJSToUnityQuestions` already handles `checkbox` (line 54) — just ensure it sets Type to 3
  3. `web-app/client/src/components/QuestionsTab.jsx`: Add `'checkbox'` to `questionTypes` in creatorOptions
  4. `Assets/Scripts/Data/SurveyQuestion.cs`: Add `MultiSelect = 3` to QuestionType enum
- **MIRROR**: SURVEYJS_QUESTION_TYPES
- **IMPORTS**: None
- **GOTCHA**: SurveyJS stores checkbox answers as arrays. When submitting, the answer for Q14 will be `["Camera Vision", "Lane-Keep Assist", "Full-self driving"]`. The Excel export should join these with semicolons to match MS Forms format.
- **VALIDATE**: Create survey with Q14, fill as student, verify multi-select works and answer is stored as array.

### Task 9: Update frontend to support post_processing and new exports

- **ACTION**: Thread postProcessing through dashboard create flow; add Excel/CSV export buttons to editor
- **IMPLEMENT**:
  1. `web-app/client/src/api.js`: Add functions:
     ```js
     export async function exportExcel(surveyId) { /* GET with blob response */ }
     export async function exportCsv(surveyId) { /* GET with blob response */ }
     ```
  2. `web-app/client/src/pages/DashboardPage.jsx`: In `handleCreateFromTemplate`, add `postProcessing: template.config.postProcessing || []` to the createSurvey call
  3. `web-app/client/src/pages/EditorPage.jsx`: Add "Export Excel" and "Export CSV" buttons next to existing "Export for Unity":
     ```jsx
     <button onClick={handleExportExcel} className="btn-secondary">Export Excel</button>
     <button onClick={handleExportCsv} className="btn-secondary">Export CSV</button>
     ```
  4. Add download handlers that fetch blob and trigger download
- **MIRROR**: Existing `handleExport` / `downloadExportJson` pattern in EditorPage.jsx
- **IMPORTS**: None
- **GOTCHA**: Excel/CSV downloads must use `fetch` with blob response type, not the standard JSON API helper. Create download via `URL.createObjectURL`.
- **VALIDATE**: Click "Export Excel" and "Export CSV" buttons, verify files download correctly.

### Task 10: Update survey create/update endpoints for postProcessing

- **ACTION**: Persist `postProcessing` in surveys.post_processing_json
- **IMPLEMENT**:
  1. `surveys.js POST /`: Add `postProcessing` to destructured body, store as `JSON.stringify(postProcessing || [])`
  2. `surveys.js GET /:id`: Add `postProcessing: JSON.parse(survey.post_processing_json || '[]')` to response
  3. `surveys.js PUT /:id`: Add `post_processing_json` to UPDATE SET clause
  4. Update SQL statements to include the new column
- **MIRROR**: Existing `questions_json`/`mappings_json`/`rules_json` pattern
- **IMPORTS**: None
- **GOTCHA**: Must also update `GET /` (list) if we want postProcessing in list view — but probably not needed for list, only for detail.
- **VALIDATE**: Create from template, verify postProcessing persists through create → read → update cycle.

### Task 11: Add Unity-side ENGG*1100 template (C# sync)

- **ACTION**: Add matching template in Unity's SurveyTemplates.cs
- **IMPLEMENT**: Add `ENGG1100Survey()` method returning SurveyConfig with same questions, mappings, and rules. Add to `TemplateNames` array and `GetTemplate` switch.
- **MIRROR**: Existing templates in SurveyTemplates.cs
- **IMPORTS**: None (all types already exist)
- **GOTCHA**: Must add `MultiSelect = 3` to `QuestionType` enum in `SurveyQuestion.cs`. Unity's JsonUtility serializes enums as ints, so adding value 3 is safe.
- **VALIDATE**: Open Unity, verify SurveyTemplates.GetTemplate("ENGG*1100 Survey") returns valid config.

### Task 12: Install xlsx dependency

- **ACTION**: Add SheetJS library to web-app
- **IMPLEMENT**: Run `cd web-app && npm install xlsx`
- **MIRROR**: N/A
- **IMPORTS**: `import XLSX from 'xlsx'` in export.js
- **GOTCHA**: SheetJS community edition is `xlsx` on npm. Use `XLSX.utils.json_to_sheet` and `XLSX.write` for generation.
- **VALIDATE**: `node -e "import('xlsx').then(m => console.log('OK'))"` succeeds

---

## Testing Strategy

### Unit Tests

| Test | Input | Expected Output | Edge Case? |
|---|---|---|---|
| applyPostProcessing avg_gte | 3 cars with facial_count [3, 4, 5], avg=4 | cars 1,2 get "facerecog" tag | No |
| applyPostProcessing avg_lte (pwd) | 3 cars with pwd_count [2, 4, 6], avg=4 | cars 0,1 get "password" tag | No |
| applyPostProcessing fixed_gt (male) | 3 cars with male_count [1, 3, 5] | cars 1,2 get "male" tag | No |
| applyPostProcessing empty | 0 cars, any rules | empty array | Yes |
| applyPostProcessing no rules | 3 cars, empty postProcessing | unchanged carData | Yes |
| Excel export column count | 14 questions | 20 columns (6 meta + 14 questions) | No |
| CSV format match | Reference input data | matches vehicleGroupData.csv | No |
| MultiSelect serialization | ["A", "B", "C"] answer | "A;B;C" in Excel | No |
| Color lookup | "Green" | "0"; "Blue" | "3" | No |

### Edge Cases Checklist
- [x] Empty responses (0 teams) — export returns empty file with headers
- [x] Single response — averages equal the single value, all gte tags match
- [x] Missing numeric answer — use DefaultValue (0)
- [x] Team name with special characters — pass through as-is
- [x] Q14 with 0 selections — empty array, empty in Excel
- [x] Q14 with >3 selections — SurveyJS `maxSelectedChoices` enforces limit

---

## Validation Commands

### Install Dependencies
```bash
cd web-app && npm install xlsx
```
EXPECT: xlsx added to package.json dependencies

### Start Web App
```bash
cd web-app && npm run dev
```
EXPECT: Server starts, "[DB] Seeded default templates" if fresh DB

### Verify Templates API
```bash
curl http://localhost:3001/api/templates | python3 -m json.tool
```
EXPECT: 4 templates including "ENGG*1100 Survey" with 14 questions

### Verify Template Questions
```bash
curl http://localhost:3001/api/templates | python3 -c "
import sys, json
data = json.load(sys.stdin)
t = [t for t in data['data'] if t['name'] == 'ENGG*1100 Survey'][0]
print(f'Questions: {len(t[\"config\"][\"questions\"])}')
print(f'Mappings: {len(t[\"config\"][\"mappings\"])}')
print(f'Rules: {len(t[\"config\"][\"rules\"])}')
print(f'PostProcessing: {len(t[\"config\"].get(\"postProcessing\", []))}')
"
```
EXPECT: Questions: 14, Mappings: 7, Rules: 7, PostProcessing: 6

### Manual Validation
- [ ] Create survey from ENGG*1100 template on Dashboard
- [ ] Verify all 14 questions appear in Questions tab (SurveyJS editor)
- [ ] Open student survey link, fill in all questions including Q14 multi-select
- [ ] Submit 3+ responses with varied data
- [ ] Click "Export Excel" — verify xlsx has correct columns matching input.xlsx
- [ ] Click "Export CSV" — verify CSV matches vehicleGroupData.csv format
- [ ] Click "Export for Unity" — verify JSON has carData with colorIndex and functions
- [ ] Click "Send to Game" — verify Unity receives correct car data
- [ ] Verify functions string contains correct tags based on average comparison

---

## Acceptance Criteria
- [ ] ENGG*1100 Survey template appears on Dashboard as a template option
- [ ] All 14 questions match MS Forms (text, options, types)
- [ ] Q14 renders as multi-select (checkbox) with max 3 selections
- [ ] Color question maps to colorIndex (Green=0, Black=1, Red=2, Blue=3, White=4)
- [ ] Average-threshold algorithm produces same functions as DataTool.py for equivalent input
- [ ] Excel export has identical column structure to input.xlsx
- [ ] CSV export matches vehicleGroupData.csv format (name,colorIndex,functions)
- [ ] JSON export includes carData with attributes including colorIndex and functions
- [ ] 7 V1 event rules are pre-configured in the template
- [ ] Rules auto-assign keyboard trigger keys (Digit1-Digit7) when loaded in Unity

## Completion Checklist
- [ ] Code follows discovered patterns (template structure, export pipeline, migration pattern)
- [ ] Error handling matches codebase style (try/catch for DB, validation for API inputs)
- [ ] No hardcoded values (color mapping in template config, not in code)
- [ ] Tests cover aggregate processing edge cases
- [ ] Unity C# template stays in sync with web-app seed template
- [ ] No unnecessary scope additions
- [ ] Self-contained — no questions needed during implementation

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Existing DB won't get new template (seeding guard) | HIGH | MEDIUM | Add migration that inserts template if not exists |
| SheetJS bundle size bloats client | LOW | LOW | xlsx is server-side only (in routes/export.js) |
| Average-threshold diverges from DataTool.py | MEDIUM | HIGH | Write regression test with reference input.xlsx data |
| postProcessing schema not supported by Unity import | MEDIUM | LOW | Unity ignores unknown JSON fields; postProcessing is web-only |
| Q1 team_name vs header teamName duplication | LOW | LOW | Document that header teamName is authoritative for game |

## Notes
- The `team_name` question (Q1) and the web-app's built-in team name header field serve the same purpose. In the student survey page, the header team name is used as the response's `team_name` in the DB. Q1's answer is stored inside `answers_json` for the Excel export but is NOT used as the car's team identity. This avoids breaking the existing response submission flow.
- The `post_processing_json` column is a web-app-only concept. Unity does not need to know about it — the aggregate processing happens server-side before export. When Unity receives the JSON, it already has the computed `functions` attribute.
- Event rules in the template do NOT include `TriggerKey` — that's a Unity runtime concern. When Unity loads rules from JSON, `SavedEventRule.ToRule(Key triggerKey)` assigns keys sequentially (Digit1-Digit7).
- Columns 8 (member_count), 9 (member_names), 16 (vehicle_type), 17 (entertainment), 18 (driving_experience), 19 (car_features) are collected for the professor's analysis only. They appear in the Excel export but are NOT mapped to car attributes for the game.
