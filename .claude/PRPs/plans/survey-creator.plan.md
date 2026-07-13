# Plan: Survey Creator (Phase 2)

## Summary

Build a React frontend with SurveyJS Creator for the Questions tab, plus custom Mapping and Rules editor components. The professor can create, edit, save, and load complete SurveyConfig (questions + attribute mappings + event rules) via the existing Express API. This replaces Unity's `SurveyBuilderPanel` (503 lines of C# UI code) with a modern web interface.

## User Story

As a professor,
I want to create and edit survey configurations (questions, attribute mappings, and event rules) in a web browser,
So that I don't need to use Unity's slow WebGL interface to configure the EDI racing game.

## Problem -> Solution

**Current state**: Professor must load Unity WebGL, navigate to SurveyBuilderPanel, and manually create questions/mappings/rules using Unity's legacy UI system (InputFields, Dropdowns, Toggles). The UI is slow, non-responsive, and requires a full Unity rebuild for any UI change.

**Desired state**: Professor logs into the Web App, sees a modern tabbed editor with SurveyJS Creator for questions, and custom React forms for mappings and rules. Changes auto-save to the API. The exported JSON matches the Unity `SurveyConfig` schema exactly.

## Metadata

- **Complexity**: Large
- **Source PRD**: `.claude/PRPs/prds/edi-survey-web-app.prd.md`
- **PRD Phase**: Phase 2 — 问卷创建器
- **Estimated Files**: ~20

---

## UX Design

### Before

```
Professor opens Unity WebGL (30s+ load) ->
  Setup Screen -> "Create Survey Config" ->
    SurveyBuilderPanel (dark overlay, 3 tabs):
      [Questions] [Mappings] [Rules]
      Add Question -> fill text, type dropdown, options
      Add Mapping  -> question dropdown -> attribute name
      Add Rule     -> display name, operator, speed, etc.
    -> Save -> back to Setup Screen
```

### After

```
Professor opens Web App (instant) ->
  Login -> Dashboard (list of surveys) ->
    "New Survey" or click existing ->
      Tabbed Editor:
        [Questions] SurveyJS Creator (drag-and-drop, visual)
        [Mappings]  Custom table: question -> attribute + transform
        [Rules]     Custom form: condition + effect + weather
      -> Auto-save to API -> back to Dashboard
```

### Interaction Changes

| Touchpoint | Before | After | Notes |
|---|---|---|---|
| Access | Unity WebGL (slow) | Browser URL (instant) | No WebGL load |
| Question editing | InputField + Dropdown | SurveyJS drag-and-drop | Visual WYSIWYG |
| Question types | Manual type selection | Toolbox with Text/MC/Numeric | Constrained to 3 types |
| Mapping editor | Dropdown + InputField | Table with dropdowns | Auto-links to questions |
| Rule editor | Multiple InputFields | Structured form | Dropdown enums |
| Save | Manual "Save" button | Auto-save on change | `saveSurveyFunc` |
| Templates | Via SetupScreen dropdown | "Start from Template" button | Pre-populated configs |

---

## Mandatory Reading

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 | `web-app/src/routes/surveys.js` | all | API endpoints we call from React |
| P0 | `web-app/src/middleware/auth.js` | all | Token-based auth pattern |
| P0 | `web-app/src/schema.sql` | all | DB schema: surveys table structure |
| P0 | `Assets/Scripts/Data/SurveyConfig.cs` | all | Target data model |
| P0 | `Assets/Scripts/Data/SurveyQuestion.cs` | all | QuestionType enum + struct fields |
| P0 | `Assets/Scripts/Data/AttributeMapping.cs` | all | Mapping struct with TransformType + LookupEntries |
| P0 | `Assets/Scripts/Data/SessionData.cs` | 67-109 | SavedEventRule struct fields |
| P1 | `Assets/Scripts/Events/ComparisonOperator.cs` | all | 9 operator values for rule conditions |
| P1 | `Assets/Scripts/Events/WeatherType.cs` | all | 4 weather values: None, Snow, Night, Sunset |
| P1 | `Assets/Scripts/Data/SurveyTemplates.cs` | all | 3 templates to seed in Web App |
| P1 | `Assets/Scripts/UI/SurveyBuilderPanel.cs` | all | Existing Unity UI to replicate feature-parity |
| P2 | `Assets/Scripts/UI/RuleEditorRow.cs` | all | Rule editor fields reference |
| P2 | `Assets/Scripts/UI/MappingEditorRow.cs` | all | Mapping editor fields reference |
| P2 | `Assets/Scripts/UI/QuestionEditorRow.cs` | all | Question editor fields reference |
| P2 | `web-app/src/index.js` | all | Express app structure |
| P2 | `web-app/package.json` | all | Current dependencies |

