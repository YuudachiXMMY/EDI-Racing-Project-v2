const http = require('http');
const crypto = require('crypto');
const { WebSocketServer } = require('ws');

const PORT = parseInt(process.env.PORT || '8080', 10);
const HEARTBEAT_INTERVAL = 30000;
const PROFESSOR_GRACE_PERIOD = 60000; // 60s before room deletion after professor disconnect

// Shared secret used for both the web-app archive call and host-token verification.
const DEFAULT_INTERNAL_SECRET = 'edi-internal-default';
const INTERNAL_SECRET = process.env.INTERNAL_SECRET || DEFAULT_INTERNAL_SECRET;
// When true, create_room requires a valid host token minted by the web-app.
// Default off so the in-game Host flow keeps working until Phase 2 wires the token.
const REQUIRE_HOST_TOKEN = (process.env.REQUIRE_HOST_TOKEN || 'false').toLowerCase() === 'true';

// Boot guard — MUST match web-app/src/hostToken.js checkSecretConfig in lockstep.
// Pure decision function: pass the RAW process.env.INTERNAL_SECRET (not the resolved
// constant above, which already collapsed unset -> default) so "unset" is still flagged.
function checkSecretConfig({ secret, requireHostToken, gameAccessGate = false }) {
  const isDefault = !secret || secret === DEFAULT_INTERNAL_SECRET;
  if (!isDefault) return { level: 'ok', message: '' };
  if (requireHostToken || gameAccessGate) {
    return {
      level: 'fatal',
      message:
        'INTERNAL_SECRET is unset or the public default while an auth boundary that trusts it ' +
        'is active (host-token enforcement or the /game/ access gate). Set a strong random ' +
        'INTERNAL_SECRET (e.g. `openssl rand -hex 32`) before starting. Refusing to start.',
    };
  }
  return {
    level: 'warn',
    message:
      "INTERNAL_SECRET is the public default 'edi-internal-default'. This is acceptable only " +
      'with REQUIRE_HOST_TOKEN=false. Set a strong secret before enabling enforcement.',
  };
}

// Host-token verification — MUST match web-app/src/hostToken.js byte-for-byte.
//   token = base64url(JSON payload) + "." + base64url(HMAC_SHA256(payloadB64, INTERNAL_SECRET))
//   payload = { v:1, sid:<surveyId|null>, iat:<epoch ms>, exp:<epoch ms> }
// If you change the format here, update web-app/src/hostToken.js in lockstep.
function b64url(buf) {
  return buf.toString('base64').replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}
function b64urlDecode(str) {
  const pad = str.length % 4 === 0 ? '' : '='.repeat(4 - (str.length % 4));
  return Buffer.from(str.replace(/-/g, '+').replace(/_/g, '/') + pad, 'base64');
}
function verifyHostToken(token, now = Date.now()) {
  if (typeof token !== 'string' || token.length === 0) {
    return { valid: false, error: 'missing token' };
  }
  const dot = token.indexOf('.');
  if (dot <= 0 || dot === token.length - 1) {
    return { valid: false, error: 'malformed token' };
  }
  const payloadB64 = token.slice(0, dot);
  const sigB64 = token.slice(dot + 1);
  const expected = b64url(crypto.createHmac('sha256', INTERNAL_SECRET).update(payloadB64).digest());
  const a = Buffer.from(sigB64);
  const b = Buffer.from(expected);
  if (a.length !== b.length) {
    return { valid: false, error: 'bad signature' };
  }
  try {
    if (!crypto.timingSafeEqual(a, b)) {
      return { valid: false, error: 'bad signature' };
    }
  } catch {
    return { valid: false, error: 'bad signature' };
  }
  let payload;
  try {
    payload = JSON.parse(b64urlDecode(payloadB64).toString('utf8'));
  } catch {
    return { valid: false, error: 'bad payload' };
  }
  if (!payload || payload.v !== 1) {
    return { valid: false, error: 'unsupported version' };
  }
  if (typeof payload.exp !== 'number' || payload.exp <= now) {
    return { valid: false, error: 'expired' };
  }
  return { valid: true, surveyId: payload.sid ?? null };
}

