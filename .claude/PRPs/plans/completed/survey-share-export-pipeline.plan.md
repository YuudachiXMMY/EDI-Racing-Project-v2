# Plan: Survey Share Link & Export Pipeline

## Summary
Enhance the EDI Survey web-app so professors can generate shareable survey links (with copy/QR support), view collected responses in a dashboard, and export complete data (responses + mappings + rules) as Unity-ready JSON. This bridges the full professor workflow: create survey → distribute → collect → export → import into Unity racing game.

## User Story
As a professor,
I want to create a survey, generate a shareable link for students, view their responses, and export the collected data with mappings and rules,
So that I can import everything into the Unity racing game for the EDI simulation.

## Problem → Solution
**Current**: Survey creation works, but share codes are displayed as raw 8-char strings without a full URL or copy button. Professors have no UI to view collected responses before exporting. The export JSON omits raw attribute mappings. There is no way to toggle survey active/inactive from the frontend.

**Desired**: Professors see a prominent "Share" section with a full copyable URL and QR code. A new Responses tab on the editor shows all submissions. Export includes mappings alongside carData and eventRules. Active/inactive toggle is accessible from both dashboard and editor.

## Metadata
- **Complexity**: Medium
- **Source PRD**: N/A
- **PRD Phase**: N/A
- **Estimated Files**: 10

---

## UX Design

### Before
```
┌─ Dashboard ─────────────────────────────────────┐
│ Survey Card                                      │
│   "My Survey"                                    │
│   Code: A1B2C3D4          Updated: 7/20/2026     │
│   [Delete]                                       │
└──────────────────────────────────────────────────┘

┌─ Editor Page ────────────────────────────────────┐
│ ← Dashboard  [Survey Name]  Saved  2 responses  │
│                              [Export for Unity]  │
│ [Questions] [Mappings] [Rules]                   │
│ ┌────────────────────────────────────────────┐   │
│ │ (current tab content)                       │   │
│ └────────────────────────────────────────────┘   │
└──────────────────────────────────────────────────┘
```

### After
```
┌─ Dashboard ─────────────────────────────────────┐
│ Survey Card                                      │
│   "My Survey"           ● Active / ○ Inactive    │
│   🔗 Share Link: [full URL]  [Copy] [QR]         │
│   3 responses           Updated: 7/20/2026       │
│   [Delete]                                       │
└──────────────────────────────────────────────────┘

┌─ Editor Page ────────────────────────────────────┐
│ ← Dashboard  [Survey Name]  Saved  3 responses  │
│                              [Export for Unity]  │
│ ┌─ Share Panel ──────────────────────────────┐   │
│ │ Survey Link: https://host/survey/#/s/CODE  │   │
│ │ [Copy Link]  [Show QR]  [● Active]         │   │
│ └────────────────────────────────────────────┘   │
│ [Questions] [Mappings] [Rules] [Responses]       │
│ ┌────────────────────────────────────────────┐   │
│ │ (current tab content)                       │   │
│ └────────────────────────────────────────────┘   │
└──────────────────────────────────────────────────┘
```

### Interaction Changes
| Touchpoint | Before | After | Notes |
|---|---|---|---|
| Dashboard card | Shows raw `Code: A1B2C3D4` | Shows full URL + copy button + response count | Click card still navigates to editor |
| Editor header | Shows response count only | Shows response count + share panel always visible | Share panel is collapsible |
| Editor tabs | 3 tabs: Questions/Mappings/Rules | 4 tabs: Questions/Mappings/Rules/Responses | New Responses tab shows data table |
| Export JSON | `{ configName, carData, eventRules }` | `{ configName, carData, mappings, eventRules }` | Adds raw `mappings` array for Unity reference |
| Survey active toggle | No UI (only DB field) | Toggle on dashboard card + editor share panel | PATCH endpoint to toggle |

---

## Mandatory Reading