## External Documentation

| Topic | Source | Key Takeaway |
|---|---|---|
| SurveyJS Creator React setup | surveyjs/survey-creator docs | `SurveyCreatorComponent` + `SurveyCreator` class, `saveSurveyFunc` for auto-save, `creator.JSON` to get schema |
| SurveyJS question types | surveyjs/survey-creator docs | Limit toolbox via `questionTypes: ["text", "checkbox", "radiogroup", "dropdown", "rating"]` |
| SurveyJS Runner React | surveyjs/survey-library docs | `Model` + `Survey` component for Phase 3 (not this phase) |
| Vite React setup | Vite docs | `npm create vite@latest client -- --template react` for fast React dev |

---

## Patterns to Mirror

### API_RESPONSE_FORMAT
```javascript
// SOURCE: web-app/src/routes/surveys.js:29-37
res.json({
  success: true,
  data: {
    ...survey,
    questions: JSON.parse(survey.questions_json),
    mappings: JSON.parse(survey.mappings_json),
    rules: JSON.parse(survey.rules_json),
  }
});
```

### AUTH_HEADER_PATTERN
```javascript
// SOURCE: web-app/src/middleware/auth.js:16-27
const header = req.headers.authorization;
if (!header || !header.startsWith('Bearer ')) {
  return res.status(401).json({ success: false, error: 'Authentication required' });
}
const token = header.slice(7);
```

### ERROR_HANDLING
```javascript
// SOURCE: web-app/src/index.js:28-31
app.use((err, req, res, _next) => {
  console.error('[API] Error:', err.message);
  res.status(500).json({ success: false, error: 'Internal server error' });
});
```

### SURVEY_CREATE_PAYLOAD
```javascript
// SOURCE: web-app/src/routes/surveys.js:42-60
const { configName, description, questions, mappings, rules } = req.body;
// questions: SurveyJS JSON schema (pages/elements array)
// mappings: AttributeMapping[] as JSON
// rules: SavedEventRule[] as JSON
```

### UNITY_DATA_SCHEMA
```csharp
// SOURCE: Assets/Scripts/Data/SurveyConfig.cs:1-18
// SurveyConfig { ConfigName, Description, CreatedAt, Version, Questions[], Mappings[], Rules[] }

// SOURCE: Assets/Scripts/Data/SurveyQuestion.cs:19-33
// SurveyQuestion { Id, Text, Type(int), Options[], MinValue, MaxValue, Required }
// QuestionType: Text=0, MultipleChoice=1, Numeric=2

// SOURCE: Assets/Scripts/Data/AttributeMapping.cs:11-24
// AttributeMapping { QuestionId, AttributeName, DefaultValue, TransformType, LookupEntries[] }
// TransformType: "direct" | "lookup" | "numeric"

// SOURCE: Assets/Scripts/Data/SessionData.cs:67-109
// SavedEventRule { DisplayName, AttributeName, Operator(int), CompareValue, SpeedDelta, Duration, Weather(int), AllowRepeat }

// SOURCE: Assets/Scripts/Events/ComparisonOperator.cs
// 0=Equals, 1=NotEquals, 2=Contains, 3=NotContains, 4=GreaterThan, 5=LessThan, 6=LengthGreaterThan, 7=LengthLessThan, 8=All

// SOURCE: Assets/Scripts/Events/WeatherType.cs
// 0=None, 1=Snow, 2=Night, 3=Sunset
```

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `web-app/client/package.json` | CREATE | React (Vite) frontend project |
| `web-app/client/vite.config.js` | CREATE | Vite config with API proxy to :3001 |
| `web-app/client/index.html` | CREATE | Vite entry HTML |
| `web-app/client/src/main.jsx` | CREATE | React app entry point |
| `web-app/client/src/App.jsx` | CREATE | Router: Login, Dashboard, Editor |
| `web-app/client/src/api.js` | CREATE | API client with auth token management |
| `web-app/client/src/pages/LoginPage.jsx` | CREATE | Email + password login/register |
| `web-app/client/src/pages/DashboardPage.jsx` | CREATE | Survey list + create/delete |
| `web-app/client/src/pages/EditorPage.jsx` | CREATE | Tabbed editor: Questions/Mappings/Rules |
| `web-app/client/src/components/QuestionsTab.jsx` | CREATE | SurveyJS Creator wrapper |
| `web-app/client/src/components/MappingsTab.jsx` | CREATE | Custom mapping editor table |
| `web-app/client/src/components/RulesTab.jsx` | CREATE | Custom rule editor form |
| `web-app/client/src/components/MappingRow.jsx` | CREATE | Single mapping row component |
| `web-app/client/src/components/RuleRow.jsx` | CREATE | Single rule row component |
| `web-app/client/src/constants.js` | CREATE | ComparisonOperator, WeatherType, TransformType enums |
| `web-app/client/src/surveyjs-config.js` | CREATE | SurveyJS → Unity question format converter |
| `web-app/src/routes/surveys.js` | UPDATE | Add `GET /api/surveys/:id/responses/count` endpoint |
| `web-app/src/routes/templates.js` | CREATE | `GET /api/templates` — seed templates from Unity |
| `web-app/src/index.js` | UPDATE | Mount templates route + serve static client build |
| `web-app/package.json` | UPDATE | Add build script for client |