// Room: { professor: WebSocket|null, students: Set<WebSocket>, webapps: Set<WebSocket>, raceStarted: boolean, latestState: string|null, latestRaceStart: string|null, gamePhase: string, raceResults: string|null, surveyData: string|null, professorSessionId: string|null, graceTimer: NodeJS.Timeout|null }
// latestState holds the most recent position frame (overwritten by every state_update).
// latestRaceStart holds the one-time race_start roster (car list) — cached SEPARATELY so a
// late joiner can be replayed the roster (spawn cars) before latestState (snap to positions).
const rooms = new Map();
const clientRooms = new Map(); // WebSocket -> { roomCode, role, sessionId }
const sessions = new Map();   // sessionId -> { roomCode, role }
// surveyId -> roomCode. Populated on create_room from the host token's `sid` claim so the
// web-app can discover the room it just launched (the WS server owns room-code generation,
// and the prebuilt Unity client never reports the code back to the web-app). Latest room per
// survey wins; the entry is removed in destroyRoom only when it still points at that room.
const surveyRooms = new Map();

function generateRoomCode() {
  const chars = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789'; // no I/O/0/1 to avoid confusion
  let code;
  do {
    code = '';
    for (let i = 0; i < 6; i++) {
      code += chars[Math.floor(Math.random() * chars.length)];
    }
  } while (rooms.has(code));
  return code;
}

function sendJSON(ws, obj) {
  if (ws.readyState === 1) {
    ws.send(JSON.stringify(obj));
  }
}

function broadcastToStudents(roomCode, message) {
  const room = rooms.get(roomCode);
  if (!room) return;
  const data = typeof message === 'string' ? message : JSON.stringify(message);
  for (const student of room.students) {
    if (student.readyState === 1) {
      student.send(data);
    }
  }
}

// Replay the cached race roster to a late-joining client so its Unity/2D view spawns the
// visual cars; the caller then sends latestState to snap them to current positions. Roster
// order matters: the client's HandleStateUpdate is a no-op until race_start has spawned the
// cars, so this MUST be sent before latestState. Personalized per team — yourCarIndex is -1
// for anonymous students and web viewers. No-op before race start or if nothing is cached.
function sendRaceStartTo(ws, room, teamName) {
  if (!room.raceStarted || !room.latestRaceStart || ws.readyState !== 1) return;
  try {
    const startMsg = JSON.parse(room.latestRaceStart);
    const cars = startMsg.cars || [];
    let yourIndex = -1;
    if (teamName) {
      yourIndex = cars.findIndex(c =>
        c.teamName && c.teamName.toLowerCase() === teamName.toLowerCase()
      );
    }
    ws.send(JSON.stringify({ ...startMsg, yourCarIndex: yourIndex }));
  } catch { /* malformed cache — caller still sends latestState */ }
}

const API_URL = process.env.API_URL || 'http://localhost:3001';

// Push the latest race results straight to the web-app so they land in the survey's Results tab
// (race_results table) without waiting for the room to close. Fire-and-forget: a web-app outage
// must never crash the relay. `rawResultsMsg` is the exact `race_results` message JSON from Unity
// ({ configName, resultsJson }); the room-close archive path remains as a fallback.
function postRaceResults(roomCode, rawResultsMsg) {
  let payload = { roomCode, configName: '', rankings: [], eventLog: [], totalRaceTime: 0 };
  try {
    const parsed = JSON.parse(rawResultsMsg);
    payload.configName = parsed.configName || '';
    if (parsed.resultsJson) {
      const results = JSON.parse(parsed.resultsJson);
      payload.rankings = results.Rankings || [];
      payload.eventLog = results.EventLog || [];
      payload.totalRaceTime = results.TotalRaceTime || 0;
    }
  } catch { return; /* malformed message — nothing to archive */ }

  fetch(`${API_URL}/api/internal/race-results`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'x-internal-secret': INTERNAL_SECRET,
    },
    body: JSON.stringify(payload),
  }).catch(() => {});
}

