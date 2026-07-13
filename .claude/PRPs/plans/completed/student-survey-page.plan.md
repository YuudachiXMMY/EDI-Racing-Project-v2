# Plan: Student Survey Response Page

## Summary
Build the student-facing survey page for the EDI Survey Web App. Students access a survey via share link (`/s/:shareCode`), enter email + team name, answer questions rendered by SurveyJS Runner, and submit. The backend stores responses in the existing `responses` table and provides professor-facing endpoints to view response data.

## User Story
As a student, I want to open a survey link on my phone browser, answer the questions, and submit my responses, so that my team car is generated for the EDI racing game.

## Problem -> Solution
Students currently must load a full Unity WebGL build, connect via WebSocket, and wait for the professor to distribute a survey. -> Students open a lightweight web link, fill out a mobile-friendly form, and submit in under 3 minutes.

## Metadata
- **Complexity**: Medium
- **Source PRD**: `.claude/PRPs/prds/edi-survey-web-app.prd.md`
- **PRD Phase**: Phase 3 - Student Survey Response Page
- **Estimated Files**: 7 (3 backend, 4 frontend)

---

## UX Design

### Before
```
N/A - No web-based student survey page exists yet.
Students use Unity WebGL (StudentSurveyPanel.cs):
  Load Unity WebGL (30s+) -> Join room -> Wait for professor -> Answer survey -> Submit via WebSocket
```

### After
```
Student flow (mobile-friendly, < 3 minutes):

  ┌──────────────────────────────────────┐
  │  EDI Survey                          │
  │                                      │
  │  Email:    [student@example.com]     │
  │  Team:     [Team Alpha           ]   │
  │                                      │
  │  ┌──────────────────────────────┐    │
  │  │  1. Do you have a disability │    │
  │  │     that affects daily       │    │
  │  │     activities?              │    │
  │  │                              │    │
  │  │  ○ No                        │    │
  │  │  ● Yes - Physical            │    │
  │  │  ○ Yes - Cognitive           │    │
  │  │  ○ Yes - Sensory             │    │
  │  │  ○ Prefer not to say         │    │
  │  │                              │    │
  │  │  2. Do you use assistive     │    │
  │  │     technology?              │    │
  │  │  ...                         │    │
  │  └──────────────────────────────┘    │
  │                                      │
  │  [        Submit Survey          ]   │
  └──────────────────────────────────────┘

After submit:
  ┌──────────────────────────────────────┐
  │                                      │
  │     ✓ Response Submitted!            │
  │                                      │
  │     Thank you, Team Alpha.           │
  │     Your responses have been         │
  │     recorded. Your team car will     │
  │     be ready for the race.           │
  │                                      │
  └──────────────────────────────────────┘
```

### Interaction Changes
| Touchpoint | Before | After | Notes |
|---|---|---|---|
| Entry point | Unity WebGL + WebSocket join | Direct URL `/s/:shareCode` | No app download, instant load |
| Identity | Team name only (via Unity UI) | Email + team name | Email enables unique constraint |
| Question rendering | C# `StudentSurveyPanel` with manual UI | SurveyJS Runner (React) | Consistent with creator, mobile-optimized |
| Submission | WebSocket message to professor | HTTP POST to REST API | Persisted in SQLite, survives disconnects |
| Confirmation | Unity confirmation panel | Web confirmation page | Clear success state |
| Error: already submitted | Not handled in Unity | Show friendly "already submitted" message | Unique constraint on (survey_id, email) |
| Error: survey not found | N/A | 404 page with clear message | Bad/expired share code |
| Error: survey inactive | N/A | Show "survey closed" message | Professor deactivated survey |

---

## Mandatory Reading

Files that MUST be read before implementing:

| Priority | File | Lines | Why |
|---|---|---|---|
| P0 | `web-app/src/routes/surveys.js` | all | Route pattern, error handling, DB access pattern |
| P0 | `web-app/src/schema.sql` | all | Database schema — `responses` table already defined |
| P0 | `web-app/client/src/surveyjs-config.js` | all | `unityQuestionsToSurveyJS()` converts stored questions to SurveyJS format |
| P0 | `web-app/client/src/App.jsx` | all | Router setup, ProtectedRoute pattern |
| P0 | `web-app/client/src/api.js` | all | API client pattern, `request()` wrapper |
| P1 | `web-app/client/src/constants.js` | all | Shared enums matching Unity |
| P1 | `web-app/client/src/index.css` | all | CSS custom properties, dark theme, component styles |
| P1 | `web-app/client/src/pages/LoginPage.jsx` | all | Page component pattern (simplest page) |
| P1 | `web-app/src/middleware/auth.js` | all | Auth middleware — student routes must NOT use it |
| P2 | `web-app/src/index.js` | all | Express app setup, route mounting |
| P2 | `web-app/client/src/components/QuestionsTab.jsx` | all | SurveyJS Creator usage — Runner is similar but simpler |
| P2 | `Assets/Scripts/UI/StudentSurveyPanel.cs` | all | Unity student survey flow for feature parity reference |