## NOT Building

- Student answer page (Phase 3)
- JSON export with CarData mapping (Phase 4)
- Template migration/seed script (Phase 5)
- SurveyJS Theme Editor (unnecessary complexity)
- Image upload in surveys (not needed for EDI)
- Mobile-specific professor UI (desktop-first for professors)
- Real-time collaboration (single professor per survey)

---

## Step-by-Step Tasks

### Task 1: Initialize React (Vite) Project

- **ACTION**: Create `web-app/client/` as a Vite React project
- **IMPLEMENT**:
  ```bash
  cd web-app && npm create vite@latest client -- --template react
  cd client && npm install
  npm install survey-core survey-creator-core survey-creator-react survey-react-ui react-router-dom
  ```
- **MIRROR**: N/A (new project)
- **IMPORTS**: `react`, `react-dom`, `react-router-dom`, `survey-creator-react`, `survey-core`
- **GOTCHA**: SurveyJS requires CSS imports: `survey-core/survey-core.css` and `survey-creator-core/survey-creator-core.css`. These must be imported in the component, not globally, to avoid SSR issues (not applicable here since no SSR, but good practice).
- **VALIDATE**: `cd web-app/client && npm run dev` opens Vite dev server

### Task 2: Configure Vite Proxy

- **ACTION**: Set up `vite.config.js` to proxy `/api` requests to Express backend on port 3001
- **IMPLEMENT**:
  ```javascript
  import { defineConfig } from 'vite'
  import react from '@vitejs/plugin-react'

  export default defineConfig({
    plugins: [react()],
    server: {
      port: 5173,
      proxy: {
        '/api': {
          target: 'http://localhost:3001',
          changeOrigin: true
        }
      }
    }
  })
  ```
- **MIRROR**: N/A
- **IMPORTS**: `@vitejs/plugin-react`
- **GOTCHA**: Proxy only works in dev mode. Production will serve client build via Express static middleware.
- **VALIDATE**: `fetch('/api/health')` from browser returns `{"success":true}`

### Task 3: Create API Client Module

- **ACTION**: Create `api.js` with fetch wrappers and token management
- **IMPLEMENT**:
  - Store auth token in `localStorage`
  - Export functions: `login(email, password)`, `register(email, password, displayName)`, `logout()`, `getSurveys()`, `getSurvey(id)`, `createSurvey(data)`, `updateSurvey(id, data)`, `deleteSurvey(id)`, `getTemplates()`
  - All functions return `{ success, data?, error? }` matching API_RESPONSE_FORMAT
  - Auto-attach `Authorization: Bearer <token>` header
- **MIRROR**: AUTH_HEADER_PATTERN, API_RESPONSE_FORMAT
- **IMPORTS**: None (native `fetch`)
- **GOTCHA**: Handle 401 responses by clearing token and redirecting to login
- **VALIDATE**: Can call `login()` and `getSurveys()` successfully from browser console

### Task 4: Create Constants Module