function destroyRoom(roomCode) {
  const room = rooms.get(roomCode);
  if (!room) return;
  if (room.graceTimer) clearTimeout(room.graceTimer);

  // Archive session to web app DB (fire-and-forget)
  const archivePayload = {
    roomCode,
    configName: '',
    studentCount: room.students.size,
    studentNames: [...room.studentTeamNames.values()],
    gamePhase: room.gamePhase || 'Setup',
    raceStarted: room.raceStarted,
    rankings: [],
    eventLog: [],
    totalRaceTime: 0,
    startedAt: room.createdAt || new Date().toISOString(),
  };
  if (room.raceResults) {
    try {
      const parsed = JSON.parse(room.raceResults);
      archivePayload.configName = parsed.configName || '';
      if (parsed.resultsJson) {
        const results = JSON.parse(parsed.resultsJson);
        archivePayload.rankings = results.Rankings || [];
        archivePayload.eventLog = results.EventLog || [];
        archivePayload.totalRaceTime = results.TotalRaceTime || 0;
      }
    } catch { /* ignore parse errors */ }
  }
  fetch(`${API_URL}/api/sessions/archive`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'x-internal-secret': INTERNAL_SECRET,
    },
    body: JSON.stringify(archivePayload),
  }).catch(() => {});

  broadcastToStudents(roomCode, { type: 'room_closed' });
  for (const student of room.students) {
    clientRooms.delete(student);
  }
  // Clean up session references for this room
  for (const [sid, info] of sessions) {
    if (info.roomCode === roomCode) sessions.delete(sid);
  }
  // Drop the survey->room link only if it still points here; a newer re-host may already own it.
  if (room.surveyId !== null && room.surveyId !== undefined && surveyRooms.get(room.surveyId) === roomCode) {
    surveyRooms.delete(room.surveyId);
  }
  rooms.delete(roomCode);
  console.log(`[Room ${roomCode}] Destroyed (archived)`);
}

function cleanupClient(ws) {
  const info = clientRooms.get(ws);
  if (!info) return;
  clientRooms.delete(ws);

  const room = rooms.get(info.roomCode);
  if (!room) return;

  if (info.role === 'professor') {
    // Professor left — suspend room with grace period instead of immediate deletion
    room.professor = null;
    broadcastToStudents(info.roomCode, { type: 'host_reconnecting' });
    console.log(`[Room ${info.roomCode}] Professor disconnected — grace period ${PROFESSOR_GRACE_PERIOD / 1000}s`);

    room.graceTimer = setTimeout(() => {
      room.graceTimer = null;
      if (!room.professor) {
        console.log(`[Room ${info.roomCode}] Grace period expired`);
        destroyRoom(info.roomCode);
      }
    }, PROFESSOR_GRACE_PERIOD);
  } else if (info.role === 'webapp') {
    room.webapps.delete(ws);
    console.log(`[Room ${info.roomCode}] Web-app client disconnected`);
  } else {
    room.students.delete(ws);
    room.studentTeamNames.delete(ws);
    // Notify professor of updated count and list
    if (room.professor && room.professor.readyState === 1) {
      sendJSON(room.professor, { type: 'student_count', count: room.students.size });
      sendJSON(room.professor, {
        type: 'student_list',
        teamNames: [...room.studentTeamNames.values()],
        count: room.students.size,
      });
    }
    console.log(`[Room ${info.roomCode}] Student left (${room.students.size} remaining)`);
  }
}