## External Documentation

| Topic | Source | Key Takeaway |
|---|---|---|
| SurveyJS React Runner | `survey-react-ui` (already installed v2.5.33) | Use `<Survey model={model} />` with `Model` from `survey-core`. Set `model.onComplete.add(callback)` to handle submission. Import `survey-core/survey-core.css` for default styles. |
| SurveyJS Model API | `survey-core` `Model` class | `new Model(surveyJSON)` creates a runner model. `model.data` contains answers after completion. `model.onComplete` fires on submit. |

---

## Patterns to Mirror

Code patterns discovered in the codebase. Follow these exactly.

### NAMING_CONVENTION
```js
// SOURCE: web-app/src/routes/surveys.js:1-4
// Backend: ES modules, Router from express, camelCase functions
import { Router } from 'express';
import { getDb } from '../db.js';
const router = Router();

// SOURCE: web-app/client/src/pages/LoginPage.jsx:1-5
// Frontend: default export function components, PascalCase pages
export default function LoginPage() { ... }

// SOURCE: web-app/client/src/api.js:19-33
// API functions: async, camelCase, return json directly
async function request(path, options = {}) {
  const res = await fetch(`/api${path}`, { ...options, headers });
  const json = await res.json();
  return json;
}
```

### ERROR_HANDLING
```js
// SOURCE: web-app/src/routes/surveys.js:26-28
// Backend: check existence, return { success: false, error: 'message' } with HTTP status
if (!survey) {
  return res.status(404).json({ success: false, error: 'Survey not found' });
}

// SOURCE: web-app/src/index.js:43-46
// Global error handler: log + generic 500
app.use((err, req, res, _next) => {
  console.error('[API] Error:', err.message);
  res.status(500).json({ success: false, error: 'Internal server error' });
});
```

### API_RESPONSE_FORMAT
```js
// SOURCE: web-app/src/routes/surveys.js:18,73-78
// Success: { success: true, data: <payload> }
res.json({ success: true, data: surveys });
res.status(201).json({ success: true, data: { id: result.lastInsertRowid, shareCode } });

// Error: { success: false, error: 'message' }
res.status(400).json({ success: false, error: 'configName is required' });
```

### FRONTEND_STATE_PATTERN
```js
// SOURCE: web-app/client/src/pages/LoginPage.jsx:6-12
// useState for local state, loading/error pattern
const [loading, setLoading] = useState(false);
const [error, setError] = useState('');

// SOURCE: web-app/client/src/pages/DashboardPage.jsx:15-20
// Async data loading in useEffect
useEffect(() => { loadData(); }, []);
async function loadData() {
  setLoading(true);
  const result = await getSurveys();
  if (result.success) setSurveys(result.data);
  setLoading(false);
}
```

### CSS_PATTERN
```css
/* SOURCE: web-app/client/src/index.css:1-14 */
/* Dark theme CSS custom properties */
:root {
  --bg: #14141f;
  --bg-card: #1e1e2e;
  --bg-input: #2a2a3a;
  --text: #e0e0e0;
  --text-dim: #888;
  --accent: #4a9eff;
  --danger: #e04040;
  --success: #40a040;
}

/* SOURCE: web-app/client/src/index.css:48-57 */
/* Page-level class naming: .login-page, .dashboard-page, etc. */
.login-page { display: flex; align-items: center; justify-content: center; min-height: 100vh; }
.login-card { background: var(--bg-card); padding: 40px; border-radius: 8px; }
```

### ROUTER_PATTERN
```js
// SOURCE: web-app/client/src/App.jsx:1-22
// HashRouter, unprotected routes alongside protected routes
<HashRouter>
  <Routes>
    <Route path="/login" element={<LoginPage />} />
    <Route path="/dashboard" element={<ProtectedRoute><DashboardPage /></ProtectedRoute>} />
    <Route path="*" element={<Navigate to="..." replace />} />
  </Routes>
</HashRouter>
```