- **ACTION**: Create `constants.js` with enum values matching Unity C# enums
- **IMPLEMENT**:
  ```javascript
  export const QuestionType = { Text: 0, MultipleChoice: 1, Numeric: 2 };
  export const ComparisonOperator = {
    Equals: 0, NotEquals: 1, Contains: 2, NotContains: 3,
    GreaterThan: 4, LessThan: 5, LengthGreaterThan: 6, LengthLessThan: 7, All: 8
  };
  export const ComparisonOperatorLabels = [
    'Equals', 'Not Equals', 'Contains', 'Not Contains',
    'Greater Than', 'Less Than', 'Length >', 'Length <', 'All (global)'
  ];
  export const WeatherType = { None: 0, Snow: 1, Night: 2, Sunset: 3 };
  export const WeatherTypeLabels = ['None', 'Snow', 'Night', 'Sunset'];
  export const TransformTypes = ['direct', 'lookup', 'numeric'];
  ```
- **MIRROR**: UNITY_DATA_SCHEMA
- **IMPORTS**: None
- **GOTCHA**: Integer values MUST match C# enum order exactly. Verify against `ComparisonOperator.cs` and `WeatherType.cs`.
- **VALIDATE**: `ComparisonOperator.All === 8` (matches C# enum)

### Task 5: Create SurveyJS <-> Unity Converter

- **ACTION**: Create `surveyjs-config.js` to convert between SurveyJS JSON schema and Unity's `SurveyQuestion[]` format
- **IMPLEMENT**:
  - `unityQuestionsToSurveyJS(questions)` — converts `SurveyQuestion[]` to SurveyJS pages/elements JSON
    - `Text` -> `{ type: "text" }`
    - `MultipleChoice` -> `{ type: "radiogroup", choices: options }`
    - `Numeric` -> `{ type: "text", inputType: "number", min, max }` or `{ type: "rating", rateMin, rateMax }`
    - Map `Id` -> `name`, `Text` -> `title`, `Required` -> `isRequired`
  - `surveyJSToUnityQuestions(surveyJSON)` — converts SurveyJS JSON back to `SurveyQuestion[]`
    - Flatten `pages[].elements[]` into flat array
    - Map `name` -> `Id`, `title` -> `Text`, `isRequired` -> `Required`
    - Detect type: `text` -> Text(0), `radiogroup`/`checkbox`/`dropdown` -> MultipleChoice(1), inputType=number -> Numeric(2)
    - Extract `choices` -> `Options`, `min`/`max` -> `MinValue`/`MaxValue`
- **MIRROR**: UNITY_DATA_SCHEMA (SurveyQuestion struct)
- **IMPORTS**: None
- **GOTCHA**: SurveyJS uses `name` as unique ID (matches Unity's `SurveyQuestion.Id`). Must preserve these across conversions. SurveyJS `radiogroup` choices can be strings or `{value, text}` objects — handle both.
- **VALIDATE**: Round-trip test: `surveyJSToUnityQuestions(unityQuestionsToSurveyJS(accessibilityTemplate.Questions))` produces identical output

### Task 6: Create App Shell with Router

- **ACTION**: Create `App.jsx` with React Router for Login, Dashboard, and Editor pages
- **IMPLEMENT**:
  - Routes: `/login`, `/dashboard`, `/surveys/:id`
  - Auth guard: redirect to `/login` if no token in localStorage
  - Simple layout: header with app name + logout button
- **MIRROR**: N/A
- **IMPORTS**: `react-router-dom` (BrowserRouter, Routes, Route, Navigate)
- **GOTCHA**: Use `HashRouter` instead of `BrowserRouter` to avoid issues when served from Express static. Or configure Express to serve `index.html` for all non-API routes.
- **VALIDATE**: Navigate between `/login` and `/dashboard` without errors

### Task 7: Create Login Page

- **ACTION**: Create `LoginPage.jsx` with email/password form, login/register toggle
- **IMPLEMENT**:
  - Two modes: Login and Register (toggle with link)
  - Register mode shows additional "Display Name" field
  - On success: store token in localStorage, redirect to `/dashboard`
  - On error: show error message
  - Minimal styling (CSS module or inline)
- **MIRROR**: API_RESPONSE_FORMAT
- **IMPORTS**: `api.js` (login, register)
- **GOTCHA**: Backend requires password >= 6 chars. Show validation message.
- **VALIDATE**: Can register new user, login, and reach dashboard

### Task 8: Create Dashboard Page

- **ACTION**: Create `DashboardPage.jsx` showing list of surveys with create/delete actions
- **IMPLEMENT**:
  - Fetch surveys on mount via `getSurveys()`
  - Display as card grid: config_name, description, share_code, updated_at
  - "New Survey" button -> `createSurvey({ configName: "Untitled Survey" })` -> navigate to editor
  - "Start from Template" button -> dropdown of template names -> create from template
  - Delete button on each card (with confirmation)
  - Click card -> navigate to `/surveys/:id`
- **MIRROR**: API_RESPONSE_FORMAT
- **IMPORTS**: `api.js` (getSurveys, createSurvey, deleteSurvey, getTemplates)
- **GOTCHA**: Share code display is informational only (Phase 3 will use it for student links)
- **VALIDATE**: Create, view, and delete surveys from dashboard

### Task 9: Create Editor Page (Tab Container)

- **ACTION**: Create `EditorPage.jsx` as tabbed container for Questions, Mappings, and Rules
- **IMPLEMENT**:
  - Load survey by ID on mount via `getSurvey(id)`
  - 3 tabs: Questions, Mappings, Rules (active tab state)
  - Header: survey name (editable), "Back to Dashboard" button
  - Auto-save: debounce 2 seconds after any change, call `updateSurvey(id, data)`
  - Save status indicator: "Saved", "Saving...", "Error"
  - Pass survey data down to tab components
  - When tab components call `onChange(field, value)`, merge into survey state and trigger auto-save
- **MIRROR**: SURVEY_CREATE_PAYLOAD
- **IMPORTS**: `api.js` (getSurvey, updateSurvey), tab components
- **GOTCHA**: SurveyJS Creator needs the full JSON on mount. Don't re-create the Creator instance on every re-render — use `useState` with lazy initializer.
- **VALIDATE**: Switch between tabs, edit data, verify auto-save calls API

### Task 10: Create Questions Tab (SurveyJS Creator)

- **ACTION**: Create `QuestionsTab.jsx` wrapping SurveyJS Creator
- **IMPLEMENT**:
  - Initialize `SurveyCreator` with options:
    ```javascript
    const creatorOptions = {
      questionTypes: ["text", "radiogroup", "text"],  // Text, MultipleChoice, Numeric
      showThemeTab: false,
      showLogicTab: false,
      showTranslationTab: false,
      showJSONEditorTab: false,
      autoSaveEnabled: true,
    };
    ```
  - On mount: convert Unity `questions[]` to SurveyJS JSON via `unityQuestionsToSurveyJS()`, set as `creator.JSON`
  - On save (`saveSurveyFunc`): convert back via `surveyJSToUnityQuestions(creator.JSON)`, call parent `onChange('questions', unityQuestions)`
  - Limit toolbox to relevant question types:
    - `text` — for Text and Numeric (with inputType="number")
    - `radiogroup` — for MultipleChoice
    - Optionally add a custom "Numeric Range" toolbox item with pre-configured min/max
- **MIRROR**: SurveyJS Creator React setup (from External Documentation)
- **IMPORTS**: `survey-creator-react`, `survey-core/survey-core.css`, `survey-creator-core/survey-creator-core.css`, `surveyjs-config.js`
- **GOTCHA**:
  1. SurveyJS Creator is a complex component. Only create the `SurveyCreator` instance once (lazy init in useState). Re-creating it on every render causes flicker and data loss.
  2. The conversion from SurveyJS to Unity format must handle: `radiogroup` choices as `string[]` or `{value,text}[]`, and `text` with `inputType: "number"` as Numeric.
  3. SurveyJS uses `name` as element ID — this maps to Unity's `SurveyQuestion.Id`. Must preserve user-defined IDs.
- **VALIDATE**: Add text, multiple choice, and numeric questions in the Creator. Verify `onChange` sends correctly formatted Unity `SurveyQuestion[]`.

### Task 11: Create Mappings Tab

- **ACTION**: Create `MappingsTab.jsx` with a table of AttributeMapping rows
- **IMPLEMENT**:
  - Props: `mappings`, `questions` (for question ID dropdown), `onChange`
  - "Add Mapping" button (max 20)
  - Each row (`MappingRow.jsx`):
    - Question ID dropdown (populated from current questions)
    - "->" arrow label
    - Attribute Name text input
    - Transform Type dropdown: direct, lookup, numeric
    - Default Value text input
    - Lookup entries section (visible only when transform = "lookup"):
      - Key=Value pairs, `|` separated text input or structured list
    - Delete (X) button
  - On any change, call `onChange('mappings', updatedMappings)`
- **MIRROR**: UNITY_DATA_SCHEMA (AttributeMapping struct)
- **IMPORTS**: `constants.js` (TransformTypes)
- **GOTCHA**: Question IDs come from the Questions tab. If a question is deleted, mappings referencing it become orphaned. Show a warning but don't auto-delete (professor might switch tabs).
- **VALIDATE**: Add mapping, select question, set transform type to "lookup", add lookup entries. Verify output matches `AttributeMapping` struct format.

### Task 12: Create Rules Tab

- **ACTION**: Create `RulesTab.jsx` with a list of SavedEventRule editors
- **IMPLEMENT**:
  - Props: `rules`, `onChange`
  - "Add Rule" button (max 9, matching keyboard keys 1-9)
  - Each row (`RuleRow.jsx`):
    - Display Name text input
    - Condition section:
      - "If" label
      - Attribute Name text input
      - Operator dropdown (9 values from ComparisonOperator)
      - Compare Value text input
      - When operator = "All", disable attribute/value fields
    - Effect section:
      - Speed Delta number input (can be negative)
      - Duration (seconds) number input
      - Weather dropdown (4 values from WeatherType)
      - Allow Repeat checkbox
    - Delete (X) button
  - On any change, call `onChange('rules', updatedRules)`
- **MIRROR**: UNITY_DATA_SCHEMA (SavedEventRule struct), RuleEditorRow.cs fields
- **IMPORTS**: `constants.js` (ComparisonOperator, ComparisonOperatorLabels, WeatherType, WeatherTypeLabels)
- **GOTCHA**: Speed delta can be negative (penalty) or positive (boost). Duration and speed are floats. Operator and Weather are stored as integers (enum index), not strings.
- **VALIDATE**: Add rule with all fields, verify output JSON matches `SavedEventRule` struct exactly.

### Task 13: Create Templates API Route

- **ACTION**: Create `web-app/src/routes/templates.js` serving built-in survey templates
- **IMPLEMENT**:
  - `GET /api/templates` — returns list of template names and full configs
  - Hardcode the 3 templates (V1 Parity, Accessibility, Diversity) translated from Unity `SurveyTemplates.cs`
  - Response format: `{ success: true, data: [{ name, description, config: { questions, mappings, rules } }] }`
- **MIRROR**: API_RESPONSE_FORMAT, UNITY_DATA_SCHEMA
- **IMPORTS**: None (pure data)
- **GOTCHA**: Template data must exactly match Unity's `SurveyTemplates.cs` values — same question IDs, same operator integers, same weather integers. Use the constants, not magic numbers.
- **VALIDATE**: `GET /api/templates` returns 3 templates. Each template's rules array matches `SurveyTemplates.cs`.

### Task 14: Create Response Count Endpoint

- **ACTION**: Add `GET /api/surveys/:id/responses/count` to `surveys.js`
- **IMPLEMENT**:
  ```javascript
  router.get('/:id/responses/count', requireAuth, (req, res) => {
    const db = getDb();
    const survey = db.prepare('SELECT id FROM surveys WHERE id = ? AND user_id = ?')
      .get(req.params.id, req.user.userId);
    if (!survey) return res.status(404).json({ success: false, error: 'Survey not found' });
    const count = db.prepare('SELECT COUNT(*) as count FROM responses WHERE survey_id = ?')
      .get(req.params.id);
    res.json({ success: true, data: { count: count.count } });
  });
  ```
- **MIRROR**: API_RESPONSE_FORMAT, AUTH_HEADER_PATTERN
- **IMPORTS**: None
- **GOTCHA**: Must be placed BEFORE the `/:id` route to avoid parameter capture conflict. Actually since `:id/responses/count` has more path segments, Express will route correctly. But verify.
- **VALIDATE**: Create a survey, check count returns 0.

### Task 15: Update Express to Serve Client Build + Mount Templates

- **ACTION**: Update `web-app/src/index.js` to serve static client build and mount templates route
- **IMPLEMENT**:
  - Import and mount `templates.js` route
  - Add static file serving for production:
    ```javascript
    import { existsSync } from 'fs';
    import { join, dirname } from 'path';
    import { fileURLToPath } from 'url';

    const __dirname = dirname(fileURLToPath(import.meta.url));
    const clientDist = join(__dirname, '..', 'client', 'dist');

    // After API routes:
    if (existsSync(clientDist)) {
      app.use(express.static(clientDist));
      app.get('*', (req, res) => {
        res.sendFile(join(clientDist, 'index.html'));
      });
    }
    ```
  - Update `package.json` with `build` script that builds client
- **MIRROR**: ERROR_HANDLING
- **IMPORTS**: `express.static`, `path`, `fs`
- **GOTCHA**: The catch-all `*` route must be AFTER all API routes, otherwise it will intercept API requests. Also, only serve `index.html` for non-API routes.
- **VALIDATE**: `cd web-app/client && npm run build && cd .. && npm start` serves the React app at `http://localhost:3001/`

### Task 16: Update Dockerfile for Client Build

- **ACTION**: Update `web-app/Dockerfile` to include client build step
- **IMPLEMENT**:
  - Multi-stage build:
    ```dockerfile
    # Stage 1: Build client
    FROM node:20-alpine AS client-build
    WORKDIR /app/client
    COPY client/package.json client/package-lock.json* ./
    RUN npm ci
    COPY client/ ./
    RUN npm run build

    # Stage 2: Production
    FROM node:20-alpine
    WORKDIR /app
    COPY package.json package-lock.json* ./
    RUN npm ci --omit=dev
    COPY src/ ./src/
    COPY --from=client-build /app/client/dist ./client/dist
    RUN mkdir -p /app/data
    ENV API_PORT=3001
    ENV DB_PATH=/app/data/edi-survey.db
    EXPOSE 3001
    CMD ["node", "src/index.js"]
    ```
- **MIRROR**: Existing Dockerfile pattern
- **IMPORTS**: N/A
- **GOTCHA**: `client/node_modules/` is large. Multi-stage build keeps production image small. Add `.dockerignore` to exclude client node_modules from COPY context.
- **VALIDATE**: `docker build -t edi-survey .` succeeds. Container serves both API and client.

### Task 17: Add Basic Styling

- **ACTION**: Create minimal CSS for the app shell, login, dashboard, and editor pages
- **IMPLEMENT**:
  - `web-app/client/src/index.css` — global reset, fonts, colors
  - Use CSS modules or a single stylesheet (keep simple for MVP)
  - Color scheme: dark background matching Unity's `new Color(0.08f, 0.08f, 0.12f, 0.95f)` = `#14141f`
  - Clean, functional styling — not fancy, just usable
- **MIRROR**: Unity SurveyBuilderPanel color scheme (dark background, light text)
- **IMPORTS**: N/A
- **GOTCHA**: SurveyJS Creator has its own CSS. Don't override SurveyJS styles. Only style the wrapper/shell.
- **VALIDATE**: App looks presentable in Chrome desktop

---

## Testing Strategy

### Unit Tests

| Test | Input | Expected Output | Edge Case? |
|---|---|---|---|
| `surveyJSToUnityQuestions` — text | SurveyJS `{type:"text", name:"q1", title:"Name?"}` | `{Id:"q1", Text:"Name?", Type:0, Required:false}` | No |
| `surveyJSToUnityQuestions` — radiogroup | SurveyJS `{type:"radiogroup", choices:["A","B"]}` | `{Type:1, Options:["A","B"]}` | No |
| `surveyJSToUnityQuestions` — numeric | SurveyJS `{type:"text", inputType:"number", min:1, max:10}` | `{Type:2, MinValue:1, MaxValue:10}` | No |
| `surveyJSToUnityQuestions` — choices as objects | `choices:[{value:"a",text:"A"}]` | `Options:["A"]` (uses text) | Yes |
| `unityQuestionsToSurveyJS` — round trip | Accessibility template questions | Identical after convert->convert back | Yes |
| `RuleRow` — All operator | operator=8 (All) | attribute/value fields disabled | Yes |
| `MappingRow` — lookup transform | transform="lookup" | lookup entries section visible | No |
| `MappingRow` — direct transform | transform="direct" | lookup entries hidden | No |
| Templates endpoint | GET /api/templates | 3 templates, each with correct structure | No |
| Response count | survey with 0 responses | `{ count: 0 }` | No |

### Edge Cases Checklist

- [x] Empty questions array (new survey)
- [x] Maximum 20 questions / 20 mappings / 9 rules
- [x] Question deleted while mapping references it
- [x] SurveyJS choices as strings vs objects
- [x] Negative speed delta values
- [x] Float precision for duration/speed
- [x] Operator "All" disabling condition fields
- [x] Empty template (V1 Parity has no questions/mappings)
- [x] Auth token expired mid-session
- [x] Concurrent saves (debounce handles this)

---

## Validation Commands

### Install Dependencies
```bash
cd web-app/client && npm install
```
EXPECT: No errors

### Dev Server (Client)
```bash
cd web-app/client && npm run dev
```
EXPECT: Vite dev server on port 5173 with API proxy

### Dev Server (API)
```bash
cd web-app && npm run dev
```
EXPECT: Express on port 3001

### Build Client
```bash
cd web-app/client && npm run build
```
EXPECT: `dist/` directory created, no errors

### Production Mode
```bash
cd web-app && npm start
```
EXPECT: Serves both API and client at `http://localhost:3001/`

### Docker Build
```bash
cd web-app && docker build -t edi-survey-test .
```
EXPECT: Build succeeds

### API Tests
```bash
# Health check
curl -s http://localhost:3001/api/health | grep '"success":true'

# Templates
curl -s http://localhost:3001/api/templates | grep '"success":true'
```
EXPECT: Both return success

### Manual Validation

- [ ] Register new professor account
- [ ] Login with existing account
- [ ] Create new empty survey from dashboard
- [ ] Create survey from "Accessibility" template
- [ ] Edit survey name in editor
- [ ] Questions tab: add Text question, add MultipleChoice question, add Numeric question
- [ ] Questions tab: drag-and-drop reorder
- [ ] Questions tab: delete question
- [ ] Switch to Mappings tab: add mapping, select question, set transform types
- [ ] Mappings tab: "lookup" transform shows lookup entries field
- [ ] Switch to Rules tab: add rule, set all fields
- [ ] Rules tab: "All" operator disables attribute/value
- [ ] Verify auto-save (check network tab for PUT requests)
- [ ] Navigate back to dashboard, re-open survey, verify data persisted
- [ ] Delete survey from dashboard

---

## Acceptance Criteria

- [ ] All tasks completed
- [ ] Professor can register/login/logout
- [ ] Professor can create surveys from scratch or from templates
- [ ] SurveyJS Creator provides drag-and-drop question editing
- [ ] Mapping editor links questions to attributes with transform types
- [ ] Rule editor supports all 9 ComparisonOperator values and 4 WeatherType values
- [ ] Auto-save persists changes to API
- [ ] Survey data round-trips correctly (create in Web App -> API -> re-open matches)
- [ ] Template data matches Unity `SurveyTemplates.cs` exactly
- [ ] `npm run build` produces working production build
- [ ] Docker build works

## Completion Checklist

- [ ] API responses follow `{ success, data?, error? }` pattern
- [ ] Auth token stored in localStorage, sent as Bearer header
- [ ] Constants match Unity C# enum values exactly
- [ ] SurveyJS <-> Unity question conversion is lossless
- [ ] No hardcoded values (enums in constants.js)
- [ ] Max limits enforced: 20 questions, 20 mappings, 9 rules
- [ ] Debounced auto-save (2 second delay)
- [ ] Production build served from Express static

## Risks

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| SurveyJS question types don't map cleanly to Unity's 3 types | Medium | High | Converter handles only text/radiogroup/number; ignore unsupported types with warning |
| SurveyJS Creator CSS conflicts with app styles | Low | Medium | Isolate Creator in its own container, avoid global CSS overrides |
| Auto-save race conditions | Low | Medium | Debounce + ignore stale saves by checking save timestamp |
| Vite proxy doesn't work in certain environments | Low | Low | Document manual API URL configuration |
| SurveyJS bundle size too large | Medium | Low | Acceptable for professor-facing tool; not student-facing (Phase 3 uses lighter Runner) |

## Notes

- **SurveyJS licensing**: survey-core and survey-creator-core are MIT licensed. Free for this use case.
- **No SSR needed**: This is a professor tool, not SEO-critical. Client-side rendering is fine.
- **SurveyJS Creator vs custom form**: We evaluated building a fully custom question editor. SurveyJS Creator provides drag-and-drop, preview, and validation out of the box. The ~500KB bundle is acceptable for a professor dashboard.
- **Template seeding**: For Phase 2, templates are hardcoded in the API route. Phase 5 will move them to database seed data.
- **Data flow**: Questions tab uses SurveyJS JSON internally but converts to Unity format on save. Mappings and Rules tabs work directly with Unity-compatible JSON structures. The API stores all three as JSON text columns.