// --- HTTP server for room-status API ---
const server = http.createServer((req, res) => {
  res.setHeader('Access-Control-Allow-Origin', '*');
  res.setHeader('Access-Control-Allow-Methods', 'GET, POST, OPTIONS');
  res.setHeader('Access-Control-Allow-Headers', 'Content-Type');
  res.setHeader('Content-Type', 'application/json');

  // CORS preflight
  if (req.method === 'OPTIONS') {
    res.writeHead(204);
    res.end();
    return;
  }

  const match = req.url.match(/^\/api\/room-status\/([A-Za-z0-9]+)$/);
  if (req.method === 'GET' && match) {
    const code = match[1].toUpperCase();
    const room = rooms.get(code);
    if (!room) {
      res.writeHead(200);
      res.end(JSON.stringify({ exists: false }));
      return;
    }
    res.writeHead(200);
    res.end(JSON.stringify({
      exists: true,
      roomCode: code,
      studentCount: room.students.size,
      gamePhase: room.gamePhase || 'Setup',
      raceStarted: room.raceStarted,
    }));
    return;
  }

  // GET /api/survey-room/:surveyId — resolve the live room a survey is currently hosting, so the
  // web-app can show the student join link + QR after launching. Returns the code only while the
  // room still exists (stale mappings are cleaned in destroyRoom).
  const surveyRoomMatch = req.url.match(/^\/api\/survey-room\/(\d+)$/);
  if (req.method === 'GET' && surveyRoomMatch) {
    // Internal-only: the web-app proxies this after its own auth + ownership check, so the raw
    // endpoint must not be callable by anything else that can reach the game server (previously it
    // relied on Docker network isolation alone). Require the shared secret — constant-time compared
    // so it cannot be probed byte-by-byte via timing — before disclosing any survey→room mapping.
    const provided = Buffer.from(req.headers['x-internal-secret'] || '');
    const expected = Buffer.from(INTERNAL_SECRET);
    if (provided.length !== expected.length || !crypto.timingSafeEqual(provided, expected)) {
      res.writeHead(403);
      res.end(JSON.stringify({ error: 'Forbidden' }));
      return;
    }
    const surveyId = parseInt(surveyRoomMatch[1], 10);
    const code = surveyRooms.get(surveyId);
    if (!code || !rooms.has(code)) {
      res.writeHead(200);
      res.end(JSON.stringify({ exists: false }));
      return;
    }
    res.writeHead(200);
    res.end(JSON.stringify({ exists: true, roomCode: code }));
    return;
  }

  const resultsMatch = req.url.match(/^\/api\/room-results\/([A-Za-z0-9]+)$/);
  if (req.method === 'GET' && resultsMatch) {
    const code = resultsMatch[1].toUpperCase();
    const room = rooms.get(code);
    if (!room || !room.raceResults) {
      res.writeHead(200);
      res.end(JSON.stringify({ exists: false }));
      return;
    }
    res.writeHead(200);
    res.end(room.raceResults);
    return;
  }

  // POST /api/notify-response — notify room when a new web survey response is submitted
  if (req.method === 'POST' && req.url === '/api/notify-response') {
    let body = '';
    req.on('data', chunk => { body += chunk; });
    req.on('end', () => {
      try {
        const { roomCode, responseCount, teamName, surveyId } = JSON.parse(body);
        const code = (roomCode || '').toUpperCase();
        const room = rooms.get(code);
        const notification = {
          type: 'new_web_response',
          responseCount: responseCount || 0,
          teamName: teamName || '',
          surveyId: surveyId || 0,
        };
        if (room && room.professor && room.professor.readyState === 1) {
          sendJSON(room.professor, notification);
        }
        if (room) {
          for (const webapp of room.webapps) {
            if (webapp.readyState === 1) sendJSON(webapp, notification);
          }
        }
        res.writeHead(200);
        res.end(JSON.stringify({ success: true }));
        console.log(`[Notify] Response for room ${code}: ${responseCount} total (${teamName})`);
      } catch {
        res.writeHead(400);
        res.end(JSON.stringify({ success: false, error: 'Invalid JSON' }));
      }
    });
    return;
  }

  res.writeHead(404);
  res.end(JSON.stringify({ error: 'Not found' }));
});

// --- WebSocket server attached to HTTP server ---
const wss = new WebSocketServer({ server });

// Heartbeat to detect dead connections
const heartbeat = setInterval(() => {
  for (const ws of wss.clients) {
    if (ws.isAlive === false) {
      cleanupClient(ws);
      ws.terminate();
      continue;
    }
    ws.isAlive = false;
    ws.ping();
  }
}, HEARTBEAT_INTERVAL);

wss.on('close', () => clearInterval(heartbeat));