Files that MUST be read before implementing:

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 (critical) | `web-app/src/routes/surveys.js` | all | Survey CRUD patterns — all new endpoints follow this style |
| P0 (critical) | `web-app/src/routes/responses.js` | all | Response endpoints + share code resolution pattern |
| P0 (critical) | `web-app/src/routes/export.js` | all | Export logic — must add `mappings` to output |
| P0 (critical) | `web-app/client/src/pages/EditorPage.jsx` | all | Main editing UI — add share panel + responses tab |
| P0 (critical) | `web-app/client/src/pages/DashboardPage.jsx` | all | Dashboard cards — add share link + copy + toggle |
| P1 (important) | `web-app/client/src/api.js` | all | API client — add new endpoint functions |
| P1 (important) | `web-app/client/src/index.css` | all | Styling patterns for new components |
| P1 (important) | `web-app/src/middleware/auth.js` | all | Auth pattern for protected routes |
| P2 (reference) | `web-app/client/src/App.jsx` | all | Router structure |
| P2 (reference) | `Assets/Scripts/Data/JsonImporter.cs` | all | Unity import format — export must match |

## External Documentation

| Topic | Source | Key Takeaway |
|---|---|---|
| SurveyJS | Already integrated | SurveyJS Creator handles question editing; no changes needed |
| QR Code | `qrcode` npm package or inline SVG generation | Lightweight QR generation for share links |

---

## Patterns to Mirror

Code patterns discovered in the codebase. Follow these exactly.

### NAMING_CONVENTION
// SOURCE: web-app/src/routes/surveys.js:1-5
```javascript
import { Router } from 'express';
import { getDb } from '../db.js';
import { requireAuth } from '../middleware/auth.js';
const router = Router();
```
- Routes: kebab-case files, Router() instances, export default router
- DB queries: inline prepared statements with getDb()
- Response format: `{ success: true/false, data/error }`

### ERROR_HANDLING
// SOURCE: web-app/src/routes/surveys.js:37-41
```javascript
const survey = db.prepare('SELECT * FROM surveys WHERE id = ? AND user_id = ?')
  .get(req.params.id, req.user.userId);
if (!survey) {
  return res.status(404).json({ success: false, error: 'Survey not found' });
}
```
- Check ownership with `user_id = ?` for all protected resources
- Return `{ success: false, error: 'message' }` with appropriate HTTP status

### API_CLIENT_PATTERN
// SOURCE: web-app/client/src/api.js:19-33
```javascript
async function request(path, options = {}) {
  const token = getToken();
  const headers = { 'Content-Type': 'application/json', ...options.headers };
  if (token) headers['Authorization'] = `Bearer ${token}`;
  const res = await fetch(`/api${path}`, { ...options, headers });
  const json = await res.json();
  if (res.status === 401) {
    clearToken();
    window.location.hash = '#/login';
  }
  return json;
}
```
- All authenticated requests go through `request()` helper
- Public endpoints use `fetch()` directly

### REACT_COMPONENT_PATTERN
// SOURCE: web-app/client/src/pages/DashboardPage.jsx:5-10
```javascript
export default function DashboardPage() {
  const navigate = useNavigate();
  const [surveys, setSurveys] = useState([]);
  const [loading, setLoading] = useState(true);
  // ...
}
```
- Function components (no classes)
- Default exports
- State hooks at top
- Async data loading in useEffect or handler functions

### CSS_PATTERN
// SOURCE: web-app/client/src/index.css:1-14
```css
:root {
  --bg: #14141f;
  --bg-card: #1e1e2e;
  --bg-input: #2a2a3a;
  --text: #e0e0e0;
  --text-dim: #888;
  --accent: #4a9eff;
}
```
- Dark theme via CSS variables
- Single `index.css` file (no CSS modules)
- BEM-lite class naming: `.survey-card`, `.card-meta`, `.export-panel`

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `web-app/src/routes/surveys.js` | UPDATE | Add PATCH /:id/active endpoint for toggle |
| `web-app/src/routes/export.js` | UPDATE | Include raw `mappings` in export JSON output |
| `web-app/client/src/api.js` | UPDATE | Add toggleSurveyActive(), getResponses() already exists |
| `web-app/client/src/pages/DashboardPage.jsx` | UPDATE | Add share link, copy button, response count, active toggle |
| `web-app/client/src/pages/EditorPage.jsx` | UPDATE | Add share panel + Responses tab |
| `web-app/client/src/components/ResponsesTab.jsx` | CREATE | New tab component showing response data table |
| `web-app/client/src/components/SharePanel.jsx` | CREATE | Reusable share link + copy + QR + active toggle |
| `web-app/client/src/index.css` | UPDATE | Add styles for SharePanel, ResponsesTab |

## NOT Building