### SURVEYJS_PATTERN
```js
// SOURCE: web-app/client/src/components/QuestionsTab.jsx:1-5
// SurveyJS imports — Creator uses survey-creator-react; Runner uses survey-react-ui
import { SurveyCreatorComponent, SurveyCreator } from 'survey-creator-react';
import 'survey-core/survey-core.css';

// SOURCE: web-app/client/src/surveyjs-config.js:7-31
// Convert Unity questions to SurveyJS JSON format
export function unityQuestionsToSurveyJS(questions) {
  const elements = questions.map(q => { ... });
  return { pages: [{ name: 'page1', elements }] };
}
```

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `web-app/src/routes/responses.js` | CREATE | New route file for student-facing survey and response submission endpoints |
| `web-app/src/index.js` | UPDATE | Mount the new responses router |
| `web-app/client/src/pages/StudentSurveyPage.jsx` | CREATE | Student survey page with SurveyJS Runner |
| `web-app/client/src/App.jsx` | UPDATE | Add unprotected `/s/:shareCode` route |
| `web-app/client/src/api.js` | UPDATE | Add `getPublicSurvey()` and `submitResponse()` API functions |
| `web-app/client/src/index.css` | UPDATE | Add styles for student survey page |
| `web-app/src/routes/surveys.js` | UPDATE | Add professor endpoint to list responses |

## NOT Building

- Real-time response count updates (WebSocket push) — professor refreshes manually
- Student authentication or registration — email-only identification
- Response editing after submission — students submit once
- Survey branching/skip logic — simple linear questions only
- Analytics dashboard — just response count and list
- Email notifications to students — share link distribution is manual

---

## Step-by-Step Tasks

### Task 1: Create backend response routes (`responses.js`)
- **ACTION**: Create `web-app/src/routes/responses.js` with three endpoints for student survey access
- **IMPLEMENT**:
  1. `GET /api/s/:shareCode` — Look up survey by `share_code`, return survey metadata + parsed `questions_json` only (no mappings/rules). Check `is_active`. No auth required.
  2. `POST /api/s/:shareCode/respond` — Accept `{ email, teamName, answers }`. Validate: email required, teamName required, survey must exist and be active. Insert into `responses` table. Handle UNIQUE constraint (409 if already submitted for this email+survey).
  3. `GET /api/surveys/:id/responses` — Professor endpoint with `requireAuth`. Return all responses for a survey owned by the professor. Include response count.
- **MIRROR**: `NAMING_CONVENTION`, `ERROR_HANDLING`, `API_RESPONSE_FORMAT` patterns from `surveys.js`
- **IMPORTS**: `Router` from `express`, `getDb` from `../db.js`, `requireAuth` from `../middleware/auth.js`
- **GOTCHA**: The UNIQUE index `idx_responses_unique` on `(survey_id, email)` will throw a SQLite error on duplicate. Catch this and return 409 with a friendly message. Use `try/catch` around the INSERT.
- **GOTCHA**: Do NOT expose `mappings_json` or `rules_json` to students — they should only see questions.
- **GOTCHA**: Convert `questions_json` from JSON string to parsed array in the response (matching `surveys.js:47` pattern).
- **VALIDATE**: 
  - Start server, create a survey via professor API, then `curl GET /api/s/<shareCode>` returns questions
  - `curl POST /api/s/<shareCode>/respond` with valid data returns 201
  - Duplicate email submission returns 409
  - Inactive survey returns 403
  - Invalid share code returns 404

### Task 2: Mount response routes in Express app
- **ACTION**: Update `web-app/src/index.js` to import and mount the new responses router
- **IMPLEMENT**: Add `import responseRoutes from './routes/responses.js';` and `app.use('/api', responseRoutes);` (note: NOT `/api/responses` because the routes define `/s/:shareCode` paths directly)
- **MIRROR**: Pattern from `index.js:7-10,26-28`
- **IMPORTS**: `responseRoutes` from `./routes/responses.js`
- **GOTCHA**: The student routes `/api/s/:shareCode` must be mounted BEFORE the catch-all `app.get('*')` that serves the SPA. They already will be since routes are mounted before the static file serving block.
- **GOTCHA**: The professor responses endpoint `GET /api/surveys/:id/responses` uses the same `/api/surveys` prefix — mount it as `app.use('/api/surveys', responseRoutes)` alongside the existing `surveyRoutes` and `exportRoutes`.
- **VALIDATE**: Server starts without errors, all existing routes still work