wss.on('connection', (ws) => {
  ws.isAlive = true;
  ws.on('pong', () => { ws.isAlive = true; });

  ws.on('message', (data) => {
    let msg;
    try {
      msg = JSON.parse(data.toString());
    } catch {
      return;
    }

    switch (msg.type) {
      case 'create_room': {
        // Decode the host token even when enforcement is off: its `sid` claim is how the
        // web-app links this room back to the survey it launched from (survey-room lookup).
        const tokenResult = verifyHostToken(msg.hostToken);
        if (REQUIRE_HOST_TOKEN && !tokenResult.valid) {
          sendJSON(ws, { type: 'error', message: 'Host authorization required' });
          console.log(`[Auth] Rejected create_room (${tokenResult.error || 'no token'})`);
          return;
        }
        const surveyId = tokenResult.valid ? tokenResult.surveyId ?? null : null;
        const roomCode = generateRoomCode();
        rooms.set(roomCode, {
          professor: ws,
          students: new Set(),
          webapps: new Set(),
          studentTeamNames: new Map(),
          raceStarted: false,
          latestState: null,
          latestRaceStart: null,
          gamePhase: 'Setup',
          raceResults: null,
          latestLeaderboard: null,
          surveyData: null,
          latestConfig: null,
          professorSessionId: msg.sessionId || null,
          surveyId,
          graceTimer: null,
          createdAt: new Date().toISOString(),
        });
        // Latest room per survey wins, so a re-host of the same survey supersedes the old code.
        if (surveyId !== null) surveyRooms.set(surveyId, roomCode);
        const clientInfo = { roomCode, role: 'professor', sessionId: msg.sessionId || null };
        clientRooms.set(ws, clientInfo);
        if (msg.sessionId) {
          sessions.set(msg.sessionId, { roomCode, role: 'professor' });
        }
        sendJSON(ws, { type: 'room_created', roomCode });
        console.log(`[Room ${roomCode}] Created${msg.sessionId ? ` (session: ${msg.sessionId})` : ''}`);
        break;
      }

      case 'join_room': {
        const code = (msg.roomCode || '').toUpperCase();
        const room = rooms.get(code);
        if (!room) {
          sendJSON(ws, { type: 'error', message: 'Room not found' });
          return;
        }
        room.students.add(ws);
        const teamName = (msg.teamName || '').trim();
        const clientInfo = { roomCode: code, role: 'student', sessionId: msg.sessionId || null, teamName };
        clientRooms.set(ws, clientInfo);
        if (teamName) room.studentTeamNames.set(ws, teamName);
        if (msg.sessionId) {
          sessions.set(msg.sessionId, { roomCode: code, role: 'student', teamName });
        }
        sendJSON(ws, { type: 'room_joined', roomCode: code });
        // Notify professor with identity
        if (room.professor && room.professor.readyState === 1) {
          sendJSON(room.professor, { type: 'student_count', count: room.students.size });
          sendJSON(room.professor, {
            type: 'student_joined',
            teamName: teamName || '(anonymous)',
            count: room.students.size,
          });
          sendJSON(room.professor, {
            type: 'student_list',
            teamNames: [...room.studentTeamNames.values()],
            count: room.students.size,
          });
        }
        // Send cached survey to late-joiner (if survey distributed but race not started)
        if (room.surveyData && !room.raceStarted) {
          ws.send(room.surveyData);
        }
        // If race already started, replay the roster (spawn cars) BEFORE the latest positions.
        sendRaceStartTo(ws, room, teamName);
        // If race already started, send latest state to late-joiner
        if (room.latestState) {
          ws.send(room.latestState);
        }
        console.log(`[Room ${code}] Student '${teamName || 'anonymous'}' joined (${room.students.size} total)`);
        break;
      }

      case 'rejoin_room': {
        const code = (msg.roomCode || '').toUpperCase();
        const sid = msg.sessionId || '';
        const room = rooms.get(code);

        if (!room || !sid) {
          sendJSON(ws, { type: 'error', message: 'Room not found or invalid session' });
          return;
        }

        const sessionInfo = sessions.get(sid);
        if (!sessionInfo || sessionInfo.roomCode !== code) {
          sendJSON(ws, { type: 'error', message: 'Session not recognized for this room' });
          return;
        }

        if (sessionInfo.role === 'professor') {
          if (room.graceTimer) {
            clearTimeout(room.graceTimer);
            room.graceTimer = null;
          }
          room.professor = ws;
          clientRooms.set(ws, { roomCode: code, role: 'professor', sessionId: sid });
          broadcastToStudents(code, { type: 'host_reconnected' });
          sendJSON(ws, {
            type: 'reconnect_state',
            gamePhase: room.gamePhase || 'Setup',
            studentCount: room.students.size,
            raceStarted: room.raceStarted,
          });
          if (room.latestState) {
            ws.send(room.latestState);
          }
          console.log(`[Room ${code}] Professor reconnected (session: ${sid})`);
        } else {
          room.students.add(ws);
          const teamName = (msg.teamName || (sessionInfo && sessionInfo.teamName) || '').trim();
          clientRooms.set(ws, { roomCode: code, role: 'student', sessionId: sid, teamName });
          if (teamName) room.studentTeamNames.set(ws, teamName);
          sendJSON(ws, {
            type: 'reconnect_state',
            gamePhase: room.gamePhase || 'Setup',
            studentCount: room.students.size,
            raceStarted: room.raceStarted,
          });
          if (room.surveyData && !room.raceStarted) {
            ws.send(room.surveyData);
          }
          // Replay the roster (spawn cars) before positions, so a student who reconnected
          // after the race started re-spawns its cars instead of dropping state_updates.
          sendRaceStartTo(ws, room, teamName);
          if (room.latestState) {
            ws.send(room.latestState);
          }
          if (room.professor && room.professor.readyState === 1) {
            sendJSON(room.professor, { type: 'student_count', count: room.students.size });
          }
          console.log(`[Room ${code}] Student reconnected (${room.students.size} total)`);
        }
        break;
      }

      case 'web_join_room': {
        const webCode = (msg.roomCode || '').toUpperCase();
        const webRoom = rooms.get(webCode);
        if (!webRoom) {
          sendJSON(ws, { type: 'error', message: 'Room not found' });
          return;
        }
        webRoom.webapps.add(ws);
        clientRooms.set(ws, { roomCode: webCode, role: 'webapp' });
        sendJSON(ws, { type: 'room_joined', roomCode: webCode });
        // Send cached state to late-joining web viewer. Roster first (unpersonalized — web
        // viewers have no team, so yourCarIndex is -1) so the 2D minimap knows the cars.
        sendRaceStartTo(ws, webRoom, '');
        if (webRoom.latestState) ws.send(webRoom.latestState);
        if (webRoom.latestLeaderboard) ws.send(webRoom.latestLeaderboard);
        if (webRoom.latestConfig) ws.send(webRoom.latestConfig);
        console.log(`[Room ${webCode}] Web-app client joined`);
        break;
      }

      case 'config_export': {
        const profInfo = clientRooms.get(ws);
        if (!profInfo || profInfo.role !== 'professor') {
          sendJSON(ws, { type: 'config_sync_ack', success: false, error: 'Not authorized', direction: 'export' });
          return;
        }
        const configRoom = rooms.get(profInfo.roomCode);
        if (!configRoom) {
          sendJSON(ws, { type: 'config_sync_ack', success: false, error: 'Room not found', direction: 'export' });
          return;
        }
        configRoom.latestConfig = data.toString();
        for (const webapp of configRoom.webapps) {
          if (webapp.readyState === 1) webapp.send(data.toString());
        }
        sendJSON(ws, { type: 'config_sync_ack', success: true, direction: 'export' });
        console.log(`[Room ${profInfo.roomCode}] Config exported from Unity: ${msg.configName || '(unnamed)'}`);
        break;
      }

      case 'config_import': {
        const ciWebInfo = clientRooms.get(ws);
        if (!ciWebInfo || ciWebInfo.role !== 'webapp') {
          sendJSON(ws, { type: 'config_sync_ack', success: false, error: 'Not authorized', direction: 'import' });
          return;
        }
        const ciRoom = rooms.get(ciWebInfo.roomCode);
        if (!ciRoom || !ciRoom.professor || ciRoom.professor.readyState !== 1) {
          sendJSON(ws, { type: 'config_sync_ack', success: false, error: 'Professor not connected', direction: 'import' });
          return;
        }
        ciRoom.professor.send(data.toString());
        sendJSON(ws, { type: 'config_sync_ack', success: true, direction: 'import' });
        console.log(`[Room ${ciWebInfo.roomCode}] Config imported from web-app: ${msg.configName || '(unnamed)'}`);
        break;
      }

      case 'survey_import': {
        const webInfo = clientRooms.get(ws);
        if (!webInfo || webInfo.role !== 'webapp') {
          sendJSON(ws, { type: 'error', message: 'Not authorized' });
          return;
        }
        const importRoom = rooms.get(webInfo.roomCode);
        if (!importRoom || !importRoom.professor || importRoom.professor.readyState !== 1) {
          sendJSON(ws, { type: 'survey_import_ack', success: false, error: 'Professor not connected' });
          return;
        }
        importRoom.professor.send(data.toString());
        sendJSON(ws, { type: 'survey_import_ack', success: true });
        console.log(`[Room ${webInfo.roomCode}] Survey data sent from web-app to professor`);
        break;
      }

      default: {
        const info = clientRooms.get(ws);
        if (!info) return;
        const room = rooms.get(info.roomCode);
        if (!room) return;

        const raw = data.toString();

        if (info.role === 'professor') {
          // Professor → Students relay
          if (msg.type === 'race_start') {
            room.raceStarted = true;
            room.gamePhase = 'Racing';
            room.latestState = raw;
            // Cache the roster separately so late joiners can be replayed the car list even
            // after the first state_update overwrites latestState (~100ms later). Store the
            // RAW host message (no yourCarIndex) — personalization is per-recipient.
            room.latestRaceStart = raw;

            // Send personalized race_start to each student with yourCarIndex
            const cars = msg.cars || [];
            for (const student of room.students) {
              if (student.readyState !== 1) continue;
              const studentInfo = clientRooms.get(student);
              const studentTeam = (studentInfo && studentInfo.teamName) || '';
              let yourIndex = -1;
              if (studentTeam) {
                yourIndex = cars.findIndex(c =>
                  c.teamName && c.teamName.toLowerCase() === studentTeam.toLowerCase()
                );
              }
              const personalizedMsg = { ...msg, yourCarIndex: yourIndex };
              student.send(JSON.stringify(personalizedMsg));
            }

            // Relay to web-app viewers (no personalization)
            for (const webapp of room.webapps) {
              if (webapp.readyState === 1) webapp.send(raw);
            }
            // Skip normal broadcastToStudents — already sent individually
            break;
          } else if (msg.type === 'state_update') {
            room.latestState = raw;
          } else if (msg.type === 'leaderboard') {
            room.latestLeaderboard = raw;
          } else if (msg.type === 'survey_questions') {
            room.surveyData = raw;
          } else if (msg.type === 'game_state') {
            room.gamePhase = msg.state || 'Setup';
          } else if (msg.type === 'race_results') {
            room.raceResults = raw;
            room.gamePhase = 'Finished';
            // Deliver to the survey's Results tab immediately (not only on room close).
            postRaceResults(info.roomCode, raw);
          } else if (msg.type === 'race_end') {
            room.gamePhase = 'Finished';
          }

          broadcastToStudents(info.roomCode, raw);

          // Relay game messages to web-app live viewers
          const WEBAPP_RELAY_TYPES = ['state_update', 'leaderboard', 'game_state',
            'event_triggered', 'race_start', 'race_end', 'race_results'];
          if (WEBAPP_RELAY_TYPES.includes(msg.type)) {
            for (const webapp of room.webapps) {
              if (webapp.readyState === 1) webapp.send(raw);
            }
          }
        } else if (info.role === 'student') {
          // Student → Professor relay
          if (room.professor && room.professor.readyState === 1) {
            room.professor.send(raw);
          }
        }
        break;
      }
    }
  });

  ws.on('close', () => cleanupClient(ws));
  ws.on('error', () => cleanupClient(ws));
});

// Fail fast (or warn) on a misconfigured host-token secret before binding the port.
const secretCheck = checkSecretConfig({
  secret: process.env.INTERNAL_SECRET,
  requireHostToken: REQUIRE_HOST_TOKEN,
});
if (secretCheck.level === 'fatal') {
  console.error(`[Auth] FATAL: ${secretCheck.message}`);
  process.exit(1);
}
if (secretCheck.level === 'warn') {
  console.warn(`[Auth] WARNING: ${secretCheck.message}`);
}

server.listen(PORT, () => {
  console.log(`WebSocket + HTTP server listening on port ${PORT}`);
});