- QR code generation (adds npm dependency, keep it simple with just copy link)
- Email/notification system for distributing survey links
- Response editing or modification by professors
- CSV export format (JSON only, matching Unity import)
- Real-time response streaming / websocket updates
- Survey analytics or charts
- Student authentication for survey responses (stays anonymous with email+teamName)

---

## Step-by-Step Tasks

### Task 1: Add PATCH endpoint for survey active toggle
- **ACTION**: Add a `PATCH /api/surveys/:id/active` endpoint to `web-app/src/routes/surveys.js`
- **IMPLEMENT**: Accept `{ isActive: boolean }` in request body. Update `is_active` and `updated_at` in the surveys table. Return `{ success: true }`.
- **MIRROR**: ERROR_HANDLING pattern (check ownership first), NAMING_CONVENTION
- **IMPORTS**: No new imports needed
- **GOTCHA**: `is_active` is stored as INTEGER (0/1) in SQLite, convert boolean to int
- **VALIDATE**: `curl -X PATCH localhost:3001/api/surveys/1/active -H 'Authorization: Bearer TOKEN' -H 'Content-Type: application/json' -d '{"isActive":false}'` returns `{ success: true }`

### Task 2: Include mappings in export JSON
- **ACTION**: Modify the export endpoint in `web-app/src/routes/export.js` to include raw `mappings` array
- **IMPLEMENT**: Add `mappings` field to `exportData` object alongside `carData` and `eventRules`
- **MIRROR**: Existing export pattern at line 103-107
- **IMPORTS**: None
- **GOTCHA**: The `mappings` variable is already parsed from `survey.mappings_json` on line 89. Just add it to the output object. Ensure it doesn't break Unity's `JsonImporter.cs` — Unity ignores unknown fields in `JsonUtility.FromJson`
- **VALIDATE**: `GET /api/surveys/:id/export` response now contains `mappings` array at top level

### Task 3: Add API client functions
- **ACTION**: Add `toggleSurveyActive(id, isActive)` to `web-app/client/src/api.js`
- **IMPLEMENT**: 
  ```javascript
  export async function toggleSurveyActive(id, isActive) {
    return request(`/surveys/${id}/active`, {
      method: 'PATCH',
      body: JSON.stringify({ isActive })
    });
  }
  ```
- **MIRROR**: API_CLIENT_PATTERN
- **IMPORTS**: None (uses existing `request` function)
- **GOTCHA**: `getResponses(id)` already exists in api.js
- **VALIDATE**: Import works in DashboardPage and EditorPage

### Task 4: Create SharePanel component
- **ACTION**: Create `web-app/client/src/components/SharePanel.jsx`
- **IMPLEMENT**: Component that receives `shareCode`, `isActive`, `onToggleActive` props. Displays:
  - Full survey URL: `{window.location.origin}/survey/#/s/{shareCode}`
  - Copy Link button (uses `navigator.clipboard.writeText`)
  - Active/Inactive toggle button
  - Visual feedback on copy ("Copied!")
- **MIRROR**: REACT_COMPONENT_PATTERN
- **IMPORTS**: `useState` from react
- **GOTCHA**: The app uses `HashRouter` so the URL must include `#`. The base path is `/survey/` (from vite.config.js). Full URL pattern: `{origin}/survey/#/s/{shareCode}`
- **VALIDATE**: Component renders, copy button works, toggle fires callback

### Task 5: Create ResponsesTab component
- **ACTION**: Create `web-app/client/src/components/ResponsesTab.jsx`
- **IMPLEMENT**: Component that receives `surveyId` prop. On mount, calls `getResponses(surveyId)`. Displays a table with columns: email, team name, submitted at, and expandable answers JSON. Shows "No responses yet" when empty.
- **MIRROR**: REACT_COMPONENT_PATTERN (data loading pattern like DashboardPage)
- **IMPORTS**: `useState, useEffect` from react, `getResponses` from api.js
- **GOTCHA**: Response `answers` is already parsed by the API (see responses.js:82-84). Each answer is a `{ questionId: value }` object — display as key-value pairs, not raw JSON.
- **VALIDATE**: Tab shows response data correctly, handles empty state

### Task 6: Update DashboardPage with share link and response count
- **ACTION**: Update `web-app/client/src/pages/DashboardPage.jsx`
- **IMPLEMENT**:
  - Replace `<span className="share-code">Code: {s.share_code}</span>` with a share link row showing full URL + copy button
  - Add response count next to each survey card (batch-fetch counts or display from survey data)
  - Add active/inactive indicator with toggle button on each card