### Task 3: Add API client functions
- **ACTION**: Update `web-app/client/src/api.js` to add public (no-auth) API functions for student survey page
- **IMPLEMENT**:
  1. `getPublicSurvey(shareCode)` — GET `/s/${shareCode}` (note: uses `fetch` directly, NOT the `request()` wrapper, because `request()` adds auth token and redirects on 401 which is wrong for public routes)
  2. `submitResponse(shareCode, { email, teamName, answers })` — POST `/s/${shareCode}/respond` (also direct `fetch`, no auth)
  3. `getResponses(surveyId)` — GET `/surveys/${surveyId}/responses` (uses `request()` wrapper since it's professor-authed)
- **MIRROR**: `NAMING_CONVENTION` pattern from `api.js`
- **IMPORTS**: None (fetch is global)
- **GOTCHA**: The `request()` wrapper in `api.js` does `window.location.hash = '#/login'` on 401. Student pages should NOT redirect to login on 401 — they don't need auth. Use direct `fetch('/api/s/...')` for student endpoints instead of `request()`.
- **VALIDATE**: Functions are importable and return expected response shapes

### Task 4: Create StudentSurveyPage component
- **ACTION**: Create `web-app/client/src/pages/StudentSurveyPage.jsx` — the main student experience
- **IMPLEMENT**:
  1. Use `useParams()` to get `shareCode` from route
  2. On mount, call `getPublicSurvey(shareCode)` to load survey
  3. Show loading state, then error states (not found, inactive, already submitted)
  4. Render a form with email + team name inputs at the top
  5. Render SurveyJS Runner using `survey-react-ui`:
     - `import { Model } from 'survey-core'`
     - `import { Survey } from 'survey-react-ui'`
     - `import 'survey-core/survey-core.css'`
     - Create model from `unityQuestionsToSurveyJS(survey.questions)`
     - Use `model.onComplete.add((sender) => { ... })` to get answers
  6. On SurveyJS complete: combine email + teamName + `sender.data` → call `submitResponse()`
  7. Handle success → show confirmation screen
  8. Handle 409 → show "already submitted" message
  9. Handle other errors → show error message
  10. Page states: `loading` | `ready` | `submitting` | `submitted` | `error`
- **MIRROR**: `FRONTEND_STATE_PATTERN`, `CSS_PATTERN`, `SURVEYJS_PATTERN`
- **IMPORTS**: `useState, useEffect` from `react`, `useParams` from `react-router-dom`, `Model` from `survey-core`, `Survey` from `survey-react-ui`, `unityQuestionsToSurveyJS` from `../surveyjs-config.js`, `getPublicSurvey, submitResponse` from `../api.js`
- **GOTCHA**: SurveyJS Runner's `onComplete` fires BEFORE the UI transitions to the "thank you" page. We should NOT use SurveyJS's built-in completion page — instead, set `model.showCompletedPage = false` and manage our own confirmation UI.
- **GOTCHA**: SurveyJS model must be created only once (not on every render). Use `useState(() => new Model(...))` or `useRef` to ensure stability.
- **GOTCHA**: Email and team name are NOT SurveyJS questions — they're separate inputs above the survey. Validate them before allowing SurveyJS to submit.
- **GOTCHA**: The SurveyJS dark theme CSS will conflict with the app's dark theme. The SurveyJS survey renders with white background by default. Apply a custom theme or wrap in a container that allows the white SurveyJS theme (consistent with QuestionsTab which also uses `background: #fff` — see `index.css:115`).
- **VALIDATE**: Navigate to `/s/<valid-code>`, fill form, submit → confirmation shown. Navigate to `/s/INVALID` → 404 message.

### Task 5: Add route to React Router
- **ACTION**: Update `web-app/client/src/App.jsx` to add the student survey route
- **IMPLEMENT**: Add `<Route path="/s/:shareCode" element={<StudentSurveyPage />} />` — NO `ProtectedRoute` wrapper since students don't authenticate
- **MIRROR**: `ROUTER_PATTERN` from `App.jsx`
- **IMPORTS**: `StudentSurveyPage` from `./pages/StudentSurveyPage.jsx`
- **GOTCHA**: Place the `/s/:shareCode` route BEFORE the `*` catch-all route. Order matters in react-router-dom v7.
- **VALIDATE**: Navigate to `/#/s/ABCD1234` → loads StudentSurveyPage (or error if code invalid)

### Task 6: Add CSS for student survey page
- **ACTION**: Update `web-app/client/src/index.css` to add student page styles
- **IMPLEMENT**:
  1. `.student-page` — centered layout, max-width 640px, mobile-friendly padding
  2. `.student-card` — matches `.login-card` style but wider for survey content
  3. `.student-header` — survey title display
  4. `.student-form` — email + team name input section
  5. `.student-survey` — wrapper for SurveyJS Runner (white background like `.questions-tab`)
  6. `.student-confirmation` — success state with green accent
  7. `.student-error` — error state
  8. Mobile responsive: media query for small screens (padding reduction, full width)
- **MIRROR**: `CSS_PATTERN` — use existing custom properties, match `.login-card`/`.login-page` structure
- **GOTCHA**: SurveyJS Runner renders with its own CSS. The `.student-survey` wrapper should have `background: #fff; border-radius: 6px;` to contain SurveyJS's white theme (matches existing `.questions-tab` pattern in `index.css:115`).
- **VALIDATE**: Page looks correct on mobile (375px width) and desktop (1200px width)

### Task 7: Add professor responses endpoint to surveys route
- **ACTION**: Update `web-app/src/routes/surveys.js` to add endpoint for listing responses
- **IMPLEMENT**: Add `GET /api/surveys/:id/responses` with `requireAuth`. Query all responses for the given survey (owned by professor). Return array of responses with parsed `answers_json`.
- **MIRROR**: Pattern from `surveys.js:22-32` (`:id/responses/count`)
- **GOTCHA**: This is similar to the existing `/responses/count` endpoint but returns full data. Keep both — count is lightweight, full list is for detailed view.
- **VALIDATE**: `curl` with auth token returns response list; without auth returns 401

---

## Testing Strategy

### Unit Tests

This project does not have an existing test framework configured. Validation will be manual + curl-based.

| Test | Input | Expected Output | Edge Case? |
|---|---|---|---|
| Get survey by valid share code | `GET /api/s/ABCD1234` | 200, questions array | No |
| Get survey by invalid share code | `GET /api/s/INVALID` | 404, error message | Yes |
| Get inactive survey | `GET /api/s/<inactive-code>` | 403, "Survey is not active" | Yes |
| Submit valid response | `POST /api/s/<code>/respond` with email, teamName, answers | 201, success | No |
| Submit duplicate response | Same email + survey_id again | 409, "already submitted" | Yes |
| Submit missing email | POST without email | 400, validation error | Yes |
| Submit missing team name | POST without teamName | 400, validation error | Yes |
| Get responses as professor | `GET /api/surveys/:id/responses` with auth | 200, responses array | No |
| Get responses for other professor's survey | Wrong user auth | 404 | Yes |
| Mobile rendering | Open on 375px viewport | Responsive layout, no horizontal scroll | Yes |

### Edge Cases Checklist
- [ ] Empty survey (no questions) — should still show email/teamName form with submit
- [ ] Very long team name — no truncation issues
- [ ] Special characters in email/answers — properly stored and retrieved
- [ ] Survey with all question types (Text, MultipleChoice, Numeric) — all render correctly
- [ ] Concurrent submissions (50 students) — SQLite WAL mode handles this
- [ ] Share code case sensitivity — codes are uppercase, search should be case-insensitive
- [ ] Back button after submission — should show confirmation, not re-render form

---

## Validation Commands

### Static Analysis
```bash
cd web-app/client && npx oxlint src/
```
EXPECT: Zero errors

### Development Server
```bash
# Terminal 1: Backend
cd web-app && npm run dev
# Terminal 2: Frontend
cd web-app/client && npm run dev
```
EXPECT: Both servers start without errors

### API Smoke Test
```bash
# 1. Register professor
curl -s -X POST http://localhost:3001/api/auth/register \
  -H 'Content-Type: application/json' \
  -d '{"email":"test@test.com","password":"test123"}' | jq .

# 2. Create survey (save token from step 1)
TOKEN="<from step 1>"
curl -s -X POST http://localhost:3001/api/surveys \
  -H "Authorization: Bearer $TOKEN" \
  -H 'Content-Type: application/json' \
  -d '{"configName":"Test","questions":[{"Id":"q1","Text":"Your name?","Type":0,"Options":[],"MinValue":0,"MaxValue":0,"Required":true}],"mappings":[],"rules":[]}' | jq .

# 3. Get survey as student (save shareCode from step 2)
curl -s http://localhost:3001/api/s/<SHARE_CODE> | jq .

# 4. Submit student response
curl -s -X POST http://localhost:3001/api/s/<SHARE_CODE>/respond \
  -H 'Content-Type: application/json' \
  -d '{"email":"student@test.com","teamName":"Team Alpha","answers":{"q1":"Alice"}}' | jq .

# 5. Duplicate submission (should fail 409)
curl -s -X POST http://localhost:3001/api/s/<SHARE_CODE>/respond \
  -H 'Content-Type: application/json' \
  -d '{"email":"student@test.com","teamName":"Team Alpha","answers":{"q1":"Alice"}}' | jq .

# 6. Get responses as professor
curl -s http://localhost:3001/api/surveys/1/responses \
  -H "Authorization: Bearer $TOKEN" | jq .
```
EXPECT: Steps 1-4,6 return `{ success: true }`, step 5 returns 409

### Browser Validation
```
1. Open http://localhost:5173/#/s/<SHARE_CODE>
2. Verify survey title and questions render
3. Fill email + team name
4. Answer all questions
5. Click Complete/Submit
6. Verify confirmation page shows
7. Try again with same email -> "already submitted" message
8. Test on mobile viewport (375px width in devtools)
```
EXPECT: All steps work, mobile layout is readable and usable

### Manual Validation
- [ ] Student can access survey via share code URL
- [ ] Survey title displays correctly
- [ ] All three question types render (Text, MultipleChoice, Numeric)
- [ ] Required field validation works
- [ ] Email + team name validation works
- [ ] Successful submission shows confirmation
- [ ] Duplicate submission shows friendly error
- [ ] Invalid share code shows 404 page
- [ ] Inactive survey shows closed message
- [ ] Mobile responsive (no horizontal scroll, readable text)
- [ ] Professor can see response count on dashboard (existing feature)
- [ ] Professor can view full responses via API

---

## Acceptance Criteria
- [ ] All tasks completed
- [ ] All validation commands pass
- [ ] API endpoints return correct responses for all test cases
- [ ] Student page works on mobile viewport (375px)
- [ ] SurveyJS Runner renders all question types correctly
- [ ] Duplicate submission prevention works (409 response)
- [ ] No authentication required for student routes
- [ ] CSS follows dark theme with SurveyJS white container pattern
- [ ] Matches UX design (survey form -> confirmation flow)

## Completion Checklist
- [ ] Code follows discovered patterns (ES modules, Router pattern, API response format)
- [ ] Error handling matches codebase style ({ success: false, error: 'message' })
- [ ] No hardcoded values (share codes, URLs)
- [ ] Student routes do NOT require authentication
- [ ] SurveyJS CSS properly scoped (white background container)
- [ ] Mobile responsive design verified
- [ ] No unnecessary scope additions
- [ ] Self-contained — no questions needed during implementation

## Risks
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| SurveyJS Runner theme conflicts with dark theme | Medium | Low | Wrap in white-background container (proven pattern from QuestionsTab) |
| SQLite concurrent writes (50 students) | Low | Medium | WAL mode already enabled in db.js; better-sqlite3 is synchronous so no race conditions |
| SurveyJS answer data format mismatch | Low | High | SurveyJS `model.data` returns `{ questionName: answer }` which maps directly to our `answers_json` schema |
| Share code collision | Very Low | Low | `randomBytes(4).toString('hex')` = 4 billion possibilities; UNIQUE constraint prevents collision |

## Notes
- The `responses` table and `share_code` column already exist in the schema — no migration needed
- SurveyJS packages `survey-core` and `survey-react-ui` are already installed in the client (`package.json`)
- The `unityQuestionsToSurveyJS()` function in `surveyjs-config.js` handles the conversion from Unity question format to SurveyJS JSON — reuse this for the Runner
- Phase 4 (Export + Unity Integration) will build on the responses stored by this phase — it needs `responses` data + `mappings` to generate `CarData[]`
- The export route (`export.js:16`) has a comment `// Phase 4 will add response->CarData mapping here` — that's the integration point
