# Implementation Plan: Code Review Fixes

## Summary
Fix all MEDIUM and LOW issues identified in the code review of commits `3d8803c..8f23ac4` (GAP 4: Bi-directional Config Sync + GAP 6: Session History).

## Task Type
- [x] Backend
- [x] Frontend
- [x] Fullstack

## Technical Solution

6 targeted fixes across 4 files. No architectural changes — all are surgical edits to existing code.

---

## Implementation Steps

### Step 1: Fix `escapeCsv` to handle newlines
**File**: `web-app/client/src/pages/HistoryPage.jsx:171-175`
**Severity**: MEDIUM
**Change**: Add `\n` and `\r` to the quoting condition.

```javascript
// BEFORE
function escapeCsv(value) {
  if (!value) return '';
  const str = String(value);
  if (str.includes(',') || str.includes('"')) return '"' + str.replace(/"/g, '""') + '"';
  return str;
}

// AFTER
function escapeCsv(value) {
  if (!value) return '';
  const str = String(value);
  if (str.includes(',') || str.includes('"') || str.includes('\n') || str.includes('\r'))
    return '"' + str.replace(/"/g, '""') + '"';
  return str;
}
```

---

### Step 2: Track real room creation time for `started_at`
**Files**: `Server/server.js` (2 changes), `web-app/src/routes/results.js` (1 change)
**Severity**: MEDIUM

**2a. Add `createdAt` to room object** — `Server/server.js:250-264`

```javascript
// In case 'create_room', add to room init object:
createdAt: new Date().toISOString(),
```

**2b. Include `startedAt` in archive payload** — `Server/server.js:50-60`

```javascript
// In destroyRoom(), archivePayload:
startedAt: room.createdAt || new Date().toISOString(),
```

**2c. Use provided `startedAt` in INSERT** — `web-app/src/routes/results.js:78-95`

```javascript
// Change INSERT to include started_at:
const result = db.prepare(
  `INSERT INTO game_sessions
   (user_id, survey_id, room_code, config_name, student_count, student_names_json,
    game_phase, race_started, rankings_json, event_log_json, total_race_time, started_at)
   VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`
).run(
  linked ? linked.user_id : null,
  linked ? linked.id : null,
  roomCode,
  configName || '',
  studentCount || 0,
  JSON.stringify(studentNames || []),
  gamePhase || 'Setup',
  raceStarted ? 1 : 0,
  JSON.stringify(rankings || []),
  JSON.stringify(eventLog || []),
  totalRaceTime || 0,
  req.body.startedAt || new Date().toISOString()
);
```

---

### Step 3: Add simple shared-secret auth to `/api/sessions/archive`
**Files**: `Server/server.js` (1 change), `web-app/src/routes/results.js` (1 change)
**Severity**: MEDIUM

**3a. Send secret header from WS server** — `Server/server.js:73-77`

```javascript
const INTERNAL_SECRET = process.env.INTERNAL_SECRET || 'edi-internal-default';

fetch(`${API_URL}/api/sessions/archive`, {
  method: 'POST',
  headers: {
    'Content-Type': 'application/json',
    'x-internal-secret': INTERNAL_SECRET,
  },
  body: JSON.stringify(archivePayload),
}).catch(() => {});
```

**3b. Validate secret in endpoint** — `web-app/src/routes/results.js:63`

```javascript
const INTERNAL_SECRET = process.env.INTERNAL_SECRET || 'edi-internal-default';

router.post('/sessions/archive', (req, res) => {
  if (req.headers['x-internal-secret'] !== INTERNAL_SECRET) {
    return res.status(403).json({ success: false, error: 'Forbidden' });
  }
  // ... rest unchanged
});
```

---

### Step 4: Add try-catch for JSON.parse in WS message handlers
**File**: `web-app/src/routes/export.js:172-192, 287-315`
**Severity**: LOW

Wrap the `JSON.parse(data.toString())` calls inside `ws.on('message')` in both `send-to-game` and `send-config-to-game`:

```javascript
ws.on('message', (data) => {
  if (responded) return;
  let msg;
  try {
    msg = JSON.parse(data.toString());
  } catch {
    return; // Ignore malformed messages, timeout will fire
  }
  // ... rest unchanged, use msg instead of inline parse
});
```

---

### Step 5: Show error state in HistoryPage on load failure
**File**: `web-app/client/src/pages/HistoryPage.jsx:5-18`
**Severity**: LOW

```javascript
// Add error state
const [error, setError] = useState(null);

async function loadSessions() {
  setLoading(true);
  setError(null);
  const result = await getSessionHistory();
  if (result.success) {
    setSessions(result.data);
  } else {
    setError(result.error || 'Failed to load session history');
  }
  setLoading(false);
}

// In JSX, after loading check:
// {error && <p className="error">{error}</p>}
```

---

### Step 6: Commit untracked plan/report files
**Files**: 4 untracked files in `.claude/PRPs/`
**Severity**: LOW (documentation)

Stage and commit the plan and report files to preserve development decision records:
- `.claude/PRPs/plans/completed/bidirectional-config-sync.plan.md`
- `.claude/PRPs/plans/completed/multi-room-session-history.plan.md`
- `.claude/PRPs/reports/bidirectional-config-sync-report.md`
- `.claude/PRPs/reports/multi-room-session-history-report.md`

---

## Key Files

| File | Operation | Description |
|------|-----------|-------------|
| `web-app/client/src/pages/HistoryPage.jsx:171-175` | Modify | Fix escapeCsv newline handling |
| `web-app/client/src/pages/HistoryPage.jsx:5-18` | Modify | Add error state for load failure |
| `Server/server.js:250-264` | Modify | Add createdAt to room object |
| `Server/server.js:50-77` | Modify | Pass startedAt + internal secret in archive |
| `web-app/src/routes/results.js:63-97` | Modify | Add secret validation + startedAt param |
| `web-app/src/routes/export.js:172-192` | Modify | Add try-catch for JSON.parse in send-to-game |
| `web-app/src/routes/export.js:287-315` | Modify | Add try-catch for JSON.parse in send-config-to-game |

## NOT Fixing (Out of Scope)

- **WS connection pattern deduplication** (`export.js`) — Refactoring, not a bug. Extract shared helper in a separate PR.
- **Adding automated tests** — Large scope, warrants its own plan/PR.

## Risks and Mitigation

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| INTERNAL_SECRET env var not set in Docker | Medium | Low | Default value ensures backward compatibility. Document in docker-compose.yml |
| startedAt parsing in SQLite | Low | Low | ISO 8601 string, same format as datetime('now') |

## Estimated Impact

- 4 files modified
- ~30 lines changed total
- Zero breaking changes
- All fixes are additive or defensive

## SESSION_ID
- CODEX_SESSION: N/A (direct planning — fixes are well-defined from code review)
- GEMINI_SESSION: N/A