- **MIRROR**: Existing card layout pattern, API_CLIENT_PATTERN
- **IMPORTS**: `toggleSurveyActive, getResponseCount` from api.js
- **GOTCHA**: Fetching response counts for all surveys in parallel could be expensive. Instead, add response_count to the survey list query via SQL subquery. This requires a backend change too (Task 7).
- **VALIDATE**: Dashboard cards show full share URLs with copy buttons, active toggle works

### Task 7: Add response count to survey list endpoint
- **ACTION**: Modify the `GET /api/surveys` endpoint in `web-app/src/routes/surveys.js` to include response count
- **IMPLEMENT**: Change the SQL query to include a subquery:
  ```sql
  SELECT s.id, s.config_name, s.description, s.share_code, s.is_active, s.created_at, s.updated_at,
    (SELECT COUNT(*) FROM responses r WHERE r.survey_id = s.id) as response_count
  FROM surveys s WHERE s.user_id = ? ORDER BY s.updated_at DESC
  ```
- **MIRROR**: Existing query pattern at line 15-18
- **IMPORTS**: None
- **GOTCHA**: SQLite subqueries are efficient for small datasets. This avoids N+1 API calls from the frontend.
- **VALIDATE**: `GET /api/surveys` response includes `response_count` field per survey

### Task 8: Update EditorPage with share panel and responses tab
- **ACTION**: Update `web-app/client/src/pages/EditorPage.jsx`
- **IMPLEMENT**:
  - Import and render `SharePanel` below the editor header, passing `shareCode`, `isActive`, and toggle handler
  - Add "Responses" to the `TABS` array (becomes 4th tab)
  - Import and render `ResponsesTab` when the responses tab is active
  - Active toggle should update local state and call API
- **MIRROR**: Existing tab rendering pattern (lines 142-175)
- **IMPORTS**: `SharePanel` from components, `ResponsesTab` from components, `toggleSurveyActive` from api.js
- **GOTCHA**: `survey.share_code` is the field name from the API (snake_case). `survey.is_active` is INTEGER from SQLite — treat as boolean (truthy/falsy).
- **VALIDATE**: Share panel renders with correct URL, Responses tab loads and displays data, active toggle works

### Task 9: Add CSS styles for new components
- **ACTION**: Append styles to `web-app/client/src/index.css`
- **IMPLEMENT**: Add styles for:
  - `.share-panel` — horizontal bar with link input, copy button, active toggle
  - `.share-url` — readonly text input showing full URL
  - `.copy-btn` — styled copy button with success state
  - `.active-toggle` — green/red indicator button
  - `.responses-tab` — table layout for response data
  - `.response-table` — full-width table with dark theme
  - `.response-row` — alternating row colors
  - `.answers-cell` — expandable key-value display
- **MIRROR**: CSS_PATTERN (dark theme variables, BEM-lite naming)
- **IMPORTS**: N/A
- **GOTCHA**: Keep consistent with existing dark theme. Table should be horizontally scrollable on small screens.
- **VALIDATE**: All new UI elements render correctly with dark theme

---

## Testing Strategy

### Unit Tests

| Test | Input | Expected Output | Edge Case? |
|---|---|---|---|
| PATCH /active with true | `{ isActive: true }` | `{ success: true }`, db `is_active = 1` | No |
| PATCH /active with false | `{ isActive: false }` | `{ success: true }`, db `is_active = 0` | No |
| PATCH /active wrong owner | valid id, wrong user token | 404 | Yes |
| Export includes mappings | Survey with 3 mappings | Export JSON has `mappings` array with 3 entries | No |
| Export with no mappings | Survey with empty mappings | Export JSON has `mappings: []` | Yes |
| Survey list includes count | Survey with 5 responses | `response_count: 5` in list data | No |
| Survey list with 0 responses | New survey | `response_count: 0` | Yes |
| Share URL generation | shareCode "A1B2C3D4" | URL ends with `/survey/#/s/A1B2C3D4` | No |
| Copy to clipboard | Click copy button | Clipboard contains full URL, button shows "Copied!" | No |
| Responses tab empty state | Survey with 0 responses | Shows "No responses yet" message | Yes |

### Edge Cases Checklist
- [ ] Empty survey (no questions, no mappings, no rules) — share and export should still work
- [ ] Survey with 0 responses — export produces `carData: []`, responses tab shows empty state
- [ ] Very long share URL — doesn't break layout
- [ ] Inactive survey — student page shows "no longer accepting responses" (already handled)
- [ ] Concurrent toggle — multiple rapid clicks don't corrupt state
- [ ] Clipboard API unavailable (HTTP context) — fallback or show error

---

## Validation Commands

### Static Analysis
```bash
cd web-app/client && npx oxlint
```
EXPECT: Zero errors on new/modified files

### Server Start
```bash
cd web-app && node src/index.js
```
EXPECT: Server starts without errors, DB initializes

### Manual API Tests
```bash
# Register + login
curl -X POST localhost:3001/api/auth/register -H 'Content-Type: application/json' -d '{"email":"test@test.com","password":"test123","displayName":"Test"}'

# Create survey
curl -X POST localhost:3001/api/surveys -H 'Authorization: Bearer TOKEN' -H 'Content-Type: application/json' -d '{"configName":"Test Survey"}'

# Toggle active
curl -X PATCH localhost:3001/api/surveys/1/active -H 'Authorization: Bearer TOKEN' -H 'Content-Type: application/json' -d '{"isActive":false}'

# List surveys (check response_count)
curl localhost:3001/api/surveys -H 'Authorization: Bearer TOKEN'

# Export (check mappings field)
curl localhost:3001/api/surveys/1/export -H 'Authorization: Bearer TOKEN'
```

### Browser Validation
```bash
cd web-app/client && npm run dev
# Open http://localhost:5173/survey/
```
EXPECT:
- Dashboard shows share links with copy buttons on each card
- Active/Inactive toggle works on dashboard cards
- Editor shows share panel at top with full URL
- Responses tab loads and displays data (or empty state)
- Export JSON includes `mappings` field
- Copy link button copies correct URL to clipboard
- Student page at `/#/s/SHARECODE` still works

### Build Validation
```bash
cd web-app/client && npm run build
```
EXPECT: Vite build succeeds with no errors

---

## Acceptance Criteria
- [ ] Professor can see a full shareable survey URL on dashboard and editor
- [ ] Professor can copy the share link to clipboard with one click
- [ ] Professor can toggle survey active/inactive from the UI
- [ ] Professor can view all collected responses in a Responses tab
- [ ] Export JSON includes `mappings` array alongside `carData` and `eventRules`
- [ ] Unity's `JsonImporter.cs` still works with the updated export format (backward compatible)
- [ ] Dashboard shows response count per survey without extra API calls
- [ ] All existing functionality (create, edit, delete, template, student submit) still works
- [ ] Dark theme is consistent across all new UI elements

## Completion Checklist
- [ ] Code follows discovered patterns (Router, request helper, function components)
- [ ] Error handling matches codebase style (ownership check, 404 responses)
- [ ] No hardcoded values (URLs built dynamically from window.location)
- [ ] No unnecessary scope additions
- [ ] Self-contained — no questions needed during implementation

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Clipboard API fails on HTTP (non-HTTPS) | Medium | Low | Use `navigator.clipboard` with try-catch, fallback to `document.execCommand` or show URL in selectable input |
| Adding `mappings` to export breaks Unity import | Low | Medium | Unity's `JsonUtility.FromJson` ignores unknown fields — verified by JsonImporter.cs structure |
| Response count subquery slow with many surveys | Low | Low | SQLite handles subqueries efficiently for small-medium datasets |
| Hash router URL format confusing for users | Low | Low | Display clean URL with clear copy-to-clipboard UX |

## Notes
- The web-app uses a `HashRouter` with base path `/survey/`, so the full share URL is: `{origin}/survey/#/s/{shareCode}`
- SQLite `is_active` is INTEGER — JavaScript boolean needs conversion at the API boundary
- The Unity `JsonImporter.cs` only parses `configName`, `carData`, and `eventRules` fields — adding `mappings` to the export won't break deserialization because `JsonUtility.FromJson` ignores unrecognized fields
- Consider adding `mappings` parsing to Unity's `JsonImporter` in a separate task if needed
- The in-memory session store means toggling active/inactive persists in DB but auth sessions don't survive server restarts — this is acceptable for MVP
